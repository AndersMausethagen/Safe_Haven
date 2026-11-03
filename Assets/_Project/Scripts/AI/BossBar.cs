using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The boss's health along the bottom of the screen, frameless as the rest of the HUD: its name, a thin bar and a number, so it doesn't rest on a colour.</summary>
public sealed class BossBar : MonoBehaviour
{
    private const float Width = 780f;
    private const float Height = 7f;
    private const float FromBottom = 150f;

    private RectTransform fill;
    private TMP_Text number;

    public static BossBar Show(BossDefinition boss)
    {
        var root = new GameObject("Boss Bar");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        BossBar bar = root.AddComponent<BossBar>();
        bar.Build(boss);
        return bar;
    }

    private void Build(BossDefinition boss)
    {
        UIStyle style = UIStyle.Current;
        RectTransform track = Rect("Track", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FromBottom), new Vector2(Width, Height));
        track.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        fill = Rect("Fill", track, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(Width, 0f));
        fill.pivot = new Vector2(0f, 0.5f);
        fill.gameObject.AddComponent<Image>().color = new Color(0.82f, 0.28f, 0.2f);
        Label(boss.DisplayName, track, TextAlignmentOptions.BottomLeft, 34f, new Vector2(Width, 44f), new Vector2(0f, 8f), true, style);
        if (!string.IsNullOrWhiteSpace(boss.Title))
        {
            Label(boss.Title, track, TextAlignmentOptions.Bottom, 19f, new Vector2(Width, 30f), new Vector2(0f, 8f), false, style);
        }

        number = Label("", track, TextAlignmentOptions.BottomRight, 22f, new Vector2(Width, 30f), new Vector2(0f, 8f), false, style);
    }

    public void Set(float share)
    {
        fill.sizeDelta = new Vector2(Width * Mathf.Clamp01(share), 0f);
        number.text = $"{Mathf.CeilToInt(share * 100f)}%";
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 at, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Label(string text, Transform parent, TextAlignmentOptions alignment, float size, Vector2 box, Vector2 lift, bool title, UIStyle style)
    {
        RectTransform rect = Rect("Label", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), lift, box);
        rect.pivot = new Vector2(0.5f, 0f);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        style.OnWorld(label, title);
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }
}
