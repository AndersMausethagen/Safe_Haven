using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Sets rock from a CliffKit into both walls of a generated ravine while a raid loads, so they read as broken rock rather than smooth
/// cuts. Each piece is stood on end (a slab becomes a rock column), set into the wall where a ray from the middle of the ravine meets the
/// terrain, and mostly buried, so only its face shows; spacing, height on the wall, size and turn are all random, with gaps. The pieces
/// in the walls are only to look at (colliders and nav obstacles off), so a fall never lands on a ledge nobody could climb from. Now and
/// then a great block stands on the rim, solid like any boulder. Necks that carry a trail are left bare. Same plan and seed, same rocks.
/// </summary>
public static class RaidRavineCrags
{
    private const float NeckClearance = 3.5f;
    private const float Gap = 0.3f;
    private const float RimBlock = 0.18f;
    private const float RimBlockBack = 1.2f;
    private const float RimBlockClear = 4f;
    private const float RimBlockWidest = 7f;
    private const float ShowsAtMost = 0.55f;
    private const float BuriedAtMost = 0.35f;

    public static void Dress(Transform holder, RaidLayoutRavine ravine, CliffKit kit, Terrain terrain, System.Func<Vector2, bool> isNeck, System.Random random)
    {
        if (kit == null || kit.Pieces.Count == 0 || ravine.Line.Count < 2 || !terrain.TryGetComponent(out TerrainCollider ground))
        {
            return;
        }

        var shapes = new Dictionary<GameObject, Vector3[]>();
        Vector3 origin = terrain.transform.position;
        float length = 0f;
        for (int i = 0; i + 1 < ravine.Line.Count; i++)
        {
            length += Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]);
        }

        foreach (int side in new[] { -1, 1 })
        {
            float along = Range(random, 0f, 3f);
            int guard = 0;
            while (along < length && guard++ < 2000)
            {
                CliffKit.Piece piece = kit.Pick(random);
                if (piece == null)
                {
                    return;
                }

                At(ravine, along, out Vector2 point, out Vector2 direction, out int segment, out float t);
                var outward = new Vector3(-direction.y, 0f, direction.x) * side;
                float half = RaidLayoutRavine.At(ravine.HalfWidths, segment, t);
                Vector2 face = point + new Vector2(outward.x, outward.z) * half;
                if (isNeck(face))
                {
                    along += NeckClearance;
                    continue;
                }

                float floor = RaidLayoutRavine.At(ravine.Floor, segment, t) + origin.y;
                float rim = RaidLayoutRavine.At(ravine.Rim, segment, t) + origin.y;
                float faceHeight = Mathf.Max(2f, rim - floor);

                // Its longest side upright: a third to two thirds of the wall.
                float tall = Mathf.Max(0.5f, Mathf.Max(piece.size.x, Mathf.Max(piece.size.y, piece.size.z)));
                float middle = piece.size.x + piece.size.y + piece.size.z - tall - Mathf.Min(piece.size.x, Mathf.Min(piece.size.y, piece.size.z));
                float scale = Mathf.Clamp(faceHeight * Range(random, 0.3f, 0.65f) / tall, 0.5f, 4f);
                float across = Mathf.Max(0.5f, middle * scale);
                if (random.NextDouble() > Gap)
                {
                    float at = floor + faceHeight * Range(random, 0.25f, 0.75f);
                    var ray = new Ray(origin + new Vector3(point.x, 0f, point.y) + Vector3.up * (at - origin.y), outward);
                    if (ground.Raycast(ray, out RaycastHit hit, half + RaidLayoutRavine.RagReach + 3f))
                    {
                        SetInWall(holder, piece, scale, hit, outward, random, shapes, ground, ray.origin, half + RaidLayoutRavine.RagReach + 3f);
                    }
                }

                Vector2 rimSpot = point + new Vector2(outward.x, outward.z) * (half + RaidLayoutRavine.RagReach + RimBlockBack);
                if (random.NextDouble() < RimBlock && !isNeck(rimSpot) && !isNeck(rimSpot + new Vector2(outward.x, outward.z) * RimBlockClear))
                {
                    StandOnRim(holder, piece, Mathf.Min(scale * 0.6f, RimBlockWidest / Mathf.Max(0.5f, Mathf.Max(piece.size.x, piece.size.z))), origin, rimSpot, outward, terrain, random, shapes);
                }

                along += across * Range(random, 0.7f, 1.5f);
            }
        }
    }

    /// <summary>
    /// Sets a piece upright into a wall where a ray along <paramref name="outward"/> (from the open side into the wall, starting at
    /// <paramref name="from"/>) met it: its face towards the open side, sunk so that a third to a half of it shows, only to look at
    /// (colliders and nav obstacles off). Also dresses a valley's walls (RaidMapGenerator, Docs/WorldLookPlan_2026-11-17.md B7).
    /// </summary>
    public static GameObject SetInWall(Transform holder, CliffKit.Piece piece, float scale, RaycastHit hit, Vector3 outward, System.Random random,
        Dictionary<GameObject, Vector3[]> shapes, TerrainCollider ground, Vector3 from, float reach)
    {
        Vector3 facing = -outward;
        GameObject made = Object.Instantiate(piece.prefab, holder);
        made.name = piece.prefab.name;
        float roll = Range(random, -12f, 12f);
        float yaw = Range(random, -15f, 15f);
        float lean = Range(random, 2f, 10f);
        made.transform.SetPositionAndRotation(hit.point,
            Quaternion.LookRotation(facing) * Quaternion.Euler(-lean, yaw, roll) * FlatToWall(piece) * StandUp(piece));
        made.transform.localScale = piece.prefab.transform.localScale * scale;
        Extent(made, piece.prefab, shapes, facing, out float front, out float back, out _, out float bottom, out float top);
        float depth = Mathf.Max(0.1f, front - back);
        float shows = depth * Range(random, BuriedAtMost, ShowsAtMost);
        float middle = (top + bottom) * 0.5f;
        made.transform.position += Vector3.up * (hit.point.y - middle);
        // Backed by the wall over its whole height: a wall leaning back above or stepping in below would leave it standing off the rock, so it
        // goes in as far as the deepest point of the wall behind it.
        float wall = Vector3.Dot(hit.point, facing);
        float half = (top - bottom) * 0.4f;
        foreach (float rise in new[] { -half, half })
        {
            var probe = new Ray(new Vector3(from.x, hit.point.y + rise, from.z), outward);
            if (ground.Raycast(probe, out RaycastHit behind, reach))
            {
                wall = Mathf.Min(wall, Vector3.Dot(behind.point, facing));
            }
        }

        made.transform.position += facing * (wall + shows - front);
        OnlyToLookAt(made);
        return made;
    }

    // A great block on the ground beside the rim: solid, as any boulder is.
    private static void StandOnRim(Transform holder, CliffKit.Piece piece, float scale, Vector3 origin, Vector2 spot, Vector3 outward, Terrain terrain,
        System.Random random, Dictionary<GameObject, Vector3[]> shapes)
    {
        var at = origin + new Vector3(spot.x, 0f, spot.y);
        at.y = terrain.SampleHeight(at) + origin.y;
        GameObject made = Object.Instantiate(piece.prefab, holder);
        made.name = piece.prefab.name;
        made.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-outward) * Quaternion.Euler(0f, Range(random, -40f, 40f), 0f) * StandUp(piece));
        made.transform.localScale = piece.prefab.transform.localScale * Mathf.Clamp(scale, 0.5f, 2.5f);
        Extent(made, piece.prefab, shapes, Vector3.up, out _, out _, out _, out float bottom, out float top);
        made.transform.position += Vector3.up * (at.y - bottom - (top - bottom) * 0.2f);
    }

    // Its longest side upright, the next along the wall and the thinnest into it, so a slab lies against the rock rather than sticking
    // out of it like a fin.
    private static Quaternion FlatToWall(CliffKit.Piece piece)
    {
        Vector3 size = piece.size;
        var axes = new[] { (size.x, 0), (size.y, 1), (size.z, 2) };
        System.Array.Sort(axes, (a, b) => b.Item1.CompareTo(a.Item1));
        var target = new Vector3[3];
        target[axes[0].Item2] = Vector3.up;
        target[axes[1].Item2] = Vector3.right;
        target[axes[2].Item2] = Vector3.forward;
        return Quaternion.LookRotation(target[2], target[1]);
    }

    /// <summary>As WorldAssets.StandUp (editor): from the prefab's own pose to its finished side facing forward, upright.</summary>
    public static Quaternion StandUp(CliffKit.Piece piece) =>
        Quaternion.Euler(piece.faceTilt, 0f, 0f) * Quaternion.Euler(0f, -piece.faceYaw, 0f);

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static void OnlyToLookAt(GameObject made)
    {
        foreach (Collider collider in made.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (NavMeshObstacle obstacle in made.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            obstacle.enabled = false;
        }
    }

    private static void At(RaidLayoutRavine ravine, float distance, out Vector2 point, out Vector2 direction, out int segment, out float t)
    {
        float run = 0f;
        for (int i = 0; i + 1 < ravine.Line.Count; i++)
        {
            float length = Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]);
            if (run + length >= distance || i + 2 == ravine.Line.Count)
            {
                segment = i;
                t = Mathf.Clamp01((distance - run) / Mathf.Max(0.0001f, length));
                point = Vector2.Lerp(ravine.Line[i], ravine.Line[i + 1], t);
                direction = (ravine.Line[i + 1] - ravine.Line[i]).normalized;
                return;
            }

            run += length;
        }

        segment = 0;
        t = 0f;
        point = ravine.Line[0];
        direction = (ravine.Line[1] - ravine.Line[0]).normalized;
    }

    /// <summary>
    /// How far the placed piece reaches along <paramref name="facing"/>: its finished side (the 70th percentile of its points, so one bump
    /// doesn't decide) and back (the 30th), and from bottom to top, by the meshes' own points as CragWall measures them.
    /// </summary>
    public static void Extent(GameObject made, GameObject prefab, Dictionary<GameObject, Vector3[]> shapes, Vector3 facing,
        out float front, out float back, out float spread, out float bottom, out float top)
    {
        if (!shapes.TryGetValue(prefab, out Vector3[] points))
        {
            var list = new List<Vector3>();
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || filter.GetComponent<MeshRenderer>() == null
                    || (filter.name.Contains("_LOD") && !filter.name.EndsWith("_LOD0")))
                {
                    continue;
                }

                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    list.Add(toRoot.MultiplyPoint3x4(vertices[i]));
                }
            }

            shapes[prefab] = points = list.ToArray();
        }

        if (points.Length == 0)
        {
            Bounds bounds = made.TryGetComponent(out Renderer renderer) ? renderer.bounds : new Bounds(made.transform.position, Vector3.one);
            float centre = Vector3.Dot(bounds.center, facing);
            spread = bounds.extents.magnitude;
            front = centre + spread * 0.5f;
            back = centre - spread * 0.5f;
            bottom = bounds.min.y;
            top = bounds.max.y;
            return;
        }

        Matrix4x4 toWorld = made.transform.localToWorldMatrix;
        var along = new float[points.Length];
        top = float.MinValue;
        bottom = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 world = toWorld.MultiplyPoint3x4(points[i]);
            along[i] = Vector3.Dot(world, facing);
            top = Mathf.Max(top, world.y);
            bottom = Mathf.Min(bottom, world.y);
        }

        System.Array.Sort(along);
        front = along[Mathf.Clamp(Mathf.RoundToInt(along.Length * 0.7f), 0, along.Length - 1)];
        back = along[Mathf.Clamp(Mathf.RoundToInt(along.Length * 0.3f), 0, along.Length - 1)];
        spread = along[along.Length - 1] - along[0];
    }
}
