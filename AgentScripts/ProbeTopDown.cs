using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A top-down picture of a scene (`--entry ProbeTopDown.Shoot --args '["scene path", "out.png", x, z, size]'`). Opens the scene, renders, reopens the previous one.</summary>
public static class ProbeTopDown
{
    public static string Shoot(string scenePath, string outPath, float x, float z, float size)
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        if (previous != scenePath) EditorSceneManager.OpenScene(scenePath);
        var go = new GameObject("TopDown Probe Camera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = size * 0.5f;
        camera.transform.SetPositionAndRotation(new Vector3(x, 200f, z), Quaternion.Euler(90f, 0f, 0f));
        camera.farClipPlane = 400f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var texture = new RenderTexture(1024, 1024, 24);
        camera.targetTexture = texture;
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        camera.Render();
        RenderSettings.fog = fog;
        RenderTexture.active = texture;
        var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(outPath, image.EncodeToPNG());
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(texture);
        if (previous != scenePath && !string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        return "saved " + outPath;
    }
}
