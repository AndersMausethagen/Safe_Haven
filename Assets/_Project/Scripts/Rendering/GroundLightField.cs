using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The arithmetic of a raid's ground light field (GroundLightMap; Docs/LightingBakeResearch_2026-11-17.md §2.1): plain arrays and
/// no Unity objects, so it runs on worker threads while the loading screen is up, and in edit-mode tests. Every grid is square, row
/// by row from the map's south-west corner (index z * size + x), one value per cell centre, cells a fixed number of metres apart.
/// <list type="bullet">
/// <item><b>Relief</b>: how much sky the land round a spot leaves it (hollows, ravines, terrace cliffs, valley walls), by tracing
/// the horizon in several directions from 1.5 to 30 m out: the scale SSAO can't see from a camera 20 to 40 m up.</item>
/// <item><b>Cover</b> at ground level: SkyOcclusion.hlsl's own test against the cover map, worked out here for the terrain,
/// which can't afford to read the cover map itself.</item>
/// <item><b>Bounce</b>: ground colour times how much of the sun (or moon) reaches it at the run's fixed hour, through the land
/// and through whatever stands on it, spread over a few metres.</item>
/// </list>
/// </summary>
public static class GroundLightField
{
    /// <summary>Strength of the cover's dimming at full cover, as in SkyOcclusion.hlsl (a third of the sky's light still gets in).</summary>
    public const float CoverDimming = 0.65f;

    /// <summary>Metres a cell of the land's shade is worked out on: it is metres wide, and this is most of the field's cost.</summary>
    public const float ReliefCell = 0.75f;

    /// <summary>What a field is built from.</summary>
    public sealed class Recipe
    {
        /// <summary>Cells a side.</summary>
        public int Size;
        /// <summary>Metres a cell.</summary>
        public float Cell;
        /// <summary>The ground's height at each cell centre, in world metres.</summary>
        public float[] Ground;
        /// <summary>The surface of the water nearest each cell (a river's, a pond's, the fen's), in world metres, or NaN where none is near:
        /// for the wet band along every shore. Null: none anywhere.</summary>
        public float[] Water;
        /// <summary>How near each cell is to that water's edge, 1 at it and 0 at GroundLightMap.ShoreReach (slice 1's alpha), so the
        /// band fades out with distance instead of ending in a line where the lookup stops on a gentle shore. Null: 1 everywhere.</summary>
        public float[] WaterNear;
        /// <summary>The cover map (SkyOcclusionMap): the height of whatever stands highest at each texel, in world metres; far below the
        /// ground where nothing does. Null: no cover (nothing stands anywhere).</summary>
        public float[] Cover;
        public int CoverSize;
        /// <summary>Where the cover map's square lies, in metres from the field's corner (x, z), and its side in metres: it may reach
        /// past the map.</summary>
        public Vector2 CoverCorner;
        public float CoverSide;
        /// <summary>The ground's colour at each cell (linear), what thrown-back light takes on. Null: no bounce.</summary>
        public Vector3[] Albedo;
        /// <summary>Unit vector towards the sun (or the moon) at the run's hour.</summary>
        public Vector3 TowardLight = Vector3.up;
        /// <summary>How much cover dims indirect light (SkyOcclusionMap's strength, 0-1).</summary>
        public float CoverStrength;
        /// <summary>How much the land's shade darkens (RaidGroundLight › Relief, 0 none).</summary>
        public float Relief;
        /// <summary>The land's shade factor at each cell from an earlier build of the same ground (<see cref="Result.Relief"/>): reused
        /// instead of traced again, as it reads no cover (GroundLightMap works the field out again once the map is set up). Null: traced.</summary>
        public float[] KnownRelief;
        /// <summary>Whether to work the thrown-back light out at all (RaidGroundLight › Bounce above 0).</summary>
        public bool Bounce;
        /// <summary>How much of the night's lift the run's hour gets (<see cref="LiftShare"/>): 1 at night and at a dim dawn or
        /// dusk, when the field keeps the map's average light as it was without it; 0 by day.</summary>
        public float Dim;
        public float NightLiftMost = 1.5f;
        public int ReliefDirections = 12;
        public float ReliefNearest = 1.5f;
        public float ReliefFarthest = 30f;
        /// <summary>Metres the thrown-back light spreads over (half the width of each of two box blurs).</summary>
        public float BounceSpread = 6f;
        /// <summary>Metres towards the light that something can still cast its shade from.</summary>
        public float SunReach = 60f;
    }

    /// <summary>A built field, ready for GroundLightMap's texture.</summary>
    public sealed class Result
    {
        /// <summary>Slice 0: rgb the light thrown back (square root, so dim light keeps its steps), a the land's shade factor.</summary>
        public Color32[] Light;
        /// <summary>Slice 1: r the cover's shade factor at ground level (the terrain's); g the ground's height over
        /// <see cref="LowestHeight"/> to <see cref="HighestHeight"/>; b the nearest water's surface as metres above or below that ground
        /// (± <see cref="WaterReach"/>; 0: none), so a shore's wet band is placed to centimetres (Docs/LookAndFeelResearch_2026-11-18.md 4.6);
        /// a how near that water's edge is (<see cref="Recipe.WaterNear"/>), 255 where no water is near.</summary>
        public Color32[] Ground;
        public float LowestHeight;
        public float HighestHeight = 1f;
        /// <summary>What the field's shade is multiplied by at this hour (above 1 only at dim hours: dawn, dusk, night).</summary>
        public float Lift = 1f;
        /// <summary>The ground's light factor (the canopy's shade times the land's) averaged over the map: what the terrain loses
        /// against before the field, which read neither.</summary>
        public float MeanGround = 1f;
        /// <summary>The land's shade factor at each cell (null when Relief was 0), for a later build of the same ground
        /// (<see cref="Recipe.KnownRelief"/>).</summary>
        public float[] Relief;
        public string Timings = "";
    }

    /// <summary>Builds the whole field. Plain arithmetic: safe on a worker thread.</summary>
    public static Result Build(Recipe recipe)
    {
        if (recipe == null || recipe.Size <= 0 || recipe.Ground == null || recipe.Ground.Length != recipe.Size * recipe.Size)
        {
            throw new ArgumentException("A ground light recipe needs Size and a Ground height for every cell.");
        }

        var clock = Stopwatch.StartNew();
        int size = recipe.Size;
        int count = size * size;
        var result = new Result { Light = new Color32[count], Ground = new Color32[count] };

        // The land's shade and the thrown-back light are metres wide: worked out on cells of about ReliefCell, then spread back
        // over the field's.
        int every = Mathf.Max(1, Mathf.RoundToInt(ReliefCell / recipe.Cell));
        int coarse = (size + every - 1) / every;
        float coarseCell = recipe.Cell * every;
        float[] coarseGround = Shrink(recipe.Ground, size, every);
        float[] relief = null;
        long reliefMs = 0;
        if (recipe.KnownRelief != null && recipe.KnownRelief.Length == count)
        {
            relief = recipe.KnownRelief;
        }
        else if (recipe.Relief > 0f)
        {
            float[] open = Relief(coarseGround, coarse, coarseCell, recipe.ReliefDirections, recipe.ReliefNearest, recipe.ReliefFarthest);
            relief = every == 1 ? open : Grow(open, coarse, size, every);
            for (int i = 0; i < count; i++)
            {
                relief[i] = ReliefFactor(relief[i], recipe.Relief);
            }

            reliefMs = clock.ElapsedMilliseconds;
        }

        clock.Restart();
        float[] tops = Tops(recipe);
        var cover = new float[count];
        Parallel.For(0, size, z =>
        {
            for (int x = 0; x < size; x++)
            {
                int i = z * size + x;
                cover[i] = recipe.Cover == null ? 1f
                    : CoverAt(recipe.Cover, recipe.CoverSize, recipe.CoverSize, recipe.CoverCorner, recipe.CoverSize / recipe.CoverSide,
                              (x + 0.5f) * recipe.Cell, (z + 0.5f) * recipe.Cell, recipe.Ground[i], recipe.CoverStrength);
            }
        });
        long coverMs = clock.ElapsedMilliseconds;

        // The night's lift: the terrain read no shade before the field and what stands on it only the canopy's, so at dim hours
        // everything the field shades is lifted until the ground's average is what it was (one lift for all, so grass and the
        // ground it grows on stay alike).
        double sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum += cover[i] * (relief != null ? relief[i] : 1f);
        }

        result.MeanGround = (float)(sum / count);
        result.Lift = LiftAt(result.MeanGround, recipe.NightLiftMost, recipe.Dim);

        clock.Restart();
        float[] red = null, green = null, blue = null;
        if (recipe.Bounce && recipe.Albedo != null && recipe.Albedo.Length == count)
        {
            // Whatever stands in a coarse cell shades it: the highest, not the average.
            float[] lit = SunLight(coarseGround, Shrink(tops, size, every, true), coarse, coarseCell, recipe.TowardLight, recipe.SunReach);
            var albedo = new float[3][];
            for (int c = 0; c < 3; c++)
            {
                var channel = new float[count];
                for (int i = 0; i < count; i++)
                {
                    channel[i] = recipe.Albedo[i][c];
                }

                albedo[c] = Shrink(channel, size, every);
            }

            int radius = Mathf.Max(1, Mathf.RoundToInt(recipe.BounceSpread / coarseCell));
            var thrown = new float[3][];
            for (int c = 0; c < 3; c++)
            {
                float[] channel = every == 1 ? (float[])albedo[c].Clone() : albedo[c];
                for (int i = 0; i < channel.Length; i++)
                {
                    channel[i] *= lit[i];
                }

                Blur(channel, coarse, radius);
                Blur(channel, coarse, radius);
                thrown[c] = every == 1 ? channel : Grow(channel, coarse, size, every);
            }

            red = thrown[0];
            green = thrown[1];
            blue = thrown[2];
        }

        long bounceMs = clock.ElapsedMilliseconds;
        HeightRange(recipe.Ground, recipe.Water, out float lowest, out float highest);
        result.LowestHeight = lowest;
        result.HighestHeight = highest;
        for (int i = 0; i < count; i++)
        {
            result.Light[i] = new Color32(Root(red, i), Root(green, i), Root(blue, i), Byte(relief != null ? relief[i] : 1f));
            result.Ground[i] = GroundTexel(Byte(cover[i]), recipe.Ground[i], recipe.Water, recipe.WaterNear, i, lowest, highest);
        }

        result.Relief = relief;
        result.Timings = $"relief {reliefMs} ms, cover {coverMs} ms, bounce {bounceMs} ms";
        return result;
    }

    /// <summary>
    /// How much sky the land leaves each cell (1 on open level ground, less in a hollow): for each direction the steepest rise
    /// between <paramref name="nearest"/> and <paramref name="farthest"/> metres out, counted as the cosine-weighted share of a level
    /// patch's sky on that side that it still sees (cos² of the horizon's height), averaged over the directions.
    /// </summary>
    public static float[] Relief(float[] heights, int size, float cell, int directions = 12, float nearest = 1.5f, float farthest = 30f)
    {
        const int steps = 10;
        var open = new float[size * size];
        directions = Mathf.Max(1, directions);
        nearest = Mathf.Max(cell, nearest);
        farthest = Mathf.Max(nearest, farthest);
        // Out in growing steps: near rises need close samples, far walls only a few.
        var reach = new float[steps];
        for (int k = 0; k < steps; k++)
        {
            reach[k] = nearest * Mathf.Pow(farthest / nearest, k / (float)(steps - 1));
        }

        // Each step's offset in cells, and one over its distance in metres.
        var offsetX = new float[directions * steps];
        var offsetZ = new float[directions * steps];
        var perMetre = new float[steps];
        for (int k = 0; k < steps; k++)
        {
            perMetre[k] = 1f / reach[k];
        }

        for (int d = 0; d < directions; d++)
        {
            float angle = (d + 0.5f) * Mathf.PI * 2f / directions;
            for (int k = 0; k < steps; k++)
            {
                offsetX[d * steps + k] = Mathf.Cos(angle) * reach[k] / cell;
                offsetZ[d * steps + k] = Mathf.Sin(angle) * reach[k] / cell;
            }
        }

        Parallel.For(0, size, z =>
        {
            for (int x = 0; x < size; x++)
            {
                float here = heights[z * size + x];
                float sum = 0f;
                for (int d = 0; d < directions; d++)
                {
                    float steepest = 0f;
                    for (int k = 0; k < steps; k++)
                    {
                        int at = d * steps + k;
                        float slope = (Sample(heights, size, x + offsetX[at], z + offsetZ[at]) - here) * perMetre[k];
                        if (slope > steepest)
                        {
                            steepest = slope;
                        }
                    }

                    sum += 1f / (1f + steepest * steepest);
                }

                open[z * size + x] = sum / directions;
            }
        });
        return open;
    }

    /// <summary>What indirect light is multiplied by for a cell that sees <paramref name="open"/> of its sky, at a strength (0 none).</summary>
    public static float ReliefFactor(float open, float strength) => 1f - Mathf.Clamp01(strength) * (1f - Mathf.Clamp01(open));

    /// <summary>
    /// The night's lift: at dim hours the field's shade is spread round its average instead of only taking light away, so
    /// the ground as a whole is no darker than before the field (the user: the night must not get darker). Capped at
    /// <paramref name="most"/>.
    /// </summary>
    public static float NightLift(float meanFactor, float most) =>
        meanFactor <= 0f ? Mathf.Max(1f, most) : Mathf.Clamp(1f / meanFactor, 1f, Mathf.Max(1f, most));

    /// <summary>The lift at an hour that gets <paramref name="share"/> (0-1, <see cref="LiftShare"/>) of the night's.</summary>
    public static float LiftAt(float meanFactor, float most, float share) =>
        1f + (NightLift(meanFactor, most) - 1f) * Mathf.Clamp01(share);

    /// <summary>
    /// GroundLight.hlsl's GroundLightApply: how a factor on a surface's indirect light is put on, since URP takes the lower of the
    /// surface's occlusion and SSAO (so occlusion can't go above 1): the darkening part on occlusion, what lies above 1 (the night's
    /// lift) on the indirect diffuse light. Their product is always the factor.
    /// </summary>
    public static void SplitIndirect(float factor, out float onOcclusion, out float onIndirect)
    {
        onOcclusion = Mathf.Min(factor, 1f);
        onIndirect = Mathf.Max(factor, 1f);
    }

    /// <summary>GroundLight.hlsl's GroundLightBounce: how much of the thrown-back light a surface facing this way takes (its
    /// normal's height, -1 to 1): all of it facing sideways or down, a tenth facing the sky, so level ground isn't tinted by itself.</summary>
    public static float BounceFacing(float normalY) => Mathf.Lerp(1f, 0.1f, Mathf.Clamp01(normalY));

    /// <summary>The day-night profile's Daylight is full once the sun stands this many degrees up (DayNightProfile.Evaluate).</summary>
    public const float DaylightFullDegrees = 15f;

    /// <summary>
    /// How much of the night's lift an hour gets, by how dark it is rather than by the clock: all of it at night
    /// (<paramref name="night"/>: the profile's Night, 0-1) and while the sun is lower than <paramref name="fullBelowDegrees"/>,
    /// fading to none by the time it stands 15° up (<paramref name="daylight"/>: the profile's Daylight, 0 at night and 1 from
    /// 15°). So a dim dawn is lifted as a dusk is, and a bright evening isn't.
    /// </summary>
    public static float LiftShare(float night, float daylight, float fullBelowDegrees)
    {
        float full = Mathf.Clamp(fullBelowDegrees, 0f, DaylightFullDegrees) / DaylightFullDegrees;
        daylight = Mathf.Clamp01(daylight);
        float low = full >= 1f ? (daylight < 1f ? 1f : 0f) : 1f - SmoothStep(full, 1f, daylight);
        return Mathf.Clamp01(Mathf.Max(night, low));
    }

    /// <summary>The value of a 16-bit (half) float, as the cover map is drawn and read back. Plain arithmetic: safe on a worker thread.</summary>
    public static float HalfToFloat(ushort half)
    {
        uint sign = (uint)(half & 0x8000) << 16;
        uint exponent = (uint)(half >> 10) & 0x1F;
        uint mantissa = (uint)half & 0x3FF;
        var bits = new FloatBits();
        if (exponent == 0)
        {
            // Zero, or too small for the half's exponent: the mantissa counts 2^-24ths.
            float tiny = mantissa * 5.9604644775390625e-8f;
            return sign != 0 ? -tiny : tiny;
        }

        bits.Int = exponent == 31
            ? sign | 0x7F800000u | (mantissa << 13)
            : sign | ((exponent + 112u) << 23) | (mantissa << 13);
        return bits.Float;
    }

    /// <summary>The first <paramref name="count"/> half floats as floats, into <paramref name="floats"/> (a worker thread's job).</summary>
    public static void HalvesToFloats(ushort[] halves, float[] floats, int count)
    {
        for (int i = 0; i < count; i++)
        {
            floats[i] = HalfToFloat(halves[i]);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct FloatBits
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public uint Int;
        [System.Runtime.InteropServices.FieldOffset(0)] public float Float;
    }

    /// <summary>
    /// SkyOcclusion.hlsl's cover test for a point on the ground, worked out on the CPU for the terrain: five taps a metre apart
    /// in the cover map (<paramref name="width"/> × <paramref name="height"/> texels, its corner at <paramref name="corner"/>), each
    /// counting as cover from 0.4 to 2 m above the point, and the cover's dimming at a strength. <paramref name="x"/>,
    /// <paramref name="z"/> and the corner are metres from the field's corner.
    /// </summary>
    public static float CoverAt(float[] cover, int width, int height, Vector2 corner, float texelsPerMetre, float x, float z, float ground, float strength)
    {
        float covered = 0f;
        for (int tap = 0; tap < 5; tap++)
        {
            float tx = x + (tap == 1 ? 1f : tap == 2 ? -1f : 0f);
            float tz = z + (tap == 3 ? 1f : tap == 4 ? -1f : 0f);
            float top = Sample(cover, width, height, (tx - corner.x) * texelsPerMetre - 0.5f, (tz - corner.y) * texelsPerMetre - 0.5f);
            covered += SmoothStep(0.4f, 2f, top - ground);
        }

        float visible = 1f - covered * 0.2f;
        return Mathf.Lerp(1f - CoverDimming * Mathf.Clamp01(strength), 1f, visible);
    }

    /// <summary>
    /// How much of the light (sun or moon) reaches each cell's ground and how squarely it meets it (0-1): unseen behind higher
    /// ground or anything standing towards the light within <paramref name="reach"/> metres, or under something standing over it.
    /// <paramref name="tops"/> holds the highest of the ground and whatever stands on it.
    /// </summary>
    public static float[] SunLight(float[] ground, float[] tops, int size, float cell, Vector3 towardLight, float reach = 60f)
    {
        var lit = new float[size * size];
        if (towardLight.sqrMagnitude < 1e-6f)
        {
            return lit;
        }

        towardLight.Normalize();
        // Below a couple of degrees the light skims the ground: nothing worth throwing back.
        if (towardLight.y <= 0.035f)
        {
            return lit;
        }

        float flat = Mathf.Sqrt(towardLight.x * towardLight.x + towardLight.z * towardLight.z);
        float stepX = flat > 1e-4f ? towardLight.x / flat : 0f;
        float stepZ = flat > 1e-4f ? towardLight.z / flat : 0f;
        float climb = flat > 1e-4f ? towardLight.y / flat * cell : float.PositiveInfinity; // metres the ray rises a cell
        int steps = flat > 1e-4f ? Mathf.CeilToInt(reach / cell) : 0;
        float highest = float.MinValue;
        foreach (float top in tops)
        {
            highest = Mathf.Max(highest, top);
        }

        Parallel.For(0, size, z =>
        {
            for (int x = 0; x < size; x++)
            {
                int i = z * size + x;
                float here = ground[i];
                float slopeX = (ground[z * size + Math.Min(x + 1, size - 1)] - ground[z * size + Math.Max(x - 1, 0)]) / (2f * cell);
                float slopeZ = (ground[Math.Min(z + 1, size - 1) * size + x] - ground[Math.Max(z - 1, 0) * size + x]) / (2f * cell);
                var normal = new Vector3(-slopeX, 1f, -slopeZ);
                float facing = Vector3.Dot(normal, towardLight) / normal.magnitude;
                if (facing <= 0f)
                {
                    continue;
                }

                // Starts a little above the ground, so the ground's own bumps between samples don't shade it.
                float ray = here + 0.3f;
                float open = 1f - SmoothStep(0f, 1.5f, tops[i] - ray);
                for (int k = 1; k <= steps && open > 0f; k++)
                {
                    ray += climb;
                    if (ray > highest)
                    {
                        break;
                    }

                    float top = Sample(tops, size, x + stepX * k, z + stepZ * k);
                    open = Mathf.Min(open, 1f - SmoothStep(0f, 1.5f, top - ray));
                }

                lit[i] = open * facing;
            }
        });
        return lit;
    }

    /// <summary>Box blur in place, <paramref name="radius"/> cells each side, along rows then columns; edges repeat the edge. Twice
    /// makes a tent.</summary>
    public static void Blur(float[] grid, int size, int radius)
    {
        if (radius <= 0 || size <= 1)
        {
            return;
        }

        Parallel.For(0, size, z =>
        {
            var line = new float[size];
            Array.Copy(grid, z * size, line, 0, size);
            BlurLine(line, grid, z * size, 1, size, radius);
        });
        Parallel.For(0, size, x =>
        {
            var line = new float[size];
            for (int z = 0; z < size; z++)
            {
                line[z] = grid[z * size + x];
            }

            BlurLine(line, grid, x, size, size, radius);
        });
    }

    private static void BlurLine(float[] source, float[] target, int start, int stride, int count, int radius)
    {
        float scale = 1f / (2 * radius + 1);
        float sum = 0f;
        for (int i = -radius; i <= radius; i++)
        {
            sum += source[Math.Min(Math.Max(i, 0), count - 1)];
        }

        for (int i = 0; i < count; i++)
        {
            target[start + i * stride] = sum * scale;
            sum += source[Math.Min(i + radius + 1, count - 1)] - source[Math.Max(i - radius, 0)];
        }
    }

    /// <summary>Bilinear sample in cell units (cell centre i at i), clamped to the grid.</summary>
    public static float Sample(float[] grid, int size, float x, float z) => Sample(grid, size, size, x, z);

    /// <summary>Bilinear sample of a <paramref name="width"/> × <paramref name="height"/> grid in cell units, clamped to it.</summary>
    public static float Sample(float[] grid, int width, int height, float x, float z)
    {
        x = x < 0f ? 0f : x > width - 1 ? width - 1 : x;
        z = z < 0f ? 0f : z > height - 1 ? height - 1 : z;
        int x0 = (int)x, z0 = (int)z;
        int x1 = Math.Min(x0 + 1, width - 1), z1 = Math.Min(z0 + 1, height - 1);
        float fx = x - x0, fz = z - z0;
        float south = grid[z0 * width + x0] + (grid[z0 * width + x1] - grid[z0 * width + x0]) * fx;
        float north = grid[z1 * width + x0] + (grid[z1 * width + x1] - grid[z1 * width + x0]) * fx;
        return south + (north - south) * fz;
    }

    public static float Mean(float[] values)
    {
        if (values == null || values.Length == 0)
        {
            return 1f;
        }

        double sum = 0;
        foreach (float value in values)
        {
            sum += value;
        }

        return (float)(sum / values.Length);
    }

    /// <summary>HLSL's smoothstep.</summary>
    public static float SmoothStep(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    /// <summary>A byte for a 0-1 value, as the shader reads it back (255 is exactly 1).</summary>
    public static byte Byte(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

    /// <summary>The lowest and highest of the ground's and the water's heights (the range slice 1's g and b are written over).</summary>
    public static void HeightRange(float[] ground, float[] water, out float lowest, out float highest)
    {
        lowest = float.MaxValue;
        highest = float.MinValue;
        foreach (float height in ground)
        {
            lowest = Mathf.Min(lowest, height);
            highest = Mathf.Max(highest, height);
        }

        if (water != null)
        {
            foreach (float level in water)
            {
                if (!float.IsNaN(level))
                {
                    lowest = Mathf.Min(lowest, level);
                    highest = Mathf.Max(highest, level);
                }
            }
        }

        if (highest <= lowest)
        {
            highest = lowest + 1f;
        }
    }

    /// <summary>A height as slice 1's green: 0-255 over the range.</summary>
    public static byte HeightByte(float height, float lowest, float highest) => Byte((height - lowest) / (highest - lowest));

    /// <summary>Metres the water's surface is held above or below a cell's ground (slice 1's blue): a byte over the whole map's heights
    /// steps a fifth of a metre, too coarse for a band a hand's breadth wide; over ± this it steps 6 cm. A surface further off is clamped
    /// (it only ever lies under deep water or high above it, where no band is drawn).</summary>
    public const float WaterReach = 8f;

    /// <summary>A water surface as slice 1's blue: metres from the cell's ground as its green reads back, 1-255 over ± <see cref="WaterReach"/>,
    /// 0 kept for "no water near" (it reads as the farthest below, so a band filtered toward it fades out).</summary>
    public static byte WaterByte(float level, byte ground, float lowest, float highest) =>
        (byte)Mathf.Clamp(1 + Mathf.RoundToInt(Mathf.Clamp01((level - HeightFrom(ground, lowest, highest) + WaterReach) / (2f * WaterReach)) * 254f), 1, 255);

    /// <summary>Slice 1's texel at a cell: the cover's shade (r) as given, the ground's height (g), the nearest water (b, 0 for none)
    /// and how near its edge is (a, 255 with none). The whole field and a patch redraw both write it this way.</summary>
    public static Color32 GroundTexel(byte cover, float groundHeight, float[] water, float[] near, int index, float lowest, float highest)
    {
        byte ground = HeightByte(groundHeight, lowest, highest);
        bool wet = water != null && !float.IsNaN(water[index]);
        return new Color32(cover, ground, wet ? WaterByte(water[index], ground, lowest, highest) : (byte)0,
            wet && near != null ? Byte(near[index]) : (byte)255);
    }

    /// <summary>A height back from slice 1's green (as the shaders read it: GroundLight.hlsl).</summary>
    public static float HeightFrom(byte ground, float lowest, float highest) => lowest + ground / 255f * (highest - lowest);

    /// <summary>A water surface back from slice 1's blue and green (as GroundLight.hlsl reads it), for tests.</summary>
    public static float WaterFrom(byte water, byte ground, float lowest, float highest) =>
        HeightFrom(ground, lowest, highest) + (water - 1) / 254f * 2f * WaterReach - WaterReach;

    /// <summary>A grid shrunk <paramref name="every"/> × <paramref name="every"/> cells to one (a part block at the far edges): their
    /// average, or their highest. With every 1, the grid itself.</summary>
    public static float[] Shrink(float[] grid, int size, int every, bool highest = false)
    {
        if (every <= 1)
        {
            return grid;
        }

        int coarse = (size + every - 1) / every;
        var shrunk = new float[coarse * coarse];
        Parallel.For(0, coarse, cz =>
        {
            for (int cx = 0; cx < coarse; cx++)
            {
                float sum = 0f, top = float.MinValue;
                int n = 0;
                for (int z = cz * every; z < Math.Min(size, (cz + 1) * every); z++)
                {
                    for (int x = cx * every; x < Math.Min(size, (cx + 1) * every); x++)
                    {
                        float value = grid[z * size + x];
                        sum += value;
                        top = Math.Max(top, value);
                        n++;
                    }
                }

                shrunk[cz * coarse + cx] = highest ? top : sum / n;
            }
        });
        return shrunk;
    }

    /// <summary>A grid shrunk by <see cref="Shrink"/> spread back over the fine one, bilinearly between the coarse cells' centres.</summary>
    public static float[] Grow(float[] coarseGrid, int coarse, int size, int every)
    {
        var grown = new float[size * size];
        Parallel.For(0, size, z =>
        {
            float cz = (z + 0.5f) / every - 0.5f;
            for (int x = 0; x < size; x++)
            {
                grown[z * size + x] = Sample(coarseGrid, coarse, (x + 0.5f) / every - 0.5f, cz);
            }
        });
        return grown;
    }

    // Thrown-back light is mostly dim: its square root keeps more steps in the dark (the shader squares it).
    private static byte Root(float[] channel, int i) => channel == null ? (byte)0 : Byte(Mathf.Sqrt(Mathf.Max(0f, channel[i])));

    // The highest of the ground and whatever stands on it, per cell: the cover texels inside each cell.
    private static float[] Tops(Recipe recipe)
    {
        int size = recipe.Size;
        var tops = (float[])recipe.Ground.Clone();
        if (recipe.Cover == null || recipe.CoverSize <= 0)
        {
            return tops;
        }

        float texelsPerMetre = recipe.CoverSize / recipe.CoverSide;
        float perCell = recipe.Cell * texelsPerMetre;
        int reachTexels = Mathf.Max(1, Mathf.CeilToInt(perCell));
        Parallel.For(0, size, z =>
        {
            for (int x = 0; x < size; x++)
            {
                int u0 = Mathf.FloorToInt((x * recipe.Cell - recipe.CoverCorner.x) * texelsPerMetre);
                int v0 = Mathf.FloorToInt((z * recipe.Cell - recipe.CoverCorner.y) * texelsPerMetre);
                float top = tops[z * size + x];
                for (int v = Math.Max(0, v0); v < Math.Min(recipe.CoverSize, v0 + reachTexels); v++)
                {
                    for (int u = Math.Max(0, u0); u < Math.Min(recipe.CoverSize, u0 + reachTexels); u++)
                    {
                        top = Math.Max(top, recipe.Cover[v * recipe.CoverSize + u]);
                    }
                }

                tops[z * size + x] = top;
            }
        });
        return tops;
    }
}
