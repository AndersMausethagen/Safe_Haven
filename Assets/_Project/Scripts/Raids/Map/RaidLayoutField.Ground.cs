using UnityEngine;

/// <summary>
/// The ground the field gives the terrain: the height at a point (levels, ponds sunk into basins, ravines, rivers and
/// spires on top), the height map in metres or scaled, what is kept clear of trees and rocks, and the path, forest and
/// clearing zones. Part of RaidLayoutField, in its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    private const float SoilEdge = 1.5f;

    private const float WobbleScale = 11f;

    private const float RoughFrom = 9f;

    private const float RidgeVariety = 0.55f;

    private const float RidgeVarietyScale = 46f;

    private const float PondBank = 3.5f;

    private const float PondBelowRim = 0.12f;

    private const float PondRimBeyond = 2f;

    private const int PondRimSamples = 24;

    private const float PondSoil = 2.4f;

    private const float RiverIntoBank = 0.5f;

    private const float RavineSoil = 1f;

    private const float KeepClearBeyond = 1.5f;

    /// <summary>
    /// True where nothing may stand, in metres from the map's south-west corner: in a ravine or at its very edge, and on a spire. What grows
    /// or lies there could never be reached, so trees, rocks and resources keep off.
    /// </summary>
    public bool KeepsClear(Vector2 point)
    {
        // Nothing grows or lies in a fen's open water (a drowned pine is a set piece's, not the scatter's).
        if (rules.Fen.enabled && HeightAt(point.x / plan.Size, point.y / plan.Size) < FenWater + FenDryAbove)
        {
            return true;
        }

        if (ravineEdge != null && Sample(ravineEdge, point.x / plan.Size, point.y / plan.Size) < KeepClearBeyond + RaidLayoutRavine.RagReach)
        {
            return true;
        }

        if (riverEdge != null && Sample(riverEdge, point.x / plan.Size, point.y / plan.Size) < RiverIntoBank)
        {
            return true;
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if ((point - spire.Position).sqrMagnitude < (spire.Reach + KeepClearBeyond) * (spire.Reach + KeepClearBeyond)
                && spire.Edge(point) < KeepClearBeyond)
            {
                return true;
            }
        }

        return false;
    }

    // Just below the lowest ground around it, so the water never stands above its own banks.
    private float PondLevel(RaidLayoutPond pond)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < PondRimSamples; i++)
        {
            Vector2 rim = pond.Rim(i * Mathf.PI * 2f / PondRimSamples, PondRimBeyond);
            lowest = Mathf.Min(lowest, GroundAt(rim.x / plan.Size, rim.y / plan.Size));
        }

        return lowest - PondBelowRim;
    }

    /// <summary>The ground's height at (u, v), in metres above the map's lowest point.</summary>
    public float HeightAt(float u, float v)
    {
        float height = GroundAt(u, v);
        if (plan.Ponds.Count == 0 && plan.Spires.Count == 0 && ravineEdge == null && riverEdge == null)
        {
            return height;
        }

        var point = new Vector2(u * plan.Size, v * plan.Size);
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            height = Basin(pond, point, height);
        }

        if (riverEdge != null)
        {
            height = RiverCut(u, v, height);
        }

        if (plan.Spires.Count > 0)
        {
            height = Spires(point, height);
        }

        return ravineEdge != null ? Cut(u, v, height) : height;
    }

    // The bed sinks from the shore to the pond's depth; the bank leads back up to the ground around it.
    private static float Basin(RaidLayoutPond pond, Vector2 point, float ground)
    {
        float far = pond.Reach + PondBank;
        if ((point - pond.Position).sqrMagnitude >= far * far)
        {
            return ground;
        }

        float edge = pond.Edge(point);
        if (edge >= PondBank)
        {
            return ground;
        }

        if (edge >= 0f)
        {
            return Mathf.Lerp(pond.Level, ground, Mathf.SmoothStep(0f, 1f, edge / PondBank));
        }

        float slope = Mathf.Min(Mathf.Min(pond.Radii.x, pond.Radii.y) * 0.8f, 3.5f);
        return pond.Level - pond.Depth * Mathf.SmoothStep(0f, 1f, -edge / slope);
    }

    private float GroundAt(float u, float v)
    {
        float x = u * plan.Size;
        float y = v * plan.Size;
        float open = Mathf.Max(0f, OpenDistance(u, v));
        float ground = rules.GroundHeight + (Noise(x, y, rules.HillScale, 0f) - 0.5f) * 2f * rules.Hills * plan.Hilliness;
        if (plan.Hills.Count > 0)
        {
            var point = new Vector2(x, y);
            foreach (RaidLayoutHill hill in plan.Hills)
            {
                ground += hill.RiseAt(point);
            }
        }

        float variety = 1f + (Noise(x, y, RidgeVarietyScale, 31f) - 0.5f) * 2f * RidgeVariety;
        float ridges = plan.Regions.Count > 1 ? plan.Blend(new Vector2(x, y), mood => mood.ridges) : plan.Mood.ridges;
        float ridge = Mathf.SmoothStep(0f, 1f, open / rules.RidgeWidth) * rules.RidgeHeight * ridges * variety;
        float rough = (Noise(x, y, RoughnessScale, 67f) - 0.5f) * 2f * rules.Roughness * Mathf.Clamp01(open / RoughFrom);
        float edge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(plan.Size - x, plan.Size - y)));
        float rim = Mathf.SmoothStep(0f, 1f, 1f - edge / rules.RimWidth) * rules.RimHeight;
        float level = levelHeight != null ? Sample(levelHeight, u, v) : 0f;
        float valley = valleyBeyond != null ? ValleyAt(x, y, u, v) : 0f;
        if (rules.Fen.enabled)
        {
            // A fen turns the ridges over: the wild ground sinks under the water instead of rising (RaidLayoutField.Fen).
            return ground + FenAt(x, y, open) + rough * FenRoughShare + rim + level + valley + lift;
        }

        return ground + ridge + rough + rim + level + valley + lift;
    }

    /// <summary>The heights as a terrain takes them (rows from the south, 0–1 of the map's height).</summary>
    public float[,] Heights(int size, float mapHeight) => Scaled(Metres(size, out _), mapHeight);

    /// <summary>The heights in metres above the map's lowest point (rows from the south), and the highest of them.</summary>
    public float[,] Metres(int size, out float highest)
    {
        var heights = new float[size, size];
        var rowHighest = new float[size];
        float last = size - 1f;
        // Row by row on every core: HeightAt only reads what Build worked out.
        System.Threading.Tasks.Parallel.For(0, size, row =>
        {
            float top = 0f;
            for (int column = 0; column < size; column++)
            {
                float height = HeightAt(column / last, row / last);
                heights[row, column] = height;
                top = Mathf.Max(top, height);
            }

            rowHighest[row] = top;
        });
        if (rules.CliffRoughness > 0f || rules.CliffGullies > 0f || rules.Talus > 0f)
        {
            Roughen(heights, rowHighest);
        }

        highest = 0f;
        foreach (float top in rowHighest)
        {
            highest = Mathf.Max(highest, top);
        }

        return heights;
    }

    // Rough cliffs: from this slope (degrees) the relief starts, and from RoughFullSlope it is whole; none within RoughClearOfWalked metres
    // of a trail or clearing, coming in over RoughWalkedRamp more; by water (a river or pond, within RoughShore metres of its edge) it only
    // lifts. Its noise: a ridged fold every RoughFold metres, a finer one every RoughGrain, and grit every RoughGrit.
    private const float RoughFromSlope = 24f;
    private const float RoughFullSlope = 42f;
    private const float RoughClearOfWalked = 1.5f;
    private const float RoughWalkedRamp = 2.5f;
    private const float RoughShore = 3f;
    private const float RoughFold = 7f;
    private const float RoughGrain = 2.6f;
    private const float RoughGrit = 1.1f;
    // Of Cliff Roughness: the most cut (carve 1) and the most raised (carve 0).
    private const float RoughCarve = 1.25f;
    private const float RoughRaise = 0.3f;

    /// <summary>
    /// Jagged relief on steep ground (Layout Rules › Cliff Roughness, the user's "unnaturally smooth" cliff edges and river banks): read from
    /// the finished heights, so it follows every cliff the field makes (terrace cliffs, ravine and valley walls, banks), a ridged noise
    /// added by how steep each spot is. Never on trails, clearings or level ground, so nothing walkable moves; only upward beside water. The
    /// field's own heights (HeightAt, which placement and the golden tests read) are left as they are.
    /// </summary>
    private void Roughen(float[,] heights, float[] rowHighest)
    {
        int size = heights.GetLength(0);
        float step = plan.Size / (size - 1f);
        float amount = rules.CliffRoughness;
        var original = (float[,])heights.Clone();
        // The talus needs every cell's slope first: where the faces are that scree falls from.
        float[,] slopes = rules.Talus > 0f ? Slopes(original, step) : null;
        System.Threading.Tasks.Parallel.For(1, size - 1, row =>
        {
            float top = rowHighest[row];
            for (int column = 1; column < size - 1; column++)
            {
                float dx = (original[row, column + 1] - original[row, column - 1]) / (2f * step);
                float dy = (original[row + 1, column] - original[row - 1, column]) / (2f * step);
                float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Rad2Deg;
                float steep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoughFromSlope, RoughFullSlope, slope));
                float u = column / (size - 1f), v = row / (size - 1f);
                float x = column * step, y = row * step;
                // A shore however gentle: the last metres of a bank into the water and the first out of it.
                float shore = ShoreShare(new Vector2(x, y), u, v);
                // Scree against a cliff's foot (the cell itself not a face): how near the face above, 0 none.
                float foot = slopes != null && steep < 0.6f ? CliffFoot(slopes, original, row, column, step) : 0f;
                if (steep <= 0.001f && shore <= 0.001f && foot <= 0.001f)
                {
                    continue;
                }

                float walked = WalkedDistance(u, v);
                float away = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((walked - RoughClearOfWalked) / RoughWalkedRamp));
                if (away <= 0.001f)
                {
                    continue;
                }

                // Carved, as erosion carves a cliff: sharp gullies down the faces and notches bitten out of the lip (ridged noise taken away),
                // with only small bumps raised. A first try raised the ridges as much as it cut and stood a saw blade of spikes along every
                // lip (seen in the Woods, 2026-11-18).
                float fold = 1f - Mathf.Abs(2f * Noise(x, y, RoughFold, 211f) - 1f);
                float grain = 1f - Mathf.Abs(2f * Noise(x, y, RoughGrain, 223f) - 1f);
                float grit = Noise(x, y, RoughGrit, 227f);
                float carve = fold * 0.6f + grain * 0.3f + grit * 0.1f;
                float jag = (RoughRaise - carve * RoughCarve) * amount * steep * away;
                // Gullies down the face's fall line (its gradient), broken and offset.
                if (rules.CliffGullies > 0f && steep > 0.001f)
                {
                    jag -= Gullies(x, y, dx, dy) * rules.CliffGullies * steep * away;
                }

                bool byWater = NearWater(new Vector2(x, y), u, v);
                if (jag < 0f && byWater)
                {
                    jag = 0f;
                }

                // Scree heaped at the foot, rough as rubble; never by water (it would dam a river or fill a pool's edge).
                if (foot > 0.001f && !byWater)
                {
                    float rubble = 0.75f + 0.5f * Noise(x, y, RoughGrit * 1.3f, 251f);
                    jag += rules.Talus * foot * rubble * away;
                }

                // The shoreline breaks up: ridged patches of bank lifted into spurs and shelves of ground at the water's edge, and the
                // bays between left as they are, so the line wanders in and out and never opens a hole beside the water.
                if (shore > 0.001f)
                {
                    float patch = Mathf.Max(0f, (1f - Mathf.Abs(2f * Noise(x, y, ShorePatch, 239f) - 1f)) - 0.35f) / 0.65f;
                    float bump = Mathf.Max(0f, grain - 0.4f) / 0.6f;
                    jag = Mathf.Max(jag, (patch * 0.75f + bump * 0.25f) * amount * ShoreLift * shore * away);
                }

                float height = original[row, column] + jag;
                heights[row, column] = height;
                top = Mathf.Max(top, height);
            }

            rowHighest[row] = top;
        });
    }

    // Gullies: two octaves of channels across the fall line, each from a grid of cells whose own offset and phase break them into
    // runnels that start and stop (the idea of Rune Skovbo Johansen's erosion filter, 2026: noise whose stripes follow the slope, so
    // no simulation; written here from that idea). Metres between channels and the cells' size.
    private const float GullySpacing = 2.4f;
    private const float GullyCell = 5f;
    private const float FineGullySpacing = 1.1f;
    private const float FineGullyCell = 2.5f;

    /// <summary>How deep the gullies are at a point (0-1) on a face whose gradient (uphill) is (<paramref name="gx"/>,
    /// <paramref name="gy"/>): channels running down the fall line. Pure arithmetic on the plan's seed.</summary>
    public float Gullies(float x, float y, float gx, float gy)
    {
        float length = Mathf.Sqrt(gx * gx + gy * gy);
        if (length < 1e-4f)
        {
            return 0f;
        }

        // Across the fall line: stripes that vary across it and run along it are channels down the face.
        float ax = -gy / length, ay = gx / length;
        float coarse = Channels(x, y, ax, ay, GullySpacing, GullyCell, 263f);
        float fine = Channels(x, y, ax, ay, FineGullySpacing, FineGullyCell, 271f);
        return Mathf.Clamp01(coarse * 0.75f + fine * 0.35f);
    }

    private float Channels(float x, float y, float ax, float ay, float spacing, float cell, float salt)
    {
        int cx = Mathf.FloorToInt(x / cell), cy = Mathf.FloorToInt(y / cell);
        float total = 0f, weights = 0f;
        for (int j = -1; j <= 1; j++)
        {
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                float centreX = (gx + 0.5f + (CellHash(gx, gy, salt) - 0.5f) * 0.8f) * cell;
                float centreY = (gy + 0.5f + (CellHash(gx, gy, salt + 17f) - 0.5f) * 0.8f) * cell;
                float phase = CellHash(gx, gy, salt + 31f) * Mathf.PI * 2f;
                float ox = x - centreX, oy = y - centreY;
                float weight = Mathf.Exp(-(ox * ox + oy * oy) / (cell * cell * 0.45f));
                total += Mathf.Cos((ox * ax + oy * ay) / spacing * Mathf.PI * 2f + phase) * weight;
                weights += weight;
            }
        }

        float wave = weights > 1e-6f ? total / weights : 0f;
        // The troughs are the channels: narrow and sharp-bottomed, the ribs between them left.
        float trough = Mathf.Clamp01(-wave);
        return trough * trough * (3f - 2f * trough);
    }

    private float CellHash(int x, int y, float salt)
    {
        float h = Mathf.Sin(x * 127.1f + y * 311.7f + salt + plan.NoiseOffset.x * 0.013f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // Each cell's slope in degrees, from the heights.
    private static float[,] Slopes(float[,] heights, float step)
    {
        int size = heights.GetLength(0);
        var slopes = new float[size, size];
        System.Threading.Tasks.Parallel.For(1, size - 1, row =>
        {
            for (int column = 1; column < size - 1; column++)
            {
                float dx = (heights[row, column + 1] - heights[row, column - 1]) / (2f * step);
                float dy = (heights[row + 1, column] - heights[row - 1, column]) / (2f * step);
                slopes[row, column] = Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Rad2Deg;
            }
        });
        return slopes;
    }

    // A face steeper than this sheds scree; it must stand this many metres above the cell.
    private const float TalusFace = 50f;
    private const float TalusFaceAbove = 1.5f;

    // How near a cliff's foot a cell is (1 at the face, 0 at Talus Reach or with none above): eight ways out, a step at a time.
    private float CliffFoot(float[,] slopes, float[,] heights, int row, int column, float step)
    {
        int size = slopes.GetLength(0);
        float reach = rules.TalusReach;
        int steps = Mathf.Max(1, Mathf.CeilToInt(reach / step));
        float here = heights[row, column];
        float nearest = float.MaxValue;
        for (int way = 0; way < 8; way++)
        {
            int ox = way == 0 || way == 4 ? 0 : way < 4 ? 1 : -1;
            int oy = way == 2 || way == 6 ? 0 : way > 2 && way < 6 ? -1 : 1;
            float stride = ox != 0 && oy != 0 ? 1.4142f : 1f;
            for (int k = 1; k <= steps; k++)
            {
                int r = row + oy * k, c = column + ox * k;
                if (r < 1 || c < 1 || r >= size - 1 || c >= size - 1)
                {
                    break;
                }

                float distance = k * step * stride;
                if (distance >= nearest || distance > reach)
                {
                    break;
                }

                if (slopes[r, c] > TalusFace && heights[r, c] > here + TalusFaceAbove)
                {
                    nearest = distance;
                    break;
                }
            }
        }

        if (nearest > reach)
        {
            return 0f;
        }

        float share = 1f - nearest / reach;
        return share * share;
    }

    // The shoreline pass: metres of bank lifted at most (a share of Cliff Roughness), the patches' size, and how far either side of the
    // water's edge it reaches: into the water (a spur of bank) and up the bank.
    private const float ShoreLift = 0.7f;
    private const float ShorePatch = 4.5f;
    private const float ShoreInto = 1.2f;
    private const float ShoreUp = 2.5f;

    // How much a spot is shore (0-1): most at a river's or pond's edge, fading ShoreInto metres into the water and ShoreUp up the bank.
    private float ShoreShare(Vector2 point, float u, float v)
    {
        float best = 0f;
        if (riverEdge != null)
        {
            best = Mathf.Max(best, ShoreAt(Sample(riverEdge, u, v)));
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            float far = pond.Reach + ShoreUp;
            if ((point - pond.Position).sqrMagnitude < far * far)
            {
                best = Mathf.Max(best, ShoreAt(pond.Edge(point)));
            }
        }

        return best;
    }

    private static float ShoreAt(float edge) =>
        edge < 0f ? Mathf.Clamp01(1f + edge / ShoreInto) : Mathf.Clamp01(1f - edge / ShoreUp);

    // Within RoughShore metres of a river's or a pond's edge (the fen's sheet covers whatever dips under it, so it needs no care).
    private bool NearWater(Vector2 point, float u, float v)
    {
        if (riverEdge != null && Sample(riverEdge, u, v) < RoughShore)
        {
            return true;
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            float far = pond.Reach + RoughShore;
            if ((point - pond.Position).sqrMagnitude < far * far && pond.Edge(point) < RoughShore)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Heights in metres as 0-1 of a terrain this many metres high, in place.</summary>
    public static float[,] Scaled(float[,] metres, float mapHeight)
    {
        int rows = metres.GetLength(0), columns = metres.GetLength(1);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                metres[row, column] = Mathf.Clamp01(metres[row, column] / mapHeight);
            }
        }

        return metres;
    }

    /// <summary>The painted zones a map needs, each resolution × resolution with row 0 at the south.</summary>
    public void Zones(out byte[] path, out byte[] forest, out byte[] clearing)
    {
        path = new byte[resolution * resolution];
        forest = new byte[resolution * resolution];
        clearing = new byte[resolution * resolution];
        float density = rules.ForestDensity * plan.Mood.forest;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                float worldX = x * cell;
                float worldY = y * cell;
                // A little noise on the edges, so a clearing is never a perfect circle.
                float wobble = (Noise(worldX, worldY, WobbleScale, 11f) - 0.5f) * 2f * EdgeWobble;
                float patches = 0.55f + 0.9f * Noise(worldX, worldY, rules.ForestPatches, 113f);
                path[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (trailDistance[index] + SoilEdge) / (SoilEdge * 2f)));
                clearing[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (clearDistance[index] + wobble + SoilEdge) / (SoilEdge * 2f)));
                float here = plan.Regions.Count > 1 ? rules.ForestDensity * plan.Blend(new Vector2(worldX, worldY), mood => mood.forest) : density;
                forest[index] = Byte(here * patches * Mathf.SmoothStep(0f, 1f, (openDistance[index] + wobble) / ForestEdge));
                if (ravineEdge != null && ravineEdge[index] < 0f)
                {
                    // Gravel on a ravine's floor, which the walls' rock gives way to.
                    float floorEdge = -RaidLayoutRavine.WallRun(ravineHalf[index]);
                    float gravel = 1f - Mathf.SmoothStep(0f, 1f, (ravineEdge[index] - floorEdge + RavineSoil) / RavineSoil);
                    path[index] = (byte)Mathf.Max(path[index], Byte(gravel));
                }
            }
        }

        // A bed of bare soil under the water and along the shore: trees, rocks and plants keep off it as they do off a trail.
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            float far = pond.Reach + PondSoil;
            int xMin = Mathf.Max(0, Mathf.FloorToInt((pond.Position.x - far) / cell));
            int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((pond.Position.x + far) / cell));
            int yMin = Mathf.Max(0, Mathf.FloorToInt((pond.Position.y - far) / cell));
            int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((pond.Position.y + far) / cell));
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float soil = 1f - Mathf.SmoothStep(0f, 1f, pond.Edge(new Vector2(x * cell, y * cell)) / PondSoil);
                    path[y * resolution + x] = (byte)Mathf.Max(path[y * resolution + x], Byte(soil));
                }
            }
        }
    }

    /// <summary>
    /// Where the roads run (the two ways to the exit ahead, at Road Width), as a zone like <see cref="Zones"/>' path, for the map's Road
    /// Layer (G5). Worked out only when asked, so a map that paints no roads pays nothing; null when the plan has no routes.
    /// </summary>
    public byte[] RoadZone()
    {
        if (plan.ShortRoute.Count == 0 && plan.LongRoute.Count == 0)
        {
            return null;
        }

        float[] road = Filled(reach);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        for (int i = 0; i < plan.Trails.Count; i++)
        {
            if (plan.IsRoad(i))
            {
                StampLine(plan.Trails[i], halfWidth * rules.RoadWidth, road);
            }
        }

        var zone = new byte[resolution * resolution];
        for (int index = 0; index < zone.Length; index++)
        {
            zone[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (road[index] + SoilEdge) / (SoilEdge * 2f)));
        }

        return zone;
    }

    private static byte Byte(float strength) => (byte)Mathf.RoundToInt(Mathf.Clamp01(strength) * 255f);
}
