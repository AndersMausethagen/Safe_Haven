using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Adds a scene-bound, non-interactive environment HUD without changing authored sky settings.</summary>
public static class EnvironmentHudSetup
{
    [MenuItem("Safe Haven/UI/Add Environment HUD to Scene")]
    public static void AddFromMenu()
    {
        Selection.activeGameObject = AddToScene().gameObject;
    }

    public static EnvironmentHudView AddToScene()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Add the environment HUD outside Play mode.");
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        EnvironmentHudView existing = roots.SelectMany(r => r.GetComponentsInChildren<EnvironmentHudView>(true)).FirstOrDefault();
        if (existing != null)
        {
            return existing;
        }

        Canvas canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Screen Canvas");
        DayNightCycle cycle = roots.SelectMany(r => r.GetComponentsInChildren<DayNightCycle>(true)).FirstOrDefault();
        WeatherSystem weather = roots.SelectMany(r => r.GetComponentsInChildren<WeatherSystem>(true)).FirstOrDefault();
        if (canvas == null || cycle == null || cycle.Profile == null || weather == null)
        {
            throw new InvalidOperationException("The active scene needs Screen Canvas and a configured DayNightCycle and WeatherSystem first.");
        }

        RectTransform root = Rect("Environment HUD", canvas.transform, new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(536f, 92f));
        root.pivot = new Vector2(0.5f, 1f);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add Environment HUD");
        root.gameObject.SetActive(false);
        var background = root.gameObject.AddComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = false;
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        Color parchment = new Color(0.93f, 0.89f, 0.8f);
        Color gold = new Color(0.91f, 0.77f, 0.42f);
        var view = root.gameObject.AddComponent<EnvironmentHudView>();
        TMP_Text time = Text("Time", root, new Vector2(-84f, -20f), new Vector2(200f, 26f), 15f, gold);
        time.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform separator = Rect("Status Divider", root, new Vector2(0.5f, 1f),
            new Vector2(29f, -20f), new Vector2(1f, 14f));
        var divider = separator.gameObject.AddComponent<Image>();
        divider.color = new Color(gold.r, gold.g, gold.b, 0.4f);
        divider.raycastTarget = false;
        RectTransform iconRect = Rect("Weather Icon", root, new Vector2(0.5f, 1f),
            new Vector2(53f, -20f), new Vector2(26f, 26f));
        var icon = iconRect.gameObject.AddComponent<WeatherIconGraphic>();
        icon.color = parchment;
        icon.raycastTarget = false;
        TMP_Text weatherText = Text("Weather", root, new Vector2(146f, -20f), new Vector2(144f, 26f), 14f, parchment);
        weatherText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform track = Rect("Day Timeline", root, new Vector2(0.5f, 1f),
            new Vector2(0f, -44f), new Vector2(480f, 10f));
        var timeline = track.gameObject.AddComponent<EnvironmentTimelineGraphic>();
        timeline.color = parchment;
        timeline.raycastTarget = false;
        var labels = new TMP_Text[5];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = Text("Phase " + i, track, new Vector2(0f, -13f), new Vector2(58f, 32f), 10.5f, parchment);
            labels[i].rectTransform.pivot = new Vector2(0.5f, 1f);
            labels[i].alignment = TextAlignmentOptions.Top;
        }

        var serialized = new SerializedObject(view);
        serialized.FindProperty("cycle").objectReferenceValue = cycle;
        serialized.FindProperty("weather").objectReferenceValue = weather;
        serialized.FindProperty("timeline").objectReferenceValue = timeline;
        serialized.FindProperty("weatherIcon").objectReferenceValue = icon;
        serialized.FindProperty("timeLabel").objectReferenceValue = time;
        serialized.FindProperty("weatherLabel").objectReferenceValue = weatherText;
        SerializedProperty phases = serialized.FindProperty("phaseLabels");
        phases.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            phases.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        view.Refresh();
        root.gameObject.SetActive(true);
        // Keep ordinary windows above this informational strip.
        root.SetSiblingIndex(0);
        EditorSceneManager.MarkSceneDirty(scene);
        return view;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0.5f, 1f), position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
