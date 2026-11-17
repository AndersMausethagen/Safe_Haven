using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Creatures in co-op: the host's game runs them, friends on the same run or in the host's Haven see the host's (a friend's
/// game makes none there: <see cref="CreatureSource.Remote"/>; it takes them over if the host leaves). Two roles, one per side:
/// <see cref="CreatureHost"/> (numbers creatures, sends spawns, poses, strikes, deaths; applies friends' hits; party
/// scaling) and <see cref="CreaturePuppets"/> (no creatures of its own; the host's as puppets; this player's blows to the
/// host). The messages:
/// <list type="bullet">
/// <item><c>CreatureSpawn</c> (reliable): new creatures near a friend, by prefab id, with elite affixes.</item>
/// <item><c>CreaturePoses</c> (unreliable, 22 bytes each, with whom it is after, whether it waits its turn, its poise, and how full its
/// awareness is of whom: the friend's stealth readout): only those that moved or changed, each at least once a second.</item>
/// <item><c>CreatureStrike</c>: a strike's action, target, time, where it began and where it is aimed; <c>CreatureDied</c>, <c>CreatureGone</c>.</item>
/// <item><c>CreatureHit</c> (friend to host): a blow landed on a puppet, by the friend or the companion they brought.</item>
/// <item><c>CreatureStagger</c> (friend to host): the friend staggered a puppet (a parry, a finishing blow holding it):
/// the host's creature staggers, and its state shows that to everyone.</item>
/// <item><c>CreaturePoise</c> (friend to host): the friend's blow did something to a puppet's poise, which a puppet keeps none of (a
/// parry's share of a great beast's or boss's, a hammer's crush of a boss that topples): the host's creature takes it, and a break
/// staggers or topples it for everyone.</item>
/// <item><c>CreatureWake</c>, <c>CreatureCall</c> (friend to host): what the friend woke (an extraction beacon's alarm
/// group by its path, an event's creatures by prefab id): made in the host's game and sent after the friend.</item>
/// </list>
/// </summary>
public sealed class CoopCreatures : ICoopPart
{
    public const string SpawnMessage = "SafeHaven.CreatureSpawn";
    public const string PosesMessage = "SafeHaven.CreaturePoses";
    public const string StrikeMessage = "SafeHaven.CreatureStrike";
    public const string DiedMessage = "SafeHaven.CreatureDied";
    public const string HitMessage = "SafeHaven.CreatureHit";
    public const string GoneMessage = "SafeHaven.CreatureGone";
    public const string WakeMessage = "SafeHaven.CreatureWake";
    public const string CallMessage = "SafeHaven.CreatureCall";
    public const string StaggerMessage = "SafeHaven.CreatureStagger";
    /// <summary>Friend to host: the friend laid a status on a puppet (a great enchantment's chill or roots): the real creature takes it.</summary>
    public const string StatusMessage = "SafeHaven.CreatureStatus";
    /// <summary>Friend to host: the friend's parry or crush wore a puppet's poise (MonsterAI.PoiseBlow): the real creature takes it.</summary>
    public const string PoiseMessage = "SafeHaven.CreaturePoise";

    /// <summary>Seconds between updates.</summary>
    public const float Interval = 0.1f;

    private readonly CreatureHost host;
    private readonly CreaturePuppets puppets = new CreaturePuppets();
    private bool isHost;
    private bool running;
    private float next;

    public CoopCreatures(MultiplayerSettings settings)
    {
        host = new CreatureHost(settings != null ? settings.CreatureHealthPerFriend : 0.6f);
    }

    /// <summary>True in a friend's game on the host's run: the host's game sends the creatures, none are made here.</summary>
    public static bool FromHost => CreaturePuppets.FromHost;

    public void Begin(CoopLink link)
    {
        isHost = link.IsHost;
        running = true;
        if (isHost)
        {
            host.Begin(link);
        }
        else
        {
            puppets.Begin(link);
        }
    }

    public void Tick()
    {
        if (!running || UnityEngine.Time.unscaledTime < next)
        {
            return;
        }

        next = UnityEngine.Time.unscaledTime + Interval;
        if (isHost)
        {
            host.Tick();
        }
        else
        {
            puppets.Tick();
        }
    }

    public void FriendLeft(ulong clientId) => host.FriendLeft(clientId);

    public void End()
    {
        if (!running)
        {
            return;
        }

        running = false;
        if (isHost)
        {
            host.End();
        }
        else
        {
            puppets.End();
        }
    }

    /// <summary>This friend is going into the host's run (see <see cref="CreaturePuppets.FollowHost"/>).</summary>
    public void FollowHost() => puppets.FollowHost();
}

// ---- The messages (CoopCreatures' list above), host to friend first, then friend to host.

/// <summary><c>CreatureSpawn</c>: new creatures near a friend.</summary>
public struct CreatureSpawnMessage : INetworkSerializable
{
    public List<CreatureSpawn> Creatures;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = Creatures?.Count ?? 0;
        serializer.SerializeValue(ref count);
        if (serializer.IsReader)
        {
            Creatures = new List<CreatureSpawn>();
        }

        for (int i = 0; i < count; i++)
        {
            CreatureSpawn creature = serializer.IsReader ? default : Creatures[i];
            creature.NetworkSerialize(serializer);
            if (serializer.IsReader)
            {
                Creatures.Add(creature);
            }
        }
    }
}

/// <summary>One new creature: its number, what to build it from, where, its health (a 255th) and its elite affixes ("Frenzied|Ironhide").</summary>
public struct CreatureSpawn : INetworkSerializable
{
    public int Id;
    public string PrefabId;
    public Vector3 Position;
    public float Yaw;
    public byte Health;
    public string Affixes;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref PrefabId);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Affixes);
    }
}

/// <summary>
/// <c>CreaturePoses</c>: the session's time and the poses (unreliable, so under one packet: CreatureHost.PosesPerMessage). Read in
/// place into one kept message, its list reused, as it comes ten times a second.
/// </summary>
public struct CreaturePosesMessage : INetworkSerializable
{
    public double Time;
    public List<CreaturePose> Poses;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Time);
        ushort count = (ushort)(Poses?.Count ?? 0);
        serializer.SerializeValue(ref count);
        if (serializer.IsReader)
        {
            if (Poses == null)
            {
                Poses = new List<CreaturePose>(count);
            }
            else
            {
                Poses.Clear();
            }
        }

        for (int i = 0; i < count; i++)
        {
            CreaturePose pose = serializer.IsReader ? default : Poses[i];
            pose.NetworkSerialize(serializer);
            if (serializer.IsReader)
            {
                Poses.Add(pose);
            }
        }
    }
}

/// <summary>
/// One creature's pose (22 bytes): position exact, the rest a byte each (facing in 256ths of a turn, speed in tenths, state,
/// health and poise in 255ths, the hunting byte with CreatureHost's flags, how full its awareness is and whom it is noticing).
/// </summary>
public struct CreaturePose : INetworkSerializable
{
    public ushort Id;
    public Vector3 Position;
    public byte Yaw;
    public byte Speed;
    public byte State;
    public byte Health;
    public byte Hunting;
    public byte Poise;
    public byte Notice;
    public byte Noticing;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref State);
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Hunting);
        serializer.SerializeValue(ref Poise);
        serializer.SerializeValue(ref Notice);
        serializer.SerializeValue(ref Noticing);
    }
}

/// <summary>
/// <c>CreatureStrike</c>: which creature, its action, at whom (a client id, or CoopLink.NoOne), when, where it began and faced,
/// where it is aimed, and the string's follow-ups and hold.
/// </summary>
public struct CreatureStrikeMessage : INetworkSerializable
{
    public int Id;
    public int Action;
    public ulong At;
    public double Time;
    public Vector3 From;
    public float Yaw;
    public Vector3 Aim;
    public byte FollowUps;
    public float Hold;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Action);
        serializer.SerializeValue(ref At);
        serializer.SerializeValue(ref Time);
        serializer.SerializeValue(ref From);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Aim);
        serializer.SerializeValue(ref FollowUps);
        serializer.SerializeValue(ref Hold);
    }
}

/// <summary><c>CreatureDied</c>, <c>CreatureGone</c>: which creature.</summary>
public struct CreatureIdMessage : INetworkSerializable
{
    public int Id;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref Id);
}

/// <summary>
/// <c>CreatureHit</c>: a friend's blow on a puppet, as their game judged it: the DamageInfo's parts (type, technique and strike
/// kind by number) and how the copy's guard took it (GuardOutcome).
/// </summary>
public struct CreatureHitMessage : INetworkSerializable
{
    public int Id;
    public float Amount;
    public byte Type;
    public bool Critical;
    public Vector3 Point;
    public Vector3 Direction;
    public float Poise;
    public int Technique;
    public byte Strike;
    public byte Guard;

    /// <summary>
    /// The whole blow, as this game struck it (Docs/BugPatterns.md, guard 5): every part of the DamageInfo but who struck it (the host
    /// decides that: <see cref="Blow"/>) and its attack (what it brings travels as its parts). CopyRuleTests fails when DamageInfo
    /// gains a part this doesn't carry, as the strike kind and the guard outcome were once dropped on the way.
    /// </summary>
    public static CreatureHitMessage Of(int id, in DamageInfo damage, in DamageResult result) => new CreatureHitMessage
    {
        Id = id,
        Amount = damage.Amount,
        Type = (byte)damage.Type,
        Critical = damage.IsCritical,
        Point = damage.HitPoint,
        Direction = damage.Direction,
        Poise = damage.Poise,
        Technique = (int)damage.Technique,
        Strike = (byte)damage.Strike,
        Guard = (byte)result.Guard,
    };

    /// <summary>The blow again, in the host's game, struck by <paramref name="striker"/> (or no one).</summary>
    public DamageInfo Blow(Combatant striker) =>
        new DamageInfo(striker, null, Amount, (DamageType)Type, Critical, Point, Direction, Poise, (HitTechnique)Technique, (StrikeKind)Strike);

    /// <summary>How the friend's copy's guard took it: only a block is passed on (a parry never reaches here).</summary>
    public GuardOutcome JudgedGuard => Guard == (byte)GuardOutcome.Blocked ? GuardOutcome.Blocked : GuardOutcome.None;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Amount);
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref Critical);
        serializer.SerializeValue(ref Point);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref Poise);
        serializer.SerializeValue(ref Technique);
        serializer.SerializeValue(ref Strike);
        serializer.SerializeValue(ref Guard);
    }
}

/// <summary><c>CreatureWake</c>: the extraction beacon's alarm group a friend woke, by its path (RaidGenerator.PathOf).</summary>
public struct CreatureWakeMessage : INetworkSerializable
{
    public string Path;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref Path);
}

/// <summary><c>CreatureCall</c>: a creature an event made for a friend: its prefab id, where, and whether it hunts.</summary>
public struct CreatureCallMessage : INetworkSerializable
{
    public string PrefabId;
    public Vector3 Position;
    public float Yaw;
    public bool Hunt;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PrefabId);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Hunt);
    }
}

/// <summary><c>CreatureStagger</c>: which creature a friend staggered, and for how long.</summary>
public struct CreatureStaggerMessage : INetworkSerializable
{
    public int Id;
    public float Seconds;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Seconds);
    }
}

/// <summary><c>CreaturePoise</c>: which creature a friend's blow wore the poise of, and how (MonsterAI.PoiseBlow, by number).</summary>
public struct CreaturePoiseMessage : INetworkSerializable
{
    public int Id;
    public byte Blow;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Blow);
    }
}

/// <summary><c>CreatureStatus</c>: which creature a friend laid a status on, and the status's id.</summary>
public struct CreatureStatusMessage : INetworkSerializable
{
    public int Id;
    public FixedString64Bytes StatusId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref StatusId);
    }
}
