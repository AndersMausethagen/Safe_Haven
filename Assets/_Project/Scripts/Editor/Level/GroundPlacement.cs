using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Puts objects on the ground for the level design tools: finds the ground under a point (colliders, then
/// terrain height), sets an object down by the bottom of its meshes rather than its pivot, and optionally tilts it with
/// the slope. Menu: Safe Haven > World > Place > Drop Selection to Ground (End) / Drop and Align to Slope (Shift+End).</summary>
public static class GroundPlacement
{
    [MenuItem("Safe Haven/World/Place/Drop Selection to Ground _END")]
    private static void DropMenu() => DropSelection(false);

    [MenuItem("Safe Haven/World/Place/Drop and Align to Slope #_END")]
    private static void AlignMenu() => DropSelection(true);

    [MenuItem("Safe Haven/World/Place/Drop Selection to Ground _END", true)]
    [MenuItem("Safe Haven/World/Place/Drop and Align to Slope #_END", true)]
    private static bool HasSelection() => Selection.transforms.Length > 0;

    private static void DropSelection(bool align)
    {
        Undo.IncrementCurrentGroup();
        int dropped = Selection.transforms.Count(t => Drop(t, align));
        Undo.SetCurrentGroupName(align ? "Drop and Align to Slope" : "Drop to Ground");
        Debug.Log($"Level: set {dropped} of {Selection.transforms.Length} object(s) on the ground.");
    }

    /// <summary>The ground under <paramref name="point"/>: the first non-trigger collider below it that is not part of
    /// <paramref name="ignore"/>, else the terrain's height there.</summary>
    public static bool TryGround(Vector3 point, out Vector3 position, out Vector3 normal, Transform ignore = null, float above = 60f)
    {
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * above, Vector3.down, above * 2f + 100f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            if (ignore != null && hit.transform.IsChildOf(ignore))
            {
                continue;
            }

            position = hit.point;
            normal = hit.normal;
            return true;
        }

        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            TerrainData data = terrain.terrainData;
            if (data == null)
            {
                continue;
            }

            Vector3 local = point - terrain.transform.position;
            if (local.x < 0f || local.z < 0f || local.x > data.size.x || local.z > data.size.z)
            {
                continue;
            }

            position = new Vector3(point.x, terrain.transform.position.y + terrain.SampleHeight(point), point.z);
            normal = data.GetInterpolatedNormal(local.x / data.size.x, local.z / data.size.z);
            return true;
        }

        position = point;
        normal = Vector3.up;
        return false;
    }

    /// <summary>How far the object's pivot is above the bottom of its meshes (0 without renderers).</summary>
    public static float BottomOffset(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0)
        {
            return 0f;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return go.transform.position.y - bounds.min.y;
    }

    /// <summary>Sets the object on the ground below it (undoable), sunk by <paramref name="sink"/> metres so it does not
    /// hover on bumps, and tilted with the slope when <paramref name="align"/>. False when there is no ground below.
    /// It rests by its mesh, not its box: the lowest points of its nearest LOD are each measured against the ground under
    /// them, and it comes down until most of its base touches. (Placing by the bounding box left it floating: a tilted
    /// box reaches below the mesh, and on a slope the ground under the pivot isn't the ground under its downhill side.)</summary>
    public static bool Drop(Transform target, bool align, float sink = 0.03f)
    {
        if (!TryGround(target.position, out Vector3 ground, out Vector3 normal, target))
        {
            return false;
        }

        Undo.RecordObject(target, "Drop to Ground");
        if (align)
        {
            float yaw = target.eulerAngles.y;
            target.rotation = Quaternion.FromToRotation(Vector3.up, FootprintNormal(target, normal)) * Quaternion.Euler(0f, yaw, 0f);
        }

        // Near the ground by its box first, then down onto it by its mesh.
        float offset = BottomOffset(target.gameObject);
        target.position = new Vector3(target.position.x, ground.y + offset, target.position.z);
        if (TryRestingGap(target, out float gap))
        {
            target.position -= Vector3.up * gap;
        }

        target.position -= Vector3.up * sink;
        return true;
    }

    // Share of the base's lowest points left above the ground: a few may bridge small dips, the rest rest on or just in it.
    private const float HoveringShare = 0.15f;
    private const int MostSamples = 64;

    /// <summary>How far the object must come down for most of its base to touch the ground below each point of it.</summary>
    public static bool TryRestingGap(Transform target, out float gap)
    {
        gap = 0f;
        List<Vector3> points = LowestPoints(target);
        if (points.Count == 0)
        {
            return false;
        }

        var gaps = new List<float>(points.Count);
        foreach (Vector3 point in points)
        {
            if (TryGround(point, out Vector3 ground, out _, target, 4f))
            {
                gaps.Add(point.y - ground.y);
            }
        }

        if (gaps.Count == 0)
        {
            return false;
        }

        gaps.Sort();
        gap = gaps[Mathf.Clamp(Mathf.FloorToInt(gaps.Count * (1f - HoveringShare)), 0, gaps.Count - 1)];
        return true;
    }

    // The lowest band of the object's visible mesh (its nearest LOD only), in world space, thinned to a few dozen points.
    private static List<Vector3> LowestPoints(Transform target)
    {
        var points = new List<Vector3>();
        foreach (MeshFilter filter in VisibleMeshes(target))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices)
            {
                points.Add(toWorld.MultiplyPoint3x4(vertex));
            }
        }

        if (points.Count == 0)
        {
            return points;
        }

        float bottom = points.Min(p => p.y);
        float top = points.Max(p => p.y);
        float band = bottom + Mathf.Max(0.05f, (top - bottom) * 0.12f);
        List<Vector3> low = points.Where(p => p.y <= band).ToList();
        if (low.Count <= MostSamples)
        {
            return low;
        }

        var thinned = new List<Vector3>(MostSamples);
        float step = low.Count / (float)MostSamples;
        for (int i = 0; i < MostSamples; i++)
        {
            thinned.Add(low[Mathf.FloorToInt(i * step)]);
        }

        return thinned;
    }

    private static IEnumerable<MeshFilter> VisibleMeshes(Transform target)
    {
        LODGroup lods = target.GetComponentInChildren<LODGroup>();
        if (lods != null && lods.lodCount > 0)
        {
            LOD nearest = lods.GetLODs()[0];
            return nearest.renderers.Where(r => r != null && r.enabled).Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null);
        }

        return target.GetComponentsInChildren<MeshFilter>().Where(f => f.TryGetComponent(out Renderer r) && r.enabled);
    }

    // The slope under the whole footprint (its middle and four corners), not only under the pivot.
    private static Vector3 FootprintNormal(Transform target, Vector3 fallback)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0)
        {
            return fallback;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        Vector3 c = bounds.center, e = bounds.extents * 0.8f;
        Vector3[] corners = { c, c + new Vector3(e.x, 0f, e.z), c + new Vector3(-e.x, 0f, e.z), c + new Vector3(e.x, 0f, -e.z), c + new Vector3(-e.x, 0f, -e.z) };
        var heights = new List<Vector3>();
        foreach (Vector3 corner in corners)
        {
            if (TryGround(corner, out Vector3 ground, out _, target))
            {
                heights.Add(ground);
            }
        }

        if (heights.Count < 5)
        {
            return fallback;
        }

        // The plane through the four corners: the cross of its diagonals.
        Vector3 a = heights[1] - heights[4], b = heights[2] - heights[3];
        Vector3 normal = Vector3.Cross(b, a).normalized;
        if (normal.y < 0f) normal = -normal;
        return normal.y > 0.2f ? normal : fallback;
    }
}
