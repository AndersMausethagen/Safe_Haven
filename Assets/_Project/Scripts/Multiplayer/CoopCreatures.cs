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
