using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Draws the original Hearthline marks and installs them on the existing UI assets.</summary>
public static class HearthlineArtworkBuilder
{
    private const string Folder = "Assets/_Project/UI/Hearthline";
    private const string StylePath = "Assets/_Project/Resources/UIStyle.asset";
    private const string PlayerPath = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    private const string MenuPath = "Assets/_Project/Scenes/MainMenu.unity";
    private const int Size = 128;

    [MenuItem("Safe Haven/Setup/UI/Regenerate Hearthline Artwork", false, 200)]
    public static void Install()
    {
        UIStyle style = AssetDatabase.LoadAssetAtPath<UIStyle>(StylePath);
        if (style == null)
        {
            throw new InvalidOperationException($"UI Style is missing at {StylePath}.");
        }

        Preflight(style);
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/UI", "Hearthline");
        }

        var artwork = new Dictionary<string, Sprite>
        {
            { "hearthmark", Draw("Hearthmark", Hearthmark) },
            { "carvedCorner", Draw("CarvedCorner", Corner) },
            { "exitMark", Draw("ExitMark", Exit) },
            { "questMark", Draw("QuestMark", Quest) },
            { "companionMark", Draw("CompanionMark", Companion) },
            { "eventMark", Draw("EventMark", Event) },
            { "rumourMark", Draw("RumourMark", Rumour) },
        };

        Undo.RecordObject(style, "Install Hearthline artwork");
        var values = new SerializedObject(style);
        foreach (KeyValuePair<string, Sprite> entry in artwork)
        {
            values.FindProperty(entry.Key).objectReferenceValue = entry.Value;
        }

        values.ApplyModifiedProperties();
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        ApplyPrompt(style);
        EditScene(MenuPath, scene => ApplyMenu(scene, style));
        Debug.Log("Hearthline artwork installed on the UI style, main menu and player prompt.");
    }

    private static void Preflight(UIStyle style)
    {
        if (EditorUtility.IsDirty(style))
        {
            throw new InvalidOperationException("UI Style has unsaved changes; save or resolve them before regenerating its artwork.");
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.isDirty)
            {
                throw new InvalidOperationException($"{scene.path} has unsaved changes; it was not saved or changed.");
            }
        }

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PlayerPath)
        {
            throw new InvalidOperationException("Close the player prefab stage before updating its interaction prompt.");
        }
    }

    private static void ApplyPrompt(UIStyle style)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            WorldInteractionIndicator prompt = root.GetComponentInChildren<WorldInteractionIndicator>(true);
            Transform slate = prompt != null ? prompt.transform.Find("Slate") : null;
            var data = prompt != null ? new SerializedObject(prompt) : null;
            var title = data != null ? data.FindProperty("title").objectReferenceValue as TMP_Text : null;
            var status = data != null ? data.FindProperty("status").objectReferenceValue as TMP_Text : null;
            if (slate == null || !slate.TryGetComponent(out Image plate) || title == null || status == null)
            {
                throw new InvalidOperationException("The player prompt is missing its slate, title or status.");
            }

            Color background = style.Panel;
            background.a = 0.96f;
            plate.color = background;
            title.color = style.Text;
            title.fontSize = 23f;
            status.fontSize = 18f;
            GiveLineRoom(title);
            GiveLineRoom(status);
            if (PrefabUtility.SaveAsPrefabAsset(root, PlayerPath) == null)
            {
                throw new InvalidOperationException("The player prompt prefab could not be saved.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void GiveLineRoom(TMP_Text text)
    {
        float line = text.fontSize * text.font.faceInfo.lineHeight / text.font.faceInfo.pointSize;
        RectTransform rect = text.rectTransform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(rect.sizeDelta.y, Mathf.Ceil(line) + 4f));
    }

    private static void EditScene(string path, Action<Scene> edit)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (wasOpen && scene.isDirty)
        {
            throw new InvalidOperationException($"{path} has unsaved changes; it was not saved or changed.");
        }

        if (!wasOpen)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        try
        {
            edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Could not save {path}.");
            }
        }
        finally
        {
            if (!wasOpen && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
            }
        }
    }

    private static void ApplyMenu(Scene scene, UIStyle style)
    {
        GameObject menu = Array.Find(scene.GetRootGameObjects(), root => root.name == "Main Menu");
        Transform mark = menu != null ? menu.transform.Find("Folktale Content/Story Mark") : null;
        if (mark == null || !mark.TryGetComponent(out Image image))
        {
            throw new InvalidOperationException("The main menu's Story Mark was not found.");
        }

        var rect = (RectTransform)mark;
        Undo.RecordObject(image, "Set the Hearthline mark");
        Undo.RecordObject(rect, "Set the Hearthline mark");
        image.sprite = style.Hearthmark;
        image.color = style.Accent;
        image.preserveAspect = true;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = Vector2.one * 36f;
        rect.anchoredPosition = new Vector2(348f, -309f);
    }

    private static Sprite Draw(string name, Action<Ink> compose)
    {
        var drawing = new Ink();
        compose(drawing);
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels32(drawing.Pixels());
        texture.Apply();
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new InvalidOperationException($"Could not import {path} as a sprite.");
        }

        return sprite;
    }

    private static Vector2 P(float x, float y) => new Vector2(x, y);

    private static void Hearthmark(Ink ink)
    {
        ink.Line(0.046f, P(0.18f, 0.15f), P(0.18f, 0.63f), P(0.23f, 0.77f), P(0.35f, 0.87f),
            P(0.5f, 0.91f), P(0.65f, 0.87f), P(0.77f, 0.77f), P(0.82f, 0.63f), P(0.82f, 0.15f));
        ink.Line(0.026f, P(0.28f, 0.17f), P(0.28f, 0.61f), P(0.33f, 0.73f), P(0.42f, 0.79f),
            P(0.5f, 0.81f), P(0.58f, 0.79f), P(0.67f, 0.73f), P(0.72f, 0.61f), P(0.72f, 0.17f));
        ink.Line(0.04f, P(0.12f, 0.13f), P(0.88f, 0.13f));
        ink.Line(0.042f, P(0.5f, 0.21f), P(0.41f, 0.25f), P(0.39f, 0.34f), P(0.43f, 0.43f),
            P(0.5f, 0.55f), P(0.49f, 0.43f), P(0.56f, 0.47f), P(0.61f, 0.38f), P(0.59f, 0.27f), P(0.5f, 0.21f));
        ink.Line(0.025f, P(0.45f, 0.23f), P(0.5f, 0.33f), P(0.55f, 0.23f));
        ink.Line(0.025f, P(0.14f, 0.53f), P(0.22f, 0.56f));
        ink.Line(0.025f, P(0.78f, 0.56f), P(0.86f, 0.53f));
    }

    private static void Corner(Ink ink)
    {
        ink.Line(0.035f, P(0.10f, 0.12f), P(0.10f, 0.76f), P(0.16f, 0.90f), P(0.88f, 0.90f));
        ink.Line(0.024f, P(0.19f, 0.25f), P(0.19f, 0.69f), P(0.27f, 0.80f), P(0.72f, 0.80f));
        ink.Line(0.035f, P(0.11f, 0.68f), P(0.24f, 0.81f), P(0.37f, 0.91f));
        ink.Line(0.035f, P(0.12f, 0.82f), P(0.23f, 0.70f), P(0.35f, 0.82f), P(0.23f, 0.93f));
        ink.Line(0.025f, P(0.46f, 0.82f), P(0.53f, 0.90f));
    }

    private static void Exit(Ink ink)
    {
        ink.Line(0.09f, P(0.20f, 0.15f), P(0.20f, 0.62f), P(0.27f, 0.79f), P(0.5f, 0.90f),
            P(0.73f, 0.79f), P(0.80f, 0.62f), P(0.80f, 0.15f));
        ink.Line(0.09f, P(0.34f, 0.42f), P(0.65f, 0.42f));
        ink.Line(0.09f, P(0.53f, 0.54f), P(0.65f, 0.42f), P(0.53f, 0.30f));
    }

    private static void Quest(Ink ink)
    {
        ink.Line(0.085f, P(0.5f, 0.92f), P(0.86f, 0.5f), P(0.5f, 0.08f), P(0.14f, 0.5f), P(0.5f, 0.92f));
        ink.Line(0.10f, P(0.5f, 0.65f), P(0.5f, 0.44f));
        ink.Dot(0.5f, 0.30f, 0.065f);
    }

    private static void Companion(Ink ink)
    {
        ink.Circle(P(0.5f, 0.68f), 0.17f, 0.085f);
        ink.Line(0.095f, P(0.18f, 0.16f), P(0.20f, 0.29f), P(0.30f, 0.40f), P(0.5f, 0.44f),
            P(0.70f, 0.40f), P(0.80f, 0.29f), P(0.82f, 0.16f));
        ink.Line(0.065f, P(0.26f, 0.16f), P(0.74f, 0.16f));
    }

    private static void Event(Ink ink)
    {
        ink.Line(0.075f, P(0.5f, 0.92f), P(0.61f, 0.61f), P(0.92f, 0.5f), P(0.61f, 0.39f),
            P(0.5f, 0.08f), P(0.39f, 0.39f), P(0.08f, 0.5f), P(0.39f, 0.61f), P(0.5f, 0.92f));
        ink.Dot(0.5f, 0.5f, 0.065f);
    }

    private static void Rumour(Ink ink)
    {
        for (int i = 0; i < 4; i++)
        {
            ink.Arc(P(0.5f, 0.5f), 0.34f, 0.085f, i * 90f + 8f, i * 90f + 82f);
        }

        ink.Dot(0.5f, 0.5f, 0.055f);
    }

    private readonly struct Stroke
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly float RadiusSquared;

        public Stroke(Vector2 start, Vector2 end, float radius)
        {
            Start = start;
            End = end;
            RadiusSquared = radius * radius;
        }
    }

    private sealed class Ink
    {
        private readonly List<Stroke> strokes = new List<Stroke>();

        public void Line(float width, params Vector2[] points)
        {
            for (int i = 1; i < points.Length; i++)
            {
                strokes.Add(new Stroke(points[i - 1], points[i], width * 0.5f));
            }
        }

        public void Dot(float x, float y, float radius) => strokes.Add(new Stroke(P(x, y), P(x, y), radius));

        public void Circle(Vector2 centre, float radius, float width) => Arc(centre, radius, width, 0f, 360f);

        public void Arc(Vector2 centre, float radius, float width, float from, float to)
        {
            const int Steps = 24;
            Vector2 previous = centre + Direction(from) * radius;
            for (int i = 1; i <= Steps; i++)
            {
                Vector2 next = centre + Direction(Mathf.Lerp(from, to, i / (float)Steps)) * radius;
                strokes.Add(new Stroke(previous, next, width * 0.5f));
                previous = next;
            }
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public Color32[] Pixels()
        {
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            Vector2 point = P((x + 0.25f + sx * 0.5f) / Size, (y + 0.25f + sy * 0.5f) / Size);
                            if (Contains(point))
                            {
                                covered++;
                            }
                        }
                    }

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 4));
                }
            }

            return pixels;
        }

        private bool Contains(Vector2 point)
        {
            foreach (Stroke stroke in strokes)
            {
                Vector2 delta = stroke.End - stroke.Start;
                float length = delta.sqrMagnitude;
                float along = length > 0f ? Mathf.Clamp01(Vector2.Dot(point - stroke.Start, delta) / length) : 0f;
                if ((point - (stroke.Start + delta * along)).sqrMagnitude <= stroke.RadiusSquared)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
