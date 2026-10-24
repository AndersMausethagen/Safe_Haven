using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders a model with its imported materials and the real project shaders to a PNG, for LocalSpawn's
/// "Preview in Unity" (called through the Unity CLI). Uses an isolated preview scene, so the open scene is untouched;
/// post-processing (bloom, tonemapping) comes from the open scene's global volumes, as in the game.
/// </summary>
public static class LocalSpawnPreview
{
    private static readonly int Layer1 = Shader.PropertyToID("_L1Amount");
    private static readonly int Layer2 = Shader.PropertyToID("_L2Amount");

    /// <summary>Imports what LocalSpawn just wrote (model, sidecar, layer textures), renders the model and returns the
    /// PNG path. yaw/pitch in degrees; layer1/layer2 override Layered Lit's layer amounts (negative = keep the
    /// material's).</summary>
    public static string Render(string assetPath, string outputPng, int size = 768, float yaw = 30f, float pitch = 15f,
                                float layer1 = -1f, float layer2 = -1f, bool postProcessing = true)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model == null)
        {
            throw new FileNotFoundException($"No model at {assetPath}.");
        }

        var preview = new PreviewRenderUtility();
        try
        {
            GameObject instance = preview.InstantiatePrefabInScene(model);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                throw new System.InvalidOperationException($"{assetPath} has no renderers.");
            }

            Bounds bounds = renderers[0].bounds;
            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.GetPropertyBlock(block);
                if (layer1 >= 0f) block.SetFloat(Layer1, Mathf.Clamp01(layer1));
                if (layer2 >= 0f) block.SetFloat(Layer2, Mathf.Clamp01(layer2));
                renderer.SetPropertyBlock(block);
            }

            Camera camera = preview.camera;
            camera.fieldOfView = 30f;
            float radius = Mathf.Max(bounds.extents.magnitude, 1e-3f);
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.position = bounds.center + rotation * new Vector3(0f, 0f, -distance);
            camera.transform.rotation = rotation;
            camera.nearClipPlane = Mathf.Max(distance - radius * 2f, distance * 0.01f);
            camera.farClipPlane = distance + radius * 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.17f, 0.18f, 1f);
            camera.allowHDR = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = postProcessing;

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, yaw - 40f, 0f);
            preview.lights[1].intensity = 0.5f;
            preview.lights[1].transform.rotation = Quaternion.Euler(-20f, yaw + 150f, 0f);
            preview.ambientColor = new Color(0.25f, 0.26f, 0.28f, 1f);

            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPng)));
            File.WriteAllBytes(outputPng, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return Path.GetFullPath(outputPng);
        }
        finally
        {
            preview.Cleanup();
        }
    }
}
