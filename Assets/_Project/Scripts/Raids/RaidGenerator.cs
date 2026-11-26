using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Lays out a raid while the loading screen is up: every SpawnGroup set to fill at the raid's start fills some of its points
/// from the raid's seed (RaidSession.Seed), so each run through the portal places creatures, caches and resources
/// differently, but always within the spots the level was designed with. It also rolls which extraction points are offered
/// this raid (ExtractionPoint.AvailabilityChance; at least one always is). Stage and alarm groups are filled later through
/// SpawnNow / SpawnStage, from the same seed. Extraction points and story objects are placed by hand and never move.
/// Runs as a scene setup step (see SceneTransition), before the raid's clock starts and its rules strengthen enemies.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidGenerator : MonoBehaviour, ISceneSetup
{
    [Tooltip("Uses this seed instead of the raid's when not 0: the same layout every time, for testing a level.")]
    [SerializeField] private int fixedSeed;
    [Tooltip("Spreads the spawning over several frames behind the loading screen once this many objects are placed.")]
    [SerializeField, Min(1)] private int spawnsPerFrame = 30;
    [SerializeField] private bool logLayout = true;

    public int SetupOrder => 0;

    /// <summary>The seed the current layout was made from.</summary>
    public int Seed { get; private set; }
    public bool IsGenerated { get; private set; }

    public IEnumerator Setup()
    {
        if (IsGenerated)
        {
            yield break;
        }

        IsGenerated = true;
        Seed = fixedSeed != 0 ? fixedSeed
            : RaidDirector.Current != null ? RaidDirector.Current.Session.Seed
            : GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed
            : RaidSession.NewSeed();

        var log = logLayout ? new StringBuilder() : null;
        ChooseExtractions(log);

        BindLists(log);
        SpawnGroup[] groups = FindObjectsByType<SpawnGroup>(FindObjectsInactive.Exclude);
        int total = 0;
        int sinceYield = 0;
        int filled = 0;
        foreach (RaidSpawnList list in lists.Keys)
        {
            if (list.trigger != SpawnTrigger.RaidStart)
            {
                log?.Append($"\n  [{list.name}]: waits ({(list.trigger == SpawnTrigger.Stage ? "stage " + list.stage : "alarm")})");
                continue;
            }

            int placed = SpawnList(list).Count;
            filled++;
            total += placed;
            sinceYield += placed;
            log?.Append($"\n  [{list.name}]: {placed} of {PointsOf(list).Count}");
            if (sinceYield >= spawnsPerFrame)
            {
                sinceYield = 0;
                yield return null;
            }
        }

        foreach (SpawnGroup group in groups)
        {
            if (group == null || group.gameObject.scene != gameObject.scene || group.List != null)
            {
                continue;
            }

            // Stage and alarm groups wait for the raid to call them (SpawnNow).
            if (group.Trigger != SpawnTrigger.RaidStart)
            {
                log?.Append($"\n  {group.name}: waits ({(group.Trigger == SpawnTrigger.Stage ? "stage " + group.Stage : "alarm")})");
                continue;
            }

            int placed = SpawnNow(group).Count;
            filled++;
            total += placed;
            sinceYield += placed;
            log?.Append($"\n  {group.name}: {placed} of {group.PointsFor(Rules).Count}");
            if (sinceYield >= spawnsPerFrame)
            {
                sinceYield = 0;
                yield return null;
            }
        }

        if (log != null)
        {
            Debug.Log($"Raid layout from seed {Seed}: {total} spawned in {filled} groups " +
                      $"(enemy density x{Rules.EnemyDensity:0.##}, entry {(Rules.Entry.HasValue ? "set" : "none")}).{log}", this);
        }
    }

    /// <summary>
    /// Fills a group now from this raid's seed (the same group always rolls the same way in the same raid) and returns
    /// what it spawned. A group fills only once; later calls return the same spawns. A group used by a raid spawn list
    /// fills the whole list, and everything the list placed is returned.
    /// </summary>
    public IReadOnlyList<GameObject> SpawnNow(SpawnGroup group)
    {
        if (group == null)
        {
            return System.Array.Empty<GameObject>();
        }

        EnsureSeed();
        BindLists(null);
        if (group.List != null && lists.ContainsKey(group.List))
        {
            return SpawnList(group.List);
        }

        return group.Spawn(new System.Random(RaidLayout.SeedFor(Seed, PathOf(group.transform))), Rules);
    }

    /// <summary>The raid's spawn lists bound to their groups in this scene, in the definition's order.</summary>
    public IEnumerable<RaidSpawnList> Lists
    {
        get
        {
            BindLists(null);
            return lists.Keys;
        }
    }

    private readonly Dictionary<RaidSpawnList, List<SpawnGroup>> lists = new Dictionary<RaidSpawnList, List<SpawnGroup>>();
    private readonly Dictionary<RaidSpawnList, List<GameObject>> listSpawns = new Dictionary<RaidSpawnList, List<GameObject>>();
    private bool listsBound;

    private void EnsureSeed()
    {
        if (Seed == 0)
        {
            Seed = fixedSeed != 0 ? fixedSeed : RaidDirector.Current != null ? RaidDirector.Current.Session.Seed : RaidSession.NewSeed();
        }
    }

    // Each list takes the scene's Spawn Groups it names (the first of a name; a group serves one list only).
    private void BindLists(StringBuilder log)
    {
        if (listsBound)
        {
            return;
        }

        listsBound = true;
        RaidDefinition raid = Definition;
        if (raid == null || raid.SpawnLists.Count == 0)
        {
            return;
        }

        var byName = new Dictionary<string, SpawnGroup>();
        foreach (SpawnGroup group in FindObjectsByType<SpawnGroup>(FindObjectsInactive.Exclude))
        {
            if (group.gameObject.scene == gameObject.scene && !byName.ContainsKey(group.name))
            {
                byName.Add(group.name, group);
            }
        }

        foreach (RaidSpawnList list in raid.SpawnLists)
        {
            if (list == null || lists.ContainsKey(list))
            {
                continue;
            }

            var groups = new List<SpawnGroup>();
            foreach (string groupName in list.pointGroups)
            {
                if (string.IsNullOrEmpty(groupName) || !byName.TryGetValue(groupName, out SpawnGroup group))
                {
                    Debug.LogWarning($"Raid spawn list '{list.name}' names a Spawn Group '{groupName}' that isn't in the scene.", this);
                    continue;
                }

                if (group.List != null)
                {
                    log?.Append($"\n  ! {groupName} is already used by list '{group.List.name}', skipped for '{list.name}'");
                    continue;
                }

                group.Bind(list);
                groups.Add(group);
            }

            lists.Add(list, groups);
        }
    }

    // All the points of a list's groups in the list's entry band, with the group owning each.
    private List<(SpawnGroup group, Transform point)> PointsOf(RaidSpawnList list)
    {
        var points = new List<(SpawnGroup, Transform)>();
        foreach (SpawnGroup group in lists[list])
        {
            foreach (Transform point in group.PointsFor(Rules, list.entryBand))
            {
                points.Add((group, point));
            }
        }

        return points;
    }

    // Fills a raid spawn list once over all its groups' points; seeded by the list's name.
    private IReadOnlyList<GameObject> SpawnList(RaidSpawnList list)
    {
        if (listSpawns.TryGetValue(list, out List<GameObject> done))
        {
            return done;
        }

        EnsureSeed();
        var placed = new List<GameObject>();
        listSpawns.Add(list, placed);
        List<(SpawnGroup group, Transform point)> points = PointsOf(list);
        var transforms = new List<Transform>(points.Count);
        foreach ((SpawnGroup _, Transform point) in points)
        {
            transforms.Add(point);
        }

        var random = new System.Random(RaidLayout.SeedFor(Seed, "List:" + list.name));
        // At a raid without a session (a scene played on its own) every entry can appear.
        IReadOnlyList<SpawnTable.Entry> entries = GameSession.ActiveRaid != null ? list.EntriesFor(GameSession.ActiveRaid.Tier) : list.entries;
        List<(int point, GameObject prefab)> plan = SpawnGroup.Plan(random, list.CountFor(Rules), transforms, list.separation, entries);
        List<Vector3> positions = list.PlacesPacks ? transforms.ConvertAll(point => point.position) : null;
        Vector2Int pack = positions != null ? list.PackFor(Rules) : Vector2Int.one;
        foreach ((int index, GameObject prefab) in plan)
        {
            Place(index, prefab);
            // A pack: others of the same kind on the site's other points (Docs/RaidLayoutAnalysis_2026-11-26.md R4). Rolled only for lists
            // that place packs, so every other list draws exactly as before.
            if (positions == null)
            {
                continue;
            }

            int size = RaidLayout.RollCount(random, pack.x, pack.y, int.MaxValue);
            if (AloneIn(entries, prefab))
            {
                continue;
            }

            foreach (int other in RaidSpawnList.PackPoints(positions, index, size - 1))
            {
                Place(other, prefab);
            }
        }

        void Place(int index, GameObject prefab)
        {
            GameObject made = points[index].group.PlaceAt(points[index].point, prefab, random);
            if (made == null)
            {
                return;
            }

            placed.Add(made);
            if (list.walksTheTrails)
            {
                WalkTheTrails(made);
            }
        }

        foreach (SpawnGroup group in lists[list])
        {
            group.MarkSpawned();
        }

        return placed;
    }

    private static bool AloneIn(IReadOnlyList<SpawnTable.Entry> entries, GameObject prefab)
    {
        foreach (SpawnTable.Entry entry in entries)
        {
            if (entry.prefab == prefab)
            {
                return entry.alone;
            }
        }

        return false;
    }

    // How many of the map's camps and patrol spots a walker's round takes, and the least metres to the first.
    private const int RoundStops = 3;
    private const float RoundFirstStop = 15f;

    /// <summary>
    /// A creature that walks the trails (Raid Spawn List › Walks The Trails, Docs/RaidLayoutAnalysis_2026-11-26.md R10): a round from where
    /// it stands through the nearest camps and patrol spots of the generated map, each next stop the nearest left. Nothing on a painted map.
    /// </summary>
    public static void WalkTheTrails(GameObject made)
    {
        RaidMap map = FindAnyObjectByType<RaidMap>();
        if (map == null || map.Places.Count == 0)
        {
            return;
        }

        List<Vector3> stops = RoundFrom(made.transform.position, map.Places);
        if (stops.Count < 2)
        {
            return;
        }

        // Under the map, not the spawn group: a group's children are its spawn points.
        var route = new GameObject(made.name + " Round").transform;
        route.SetParent(map.transform, false);
        foreach (Vector3 stop in stops)
        {
            var waypoint = new GameObject("Stop").transform;
            waypoint.SetParent(route, false);
            waypoint.position = stop;
        }

        foreach (MonsterAI monster in made.GetComponentsInChildren<MonsterAI>(true))
        {
            if (!monster.TryGetComponent(out MonsterPatrol patrol))
            {
                patrol = monster.gameObject.AddComponent<MonsterPatrol>();
            }

            patrol.SetRoute(route);
        }
    }

    /// <summary>A round from <paramref name="from"/> through the nearest camps and patrol spots, each next stop the nearest left. Pure, for tests.</summary>
    public static List<Vector3> RoundFrom(Vector3 from, IReadOnlyList<RaidPlace> places)
    {
        var left = new List<Vector3>();
        foreach (RaidPlace place in places)
        {
            Vector3 flat = place.Position - from;
            flat.y = 0f;
            if ((place.Kind == RaidSiteKind.EnemyCamp || place.Kind == RaidSiteKind.Patrol) && flat.magnitude >= RoundFirstStop)
            {
                left.Add(place.Position);
            }
        }

        var round = new List<Vector3> { from };
        Vector3 at = from;
        for (int i = 0; i < RoundStops && left.Count > 0; i++)
        {
            int nearest = 0;
            for (int k = 1; k < left.Count; k++)
            {
                if ((left[k] - at).sqrMagnitude < (left[nearest] - at).sqrMagnitude)
                {
                    nearest = k;
                }
            }

            at = left[nearest];
            round.Add(at);
            left.RemoveAt(nearest);
        }

        return round;
    }

    private RaidDefinition Definition => RaidDirector.Current != null ? RaidDirector.Current.Session.Definition
        : GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Definition : null;

    /// <summary>The raid's spawn rules (enemy density, entry bands) measured from the map's player start.</summary>
    public SpawnRules Rules
    {
        get
        {
            if (!rules.HasValue)
            {
                RaidDefinition raid = Definition;
                Vector3? entry = null;
                foreach (RaidMap map in FindObjectsByType<RaidMap>(FindObjectsInactive.Exclude))
                {
                    if (map.gameObject.scene == gameObject.scene && map.PlayerStart != null)
                    {
                        entry = map.PlayerStart.position;
                    }
                }

                rules = raid != null ? raid.GetSpawnRules(entry) : SpawnRules.Default.WithEntry(entry);
            }

            return rules.Value;
        }
    }

    private SpawnRules? rules;

    /// <summary>
    /// Fills every spawn list and group in this scene waiting for the stage, and returns the groups that were filled
    /// (each holds what was placed on its points).
    /// </summary>
    public List<SpawnGroup> SpawnStage(int stage)
    {
        BindLists(null);
        var woken = new List<SpawnGroup>();
        foreach (KeyValuePair<RaidSpawnList, List<SpawnGroup>> pair in lists)
        {
            if (pair.Key.trigger == SpawnTrigger.Stage && pair.Key.stage == stage && !listSpawns.ContainsKey(pair.Key))
            {
                SpawnList(pair.Key);
                woken.AddRange(pair.Value);
            }
        }

        foreach (SpawnGroup group in FindObjectsByType<SpawnGroup>(FindObjectsInactive.Exclude))
        {
            if (group.gameObject.scene == gameObject.scene && group.List == null && group.Trigger == SpawnTrigger.Stage
                && group.Stage == stage && !group.HasSpawned)
            {
                SpawnNow(group);
                woken.Add(group);
            }
        }

        return woken;
    }

    // Each extraction point rolls its availability chance; the ones not offered this raid are switched off. One always stays.
    private void ChooseExtractions(StringBuilder log)
    {
        var points = new List<ExtractionPoint>();
        foreach (ExtractionPoint point in FindObjectsByType<ExtractionPoint>(FindObjectsInactive.Exclude))
        {
            if (point.gameObject.scene == gameObject.scene)
            {
                points.Add(point);
            }
        }

        if (points.Count == 0)
        {
            return;
        }

        // Sorted by path so the roll doesn't depend on the order Unity happens to find them in.
        points.Sort((a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
        var chances = new List<float>(points.Count);
        foreach (ExtractionPoint point in points)
        {
            chances.Add(point.AvailabilityChance);
        }

        bool[] available = RaidLayout.PickAvailable(new System.Random(RaidLayout.SeedFor(Seed, "Extraction Points")), chances);
        for (int i = 0; i < points.Count; i++)
        {
            log?.Append($"\n  {points[i].DisplayName}: {(available[i] ? "available" : "dark this raid")}");
            if (!available[i])
            {
                points[i].gameObject.SetActive(false);
            }
        }
    }

    // The group's place in the hierarchy names it for its seed, so renaming or moving a group reshuffles only that one.
    /// <summary>A spawn group of this raid by its place in the hierarchy (<see cref="PathOf"/>, the same in every game
    /// on one run), or null.</summary>
    public SpawnGroup FindGroup(string path)
    {
        foreach (SpawnGroup group in FindObjectsByType<SpawnGroup>(FindObjectsInactive.Exclude))
        {
            if (group.gameObject.scene == gameObject.scene && PathOf(group.transform) == path)
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>Where <paramref name="target"/> sits in the hierarchy: seeds its group's rolls, and names it between games.</summary>
    public static string PathOf(Transform target)
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}
