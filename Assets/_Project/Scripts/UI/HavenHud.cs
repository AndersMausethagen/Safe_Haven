using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Safe Haven's standing at the top of the screen while at home: its rank, renown towards the next rank on a thin bar,
/// "+6 Renown" rising when something is built, upgraded or cleared, and a banner across the screen when a new rank is
/// reached. Built in code by HavenProgress; it only reads it and GameSession.Settlement.
/// </summary>
public sealed class HavenHud : MonoBehaviour
{
    private static Color Gold => UIStyle.Current.Accent;
    private static Color Body => UIStyle.Current.Text;
    private const float GainSeconds = 2f;
    private const float BannerSeconds = 5f;
    private const float BadgeWidth = 390f;
    private const float GainY = -14f;
    private const float StandingSeconds = 0.5f;

    private TMP_Text badge;
    private TMP_Text standing;
    private RectTransform fill;
    private TMP_Text gain;
    private CanvasGroup banner;
    private TMP_Text bannerTitle;
    private TMP_Text bannerLine;
    private CanvasGroup group;
    private float gainAt = -10f;
    private float bannerAt = -10f;
    private float nextStanding;
    private int shownGain;

    public static HavenHud Current { get; private set; }

    public static HavenHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Haven HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var hud = root.AddComponent<HavenHud>();
        hud.group = root.AddComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false;
        hud.Build(root.transform);
        return hud;
    }

    private void Build(Transform root)
    {
        // Top left, under the vitals' margin: the day and weather panel has the top centre.
        RectTransform top = Rect("Badge", root, new Vector2(0f, 1f), new Vector2(BadgeWidth, 52f), new Vector2(30f + BadgeWidth * 0.5f, -40f));
        const float markSpace = 38f;
        HearthlineArt.AddIcon(top, "Hearthmark", UIStyle.Current.Hearthmark, new Vector2(0f, 1f), Vector2.one * 30f,
            new Vector2(15f, -18f), Gold, true);
        badge = Label(top, "Rank", new Vector2(0f, 1f), new Vector2(BadgeWidth - markSpace, 32f),
            new Vector2((BadgeWidth + markSpace) * 0.5f, -18f), 24f, Gold, TextAlignmentOptions.Left, true);
        standing = Label(top, "Standing", new Vector2(1f, 1f), new Vector2(BadgeWidth, 32f), new Vector2(BadgeWidth * -0.5f, -20f), 17f, Body, TextAlignmentOptions.Right);
        RectTransform bar = Rect("Bar", top, new Vector2(0.5f, 0f), new Vector2(BadgeWidth, 2f), new Vector2(0f, 12f));
        bar.gameObject.AddComponent<Image>().color = UIStyle.Current.Track;
        fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = Gold;
        gain = Label(top, "Gain", new Vector2(0f, 0f), new Vector2(BadgeWidth, 30f), new Vector2(BadgeWidth * 0.5f, GainY), 20f, UIStyle.Current.Good, TextAlignmentOptions.Left);

        RectTransform plate = Rect("Rank Banner", root, new Vector2(0.5f, 0.5f), new Vector2(1100f, 190f), new Vector2(0f, 190f));
        banner = plate.gameObject.AddComponent<CanvasGroup>();
        banner.alpha = 0f;
        HearthlineArt.AddIcon(plate, "Hearthmark", UIStyle.Current.Hearthmark, new Vector2(0.5f, 1f), Vector2.one * 34f,
            Vector2.zero, Gold, true);
        TMP_Text kicker = Label(plate, "Kicker", new Vector2(0.5f, 1f), new Vector2(1000f, 30f), new Vector2(0f, -30f), 20f, Body);
        kicker.text = "SAFE HAVEN HAS GROWN";
        kicker.characterSpacing = 12f;
        bannerTitle = Label(plate, "Title", new Vector2(0.5f, 0.5f), new Vector2(1000f, 76f), new Vector2(0f, 8f), 60f, Gold, TextAlignmentOptions.Center, true);
        bannerLine = Label(plate, "Line", new Vector2(0.5f, 0f), new Vector2(1000f, 34f), new Vector2(0f, 34f), 24f, Body);
        foreach (float y in new[] { 0f, 1f })
        {
            RectTransform line = Rect("Line", plate, new Vector2(0.5f, y), new Vector2(460f, 1f), Vector2.zero);
            line.gameObject.AddComponent<Image>().color = Gold;
        }
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public void ShowGain(int amount)
    {
        shownGain = Time.unscaledTime - gainAt < GainSeconds * 0.6f ? shownGain + amount : amount;
        gainAt = Time.unscaledTime;
    }

    public void ShowRank(string name, string line)
    {
        bannerTitle.text = name;
        bannerLine.text = line;
        bannerAt = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        bool windows = UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen;
        group.alpha = GamePause.IsPaused ? 0f : 1f;
        badge.transform.parent.gameObject.SetActive(!windows);
        if (Time.unscaledTime >= nextStanding)
        {
            DrawStanding();
        }

        float age = Time.unscaledTime - gainAt;
        gain.gameObject.SetActive(age < GainSeconds);
        if (age < GainSeconds)
        {
            gain.text = $"+{shownGain} Renown";
            gain.alpha = 1f - Mathf.Clamp01((age - GainSeconds * 0.5f) / (GainSeconds * 0.5f));
            gain.rectTransform.anchoredPosition = new Vector2(BadgeWidth * 0.5f, GainY - age * 10f);
        }

        float shown = Time.unscaledTime - bannerAt;
        banner.alpha = shown < 0.4f ? shown / 0.4f : shown < BannerSeconds - 1f ? 1f : Mathf.Clamp01(BannerSeconds - shown);
        float pop = shown < 0.4f ? Mathf.Lerp(1.15f, 1f, shown / 0.4f) : 1f;
        banner.transform.localScale = Vector3.one * pop;
    }

    // Not every frame: renown is only counted every two seconds, and the key's label walks its bindings.
    private void DrawStanding()
    {
        nextStanding = Time.unscaledTime + StandingSeconds;
        HavenSettings settings = HavenSettings.Current;
        int renown = HavenProgress.Current != null ? HavenProgress.Current.Renown : 0;
        int rank = Mathf.Max(GameSession.Settlement.HavenRank, settings.RankFor(renown));
        bool last = rank >= settings.Ranks.Count - 1;
        int from = settings.Ranks.Count > 0 ? settings.Ranks[rank].renown : 0;
        int to = last ? from : settings.Ranks[rank + 1].renown;
        badge.text = settings.Ranks.Count == 0 ? "" : settings.Ranks[rank].name;
        string key = GameInput.KeyLabel(GameInput.Haven);
        standing.text = last ? $"{renown} renown  <alpha=#99>{key}" : $"{renown} / {to} renown  <alpha=#99>{key}";
        fill.anchorMax = new Vector2(last ? 1f : Mathf.Clamp01((renown - from) / (float)Mathf.Max(1, to - from)), 1f);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Label(Transform parent, string name, Vector2 anchorAt, Vector2 size, Vector2 position, float fontSize, Color colour,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool title = false)
    {
        var text = Rect(name, parent, anchorAt, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        UIStyle.Current.OnWorld(text, title);
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
