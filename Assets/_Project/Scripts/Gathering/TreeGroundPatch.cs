using UnityEngine;

/// <summary>
/// A temporary copy of the ground where a tree is about to land. A TerrainCollider holds painted trees and the ground as
/// one collider, and ignoring it does not reach the trees, so a falling trunk would hang in its neighbours. Instead the
/// trunk collides with this patch alone, on a layer of its own: same heights, nothing standing on it. It is removed once
/// the tree lies still.
/// </summary>
public static class TreeGroundPatch
{
    private const float CellSize = 1f;
    private const string Layer = "TransparentFX";

    /// <summary>Lays a patch from <paramref name="from"/> along the direction. Returns null where there is no terrain.</summary>
    public static GameObject Build(Vector3 from, Vector3 direction, float length, float width)
    {
        if (Terrain.activeTerrains.Length == 0)
        {
            return null;
        }

        int rows = Mathf.CeilToInt((length + CellSize * 3f) / CellSize) + 1;
        int columns = Mathf.CeilToInt(width / CellSize) + 1;
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 origin = from - direction * (CellSize * 3f) - side * ((columns - 1) * CellSize * 0.5f);
        var vertices = new Vector3[rows * columns];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector3 point = origin + direction * (row * CellSize) + side * (column * CellSize);
                point.y = GroundHeight(point, from.y);
                vertices[row * columns + column] = point;
            }
        }

        // Side is to the right of the direction, so going row then column winds each triangle to face up.
        var triangles = new int[(rows - 1) * (columns - 1) * 6];
        int index = 0;
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                int a = row * columns + column;
                int b = a + columns;
                triangles[index++] = a;
                triangles[index++] = b;
                triangles[index++] = a + 1;
                triangles[index++] = a + 1;
                triangles[index++] = b;
                triangles[index++] = b + 1;
            }
        }

        var mesh = new Mesh { name = "Fall Ground", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var patch = new GameObject("Fall Ground") { layer = LayerMask.NameToLayer(Layer) };
        patch.AddComponent<MeshCollider>().sharedMesh = mesh;
        return patch;
    }

    public static void Remove(GameObject patch)
    {
        if (patch.TryGetComponent(out MeshCollider collider))
        {
            Object.Destroy(collider.sharedMesh);
        }

        Object.Destroy(patch);
    }

    private static float GroundHeight(Vector3 point, float fallback)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 corner = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            if (point.x >= corner.x && point.x <= corner.x + size.x && point.z >= corner.z && point.z <= corner.z + size.z)
            {
                return terrain.SampleHeight(point) + corner.y;
            }
        }

        return fallback;
    }
}
