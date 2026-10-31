using UnityEngine;

/// <summary>
/// Builds the meshes of cut timber: a tapered length of trunk standing along +Y with a cut face on each end. The bark is
/// submesh 0 and the faces submesh 1. The surface wobbles a little by height, so sections cut from one log still line up.
/// </summary>
public static class LogMesh
{
    private const int Sides = 12;
    private const float BarkTileMetres = 1.6f;

    /// <param name="bottom">Height along the whole log where this piece starts, so its wobble continues the piece before.</param>
    public static Mesh Build(float radiusBottom, float radiusTop, float length, float bottom = 0f, int seed = 0, int rings = 3)
    {
        rings = Mathf.Max(1, rings);
        int barkColumns = Sides + 1;
        int barkVertices = barkColumns * (rings + 1);
        int capVertices = Sides + 2;
        var vertices = new Vector3[barkVertices + capVertices * 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var bark = new int[Sides * rings * 6];
        var caps = new int[Sides * 3 * 2];

        float barkAround = Mathf.Max(1f, Mathf.Round(Mathf.PI * (radiusBottom + radiusTop) / BarkTileMetres));
        Vector3 lean = new Vector3(0f, (radiusBottom - radiusTop) / Mathf.Max(0.01f, length), 0f);
        int index = 0;
        for (int ring = 0; ring <= rings; ring++)
        {
            float along = ring / (float)rings;
            float y = length * along;
            float radius = Mathf.Lerp(radiusBottom, radiusTop, along);
            for (int side = 0; side < barkColumns; side++)
            {
                float angle = side % Sides * Mathf.PI * 2f / Sides;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[index] = outward * (radius * Wobble(angle, bottom + y, seed)) + Vector3.up * y;
                normals[index] = (outward + lean).normalized;
                uvs[index] = new Vector2(side / (float)Sides * barkAround, (bottom + y) / BarkTileMetres);
                index++;
            }
        }

        int triangle = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int side = 0; side < Sides; side++)
            {
                int a = ring * barkColumns + side;
                int b = a + barkColumns;
                bark[triangle++] = a;
                bark[triangle++] = b;
                bark[triangle++] = a + 1;
                bark[triangle++] = a + 1;
                bark[triangle++] = b;
                bark[triangle++] = b + 1;
            }
        }

        triangle = 0;
        Face(vertices, normals, uvs, caps, ref index, ref triangle, 0f, radiusBottom, bottom, seed, false);
        Face(vertices, normals, uvs, caps, ref index, ref triangle, length, radiusTop, bottom + length, seed, true);

        var mesh = new Mesh { name = "Log" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(bark, 0);
        mesh.SetTriangles(caps, 1);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // A fan with the rings of the texture's centre on the log's heart. Its rim follows the bark's wobble exactly.
    private static void Face(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles, ref int index, ref int triangle,
        float y, float radius, float height, int seed, bool up)
    {
        int centre = index;
        Vector3 normal = up ? Vector3.up : Vector3.down;
        vertices[index] = Vector3.up * y;
        normals[index] = normal;
        uvs[index++] = new Vector2(0.5f, 0.5f);
        for (int side = 0; side <= Sides; side++)
        {
            float angle = side % Sides * Mathf.PI * 2f / Sides;
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[index] = outward * (radius * Wobble(angle, height, seed)) + Vector3.up * y;
            normals[index] = normal;
            uvs[index++] = new Vector2(0.5f + outward.x * 0.5f, 0.5f + outward.z * 0.5f);
        }

        for (int side = 0; side < Sides; side++)
        {
            triangles[triangle++] = centre;
            triangles[triangle++] = up ? centre + side + 2 : centre + side + 1;
            triangles[triangle++] = up ? centre + side + 1 : centre + side + 2;
        }
    }

    private static float Wobble(float angle, float height, int seed)
    {
        float shift = seed * 1.7f;
        return 1f + 0.05f * Mathf.Sin(angle * 3f + height * 1.3f + shift) + 0.03f * Mathf.Sin(angle * 5f - height * 2.1f + shift * 0.6f);
    }
}
