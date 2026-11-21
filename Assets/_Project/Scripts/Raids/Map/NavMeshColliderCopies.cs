using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Readable copies of the mesh colliders' meshes, for a raid's navmesh built as the map loads (RaidMap). The navmesh is built from
/// the physics colliders, and Unity's runtime builder can read a mesh only when its model is Read/Write: in the Editor it reads any
/// mesh (with a warning), in a build it leaves the others out, so the creatures' ground lost every rock, stump and ore node and the
/// bridges and boardwalks they walk on. Turning Read/Write on for those models would keep a second copy of every render mesh in
/// memory (ModelBudgetTests forbids it); these copies hold only the colliders (convex hulls of at most 255 triangles, a bridge's
/// deck), about 1.6 MB for all of them.
/// <para>Made by Safe Haven › Navigation › Gather Collider Copies (NavMeshColliderCopiesBuilder) from every prefab's mesh colliders;
/// EngineHazardTests fails when one is missing or out of date. Lives in Resources as NavMeshColliderCopies.</para>
/// </summary>
public sealed class NavMeshColliderCopies : ScriptableObject
{
    [Serializable]
    public sealed class Copy
    {
        [Tooltip("The collider mesh's name, vertex and index counts: what it is known by without reading it.")]
        public string key;
        public Vector3[] vertices;
        public int[] indices;
    }

    public const string ResourceName = "NavMeshColliderCopies";

    [SerializeField] private List<Copy> copies = new List<Copy>();

    private static NavMeshColliderCopies loaded;
    private static bool looked;
    // The readable meshes made from the copies, one per collider mesh, kept for the session.
    private static readonly Dictionary<Mesh, Mesh> Made = new Dictionary<Mesh, Mesh>();
    private Dictionary<string, Copy> byKey;

    public IReadOnlyList<Copy> Copies => copies;

    public static NavMeshColliderCopies Current
    {
        get
        {
            if (!looked)
            {
                looked = true;
                loaded = Resources.Load<NavMeshColliderCopies>(ResourceName);
            }

            return loaded;
        }
    }

    /// <summary>What a mesh is known by without reading it (its vertices can't be read in a build).</summary>
    public static string KeyOf(Mesh mesh)
    {
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            indices += mesh.GetIndexCount(i);
        }

        return $"{mesh.name}|{mesh.vertexCount}|{indices}";
    }

    public void Set(List<Copy> made)
    {
        copies = made;
        byKey = null;
    }

    public bool TryGet(Mesh mesh, out Copy copy)
    {
        if (byKey == null)
        {
            byKey = new Dictionary<string, Copy>();
            foreach (Copy one in copies)
            {
                byKey[one.key] = one;
            }
        }

        return byKey.TryGetValue(KeyOf(mesh), out copy);
    }

    /// <summary>A readable stand-in for a collider mesh that can't be read, made once from its copy; false when there is none.</summary>
    public bool TryReadable(Mesh mesh, out Mesh readable)
    {
        if (Made.TryGetValue(mesh, out readable) && readable != null)
        {
            return true;
        }

        if (!TryGet(mesh, out Copy copy))
        {
            readable = null;
            return false;
        }

        readable = new Mesh
        {
            name = mesh.name + " (navmesh)",
            indexFormat = copy.vertices.Length > ushort.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            vertices = copy.vertices,
            triangles = copy.indices,
            hideFlags = HideFlags.DontSave,
        };
        Made[mesh] = readable;
        return true;
    }

    /// <summary>
    /// The surface's navmesh brought up to date as NavMeshSurface.UpdateNavMesh does (its own collection of sources and bounds), with
    /// each collider mesh that can't be read swapped for its readable copy. Falls back to the surface's own update (and says so) when
    /// the package's collection can't be reached.
    /// </summary>
    public static AsyncOperation Update(NavMeshSurface surface)
    {
        if (!Reach(out MethodInfo collect, out MethodInfo bounds))
        {
            return surface.UpdateNavMesh(surface.navMeshData);
        }

        var sources = (List<NavMeshBuildSource>)collect.Invoke(surface, null);
        int swapped = Swap(sources, Current, out int missing);
        if (missing > 0)
        {
            Debug.LogWarning($"Raid navmesh: {missing} collider meshes can't be read in a build and have no copy (Safe Haven › Navigation › Gather Collider Copies).", surface);
        }

        Bounds area = surface.collectObjects == CollectObjects.Volume
            ? new Bounds(surface.center, Abs(surface.size))
            : (Bounds)bounds.Invoke(surface, new object[] { sources });
        // Deep water marked as its own area, kept out of by creatures that don't swim (DeepWater).
        DeepWater.AddSources(sources, area);
        if (swapped > 0)
        {
            Debug.Log($"Raid navmesh: {swapped} collider meshes read from their copies.", surface);
        }

        return NavMeshBuilder.UpdateNavMeshDataAsync(surface.navMeshData, surface.GetBuildSettings(), sources, area);
    }

    /// <summary>Swaps each unreadable mesh source for its readable copy; counts those swapped, and those left without one.</summary>
    public static int Swap(List<NavMeshBuildSource> sources, NavMeshColliderCopies copies, out int missing)
    {
        int swapped = 0;
        missing = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            NavMeshBuildSource source = sources[i];
            if (source.shape != NavMeshBuildSourceShape.Mesh || !(source.sourceObject is Mesh mesh) || mesh.isReadable)
            {
                continue;
            }

            if (copies != null && copies.TryReadable(mesh, out Mesh readable))
            {
                source.sourceObject = readable;
                sources[i] = source;
                swapped++;
            }
            else
            {
                missing++;
            }
        }

        return swapped;
    }

    // NavMeshSurface's own source collection and bounds (internal to the package): what UpdateNavMesh itself runs.
    public static bool Reach(out MethodInfo collect, out MethodInfo bounds)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        collect = typeof(NavMeshSurface).GetMethod("CollectSources", flags, null, Type.EmptyTypes, null);
        bounds = typeof(NavMeshSurface).GetMethod("CalculateWorldBounds", flags, null, new[] { typeof(List<NavMeshBuildSource>) }, null);
        if (collect == null || bounds == null || collect.ReturnType != typeof(List<NavMeshBuildSource>) || bounds.ReturnType != typeof(Bounds))
        {
            if (!warnedUnreachable)
            {
                warnedUnreachable = true;
                Debug.LogWarning("Raid navmesh: NavMeshSurface's source collection has changed (AI Navigation package); built without the collider copies.");
            }

            return false;
        }

        return true;
    }

    private static bool warnedUnreachable;

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // Entering Play doesn't reload scripts in this project: the asset is looked up again, and the meshes made from it go.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        loaded = null;
        looked = false;
        warnedUnreachable = false;
        foreach (Mesh made in Made.Values)
        {
            if (made != null)
            {
                DestroyImmediate(made); // made in code, not an asset: safe in or out of Play
            }
        }

        Made.Clear();
    }
}
