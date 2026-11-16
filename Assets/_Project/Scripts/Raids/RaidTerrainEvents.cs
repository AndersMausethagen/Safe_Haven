using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The land changes as a raid's danger rises: at one stage a rockfall closes a neck where a trail crosses a ravine (only one that leaves
/// another way to every exit), at a later one the mist in the ravines thickens. Added per raid in the Systems Editor (Raids › Raid Systems);
/// only maps with ravines have necks to close.
/// </summary>
[Serializable]
public sealed class TerrainEventsSystem : RaidSystem
{
    [Tooltip("The danger stage (1 is the first) at which rocks come down on necks. 0: never.")]
    [SerializeField, Min(0)] private int rockfallStage = 2;
    [Tooltip("How many necks the rockfall closes.")]
    [SerializeField, Min(0)] private int rockfalls = 1;
    [Tooltip("The danger stage at which the mist in the ravines thickens. 0: never.")]
    [SerializeField, Min(0)] private int mistStage = 3;
    [Tooltip("How many times thicker the mist in the ravines grows.")]
    [SerializeField, Range(1f, 4f)] private float mistThickens = 2f;

    public int RockfallStage => rockfallStage;
    public int Rockfalls => rockfalls;
    public int MistStage => mistStage;
    public float MistThickens => mistThickens;

    public override string DisplayName => "Terrain Events";
    public override string Description => "As the danger rises, rocks come down on a ravine crossing (never the last way to an exit) and the mist in the ravines thickens.";

    public override void Install(RaidDirector director) => director.gameObject.AddComponent<RaidTerrainEvents>().Configure(this, director);
}

/// <summary>Runs a raid's Terrain Events (TerrainEventsSystem) on its director.</summary>
public sealed class RaidTerrainEvents : MonoBehaviour
{
    private const float DropHeight = 14f;
    private const float DropSeconds = 0.9f;
    private const float PileSpread = 2f;
    private const float PileWidest = 5f;

    private TerrainEventsSystem settings;
    private RaidDirector director;

    public void Configure(TerrainEventsSystem system, RaidDirector owner)
    {
        settings = system;
        director = owner;
        director.StageChanged += OnStage;
    }

    private void OnDestroy()
    {
        if (director != null)
        {
            director.StageChanged -= OnStage;
        }
    }

    private void OnStage(int stage, RaidStage _)
    {
        RaidMap map = FindAnyObjectByType<RaidMap>();
        if (map == null || map.Layout == null)
        {
            return;
        }

        if (stage == settings.RockfallStage)
        {
            Rockfall(map);
        }

        if (stage == settings.MistStage)
        {
            ThickenMist(map);
        }
    }

    private void Rockfall(RaidMap map)
    {
        CliffKit kit = map.Rules != null ? map.Rules.BoulderKit : null;
        if (kit == null || kit.Pieces.Count == 0)
        {
            return;
        }

        List<(Vector2 at, int link)> necks = ClosableNecks(map.Layout);
        var random = new System.Random(RaidLayout.SeedFor(director.Session.Seed, "Rockfall"));
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        // Every exit must stay reachable with all the falls so far, not with each alone: two could cut one off together.
        var closed = new HashSet<int>();
        for (int fallen = 0; fallen < settings.Rockfalls && necks.Count > 0;)
        {
            int pick = random.Next(necks.Count);
            (Vector2 neck, int link) = necks[pick];
            necks.RemoveAt(pick);
            if (closed.Add(link) && !StillReachable(map.Layout, closed))
            {
                closed.Remove(link);
                continue;
            }

            fallen++;
            var at = origin + new Vector3(neck.x, 0f, neck.y);
            at.y = terrain.SampleHeight(at) + origin.y;
            StartCoroutine(Fall(kit, at, terrain, random));
            GameMessages.Post($"Rocks have come down on the way across the gorge {Compass(at)}.", MessageKind.Warning);
        }
    }

    // Each a few blocks, dropping in from above and settling across the trail; solid, and cut out of the navmesh, so creatures go round too.
    private IEnumerator Fall(CliffKit kit, Vector3 at, Terrain terrain, System.Random random)
    {
        var blocks = new List<(Transform block, Vector3 rest)>();
        for (int i = 0; i < 3; i++)
        {
            CliffKit.Piece piece = kit.Pick(random);
            if (piece == null)
            {
                continue;
            }

            Vector3 spot = at + new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * PileSpread * 2f;
            spot.y = terrain.SampleHeight(spot) + terrain.transform.position.y;
            GameObject made = Instantiate(piece.prefab, spot + Vector3.up * DropHeight, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * RaidRavineCrags.StandUp(piece));
            made.name = "Rockfall";
            made.transform.localScale = piece.prefab.transform.localScale * Mathf.Min(1f, PileWidest / Mathf.Max(0.5f, Mathf.Max(piece.size.x, piece.size.z)));
            foreach (NavMeshObstacle obstacle in made.GetComponentsInChildren<NavMeshObstacle>())
            {
                obstacle.carving = true;
            }

            blocks.Add((made.transform, spot));
        }

        for (float time = 0f; time < DropSeconds; time += Time.deltaTime)
        {
            float t = time / DropSeconds;
            foreach ((Transform block, Vector3 rest) in blocks)
            {
                block.position = rest + Vector3.up * DropHeight * (1f - t * t);
            }

            yield return null;
        }

        foreach ((Transform block, Vector3 rest) in blocks)
        {
            block.position = rest;
        }
    }

    // Where a trail crosses a ravine on a neck whose trail isn't needed: without its link every exit can still be reached from the start.
    private static List<(Vector2 at, int link)> ClosableNecks(RaidLayoutPlan plan)
    {
        var necks = new List<(Vector2, int)>();
        for (int link = 0; link < plan.Links.Count && link < plan.Trails.Count; link++)
        {
            if (!StillReachable(plan, new HashSet<int> { link }))
            {
                continue;
            }

            List<Vector2> trail = plan.Trails[link];
            foreach (RaidLayoutRavine ravine in plan.Ravines)
            {
                for (int i = 0; i + 1 < trail.Count; i++)
                {
                    for (int k = 0; k + 1 < ravine.Line.Count; k++)
                    {
                        if (Intersect(trail[i], trail[i + 1], ravine.Line[k], ravine.Line[k + 1], out Vector2 crossing))
                        {
                            necks.Add((crossing, link));
                        }
                    }
                }
            }
        }

        return necks;
    }

    private static bool StillReachable(RaidLayoutPlan plan, HashSet<int> without)
    {
        var reached = new bool[plan.Rooms.Count];
        var open = new Queue<int>();
        reached[0] = true;
        open.Enqueue(0);
        while (open.Count > 0)
        {
            int room = open.Dequeue();
            for (int link = 0; link < plan.Links.Count; link++)
            {
                if (without.Contains(link))
                {
                    continue;
                }

                (int a, int b) = plan.Links[link];
                int other = a == room ? b : b == room ? a : -1;
                if (other >= 0 && !reached[other])
                {
                    reached[other] = true;
                    open.Enqueue(other);
                }
            }
        }

        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            if (plan.Rooms[i].Role == RaidRoomRole.Exit && !reached[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
    {
        at = default;
        Vector2 r = b - a, s = d - c;
        float denominator = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(denominator) < 1e-5f)
        {
            return false;
        }

        float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / denominator;
        float u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / denominator;
        if (t < 0f || t > 1f || u < 0f || u > 1f)
        {
            return false;
        }

        at = a + r * t;
        return true;
    }

    private static string Compass(Vector3 at)
    {
        Player player = Player.Current;
        if (player == null)
        {
            return "nearby";
        }

        Vector3 to = at - player.transform.position;
        string[] names = { "to the north", "to the north-east", "to the east", "to the south-east", "to the south", "to the south-west", "to the west", "to the north-west" };
        float angle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        return names[Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8];
    }

    private void ThickenMist(RaidMap map)
    {
        Transform ravines = map.transform.Find(RaidMapGenerator.RavinesName);
        if (ravines == null)
        {
            return;
        }

        foreach (FogArea area in ravines.GetComponentsInChildren<FogArea>())
        {
            area.Configure(area.Density * settings.MistThickens, area.Radius, area.Height);
        }
    }
}
