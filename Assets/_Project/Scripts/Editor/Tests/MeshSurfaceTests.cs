using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;

public sealed class MeshSurfaceTests
{
    [TestCase("ForwardLit")]
    [TestCase("ShadowCaster")]
    [TestCase("DepthOnly")]
    [TestCase("DepthNormals")]
    [TestCase("ForwardLit", true)]
    public void GeometryPasses_DisplaceAndFadeInWorldMetres(string pass, bool interiorHeight = false)
    {
        if (!SystemInfo.supportsTessellationShaders) Assert.Ignore("Hardware tessellation is required.");
        Shader shader = Shader.Find(MeshSurface.ShaderName);
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        var material = new Material(shader);
        var probe = new Material(Shader.Find("Hidden/Safe Haven/Mesh Depth Probe"));
        var heightMap = new Texture2D(16, 16, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp };
        var mesh = new Mesh();
        var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        Vector4 previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
        Vector4 previousLight = Shader.GetGlobalVector("_LightDirection");
        try
        {
            mesh.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(1, 0, 1) };
            mesh.normals = Enumerable.Repeat(Vector3.up, 4).ToArray();
            mesh.tangents = Enumerable.Repeat(new Vector4(1, 0, 0, -1), 4).ToArray();
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    heightMap.SetPixel(x, y, Color.white * (x > 2 && x < 13 && y > 2 && y < 13 ? 1 : 0));
            heightMap.Apply();
            material.SetTexture("_HeightMap", interiorHeight ? heightMap : Texture2D.whiteTexture);
            material.SetFloat("_HeightMidpoint", 0);
            material.SetFloat("_TessellationNear", 10);
            material.SetFloat("_TessellationFar", 20);
            target.Create();
            Color32[] flat = Render(0, Matrix4x4.identity);
            Color32[] raised = Render(0.3f, Matrix4x4.identity);
            Assert.Greater(Difference(flat, raised), interiorHeight ? 15 : 60, pass + " must move real geometry.");
            if (interiorHeight)
            {
                material.SetFloat("_TessellationFactor", 1);
                Assert.AreEqual(0, Difference(flat, Render(0.3f, Matrix4x4.identity)),
                    "Only new tessellated interior vertices may move with this height map.");
                material.SetFloat("_TessellationFactor", 16);
            }
            Assert.AreEqual(0, Difference(flat, Render(0.3f, Matrix4x4.identity, true)), "Displacement must fade out.");
            Matrix4x4 scaled = Matrix4x4.Scale(new Vector3(1, 3, 1));
            Assert.AreEqual(0, Difference(raised, Render(0.3f, scaled)), "Height must remain in world metres with nonuniform scale.");
            Assert.IsEmpty(ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == ShaderCompilerMessageSeverity.Error)
                .Select(m => m.message).ToArray());

            Color32[] Render(float strength, Matrix4x4 matrix, bool far = false)
            {
                material.SetFloat("_HeightStrength", strength);
                Vector3 eye = new Vector3(0, 1.5f, -4);
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                    Matrix4x4.TRS(eye, Quaternion.LookRotation(-eye), Vector3.one).inverse;
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1.5f, 1.5f, -1, 1, 0.1f, 20), false);
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, Color.magenta);
                    commands.SetViewProjectionMatrices(view, projection);
                    commands.SetGlobalVector("_WorldSpaceCameraPos", far ? new Vector3(0, 0, -100) : eye);
                    commands.SetGlobalVector("_LightDirection", Vector3.up);
                    commands.DrawMesh(mesh, matrix, material, 0, material.FindPass(pass));
                    commands.DrawProcedural(Matrix4x4.identity, probe, 0, MeshTopology.Triangles, 3);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
            Shader.SetGlobalVector("_LightDirection", previousLight);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(probe);
            Object.DestroyImmediate(heightMap);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    private static int Difference(Color32[] a, Color32[] b) =>
        a.Zip(b, (x, y) => IsBackground(x) != IsBackground(y) ? 1 : 0).Sum();

    private static bool IsBackground(Color32 value) => value.r > 240 && value.g < 10 && value.b > 240;

    [Test]
    public void Bounds_ExpandForDisplacementAndRestoreOnDisable()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Mesh Surface Bounds");
        var material = new Material(Shader.Find(MeshSurface.ShaderName));
        var mesh = new Mesh { bounds = new Bounds(Vector3.zero, Vector3.one * 2) };
        try
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Bounds original = renderer.localBounds;
            material.SetFloat("_HeightStrength", 0.4f);
            material.SetFloat("_HeightMidpoint", 0.5f);
            go.transform.localScale = new Vector3(1, 2, 0.5f);
            var surface = go.AddComponent<MeshSurface>();
            Assert.AreEqual(original.size + new Vector3(0.4f, 0.2f, 0.8f), renderer.localBounds.size);
            surface.enabled = false;
            Assert.AreEqual(original, renderer.localBounds);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void Conversion_CreatesDedicatedMaterialAndPreservesSourceAndOtherSlots()
    {
        const string path = "Assets/_Project/Art/Terrain/SafeHaven/__MeshSurfaceTest.mat";
        Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(path), "The test must never overwrite an existing asset.");
        var scene = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Mesh Surface Conversion");
        var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Material copy = null;
        try
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            source.SetColor("_BaseColor", Color.green);
            source.SetTextureScale("_BaseMap", new Vector2(2, 3));
            source.SetFloat("_Metallic", 0.4f);
            source.SetTexture("_MetallicGlossMap", Texture2D.whiteTexture);
            source.EnableKeyword("_METALLICSPECGLOSSMAP");
            source.SetFloat("_ReceiveShadows", 0);
            source.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { source, source };
            copy = MeshSurfaceWindow.ConvertAndAssign(renderer, 0, path);
            Assert.AreEqual(MeshSurface.ShaderName, copy.shader.name);
            Assert.AreEqual(Shader.Find("Universal Render Pipeline/Lit"), source.shader);
            Assert.AreEqual(0.4f, source.GetFloat("_Metallic"));
            Assert.AreEqual(1, copy.GetFloat("_Metallic"), "URP ignores the scalar metallic value when using a map.");
            Assert.AreEqual(Color.green, copy.GetColor("_BaseColor"));
            Assert.AreEqual(new Vector2(2, 3), copy.GetTextureScale("_BaseMap"));
            Assert.IsTrue(copy.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"));
            Assert.AreEqual(source, renderer.sharedMaterials[1]);
            Assert.IsNotNull(go.GetComponent<MeshSurface>());
            Assert.IsFalse(EditorUtility.IsDirty(copy));
            Assert.Throws<System.InvalidOperationException>(() => MeshSurfaceWindow.ConvertAndAssign(renderer, 1, path));
            source.SetFloat("_Surface", 1);
            Assert.Throws<System.InvalidOperationException>(() => MeshSurfaceWindow.ConvertAndAssign(renderer, 1, path));
        }
        finally
        {
            Undo.ClearUndo(go);
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) Undo.ClearUndo(renderer);
            var surface = go.GetComponent<MeshSurface>();
            if (surface != null) Undo.ClearUndo(surface);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(source);
            if (copy != null) AssetDatabase.DeleteAsset(path);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
