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
    private const string GoneMessage = "SafeHaven.CreatureGone";

    /// <summary>Seconds between pose updates.</summary>
    public const float Interval = 0.1f;

    /// <summary>Metres around a friend within which they hear of creatures.</summary>
    private const float Reach = 90f;

    /// <summary>Poses per unreliable message (18 bytes each), under the transport's single-packet limit.</summary>
    private const int PosesPerMessage = 56;

    /// <summary>Seconds after which a creature's pose is sent again even when nothing changed, in case one was lost.</summary>
    private const float KeyframeSeconds = 1f;

    /// <summary>Metres beyond which a creature's pose goes three times a second instead of ten (unless its state changes).</summary>
    private const float NearReach = 40f;

    /// <summary>Seconds a friend waits, once the host has left the run, before the creatures become their own.</summary>
    private const float HostGoneSeconds = 2f;

    private const ulong Nobody = ulong.MaxValue;

    /// <summary>Seconds after which a strike that arrived late (a lost packet sent again) is no longer played.</summary>
    private const double StaleStrike = 0.6;

    private NetworkManager network;

    // Host
    private readonly Dictionary<MonsterAI, int> ids = new Dictionary<MonsterAI, int>();
    private readonly Dictionary<int, MonsterAI> byId = new Dictionary<int, MonsterAI>();
    private readonly Dictionary<ulong, HashSet<int>> told = new Dictionary<ulong, HashSet<int>>();
    private readonly HashSet<int> hitByHost = new HashSet<int>();
    private readonly Dictionary<ulong, FixedString128Bytes> friendPlace = new Dictionary<ulong, FixedString128Bytes>();
    private readonly Dictionary<ulong, Dictionary<int, SentPose>> sent = new Dictionary<ulong, Dictionary<int, SentPose>>();
    private readonly List<SentPose> outgoing = new List<SentPose>();
    private int ticks;
    private int friendsOnRun;
    private float healthPerFriend = 0.6f;
    private static FixedString128Bytes placeKey;
    private static string placeText = string.Empty;
    private static Scene placeScene;
    private static int placeSeed;

    // A creature's pose as sent: position exact, the rest in a byte each.
    private struct SentPose
    {
        public int Id;
        public Vector3 Position;
        public byte Yaw;
        public byte Speed;
        public byte State;
        public byte Health;
        public float At;
    }
    private readonly List<int> vanished = new List<int>();
    private readonly HashSet<int> scaled = new HashSet<int>();
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

    public void StartHosting(NetworkManager manager, MultiplayerSettings settings)
    {
        network = manager;
        healthPerFriend = settings != null ? settings.CreatureHealthPerFriend : healthPerFriend;
        Reset();
        network.CustomMessagingManager.RegisterNamedMessageHandler(HitMessage, OnHit);
    }

    public void StartGuest(NetworkManager manager)
    {
        network = manager;
        Reset();
        MonsterAI.RemoteClock = () => network != null ? network.ServerTime.Time : 0.0;
        network.CustomMessagingManager.RegisterNamedMessageHandler(SpawnMessage, OnSpawn);
        network.CustomMessagingManager.RegisterNamedMessageHandler(PosesMessage, OnPoses);
        network.CustomMessagingManager.RegisterNamedMessageHandler(StrikeMessage, OnStrike);
        network.CustomMessagingManager.RegisterNamedMessageHandler(DiedMessage, OnDied);
        network.CustomMessagingManager.RegisterNamedMessageHandler(GoneMessage, OnGone);
    }

    public void Reset()
    {
        // Mid-raid, puppets left behind become this game's own creatures rather than standing still.
        HandBack(quiet: true);
        ids.Clear();
        byId.Clear();
        told.Clear();
        hitByHost.Clear();
        friendPlace.Clear();
        sent.Clear();
        puppets.Clear();
        hitByMe.Clear();
        following = false;
        hostGoneSince = -1f;
        FromHost = false;
    }

    /// <summary>This friend is going into the host's run: from the raid's start, the host's game sends its creatures.</summary>
    public void FollowHost()
    {
        following = true;
        // From now on: the raid scene loading next makes no creatures of its own here.
        FromHost = GameSession.ActiveRaid != null;
    }

    /// <summary>
    /// The place a player is in, as friends' avatars report it: the scene, and in a raid its run (see
    /// <see cref="NetworkPlayerAvatar"/>).
    /// </summary>
    public static string PlaceHere()
    {
        RefreshPlace();
        return placeText;
    }

    /// <summary><see cref="PlaceHere"/> as friends' looks carry it, for comparing without making strings every frame.</summary>
    public static FixedString128Bytes PlaceKey
    {
        get
        {
            RefreshPlace();
            return placeKey;
        }
    }

    // Worked out again only when the scene or the run changes.
    private static void RefreshPlace()
    {
        Scene active = SceneManager.GetActiveScene();
        int seed = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed : 0;
        if (active == placeScene && seed == placeSeed && placeText.Length > 0)
        {
            return;
        }

        placeScene = active;
        placeSeed = seed;
        placeText = GameSession.ActiveRaid != null ? active.name + "#" + seed : active.name;
        placeKey = default;
        placeKey.CopyFromTruncated(placeText);
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
            friendPlace.Clear();
            sent.Clear();
            scaled.Clear();
            friendsOnRun = 0;
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

        TellOfVanished();
        ticks++;
        FixedString128Bytes place = PlaceKey;
        ScaleForParty(place);
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend == null || friend.OwnerClientId == NetworkManager.ServerClientId)
            {
                continue;
            }

            // A friend who left the run (died, went home) and comes back has a fresh map: tell them everything again.
            FixedString128Bytes theirs = friend.Look.Place;
            if (!friendPlace.TryGetValue(friend.OwnerClientId, out FixedString128Bytes before) || !before.Equals(theirs))
            {
                friendPlace[friend.OwnerClientId] = theirs;
                told.Remove(friend.OwnerClientId);
                sent.Remove(friend.OwnerClientId);
            }

            if (friend.IsVisible && theirs.Equals(place))
            {
                SendTo(friend.OwnerClientId, friend.Body.transform.position);
            }
        }
    }

    // Each friend on the run adds a share of every creature's health (health keeps its share of the maximum as it grows).
    private void ScaleForParty(FixedString128Bytes place)
    {
        int friends = 0;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && friend.OwnerClientId != NetworkManager.ServerClientId && friend.IsVisible && friend.Look.Place.Equals(place))
            {
                friends++;
            }
        }

        bool changed = friends != friendsOnRun;
        friendsOnRun = friends;
        foreach (KeyValuePair<MonsterAI, int> pair in ids)
        {
            MonsterAI creature = pair.Key;
            if (creature == null || (!changed && scaled.Contains(pair.Value)) || !creature.TryGetComponent(out Combatant combatant))
            {
                continue;
            }

            combatant.Stats.RemoveModifiersFrom(this);
            if (friends > 0)
            {
                combatant.Stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, healthPerFriend * friends, this));
            }

            scaled.Add(pair.Value);
        }
    }

    // Creatures removed without dying (a roamer left far behind): friends who knew them take theirs away, without loot.
    private void TellOfVanished()
    {
        vanished.Clear();
        foreach (KeyValuePair<int, MonsterAI> pair in byId)
        {
            if (pair.Value == null)
            {
                vanished.Add(pair.Key);
            }
        }

        foreach (int id in vanished)
        {
            byId.Remove(id);
            foreach (KeyValuePair<ulong, HashSet<int>> pair in told)
            {
                if (pair.Value.Remove(id))
                {
                    using var writer = new FastBufferWriter(8, Allocator.Temp);
                    writer.WriteValueSafe(id);
                    network.CustomMessagingManager.SendNamedMessage(GoneMessage, pair.Key, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
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
            // Strings go two bytes a character: room to grow, up to a large batch.
            using var spawns = new FastBufferWriter(256 + scratch.Count * 192, Allocator.Temp, 256 * 1024);
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
                spawns.WriteValueSafe(AffixesOf(creature));
            }

            network.CustomMessagingManager.SendNamedMessage(SpawnMessage, clientId, spawns, NetworkDelivery.ReliableFragmentedSequenced);
        }

        // Where the known ones are now, unreliable: only those that moved or changed (and each at least once a second, in case
        // one was lost), the far ones three times a second instead of ten unless their state changed.
        if (!sent.TryGetValue(clientId, out Dictionary<int, SentPose> last))
        {
            sent[clientId] = last = new Dictionary<int, SentPose>();
        }

        outgoing.Clear();
        float now = Time.unscaledTime;
        foreach (int id in known)
        {
            if (!byId.TryGetValue(id, out MonsterAI creature) || creature == null || creature.CurrentState == MonsterAI.State.Dead)
            {
                continue;
            }

            float distance = (creature.transform.position - at).sqrMagnitude;
            if (distance > Reach * Reach * 1.5f)
            {
                continue;
            }

            SentPose pose = PoseOf(id, creature, now);
            bool known2 = last.TryGetValue(id, out SentPose was);
            bool changed = !known2 || (was.Position - pose.Position).sqrMagnitude > 0.0009f || was.Yaw != pose.Yaw || was.State != pose.State
                || was.Health != pose.Health || was.Speed != pose.Speed;
            bool due = !known2 || now - was.At >= KeyframeSeconds;
            bool far = distance > NearReach * NearReach;
            if ((!changed && !due) || (far && !due && known2 && was.State == pose.State && ticks % 3 != 0))
            {
                continue;
            }

            last[id] = pose;
            outgoing.Add(pose);
        }

        for (int from = 0; from < outgoing.Count; from += PosesPerMessage)
        {
            int count = Mathf.Min(PosesPerMessage, outgoing.Count - from);
            using var poses = new FastBufferWriter(16 + count * 18, Allocator.Temp);
            poses.WriteValueSafe(network.ServerTime.Time);
            poses.WriteValueSafe((ushort)count);
            for (int i = from; i < from + count; i++)
            {
                SentPose pose = outgoing[i];
                poses.WriteValueSafe((ushort)pose.Id);
                poses.WriteValueSafe(pose.Position);
                poses.WriteValueSafe(pose.Yaw);
                poses.WriteValueSafe(pose.Speed);
                poses.WriteValueSafe(pose.State);
                poses.WriteValueSafe(pose.Health);
            }

            network.CustomMessagingManager.SendNamedMessage(PosesMessage, clientId, poses, NetworkDelivery.UnreliableSequenced);
        }
    }

    // Facing to 1.4 degrees, speed to a tenth of a metre a second, health to a 255th.
    private static SentPose PoseOf(int id, MonsterAI creature, float now) => new SentPose
    {
        Id = id,
        Position = creature.transform.position,
        Yaw = (byte)(Mathf.RoundToInt(Mathf.Repeat(creature.transform.eulerAngles.y, 360f) / 360f * 256f) & 255),
        Speed = (byte)Mathf.Clamp(Mathf.RoundToInt(creature.Speed * 10f), 0, 255),
        State = (byte)creature.CurrentState,
        Health = HealthByte(creature),
        At = now,
    };

    // An elite's affixes by name ("Frenzied|Ironhide"), or empty for a plain creature.
    private static string AffixesOf(MonsterAI creature)
    {
        if (!creature.TryGetComponent(out EliteCreature elite))
        {
            return string.Empty;
        }

        var names = new List<string>();
        foreach (EliteAffixDefinition affix in elite.Affixes)
        {
            if (affix != null)
            {
                names.Add(affix.name);
            }
        }

        return string.Join("|", names);
    }

    // The same elite in this game: the run's affixes by name, scaled as this raid scales its creatures.
    private static void MakeElite(MonsterAI puppet, string names)
    {
        RaidSession run = GameSession.ActiveRaid;
        RaidDirector director = RaidDirector.Current;
        if (string.IsNullOrEmpty(names) || run == null || director == null)
        {
            return;
        }

        var chosen = new List<EliteAffixDefinition>();
        foreach (string name in names.Split('|'))
        {
            foreach (EliteAffixDefinition affix in run.Definition.EliteAffixes)
            {
                if (affix != null && affix.name == name)
                {
                    chosen.Add(affix);
                    break;
                }
            }
        }

        if (chosen.Count > 0)
        {
            EliteCreature.Make(puppet, chosen, run.Difficulty, director.HealthMultiplier, director.PowerMultiplier);
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

            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(creature.LastAction);
            writer.WriteValueSafe(at);
            writer.WriteValueSafe(network.ServerTime.Time);
            writer.WriteValueSafe(creature.transform.position);
            writer.WriteValueSafe(creature.transform.eulerAngles.y);
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

        foreach (Dictionary<int, SentPose> poses in sent.Values)
        {
            poses.Remove(id);
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

        // This game's own creatures make way for the host's (a safety net: MonsterAI removes them as they wake), once in the raid.
        if (RaidDirector.Current == null)
        {
            return;
        }

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
        FixedString128Bytes place = PlaceKey;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            hostHere |= friend != null && friend.OwnerClientId == NetworkManager.ServerClientId && friend.Look.Place.Equals(place);
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
            reader.ReadValueSafe(out string affixes);
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

            GameObject copy;
            MonsterAI.SpawningRemote = true;
            try
            {
                copy = UnityEngine.Object.Instantiate(prefab.gameObject, position, Quaternion.Euler(0f, yaw, 0f));
            }
            finally
            {
                MonsterAI.SpawningRemote = false;
            }

            if (!copy.TryGetComponent(out MonsterAI puppet))
            {
                UnityEngine.Object.Destroy(copy);
                continue;
            }

            puppet.MakeRemote();
            MakeElite(puppet, affixes);
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
        reader.ReadValueSafe(out double time);
        reader.ReadValueSafe(out ushort count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out byte yaw);
            reader.ReadValueSafe(out byte speed);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte health);
            if (puppets.TryGetValue(id, out MonsterAI puppet) && puppet != null)
            {
                puppet.ApplyRemotePose(time, position, yaw * (360f / 256f), speed / 10f, (MonsterAI.State)state);
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
        reader.ReadValueSafe(out double time);
        reader.ReadValueSafe(out Vector3 from);
        reader.ReadValueSafe(out float yaw);
        if (network.ServerTime.Time - time > StaleStrike)
        {
            return;
        }

        if (puppets.TryGetValue(id, out MonsterAI puppet) && puppet != null)
        {
            IDamageable me = at == network.LocalClientId && Player.Current != null ? Player.Current.GetComponent<IDamageable>() : null;
            puppet.PlayRemoteStrike(action, me, from, yaw);
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

    private void OnGone(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        if (puppets.TryGetValue(id, out MonsterAI puppet))
        {
            puppets.Remove(id);
            hitByMe.Remove(id);
            if (puppet != null)
            {
                UnityEngine.Object.Destroy(puppet.gameObject);
            }
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
