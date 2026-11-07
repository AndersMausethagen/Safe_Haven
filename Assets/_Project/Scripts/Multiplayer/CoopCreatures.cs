using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Raid creatures in co-op: the host's game runs them, friends on the same run see the host's.
/// <list type="bullet">
/// <item>Host: each creature gets a number. Friends on the run hear of those near them (<c>SafeHaven.CreatureSpawn</c>,
/// by prefab), then ten times a second where they are, their state and health (<c>CreaturePoses</c>, unreliable), and
/// every strike, with whom it is at (<c>CreatureStrike</c>), and every death (<c>CreatureDied</c>). A friend's body is a
/// target here (<see cref="FriendHitbox"/>); hits friends land are applied here (<c>CreatureHit</c>) and turn the creature
/// on them.</item>
/// <item>Friend: their game's own creatures are removed and roaming spawns stop (<see cref="FromHost"/>); the host's are
/// built as puppets (<see cref="MonsterAI.MakeRemote"/>). A strike plays out against the friend where they really are. A
/// puppet dies when the host says so and then drops this player's own loot. If the host leaves the run first, the puppets
/// become this game's own creatures.</item>
/// </list>
/// Plain C#: <see cref="CoopSession"/> ticks it and owns the connection.
/// </summary>
public sealed class CoopCreatures : IDisposable
{
    private const string SpawnMessage = "SafeHaven.CreatureSpawn";
    private const string PosesMessage = "SafeHaven.CreaturePoses";
    private const string StrikeMessage = "SafeHaven.CreatureStrike";
    private const string DiedMessage = "SafeHaven.CreatureDied";
    private const string HitMessage = "SafeHaven.CreatureHit";

    /// <summary>Seconds between pose updates.</summary>
    public const float Interval = 0.1f;

    /// <summary>Metres around a friend within which they hear of creatures.</summary>
    private const float Reach = 90f;

    /// <summary>Poses per unreliable message, under the transport's single-packet limit.</summary>
    private const int PosesPerMessage = 36;

    /// <summary>Seconds a friend waits, once the host has left the run, before the creatures become their own.</summary>
    private const float HostGoneSeconds = 2f;

    private const ulong Nobody = ulong.MaxValue;

    private NetworkManager network;

    // Host
    private readonly Dictionary<MonsterAI, int> ids = new Dictionary<MonsterAI, int>();
    private readonly Dictionary<int, MonsterAI> byId = new Dictionary<int, MonsterAI>();
    private readonly Dictionary<ulong, HashSet<int>> told = new Dictionary<ulong, HashSet<int>>();
    private readonly HashSet<int> hitByHost = new HashSet<int>();
    private int nextId;

    // Friend
    private readonly Dictionary<int, MonsterAI> puppets = new Dictionary<int, MonsterAI>();
    private readonly HashSet<int> hitByMe = new HashSet<int>();
    private bool following;
    private float hostGoneSince = -1f;

    private readonly List<MonsterAI> scratch = new List<MonsterAI>();
    private readonly List<ulong> one = new List<ulong>(1);
    private Scene scene;

    /// <summary>True in a friend's game on the host's run: the host's game sends the creatures, none are made here.</summary>
    public static bool FromHost { get; private set; }

    public CoopCreatures()
    {
        Damageable.AnyDied += OnAnyDied;
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    public void Dispose()
    {
        Damageable.AnyDied -= OnAnyDied;
        Damageable.AnyDamaged -= OnAnyDamaged;
        FromHost = false;
    }

    public void StartHosting(NetworkManager manager)
    {
        network = manager;
        Reset();
        network.CustomMessagingManager.RegisterNamedMessageHandler(HitMessage, OnHit);
    }

    public void StartGuest(NetworkManager manager)
    {
        network = manager;
        Reset();
        network.CustomMessagingManager.RegisterNamedMessageHandler(SpawnMessage, OnSpawn);
        network.CustomMessagingManager.RegisterNamedMessageHandler(PosesMessage, OnPoses);
        network.CustomMessagingManager.RegisterNamedMessageHandler(StrikeMessage, OnStrike);
        network.CustomMessagingManager.RegisterNamedMessageHandler(DiedMessage, OnDied);
    }

    public void Reset()
    {
        // Mid-raid, puppets left behind become this game's own creatures rather than standing still.
        HandBack(quiet: true);
        ids.Clear();
        byId.Clear();
        told.Clear();
        hitByHost.Clear();
        puppets.Clear();
        hitByMe.Clear();
        following = false;
        hostGoneSince = -1f;
        FromHost = false;
    }

    /// <summary>This friend is going into the host's run: from the raid's start, the host's game sends its creatures.</summary>
    public void FollowHost() => following = true;

    /// <summary>
    /// The place a player is in, as friends' avatars report it: the scene, and in a raid its run (see
    /// <see cref="NetworkPlayerAvatar"/>).
    /// </summary>
    public static string PlaceHere()
    {
        string place = SceneManager.GetActiveScene().name;
        return GameSession.ActiveRaid != null ? place + "#" + GameSession.ActiveRaid.Seed : place;
    }

    /// <summary>Ten times a second while in a session.</summary>
    public void Tick(bool isHost)
    {
        if (network == null)
        {
            return;
        }

        Scene now = SceneManager.GetActiveScene();
        if (now != scene)
        {
            // A new place: what was known belonged to the last one.
            scene = now;
            ids.Clear();
            byId.Clear();
            told.Clear();
            hitByHost.Clear();
            puppets.Clear();
            hitByMe.Clear();
            hostGoneSince = -1f;
        }

        if (isHost)
        {
            HostTick();
        }
        else
        {
            GuestTick();
        }
    }

    // ---- Host

    private void HostTick()
    {
        if (GameSession.ActiveRaid == null)
        {
            return;
        }

        foreach (MonsterAI creature in MonsterAI.All)
        {
            if (creature != null && !ids.ContainsKey(creature) && creature.gameObject.scene == scene)
            {
                int id = ++nextId;
                ids[creature] = id;
                byId[id] = creature;
                MonsterAI watched = creature;
                creature.AttackStarted += () => OnStrikeBegun(watched);
            }
        }

        string place = PlaceHere();
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend == null || friend.OwnerClientId == NetworkManager.ServerClientId || !friend.IsVisible
                || friend.Look.Place.ToString() != place)
            {
                continue;
            }

            SendTo(friend.OwnerClientId, friend.Body.transform.position);
        }
    }

    private void SendTo(ulong clientId, Vector3 at)
    {
        if (!told.TryGetValue(clientId, out HashSet<int> known))
        {
            told[clientId] = known = new HashSet<int>();
        }

        // New creatures within reach: reliable, with what to build them from.
        scratch.Clear();
        foreach (KeyValuePair<int, MonsterAI> pair in byId)
        {
            MonsterAI creature = pair.Value;
            if (creature != null && !known.Contains(pair.Key) && creature.CurrentState != MonsterAI.State.Dead
                && (creature.transform.position - at).sqrMagnitude <= Reach * Reach
                && creature.TryGetComponent(out SaveableEntity saveable) && !string.IsNullOrEmpty(saveable.PrefabId))
            {
                scratch.Add(creature);
            }
        }

        if (scratch.Count > 0)
        {
            using var spawns = new FastBufferWriter(64 + scratch.Count * 96, Allocator.Temp);
            spawns.WriteValueSafe(scratch.Count);
            foreach (MonsterAI creature in scratch)
            {
                int id = ids[creature];
                known.Add(id);
                spawns.WriteValueSafe(id);
                spawns.WriteValueSafe(creature.GetComponent<SaveableEntity>().PrefabId);
                spawns.WriteValueSafe(creature.transform.position);
                spawns.WriteValueSafe(creature.transform.eulerAngles.y);
                spawns.WriteValueSafe(HealthByte(creature));
            }

            network.CustomMessagingManager.SendNamedMessage(SpawnMessage, clientId, spawns, NetworkDelivery.ReliableFragmentedSequenced);
        }

        // Where the known ones are now: unreliable, in packet-sized parts.
        scratch.Clear();
        foreach (int id in known)
        {
            if (byId.TryGetValue(id, out MonsterAI creature) && creature != null && creature.CurrentState != MonsterAI.State.Dead
                && (creature.transform.position - at).sqrMagnitude <= Reach * Reach * 1.5f)
            {
                scratch.Add(creature);
            }
        }

        for (int from = 0; from < scratch.Count; from += PosesPerMessage)
        {
            int count = Mathf.Min(PosesPerMessage, scratch.Count - from);
            using var poses = new FastBufferWriter(8 + count * 32, Allocator.Temp);
            poses.WriteValueSafe(count);
            for (int i = from; i < from + count; i++)
            {
                MonsterAI creature = scratch[i];
                poses.WriteValueSafe(ids[creature]);
                poses.WriteValueSafe(creature.transform.position);
                poses.WriteValueSafe(creature.transform.eulerAngles.y);
                poses.WriteValueSafe(creature.Speed);
                poses.WriteValueSafe((byte)creature.CurrentState);
                poses.WriteValueSafe(HealthByte(creature));
            }

            network.CustomMessagingManager.SendNamedMessage(PosesMessage, clientId, poses, NetworkDelivery.UnreliableSequenced);
        }
    }

    private static byte HealthByte(MonsterAI creature) =>
        creature.TryGetComponent(out Damageable health) ? (byte)Mathf.Clamp(Mathf.CeilToInt(health.CurrentHealth / health.MaxHealth * 255f), 0, 255) : (byte)255;

    // A strike begun here: friends who know the creature play it, at whoever it is aimed.
    private void OnStrikeBegun(MonsterAI creature)
    {
        if (network == null || !network.IsServer || creature == null || !ids.TryGetValue(creature, out int id))
        {
            return;
        }

        ulong at = creature.Target is FriendHitbox friend ? friend.ClientId
            : creature.Target is Component body && Player.IsPlayer(body.gameObject) ? NetworkManager.ServerClientId
            : Nobody;
        foreach (KeyValuePair<ulong, HashSet<int>> pair in told)
        {
            if (!pair.Value.Contains(id))
            {
                continue;
            }

            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(creature.LastAction);
            writer.WriteValueSafe(at);
            network.CustomMessagingManager.SendNamedMessage(StrikeMessage, pair.Key, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    private void OnAnyDied(Damageable victim, DamageInfo blow)
    {
        if (network == null || !network.IsServer || victim == null || !victim.TryGetComponent(out MonsterAI creature)
            || !ids.TryGetValue(creature, out int id))
        {
            return;
        }

        foreach (KeyValuePair<ulong, HashSet<int>> pair in told)
        {
            if (pair.Value.Remove(id))
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(id);
                network.CustomMessagingManager.SendNamedMessage(DiedMessage, pair.Key, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }

    // A friend's blow on one of the host's creatures: applied here, and the creature turns on them.
    private void OnHit(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        reader.ReadValueSafe(out float amount);
        reader.ReadValueSafe(out byte type);
        reader.ReadValueSafe(out bool critical);
        reader.ReadValueSafe(out Vector3 point);
        reader.ReadValueSafe(out Vector3 direction);
        reader.ReadValueSafe(out float poise);
        reader.ReadValueSafe(out int technique);
        if (!byId.TryGetValue(id, out MonsterAI creature) || creature == null || !creature.TryGetComponent(out Damageable health) || !health.IsAlive)
        {
            return;
        }

        // Struck by the host before: should this blow kill it, the kill and its loot count for the host too.
        Combatant host = hitByHost.Contains(id) && Player.Current != null ? Player.Current.GetComponent<Combatant>() : null;
        health.ApplyDamage(new DamageInfo(host, null, amount, (DamageType)type, critical, point, direction, poise, (HitTechnique)technique));
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && friend.OwnerClientId == sender && friend.Body != null && friend.Body.TryGetComponent(out FriendHitbox hitbox))
            {
                creature.ProvokedBy(hitbox);
            }
        }
    }

    // ---- Friend

    private void GuestTick()
    {
        FromHost = following && GameSession.ActiveRaid != null;
        if (GameSession.ActiveRaid == null)
        {
            following = false;
            return;
        }

        if (!FromHost)
        {
            return;
        }

        // This game's own creatures make way for the host's.
        scratch.Clear();
        foreach (MonsterAI creature in MonsterAI.All)
        {
            if (creature != null && !creature.IsRemote && creature.gameObject.scene == scene)
            {
                scratch.Add(creature);
            }
        }

        foreach (MonsterAI creature in scratch)
        {
            UnityEngine.Object.Destroy(creature.gameObject);
        }

        // The host left the run: the creatures here are this game's from now on.
        bool hostHere = false;
        string place = PlaceHere();
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            hostHere |= friend != null && friend.OwnerClientId == NetworkManager.ServerClientId && friend.Look.Place.ToString() == place;
        }

        if (hostHere)
        {
            hostGoneSince = -1f;
            return;
        }

        if (hostGoneSince < 0f)
        {
            hostGoneSince = Time.unscaledTime;
        }
        else if (Time.unscaledTime - hostGoneSince >= HostGoneSeconds)
        {
            TakeOver();
        }
    }

    private void TakeOver() => HandBack(quiet: false);

    private void HandBack(bool quiet)
    {
        bool hadAny = puppets.Count > 0;
        foreach (MonsterAI puppet in puppets.Values)
        {
            if (puppet != null)
            {
                puppet.MakeLocal();
                if (puppet.TryGetComponent(out Damageable health))
                {
                    health.HealthFloor = 0f;
                }
            }
        }

        puppets.Clear();
        following = false;
        FromHost = false;
        if (hadAny && !quiet)
        {
            GameMessages.Post($"{HavenVisit.HostName} has left the raid. You're on your own out here.", MessageKind.Warning);
        }
    }

    private void OnSpawn(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out string prefabId);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out float yaw);
            reader.ReadValueSafe(out byte health);
            if (!FromHost || puppets.ContainsKey(id))
            {
                continue;
            }

            SaveableEntity prefab = content != null ? content.FindPrefab(prefabId) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Co-op: no creature prefab '{prefabId}' to show the host's creature {id}.");
                continue;
            }

            GameObject copy = UnityEngine.Object.Instantiate(prefab.gameObject, position, Quaternion.Euler(0f, yaw, 0f));
            if (!copy.TryGetComponent(out MonsterAI puppet))
            {
                UnityEngine.Object.Destroy(copy);
                continue;
            }

            puppet.MakeRemote();
            if (copy.TryGetComponent(out Damageable damageable))
            {
                damageable.HealthFloor = 1f;
                damageable.ShowShare(health / 255f);
            }

            puppets[id] = puppet;
        }
    }

    private void OnPoses(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out float yaw);
            reader.ReadValueSafe(out float speed);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte health);
            if (puppets.TryGetValue(id, out MonsterAI puppet) && puppet != null)
            {
                puppet.ApplyRemotePose(position, yaw, speed, (MonsterAI.State)state);
                if (puppet.TryGetComponent(out Damageable damageable))
                {
                    damageable.ShowShare(health / 255f);
                }
            }
        }
    }

    private void OnStrike(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        reader.ReadValueSafe(out int action);
        reader.ReadValueSafe(out ulong at);
        if (puppets.TryGetValue(id, out MonsterAI puppet) && puppet != null)
        {
            IDamageable me = at == network.LocalClientId && Player.Current != null ? Player.Current.GetComponent<IDamageable>() : null;
            puppet.PlayRemoteStrike(action, me);
        }
    }

    private void OnDied(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        if (!puppets.TryGetValue(id, out MonsterAI puppet))
        {
            return;
        }

        puppets.Remove(id);
        if (puppet != null && puppet.TryGetComponent(out Damageable health))
        {
            // Killed by this player when they had a hand in it: the kill counts for them, and the loot is theirs either way.
            Combatant killer = hitByMe.Remove(id) && Player.Current != null ? Player.Current.GetComponent<Combatant>() : null;
            health.Finish(killer);
        }
    }

    // A blow this player landed on one of the host's creatures goes to the host; the host notes its own.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (network != null && network.IsServer && victim != null && damage.Source != null && Player.IsPlayer(damage.Source.gameObject)
            && victim.TryGetComponent(out MonsterAI struck) && ids.TryGetValue(struck, out int struckId))
        {
            hitByHost.Add(struckId);
            return;
        }

        if (!FromHost || network == null || network.IsServer || victim == null || !victim.TryGetComponent(out MonsterAI creature) || !creature.IsRemote
            || damage.Source == null || !Player.IsPlayer(damage.Source.gameObject))
        {
            return;
        }

        int id = -1;
        foreach (KeyValuePair<int, MonsterAI> pair in puppets)
        {
            if (pair.Value == creature)
            {
                id = pair.Key;
                break;
            }
        }

        if (id < 0)
        {
            return;
        }

        hitByMe.Add(id);
        using var writer = new FastBufferWriter(96, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(damage.Amount);
        writer.WriteValueSafe((byte)damage.Type);
        writer.WriteValueSafe(damage.IsCritical);
        writer.WriteValueSafe(damage.HitPoint);
        writer.WriteValueSafe(damage.Direction);
        writer.WriteValueSafe(damage.Poise);
        writer.WriteValueSafe((int)damage.Technique);
        network.CustomMessagingManager.SendNamedMessage(HitMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }
}
