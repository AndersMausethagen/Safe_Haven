using System.Linq;
using UnityEngine;

/// <summary>
/// The ground's shape: the woods', the mountain's and the bog's weight at a point, the land that climbs away from the Haven, the
/// terraces cut from it (flat benches with a cliff between each and the next), and the height in metres above the portal's
/// ground with water set level into it. Part of HavenWorldPlan, in its own file.
/// <para>
/// Why terraces: the camera looks down from 20-40 m, and from there a slope is invisible while a cliff is a line. So the land
/// outside the Haven is stepped. <see cref="Land"/> is the smooth land, <see cref="Tier"/> which bench a point is on, and a
/// cliff stands wherever the land crosses half a step. A path crossing one gets a ramp, and scrambles (HavenWorldPlan.Terraces)
/// are ways up without a path.
/// </para>
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float LakeBank = 25f;

    private const float LakeSlope = 18f;

    private const float PondBank = 4f;

    private const float MereBank = 9f;

    private const float MereSlope = 8f;

    // How far from the lake's shore the land has come down to the lake's bench, and from a mere's to its own.
    private const float LakeBasin = 85f;

    private const float MereBasin = 26f;

    // Past a place's clearing, the land comes back to its own over this many metres.
    private const float PlaceApron = 14f;

    // The bog's water lies this far under its bench; its hummocks stand at most this far over it.
    private const float BogWaterBelow = 0.12f;

    /// <summary>The bog's middle, the height of its floor and of its water (metres above the portal's ground).</summary>
    public Vector2 BogCentre { get; private set; }
    public float BogBench { get; private set; }
    public float BogLevel => BogBench - BogWaterBelow;
    public bool HasBog => Recipe.bogAt.y > 0f && Recipe.bogRadius > 0f;

    /// <summary>The height of the bench the lake lies in: one terrace under the Haven.</summary>
    public float LakeBench { get; private set; }

    private static float Ease(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

    /// <summary>0 in the Haven's ring, 1 in the Misty Woods, with a wandering border.</summary>
    public float WoodsWeight(Vector2 point)
    {
        float wander = (Perlin(point, 70f, 41f) - 0.5f) * 60f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.haven, Recipe.forest, point.magnitude + wander));
    }

    /// <summary>How much of the mountain stands at a point, 0 to 1.</summary>
    public float MountainWeight(Vector2 point)
    {
        float half = Recipe.mountainAt.y * 0.5f;
        float off = Mathf.Abs(Mathf.DeltaAngle(BearingOf(point), Recipe.mountainAt.x));
        float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.55f, half, off));
        float along = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.mountainFrom, Recipe.mountainFrom + 170f, point.magnitude));
        return across * along;
    }

    /// <summary>How much of the bog lies at a point: 1 in it, 0 outside, with a ragged rim.</summary>
    public float BogWeight(Vector2 point)
    {
        if (!HasBog)
        {
            return 0f;
        }

        float distance = Vector2.Distance(point, BogCentre);
        if (distance > Recipe.bogRadius * 1.3f)
        {
            return 0f;
        }

        float rim = Recipe.bogRadius * (0.78f + 0.5f * Perlin(point, 55f, 311f));
        return 1f - Ease(rim * 0.72f, rim, distance);
    }

    /// <summary>True where the bog stands under water: its pools and channels, not its hummocks.</summary>
    public bool BogWet(Vector2 point, float beyond = 0f) =>
        HasBog && BogWeight(point) > 0.5f && Ground(point) < BogLevel + beyond * 0.1f;

    // The land before anything is pressed into it: a slow climb out from the Haven that swells and sinks as it goes (so the cliffs
    // wander into bays and headlands instead of rings), the mountain, and the rim at the edge of the world.
    private float OpenLand(Vector2 point)
    {
        float r = point.magnitude;
        float open = Ease(Recipe.buildable, Recipe.buildable + 70f, r);
        float climb = Mathf.Clamp01(Mathf.InverseLerp(Recipe.buildable + 40f, Half - Recipe.rim.y, r));
        float rise = Recipe.terraceRise * Mathf.Pow(climb, 1.15f);
        float fade = Ease(Recipe.buildable + 20f, Recipe.haven + 60f, r);
        // Mostly the broad swell: a little of a smaller one frays the cliffs' line, more of it litters the land with table rocks.
        float wander = (Perlin(point, Recipe.terraceScale, 131f) - 0.5f) * 2f + (Perlin(point, Recipe.terraceScale * 0.45f, 173f) - 0.5f) * 0.4f;
        float land = (rise + wander * Recipe.terraceWander * fade) * open;
        // Each road out from the Haven runs in a valley: its walls are cliffs, and the benches above are rooms off the road.
        float road = RoadDistance(point);
        if (road < Recipe.roadValleyWidth && Recipe.roadValley > 0f)
        {
            float valley = 1f - Ease(Recipe.roadValleyWidth * 0.25f, Recipe.roadValleyWidth, road);
            land -= Recipe.roadValley * Recipe.terraceStep * valley * Ease(Recipe.haven - 60f, Recipe.haven + 30f, r) * (1f - 0.5f * MountainWeight(point));
        }

        float fold = Perlin(point, Recipe.hillScale * 0.6f, 97f) * 2f - 1f;
        land += Recipe.mountainHeight * MountainWeight(point) * (0.7f + 0.3f * (1f - fold * fold));
        // The edge of the world rises into a broken, rocky crest: stepped like the rest, so it stands as a wall of ledges.
        float edge = Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
        float rim = Mathf.Clamp01(1f - edge / Recipe.rim.y);
        if (rim > 0f)
        {
            float crest = 1f - Mathf.Abs(Perlin(point, 38f, 211f) * 2f - 1f);
            float rocky = Mathf.Lerp(0.55f, 1.35f, crest) + (Perlin(point, 11f, 223f) - 0.5f) * 0.3f;
            land += Recipe.rim.x * Mathf.Pow(rim, 1.6f) * rocky;
        }

        return land;
    }

    /// <summary>
    /// The land the terraces are cut from, in metres above the portal's ground: the open land with the lake's valley, each mere's
    /// shelf and the bog's basin pressed into it, so water lies in flat ground and the cliffs stand back from it.
    /// </summary>
    public float Land(Vector2 point)
    {
        float land = OpenLand(point);
        if (Lake != null)
        {
            float edge = Lake.Edge(point);
            if (edge < LakeBasin)
            {
                land = Mathf.Lerp(LakeBench, land, Ease(0f, LakeBasin, edge));
            }
        }

        foreach (RaidLayoutPond mere in Meres)
        {
            if ((point - mere.Position).sqrMagnitude < (mere.Reach + MereBasin) * (mere.Reach + MereBasin))
            {
                land = Mathf.Lerp(mere.Level + MereShore, land, Ease(0f, MereBasin, mere.Edge(point)));
            }
        }

        float bog = BogWeight(point);
        if (bog > 0f)
        {
            land = Mathf.Lerp(land, BogBench, bog);
        }

        // Level under every place, whatever bench it stands on: a cliff goes round a place, never through it.
        foreach (Poi poi in Pois)
        {
            float reach = poi.Rule.clearing + PlaceApron;
            float squared = (poi.At - point).sqrMagnitude;
            if (squared < reach * reach)
            {
                land = Mathf.Lerp(poi.Land, land, Ease(poi.Rule.clearing, reach, Mathf.Sqrt(squared)));
            }
        }

        return land;
    }

    /// <summary>Which bench a point is on: 0 the Haven's, counting up outward and down into the lake's valley.</summary>
    public int Tier(Vector2 point) => Mathf.RoundToInt(Land(point) / Recipe.terraceStep);

    /// <summary>True when the ground within a radius of a point is all one bench: no cliff runs through it.</summary>
    public bool OneBench(Vector2 point, float radius)
    {
        int tier = Tier(point);
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Dir(i * 45f) * radius;
            if (Tier(point + offset) != tier || (i % 2 == 0 && Tier(point + offset * 0.5f) != tier))
            {
                return false;
            }
        }

        return true;
    }

    // The land in steps: flat benches, and between each and the next a face riserWidth metres wide whatever the land's own slope.
    // Where a path crosses, or a scramble is, the face is drawn out into a ramp instead.
    private float Terraced(Vector2 point)
    {
        const float apart = 2.5f;
        float step = Recipe.terraceStep;
        float land = Land(point);
        float dx = Land(point + new Vector2(apart, 0f)) - Land(point - new Vector2(apart, 0f));
        float dy = Land(point + new Vector2(0f, apart)) - Land(point - new Vector2(0f, apart));
        float slope = Mathf.Sqrt(dx * dx + dy * dy) / (2f * apart);
        float width = Mathf.Lerp(Recipe.riserWidth, Recipe.rampLength, RampAt(point));
        // The share of a step the face takes: half of it either side of the middle. Past a half, the land is steeper than a face.
        float share = Mathf.Clamp(width * slope / (2f * step), 0.012f, 0.5f);
        float steps = land / step;
        float tier = Mathf.Floor(steps);
        float within = steps - tier;
        float stepped = tier + Ease(0.5f - share, 0.5f + share, within);
        // A bench leans a little up towards the next cliff, easing off to nothing at the faces so they stay clean.
        float across = within < 0.5f ? within : within - 1f;
        float lean = across * (1f - Mathf.Pow(Mathf.Abs(across) * 2f, 4f)) * 0.5f * Recipe.benchRoll;
        return (stepped + lean) * step;
    }

    // What the terraces leave alone: the small rolling of the ground, gentler on the benches than in the Haven's meadows, and the
    // bog's hummocks and pools.
    private float Detail(Vector2 point)
    {
        float r = point.magnitude;
        // Gentle and open where the Haven is built: the ward reaches about 50 m.
        float open = Ease(Recipe.buildable, Recipe.buildable + 70f, r);
        float near = (Perlin(point, 25f, 13f) - 0.5f) * 0.8f * Ease(10f, 40f, r);
        float benches = Ease(Recipe.haven - 30f, Recipe.haven + 60f, r);
        float roll = (Perlin(point, Recipe.hillScale, 0f) - 0.5f) * 2f * Recipe.havenHills * Mathf.Lerp(1f, Recipe.benchRoll, benches);
        float detail = near * (1f - open) + roll * open;
        float bog = BogWeight(point);
        if (bog <= 0f)
        {
            return detail;
        }

        // Hummocks with pools and channels between: the water lies level, the ground dips under it.
        float lumps = Perlin(point, 11f, 401f) * 0.65f + Perlin(point, 4.3f, 409f) * 0.35f;
        float dry = 0.55f + (Recipe.bogWater - 0.5f) * 0.45f;
        float hummocks = Mathf.Max(-0.7f, (lumps - dry) * 2.4f);
        // A path through it runs on a low causeway; a place in it stands on a dry mound.
        float raised = Ease(0.6f, 1f, RampAt(point));
        foreach (Poi poi in Pois)
        {
            if (poi.Rule.inBog)
            {
                raised = Mathf.Max(raised, 1f - Ease(poi.Rule.clearing * 0.8f, poi.Rule.clearing * 1.5f, Vector2.Distance(poi.At, point)));
            }
        }

        hummocks = Mathf.Lerp(hummocks, Mathf.Max(hummocks, 0.3f), raised);
        return Mathf.Lerp(detail, hummocks, bog);
    }

    /// <summary>
    /// How much mist always lies at a point, 0 to 1: in the Misty Woods (thinner in their glades, which stand as clear rooms)
    /// and thickest over the bog; none in the Haven's own meadows, which keep only the weather's and the morning's.
    /// </summary>
    public float MistAt(Vector2 point)
    {
        float patches = 0.7f + 0.6f * Perlin(point, 95f, 517f);
        float woods = WoodsWeight(point) * Recipe.woodsMist * patches * Mathf.Lerp(1f, Recipe.gladeMist, GladeOpen(point));
        // The mountain stands out of it.
        woods *= 1f - 0.7f * MountainWeight(point);
        return Mathf.Clamp01(Mathf.Max(woods, BogWeight(point) * Recipe.bogMist));
    }

    /// <summary>The ground before the lake, the meres, the rivers and the ponds are dug, in metres above the portal's ground.</summary>
    public float Ground(Vector2 point) => Terraced(point) + Detail(point);

    /// <summary>The ground with all water dug, in metres above the portal's ground.</summary>
    public float Height(Vector2 point)
    {
        float ground = Ground(point);
        if (Lake != null)
        {
            ground = Dig(Lake, point, ground, LakeBank, LakeSlope);
        }

        ground = RiverValley(point, ground);
        foreach (RaidLayoutPond mere in Meres)
        {
            ground = Dig(mere, point, ground, MereBank, MereSlope);
        }

        foreach (RaidLayoutPond pond in Ponds)
        {
            ground = Dig(pond, point, ground, PondBank, Mathf.Min(3f, Mathf.Min(pond.Radii.x, pond.Radii.y) * 0.8f));
        }

        return ground;
    }

    // Still water set into the ground: a bank coming down to its level, a bed sloping to its depth, and its islands standing out of it.
    private float Dig(RaidLayoutPond water, Vector2 point, float ground, float bank, float slope)
    {
        float reach = water.Reach + bank;
        if ((point - water.Position).sqrMagnitude >= reach * reach)
        {
            return ground;
        }

        float edge = water.Edge(point);
        if (edge >= bank)
        {
            return ground;
        }

        // The bank always climbs out of the water, even where the ground behind it rolls low: water never stands above its shore.
        ground = edge >= 0f
            ? Mathf.Lerp(water.Level, Mathf.Max(ground, water.Level + 0.3f), Mathf.SmoothStep(0f, 1f, edge / bank))
            : water.Level - water.Depth * Mathf.SmoothStep(0f, 1f, -edge / slope);
        // Where a river runs in, its fan is scoured into the bed: the channel carries on into the water and shallows out there.
        foreach (Mouth mouth in Mouths)
        {
            if (mouth.Water == water && edge < 2f && (mouth.At - point).sqrMagnitude < mouth.Reach * mouth.Reach)
            {
                float scour = (0.4f + mouth.Width * 0.1f) * (1f - Ease(mouth.Reach * 0.25f, mouth.Reach, Vector2.Distance(mouth.At, point))) * Ease(2f, -1f, edge);
                ground = Mathf.Min(ground, water.Level - scour);
            }
        }

        foreach (Island island in Islands)
        {
            if (island.Water != water)
            {
                continue;
            }

            float d = Vector2.Distance(island.Centre, point);
            if (d < island.Radius * 1.6f)
            {
                float dome = Mathf.SmoothStep(0f, 1f, 1f - d / (island.Radius * 1.6f));
                ground = Mathf.Max(ground, Mathf.Lerp(water.Level - water.Depth, water.Level + island.Height, dome));
            }
        }

        return ground;
    }

    // The water lies just under the lowest ground around it, so it never stands above its own banks.
    private float LevelAround(RaidLayoutPond water, float beyond, float below)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < 32; i++)
        {
            lowest = Mathf.Min(lowest, Ground(water.Rim(i * Mathf.PI * 2f / 32f, beyond)));
        }

        return lowest - below;
    }
}
