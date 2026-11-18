using UnityEngine;

/// <summary>
/// Black sap dripping on a boss arena's outer ring once the boss has raged (Docs/HeartrootEncounterDesign_2026-11-17.md §2.3 and §3.4,
/// Docs/HeartrootBossReview_2026-11-17.md C8: "drips only at the rage", so one hazard family comes at a time). Every few seconds a dark
/// ring fills on the rim (the purple of a blow to dodge, LobStrike's warning) and a drop lands there: a small blow with the boss's rot,
/// and a pool that stays. The drops keep a fixed order round the ring, spread apart, so nothing is random and the clean ground (the
/// mid ring and the light) never changes; the oldest pool dries as the next lands, so the ring holds a set number at most.
/// <para>
/// The drops are the boss's blows (it is their maker), so they hit as hard as its stage and rage make it, and only its foes. Where it
/// belongs: on an object in the arena scene beside its EncounterArena. A friend's game (co-op) runs its own from its own arena clock,
/// as EruptionRings does with the stages: each game's drops only hurt that game's player.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "each game drips its own from its own arena's rage clock; the maker is its copy of the boss")]
public sealed class ArenaHazardDrops : MonoBehaviour
{
    [Tooltip("The arena's middle, the ring's centre. Empty: where the placed boss stands as the scene wakes (its home).")]
    [SerializeField] private Transform centre;
    [Tooltip("The boss placed in the arena: only read for its home when Centre is empty.")]
    [SerializeField] private BossEncounter boss;
    [Tooltip("Metres from the centre to each drop: the floor's edge, inside the bowl's wall (a flat pool is hidden under a slope), with the " +
             "ring the fight is fought in left clean. Guardian's Hollow's floor is flat to about 17.5 m.")]
    [SerializeField, Min(1f)] private float radius = 18.2f;
    [Tooltip("How many places round the ring take a drop, evenly spaced.")]
    [SerializeField, Min(3)] private int places = 12;
    [Tooltip("Places skipped from one drop to the next, so each lands far from the last. Should share no factor with Places (5 with 12).")]
    [SerializeField, Min(1)] private int stride = 5;
    [Tooltip("Seconds between drops.")]
    [SerializeField, Min(0.5f)] private float interval = 8f;
    [Tooltip("Seconds the ring fills before the drop lands: the time to step out.")]
    [SerializeField, Min(0.2f)] private float warningSeconds = 2f;
    [Tooltip("The drop's blow: its Range is the ring's radius (an Arc of 360, Unblockable), with its status (Heartrot).")]
    [SerializeField] private AttackDefinition drip;
    [Tooltip("Damage of the drop itself, as a strike's Damage (the boss's stage and rage add to it). The pool does the rest.")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [Tooltip("Left where it lands (a prefab with a GroundHazard): the pool. Its Seconds decide how many lie at once (Interval x that many).")]
    [SerializeField] private GameObject poolPrefab;
    [Tooltip("Seen falling onto each ring as it fills, only a look (a strand of sap). Empty: none, the ring is the warning.")]
    [SerializeField] private GameObject strandPrefab;

    private Vector3 home;
    private bool hasHome;
    private bool dripping;
    private bool done;
    private float nextDropAt;
    private int dropped;
    // What is still falling, called off when the boss falls (review 2026-11-18: rot kept landing on the player looting the kill).
    private readonly System.Collections.Generic.List<LobStrike> falling = new System.Collections.Generic.List<LobStrike>();

    public float Radius => radius;

    /// <summary>The ring's centre as placed: Centre, else the placed boss (its home). For tests and tools; in play the home is read on waking.</summary>
    public Vector3 Centre => centre != null ? centre.position : boss != null ? boss.transform.position : transform.position;
    public int Places => places;
    public int Stride => stride;
    public GameObject PoolPrefab => poolPrefab;

    private void Awake()
    {
        // The placed boss is read before it can be removed (a friend's game takes the host's copy in its place as it wakes).
        if (centre == null && boss != null)
        {
            home = boss.transform.position;
            hasHome = true;
        }
    }

    private void Update()
    {
        EncounterArena arena = EncounterArena.Current;
        if (arena != null && arena.BossFallen && dripping && !done)
        {
            Dry();
            return;
        }

        if (done || arena == null || drip == null || arena.BossFallen || !arena.IsEnraged || RaidDirector.Current == null || !RaidDirector.Current.IsRunning)
        {
            return;
        }

        if (!dripping)
        {
            dripping = true;
            nextDropAt = Time.time;
        }

        if (Time.time < nextDropAt)
        {
            return;
        }

        nextDropAt = Time.time + interval;
        BossEncounter maker = arena.Boss;
        if (maker == null || !maker.IsAwake || !maker.TryGetComponent(out Combatant combatant) || !maker.TryGetComponent(out MonsterAI ai))
        {
            return;
        }

        Vector3 middle = centre != null ? centre.position : hasHome ? home : maker.transform.position;
        Vector3 at = DropPoint(dropped++, middle, radius, places, stride);
        Vector3 from = strandPrefab != null ? at + Vector3.up * 8f : at;
        falling.RemoveAll(lob => lob == null);
        LobStrike thrown = LobStrike.Throw(combatant, Blow(), from, at, ai.TargetLayers);
        if (thrown != null)
        {
            falling.Add(thrown);
        }
    }

    // The boss has fallen: no drop still falling lands, and the rot it left dries up.
    private void Dry()
    {
        done = true;
        foreach (LobStrike lob in falling)
        {
            if (lob != null)
            {
                Destroy(lob.gameObject);
            }
        }

        falling.Clear();
        if (poolPrefab == null)
        {
            return;
        }

        foreach (GroundHazard pool in FindObjectsByType<GroundHazard>(FindObjectsInactive.Exclude))
        {
            if (pool != null && pool.name.StartsWith(poolPrefab.name, System.StringComparison.Ordinal))
            {
                Destroy(pool.gameObject);
            }
        }
    }

    // One drop as a lob (LobStrike): its ring, damage, warning and pool.
    private MonsterAttack Blow() => new MonsterAttack
    {
        attack = drip,
        kind = MonsterAttackKind.Lob,
        damage = damage,
        lobSeconds = warningSeconds,
        hazardPrefab = poolPrefab,
        projectilePrefab = strandPrefab,
    };

    /// <summary>
    /// Where the <paramref name="index"/>th drop lands: one of <paramref name="places"/> evenly round <paramref name="middle"/> at
    /// <paramref name="ringRadius"/>, stepping <paramref name="stride"/> places from the last, so the order is fixed and each drop falls
    /// far from the one before. Pure, for tests.
    /// </summary>
    public static Vector3 DropPoint(int index, Vector3 middle, float ringRadius, int places, int stride)
    {
        places = Mathf.Max(1, places);
        int place = (int)((long)index * Mathf.Max(1, stride) % places);
        float angle = (place + 0.5f) / places * Mathf.PI * 2f;
        return middle + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
    }
}
