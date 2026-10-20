using UnityEditor;
using UnityEngine;

/// <summary>
/// Collision for models exported by the Local Generator: a child mesh whose name ends in "_Collider" (a simplified
/// convex hull, at most 255 triangles) becomes an invisible convex MeshCollider. LOD meshes named _LOD0, _LOD1, ...
/// need nothing here, since Unity's model importer already builds a LOD Group from those names.
/// </summary>
public sealed class GeneratedModelPostprocessor : AssetPostprocessor
{
    private const string ColliderSuffix = "_Collider";

    private void OnPostprocessModel(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.name.EndsWith(ColliderSuffix, System.StringComparison.Ordinal))
            {
                continue;
            }

            GameObject hull = filter.gameObject;
            var collider = hull.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;
            Object.DestroyImmediate(hull.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(filter);
        }
    }
}
