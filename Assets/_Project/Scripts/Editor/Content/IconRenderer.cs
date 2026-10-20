using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Turns a 3D model or prefab into a transparent sprite for the UI. It photographs the model twice, on black and on white,
/// and works out transparency from the difference, which is reliable under any render pipeline.
/// </summary>
public static class IconRenderer
{
    private const float FieldOfView = 25f;

    /// <summary>Renders the object and saves it as a new sprite at a unique path next to assetPath.</summary>
    public static Sprite RenderToSprite(GameObject source, string assetPath, int size = 256)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var preview = new PreviewRenderUtility();
        Texture2D onBlack = null;
        Texture2D onWhite = null;
        Texture2D icon = null;

        try
        {
            GameObject instance = preview.InstantiatePrefabInScene(source);
            foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
            {
                canvas.gameObject.SetActive(false);
            }

            if (!TryGetBounds(instance, out Bounds bounds))
            {
                throw new InvalidOperationException($"'{source.name}' has nothing visible to take a picture of.");
            }

            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            Camera camera = preview.camera;
            camera.transform.rotation = Quaternion.Euler(22f, 155f, 0f);
            camera.transform.position = bounds.center - camera.transform.forward * distance;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            preview.cameraFieldOfView = FieldOfView;

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 120f, 0f);
            preview.lights[1].intensity = 0.6f;
            preview.lights[1].transform.rotation = Quaternion.Euler(-20f, -60f, 0f);
            preview.ambientColor = new Color(0.35f, 0.35f, 0.4f);

            onBlack = Render(preview, Color.black, size);
            onWhite = Render(preview, Color.white, size);
            icon = CombineTransparency(onBlack, onWhite);

            PrefabBuildUtility.EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            string path = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            File.WriteAllBytes(Path.GetFullPath(path), icon.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        finally
        {
            preview.Cleanup();
            DestroyIfSet(onBlack);
            DestroyIfSet(onWhite);
            DestroyIfSet(icon);
        }
    }

    private static Texture2D Render(PreviewRenderUtility preview, Color background, int size)
    {
        preview.camera.backgroundColor = background;
        preview.BeginStaticPreview(new Rect(0f, 0f, size, size));
        preview.Render(true);
        return preview.EndStaticPreview();
    }

    // A pixel that is fully opaque looks the same on both backgrounds; a transparent one takes on the background.
    private static Texture2D CombineTransparency(Texture2D onBlack, Texture2D onWhite)
    {
        Color[] black = onBlack.GetPixels();
        Color[] white = onWhite.GetPixels();
        var result = new Color[black.Length];
        for (int i = 0; i < black.Length; i++)
        {
            float difference = ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f;
            float alpha = Mathf.Clamp01(1f - difference);
            result[i] = alpha > 0.004f
                ? new Color(Mathf.Clamp01(black[i].r / alpha), Mathf.Clamp01(black[i].g / alpha), Mathf.Clamp01(black[i].b / alpha), alpha)
                : Color.clear;
        }

        var texture = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
        texture.SetPixels(result);
        texture.Apply();
        return texture;
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (found)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                found = true;
            }
        }

        return found;
    }

    private static void DestroyIfSet(Object target)
    {
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }
    }
}
