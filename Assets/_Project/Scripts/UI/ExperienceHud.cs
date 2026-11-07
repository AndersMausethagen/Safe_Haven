using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The experience bar along the bottom of the screen: the skill trained last, its level and how far it is to the next
/// one, "+12 Woodcutting" rising above it as experience comes in (amounts close together for the same skill add up), and
/// a reminder (N) while Insight waits to be put into a skill or a perk to be chosen; beside it the next milestone and, on a
/// raid, the bonus experience waiting for the way home. A level reached shows a banner high in the middle of the screen,
/// held longer for a milestone. Built in code for every scene with a player (ExperienceTracker); it only reads
/// GameSession.Progress.
/// </summary>
public sealed class ExperienceHud : MonoBehaviour
{
    private const float GainSeconds = 1.6f;
    private const float BannerSeconds = 2.6f;
    private const float MilestoneBannerSeconds = 5f;
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
    private int writtenHeld = -1;
    private TMP_Text bannerTitle;
    private TMP_Text bannerLine;
    private CanvasGroup banner;
    private float bannerAt = -100f;
    private float bannerFor;

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

        // Above the bar's right end, reading leftwards, so a long line never runs off the screen.
        hud.level = Label("Level", bar, new Vector2(1f, 1f), new Vector2(560f, 24f), Vector2.zero, 17f, TextAlignmentOptions.BottomRight);
        hud.level.rectTransform.pivot = new Vector2(1f, 0f);
        hud.level.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        hud.gain = Label("Gain", bar, new Vector2(0.5f, 1f), new Vector2(320f, 28f), new Vector2(0f, 44f), 19f, TextAlignmentOptions.Center);

        // The banner: type on nothing, like the rest of the HUD, between two thin lines.
        RectTransform bannerRoot = Rect("Level Banner", root.transform, new Vector2(0.5f, 0.74f), new Vector2(900f, 110f), Vector2.zero);
        hud.banner = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        hud.banner.alpha = 0f;
        hud.bannerTitle = Label("Title", bannerRoot, new Vector2(0.5f, 0.62f), new Vector2(900f, 52f), Vector2.zero, 40f, TextAlignmentOptions.Center);
        hud.bannerTitle.characterSpacing = 6f;
        hud.bannerLine = Label("Line", bannerRoot, new Vector2(0.5f, 0.2f), new Vector2(900f, 30f), Vector2.zero, 20f, TextAlignmentOptions.Center);
        hud.bannerLine.color = UIStyle.Current.Text;
        foreach (float y in new[] { 1f, 0f })
        {
            Image line = Rect("Rule", bannerRoot, new Vector2(0.5f, y), new Vector2(420f, 1f), Vector2.zero).gameObject.AddComponent<Image>();
            line.color = UIStyle.Current.Accent;
            line.raycastTarget = false;
        }

        ExperienceTracker.Gained += hud.OnGained;
        ExperienceTracker.LevelReached += hud.OnLevelReached;
        return hud;
    }

    private void OnEnable() => Current = this;

    private void OnDestroy()
    {
        ExperienceTracker.Gained -= OnGained;
        ExperienceTracker.LevelReached -= OnLevelReached;
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

    private void OnLevelReached(ProficiencyDefinition skill, int reached, bool milestone)
    {
        bannerTitle.text = $"{skill.DisplayName.ToUpperInvariant()}  {reached}";
        bannerLine.text = milestone
            ? GameInput.WithRebinds($"A milestone: <color={UIStyle.Current.GoodHex}>a perk to choose (N)</color>")
            : NextMilestone(skill, reached) is int next ? $"Next milestone at {next}" : "Mastered";
        bannerAt = Time.unscaledTime;
        bannerFor = milestone ? MilestoneBannerSeconds : BannerSeconds;
    }

    private static int? NextMilestone(ProficiencyDefinition skill, int level)
    {
        foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
        {
            if (milestone.level > level)
            {
                return milestone.level;
            }
        }

        return null;
    }

    private void LateUpdate()
    {
        float shown = Time.unscaledTime - bannerAt;
        banner.alpha = shown >= bannerFor ? 0f : Mathf.Clamp01(shown / 0.25f) * Mathf.Clamp01((bannerFor - shown) / 0.6f);

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
        int held = ExperienceTracker.HeldBonus;
        if (shownSkill != writtenSkill || skillLevel != writtenLevel || points != writtenPoints || InputRebinds.Changes != writtenKeys
            || held != writtenHeld)
        {
            writtenSkill = shownSkill;
            writtenLevel = skillLevel;
            writtenPoints = points;
            writtenKeys = InputRebinds.Changes;
            writtenHeld = held;
            string muted = UIStyle.Current.MutedHex;
            string name = shownSkill != null ? $"{shownSkill.DisplayName} {skillLevel}" : "Skills";
            if (shownSkill != null && NextMilestone(shownSkill, skillLevel) is int next)
            {
                name += $"  <color={muted}>perk at {next}</color>";
            }

            if (held > 0)
            {
                name += $"  <color={muted}>+{held} if you get out</color>";
            }

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
            gain.rectTransform.anchoredPosition = new Vector2(0f, 44f + age * 14f);
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
