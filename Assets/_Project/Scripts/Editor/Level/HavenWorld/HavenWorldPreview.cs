using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Draws a HavenWorldPlan as a map, north up, and counts what is in it: the terraces shaded like a relief map, water, the
/// cliffs and the ways up them, paths by kind, glades, places, where creatures and game spawn, what can be gathered, mist,
/// and what is wrong (ground nothing climbs to, walks that pass nothing, paths that end at nothing, empty ground). Nothing
/// here touches a scene: a plan takes about a second, so a recipe can be tuned by eye before the world is generated from it
/// (HavenWorldPlannerWindow shows it; AgentScripts/ProbeHavenPlan.cs saves it as a picture).
/// </summary>
public static class HavenWorldPreview
{
    [System.Flags]
    public enum Layers
    {
        Water = 1,
        Cliffs = 2,
        Paths = 4,
        Glades = 8,
        Places = 16,
        Creatures = 32,
        Resources = 64,
        Scenes = 128,
        Mist = 256,
        Problems = 512,
        All = ~0,
    }

    private static readonly Color32 Meadow = new Color32(128, 158, 84, 255);
    private static readonly Color32 Woods = new Color32(56, 88, 60, 255);
    private static readonly Color32 GladeGround = new Color32(152, 172, 96, 255);
    private static readonly Color32 Mountain = new Color32(130, 126, 118, 255);
    private static readonly Color32 Bog = new Color32(100, 98, 62, 255);
    private static readonly Color32 BogPool = new Color32(52, 72, 70, 255);
    private static readonly Color32 Rock = new Color32(86, 80, 76, 255);
    private static readonly Color32 Shallow = new Color32(86, 142, 176, 255);
    private static readonly Color32 Deep = new Color32(44, 84, 128, 255);
    private static readonly Color32 River = new Color32(78, 146, 204, 255);
    private static readonly Color32 Road = new Color32(226, 202, 150, 255);
    private static readonly Color32 Trail = new Color32(196, 170, 128, 255);
    private static readonly Color32 CliffLine = new Color32(40, 32, 30, 255);
    private static readonly Color32 Scramble = new Color32(214, 190, 120, 255);
    private static readonly Color32 Place = new Color32(238, 84, 204, 255);
    private static readonly Color32 Hostile = new Color32(232, 62, 50, 255);
    private static readonly Color32 Game = new Color32(96, 232, 132, 255);
    private static readonly Color32 Wrong = new Color32(255, 40, 40, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);

    /// <summary>The colour each kind of glade is ringed in.</summary>
    public static Color32 GladeColour(HavenWorldPlan.GladeKind kind) => kind switch
    {
        HavenWorldPlan.GladeKind.Meadow => new Color32(236, 232, 120, 255),
        HavenWorldPlan.GladeKind.Rocky => new Color32(200, 200, 204, 255),
        HavenWorldPlan.GladeKind.Lair => new Color32(226, 110, 84, 255),
        _ => new Color32(126, 206, 236, 255),
    };

    /// <summary>The colour each kind of resource point is dotted in.</summary>
    public static Color32 NodeColour(ResourceNodeKind kind) => kind switch
    {
        ResourceNodeKind.Rock => new Color32(226, 226, 232, 255),
        ResourceNodeKind.Wood => new Color32(168, 112, 64, 255),
        ResourceNodeKind.Forage => new Color32(244, 128, 168, 255),
        _ => new Color32(176, 236, 96, 255),
    };

    /// <summary>The plan as a picture, <paramref name="size"/> pixels square, north up.</summary>
    public static Texture2D Texture(HavenWorldPlan plan, int size, Layers layers)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Haven World Plan", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixels32(Render(plan, size, layers));
        texture.Apply(false, false);
        return texture;
    }

    public static Color32[] Render(HavenWorldPlan plan, int size, Layers layers)
    {
        HavenWorldRecipe recipe = plan.Recipe;
        float world = recipe.size, half = plan.Half, metre = size / world;
        var heights = new float[size * size];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                heights[y * size + x] = plan.Height(At(x, y, size, world, half));
            }
        });

        var pixels = new Color32[size * size];
        bool water = (layers & Layers.Water) != 0, mist = (layers & Layers.Mist) != 0, problems = (layers & Layers.Problems) != 0;
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = At(x, y, size, world, half);
                float h = heights[y * size + x];
                float dx = heights[y * size + Mathf.Min(size - 1, x + 1)] - heights[y * size + Mathf.Max(0, x - 1)];
                float dy = heights[Mathf.Min(size - 1, y + 1) * size + x] - heights[Mathf.Max(0, y - 1) * size + x];
                float run = 2f / metre;
                float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy) / run) * Mathf.Rad2Deg;
                Color colour = Color.Lerp(Meadow, Woods, plan.WoodsWeight(point));
                colour = Color.Lerp(colour, GladeGround, plan.GladeOpen(point) * 0.85f);
                colour = Color.Lerp(colour, Mountain, plan.MountainWeight(point) * 0.75f);
                float bog = plan.BogWeight(point);
                if (bog > 0f)
                {
                    colour = Color.Lerp(colour, h < plan.BogLevel && water ? BogPool : Bog, bog);
                }

                // Each bench a shade lighter than the one below; lit from the north-west like a relief map; rock where it is steep.
                colour *= 1f + 0.05f * Mathf.Clamp(plan.Tier(point), -2, 8);
                colour *= Mathf.Clamp(0.9f + (dy - dx) / run * 0.45f, 0.45f, 1.3f);
                colour = Color.Lerp(colour, Rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 48f, slope)) * 0.85f);
                if (water)
                {
                    float depth = StillDepth(plan, point, h);
                    if (depth > 0f)
                    {
                        colour = Color.Lerp(Shallow, Deep, Mathf.Clamp01(depth / 3f));
                    }
                }

                if (mist)
                {
                    colour = Color.Lerp(colour, Color.white, plan.MistAt(point) * 0.45f);
                }

                if (problems && !plan.Reachable(point) && StillDepth(plan, point, h) <= 0f && (x + y) % 6 < 3
                    && half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)) > recipe.rim.y)
                {
                    colour = Color.Lerp(colour, (Color)Wrong, 0.6f);
                }

                colour.a = 1f;
                pixels[y * size + x] = colour;
            }
        });

        var canvas = new Canvas(pixels, size, metre, half);
        if (water)
        {
            for (int r = 0; r < plan.Rivers.Count; r++)
            {
                canvas.Line(plan.Rivers[r], River, Mathf.Max(1.4f, plan.RiverWidths[r] * metre));
            }

            foreach (Vector2 fall in plan.Falls)
            {
                canvas.Disc(fall, 2.6f, White);
            }
        }

        if ((layers & Layers.Glades) != 0)
        {
            foreach (List<Vector2> grove in plan.Groves)
            {
                var loop = new List<Vector2>(grove) { grove[0] };
                canvas.Line(loop, new Color32(196, 120, 60, 255), 1.2f);
            }

            foreach (HavenWorldPlan.Glade glade in plan.Glades)
            {
                canvas.Ring(glade.Centre, glade.Radius * metre, GladeColour(glade.Kind), 1.3f);
            }
        }

        if ((layers & Layers.Cliffs) != 0)
        {
            foreach (HavenWorldPlan.Cliff cliff in plan.Cliffs)
            {
                canvas.Line(cliff.Line, CliffLine, 1.7f);
            }

            foreach (Vector2 scramble in plan.Scrambles)
            {
                canvas.Disc(scramble, 2.2f, Scramble);
            }
        }

        if ((layers & Layers.Paths) != 0)
        {
            for (int p = 0; p < plan.Paths.Count; p++)
            {
                HavenWorldPlan.PathKind kind = plan.PathKinds[p];
                bool main = kind == HavenWorldPlan.PathKind.Road || kind == HavenWorldPlan.PathKind.Ring;
                canvas.Line(plan.Paths[p], main ? Road : Trail, main ? Mathf.Max(2.2f, recipe.roadWidth * metre) : 1.3f);
            }
        }

        if ((layers & Layers.Scenes) != 0)
        {
            foreach (HavenWorldPlan.Placed scene in plan.TrailScenes)
            {
                canvas.Disc(scene.At, 1.5f, new Color32(250, 246, 226, 255));
            }

            foreach (HavenWorldPlan.Placed marker in plan.Junctions)
            {
                canvas.Disc(marker.At, 1.2f, new Color32(60, 60, 60, 255));
            }
        }

        if ((layers & Layers.Resources) != 0)
        {
            foreach (HavenWorldPlan.Node node in plan.Nodes)
            {
                canvas.Disc(node.At, 1.6f, NodeColour(node.Band.kind));
            }
        }

        if ((layers & Layers.Creatures) != 0)
        {
            foreach (HavenWorldPlan.Zone zone in plan.Zones)
            {
                Color32 colour = zone.Band.peaceful ? Game : Hostile;
                canvas.Ring(zone.At, Mathf.Max(4f, zone.Radius * metre), colour, 1.6f);
                canvas.Disc(zone.At, 1.6f, colour);
            }
        }

        if ((layers & Layers.Places) != 0)
        {
            foreach (HavenWorldPlan.Poi poi in plan.Pois)
            {
                canvas.Diamond(poi.At, 6.5f, White);
                canvas.Diamond(poi.At, 4.8f, Place);
            }

            foreach ((Vector2 at, float _) in plan.Landings)
            {
                canvas.Diamond(at, 3.5f, White);
            }

            canvas.Ring(Vector2.zero, recipe.buildable * metre, new Color32(255, 255, 255, 150), 1f);
            canvas.Disc(Vector2.zero, 3f, White);
        }

        if (problems)
        {
            foreach ((Vector2 from, Vector2 to, float _) in plan.DullWalks)
            {
                canvas.Line(new List<Vector2> { from, to }, Wrong, 2.4f);
            }

            foreach (Vector2 end in plan.DeadEnds)
            {
                canvas.Cross(end, 5f, Wrong);
            }

            foreach (Vector2 empty in plan.EmptyGround)
            {
                canvas.Disc(empty, 1.3f, new Color32(255, 120, 255, 255));
            }
        }

        return pixels;
    }

    private static Vector2 At(int x, int y, int size, float world, float half) => new Vector2((x + 0.5f) / size * world - half, (y + 0.5f) / size * world - half);

    // How deep the still water stands over the ground at a point (0: dry).
    private static float StillDepth(HavenWorldPlan plan, Vector2 point, float ground)
    {
        if (plan.Lake.Edge(point) < 0f)
        {
            return Mathf.Max(0.01f, plan.Lake.Level - ground);
        }

        foreach (RaidLayoutPond mere in plan.Meres)
        {
            if (mere.Edge(point) < 0f)
            {
                return Mathf.Max(0.01f, mere.Level - ground);
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(point) < 0f)
            {
                return Mathf.Max(0.01f, pond.Level - ground);
            }
        }

        return 0f;
    }

    /// <summary>What the plan holds and how it measures up, a line per thing.</summary>
    public static string Stats(HavenWorldPlan plan)
    {
        HavenWorldRecipe recipe = plan.Recipe;
        var text = new StringBuilder();
        int asked = recipe.pointsOfInterest.Where(rule => rule != null && rule.prefab != null).Sum(rule => rule.count);
        int rivers = plan.RiverWidths.Count(width => width >= recipe.riverWidth.x - 0.01f);
        text.AppendLine($"Seed {plan.Seed}");
        text.AppendLine($"Places {plan.Pois.Count} of {asked} asked: " + string.Join(", ", plan.Pois.GroupBy(poi => poi.Rule.name).Select(group => group.Count() > 1 ? $"{group.Key} ×{group.Count()}" : group.Key)));
        text.AppendLine($"Glades {plan.Glades.Count}: " + string.Join(", ", plan.Glades.GroupBy(glade => glade.Kind).OrderBy(group => group.Key).Select(group => $"{group.Count()} {group.Key}")));
        text.AppendLine($"Water: lake with {plan.Islands.Count(island => island.Water == plan.Lake)} islands, {plan.Meres.Count} of {recipe.meres.Count} meres with " +
                        $"{plan.Islands.Count(island => island.Water != plan.Lake)} islands, {rivers} rivers, {plan.Rivers.Count - rivers} streams, {plan.Falls.Count} falls, " +
                        $"{plan.Ponds.Count} ponds ({plan.Springs.Count} springs), {plan.Fords.Count} fords" + (plan.HasBog ? ", a bog" : "")
                        + (plan.HavenPond != null ? $"; in the Haven a fishing pond {plan.HavenPond.Position.magnitude:0} m out and {plan.HavenStreams.Count} streams" : "; no fishing pond in the Haven")
                        + $", {plan.Knolls.Count} knolls");
        text.AppendLine($"Cliffs {plan.CliffMetres:0} m, {plan.DressedCliffMetres:0} m with rock stood along them in {plan.Cliffs.Count} lines; {plan.Scrambles.Count} scrambles");
        text.AppendLine($"Paths {plan.PathMetres:0} m in {plan.Paths.Count}: " + string.Join(", ", plan.PathKinds.GroupBy(kind => kind).OrderBy(group => group.Key).Select(group => $"{group.Count()} {group.Key}")));
        text.AppendLine($"Along the way: {plan.TrailScenes.Count} scenes, {plan.Junctions.Count} junction markers, {plan.GladeProps.Count} glade props, {plan.Rocks.Count} mountain rocks");
        text.AppendLine($"Creatures: {plan.Zones.Count(zone => !zone.Band.peaceful)} hostile zones, {plan.Zones.Count(zone => zone.Band.peaceful)} game zones (" +
                        string.Join(", ", plan.Zones.GroupBy(zone => zone.Band.name).Select(group => $"{group.Count()} {group.Key}")) + ")");
        text.AppendLine($"Resource points {plan.Nodes.Count}: " + string.Join(", ", plan.Nodes.GroupBy(node => node.Band.name).Select(group => $"{group.Count()} {group.Key}")));
        text.AppendLine($"Ground that can be walked to: {plan.ReachableShare * 100f:0.0}%");
        text.AppendLine($"Longest walk with nothing on it: {plan.LongestDullWalk:0} m ({plan.DullWalks.Count} over {recipe.longestDullWalk:0} m)");
        text.AppendLine($"Paths that end at nothing: {plan.DeadEnds.Count}");
        text.Append($"Empty ground (nothing within {HavenWorldPlan.EmptyBeyond:0} m): {plan.EmptyShare * 100f:0.0}%");
        return text.ToString();
    }

    // Draws over the picture in pixels, taking places in world metres.
    private sealed class Canvas
    {
        private readonly Color32[] pixels;
        private readonly int size;
        private readonly float metre, half;

        public Canvas(Color32[] pixels, int size, float metre, float half)
        {
            this.pixels = pixels;
            this.size = size;
            this.metre = metre;
            this.half = half;
        }

        private Vector2 Pixel(Vector2 world) => new Vector2((world.x + half) * metre, (world.y + half) * metre);

        private void Blend(int x, int y, Color32 colour, float cover)
        {
            if (x < 0 || y < 0 || x >= size || y >= size || cover <= 0f)
            {
                return;
            }

            float a = Mathf.Clamp01(cover) * colour.a / 255f;
            Color32 under = pixels[y * size + x];
            pixels[y * size + x] = new Color32((byte)Mathf.Lerp(under.r, colour.r, a), (byte)Mathf.Lerp(under.g, colour.g, a), (byte)Mathf.Lerp(under.b, colour.b, a), 255);
        }

        public void Line(List<Vector2> line, Color32 colour, float width)
        {
            float reach = width * 0.5f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = Pixel(line[i]), b = Pixel(line[i + 1]);
                Vector2 along = b - a;
                float squared = Mathf.Max(0.0001f, along.sqrMagnitude);
                for (int y = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach - 1f); y <= Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach + 1f); y++)
                {
                    for (int x = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach - 1f); x <= Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach + 1f); x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / squared);
                        Blend(x, y, colour, reach + 0.5f - Vector2.Distance(a + along * t, point));
                    }
                }
            }
        }

        public void Disc(Vector2 centre, float radius, Color32 colour)
        {
            Vector2 c = Pixel(centre);
            for (int y = Mathf.FloorToInt(c.y - radius - 1f); y <= Mathf.CeilToInt(c.y + radius + 1f); y++)
            {
                for (int x = Mathf.FloorToInt(c.x - radius - 1f); x <= Mathf.CeilToInt(c.x + radius + 1f); x++)
                {
                    Blend(x, y, colour, radius + 0.5f - Vector2.Distance(c, new Vector2(x + 0.5f, y + 0.5f)));
                }
            }
        }

        public void Ring(Vector2 centre, float radius, Color32 colour, float width)
        {
            Vector2 c = Pixel(centre);
            for (int y = Mathf.FloorToInt(c.y - radius - width - 1f); y <= Mathf.CeilToInt(c.y + radius + width + 1f); y++)
            {
                for (int x = Mathf.FloorToInt(c.x - radius - width - 1f); x <= Mathf.CeilToInt(c.x + radius + width + 1f); x++)
                {
                    Blend(x, y, colour, width * 0.5f + 0.5f - Mathf.Abs(Vector2.Distance(c, new Vector2(x + 0.5f, y + 0.5f)) - radius));
                }
            }
        }

        public void Diamond(Vector2 centre, float radius, Color32 colour)
        {
            Vector2 c = Pixel(centre);
            for (int y = Mathf.FloorToInt(c.y - radius - 1f); y <= Mathf.CeilToInt(c.y + radius + 1f); y++)
            {
                for (int x = Mathf.FloorToInt(c.x - radius - 1f); x <= Mathf.CeilToInt(c.x + radius + 1f); x++)
                {
                    Blend(x, y, colour, radius + 0.5f - (Mathf.Abs(x + 0.5f - c.x) + Mathf.Abs(y + 0.5f - c.y)));
                }
            }
        }

        public void Cross(Vector2 centre, float radius, Color32 colour)
        {
            Vector2 c = Pixel(centre) / metre - new Vector2(half, half);
            float r = radius / metre;
            Line(new List<Vector2> { c + new Vector2(-r, -r), c + new Vector2(r, r) }, colour, 2f);
            Line(new List<Vector2> { c + new Vector2(-r, r), c + new Vector2(r, -r) }, colour, 2f);
        }
    }
}
