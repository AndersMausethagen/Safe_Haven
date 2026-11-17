using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The boss's health along the bottom of the screen, frameless as the rest of the HUD: its name, a thin bar and a number, so it doesn't rest on a colour.
/// <para>
/// Marks on the bar show where each stage of its fight begins (Boss Definition › Phases), and a thinner amber strip under it shows its poise
/// (Docs/HeartrootBossReview_2026-11-17.md C2, C9): what turns parries and heavy blows into a stagger or a topple, made visible. The strip
/// pulses while its poise is worn low, as the body's reeling does (ReelingTell), and while it lies toppled it shows the topple running out in
/// the pale green of an opening. BossEncounter feeds it, on a friend's copy too (the copy's poise follows the host's).
/// </para>
/// </summary>
public sealed class BossBar : MonoBehaviour
{
    private const float Width = 780f;
    private const float Height = 7f;
    private const float FromBottom = 150f;
    private const float PoiseHeight = 3f;
    private const float PoiseGap = 3f;
    private const float MarkWidth = 2f;
    // Beats a second of the strip's pulse while its poise is worn low.
    private const float PulseRate = 2.5f;
    private static readonly Color PoiseColour = new Color(0.96f, 0.68f, 0.22f, 0.95f);
    private static readonly Color MarkColour = new Color(1f, 0.93f, 0.8f, 0.7f);

    /// <summary>How the poise strip is drawn.</summary>
    public enum PoiseLook
    {
        /// <summary>Its poise, in amber.</summary>
        Steady,
        /// <summary>Worn low (a stagger or topple is near): the amber pulses.</summary>
        Reeling,
        /// <summary>It lies toppled: the time left, in the opening's pale green.</summary>
        Toppled,
    }

    private RectTransform fill;
    private TMP_Text number;
    private string title;
    private string subtitle;
    private RectTransform poiseTrack;
    private RectTransform poiseFill;
    private Image poiseImage;
    private float poiseShown = -1f;

    public static BossBar Show(BossDefinition boss)
    {
        var root = new GameObject("Boss Bar");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        // The UI's own scaler, so "UI and Text Size" applies from the start, not only after it is changed in a session.
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        BossBar bar = root.AddComponent<BossBar>();
        bar.Build(boss);
        return bar;
    }

    private void Build(BossDefinition boss)
    {
        UIStyle style = UIStyle.Current;
        title = boss.DisplayName;
        subtitle = boss.Title;
        RectTransform track = Rect("Track", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FromBottom), new Vector2(Width, Height));
        track.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        fill = Rect("Fill", track, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(Width, 0f));
        fill.pivot = new Vector2(0f, 0.5f);
        fill.gameObject.AddComponent<Image>().color = new Color(0.82f, 0.28f, 0.2f);
        AddPhaseMarks(boss, track);
        Label(boss.DisplayName, track, TextAlignmentOptions.BottomLeft, 34f, new Vector2(Width, 44f), new Vector2(0f, 8f), true, style);
        if (!string.IsNullOrWhiteSpace(boss.Title))
        {
            Label(boss.Title, track, TextAlignmentOptions.Bottom, 19f, new Vector2(Width, 30f), new Vector2(0f, 8f), false, style);
        }

        number = Label("", track, TextAlignmentOptions.BottomRight, 22f, new Vector2(Width, 30f), new Vector2(0f, 8f), false, style);

        // Under the health bar, hidden until BossEncounter first feeds it (Poise Bars off, or a boss without poise, keeps it hidden).
        poiseTrack = Rect("Poise Track", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, FromBottom - Height * 0.5f - PoiseGap - PoiseHeight * 0.5f), new Vector2(Width, PoiseHeight));
        poiseTrack.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        poiseFill = Rect("Poise", poiseTrack, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(Width, 0f));
        poiseFill.pivot = new Vector2(0f, 0.5f);
        poiseImage = poiseFill.gameObject.AddComponent<Image>();
        poiseImage.color = PoiseColour;
        poiseTrack.gameObject.SetActive(false);
    }

    // A thin upright mark where each stage begins (66% and 33% for the Heartroot), over the fill, so the next change is in sight.
    private static void AddPhaseMarks(BossDefinition boss, RectTransform track)
    {
        BossPhase[] phases = boss.Phases;
        if (phases == null)
        {
            return;
        }

        foreach (BossPhase phase in phases)
        {
            if (phase == null || phase.belowHealth <= 0f || phase.belowHealth >= 1f)
            {
                continue;
            }

            var at = new Vector2(phase.belowHealth, 0.5f);
            RectTransform mark = Rect("Phase Mark", track, at, at, Vector2.zero, new Vector2(MarkWidth, Height + 6f));
            var image = mark.gameObject.AddComponent<Image>();
            image.color = MarkColour;
            image.raycastTarget = false;
        }
    }

    /// <summary>Its name across the middle of the screen for a moment, as it wakes.</summary>
    public void ShowCard(float seconds)
    {
        StartCoroutine(Card(seconds));
    }

    private System.Collections.IEnumerator Card(float seconds)
    {
        UIStyle style = UIStyle.Current;
        RectTransform box = Rect("Card", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1400f, 140f));
        var name = box.gameObject.AddComponent<TextMeshProUGUI>();
        style.OnWorld(name, true);
        name.text = $"{title}\n<size=38%>{subtitle}</size>";
        name.fontSize = 76f;
        name.alignment = TextAlignmentOptions.Center;
        name.raycastTarget = false;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float rise = Mathf.Clamp01(t / 0.5f);
            float fall = Mathf.Clamp01((seconds - t) / 0.7f);
            name.alpha = Mathf.Min(rise, fall);
            yield return null;
        }

        Destroy(box.gameObject);
    }

    public void Set(float share)
    {
        fill.sizeDelta = new Vector2(Width * Mathf.Clamp01(share), 0f);
        number.text = $"{Mathf.CeilToInt(share * 100f)}%";
    }

    /// <summary>
    /// The poise strip: <paramref name="share"/> of its poise (or, toppled, of the topple left), drawn as <paramref name="look"/> says.
    /// Called every frame: it only touches the UI when something changed, so the canvas isn't rebuilt for nothing.
    /// </summary>
    public void SetPoise(float share, PoiseLook look)
    {
        if (!poiseTrack.gameObject.activeSelf)
        {
            poiseTrack.gameObject.SetActive(true);
        }

        share = Mathf.Clamp01(share);
        if (Mathf.Abs(share - poiseShown) > 0.002f)
        {
            poiseShown = share;
            poiseFill.sizeDelta = new Vector2(Width * share, 0f);
        }

        Color colour = PoiseColour;
        if (look == PoiseLook.Toppled)
        {
            colour = CombatFeelSettings.Current.OpeningColor;
            colour.a = 1f;
        }
        else if (look == PoiseLook.Reeling)
        {
            colour.a *= Mathf.Lerp(0.35f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRate * Mathf.PI * 2f));
        }

        if (poiseImage.color != colour)
        {
            poiseImage.color = colour;
        }
    }

    /// <summary>Hides the poise strip (the Poise Bars option is off).</summary>
    public void HidePoise()
    {
        if (poiseTrack.gameObject.activeSelf)
        {
            poiseTrack.gameObject.SetActive(false);
        }
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
