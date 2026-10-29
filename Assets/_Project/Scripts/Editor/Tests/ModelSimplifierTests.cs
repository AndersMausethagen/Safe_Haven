using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ModelSimplifierTests
{
    // A finely divided, gently waved sheet: enough triangles for the LOD generator to remove some.
    private static Mesh Sheet(int cells)
    {
        var vertices = new Vector3[(cells + 1) * (cells + 1)];
        var wind = new Vector2[vertices.Length];
        for (int z = 0, i = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++, i++)
            {
                vertices[i] = new Vector3(x, Mathf.Sin(x * 0.3f) * Mathf.Cos(z * 0.3f), z);
                wind[i] = new Vector2(x, z);
            }

        var triangles = new int[cells * cells * 6];
        for (int z = 0, t = 0; z < cells; z++)
            for (int x = 0; x < cells; x++, t += 6)
            {
                int a = z * (cells + 1) + x, b = a + cells + 1;
                triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1; triangles[t + 4] = b; triangles[t + 5] = b + 1;
            }

        var mesh = new Mesh { name = "Sheet", indexFormat = IndexFormat.UInt32, vertices = vertices, triangles = triangles };
        mesh.SetUVs(2, wind);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    [Test]
    public void Bake_KeepsVerticesAndDropsTriangles()
    {
        Mesh source = Sheet(60);
        Mesh levels = ModelSimplifier.GenerateLevels(source);
        Mesh baked = ModelSimplifier.Bake(source, levels, 2);
        try
        {
            Assert.That(levels.lodCount, Is.GreaterThan(2));
            Assert.That(baked.GetIndexCount(0), Is.EqualTo(levels.GetLods(0)[2].indexCount));
            Assert.That(baked.GetIndexCount(0), Is.LessThan(source.GetIndexCount(0)));
            Assert.That(baked.vertexCount, Is.EqualTo(source.vertexCount));
            Assert.That(baked.HasVertexAttribute(VertexAttribute.TexCoord2), "wind weights are kept");
            Assert.That(baked.lodCount, Is.EqualTo(1), "one lighter mesh, not an LOD chain");
            foreach (int index in baked.triangles) Assert.That(index, Is.InRange(0, source.vertexCount - 1));
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(levels);
            Object.DestroyImmediate(baked);
        }
    }

    [Test]
    public void Bake_LevelZeroIsTheSource()
    {
        Mesh source = Sheet(20);
        Mesh levels = ModelSimplifier.GenerateLevels(source);
        Mesh baked = ModelSimplifier.Bake(source, levels, 0);
        try
        {
            CollectionAssert.AreEqual(source.triangles, baked.triangles);
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(levels);
            Object.DestroyImmediate(baked);
        }
    }
}
