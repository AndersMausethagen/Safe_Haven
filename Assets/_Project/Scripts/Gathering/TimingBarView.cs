using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bar of a timing minigame (gathering, lockpicking): a small vertical bar right of the screen centre with the yellow
/// zone, the critical zone and the moving marker, a counter, and a flash of each press's result. Built in code under the
/// scene's UIRoot by <see cref="Attach"/>, shown only while its source has something running.
/// </summary>
public sealed class TimingBarView : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0.075f, 0.066f, 0.055f, 0.82f);
    private static Color NormalColor => UIStyle.Current.Accent;
    private static Color CriticalColor => UIStyle.Current.Good;
    private static Color GlancingColor => UIStyle.Current.Danger;
    private const float FlashSeconds = 0.6f;

    private Func<ITimingBar> source;
    private GameObject panel;
    private RectTransform normalZone;
    private RectTransform criticalZone;
    private RectTransform marker;
    private TMP_Text counter;
    private TMP_Text flash;
    private float flashUntil;

    /// <param name="source">The minigame running now, or null.</param>
    public static TimingBarView Attach(Func<ITimingBar> source)
    {
        if (source == null || UIRoot.Current == null)
        {
            return null;
        }

        var root = new GameObject("Timing Bar", typeof(RectTransform));
        root.transform.SetParent(UIRoot.Current.Canvas.transform, false);
        Stretch((RectTransform)root.transform);
        root.AddComponent<Canvas>();
        TimingBarView view = root.AddComponent<TimingBarView>();
        view.source = source;
        view.Build();
        return view;
    }

    /// <summary>Shows how a press went above the bar, in the color of its zone.</summary>
    public void Flash(string text, SwingResult result)
    {
        flashUntil = Time.unscaledTime + FlashSeconds;
        flash.text = text;
        flash.color = result == SwingResult.Critical ? CriticalColor : result == SwingResult.Normal ? NormalColor : GlancingColor;
    }

    private void Update()
    {
        ITimingBar session = source();
        bool showFlash = Time.unscaledTime < flashUntil;
        panel.SetActive(session != null || showFlash);
        flash.gameObject.SetActive(showFlash);
        if (session == null)
        {
            return;
        }

        SetBand(normalZone, session.Zones.Center, session.Zones.NormalWidth);
        SetBand(criticalZone, session.Zones.Center, session.Zones.CriticalWidth);
        marker.anchorMin = new Vector2(0f, session.Position);
        marker.anchorMax = new Vector2(1f, session.Position);
        bool auto = GatheringMinigameSettings.Current.AutoGather;
        normalZone.gameObject.SetActive(!auto);
        criticalZone.gameObject.SetActive(!auto);
        marker.gameObject.SetActive(!auto);
        counter.text = session.Counter;
    }

    private void Build()
    {
        panel = new GameObject("Panel", typeof(RectTransform));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(14f, 220f);
        panelRect.anchoredPosition = new Vector2(150f, 20f);

        RectTransform bar = Box("Bar", panelRect, BackgroundColor);
        Stretch(bar);
        normalZone = Box("Normal Zone", bar, NormalColor);
        criticalZone = Box("Critical Zone", bar, CriticalColor);
        marker = Box("Marker", bar, Color.white);
        marker.sizeDelta = new Vector2(8f, 3f);

        counter = Label("Counter", panelRect, 22f);
        counter.rectTransform.anchorMin = counter.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        counter.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        flash = Label("Result", panelRect, 26f);
        flash.rectTransform.anchorMin = flash.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        flash.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        flash.fontStyle = FontStyles.Bold;

        panel.SetActive(false);
    }

    private static void SetBand(RectTransform band, float center, float width)
    {
        band.anchorMin = new Vector2(0f, Mathf.Clamp01(center - width * 0.5f));
        band.anchorMax = new Vector2(1f, Mathf.Clamp01(center + width * 0.5f));
        band.offsetMin = band.offsetMax = Vector2.zero;
    }

    private static RectTransform Box(string name, Transform parent, Color color)
    {
        var box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        Image image = box.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return (RectTransform)box.transform;
    }

    private static TMP_Text Label(string name, Transform parent, float size)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);
        var text = label.GetComponent<TextMeshProUGUI>();
        UIStyle.Current.OnWorld(text);
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(200f, 32f);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
