using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The host's side of raid creatures in co-op (<see cref="CoopCreatures"/>): numbers each creature, tells each friend on
/// the same run of those near them, then sends what changed (poses, strikes, deaths, creatures gone), applies friends'
/// hits, and makes creatures tougher for each friend on the run. A creature's parts are looked up once, when it is first
/// seen. Plain C#.
/// </summary>
public sealed class CreatureHost
{
    /// <summary>Metres around a friend within which they hear of creatures.</summary>
    private const float Reach = 90f;

    /// <summary>Metres beyond which a creature's pose goes three times a second instead of ten (unless its state changes).</summary>
    private const float NearReach = 40f;

    /// <summary>Poses per unreliable message (19 bytes each), under the transport's single-packet limit.</summary>
    private const int PosesPerMessage = 54;

    /// <summary>Seconds after which a creature's pose is sent again even when nothing changed, in case one was lost.</summary>
    private const float KeyframeSeconds = 1f;

    // One creature as the host knows it: its parts, found once.
    private sealed class Hosted
    {
        public int Id;
        public MonsterAI Ai;
        public Damageable Health;
        public Combatant Combatant;
        public string PrefabId;
        public bool HitByHost;
        public bool Scaled;
    }

    // A creature's pose as last sent to one friend: position exact, the rest in a byte each.
    private struct SentPose
    {
        public Vector3 Position;
        public byte Yaw;
        public byte Speed;
        public byte State;
        public byte Health;
        public byte Hunting;
        public float At;
    }

    private readonly Dictionary<int, Hosted> byId = new Dictionary<int, Hosted>();
    private readonly Dictionary<MonsterAI, Hosted> byCreature = new Dictionary<MonsterAI, Hosted>();
    private readonly Dictionary<ulong, HashSet<int>> told = new Dictionary<ulong, HashSet<int>>();
    private readonly Dictionary<ulong, Dictionary<int, SentPose>> sent = new Dictionary<ulong, Dictionary<int, SentPose>>();
    private readonly Dictionary<ulong, FixedString128Bytes> friendPlace = new Dictionary<ulong, FixedString128Bytes>();
    private readonly List<Hosted> scratch = new List<Hosted>();
    private readonly List<(int id, SentPose pose)> outgoing = new List<(int, SentPose)>();
    private readonly List<int> vanished = new List<int>();
    private readonly float healthPerFriend;
    private CoopLink link;
    private Scene scene;
    private int nextId;
    private int ticks;
    private int friendsOnRun;
    private ContentDatabase content;

    public CreatureHost(float healthPerFriend)
    {
        this.healthPerFriend = healthPerFriend;
    }

    public void Begin(CoopLink session)
    {
        link = session;
        Forget();
        link.On(CoopCreatures.HitMessage, OnHit);
        link.On(CoopCreatures.WakeMessage, OnWake);
        link.On(CoopCreatures.CallMessage, OnCall);
        Damageable.AnyDied += OnAnyDied;
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    public void End()
    {
        Damageable.AnyDied -= OnAnyDied;
        Damageable.AnyDamaged -= OnAnyDamaged;
        Forget();
        link = null;
    }

    /// <summary>A friend left: what they were told goes with them.</summary>
    public void FriendLeft(ulong clientId)
    {
        told.Remove(clientId);
        sent.Remove(clientId);
        friendPlace.Remove(clientId);
    }

    private void Forget()
    {
        byId.Clear();
        byCreature.Clear();
        told.Clear();
        sent.Clear();
        friendPlace.Clear();
        friendsOnRun = 0;
    }

    /// <summary>Ten times a second.</summary>
    public void Tick()
    {
        Scene now = SceneManager.GetActiveScene();
        if (now != scene)
        {
            // A new place: what was known belonged to the last one.
            scene = now;
            Forget();
        }

        if (link == null || GameSession.ActiveRaid == null)
        {
            return;
        }

        Register();
        TellOfVanished();
        ticks++;
        FixedString128Bytes place = CoopPlace.Key;
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

    // Every creature in the raid gets a number the first time it is seen.
    private void Register()
    {
        foreach (MonsterAI creature in MonsterAI.All)
        {
            if (creature == null || byCreature.ContainsKey(creature) || creature.gameObject.scene != scene)
            {
                continue;
            }

            var hosted = new Hosted
            {
                Id = ++nextId,
                Ai = creature,
                Health = creature.GetComponent<Damageable>(),
                Combatant = creature.GetComponent<Combatant>(),
                PrefabId = creature.TryGetComponent(out SaveableEntity saveable) ? saveable.PrefabId : null,
            };
            byId[hosted.Id] = hosted;
            byCreature[creature] = hosted;
            creature.AttackStarted += () => OnStrikeBegun(hosted);
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
        foreach (Hosted hosted in byId.Values)
        {
            if (hosted.Ai == null || hosted.Combatant == null || (!changed && hosted.Scaled))
            {
                continue;
            }

            hosted.Combatant.Stats.RemoveModifiersFrom(this);
            if (friends > 0)
            {
                hosted.Combatant.Stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, healthPerFriend * friends, this));
            }

            hosted.Scaled = true;
        }
    }

    // Creatures removed without dying (a roamer left far behind): friends who knew them take theirs away, without loot.
    private void TellOfVanished()
    {
        vanished.Clear();
        foreach (Hosted hosted in byId.Values)
        {
            if (hosted.Ai == null)
            {
                vanished.Add(hosted.Id);
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
                    link.Send(CoopCreatures.GoneMessage, pair.Key, writer);
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
        foreach (Hosted hosted in byId.Values)
        {
            if (hosted.Ai != null && !known.Contains(hosted.Id) && hosted.Ai.CurrentState != MonsterAI.State.Dead && !string.IsNullOrEmpty(hosted.PrefabId)
                && (hosted.Ai.transform.position - at).sqrMagnitude <= Reach * Reach)
            {
                scratch.Add(hosted);
            }
        }

        if (scratch.Count > 0)
        {
            // Strings go two bytes a character: room to grow, up to a large batch.
            using var spawns = new FastBufferWriter(256 + scratch.Count * 192, Allocator.Temp, 256 * 1024);
            spawns.WriteValueSafe(scratch.Count);
            foreach (Hosted hosted in scratch)
            {
                known.Add(hosted.Id);
                spawns.WriteValueSafe(hosted.Id);
                spawns.WriteValueSafe(hosted.PrefabId);
                spawns.WriteValueSafe(hosted.Ai.transform.position);
                spawns.WriteValueSafe(hosted.Ai.transform.eulerAngles.y);
                spawns.WriteValueSafe(HealthByte(hosted));
                spawns.WriteValueSafe(AffixesOf(hosted.Ai));
            }

            link.Send(CoopCreatures.SpawnMessage, clientId, spawns);
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
            if (!byId.TryGetValue(id, out Hosted hosted) || hosted.Ai == null || hosted.Ai.CurrentState == MonsterAI.State.Dead)
            {
                continue;
            }

            float distance = (hosted.Ai.transform.position - at).sqrMagnitude;
            if (distance > Reach * Reach * 1.5f)
            {
                continue;
            }

            SentPose pose = PoseOf(hosted, now, clientId);
            bool seen = last.TryGetValue(id, out SentPose was);
            bool changed = !seen || (was.Position - pose.Position).sqrMagnitude > 0.0009f || was.Yaw != pose.Yaw || was.State != pose.State
                || was.Health != pose.Health || was.Speed != pose.Speed || was.Hunting != pose.Hunting;
            bool due = !seen || now - was.At >= KeyframeSeconds;
            bool far = distance > NearReach * NearReach;
            if ((!changed && !due) || (far && !due && seen && was.State == pose.State && ticks % 3 != 0))
            {
                continue;
            }

            last[id] = pose;
            outgoing.Add((id, pose));
        }

        for (int from = 0; from < outgoing.Count; from += PosesPerMessage)
        {
            int count = Mathf.Min(PosesPerMessage, outgoing.Count - from);
            using var poses = new FastBufferWriter(16 + count * 19, Allocator.Temp);
            poses.WriteValueSafe(link.Now);
            poses.WriteValueSafe((ushort)count);
            for (int i = from; i < from + count; i++)
            {
                (int id, SentPose pose) = outgoing[i];
                poses.WriteValueSafe((ushort)id);
                poses.WriteValueSafe(pose.Position);
                poses.WriteValueSafe(pose.Yaw);
                poses.WriteValueSafe(pose.Speed);
                poses.WriteValueSafe(pose.State);
                poses.WriteValueSafe(pose.Health);
                poses.WriteValueSafe(pose.Hunting);
            }

            link.Send(CoopCreatures.PosesMessage, clientId, poses, NetworkDelivery.UnreliableSequenced);
        }
    }

    // Facing to 1.4 degrees, speed to a tenth of a metre a second, health to a 255th; and whom it is after, for that friend.
    private static SentPose PoseOf(Hosted hosted, float now, ulong friend) => new SentPose
    {
        Position = hosted.Ai.transform.position,
        Yaw = (byte)(Mathf.RoundToInt(Mathf.Repeat(hosted.Ai.transform.eulerAngles.y, 360f) / 360f * 256f) & 255),
        Speed = (byte)Mathf.Clamp(Mathf.RoundToInt(hosted.Ai.Speed * 10f), 0, 255),
        State = (byte)hosted.Ai.CurrentState,
        Health = HealthByte(hosted),
        Hunting = (byte)HuntingFor(hosted.Ai, friend),
        At = now,
    };

    // After this friend, after someone else on the players' side (the host, another friend), or nobody.
    private static MonsterAI.RemoteHunt HuntingFor(MonsterAI creature, ulong friend)
    {
        if (!creature.IsHuntingPlayerSide)
        {
            return MonsterAI.RemoteHunt.None;
        }

        return creature.Target is FriendHitbox hitbox && hitbox.ClientId == friend ? MonsterAI.RemoteHunt.You : MonsterAI.RemoteHunt.Friend;
    }

    private static byte HealthByte(Hosted hosted) =>
        hosted.Health != null ? (byte)Mathf.Clamp(Mathf.CeilToInt(hosted.Health.CurrentHealth / hosted.Health.MaxHealth * 255f), 0, 255) : (byte)255;

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

    // A strike begun here: friends who know the creature play it, from where it struck, at whoever it is aimed.
    private void OnStrikeBegun(Hosted hosted)
    {
        if (link == null || hosted.Ai == null)
        {
            return;
        }

        MonsterAI creature = hosted.Ai;
        ulong at = creature.Target is FriendHitbox friend ? friend.ClientId
            : creature.Target is Component body && Player.IsPlayer(body.gameObject) ? NetworkManager.ServerClientId
            : CoopLink.NoOne;
        foreach (KeyValuePair<ulong, HashSet<int>> pair in told)
        {
            if (!pair.Value.Contains(hosted.Id))
            {
                continue;
            }

            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(hosted.Id);
            writer.WriteValueSafe(creature.LastAction);
            writer.WriteValueSafe(at);
            writer.WriteValueSafe(link.Now);
            writer.WriteValueSafe(creature.transform.position);
            writer.WriteValueSafe(creature.transform.eulerAngles.y);
            link.Send(CoopCreatures.StrikeMessage, pair.Key, writer);
        }
    }

    private void OnAnyDied(Damageable victim, DamageInfo blow)
    {
        if (link == null || victim == null || !victim.TryGetComponent(out MonsterAI creature) || !byCreature.TryGetValue(creature, out Hosted hosted))
        {
            return;
        }

        foreach (KeyValuePair<ulong, HashSet<int>> pair in told)
        {
            if (pair.Value.Remove(hosted.Id))
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(hosted.Id);
                link.Send(CoopCreatures.DiedMessage, pair.Key, writer);
            }
        }

        foreach (Dictionary<int, SentPose> poses in sent.Values)
        {
            poses.Remove(hosted.Id);
        }
    }

    // The host's own blows: noted, so a friend's finishing blow still counts the kill for the host too.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (victim != null && damage.Source != null && Player.IsPlayer(damage.Source.gameObject) && victim.TryGetComponent(out MonsterAI creature)
            && byCreature.TryGetValue(creature, out Hosted hosted))
        {
            hosted.HitByHost = true;
        }
    }

    /// <summary>Metres from a friend's body within which they may ask for creatures (an event, a beacon: where they stand).</summary>
    private const float CallReach = 60f;

    // A friend lit an extraction beacon on this run: its alarm group is filled here and hunts them.
    private void OnWake(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string path);
        FriendHitbox friend = FriendOnThisRun(sender);
        RaidDirector director = RaidDirector.Current;
        RaidGenerator generator = director != null ? director.GetComponentInChildren<RaidGenerator>() : null;
        if (generator == null)
        {
            generator = Object.FindAnyObjectByType<RaidGenerator>();
        }

        SpawnGroup group = generator != null ? generator.FindGroup(path) : null;
        if (friend == null || group == null || (group.transform.position - friend.transform.position).sqrMagnitude > CallReach * CallReach * 4f)
        {
            return;
        }

        director.Wake(group, friend);
    }

    // An event a friend set off made creatures: made here, by prefab id, and sent after them if they hunt.
    private void OnCall(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string prefabId);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out float yaw);
        reader.ReadValueSafe(out bool hunt);
        FriendHitbox friend = FriendOnThisRun(sender);
        RaidDirector director = RaidDirector.Current;
        if (friend == null || director == null || (position - friend.transform.position).sqrMagnitude > CallReach * CallReach)
        {
            return;
        }

        if (content == null)
        {
            content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        }

        SaveableEntity prefab = content != null ? content.FindPrefab(prefabId) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"Co-op: no creature prefab '{prefabId}' for what a friend's event called.");
            return;
        }

        GameObject made = Object.Instantiate(prefab.gameObject, position, Quaternion.Euler(0f, yaw, 0f));
        director.PrepareSpawned(made, hunt, true, friend);
    }

    // The body in this game of a friend on the host's run, or null (gone, elsewhere, or down for good).
    private static FriendHitbox FriendOnThisRun(ulong clientId)
    {
        FixedString128Bytes place = CoopPlace.Key;
        foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
        {
            if (avatar != null && avatar.OwnerClientId == clientId && avatar.Look.Place.Equals(place) && avatar.Body != null
                && avatar.Body.TryGetComponent(out FriendHitbox hitbox) && hitbox.IsAlive)
            {
                return hitbox;
            }
        }

        return null;
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
        if (!byId.TryGetValue(id, out Hosted hosted) || hosted.Ai == null || hosted.Health == null || !hosted.Health.IsAlive)
        {
            return;
        }

        // Struck by the host before: should this blow kill it, the kill and its loot count for the host too.
        Combatant host = hosted.HitByHost && Player.Current != null ? Player.Current.GetComponent<Combatant>() : null;
        hosted.Health.ApplyDamage(new DamageInfo(host, null, amount, (DamageType)type, critical, point, direction, poise, (HitTechnique)technique));
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && friend.OwnerClientId == sender && friend.Body != null && friend.Body.TryGetComponent(out FriendHitbox hitbox))
            {
                hosted.Ai.ProvokedBy(hitbox);
            }
        }
    }
}
