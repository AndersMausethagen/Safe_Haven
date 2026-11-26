using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>When a spawn group is filled.</summary>
public enum SpawnTrigger
{
    RaidStart = 0,
    Stage = 1,
    Alarm = 2,
}

/// <summary>
/// A set of hand-placed spawn points (this object's children) of which a raid fills only some: "3 to 4 of these 8 spots
/// get a wolf". Each filled point gets a prefab from the spawn table, nudged a little and turned at random, so a raid
/// feels different each run while creatures, caches and resources stay where the level designer meant them to be.
/// RaidGenerator fills RaidStart groups when the raid loads; Stage and Alarm groups wait until the raid calls for them
/// (RaidDirector). Move, add or delete the child points to reshape it.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpawnGroup : MonoBehaviour
{
    [SerializeField] private SpawnTable table;
    [Tooltip("How many points are filled each raid, at least and at most. Limited to the number of points.")]
    [SerializeField] private Vector2Int count = new Vector2Int(2, 3);
    [Tooltip("Spawns land up to this far from their point, on walkable ground. 0 places them exactly on it.")]
    [SerializeField, Min(0f)] private float scatterRadius = 1.5f;
    [Tooltip("Turns each spawn to a random facing.")]
    [SerializeField] private bool randomFacing = true;
    [Tooltip("Keeps selected points this far apart, useful when one spawn is a whole pack. 0 preserves the original selection.")]
    [SerializeField, Min(0f)] private float minimumSpawnSeparation;
    [Tooltip("Only fills points this far from the raid's entry (the player start); the distances are set on the raid. " +
             "Any uses every point.")]
    [SerializeField] private EntryBand entryBand = EntryBand.Any;

    [Header("When")]
    [Tooltip("RaidStart: filled while the raid loads. Stage: appears when the raid reaches the stage below. Alarm: " +
             "appears when something sets it off, such as an extraction point with this group as its alarm.")]
    [SerializeField] private SpawnTrigger trigger = SpawnTrigger.RaidStart;
    [Tooltip("For Stage groups: the raid stage that wakes the group (1 is the raid definition's first stage).")]
    [SerializeField, Min(1)] private int stage = 1;
    [Tooltip("Creatures from this group head straight for the player and never give up the chase.")]
    [SerializeField] private bool huntPlayer;

    [Header("Map")]
    [Tooltip("Baking the raid map (RaidMap) replaces this group's points with the markers of this kind; groups sharing a " +
             "kind take turns, a whole site each. None (and no Zone) keeps the points placed by hand.")]
    [SerializeField] private RaidSiteKind site = RaidSiteKind.None;
    [Tooltip("Baking the raid map also gives this group the candidate points of the painted spawn zone of this name " +
             "(RaidMapDefinition › Zones); groups sharing a zone take turns. Empty uses no zone.")]
    [SerializeField] private string zone = "";
    [Tooltip("Generated maps: this group's share of the sites of its kind against the other groups of the kind (1 each: turn and " +
             "turn about). A timber stand at 1.4 gets more sites than an ore vein at 0.6.")]
    [SerializeField, Min(0f)] private float siteShare = 1f;
    [Tooltip("Generated maps: takes the sites of its kind farthest from the start (ore lies farther out).")]
    [SerializeField] private bool farthestSites;
    [Tooltip("Generated maps: takes one point at every site of its kind instead of a share of them, beside what stands there (a camp's " +
             "stash beside its guards). Not counted in the share of the others.")]
    [SerializeField] private bool everySite;
    [Tooltip("Generated maps: the minimap's mark for a site of this group once the player has seen it (an ore vein, a timber stand), " +
             "until what stands there is gathered. Empty: not marked.")]
    [SerializeField] private Sprite mapIcon;

    private readonly List<GameObject> spawned = new List<GameObject>();

    public SpawnTable Table => table;
    public int MinCount => Mathf.Max(0, Mathf.Min(count.x, count.y));
    public int MaxCount => Mathf.Max(count.x, count.y);
    public IReadOnlyList<GameObject> Spawned => spawned;
    /// <summary>The raid spawn list that fills this group's points, if any (RaidGenerator binds it by name).</summary>
    public RaidSpawnList List { get; private set; }
    public SpawnTrigger Trigger => List != null ? List.trigger : trigger;
    public int Stage => List != null ? List.stage : stage;
    public bool HuntPlayer => List != null ? List.huntPlayer : huntPlayer;
    public RaidSiteKind Site => site;
    public string Zone => zone ?? "";
    public float SiteShare => siteShare;
    public bool FarthestSites => farthestSites;
    public bool EverySite => everySite;
    public Sprite MapIcon => mapIcon;
    /// <summary>True when baking the raid map rebuilds this group's points (it has a Site or a Zone).</summary>
    public bool IsBakedFromMap => site != RaidSiteKind.None || !string.IsNullOrEmpty(zone);
    public EntryBand Band => entryBand;
    public float MinimumSpawnSeparation => minimumSpawnSeparation;
    /// <summary>
    /// True when every prefab in the table is (or carries) a creature, so the raid's enemy density applies to the group.
    /// Mixed tables such as event sites keep their authored count.
    /// </summary>
    public bool SpawnsEnemies => table != null && SpawnTable.AllCreatures(table.Entries);
    /// <summary>True once the group has been filled; each group spawns once per raid.</summary>
    public bool HasSpawned { get; private set; }

    /// <summary>The spawn points: the direct children, as authored (not what was spawned).</summary>
    public List<Transform> CollectPoints()
    {
        var points = new List<Transform>(transform.childCount);
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf && !spawned.Contains(child.gameObject))
            {
                points.Add(child);
            }
        }

        return points;
    }

    /// <summary>Fills some of the points, once, with normal density and no entry band.</summary>
    public IReadOnlyList<GameObject> Spawn(System.Random random) => Spawn(random, SpawnRules.Default);

    /// <summary>Fills some of the points, once. Deterministic for a given random state and rules; see RaidGenerator.</summary>
    public IReadOnlyList<GameObject> Spawn(System.Random random, SpawnRules rules)
    {
        if (HasSpawned)
        {
            return spawned;
        }

        HasSpawned = true;
        if (table == null)
        {
            Debug.LogWarning($"Spawn group '{name}' has no spawn table, so nothing spawns there.", this);
            return spawned;
        }

        // Everything is rolled before anything is placed, so the layout depends only on the seed.
        List<Transform> points = PointsFor(rules);
        List<(int point, GameObject prefab)> plan = Plan(random, rules, points);
        foreach ((int index, GameObject prefab) in plan)
        {
            PlaceAt(points[index], prefab, random);
        }

        return spawned;
    }

    /// <summary>The authored points inside this group's entry band.</summary>
    public List<Transform> PointsFor(SpawnRules rules) => PointsFor(rules, entryBand);

    /// <summary>The authored points inside the given entry band.</summary>
    public List<Transform> PointsFor(SpawnRules rules, EntryBand band)
    {
        List<Transform> points = CollectPoints();
        points.RemoveAll(point => !rules.Accepts(point.position, band));
        return points;
    }

    /// <summary>Hands the group's points to a raid spawn list; its trigger, stage and hunting then come from the list.</summary>
    public void Bind(RaidSpawnList list) => List = list;

    /// <summary>Marks the group filled without spawning (its list filled it, perhaps with nothing on these points).</summary>
    public void MarkSpawned() => HasSpawned = true;

    /// <summary>
    /// Places a prefab at one of this group's points with the group's scatter and facing, and keeps it as spawned here.
    /// Consumes the random state as Spawn does for one pick.
    /// </summary>
    /// <summary>Places one copy of <paramref name="prefab"/> at <paramref name="point"/>, scattered and turned by
    /// <paramref name="random"/>. Null, after the same rolls, for a creature while another game makes them (co-op).</summary>
    public GameObject PlaceAt(Transform point, GameObject prefab, System.Random random)
    {
        Vector2 offset = InsideCircle(random) * scatterRadius;
        float yaw = (float)random.NextDouble() * 360f;
        if (CreatureSource.Remote != null && prefab.GetComponentInChildren<MonsterAI>(true) != null)
        {
            // Kept with its rolls, should this game have to make it after all (the host left: PlaceSkipped).
            skipped.Add((point, prefab, offset, yaw));
            return null;
        }

        return Make(point, prefab, offset, yaw);
    }

    private GameObject Make(Transform point, GameObject prefab, Vector2 offset, float yaw)
    {
        Vector3 position = Place(point.position, offset);
        Quaternion rotation = randomFacing ? Quaternion.Euler(0f, yaw, 0f) : point.rotation;
        GameObject copy = Instantiate(prefab, position, rotation, transform);
        copy.name = prefab.name;
        spawned.Add(copy);
        return copy;
    }

    // Creatures not made here while another game made them (co-op: a friend on the host's run), with the rolls they had.
    private readonly List<(Transform point, GameObject prefab, Vector2 offset, float yaw)> skipped =
        new List<(Transform, GameObject, Vector2, float)>();

    /// <summary>
    /// Makes here the creatures skipped while the host's game made them, each where its own rolls put it, except within
    /// <paramref name="clear"/> metres of <paramref name="near"/>, where the host's own (handed back) already stand. The host
    /// gone, the rest of a follower's map was empty: a free run. Returns what was made.
    /// </summary>
    public List<GameObject> PlaceSkipped(Vector3 near, float clear)
    {
        var made = new List<GameObject>();
        foreach ((Transform point, GameObject prefab, Vector2 offset, float yaw) in skipped)
        {
            Vector3 flat = point != null ? point.position - near : Vector3.zero;
            flat.y = 0f;
            if (point != null && prefab != null && flat.sqrMagnitude > clear * clear)
            {
                made.Add(Make(point, prefab, offset, yaw));
            }
        }

        skipped.Clear();
        return made;
    }

    /// <summary>The count range after the raid's enemy density, before limiting to the points.</summary>
    public Vector2Int CountFor(SpawnRules rules)
    {
        if (!SpawnsEnemies)
        {
            return new Vector2Int(MinCount, MaxCount);
        }

        return new Vector2Int(rules.Scale(MinCount), rules.Scale(MaxCount));
    }

    /// <summary>
    /// Which of the points get which prefab, without placing anything; the editor uses it to preview a layout.
    /// Consumes the random state exactly as Spawn does before placing.
    /// </summary>
    public List<(int point, GameObject prefab)> Plan(System.Random random, SpawnRules rules, List<Transform> points)
    {
        if (table == null)
        {
            return new List<(int, GameObject)>();
        }

        // Only what appears at this raid's tier, as raid spawn lists and roamers take it (SpawnTable.Entry.AppearsAt): a group of
        // its own placed tier-gated creatures at tier I. With no raid (a scene played alone) every entry can appear.
        RaidSession run = GameSession.ActiveRaid;
        IReadOnlyList<SpawnTable.Entry> entries = run != null
            ? new List<SpawnTable.Entry>(table.Entries).FindAll(entry => entry.AppearsAt(run.Tier))
            : table.Entries;
        return Plan(random, CountFor(rules), points, minimumSpawnSeparation, entries);
    }

    /// <summary>
    /// Which of the points get which prefab: rolls how many in range (limited to the points), picks that many points kept
    /// separation apart, then rolls the entries for them. Shared by spawn groups and raid spawn lists.
    /// </summary>
    public static List<(int point, GameObject prefab)> Plan(System.Random random, Vector2Int range, List<Transform> points,
        float separation, IReadOnlyList<SpawnTable.Entry> entries)
    {
        var plan = new List<(int, GameObject)>();
        float minimumSpawnSeparation = separation;
        int amount = RaidLayout.RollCount(random, range.x, range.y, points.Count);
        List<int> chosen = RaidLayout.PickIndices(random, points.Count, amount);
        if (minimumSpawnSeparation > 0f && amount > 0)
        {
            chosen.Clear();
            foreach (int index in RaidLayout.PickIndices(random, points.Count, points.Count))
            {
                bool separated = true;
                foreach (int previous in chosen)
                {
                    if (Vector3.Distance(points[index].position, points[previous].position) < minimumSpawnSeparation)
                    {
                        separated = false;
                        break;
                    }
                }
                if (separated)
                {
                    chosen.Add(index);
                    if (chosen.Count >= amount)
                    {
                        break;
                    }
                }
            }
        }

        List<GameObject> prefabs = SpawnTable.Roll(entries, random, chosen.Count);
        for (int i = 0; i < prefabs.Count; i++)
        {
            plan.Add((chosen[i], prefabs[i]));
        }

        return plan;
    }

    private Vector3 Place(Vector3 point, Vector2 offset)
    {
        if (offset == Vector2.zero)
        {
            return point;
        }

        // A scattered spot is kept only if it is on walkable ground near the point; otherwise the point itself is used.
        var candidate = new Vector3(point.x + offset.x, point.y, point.z + offset.y);
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, scatterRadius, NavMesh.AllAreas))
        {
            return point;
        }

        // The navmesh floats a little above the ground; sit on the real surface.
        if (Physics.Raycast(hit.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            return ground.point;
        }

        return hit.position;
    }

    private static Vector2 InsideCircle(System.Random random)
    {
        double angle = random.NextDouble() * Mathf.PI * 2d;
        double radius = System.Math.Sqrt(random.NextDouble());
        return new Vector2((float)(System.Math.Cos(angle) * radius), (float)(System.Math.Sin(angle) * radius));
    }

#if UNITY_EDITOR
    /// <summary>For builders and tests: how this group takes the sites of its kind on a generated map.</summary>
    public void ConfigureSites(RaidSiteKind kind, float share, bool farthest, bool every)
    {
        site = kind;
        siteShare = share;
        farthestSites = farthest;
        everySite = every;
    }

    /// <summary>For builders and tests: the minimap's mark for this group's sites.</summary>
    public void ConfigureMapIcon(Sprite icon) => mapIcon = icon;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
        foreach (Transform point in transform)
        {
            if (spawned.Contains(point.gameObject))
            {
                continue;
            }

            Gizmos.DrawWireSphere(point.position, 0.4f);
            if (scatterRadius > 0f)
            {
                UnityEditor.Handles.color = new Color(1f, 0.75f, 0.2f, 0.25f);
                UnityEditor.Handles.DrawWireDisc(point.position, Vector3.up, scatterRadius);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        int points = CollectPoints().Count;
        string what = List != null ? "list " + List.name : table != null ? table.name : "no table";
        string when = trigger == SpawnTrigger.Stage ? $"stage {stage}" : trigger == SpawnTrigger.Alarm ? "on alarm" : "at start";
        if (huntPlayer)
        {
            when += ", hunts the player";
        }

        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"{name}\n{MinCount}–{Mathf.Min(MaxCount, points)} of {points} points\n{what}\n{when}");
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.5f);
        foreach (Transform point in transform)
        {
            Gizmos.DrawLine(transform.position, point.position);
        }
    }
#endif
}
