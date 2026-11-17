using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lays out a map's lily pads (RaidLilyPadRule) and builds them into meshes. <see cref="Place"/> is plain arithmetic over the bed's
/// heights, so it can be tested: clumps of 5-20 pads start on a jittered grid where a patch of noise allows, and every pad floats where
/// the bed lies within the rule's depth band under still water and a shore is within reach, off trails and crossings. Pads in a clump
/// keep from overlapping and float at one of three heights a few millimetres apart, so two that touch never fight over a pixel.
/// <see cref="Build"/> merges them into one mesh per <see cref="ChunkSize"/> metres of map: a handful of draw calls, and the stretches
/// out of view are culled. Everything comes from the seed's own stream, so every co-op player gets the same pads.
/// </summary>
public static class RaidLilyPads
{
    /// <summary><see cref="Piece.Kind"/> of a flower and of a bud; pads use their index in the rule's list.</summary>
    public const int Flower = -1;
    public const int Bud = -2;

    /// <summary>Metres of map each built mesh covers, so the pads out of view are culled.</summary>
    public const float ChunkSize = 64f;

    /// <summary>Metres between the samples the shore distances are worked out on.</summary>
    private const float GridCell = 0.5f;
    // A clump's pads fall within this many of its mean pad's radius times the square root of its count from its middle: about
    // two fifths of the clump's disc covered, which pads tried at random can still fill (much over half and the big clumps stay short).
    private const float ClumpLoose = 1.4f;
    // Two pads may come this close, as a share of their radii added together: touching, the odd rim over a neighbour's.
    private const float Touching = 0.92f;
    // The three heights pads float at, so touching rims never share one.
    private const float Stagger = 0.003f;
    private const float FlowerAbove = 0.002f;
    private const float BudBeyond = 0.06f;
    private static readonly Vector2 FlowerScale = new Vector2(0.9f, 1.15f);
    private static readonly Vector2 BudScale = new Vector2(0.85f, 1.15f);
    // The patch noise's share where clumps begin and where they reach their full chance.
    private const float PatchFrom = 0.38f;
    private const float PatchFull = 0.62f;
    private const int MostPieces = 40000;
    private const float NoiseRange = 1000f;

    /// <summary>One piece laid out: where (metres from the map's corner), its pivot's height over the map's lowest point, turn and size.</summary>
    public struct Piece
    {
        public Vector2 Position;
        public float Height;
        public float Yaw;
        public float Scale;
        /// <summary>The pad's index in the rule's list, or <see cref="Flower"/> or <see cref="Bud"/>.</summary>
        public int Kind;
    }

    /// <summary>
    /// The pieces for a map <paramref name="size"/> metres across. <paramref name="bed"/>: the ground's heights in metres over the map's
    /// lowest point, rows south to north and columns west to east, spanning the map (a terrain's heights times its height).
    /// <paramref name="stillWater"/>: the surface's height at a point (same frame), NaN where there is no still water.
    /// <paramref name="keepClear"/>: true where nothing may float. <paramref name="radii"/>: each pad's radius in metres at scale 1 (0 or
    /// missing: the pad is never picked). Empty when the rule has no pad that can be picked.
    /// </summary>
    public static List<Piece> Place(RaidLilyPadRule rule, int seed, float size, float[,] bed, Func<Vector2, float> stillWater,
        Func<Vector2, bool> keepClear, IReadOnlyList<float> radii)
    {
        var pieces = new List<Piece>();
        if (rule == null || rule.pads == null || bed == null || stillWater == null || radii == null || size <= 0f)
        {
            return pieces;
        }

        var weights = new float[rule.pads.Count];
        float totalWeight = 0f, meanRadius = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            RaidLilyPadRule.Pad pad = rule.pads[i];
            float radius = i < radii.Count ? radii[i] : 0f;
            weights[i] = pad != null && radius > 0f ? Mathf.Max(0f, pad.weight) : 0f;
            totalWeight += weights[i];
            meanRadius += weights[i] * radius * (pad != null ? (pad.scale.x + pad.scale.y) * 0.5f : 1f);
        }

        if (totalWeight <= 0f)
        {
            return pieces;
        }

        meanRadius /= totalWeight;
        var shore = new ShoreGrid(size, bed, stillWater);
        var taken = new Dictionary<long, List<Vector3>>();
        var random = new System.Random(seed);
        var patches = new Vector2((float)random.NextDouble() * NoiseRange, (float)random.NextDouble() * NoiseRange);
        float spacing = Mathf.Max(1f, rule.clumpSpacing);
        int cells = Mathf.CeilToInt(size / spacing);
        int fewest = Mathf.Max(1, Mathf.Min(rule.clumpPads.x, rule.clumpPads.y));
        int most = Mathf.Max(fewest, rule.clumpPads.y);

        bool Floats(Vector2 at, out float level)
        {
            level = float.NaN;
            if (at.x < 0f || at.y < 0f || at.x > size || at.y > size)
            {
                return false;
            }

            level = stillWater(at);
            if (float.IsNaN(level))
            {
                return false;
            }

            float depth = level - Bed(bed, size, at);
            return depth >= rule.depth.x && depth <= rule.depth.y && shore.At(at) <= rule.shoreReach && (keepClear == null || !keepClear(at));
        }

        for (int gy = 0; gy < cells && pieces.Count < MostPieces; gy++)
        {
            for (int gx = 0; gx < cells && pieces.Count < MostPieces; gx++)
            {
                // Three draws a spot whatever comes of it, so one spot's luck never moves another's clump.
                var centre = new Vector2((gx + (float)random.NextDouble()) * spacing, (gy + (float)random.NextDouble()) * spacing);
                double roll = random.NextDouble();
                if (!Floats(centre, out _))
                {
                    continue;
                }

                float patch = Mathf.PerlinNoise(centre.x / rule.patchSize + patches.x, centre.y / rule.patchSize + patches.y);
                if (roll >= rule.clumpChance * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PatchFrom, PatchFull, patch)))
                {
                    continue;
                }

                var clump = new System.Random(unchecked(seed * 486187739 + gx * 73856093 + gy * 19349663));
                Clump(rule, clump, centre, clump.Next(fewest, most + 1), meanRadius, weights, totalWeight, radii, Floats, taken, pieces);
            }
        }

        return pieces;
    }

    // One clump round its middle: pads tried at random spots until it holds its count or the tries run out.
    private delegate bool FloatTest(Vector2 at, out float level);

    private static void Clump(RaidLilyPadRule rule, System.Random random, Vector2 centre, int count, float meanRadius, float[] weights,
        float totalWeight, IReadOnlyList<float> radii, FloatTest floats, Dictionary<long, List<Vector3>> taken, List<Piece> pieces)
    {
        float spread = ClumpLoose * Mathf.Sqrt(count) * meanRadius;
        int made = 0;
        for (int attempt = 0; attempt < count * 8 && made < count && pieces.Count < MostPieces; attempt++)
        {
            int kind = PickByWeight(weights, totalWeight, random);
            RaidLilyPadRule.Pad pad = rule.pads[kind];
            float scale = Mathf.Lerp(pad.scale.x, pad.scale.y, (float)random.NextDouble());
            float radius = radii[kind] * scale;
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float reach = attempt == 0 ? 0f : spread * Mathf.Sqrt((float)random.NextDouble());
            var at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
            float yaw = (float)random.NextDouble() * 360f;
            double flowerRoll = random.NextDouble(), budRoll = random.NextDouble();
            float budAngle = (float)random.NextDouble() * Mathf.PI * 2f;
            float extra = (float)random.NextDouble();
            if (!floats(at, out float level) || Overlaps(taken, at, radius))
            {
                continue;
            }

            Take(taken, at, radius);
            float height = level + rule.aboveWater + Stagger * (made % 3);
            pieces.Add(new Piece { Position = at, Height = height, Yaw = yaw, Scale = scale, Kind = kind });
            made++;
            if (rule.flower != null && flowerRoll < rule.flowerChance)
            {
                pieces.Add(new Piece
                {
                    Position = at, Height = height + FlowerAbove, Yaw = (yaw + 137f) % 360f,
                    Scale = Mathf.Lerp(FlowerScale.x, FlowerScale.y, extra), Kind = Flower,
                });
            }

            if (rule.bud != null && budRoll < rule.budChance)
            {
                Vector2 beside = at + new Vector2(Mathf.Cos(budAngle), Mathf.Sin(budAngle)) * (radius + BudBeyond);
                if (floats(beside, out float budLevel))
                {
                    pieces.Add(new Piece
                    {
                        Position = beside, Height = budLevel, Yaw = (yaw * 3f) % 360f,
                        Scale = Mathf.Lerp(BudScale.x, BudScale.y, extra), Kind = Bud,
                    });
                }
            }
        }
    }

    private static int PickByWeight(float[] weights, float total, System.Random random)
    {
        double roll = random.NextDouble() * total;
        int last = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0f)
            {
                continue;
            }

            last = i;
            roll -= weights[i];
            if (roll < 0d)
            {
                return i;
            }
        }

        return last;
    }

    // Pads already floating, in 1 m cells: (x, y, radius).
    private static long CellOf(Vector2 at) => ((long)Mathf.FloorToInt(at.x) << 32) ^ (uint)Mathf.FloorToInt(at.y);

    private static bool Overlaps(Dictionary<long, List<Vector3>> taken, Vector2 at, float radius)
    {
        int cx = Mathf.FloorToInt(at.x), cy = Mathf.FloorToInt(at.y);
        for (int y = cy - 1; y <= cy + 1; y++)
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                if (!taken.TryGetValue(((long)x << 32) ^ (uint)y, out List<Vector3> near))
                {
                    continue;
                }

                foreach (Vector3 other in near)
                {
                    float apart = (radius + other.z) * Touching;
                    if ((new Vector2(other.x, other.y) - at).sqrMagnitude < apart * apart)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void Take(Dictionary<long, List<Vector3>> taken, Vector2 at, float radius)
    {
        long key = CellOf(at);
        if (!taken.TryGetValue(key, out List<Vector3> list))
        {
            taken[key] = list = new List<Vector3>();
        }

        list.Add(new Vector3(at.x, at.y, radius));
    }

    /// <summary>The bed's height at a point, between the four samples round it (as the terrain draws it).</summary>
    public static float Bed(float[,] bed, float size, Vector2 at)
    {
        int rows = bed.GetLength(0), columns = bed.GetLength(1);
        float x = Mathf.Clamp01(at.x / size) * (columns - 1), y = Mathf.Clamp01(at.y / size) * (rows - 1);
        int x0 = Mathf.Min((int)x, columns - 2), y0 = Mathf.Min((int)y, rows - 2);
        float tx = x - x0, ty = y - y0;
        float south = Mathf.Lerp(bed[y0, x0], bed[y0, x0 + 1], tx);
        float north = Mathf.Lerp(bed[y0 + 1, x0], bed[y0 + 1, x0 + 1], tx);
        return Mathf.Lerp(south, north, ty);
    }

    /// <summary>
    /// How far each spot under still water lies from the nearest dry ground (a bank, a hummock, a string, the bed above the surface), on a
    /// <see cref="GridCell"/> grid: two sweeps of a chamfer distance, which never comes out nearer than the true distance.
    /// </summary>
    private sealed class ShoreGrid
    {
        private readonly int count;
        private readonly float cell;
        private readonly float[] distance;

        public ShoreGrid(float size, float[,] bed, Func<Vector2, float> stillWater)
        {
            count = Mathf.Max(2, Mathf.CeilToInt(size / GridCell) + 1);
            cell = size / (count - 1);
            distance = new float[count * count];
            for (int y = 0; y < count; y++)
            {
                for (int x = 0; x < count; x++)
                {
                    var at = new Vector2(x * cell, y * cell);
                    float level = stillWater(at);
                    distance[y * count + x] = !float.IsNaN(level) && level > Bed(bed, size, at) ? float.MaxValue : 0f;
                }
            }

            float straight = cell, diagonal = cell * Mathf.Sqrt(2f);
            for (int y = 0; y < count; y++)
            {
                for (int x = 0; x < count; x++)
                {
                    int i = y * count + x;
                    float d = distance[i];
                    if (d == 0f) continue;
                    if (x > 0) d = Mathf.Min(d, distance[i - 1] + straight);
                    if (y > 0)
                    {
                        d = Mathf.Min(d, distance[i - count] + straight);
                        if (x > 0) d = Mathf.Min(d, distance[i - count - 1] + diagonal);
                        if (x < count - 1) d = Mathf.Min(d, distance[i - count + 1] + diagonal);
                    }

                    distance[i] = d;
                }
            }

            for (int y = count - 1; y >= 0; y--)
            {
                for (int x = count - 1; x >= 0; x--)
                {
                    int i = y * count + x;
                    float d = distance[i];
                    if (d == 0f) continue;
                    if (x < count - 1) d = Mathf.Min(d, distance[i + 1] + straight);
                    if (y < count - 1)
                    {
                        d = Mathf.Min(d, distance[i + count] + straight);
                        if (x < count - 1) d = Mathf.Min(d, distance[i + count + 1] + diagonal);
                        if (x > 0) d = Mathf.Min(d, distance[i + count - 1] + diagonal);
                    }

                    distance[i] = d;
                }
            }
        }

        /// <summary>The farthest of the four samples round a point: a point is called near a shore only when all of them are.</summary>
        public float At(Vector2 at)
        {
            float x = Mathf.Clamp(at.x / cell, 0f, count - 1), y = Mathf.Clamp(at.y / cell, 0f, count - 1);
            int x0 = Mathf.Min((int)x, count - 2), y0 = Mathf.Min((int)y, count - 2);
            int i = y0 * count + x0;
            return Mathf.Max(Mathf.Max(distance[i], distance[i + 1]), Mathf.Max(distance[i + count], distance[i + count + 1]));
        }
    }

    /// <summary>
    /// Builds the pieces into meshes under <paramref name="holder"/> (placed at the map's corner, unturned): one per <see cref="ChunkSize"/>
    /// metres of map that holds any, no collider, casting no shadow. A piece whose mesh is missing is left out. Adds the meshes made to
    /// <paramref name="made"/> for the map's owner to destroy, and returns how many vertices they hold. The source meshes must be readable
    /// in a player (the model's Read/Write).
    /// </summary>
    public static int Build(Transform holder, List<Piece> pieces, IReadOnlyList<Mesh> padMeshes, Mesh flower, Mesh bud, Material material,
        List<Mesh> made)
    {
        var chunks = new SortedDictionary<long, List<CombineInstance>>();
        foreach (Piece piece in pieces)
        {
            Mesh mesh = piece.Kind == Flower ? flower : piece.Kind == Bud ? bud
                : piece.Kind >= 0 && piece.Kind < padMeshes.Count ? padMeshes[piece.Kind] : null;
            if (mesh == null)
            {
                continue;
            }

            long key = ((long)Mathf.FloorToInt(piece.Position.x / ChunkSize) << 32) ^ (uint)Mathf.FloorToInt(piece.Position.y / ChunkSize);
            if (!chunks.TryGetValue(key, out List<CombineInstance> parts))
            {
                chunks[key] = parts = new List<CombineInstance>();
            }

            parts.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(new Vector3(piece.Position.x, piece.Height, piece.Position.y), Quaternion.Euler(0f, piece.Yaw, 0f),
                    Vector3.one * piece.Scale),
            });
        }

        int vertices = 0;
        foreach (KeyValuePair<long, List<CombineInstance>> chunk in chunks)
        {
            int count = 0;
            foreach (CombineInstance part in chunk.Value)
            {
                count += part.mesh.vertexCount;
            }

            var mesh = new Mesh { name = "Lily Pads", indexFormat = count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.CombineMeshes(chunk.Value.ToArray(), true, true, false);
            mesh.RecalculateBounds();
            // Never read again: the CPU copy is let go.
            mesh.UploadMeshData(true);
            made.Add(mesh);
            vertices += count;

            var stretch = new GameObject($"Lily Pads {chunk.Key >> 32},{(int)(chunk.Key & 0xffffffff)}");
            stretch.transform.SetParent(holder, false);
            stretch.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = stretch.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        return vertices;
    }

    /// <summary>A pad mesh's radius at scale 1: the farthest its bounds reach from the pivot across the water.</summary>
    public static float Radius(Mesh mesh)
    {
        if (mesh == null)
        {
            return 0f;
        }

        Bounds bounds = mesh.bounds;
        return Mathf.Max(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)));
    }
}
