using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>What one generated map is like, measured on its plan and height field (no terrain): for the generator report and tests.</summary>
public sealed class RaidMapMeasure
{
    public int Seed;
    public string Mood;
    public string Shape;
    public int Regions;
    public int Rooms;
    public int Links;
    public int Loops;
    public int DeadEnds;
    public bool TwoRoutes;
    public float ShortRoute;
    public float LongRoute;
    public float LowestGround;
    public float HighestGround;
    public float SteepestTrail;
    public float BiggestStep;
    public int Ravines;
    public float RavineLength;
    public int Descents;
    public int Rivers;
    public int Hills;
    public int Boulders;
    public int Landmarks;
    public int Spires;
    public int Sites;
    public float NearestSites;
    /// <summary>Landmarks placed at nodes (G4), short flags beside the roads, and the longest walk along the short way with neither
    /// within <see cref="SignReach"/> metres: how long the player goes without a sign.</summary>
    public int NodeLandmarks;
    public int Flags;
    public float LongestUnmarked;
    /// <summary>Valley maps: the river's crossings and how many carry a bridge (G8).</summary>
    public int Crossings;
    public int Bridges;
    /// <summary>The share of the camps standing in the Lair district, on a map with districts (G1); 0 on a map of one.</summary>
    public float CampsInLair;
    public long PlanMilliseconds;
    public long FieldMilliseconds;

    private const float TrailStep = 0.5f;
    public const float SignReach = 40f;
    private const float SignStep = 2f;
    private const int HeightSamples = 129;

    public static RaidMapMeasure Of(RaidLayoutRules rules, float size, int seed)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, size, seed);
        long planned = clock.ElapsedMilliseconds;
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        var measure = new RaidMapMeasure
        {
            Seed = seed,
            Mood = plan.Mood.name,
            Shape = plan.Shape.shape.ToString(),
            Regions = Mathf.Max(1, plan.Regions.Count),
            Rooms = plan.Rooms.Count,
            Links = plan.Links.Count,
            Loops = plan.Links.Count - plan.Rooms.Count + 1,
            DeadEnds = plan.Rooms.Count(room => room.Links == 1),
            TwoRoutes = plan.LongRoute.Count > 0,
            ShortRoute = RouteLength(plan, plan.ShortRoute),
            LongRoute = RouteLength(plan, plan.LongRoute),
            Ravines = plan.Ravines.Count,
            RavineLength = plan.Ravines.Sum(ravine => Length(ravine.Line)),
            Descents = plan.Ravines.Count(ravine => ravine.Descent),
            Rivers = plan.Rivers.Count,
            Hills = plan.Hills.Count,
            Boulders = plan.Boulders.Count,
            Landmarks = plan.Landmarks.Count,
            Spires = plan.Spires.Count,
            Sites = plan.Sites.Count,
            NodeLandmarks = plan.Landmarks.Count(landmark => landmark.Rule >= 0),
            Flags = plan.Flags.Count,
            Crossings = plan.Crossings.Count,
            Bridges = plan.Crossings.Count(crossing => crossing.Bridge),
            PlanMilliseconds = planned,
            FieldMilliseconds = clock.ElapsedMilliseconds - planned,
        };

        float[,] heights = field.Metres(HeightSamples, out measure.HighestGround);
        measure.LowestGround = float.MaxValue;
        foreach (float height in heights)
        {
            measure.LowestGround = Mathf.Min(measure.LowestGround, height);
        }

        var lines = new List<List<Vector2>>(plan.Trails);
        lines.AddRange(plan.Spurs);
        foreach (List<Vector2> trail in lines)
        {
            for (int i = 0; i + 1 < trail.Count; i++)
            {
                float length = Vector2.Distance(trail[i], trail[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / TrailStep));
                float last = field.HeightAt(trail[i].x / size, trail[i].y / size);
                for (int k = 1; k <= steps; k++)
                {
                    Vector2 at = Vector2.Lerp(trail[i], trail[i + 1], k / (float)steps);
                    float height = field.HeightAt(at.x / size, at.y / size);
                    float rise = Mathf.Abs(height - last);
                    measure.BiggestStep = Mathf.Max(measure.BiggestStep, rise);
                    measure.SteepestTrail = Mathf.Max(measure.SteepestTrail, Mathf.Atan(rise / (length / steps)) * Mathf.Rad2Deg);
                    last = height;
                }
            }
        }

        measure.LongestUnmarked = UnmarkedWalk(plan);
        if (plan.Regions.Count > 1)
        {
            List<RaidSite> camps = plan.SitesOf(RaidSiteKind.EnemyCamp);
            int inLair = camps.Count(camp => plan.Regions[plan.RegionIndexAt(camp.Position * size)].Role == RaidDistrictRole.Lair);
            measure.CampsInLair = camps.Count > 0 ? inLair / (float)camps.Count : 0f;
        }

        measure.NearestSites = float.MaxValue;
        for (int i = 0; i < plan.Sites.Count; i++)
        {
            for (int k = i + 1; k < plan.Sites.Count; k++)
            {
                measure.NearestSites = Mathf.Min(measure.NearestSites, Vector2.Distance(plan.Sites[i].Position, plan.Sites[k].Position) * size);
            }
        }

        return measure;
    }

    private static float RouteLength(RaidLayoutPlan plan, HashSet<int> links) => links.Sum(link => Length(plan.Trails[link]));

    // Walking the short way from the start, the longest stretch with no landmark or flag within SignReach metres.
    private static float UnmarkedWalk(RaidLayoutPlan plan)
    {
        List<Vector2> line = RaidLayoutPlanner.RouteLine(plan, plan.ShortRoute);
        var signs = plan.Landmarks.Select(landmark => landmark.Position).Concat(plan.Flags.Select(flag => flag.Position)).ToList();
        float longest = 0f, stretch = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            float length = Vector2.Distance(line[i], line[i + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / SignStep));
            for (int k = 0; k < steps; k++)
            {
                Vector2 at = Vector2.Lerp(line[i], line[i + 1], k / (float)steps);
                bool seen = signs.Any(sign => (sign - at).sqrMagnitude < SignReach * SignReach);
                stretch = seen ? 0f : stretch + length / steps;
                longest = Mathf.Max(longest, stretch);
            }
        }

        return longest;
    }

    private static float Length(List<Vector2> line)
    {
        float length = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            length += Vector2.Distance(line[i], line[i + 1]);
        }

        return length;
    }
}

/// <summary>
/// Safe Haven › Content › Raid Generator Report: lays out many runs of a raid (plans and height fields only, no terrain) and sums them
/// up per mood: routes, height, trails, ravines, rivers, what stands on the map and how long it took. For tuning the rules by numbers,
/// and for catching bad maps (steep trails, steps, no second way out) before a playtest does.
/// </summary>
public sealed class RaidGeneratorReport : EditorWindow
{
    private RaidDefinition raid;
    private int seeds = 24;
    private bool large;
    private readonly List<RaidMapMeasure> measures = new List<RaidMapMeasure>();
    private Vector2 scroll;

    [MenuItem("Safe Haven/Content/Raid Generator Report")]
    private static void Open() => GetWindow<RaidGeneratorReport>("Raid Generator Report");

    private void OnEnable()
    {
        if (raid == null)
        {
            raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        }
    }

    private void OnGUI()
    {
        raid = (RaidDefinition)EditorGUILayout.ObjectField("Raid", raid, typeof(RaidDefinition), false);
        seeds = EditorGUILayout.IntSlider("Runs", seeds, 4, 200);
        large = EditorGUILayout.Toggle("Large", large);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = raid != null && raid.LayoutRules != null;
            if (GUILayout.Button("Lay out the runs"))
            {
                Run();
            }

            GUI.enabled = measures.Count > 0;
            if (GUILayout.Button("Save as CSV"))
            {
                string path = EditorUtility.SaveFilePanel("Save report", "", "RaidGeneratorReport.csv", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, Csv(measures));
                }
            }

            GUI.enabled = true;
        }

        if (measures.Count == 0)
        {
            EditorGUILayout.HelpBox("Lays out runs from their plans and height fields, as the game would, and sums them up per mood. No scene is touched.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (IGrouping<string, RaidMapMeasure> mood in measures.GroupBy(one => one.Mood).OrderBy(group => group.Key))
        {
            EditorGUILayout.LabelField($"{mood.Key} ({mood.Count()} runs)", EditorStyles.boldLabel);
            Row("Clearings, trails, loops, dead ends", $"{mood.Average(m => m.Rooms):0.#}, {mood.Average(m => m.Links):0.#}, {mood.Average(m => m.Loops):0.#}, {mood.Average(m => m.DeadEnds):0.#}");
            Row("Two ways out", $"{mood.Count(m => m.TwoRoutes) * 100f / mood.Count():0}%, short {mood.Average(m => m.ShortRoute):0} m, long {Average(mood.Where(m => m.TwoRoutes), m => m.LongRoute):0} m");
            Row("Ground (low to high)", $"{mood.Average(m => m.LowestGround):0.#} to {mood.Average(m => m.HighestGround):0.#} m");
            Row("Trails: steepest, biggest step", $"{mood.Max(m => m.SteepestTrail):0}°, {mood.Max(m => m.BiggestStep):0.00} m", mood.Max(m => m.SteepestTrail) > 36f || mood.Max(m => m.BiggestStep) > 0.4f);
            Row("Ravines (length, ways down), rivers", $"{mood.Average(m => m.Ravines):0.#} ({mood.Average(m => m.RavineLength):0} m, {mood.Average(m => m.Descents):0.#}), {mood.Average(m => m.Rivers):0.#}");
            Row("Hills, boulders, landmarks, spires", $"{mood.Average(m => m.Hills):0.#}, {mood.Average(m => m.Boulders):0.#}, {mood.Average(m => m.Landmarks):0.#}, {mood.Average(m => m.Spires):0.#}");
            Row("Sites, closest two", $"{mood.Average(m => m.Sites):0.#}, {mood.Min(m => m.NearestSites):0.#} m");
            Row("Regions or districts, camps in the Lair", $"{mood.Average(m => m.Regions):0.#}, {mood.Average(m => m.CampsInLair) * 100f:0}%");
            Row("Node landmarks, flags, longest unmarked walk", $"{mood.Average(m => m.NodeLandmarks):0.#}, {mood.Average(m => m.Flags):0.#}, {mood.Max(m => m.LongestUnmarked):0} m");
            if (mood.Any(m => m.Crossings > 0))
            {
                Row("River crossings (bridges)", $"{mood.Average(m => m.Crossings):0.#} ({mood.Average(m => m.Bridges):0.#}), most {mood.Max(m => m.Crossings)}");
            }

            Row("Plan, field", $"{mood.Average(m => m.PlanMilliseconds):0} ms, {mood.Average(m => m.FieldMilliseconds):0} ms");
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();
    }

    private static float Average(IEnumerable<RaidMapMeasure> measures, System.Func<RaidMapMeasure, float> value)
    {
        List<RaidMapMeasure> list = measures.ToList();
        return list.Count > 0 ? list.Average(value) : 0f;
    }

    private static void Row(string label, string value, bool warn = false)
    {
        Color before = GUI.color;
        if (warn)
        {
            GUI.color = new Color(1f, 0.6f, 0.4f);
        }

        EditorGUILayout.LabelField(label, value);
        GUI.color = before;
    }

    private void Run()
    {
        measures.Clear();
        RaidLayoutRules rules = large && raid.CanBeLarge ? raid.LayoutRules.ScaledBy(raid.LargeRaid.widthScale) : raid.LayoutRules;
        float size = raid.LayoutRules.MapSize * (large && raid.CanBeLarge ? raid.LargeRaid.widthScale : 1f);
        try
        {
            for (int i = 0; i < seeds; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Raid Generator Report", $"Run {i + 1} of {seeds}", i / (float)seeds))
                {
                    break;
                }

                measures.Add(RaidMapMeasure.Of(rules, size, 1000 + i * 7919));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (rules != raid.LayoutRules)
            {
                DestroyImmediate(rules);
            }
        }
    }

    /// <summary>Every run, one row each.</summary>
    public static string Csv(IEnumerable<RaidMapMeasure> measures)
    {
        var csv = new StringBuilder("Seed,Mood,Shape,Regions,Rooms,Links,Loops,DeadEnds,TwoRoutes,ShortRoute,LongRoute,Low,High,SteepestTrail,BiggestStep,Ravines,RavineLength,Descents,Rivers,Hills,Boulders,Landmarks,Spires,Sites,NearestSites,NodeLandmarks,Flags,LongestUnmarked,Crossings,Bridges,CampsInLair,PlanMs,FieldMs\n");
        foreach (RaidMapMeasure m in measures)
        {
            csv.AppendLine(string.Join(",", new object[]
            {
                m.Seed, m.Mood, m.Shape, m.Regions, m.Rooms, m.Links, m.Loops, m.DeadEnds, m.TwoRoutes, m.ShortRoute, m.LongRoute, m.LowestGround, m.HighestGround,
                m.SteepestTrail, m.BiggestStep, m.Ravines, m.RavineLength, m.Descents, m.Rivers, m.Hills, m.Boulders, m.Landmarks, m.Spires, m.Sites, m.NearestSites,
                m.NodeLandmarks, m.Flags, m.LongestUnmarked, m.Crossings, m.Bridges, m.CampsInLair, m.PlanMilliseconds, m.FieldMilliseconds,
            }.Select(value => System.Convert.ToString(value, CultureInfo.InvariantCulture))));
        }

        return csv.ToString();
    }
}
