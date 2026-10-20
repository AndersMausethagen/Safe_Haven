using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The gathering minigame's bar: a small vertical bar right of the screen centre with the yellow zone, the critical zone
/// and the moving marker, a swing counter, and a flash of each swing's result. Built in code under the scene's UIRoot by
/// <see cref="Attach"/> (PlayerGathering calls it), shown only while a gather runs.
/// </summary>
public sealed class GatheringBarView : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color NormalColor = new Color(0.95f, 0.8f, 0.2f, 0.9f);
    private static readonly Color CriticalColor = new Color(0.3f, 1f, 0.4f, 1f);
    private static readonly Color GlancingColor = new Color(1f, 0.35f, 0.3f, 1f);
    private const float FlashSeconds = 0.6f;

    private PlayerGathering gathering;
    private GameObject panel;
    private RectTransform normalZone;
    private RectTransform criticalZone;
    private RectTransform marker;
    private TMP_Text counter;
    private TMP_Text flash;
    private float flashUntil;

    public static GatheringBarView Attach(PlayerGathering gathering)
    {
        if (gathering == null || UIRoot.Current == null)
        {
            return null;
        }

        var root = new GameObject("Gathering Bar", typeof(RectTransform));
        root.transform.SetParent(UIRoot.Current.Canvas.transform, false);
        Stretch((RectTransform)root.transform);
        root.AddComponent<Canvas>();
        GatheringBarView view = root.AddComponent<GatheringBarView>();
        view.Build(gathering);
        return view;
    }

    private void OnDestroy()
    {
        if (gathering != null)
        {
            gathering.SwingResolved -= OnSwing;
        }
    }

    private void Update()
    {
        GatheringSession session = gathering != null ? gathering.Session : null;
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
        string combo = session.Combo > 1 ? $"  x{session.Combo}" : "";
        counter.text = $"{Mathf.Min(session.SwingsDone + 1, session.SwingsRequired)}/{session.SwingsRequired}{combo}";
    }

    private void OnSwing(SwingResult result)
    {
        flashUntil = Time.unscaledTime + FlashSeconds;
        switch (result)
        {
            case SwingResult.Critical:
                flash.text = "Critical!";
                flash.color = CriticalColor;
                break;
            case SwingResult.Normal:
                flash.text = "Good";
                flash.color = NormalColor;
                break;
            default:
                flash.text = "Glancing";
                flash.color = GlancingColor;
                break;
        }
    }

    private void Build(PlayerGathering owner)
    {
        gathering = owner;
        gathering.SwingResolved += OnSwing;

        panel = new GameObject("Panel", typeof(RectTransform));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(36f, 220f);
        panelRect.anchoredPosition = new Vector2(150f, 20f);

        RectTransform bar = Box("Bar", panelRect, BackgroundColor);
        Stretch(bar);
        normalZone = Box("Normal Zone", bar, NormalColor);
        criticalZone = Box("Critical Zone", bar, CriticalColor);
        marker = Box("Marker", bar, Color.white);
        marker.sizeDelta = new Vector2(10f, 4f);

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
