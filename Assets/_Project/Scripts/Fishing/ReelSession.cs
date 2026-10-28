/// <summary>
/// The reeling game, as in Palworld: the fish darts up and down a bar, and the player keeps a catch zone on it by
/// holding attack (the zone climbs) and letting go (it falls). While the fish is inside the zone the catch fills; while
/// it isn't, the catch drains. Full: caught. Empty: it got away. Plain C# so it is testable; PlayerFishing drives it and
/// FishingHud draws it. Positions run from 0 (bottom) to 1 (top).
/// </summary>
public sealed class ReelSession
{
    private readonly float fight;
    private readonly float zoneSize;
    private readonly float lift;
    private readonly float fall;
    private readonly float fill;
    private readonly float drain;
    private readonly System.Random random;
    private float fishTarget;
    private float retarget;
    private float zoneSpeed;

    public ReelSession(float fight, float zoneSize, float lift, float fall, float fill, float drain, int seed)
    {
        this.fight = UnityEngine.Mathf.Clamp01(fight);
        this.zoneSize = zoneSize;
        this.lift = lift;
        this.fall = fall;
        this.fill = fill;
        this.drain = drain;
        random = new System.Random(seed);
        Fish = 0.5f;
        fishTarget = 0.5f;
        Zone = 0.5f - zoneSize * 0.5f;
        Catch = 0.35f;
    }

    /// <summary>Where the fish is.</summary>
    public float Fish { get; private set; }

    /// <summary>The bottom of the catch zone (it covers Zone to Zone + ZoneSize).</summary>
    public float Zone { get; private set; }

    public float ZoneSize => zoneSize;

    /// <summary>0..1: how close the fish is to being landed.</summary>
    public float Catch { get; private set; }

    public bool InZone => Fish >= Zone && Fish <= Zone + zoneSize;
    public bool Caught => Catch >= 1f;
    public bool Escaped => Catch <= 0f;
    public bool Over => Caught || Escaped;

    /// <summary>Moves everything on by <paramref name="seconds"/>; <paramref name="reeling"/> is attack being held.</summary>
    public void Tick(float seconds, bool reeling)
    {
        if (Over || seconds <= 0f)
        {
            return;
        }

        // The fish picks a new spot now and then (more often, and further, the more it fights) and swims to it.
        retarget -= seconds;
        if (retarget <= 0f)
        {
            float reach = 0.15f + fight * 0.7f;
            fishTarget = UnityEngine.Mathf.Clamp01(Fish + ((float)random.NextDouble() * 2f - 1f) * reach);
            retarget = UnityEngine.Mathf.Lerp(1.4f, 0.35f, fight) * (0.6f + (float)random.NextDouble() * 0.8f);
        }

        float speed = UnityEngine.Mathf.Lerp(0.25f, 1.6f, fight);
        Fish = UnityEngine.Mathf.MoveTowards(Fish, fishTarget, speed * seconds);

        zoneSpeed += (reeling ? lift : -fall) * seconds;
        Zone += zoneSpeed * seconds;
        if (Zone <= 0f)
        {
            Zone = 0f;
            zoneSpeed = UnityEngine.Mathf.Max(0f, zoneSpeed) * 0.3f;
        }
        else if (Zone >= 1f - zoneSize)
        {
            Zone = 1f - zoneSize;
            zoneSpeed = UnityEngine.Mathf.Min(0f, zoneSpeed) * 0.3f;
        }

        Catch = UnityEngine.Mathf.Clamp01(Catch + (InZone ? fill : -drain) * seconds);
    }
}
