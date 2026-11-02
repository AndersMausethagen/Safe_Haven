using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Add to a Unity Terrain and choose a painted Terrain Layer. Builds a moss skin and short fronds without replacing
/// the terrain material. Generated render objects are disposable, non-colliding and never part of world saves.
/// Rebuild after painting to redistribute fronds; the surface mask follows painting immediately.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Terrain))]
public sealed class TerrainMoss : MonoBehaviour
{
    [Header("Paint mask")]
    [SerializeField] private TerrainLayer mossLayer;
    [Tooltip("Assign Safe Haven/Terrain Moss. Kept as a serialized reference so builds retain the shader.")]
    [SerializeField] private Shader mossShader;
    [SerializeField, Range(0.01f, 0.99f)] private float coverageThreshold = 0.15f;
    [SerializeField, Range(0.01f, 0.5f)] private float edgeSoftness = 0.2f;

    [Header("Forest palette")]
    [SerializeField] private Color shadowColor = new Color(0.065f, 0.12f, 0.045f);
    [SerializeField] private Color mossColor = new Color(0.22f, 0.34f, 0.09f);
    [SerializeField] private Color tipColor = new Color(0.43f, 0.49f, 0.19f);
    [SerializeField, Min(0.05f)] private float patternScale = 3f;
    [SerializeField, Range(0f, 1f)] private float smoothness = 0.15f;

    [Header("Optional PBR surface")]
    [SerializeField] private Texture2D baseColorMap;
    [SerializeField] private Texture2D normalMap;
    [Tooltip("R: ambient occlusion, G: roughness, B: height. Imported as linear data.")]
    [SerializeField] private Texture2D packedMap;
    [SerializeField, Min(0.1f)] private float textureSize = 1.5f;
    [SerializeField, Range(0f, 2f)] private float normalStrength = 1f;
    [SerializeField, Range(0f, 1f)] private float textureInfluence = 1f;

    [Header("Surface")]
    [Tooltip("Grid subdivisions across the whole terrain. Increase for sharply changing ground.")]
    [SerializeField, Range(8, 256)] private int surfaceResolution = 128;
    [SerializeField, Range(0.001f, 0.1f)] private float surfaceOffset = 0.025f;

    [Header("Sparse tufts")]
    [SerializeField, Range(0f, 4f)] private float tuftsPerSquareMetre = 0.8f;
    [SerializeField, Range(0, 12000)] private int maximumTufts = 4000;
    [SerializeField] private Vector2 tuftHeight = new Vector2(0.1f, 0.22f);
    [SerializeField, Range(0.03f, 0.4f)] private float tuftWidth = 0.14f;
    [SerializeField, Range(0f, 89f)] private float maximumSlope = 55f;
    [SerializeField, Min(1f)] private float drawDistance = 65f;
    [SerializeField, Range(0f, 2f)] private float windResponse = 1f;
    [SerializeField] private int seed = 731;
    [Tooltip("Feathered leafy sprigs instead of the original crossed ribbons. More vertices per tuft.")]
    [SerializeField] private bool leafyTufts;

    private Terrain terrain;
    private GameObject generated;
    private Mesh surfaceMesh;
    private Mesh tuftMesh;
    private Material surfaceMaterial;
    private Material tuftMaterial;
    [NonSerialized] private bool rebuildRequested;

    public TerrainLayer MossLayer
    {
        get => mossLayer;
        set => mossLayer = value;
    }

    public int TuftCount { get; private set; }
    public string LastError { get; private set; }

    public void Configure(TerrainLayer layer, Shader shader)
    {
        mossLayer = layer;
        mossShader = shader;
        Rebuild();
    }

    private void OnEnable()
    {
        RequestRebuild();
    }

    private void OnValidate()
    {
        // OnValidate can run on Unity's loading thread; allocate/destroy render objects on Update instead.
        RequestRebuild();
    }

    private void RequestRebuild()
    {
        rebuildRequested = true;
#if UNITY_EDITOR
        // ExecuteAlways.Update is not guaranteed after opening an unchanged scene.
        UnityEditor.EditorApplication.update -= RebuildInEditor;
        UnityEditor.EditorApplication.update += RebuildInEditor;
#endif
    }

#if UNITY_EDITOR
    private void RebuildInEditor()
    {
        UnityEditor.EditorApplication.update -= RebuildInEditor;
        if (this != null && isActiveAndEnabled && rebuildRequested) Rebuild();
    }
#endif

    private void Update()
    {
        if (rebuildRequested)
        {
            rebuildRequested = false;
            Rebuild();
        }
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= RebuildInEditor;
#endif
        ClearGenerated();
    }

    public bool Rebuild()
    {
        rebuildRequested = false;
        ClearGenerated();
        terrain = GetComponent<Terrain>();
        if (!ValidateSetup(out string error))
        {
            LastError = error;
            Debug.LogWarning(error, this);
            return false;
        }

        LastError = null;
        TerrainData data = terrain.terrainData;
        int layerIndex = Array.IndexOf(data.terrainLayers, mossLayer);
        surfaceMaterial = CreateMaterial(data, layerIndex, false);
        tuftMaterial = CreateMaterial(data, layerIndex, true);
        generated = new GameObject("Moss (generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generated.transform.SetParent(transform, false);
        surfaceMesh = BuildSurface(data);
        CreateRenderer("Moss surface", surfaceMesh, surfaceMaterial);
        tuftMesh = BuildTufts(data, layerIndex);
        CreateRenderer("Moss tufts", tuftMesh, tuftMaterial);
        return true;
    }

    private bool ValidateSetup(out string error)
    {
        error = null;
        if (terrain == null || terrain.terrainData == null)
            error = "Terrain Moss needs a Terrain with Terrain Data.";
        else if (mossLayer == null || Array.IndexOf(terrain.terrainData.terrainLayers, mossLayer) < 0)
            error = "Terrain Moss: choose a Moss Layer that is present in this terrain's Paint Texture layers.";
        else if (mossShader == null || !mossShader.isSupported)
            error = "Terrain Moss: assign the supported Safe Haven/Terrain Moss shader.";
        else if (transform.lossyScale != Vector3.one || Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f)
            error = "Terrain Moss requires an unrotated terrain with scale (1, 1, 1).";
        return error == null;
    }

    private Material CreateMaterial(TerrainData data, int layerIndex, bool tufts)
    {
        var material = new Material(mossShader) { hideFlags = HideFlags.HideAndDontSave };
        material.SetTexture("_Control", data.alphamapTextures[layerIndex / 4]);
        Vector4 channel = Vector4.zero;
        channel[layerIndex % 4] = 1f;
        material.SetVector("_MaskChannel", channel);
        material.SetTexture("_Holes", data.holesTexture);
        material.SetColor("_ShadowColor", shadowColor);
        material.SetColor("_MossColor", mossColor);
        material.SetColor("_TipColor", tipColor);
        material.SetFloat("_Threshold", coverageThreshold);
        material.SetFloat("_Softness", edgeSoftness);
        material.SetFloat("_PatternScale", patternScale);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Tufts", tufts ? 1f : 0f);
        material.SetFloat("_DrawDistance", drawDistance);
        material.SetFloat("_WindResponse", windResponse);
        material.SetFloat("_UseTextures", baseColorMap != null ? textureInfluence : 0f);
        material.SetFloat("_UseNormal", normalMap != null ? normalStrength : 0f);
        material.SetFloat("_UsePacked", packedMap != null ? 1f : 0f);
        material.SetFloat("_TextureSize", Mathf.Max(0.1f, textureSize));
        if (baseColorMap != null) material.SetTexture("_BaseMap", baseColorMap);
        if (normalMap != null) material.SetTexture("_BumpMap", normalMap);
        if (packedMap != null) material.SetTexture("_PackedMap", packedMap);
        return material;
    }

    private Mesh BuildSurface(TerrainData data)
    {
        int resolution = Mathf.Clamp(surfaceResolution, 8, 256);
        int stride = resolution + 1;
        var vertices = new Vector3[stride * stride];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new int[resolution * resolution * 6];
        int triangle = 0;
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int i = z * stride + x;
                uv[i] = new Vector2((float)x / resolution, (float)z / resolution);
                normals[i] = data.GetInterpolatedNormal(uv[i].x, uv[i].y);
                vertices[i] = Ground(data, uv[i]) + normals[i] * surfaceOffset;
                colors[i] = new Color(1f, 1f, 1f, 0f);
                if (x == resolution || z == resolution) continue;
                triangles[triangle++] = i;
                triangles[triangle++] = i + stride;
                triangles[triangle++] = i + 1;
                triangles[triangle++] = i + 1;
                triangles[triangle++] = i + stride;
                triangles[triangle++] = i + stride + 1;
            }
        }

        var mesh = NewMesh("Moss surface");
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.uv2 = uv;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildTufts(TerrainData data, int layerIndex)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var maskUV = new List<Vector2>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        float[,,] weights = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        var random = new System.Random(seed);
        int candidates = Mathf.Min(Mathf.Max(0, maximumTufts) * 8,
            Mathf.CeilToInt(data.size.x * data.size.z * Mathf.Clamp(tuftsPerSquareMetre, 0f, 4f)));
        float minimumUp = Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
        float minHeight = Mathf.Max(0.01f, Mathf.Min(tuftHeight.x, tuftHeight.y));
        float maxHeight = Mathf.Max(minHeight, Mathf.Max(tuftHeight.x, tuftHeight.y));
        for (int i = 0; i < candidates && TuftCount < maximumTufts; i++)
        {
            var position = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            float weight = SampleWeight(weights, position, layerIndex);
            if (weight <= coverageThreshold || random.NextDouble() > weight) continue;
            int holeX = Mathf.Min((int)(position.x * data.holesResolution), data.holesResolution - 1);
            int holeZ = Mathf.Min((int)(position.y * data.holesResolution), data.holesResolution - 1);
            if (data.IsHole(holeX, holeZ)) continue;
            Vector3 normal = data.GetInterpolatedNormal(position.x, position.y);
            if (normal.y < minimumUp) continue;
            Vector3 root = Ground(data, position) + normal * surfaceOffset;
            float height = Mathf.Lerp(minHeight, maxHeight, (float)random.NextDouble());
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector3 right = Vector3.ProjectOnPlane(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)), normal).normalized;
            if (leafyTufts)
            {
                AddSprig(root, normal, right, height, position, vertices, normals, uv, maskUV, colors, triangles);
                TuftCount++;
                continue;
            }
            for (int plane = 0; plane < 2; plane++)
            {
                Vector3 side = plane == 0 ? right : Vector3.Cross(normal, right);
                int start = vertices.Count;
                for (int row = 0; row < 3; row++)
                {
                    float t = row * 0.5f;
                    float width = tuftWidth * (row == 0 ? 0.25f : row == 1 ? 0.5f : 0.015f);
                    for (int edge = 0; edge < 2; edge++)
                    {
                        vertices.Add(root + normal * (t * height) + side * ((edge * 2 - 1) * width));
                        normals.Add(normal);
                        uv.Add(new Vector2(edge, t));
                        maskUV.Add(position);
                        colors.Add(new Color(1f, 1f, 1f, t));
                    }
                }

                for (int row = 0; row < 2; row++)
                {
                    int a = start + row * 2;
                    triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
                }
            }

            TuftCount++;
        }

        var mesh = NewMesh("Moss tufts");
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, maskUV);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        Bounds bounds = mesh.bounds;
        bounds.Expand(1f); // Keep tips inside renderer bounds while the wind bends them.
        mesh.bounds = bounds;
        return mesh;
    }

    private void AddSprig(Vector3 root, Vector3 up, Vector3 right, float height, Vector2 position,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<Vector2> maskUV,
        List<Color> colors, List<int> triangles)
    {
        Vector3 forward = Vector3.Cross(up, right);
        for (int stem = 0; stem < 5; stem++)
        {
            float angle = stem * Mathf.PI * 0.4f;
            Vector3 outward = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
            Vector3 side = Vector3.Cross(up, outward);
            for (int pair = 0; pair < 5; pair++)
            {
                float t = (pair + 1f) / 6f;
                Vector3 center = root + up * (height * t) + outward * (height * 0.5f * t * t);
                float length = tuftWidth * (0.7f - t * 0.5f);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 tip = center + side * (sign * length) + outward * length * 0.35f + up * length * 0.3f;
                    Vector3 mid = Vector3.Lerp(center, tip, 0.55f);
                    Vector3 breadth = outward * length * 0.22f;
                    int start = vertices.Count;
                    vertices.Add(center);
                    vertices.Add(mid - breadth);
                    vertices.Add(tip);
                    vertices.Add(mid + breadth);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        normals.Add((up + side * sign * 0.25f).normalized);
                        uv.Add(new Vector2(corner == 1 ? 0f : corner == 3 ? 1f : 0.5f, corner == 2 ? 1f : 0f));
                        maskUV.Add(position);
                        colors.Add(new Color(1f, 1f, 1f, Mathf.Clamp01((vertices[start + corner] - root).magnitude / height)));
                    }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }
        }
    }

    public static float SampleWeight(float[,,] weights, Vector2 uv, int layer)
    {
        float x = Mathf.Clamp01(uv.x) * (weights.GetLength(1) - 1);
        float z = Mathf.Clamp01(uv.y) * (weights.GetLength(0) - 1);
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        int x1 = Mathf.Min(x0 + 1, weights.GetLength(1) - 1);
        int z1 = Mathf.Min(z0 + 1, weights.GetLength(0) - 1);
        return Mathf.Lerp(Mathf.Lerp(weights[z0, x0, layer], weights[z0, x1, layer], x - x0),
            Mathf.Lerp(weights[z1, x0, layer], weights[z1, x1, layer], x - x0), z - z0);
    }

    private static Vector3 Ground(TerrainData data, Vector2 uv)
    {
        return new Vector3(uv.x * data.size.x, data.GetInterpolatedHeight(uv.x, uv.y), uv.y * data.size.z);
    }

    private static Mesh NewMesh(string name)
    {
        return new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
    }

    private void CreateRenderer(string name, Mesh mesh, Material material)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        child.transform.SetParent(generated.transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
    }

    private void ClearGenerated()
    {
        Release(generated);
        Release(surfaceMesh);
        Release(tuftMesh);
        Release(surfaceMaterial);
        Release(tuftMaterial);
        generated = null;
        surfaceMesh = null;
        tuftMesh = null;
        surfaceMaterial = null;
        tuftMaterial = null;
        TuftCount = 0;
    }

    private static void Release(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
