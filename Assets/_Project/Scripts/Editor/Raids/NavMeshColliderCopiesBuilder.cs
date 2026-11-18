using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gathers NavMeshColliderCopies: a readable copy of every prefab's mesh collider mesh whose model isn't Read/Write, for the raids'
/// navmesh in a build (see NavMeshColliderCopies). Run again after importing or changing a model with a collider;
/// EngineHazardTests.EveryMeshCollidersMesh_CanBeReadByTheRaidsNavMesh says when.
/// </summary>
public static class NavMeshColliderCopiesBuilder
{
    public const string AssetPath = "Assets/_Project/Resources/" + NavMeshColliderCopies.ResourceName + ".asset";

    [MenuItem("Safe Haven/Navigation/Gather Collider Copies")]
    public static void GatherMenu()
    {
        int count = Gather();
        Debug.Log($"Navmesh collider copies: {count} meshes in {AssetPath}.");
    }

    /// <summary>The meshes the raids' navmesh would read from mesh colliders and can't in a build, each once.</summary>
    public static List<Mesh> Unreadable()
    {
        var meshes = new Dictionary<string, Mesh>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null)
            {
                continue;
            }

            foreach (MeshCollider collider in prefab.GetComponentsInChildren<MeshCollider>(true))
            {
                Mesh mesh = collider.sharedMesh;
                if (!collider.isTrigger && mesh != null && !mesh.isReadable)
                {
                    meshes[NavMeshColliderCopies.KeyOf(mesh)] = mesh;
                }
            }
        }

        return meshes.OrderBy(pair => pair.Key, System.StringComparer.Ordinal).Select(pair => pair.Value).ToList();
    }

    public static NavMeshColliderCopies.Copy CopyOf(Mesh mesh) => new NavMeshColliderCopies.Copy
    {
        key = NavMeshColliderCopies.KeyOf(mesh),
        vertices = mesh.vertices,
        indices = Enumerable.Range(0, mesh.subMeshCount).SelectMany(sub => mesh.GetTriangles(sub)).ToArray(),
    };

    public static int Gather()
    {
        var copies = AssetDatabase.LoadAssetAtPath<NavMeshColliderCopies>(AssetPath);
        if (copies == null)
        {
            copies = ScriptableObject.CreateInstance<NavMeshColliderCopies>();
            AssetDatabase.CreateAsset(copies, AssetPath);
        }

        List<NavMeshColliderCopies.Copy> made = Unreadable().Select(CopyOf).ToList();
        copies.Set(made);
        EditorUtility.SetDirty(copies);
        AssetDatabase.SaveAssetIfDirty(copies);
        return made.Count;
    }
}
