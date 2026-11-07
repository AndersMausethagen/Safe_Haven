using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The experience bar along the bottom of the screen: the skill trained last, its level and how far it is to the next
/// one, "+12 Woodcutting" rising above it as experience comes in (amounts close together for the same skill add up), and
/// a reminder (N) while Insight waits to be put into a skill or a perk to be chosen. Built in code for every scene with a
/// player (ExperienceTracker); it only reads GameSession.Progress.
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
    private ProficiencyDefinition gainedSkill;
    private ProficiencyDefinition writtenSkill;
    private int writtenLevel = -1;
    private int writtenPoints = -1;
    private int writtenKeys = -1;
    private int writtenGain = -1;

    // The skill the bar follows: the one trained last, kept across scenes.
    private static ProficiencyDefinition shownSkill;

    public static ExperienceHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        shownSkill = null;
    }

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

        hud.level = Label("Level", bar, new Vector2(1f, 0.5f), new Vector2(260f, 24f), new Vector2(136f, 0f), 17f, TextAlignmentOptions.Left);
        hud.gain = Label("Gain", bar, new Vector2(0.5f, 1f), new Vector2(320f, 28f), new Vector2(0f, 18f), 19f, TextAlignmentOptions.Center);
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

    private void OnGained(ProficiencyDefinition skill, int amount)
    {
        bool same = skill == gainedSkill && Time.unscaledTime - gainAt < GainSeconds * 0.6f;
        shownGain = same ? shownGain + amount : amount;
        gainedSkill = skill;
        gainAt = Time.unscaledTime;
        writtenGain = -1;
        if (skill != null)
        {
            shownSkill = skill;
        }
    }

    private void LateUpdate()
    {
        group.alpha = GamePause.IsPaused ? 0f : 1f;
        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int needed = 0;
        int towards = shownSkill != null ? progress.TowardsNext(shownSkill, settings, out needed) : 0;
        float share = shownSkill == null ? 0f : needed <= 0 ? 1f : towards / (float)needed;
        fill.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
        int skillLevel = shownSkill != null ? progress.LevelOf(shownSkill, settings) : 0;
        // Something to do in the skills window: a perk to choose, or Insight to put into a skill.
        int points = progress.PendingChoices(settings) + (progress.Insight > 0 ? 1 : 0);
        if (shownSkill != writtenSkill || skillLevel != writtenLevel || points != writtenPoints || InputRebinds.Changes != writtenKeys)
        {
            writtenSkill = shownSkill;
            writtenLevel = skillLevel;
            writtenPoints = points;
            writtenKeys = InputRebinds.Changes;
            string name = shownSkill != null ? $"{shownSkill.DisplayName} {skillLevel}" : "Skills";
            level.text = points > 0 ? GameInput.WithRebinds($"{name}  <color={UIStyle.Current.GoodHex}>(N)</color>") : name;
        }

        float age = Time.unscaledTime - gainAt;
        gain.gameObject.SetActive(age < GainSeconds);
        if (age < GainSeconds)
        {
            if (shownGain != writtenGain)
            {
                writtenGain = shownGain;
                gain.text = $"+{shownGain} {(gainedSkill != null ? gainedSkill.DisplayName : "Insight")}";
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
