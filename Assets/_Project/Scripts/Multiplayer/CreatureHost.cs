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

    /// <summary>Metres round a friend inside which the host's creatures are sent to them (and, the host gone, handed back to them).</summary>
    public const float SendReach = Reach;

    /// <summary>Metres beyond which a creature's pose goes three times a second instead of ten (unless its state changes).</summary>
    private const float NearReach = 40f;

    /// <summary>Poses per unreliable message (22 bytes each), under the transport's single-packet limit (no more than 54 of the old
    /// 19-byte poses).</summary>
    private const int PosesPerMessage = 46;

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
        public System.Action OnStrike;
    }

    /// <summary>The longest a friend's game may ask a creature to stagger for (a parry is 1.3 s; a finishing blow holds less).</summary>
    private const float LongestAskedStagger = 3f;

    // A creature's pose as last sent to one friend: position exact, the rest in a byte each.
    private struct SentPose
    {
        public Vector3 Position;
        public byte Yaw;
        public byte Speed;
        public byte State;
        public byte Health;
        // Whom it is after (RemoteHunt) in the low bits; WaitingFlag set while it waits its turn.
        public byte Hunting;
        public byte Poise;
        // How full its awareness is, and whom it is noticing (RemoteHunt, for that friend): their stealth readout.
        public byte Notice;
        public byte Noticing;
        public float At;
    }

    /// <summary>Set in a pose's hunting byte while the creature waits its turn (a ready stance).</summary>
    public const byte WaitingFlag = 0x80;

    /// <summary>Set in a pose's hunting byte while the creature is down from a felling blow.</summary>
    public const byte FelledFlag = 0x40;

    /// <summary>Set in a pose's hunting byte while the creature's weapon guard is up.</summary>
    public const byte GuardingFlag = 0x20;

    /// <summary>Set in a pose's hunting byte while the creature is marked (Hunter's Mark).</summary>
    public const byte MarkedFlag = 0x10;

    /// <summary>Set in a pose's hunting byte once the creature's shield has splintered.</summary>
    public const byte ShieldBrokenFlag = 0x08;

    /// <summary>The hunting byte's low bits: whom it is after (MonsterAI.RemoteHunt).</summary>
    public const byte HuntMask = 0x03;

    // Poise in steps of a 64th, so a creature getting its breath back isn't a change every tick.
    private const int PoiseSteps = 64;

    private readonly Dictionary<int, Hosted> byId = new Dictionary<int, Hosted>();
    private readonly Dictionary<MonsterAI, Hosted> byCreature = new Dictionary<MonsterAI, Hosted>();
    private readonly Dictionary<ulong, HashSet<int>> told = new Dictionary<ulong, HashSet<int>>();
    private readonly Dictionary<ulong, Dictionary<int, SentPose>> sent = new Dictionary<ulong, Dictionary<int, SentPose>>();
    private readonly Dictionary<ulong, FixedString128Bytes> friendPlace = new Dictionary<ulong, FixedString128Bytes>();
    private readonly List<Hosted> scratch = new List<Hosted>();
    private readonly List<(int id, SentPose pose)> outgoing = new List<(int, SentPose)>();
    private readonly List<CreatureSpawn> spawning = new List<CreatureSpawn>();
    private readonly List<CreaturePose> posing = new List<CreaturePose>();
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
        link.On(CoopCreatures.StaggerMessage, OnStagger);
        link.On(CoopCreatures.StatusMessage, OnStatus);
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
        // Each creature told this of its strikes: no longer, or a session opened again would hear every strike twice over.
        foreach (Hosted hosted in byId.Values)
        {
            if (hosted.Ai != null && hosted.OnStrike != null)
            {
                hosted.Ai.AttackStarted -= hosted.OnStrike;
            }

            // The party's extra health goes with the party: closed mid-raid, the host fought every creature at +60% a friend alone.
            if (hosted.Combatant != null)
            {
                hosted.Combatant.Stats.RemoveModifiersFrom(this);
            }
        }

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

        // A raid, or the host's own Haven with friends visiting (each friend is told of what is in their place only).
        if (link == null || (GameSession.ActiveRaid == null && !HavenVisit.HostIsHome()))
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
            hosted.OnStrike = () => OnStrikeBegun(hosted);
            creature.AttackStarted += hosted.OnStrike;
        }
    }

    // Each friend on the run adds a share of every creature's health (health keeps its share of the maximum as it grows).
    private void ScaleForParty(FixedString128Bytes place)
    {
        int friends = 0;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            // Standing, or down and waiting to be helped up (so the count doesn't flap): one fallen for good and watching the others
            // (CoopSpectate) left the survivors fighting at +60% for each of them for the rest of the run.
            if (friend != null && friend.OwnerClientId != NetworkManager.ServerClientId && friend.IsVisible && friend.Look.Place.Equals(place)
                && (friend.Look.Health > 0 || friend.Look.Downed))
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
                    writer.WriteNetworkSerializable(new CreatureIdMessage { Id = id });
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
            spawning.Clear();
            foreach (Hosted hosted in scratch)
            {
                known.Add(hosted.Id);
                spawning.Add(new CreatureSpawn
                {
                    Id = hosted.Id,
                    PrefabId = hosted.PrefabId,
                    Position = hosted.Ai.transform.position,
                    Yaw = hosted.Ai.transform.eulerAngles.y,
                    Health = HealthByte(hosted),
                    Affixes = AffixesOf(hosted.Ai),
                });
            }

            spawns.WriteNetworkSerializable(new CreatureSpawnMessage { Creatures = spawning });
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
                || was.Health != pose.Health || was.Speed != pose.Speed || was.Hunting != pose.Hunting || was.Poise != pose.Poise
                || was.Notice != pose.Notice || was.Noticing != pose.Noticing;
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
            using var poses = new FastBufferWriter(16 + count * 22, Allocator.Temp);
            posing.Clear();
            for (int i = from; i < from + count; i++)
            {
                (int id, SentPose pose) = outgoing[i];
                posing.Add(new CreaturePose
                {
                    Id = (ushort)id,
                    Position = pose.Position,
                    Yaw = pose.Yaw,
                    Speed = pose.Speed,
                    State = pose.State,
                    Health = pose.Health,
                    Hunting = pose.Hunting,
                    Poise = pose.Poise,
                    Notice = pose.Notice,
                    Noticing = pose.Noticing,
                });
            }

            poses.WriteNetworkSerializable(new CreaturePosesMessage { Time = link.Now, Poses = posing });
            link.Send(CoopCreatures.PosesMessage, clientId, poses, NetworkDelivery.UnreliableSequenced);
        }
    }

    // Facing to 1.4 degrees, speed to a tenth of a metre a second, health to a 255th; whom it is after, for that friend, and whether it
    // waits its turn; its poise to a 64th (friends' copies show it: reeling, the poise bar).
    private static SentPose PoseOf(Hosted hosted, float now, ulong friend) => new SentPose
    {
        Position = hosted.Ai.transform.position,
        Yaw = (byte)(Mathf.RoundToInt(Mathf.Repeat(hosted.Ai.transform.eulerAngles.y, 360f) / 360f * 256f) & 255),
        Speed = (byte)Mathf.Clamp(Mathf.RoundToInt(hosted.Ai.Speed * 10f), 0, 255),
        State = (byte)hosted.Ai.CurrentState,
        Health = HealthByte(hosted),
        Hunting = (byte)((byte)HuntingFor(hosted.Ai, friend) | (hosted.Ai.IsWaitingTurn ? WaitingFlag : 0)
            | (hosted.Ai.IsStaggered && hosted.Ai.IsFelled ? FelledFlag : 0) | (hosted.Ai.IsWeaponGuarding ? GuardingFlag : 0)
            | (hosted.Ai.IsMarked ? MarkedFlag : 0) | (hosted.Ai.ShieldBroken ? ShieldBrokenFlag : 0)),
        Poise = PoiseByte(hosted.Ai),
        Notice = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Round(hosted.Ai.AwarenessLevel * PoiseSteps) / PoiseSteps * 255f), 0, 255),
        Noticing = (byte)NoticingFor(hosted.Ai, friend),
        At = now,
    };

    // Whom it is noticing, for that friend: them (their body here), someone else on the players' side, or nobody.
    private static MonsterAI.RemoteHunt NoticingFor(MonsterAI creature, ulong friend)
    {
        IDamageable noticed = creature.Noticing;
        if (noticed is FriendHitbox body && body != null)
        {
            return body.ClientId == friend ? MonsterAI.RemoteHunt.You : MonsterAI.RemoteHunt.Friend;
        }

        return noticed is Component other && other != null && Player.IsPlayer(other.gameObject) ? MonsterAI.RemoteHunt.Friend : MonsterAI.RemoteHunt.None;
    }

    private static byte PoiseByte(MonsterAI creature)
    {
        float share = creature.Poise != null ? creature.Poise.Fraction : 1f;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Round(share * PoiseSteps) / PoiseSteps * 255f), 0, 255);
    }

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
        // A combo's later blows (AttackStarted is raised for each) aren't sent: friends' copies play the string from its first.
        if (creature.StrikeBlow > 0)
        {
            return;
        }

        ulong at = creature.Target is FriendHitbox friend ? friend.ClientId
            : creature.Target is Component body && Player.IsPlayer(body.gameObject) ? NetworkManager.ServerClientId
            : CoopLink.NoOne;
        // Where it is aimed, for everyone it isn't aimed at: their copy strikes that way, as far as this one does.
        Vector3 aim = creature.Target is Component aimed && aimed != null ? aimed.transform.position
            : creature.transform.position + creature.AttackDirection * 4f;
        foreach (KeyValuePair<ulong, HashSet<int>> pair in told)
        {
            if (!pair.Value.Contains(hosted.Id))
            {
                continue;
            }

            using var writer = new FastBufferWriter(80, Allocator.Temp);
            writer.WriteNetworkSerializable(new CreatureStrikeMessage
            {
                Id = hosted.Id,
                Action = creature.LastAction,
                At = at,
                Time = link.Now,
                From = creature.transform.position,
                Yaw = creature.transform.eulerAngles.y,
                Aim = aim,
                FollowUps = (byte)Mathf.Clamp(creature.StrikeFollowUps, 0, 255),
                Hold = creature.StrikeHold,
            });
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
                writer.WriteNetworkSerializable(new CreatureIdMessage { Id = hosted.Id });
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
        reader.ReadNetworkSerializable(out CreatureWakeMessage wake);
        string path = wake.Path;
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

        director.Creatures.Wake(group, friend);
    }

    // An event a friend set off made creatures: made here, by prefab id, and sent after them if they hunt.
    private void OnCall(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out CreatureCallMessage call);
        string prefabId = call.PrefabId;
        Vector3 position = call.Position;
        float yaw = call.Yaw;
        bool hunt = call.Hunt;
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
        director.Creatures.Prepare(made, hunt, true, friend);
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

    // Whether a blow of this size ends the creature as it stands: after its defenses, and unless it can't be hit just now.
    private static bool WouldKill(Hosted hosted, float amount, DamageType type, StrikeKind strike)
    {
        if (hosted.Health.IsInvulnerable || hosted.Combatant == null)
        {
            return false;
        }

        float worth = DamageCalculator.Mitigate(amount, type, strike, hosted.Combatant.Stats, hosted.Combatant.Rules) * hosted.Health.DamageTakenMultiplier;
        return worth >= hosted.Health.CurrentHealth - hosted.Health.HealthFloor;
    }

    // A friend staggered their copy of a creature (a parry, a finishing blow holding it): the real one staggers, at least
    // that long, which every game then sees in its state. Without this a friend's parry stopped nothing: the creature here
    // fought on, and its next strike played over the stagger in their game.
    private void OnStagger(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out CreatureStaggerMessage stagger);
        int id = stagger.Id;
        float seconds = stagger.Seconds;
        if (!byId.TryGetValue(id, out Hosted hosted) || hosted.Ai == null || hosted.Health == null || !hosted.Health.IsAlive
            || !(seconds > 0f) || FriendOnThisRun(sender) == null)
        {
            return;
        }

        hosted.Ai.StaggerAtLeast(Mathf.Min(seconds, LongestAskedStagger));
    }

    // A friend laid a status on their copy of a creature: the real one takes it (statuses are found among the loaded definitions,
    // which a great enchantment holds by reference).
    private void OnStatus(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out CreatureStatusMessage laid);
        int id = laid.Id;
        FixedString64Bytes statusId = laid.StatusId;
        if (!byId.TryGetValue(id, out Hosted hosted) || hosted.Health == null || !hosted.Health.IsAlive || FriendOnThisRun(sender) == null)
        {
            return;
        }

        StatusEffectDefinition status = StatusById(statusId.ToString());
        if (status != null)
        {
            StatusEffects.Apply(hosted.Health, status, null);
        }
    }

    // Statuses by id, found once each: a friend in a hazard refreshes its status every half second, and each message scanned every
    // loaded object for it.
    private readonly Dictionary<string, StatusEffectDefinition> statusesById = new Dictionary<string, StatusEffectDefinition>();

    private StatusEffectDefinition StatusById(string wanted)
    {
        if (statusesById.TryGetValue(wanted, out StatusEffectDefinition known) && known != null)
        {
            return known;
        }

        foreach (StatusEffectDefinition status in Resources.FindObjectsOfTypeAll<StatusEffectDefinition>())
        {
            if (status != null && status.Id == wanted)
            {
                statusesById[wanted] = status;
                return status;
            }
        }

        return null;
    }

    // A friend's blow on one of the host's creatures: applied here, and the creature turns on them.
    private void OnHit(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out CreatureHitMessage hit);
        int id = hit.Id;
        // From a friend still on this run, as the other friend messages ask: a late arrow from one who just extracted lands nowhere.
        if (!byId.TryGetValue(id, out Hosted hosted) || hosted.Ai == null || hosted.Health == null || !hosted.Health.IsAlive
            || FriendOnThisRun(sender) == null)
        {
            return;
        }

        // Struck by the host before: should this blow kill it, the kill and its loot count for the host too. Only that blow
        // carries the host's name. Every other is the friend's alone: named as the host's, each showed in the host's game as
        // a blow of their own (its hit feedback, their stealth and their relics all listen for those).
        DamageInfo blow = hit.Blow(null);
        Combatant host = hosted.HitByHost && Player.Current != null && WouldKill(hosted, blow.Amount, blow.Type, blow.Strike) ? Player.Current.Combatant : null;
        // Judged already: the friend's game put the blow to its copy's guard (a shield, as it faced there) before sending it. Whether it
        // was caught unaware is this game's to say, from where the blow came.
        hosted.Ai.JudgeAmbushFrom(blow.HitPoint - blow.Direction * 2f);
        // With how it lands (a hammer against a creature weak to blunt) and how its shield there took it (poise broken through a raised
        // shield breaks the guard here, toward the splinter).
        hosted.Health.ApplyJudged(hit.Blow(host), hit.JudgedGuard);
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && friend.OwnerClientId == sender && friend.Body != null && friend.Body.TryGetComponent(out FriendHitbox hitbox))
            {
                hosted.Ai.ProvokedBy(hitbox);
            }
        }
    }
}
