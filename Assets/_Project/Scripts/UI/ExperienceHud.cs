using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The experience bar along the bottom of the screen: the level, how far it is to the next one, and "+12 XP" rising
/// above it as experience comes in (amounts close together add up). Built in code for every scene with a player
/// (ExperienceTracker); it only reads GameSession.Progress.
/// </summary>
public sealed class ExperienceHud : MonoBehaviour
{
    private const float GainSeconds = 1.6f;
    private static Color Gold => UIStyle.Current.Accent;

    private RectTransform fill;
    private TMP_Text level;
    private TMP_Text gain;
    private CanvasGroup group;
    private int shownGain;
    private float gainAt = -10f;
    // What the texts say now: written again only when it changes (no new strings every frame).
    private int writtenLevel = -1;
    private int writtenPoints = -1;
    private int writtenKeys = -1;
    private int writtenGain = -1;

    public static ExperienceHud Current { get; private set; }

    public static ExperienceHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Experience HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var hud = root.AddComponent<ExperienceHud>();
        hud.group = root.AddComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false;

        RectTransform bar = Rect("Bar", root.transform, new Vector2(0.5f, 0f), new Vector2(560f, 8f), new Vector2(0f, 8f));
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        hud.fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        hud.fill.anchorMin = new Vector2(0f, 0f);
        hud.fill.anchorMax = new Vector2(0f, 1f);
        hud.fill.pivot = new Vector2(0f, 0.5f);
        hud.fill.offsetMin = hud.fill.offsetMax = Vector2.zero;
        hud.fill.gameObject.AddComponent<Image>().color = Gold;

        hud.level = Label("Level", bar, new Vector2(1f, 0.5f), new Vector2(120f, 24f), new Vector2(66f, 0f), 17f, TextAlignmentOptions.Left);
        hud.gain = Label("Gain", bar, new Vector2(0.5f, 1f), new Vector2(240f, 28f), new Vector2(0f, 18f), 19f, TextAlignmentOptions.Center);
        ExperienceTracker.Gained += hud.OnGained;
        return hud;
    }

    private void OnEnable() => Current = this;

    private void OnDestroy()
    {
        ExperienceTracker.Gained -= OnGained;
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnGained(int amount)
    {
        shownGain = Time.unscaledTime - gainAt < GainSeconds * 0.6f ? shownGain + amount : amount;
        gainAt = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        group.alpha = GamePause.IsPaused ? 0f : 1f;
        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        float share = progress.IsMaxLevel(settings) ? 1f : progress.Experience / (float)settings.ExperienceToNext(progress.Level);
        fill.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
        int points = progress.UnspentPoints(settings);
        if (progress.Level != writtenLevel || points != writtenPoints || InputRebinds.Changes != writtenKeys)
        {
            writtenLevel = progress.Level;
            writtenPoints = points;
            writtenKeys = InputRebinds.Changes;
            level.text = points > 0 ? GameInput.WithRebinds($"Lv {progress.Level}  <color=#8BE08B>+{points} (N)</color>") : $"Lv {progress.Level}";
        }

        float age = Time.unscaledTime - gainAt;
        gain.gameObject.SetActive(age < GainSeconds);
        if (age < GainSeconds)
        {
            if (shownGain != writtenGain)
            {
                writtenGain = shownGain;
                gain.text = $"+{shownGain} XP";
            }

            gain.alpha = 1f - Mathf.Clamp01((age - GainSeconds * 0.5f) / (GainSeconds * 0.5f));
            gain.rectTransform.anchoredPosition = new Vector2(0f, 18f + age * 14f);
        }
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

    private static TMP_Text Label(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position, float fontSize, TextAlignmentOptions alignment)
    {
        var text = Rect(name, parent, anchorAt, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        UIStyle.Current.OnWorld(text);
        text.fontSize = fontSize;
        text.color = Gold;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
