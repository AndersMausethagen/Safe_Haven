using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drops the selected objects with physics so they settle the way things fall: tumbling onto the slope, leaning on rocks
/// and on each other. Each gets a temporary Rigidbody (and a convex collider when it has none), the scene's physics is
/// stepped in the editor until everything is at rest, and the temporary parts are removed again, leaving plain placed
/// objects. Other rigidbodies in the scene are held still meanwhile. One undo step. Menu: Safe Haven › World › Place ›
/// Drop with Physics (Ctrl+End), and the World Tools overlay.
/// </summary>
public static class PhysicsDrop
{
    private const float MostSeconds = 8f;
    private const float RestSpeed = 0.03f;
    private const float RestSpin = 2f;          // degrees per second
    private const float RestSeconds = 0.4f;     // at rest this long counts as settled
    private const float LiftWhenBuried = 0.5f;  // metres: an object starting inside the ground is lifted out first
    private const float MostSeat = 0.5f;        // metres it may be lowered onto the ground after settling

    [MenuItem("Safe Haven/World/Place/Drop with Physics %_END")]
    private static void DropMenu() => DropSelection(false);

    [MenuItem("Safe Haven/World/Place/Drop with Physics (Upright) %#_END")]
    private static void DropUprightMenu() => DropSelection(true);

    [MenuItem("Safe Haven/World/Place/Drop with Physics %_END", true)]
    [MenuItem("Safe Haven/World/Place/Drop with Physics (Upright) %#_END", true)]
    private static bool HasSelection() => Selection.transforms.Length > 0 && !EditorApplication.isPlaying;

    private static void DropSelection(bool upright)
    {
        List<Transform> targets = Expand(Selection.transforms.Where(t => !Selection.transforms.Any(other => other != t && t.IsChildOf(other))));
        int settled = Drop(targets, upright);
        Debug.Log($"Level: {settled} of {targets.Count} object(s) settled with physics{(upright ? ", kept upright" : "")}.");
    }

    /// <summary>A selected folder (no mesh of its own, such as a scatter folder) stands for the objects in it.</summary>
    public static List<Transform> Expand(IEnumerable<Transform> selected)
    {
        var targets = new List<Transform>();
        foreach (Transform t in selected)
        {
            bool folder = t.GetComponent<Renderer>() == null && t.GetComponent<LODGroup>() == null && t.childCount > 0
                          && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject);
            if (folder) foreach (Transform child in t) targets.Add(child);
            else targets.Add(t);
        }

        return targets;
    }

    /// <summary>Drops <paramref name="targets"/> with physics until they rest. Returns how many settled in time.</summary>
    /// <param name="upright">Never tips over (trees, posts): it falls and slides but keeps its tilt.</param>
    public static int Drop(IReadOnlyList<Transform> targets, bool upright = false)
    {
        if (targets.Count == 0 || EditorApplication.isPlaying)
        {
            return 0;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Drop with Physics");
        int group = Undo.GetCurrentGroup();
        Undo.RecordObjects(targets.Cast<Object>().ToArray(), "Drop with Physics");

        var added = new List<Component>();
        var bodies = new List<Rigidbody>();
        var held = new List<Rigidbody>();
        var switchedOff = new List<MeshCollider>();
        SimulationMode mode = Physics.simulationMode;
        try
        {
            // Everything else stays where it is.
            foreach (Rigidbody other in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
            {
                if (!other.isKinematic && !targets.Any(t => other.transform.IsChildOf(t)))
                {
                    other.isKinematic = true;
                    held.Add(other);
                }
            }

            foreach (Transform target in targets)
            {
                Rigidbody body = Prepare(target, added, switchedOff);
                if (upright) body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                bodies.Add(body);
            }

            Physics.simulationMode = SimulationMode.Script;
            Physics.SyncTransforms();
            float step = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            var still = new float[bodies.Count];
            int settled = 0;
            for (float time = 0f; time < MostSeconds && settled < bodies.Count; time += step)
            {
                Physics.Simulate(step);
                settled = 0;
                for (int i = 0; i < bodies.Count; i++)
                {
                    Rigidbody body = bodies[i];
                    bool resting = body.IsSleeping() || (body.linearVelocity.magnitude < RestSpeed && body.angularVelocity.magnitude * Mathf.Rad2Deg < RestSpin);
                    still[i] = resting ? still[i] + step : 0f;
                    if (still[i] >= RestSeconds) settled++;
                }
            }

            return settled;
        }
        finally
        {
            Physics.simulationMode = mode;
            // Settled on its convex hull, which bridges the mesh's hollows: down onto the ground by the mesh itself.
            foreach (Transform target in targets)
            {
                if (target != null && GroundPlacement.TryRestingGap(target, out float gap) && gap > 0f)
                {
                    target.position -= Vector3.up * Mathf.Min(gap, MostSeat);
                }
            }

            foreach (Component part in added)
            {
                if (part != null) Object.DestroyImmediate(part);
            }

            foreach (MeshCollider concave in switchedOff)
            {
                if (concave != null) concave.enabled = true;
            }

            foreach (Rigidbody other in held)
            {
                if (other != null) other.isKinematic = false;
            }

            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(group);
        }
    }

    // A temporary body on the object, with a convex collider fitted to its nearest LOD when it has no solid collider.
    private static Rigidbody Prepare(Transform target, List<Component> added, List<MeshCollider> switchedOff)
    {
        if (GroundPlacement.TryGround(target.position, out Vector3 ground, out _, target) && target.position.y < ground.y + 0.05f)
        {
            target.position = new Vector3(target.position.x, ground.y + GroundPlacement.BottomOffset(target.gameObject) + LiftWhenBuried, target.position.z);
        }

        bool solid = target.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger && !(c is MeshCollider mesh && !mesh.convex));
        if (!solid)
        {
            foreach (MeshFilter filter in NearestMeshes(target))
            {
                var hull = filter.gameObject.AddComponent<MeshCollider>();
                hull.sharedMesh = filter.sharedMesh;
                hull.convex = true;
                added.Add(hull);
            }
        }

        // Concave mesh colliders can't be on a moving body: switch them off while it falls.
        foreach (MeshCollider concave in target.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled && !c.convex))
        {
            concave.enabled = false;
            switchedOff.Add(concave);
        }

        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = target.gameObject.AddComponent<Rigidbody>();
            added.Add(body);
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.mass = Mathf.Max(1f, Volume(target) * 600f);
        body.linearDamping = 0.05f;
        // Rocks on a slope would otherwise roll on and on; this is roughly the ground's own grip on them.
        body.angularDamping = 2f;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return body;
    }

    private static IEnumerable<MeshFilter> NearestMeshes(Transform target)
    {
        LODGroup lods = target.GetComponentInChildren<LODGroup>();
        IEnumerable<Renderer> renderers = lods != null && lods.lodCount > 0
            ? lods.GetLODs()[0].renderers.Where(r => r != null)
            : target.GetComponentsInChildren<Renderer>();
        return renderers.Where(r => r.enabled).Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null);
    }

    private static float Volume(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1f;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds.size.x * bounds.size.y * bounds.size.z * 0.5f;
    }
}
