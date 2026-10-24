using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Puts objects on the ground for the level design tools: finds the ground under a point (colliders, then
/// terrain height), sets an object down by the bottom of its meshes rather than its pivot, and optionally tilts it with
/// the slope. Menu: Safe Haven > Level > Drop Selection to Ground (End) / Drop and Align to Slope (Shift+End).</summary>
public static class GroundPlacement
{
    [MenuItem("Safe Haven/Level/Drop Selection to Ground _END")]
    private static void DropMenu() => DropSelection(false);

    [MenuItem("Safe Haven/Level/Drop and Align to Slope #_END")]
    private static void AlignMenu() => DropSelection(true);

    [MenuItem("Safe Haven/Level/Drop Selection to Ground _END", true)]
    [MenuItem("Safe Haven/Level/Drop and Align to Slope #_END", true)]
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
    /// hover on bumps, and tilted with the slope when <paramref name="align"/>. False when there is no ground below.</summary>
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
            target.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
        }

        float offset = BottomOffset(target.gameObject);
        target.position = new Vector3(target.position.x, ground.y + offset - sink, target.position.z);
        return true;
    }
}
