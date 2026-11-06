using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Generates the low-poly forest kit the raid maps scatter: spruces, a pine, a dead tree, boulders and a log.
/// Meshes, materials and prefabs are written as assets so they can be swapped for real art later without touching the maps.
/// Each prefab has its renderer and a capsule collider on the root, as terrain trees require.
/// </summary>
public static class ForestKitBuilder
{
    public const string Root = "Assets/_Project/Art/Forest";
    private const string BoulderSource = "Assets/_Project/Art/Moss/Ground/Mossy Granite.mat";

    [MenuItem("Safe Haven/Setup/Raids/Build Forest Kit", false, 200)]
    private static void BuildFromMenu() => Debug.Log(Build());

    public static string Build()
    {
        Ensure(Root);
        Ensure(Root + "/Meshes");
        Ensure(Root + "/Materials");
        Ensure(Root + "/Prefabs");

        Material bark = LitMaterial("Bark", new Color(0.24f, 0.17f, 0.12f));
        Material needles = LitMaterial("Needles", new Color(0.13f, 0.24f, 0.15f));
        Material pineNeedles = LitMaterial("Pine Needles", new Color(0.19f, 0.28f, 0.14f));
        Material deadWood = LitMaterial("Dead Wood", new Color(0.36f, 0.33f, 0.29f));
        Material stone = BoulderMaterial();

        var made = new List<string>
        {
            Save("Spruce A", Spruce(9f, 4, 2.6f, 11), new[] { bark, needles }, 0.35f, 9f),
            Save("Spruce B", Spruce(7.5f, 3, 2.3f, 23), new[] { bark, needles }, 0.3f, 7.5f),
            Save("Spruce C", Spruce(11f, 5, 2.9f, 37), new[] { bark, needles }, 0.4f, 11f),
            Save("Pine", Pine(10f, 51), new[] { bark, pineNeedles }, 0.3f, 10f),
            Save("Dead Spruce", DeadTree(8f, 67), new[] { deadWood }, 0.3f, 8f),
            Save("Boulder A", Boulder(new Vector3(1.7f, 1.1f, 1.4f), 5), new[] { stone }, 1.2f, 2.2f),
            Save("Boulder B", Boulder(new Vector3(1.2f, 1.5f, 1.1f), 9), new[] { stone }, 1f, 3f),
            Save("Fallen Log", Log(5f, 0.35f), new[] { deadWood }, 0.35f, 5f, horizontal: true),
        };

        AssetDatabase.SaveAssets();
        return "Forest kit built: " + string.Join(", ", made);
    }

    public static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/{name}.prefab");

    private static Mesh Spruce(float height, int tiers, float radius, int seed)
    {
        var random = new System.Random(seed);
        var builder = new MeshBuilder(2);
        builder.Frustum(Vector3.zero, Vector3.up * height * 0.35f, 0.28f, 0.2f, 6, 0);
        for (int i = 0; i < tiers; i++)
        {
            float t = i / (float)tiers;
            float baseY = height * Mathf.Lerp(0.18f, 0.72f, t);
            float r = radius * Mathf.Lerp(1f, 0.38f, t) * Jitter(random, 0.1f);
            float tipY = i == tiers - 1 ? height : baseY + height * 0.34f;
            builder.Cone(new Vector3(0f, baseY, 0f), new Vector3(0f, tipY, 0f), r, 8, 1, random, 0.18f);
        }
        return builder.Build("Spruce");
    }

    private static Mesh Pine(float height, int seed)
    {
        var random = new System.Random(seed);
        var builder = new MeshBuilder(2);
        builder.Frustum(Vector3.zero, Vector3.up * height * 0.78f, 0.26f, 0.16f, 6, 0);
        builder.Cone(new Vector3(0f, height * 0.55f, 0f), new Vector3(0f, height * 0.82f, 0f), 2.1f, 7, 1, random, 0.25f);
        builder.Cone(new Vector3(0f, height * 0.72f, 0f), new Vector3(0f, height, 0f), 1.6f, 7, 1, random, 0.25f);
        return builder.Build("Pine");
    }

    private static Mesh DeadTree(float height, int seed)
    {
        var random = new System.Random(seed);
        var builder = new MeshBuilder(1);
        builder.Frustum(Vector3.zero, Vector3.up * height, 0.3f, 0.04f, 6, 0);
        for (int i = 0; i < 6; i++)
        {
            float y = height * Mathf.Lerp(0.35f, 0.85f, i / 5f);
            float angle = i * 2.4f + (float)random.NextDouble();
            var direction = new Vector3(Mathf.Cos(angle), 0.35f, Mathf.Sin(angle)).normalized;
            float length = Mathf.Lerp(1.6f, 0.6f, i / 5f);
            builder.Cone(new Vector3(0f, y, 0f), new Vector3(0f, y, 0f) + direction * length, 0.07f, 4, 0, random, 0f);
        }
        return builder.Build("Dead Spruce");
    }

    private static Mesh Boulder(Vector3 scale, int seed)
    {
        var random = new System.Random(seed);
        var builder = new MeshBuilder(1);
        // An icosahedron, subdivided once and pushed about by noise, sunk a little so it sits in the ground.
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var points = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var midpoints = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpoints.TryGetValue(key, out int index)) return index;
            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            midpoints[key] = points.Count - 1;
            return points.Count - 1;
        }

        var triangles = new List<int>();
        for (int i = 0; i < faces.Length; i += 3)
        {
            int a = faces[i], b = faces[i + 1], c = faces[i + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i].normalized * Jitter(random, 0.16f);
            p = Vector3.Scale(p, scale);
            p.y = Mathf.Max(p.y, -scale.y * 0.5f) + scale.y * 0.3f;
            points[i] = p;
        }

        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            builder.Triangle(a, b, c, (a + b + c) / 3f - Vector3.up * scale.y * 0.3f, 0);
        }
        return builder.Build("Boulder");
    }

    private static Mesh Log(float length, float radius)
    {
        var builder = new MeshBuilder(1);
        var start = new Vector3(-length / 2f, radius * 0.8f, 0f);
        var end = new Vector3(length / 2f, radius * 0.7f, 0f);
        builder.Frustum(start, end, radius, radius * 0.85f, 7, 0, capBase: true);
        return builder.Build("Fallen Log");
    }

    private static string Save(string name, Mesh mesh, Material[] materials, float colliderRadius, float colliderHeight, bool horizontal = false)
    {
        string meshPath = $"{Root}/Meshes/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        var go = new GameObject(name);
        try
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = colliderRadius;
            capsule.height = colliderHeight;
            capsule.direction = horizontal ? 0 : 1;
            capsule.center = horizontal ? new Vector3(0f, colliderRadius, 0f) : new Vector3(0f, colliderHeight / 2f, 0f);
            // Terrain trees are only cut out of a baked navmesh when their prototype has an obstacle.
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = colliderRadius;
            obstacle.height = colliderHeight;
            obstacle.center = capsule.center;
            if (horizontal)
            {
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(colliderHeight, colliderRadius * 2f, colliderRadius * 2f);
            }
            PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
        return name;
    }

    private static Material LitMaterial(string name, Color color)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.08f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BoulderMaterial()
    {
        string path = $"{Root}/Materials/Forest Boulder.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        var source = AssetDatabase.LoadAssetAtPath<Material>(BoulderSource);
        material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static float Jitter(System.Random random, float amount) => 1f + ((float)random.NextDouble() * 2f - 1f) * amount;

    private static void Ensure(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }

    /// <summary>Flat-shaded mesh assembly: every triangle gets its own vertices, which gives the faceted low-poly look.</summary>
    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector3> normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int>[] submeshes;

        public MeshBuilder(int submeshCount)
        {
            submeshes = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) submeshes[i] = new List<int>();
        }

        /// <summary>Adds a triangle facing away from <paramref name="inside"/>.</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, int submesh)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            foreach (Vector3 p in new[] { a, b, c })
            {
                submeshes[submesh].Add(vertices.Count);
                vertices.Add(p);
                normals.Add(normal);
                uvs.Add(new Vector2((p.x + p.z) * 0.5f, p.y * 0.5f));
            }
        }

        public void Cone(Vector3 baseCentre, Vector3 tip, float radius, int sides, int submesh, System.Random random, float ragged)
        {
            Ring(baseCentre, tip - baseCentre, radius, sides, random, ragged, out Vector3[] ring);
            Vector3 inside = Vector3.Lerp(baseCentre, tip, 0.3f);
            for (int i = 0; i < sides; i++)
            {
                Triangle(ring[i], ring[(i + 1) % sides], tip, inside, submesh);
                Triangle(ring[i], ring[(i + 1) % sides], baseCentre, inside + (tip - baseCentre).normalized * 0.01f, submesh);
            }
        }

        public void Frustum(Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, int submesh, bool capBase = false)
        {
            Ring(from, to - from, fromRadius, sides, null, 0f, out Vector3[] bottom);
            Ring(to, to - from, toRadius, sides, null, 0f, out Vector3[] top);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 axisPoint = Vector3.Project(bottom[i] - from, to - from) + from;
                Triangle(bottom[i], bottom[j], top[i], axisPoint, submesh);
                Triangle(bottom[j], top[j], top[i], axisPoint + (to - from) * 0.5f, submesh);
                Triangle(top[i], top[j], to, from, submesh);
                if (capBase) Triangle(bottom[i], bottom[j], from, to, submesh);
            }
        }

        private static void Ring(Vector3 centre, Vector3 axis, float radius, int sides, System.Random random, float ragged, out Vector3[] ring)
        {
            axis.Normalize();
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(axis, side);
            ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float r = random != null ? radius * Jitter(random, ragged) : radius;
                ring[i] = centre + (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * r;
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, subMeshCount = submeshes.Length };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
