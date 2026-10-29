using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TerrainDisplacementTests
{
    [TestCase("WorldTerrainLit.shader", 6)]
    [TestCase("WorldTerrainAdd.shader", 2)]
    [TestCase("WorldTerrainBase.shader", 5)]
    public void GeometryPasses_UseSharedTessellationAndCompile(string file, int minimumPasses)
    {
        string path = "Assets/_Project/Shaders/Terrain/" + file;
        string source = File.ReadAllText(path);
        Assert.GreaterOrEqual(source.Split(new[] { "#pragma domain WorldDomain" },
            System.StringSplitOptions.None).Length - 1, minimumPasses);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        Assert.IsEmpty(ShaderUtil.GetShaderMessages(shader)
            .Where(m => m.severity == ShaderCompilerMessageSeverity.Error).Select(m => m.message).ToArray());
    }

    [TestCase("Safe Haven/Terrain (World)", 0)]
    [TestCase("Safe Haven/Terrain (World)", 4)]
    [TestCase("Safe Haven/Terrain (World)", 8)]
    [TestCase("Hidden/Safe Haven/Terrain/Add", 4)]
    [TestCase("Hidden/Safe Haven/Terrain/Add", 8)]
    [TestCase("Hidden/Safe Haven/Terrain/Base", 0)]
    [TestCase("Safe Haven/Terrain (World)", 8, true)]
    [TestCase("Hidden/Safe Haven/Terrain/Base", 0, true)]
    [TestCase("Safe Haven/Terrain (World)", 0, false, true)]
    [TestCase("Hidden/Safe Haven/Terrain/Add", 8, false, true)]
    [TestCase("Hidden/Safe Haven/Terrain/Base", 0, false, true)]
    public void Displacement_MovesRenderedGeometryIncludingAdditionalLayerPasses(string shaderName, int layer, bool checkNormals = false, bool footprints = false)
    {
        if (!SystemInfo.supportsTessellationShaders)
            Assert.Ignore("Hardware tessellation is required.");
        var material = new Material(Shader.Find(shaderName));
        var masks = new Texture2DArray(2, 2, 9, TextureFormat.RGBA32, false, true);
        var layerData = new Texture2D(16, 3, TextureFormat.RGBAFloat, false, true);
        var control = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var empty = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var mesh = new Mesh();
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        Vector4 previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
        try
        {
            for (int i = 0; i < 9; i++)
            {
                masks.SetPixels(checkNormals ? new[] { Color.black, Color.white, Color.black, Color.white }
                    : new[] { Color.white, Color.white, Color.white, Color.white }, i);
                layerData.SetPixel(i, 0, new Color(1, 1, 0, 0));
                layerData.SetPixel(i, 1, new Color(1, 0, 1, 0.5f));
                layerData.SetPixel(i, 2, new Color(1, 0, 0, 0));
            }
            masks.Apply();
            layerData.Apply();
            control.SetPixels(Enumerable.Repeat(new Color(1, 0, 0, 0), 4).ToArray());
            control.Apply();
            empty.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
            empty.Apply();
            material.SetTexture("_DisplacementMasks", masks);
            material.SetTexture("_DisplacementLayers", layerData);
            material.SetFloat("_DisplacementLayerCount", 9);
            material.SetFloat("_CliffStrength", 0);
            material.SetFloat("_DisplacementNear", 8);
            material.SetFloat("_DisplacementFar", 35);
            material.SetFloat("_TessellationFactor", 8);
            material.SetFloat("_TessellationEdgeLength", 0.08f);
            material.SetVector("_DisplacementTerrainSize", new Vector4(2, 2, 0.01f, 0));
            material.SetTexture("_Control", control);
            for (int i = 0; i < 4; i++)
                material.SetTexture("_DisplacementControl" + i, i == layer / 4 ? control : empty);
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 2), new Vector3(2, 0, 0), new Vector3(2, 0, 2) };
            mesh.normals = Enumerable.Repeat(Vector3.up, 4).ToArray();
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            target.Create();
            if (footprints)
            {
                mesh.uv = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.505f),
                    new Vector2(0.505f, 0.5f), new Vector2(0.505f, 0.505f) };
                material.SetVector("_DisplacementTerrainSize", new Vector4(400, 400, 0.01f, 0));
                material.SetFloat("_DisplacementDataVersion", 2);
                material.SetTexture("_FootprintField", Texture2D.whiteTexture);
                material.SetVector("_FootprintArea", new Vector4(0, 0, 0.5f, 0));
                Color32[] original = Render(0);
                material.SetVector("_FootprintSettings", new Vector4(0.2f, 0.75f, 0.65f, 1));
                Color32[] pressed = Render(0);
                Assert.Greater(original.Zip(pressed, (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum(), 100,
                    "Footprints must press real geometry even with authored displacement amplitude at zero.");
                if (shaderName == "Safe Haven/Terrain (World)")
                {
                    material.SetFloat("_SurfaceDebug", 3);
                    Assert.Greater(Render(0).Count(c => c.a > 128 && c.r > 200), 100,
                        "The fragment footprint lookup must use world positions, not global terrain UVs as patch-local positions.");
                    material.SetFloat("_SurfaceDebug", 0);
                }
                for (int i = 0; i < 9; i++) layerData.SetPixel(i, 2, Color.clear);
                layerData.Apply();
                Color32[] hardGround = Render(0);
                Assert.AreEqual(0, original.Zip(hardGround, (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum(),
                    "Hard painted layers must not retain impressions.");

                for (int i = 0; i < 9; i++) layerData.SetPixel(i, 2, Color.white);
                layerData.Apply();
                material.SetVector("_FootprintSettings", Vector4.zero);
                masks.SetPixels(Enumerable.Repeat(Color.black, 4).ToArray(), layer);
                masks.Apply();
                Color32[] compressedFloor = Render(0.2f);
                masks.SetPixels(Enumerable.Repeat(new Color(0.25f, 0.25f, 0.25f), 4).ToArray(), layer);
                masks.Apply();
                Color32[] negativeHeight = Render(0.2f);
                material.SetVector("_FootprintSettings", new Vector4(0, 1, 1, 1));
                Color32[] compressedNegativeHeight = Render(0.2f);
                Assert.Greater(negativeHeight.Zip(compressedNegativeHeight,
                    (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum(), 100,
                    "Compression must flatten detail below the terrain origin too, not only positive peaks.");
                Assert.AreEqual(0, compressedFloor.Zip(compressedNegativeHeight,
                    (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum(),
                    "Full sole compression must reach the painted height range's lower bound without raising valleys.");
                return;
            }
            if (checkNormals)
            {
                material.SetFloat("_SurfaceDebug", 2);
                material.SetFloat("_DisplacementNormalStrength", 0);
                Color32[] oldNormals = Render(0.2f);
                material.SetFloat("_DisplacementNormalStrength", 1);
                Color32[] heightNormals = Render(0.2f);
                int litChanges = oldNormals.Zip(heightNormals, (a, b) =>
                    a.a > 128 && b.a > 128 && Mathf.Abs(a.r - b.r) + Mathf.Abs(a.b - b.b) > 5 ? 1 : 0).Sum();
                Assert.Greater(litChanges, 100, "Height slopes must change lighting normals without changing geometry.");
                material.SetVector("_TerrainDetailQuality", new Vector4(1, 1, 1, 0));
                CollectionAssert.AreEqual(oldNormals, Render(0.2f),
                    "The quality override must disable height-normal sampling independently of geometry.");
                return;
            }
            Color32[] flat = Render(0);
            Color32[] displaced = Render(0.2f);
            Assert.Greater(flat.Count(c => c.a > 128), 100, "The fixture must render visible terrain geometry.");
            int changed = flat.Zip(displaced, (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum();
            Assert.Greater(changed, 100, "Displacement must change the actual projected silhouette, not just shading.");
            material.SetVector("_TerrainDetailQuality", TerrainSurface.QualityParameters(TerrainSurface.DetailQuality.Off));
            Color32[] disabled = Render(0.2f);
            Assert.AreEqual(0, flat.Zip(disabled, (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum(),
                "Off quality must restore the original rendered silhouette.");
            material.SetVector("_TerrainDetailQuality", Vector4.one);
            Color32[] distant = Render(0.2f, true);
            Assert.AreEqual(0, flat.Zip(distant, (a, b) => (a.a > 128) != (b.a > 128) ? 1 : 0).Sum(),
                "Geometry must return to the original surface beyond the 35-metre fade distance.");

            Color32[] Render(float strength, bool far = false)
            {
                material.SetFloat("_DisplacementStrength", strength);
                Vector3 eye = new Vector3(1, 1, -3);
                Matrix4x4 view = Matrix4x4.TRS(eye, Quaternion.LookRotation(new Vector3(1, 0, 1) - eye), Vector3.one).inverse;
                view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * view;
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1.5f, 1.5f, -0.75f, 0.75f, 0.1f, 20), false);
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(view, projection);
                    commands.SetGlobalVector("_WorldSpaceCameraPos", far ? new Vector3(1, 1, -100) : eye);
                    commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, material.FindPass("ForwardLit"));
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }

        }
        finally
        {
            RenderTexture.active = previous;
            Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(masks);
            Object.DestroyImmediate(layerData);
            Object.DestroyImmediate(control);
            Object.DestroyImmediate(empty);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void FootprintField_StampsTreadRecoversAndRecentresWithoutMutatingAssets()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(64, 2, 64) };
        var profile = ScriptableObject.CreateInstance<TerrainSurfaceProfile>();
        profile.footprintArea = 8;
        profile.footprintResolution = TerrainSurfaceProfile.HeightResolution.Medium;
        profile.footprintLifetime = 30;
        var go = Terrain.CreateTerrainGameObject(data);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        try
        {
            go.AddComponent<TerrainSurface>().Profile = profile;
            var prints = go.AddComponent<TerrainFootprints>();
            var serialized = new SerializedObject(prints);
            serialized.FindProperty("stampShader").objectReferenceValue = Shader.Find("Hidden/Safe Haven/Terrain/Footprint Stamp");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            prints.Stamp(new Vector3(20, 0, 20), Vector3.forward);
            float[] initial = Read(prints.Field);
            Assert.AreEqual(prints.Field, Shader.GetGlobalTexture("_GrassFootprint0"));
            Assert.AreEqual(1, Shader.GetGlobalFloat("_GrassFootprintCount"));
            Assert.Greater(initial.Max(), 0.8f);
            Assert.That(initial.Count(x => x > 0.1f), Is.InRange(20, 300), "A boot must occupy a small, foot-sized area, not the whole field.");
            Assert.Greater(initial.Count(x => x > 0.2f && x < 0.7f), 5, "Tread grooves must differ from the solid sole.");
            prints.Advance(0);
            CollectionAssert.AreEqual(initial, Read(prints.Field), "Paused game time must not erase tracks.");
            for (int frame = 0; frame < 900; frame++) prints.Advance(1f / 60);
            Assert.That(Read(prints.Field).Max(), Is.EqualTo(initial.Max() - 0.5f).Within(0.002f));
            prints.Stamp(new Vector3(20, 0, 20), Vector3.forward);
            Assert.Greater(Read(prints.Field).Max(), 0.8f, "Walking on an old print must refresh it.");
            prints.Stamp(new Vector3(22.5f, 0, 20), Vector3.right);
            Assert.Greater(Read(prints.Field).Count(x => x > 0.1f), initial.Count(x => x > 0.1f),
                "A small window shift must preserve the older print.");
            prints.Stamp(new Vector3(45, 0, 45), Vector3.forward);
            Assert.Less(Read(prints.Field).Count(x => x > 0.1f), initial.Count(x => x > 0.1f) * 1.5f,
                "A distant recenter must discard old tracks rather than repeat them.");
            prints.Advance(30.1f);
            Assert.AreEqual(0, Read(prints.Field).Max(), "Footprints must be completely gone after the configured lifetime.");
            Assert.AreEqual(30, profile.footprintLifetime);
            profile.footprints = false;
            prints.Advance(0);
            Assert.IsNull(prints.Field);
            var block = new MaterialPropertyBlock();
            go.GetComponent<Terrain>().GetSplatMaterialPropertyBlock(block);
            Assert.AreEqual(0, block.GetVector("_FootprintSettings").w);
            Assert.AreEqual(0, Shader.GetGlobalFloat("_GrassFootprintCount"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(profile);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }

        float[] Read(RenderTexture texture)
        {
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                return readback.GetPixels().Select(c => c.r).ToArray();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }
    }

    [Test]
    public void GrassFootprints_FlattenRenderedBladesAndRestoreTheirSilhouette()
    {
        Shader shader = Shader.Find("Safe Haven/Grass");
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        var material = new Material(shader);
        var mesh = new Mesh();
        var target = new RenderTexture(256, 256, 24);
        var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        Texture previousField = Shader.GetGlobalTexture("_GrassFootprint0");
        Vector4 previousDepths = Shader.GetGlobalVector("_GrassFootprintDepths");
        Vector4[] previousAreas = Shader.GetGlobalVectorArray("_GrassFootprintAreas");
        float previousCount = Shader.GetGlobalFloat("_GrassFootprintCount");
        float previousColour = Shader.GetGlobalFloat("_GrassTerrainColorOn");
        float previousInteractors = Shader.GetGlobalFloat("_GrassInteractorCount");
        try
        {
            material.SetFloat("_WindStrength", 0);
            material.SetFloat("_ClumpHeight", 0);
            material.SetFloat("_FacingBlades", 0);
            material.SetFloat("_FarStart", 100);
            material.SetFloat("_FarEnd", 200);
            mesh.vertices = new[] { new Vector3(-0.1f, 0, 0), new Vector3(0.1f, 0, 0),
                new Vector3(-0.1f, 0.5f, 0), new Vector3(0.1f, 0.5f, 0) };
            mesh.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
            mesh.colors = new[] { Color.black, Color.black, Color.red, Color.red };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            target.Create();
            Shader.SetGlobalTexture("_GrassFootprint0", Texture2D.whiteTexture);
            Shader.SetGlobalVectorArray("_GrassFootprintAreas", new[] {
                new Vector4(-1, -1, 0.5f, 1), Vector4.zero, Vector4.zero, Vector4.zero });
            Shader.SetGlobalFloat("_GrassTerrainColorOn", 0);
            Shader.SetGlobalFloat("_GrassInteractorCount", 0);
            Shader.SetGlobalVector("_GrassFootprintDepths", Vector4.zero);
            int standing = Render(0);
            Render(1);
            double flatRow = readback.GetPixels32().Select((c, i) => new { c.a, row = i / 256 })
                .Where(p => p.a > 128).Average(p => p.row);
            Shader.SetGlobalVector("_GrassFootprintDepths", new Vector4(0.2f, 0, 0, 0));
            int flattened = Render(1);
            Assert.Greater(standing, 500);
            Assert.Greater(flattened, 0, "Depressed blades must remain inside the verification camera.");
            Assert.Less(flattened, standing / 4, "Pressed blades must visibly lie down, exposing the ground trail.");
            double depressedRow = readback.GetPixels32().Select((c, i) => new { c.a, row = i / 256 })
                .Where(p => p.a > 128).Average(p => p.row);
            Assert.Less(depressedRow, flatRow - 40,
                "Flattened grass must descend with the compressed ground instead of floating above the boot tread.");
            Assert.AreEqual(standing, Render(0), "Recovered grass must return to the original silhouette.");
        }
        finally
        {
            Shader.SetGlobalTexture("_GrassFootprint0", previousField);
            Shader.SetGlobalVector("_GrassFootprintDepths", previousDepths);
            if (previousAreas.Length > 0) Shader.SetGlobalVectorArray("_GrassFootprintAreas", previousAreas);
            Shader.SetGlobalFloat("_GrassFootprintCount", previousCount);
            Shader.SetGlobalFloat("_GrassTerrainColorOn", previousColour);
            Shader.SetGlobalFloat("_GrassInteractorCount", previousInteractors);
            RenderTexture.active = previous;
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }

        int Render(float pressed)
        {
            Shader.SetGlobalFloat("_GrassFootprintCount", pressed);
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                Matrix4x4.TRS(new Vector3(0, 0.25f, -2), Quaternion.identity, Vector3.one).inverse;
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(view, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, 0.1f, 10), false));
                commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, material.FindPass("ForwardLit"));
                Graphics.ExecuteCommandBuffer(commands);
            }
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            readback.Apply();
            return readback.GetPixels32().Count(c => c.a > 128);
        }
    }

    [Test]
    public void QualityParameters_DisableGeometryAndScaleCostWithoutChangingAuthoredAmplitude()
    {
        Assert.AreEqual(0, TerrainSurface.QualityParameters(TerrainSurface.DetailQuality.Off).x);
        Assert.AreEqual(Vector4.one, TerrainSurface.QualityParameters(TerrainSurface.DetailQuality.Ultra));
        Vector4 balanced = TerrainSurface.QualityParameters(TerrainSurface.DetailQuality.Balanced);
        Assert.AreEqual(1, balanced.x);
        Assert.That(balanced.y, Is.InRange(0.1f, 0.99f));
        Assert.That(balanced.z, Is.InRange(0.1f, 0.99f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => TerrainSurface.QualityParameters((TerrainSurface.DetailQuality)999));
    }

    [Test]
    public void Setup_BakesHalfFloatHeightProfileAndRefreshPreservesTuningAndGuids()
    {
        string folder = "Assets/__TerrainSurfaceTest_" + System.Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(8, 2, 8) };
        var source = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true);
        var terrainLayer = new TerrainLayer();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject go = null;
        try
        {
            source.SetPixels(new[] { new Color(0.1234f, 0, 0.2f), new Color(0.4567f, 0, 0.4f),
                new Color(0.6789f, 0, 0.6f), new Color(0.9123f, 0, 0.8f) });
            source.Apply();
            AssetDatabase.CreateAsset(source, folder + "/Height.asset");
            terrainLayer.maskMapTexture = source;
            AssetDatabase.CreateAsset(terrainLayer, folder + "/Layer.terrainlayer");
            data.terrainLayers = new[] { terrainLayer };
            AssetDatabase.CreateAsset(data, folder + "/Terrain.asset");
            go = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            Material first = TerrainDisplacementEditor.Apply(terrain);
            Assert.IsFalse(terrain.drawInstanced, "Domain displacement requires expanded, non-instanced terrain vertices.");
            TerrainSurface surface = terrain.GetComponent<TerrainSurface>();
            Assert.IsNotNull(surface);
            TerrainSurfaceProfile profile = surface.Profile;
            Assert.IsNotNull(profile);
            profile.resolution = TerrainSurfaceProfile.HeightResolution.Medium;
            var entry = profile.Find(terrainLayer);
            entry.heightMap = source;
            entry.amplitude = 2;
            entry.midpoint = 0.3f;
            entry.footprintResponse = 0.8f;
            first.SetFloat("_DisplacementStrength", 0);
            first.SetFloat("_TessellationFactor", 23);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first.GetTexture("_DisplacementMasks")));
            Material refreshed = TerrainDisplacementEditor.Apply(terrain);
            Assert.AreEqual(first, refreshed);
            Assert.AreEqual(0, refreshed.GetFloat("_DisplacementStrength"), "Refreshing must preserve disabled displacement.");
            Assert.AreEqual(23, refreshed.GetFloat("_TessellationFactor"));
            var masks = (Texture2DArray)refreshed.GetTexture("_DisplacementMasks");
            Assert.AreEqual(TextureFormat.RHalf, masks.format);
            Assert.AreEqual(512, masks.width);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(masks)));
            Color[] pixels = masks.GetPixels(0);
            Assert.That(pixels.Min(p => p.r), Is.EqualTo(0.1234f).Within(0.002f));
            Assert.That(pixels.Max(p => p.r), Is.EqualTo(0.9123f).Within(0.002f));
            var settings = (Texture2D)refreshed.GetTexture("_DisplacementLayers");
            Assert.AreEqual(2, settings.GetPixel(0, 1).b);
            Assert.AreEqual(0.3f, settings.GetPixel(0, 1).a);
            Assert.AreEqual(0.8f, settings.GetPixel(0, 2).r);
            Assert.IsNotNull(go.GetComponent<TerrainFootprints>());
            Assert.AreEqual(source, terrainLayer.maskMapTexture);
            surface.SetQuality(TerrainSurface.DetailQuality.Off);
            var block = new MaterialPropertyBlock();
            terrain.GetSplatMaterialPropertyBlock(block);
            Assert.AreEqual(0, block.GetVector("_TerrainDetailQuality").x);
            Assert.AreEqual(23, refreshed.GetFloat("_TessellationFactor"));
            terrain.materialTemplate = null;
            Material restored = TerrainDisplacementEditor.Apply(terrain);
            Assert.AreEqual(refreshed, restored);
            Assert.AreEqual(0, restored.GetFloat("_DisplacementStrength"));
            Assert.AreEqual(23, restored.GetFloat("_TessellationFactor"));
        }
        finally
        {
            if (go != null)
            {
                foreach (Component component in go.GetComponents<Component>())
                    Undo.ClearUndo(component);
                Undo.ClearUndo(go);
                Object.DestroyImmediate(go);
            }
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test]
    public void FootContacts_AutomaticallyWireTerrainRoutingWithoutDuplicates()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Foot contact wiring");
        try
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var audio = go.AddComponent<PlayerAudio>();
            var awake = typeof(PlayerAudio).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awake.Invoke(audio, null);
            awake.Invoke(audio, null);
            Assert.AreEqual(1, go.GetComponents<PlayerTerrainFootprints>().Length);
            Assert.IsTrue(go.GetComponent<PlayerTerrainFootprints>().enabled);
        }
        finally
        {
            Object.DestroyImmediate(go);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
