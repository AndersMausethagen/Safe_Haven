using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in art upgrade for MossPreview only. All generated material and mesh assets remain editable.</summary>
public static class ForestGroundShowcase
{
    private const string RootName = "Forest Material Showcase";
    private const string Root = ForestGroundTextures.Root;

    [MenuItem("Safe Haven/Art Pipeline/Upgrade Moss Preview Materials")]
    public static void Upgrade()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != TerrainMossTools.PreviewPath || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open the saved MossPreview scene outside Play mode before upgrading.");
        if (GameObject.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog("Forest materials", "The showcase already exists. Edit its materials or Terrain Moss controls directly.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Moss_BaseColor.png") == null) ForestGroundTextures.Generate();
        ForestGroundShaders.CreateMissing();
        Shader groundShader = AssetDatabase.LoadAssetAtPath<Shader>(ForestGroundShaders.GroundPath);
        Shader rockShader = AssetDatabase.LoadAssetAtPath<Shader>(ForestGroundShaders.StonePath);
        if (groundShader == null || rockShader == null || !groundShader.isSupported || !rockShader.isSupported)
            throw new InvalidOperationException("Resolve the Amplify shader errors before upgrading the preview.");
        foreach (Shader shader in new[] { groundShader, rockShader })
            if (ShaderUtil.GetShaderMessages(shader).Any(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                throw new InvalidOperationException("Resolve shader compilation errors in " + shader.name + " before upgrading the preview.");

        TerrainMoss moss = UnityEngine.Object.FindAnyObjectByType<TerrainMoss>();
        if (moss == null || moss.gameObject.scene != scene) throw new InvalidOperationException("MossPreview has no Terrain Moss.");
        Terrain terrain = moss.GetComponent<Terrain>();
        TerrainLayer[] layers = terrain.terrainData.terrainLayers;
        foreach (TerrainLayer layer in layers)
        {
            bool isMoss = layer == moss.MossLayer;
            string kind = isMoss ? "Moss" : "Soil";
            Undo.RecordObject(layer, "Apply forest PBR layer");
            layer.diffuseTexture = Texture(kind, "BaseColor");
            layer.normalMapTexture = Texture(kind, "Normal");
            layer.maskMapTexture = Texture(kind, "TerrainMask");
            layer.tileSize = new Vector2(1.5f, 1.5f);
            layer.normalScale = 0.8f;
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
        }
        var settings = new SerializedObject(moss);
        settings.FindProperty("baseColorMap").objectReferenceValue = Texture("Moss", "BaseColor");
        settings.FindProperty("normalMap").objectReferenceValue = Texture("Moss", "Normal");
        settings.FindProperty("packedMap").objectReferenceValue = Texture("Moss", "Packed");
        settings.FindProperty("normalStrength").floatValue = 1.15f;
        settings.FindProperty("textureSize").floatValue = 1.5f;
        settings.FindProperty("leafyTufts").boolValue = true;
        settings.FindProperty("tuftsPerSquareMetre").floatValue = 3.5f;
        settings.FindProperty("maximumTufts").intValue = 3500;
        settings.FindProperty("tuftHeight").vector2Value = new Vector2(0.045f, 0.11f);
        settings.FindProperty("tuftWidth").floatValue = 0.055f;
        settings.FindProperty("tipColor").colorValue = new Color(0.3f, 0.36f, 0.12f);
        settings.ApplyModifiedProperties();
        if (!moss.Rebuild()) throw new InvalidOperationException("Preview moss rebuild failed: " + moss.LastError);
        terrain.heightmapPixelError = 1f;

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create forest material showcase");
        Material soil = MaterialAsset("Damp Leaf Soil", groundShader, "Soil", 0f, new Color(0.9f, 0.92f, 0.9f));
        Material carpet = MaterialAsset("Cushion Moss", groundShader, "Soil", 1f, Color.white);
        Material blend = MaterialAsset("Forest Floor Blend", groundShader, "Soil", 0.5f, Color.white);
        Material stone = MaterialAsset("Mossy Granite", rockShader, "Stone", 1.65f, Color.white);
        Material bareStone = MaterialAsset("Weathered Granite", rockShader, "Stone", 0f, Color.white);

        var random = new System.Random(913);
        for (int i = 0; i < 11; i++)
        {
            float x = i < 6 ? 11f + (float)random.NextDouble() * 2f : 19f + (float)random.NextDouble() * 4f;
            float z = 10f + (float)random.NextDouble() * 14f;
            Vector3 point = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y, z);
            GameObject rock = new GameObject("Moss-covered granite " + (i + 1));
            rock.transform.SetParent(root.transform);
            rock.transform.position = point - Vector3.up * 0.12f;
            rock.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
            float scale = i == 0 ? 1.5f : Mathf.Lerp(0.5f, 1.3f, (float)random.NextDouble());
            rock.transform.localScale = new Vector3(scale * 1.3f, scale * 0.8f, scale);
            Mesh mesh = CreateRock(i);
            string path = Root + "/Granite_" + i + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            rock.AddComponent<MeshFilter>().sharedMesh = mesh;
            rock.AddComponent<MeshRenderer>().sharedMaterial = stone;
        }

        Material[] samples = { soil, blend, carpet, bareStone };
        string[] captions = { "DAMP FOREST SOIL", "SOIL + MOSS BLEND", "CUSHION MOSS", "WEATHERED GRANITE" };
        for (int i = 0; i < samples.Length; i++)
        {
            float x = 11f + i * 2.8f;
            float z = 7f;
            float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sample.name = captions[i];
            sample.transform.SetParent(root.transform);
            sample.transform.position = new Vector3(x, y + 0.2f, z);
            sample.transform.localScale = new Vector3(2.25f, 0.3f, 2f);
            UnityEngine.Object.DestroyImmediate(sample.GetComponent<Collider>());
            sample.GetComponent<MeshRenderer>().sharedMaterial = samples[i];
            Label(root.transform, captions[i], new Vector3(x, y + 0.12f, z - 1.3f));
        }

        Camera camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        camera.transform.position = new Vector3(15.2f, 7.5f, -1.5f);
        camera.transform.LookAt(new Vector3(15.2f, 0.5f, 13f));
        camera.fieldOfView = 60f;
        camera.backgroundColor = new Color(0.34f, 0.4f, 0.4f);
        camera.allowHDR = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        Light sun = RenderSettings.sun;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(32f, -50f, 0f);
            sun.intensity = 3.5f;
            sun.color = new Color(1f, 0.91f, 0.77f);
            sun.shadowStrength = 0.85f;
        }
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.52f, 0.58f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.35f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.16f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.28f, 0.35f, 0.33f);
        RenderSettings.fogDensity = 0.014f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);
        var colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(0.5f);
        colorAdjustments.saturation.Override(-5f);
        AssetDatabase.CreateAsset(profile, Root + "/ForestPreviewVolume.asset");
        foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        var volume = new GameObject("Forest light and tonemapping").AddComponent<Volume>();
        volume.transform.SetParent(root.transform);
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(moss);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save the upgraded MossPreview scene.");
        Selection.activeGameObject = terrain.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(16f, 0.6f, 12f), Quaternion.Euler(30f, 0f, 0f), 14f);
    }

    private static Texture2D Texture(string kind, string map)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + kind + "_" + map + ".png");
        if (texture == null) throw new InvalidOperationException("Missing forest texture: " + kind + "_" + map);
        return texture;
    }

    private static Material MaterialAsset(string name, Shader shader, string ground, float coverage, Color tint)
    {
        var material = new Material(shader) { name = name };
        material.SetTexture("_GroundColor", Texture(ground, "BaseColor"));
        material.SetTexture("_GroundNormal", Texture(ground, "Normal"));
        material.SetTexture("_GroundPacked", Texture(ground, "Packed"));
        material.SetTexture("_GroundSurface", Texture(ground, "Packed"));
        material.SetTexture("_MossColor", Texture("Moss", "BaseColor"));
        material.SetTexture("_MossNormal", Texture("Moss", "Normal"));
        material.SetTexture("_MossSurface", Texture("Moss", "Packed"));
        material.SetFloat("_MossCoverage", coverage);
        material.SetFloat("_Tiling", 2f);
        material.SetFloat("_Relief", 0.018f);
        material.SetFloat("_GroundNormalStrength", 0.65f);
        material.SetFloat("_MossNormalStrength", 0.85f);
        material.SetColor("_Tint", tint);
        AssetDatabase.CreateAsset(material, Root + "/" + name + ".mat");
        return material;
    }

    private static Mesh CreateRock(int seed)
    {
        int rings = 24, slices = 40;
        var vertices = new Vector3[(rings + 1) * (slices + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[rings * slices * 6];
        int index = 0;
        for (int y = 0; y <= rings; y++)
            for (int x = 0; x <= slices; x++)
            {
                float u = (float)x / slices, v = (float)y / rings;
                float phi = u * Mathf.PI * 2f, theta = v * Mathf.PI;
                Vector3 direction = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                float shape = 0.85f + Mathf.PerlinNoise(direction.x * 2f + seed * 7f, direction.z * 2f + direction.y * 1.3f) * 0.3f;
                int i = y * (slices + 1) + x;
                vertices[i] = direction * shape;
                uv[i] = new Vector2(u * 2f, v);
                if (x == slices || y == rings) continue;
                int b = i + slices + 1;
                triangles[index++] = i; triangles[index++] = i + 1; triangles[index++] = b;
                triangles[index++] = i + 1; triangles[index++] = b + 1; triangles[index++] = b;
            }
        var mesh = new Mesh { name = "Weathered granite " + seed, vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals();
        // The pole's triangles have no area, which leaves it without a normal: a vertex like that lights as NaN.
        Vector3[] normals = mesh.normals;
        for (int n = 0; n < normals.Length; n++)
        {
            if (normals[n].sqrMagnitude < 0.5f)
            {
                normals[n] = vertices[n].normalized;
            }
        }

        mesh.normals = normals;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void Label(Transform parent, string text, Vector3 position)
    {
        var label = new GameObject(text + " label").AddComponent<TextMeshPro>();
        label.transform.SetParent(parent);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        label.text = text;
        label.fontSize = 2.3f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.9f, 0.85f, 0.65f);
        label.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
    }
}
