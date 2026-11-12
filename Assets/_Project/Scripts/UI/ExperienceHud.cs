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

    private RectTransform bar;
    private RectTransform fill;
    private Image fillImage;
    private RectTransform bannerRect;
    private Image screenGlow;
    private float shownShare;
    private float lastMote = -10f;
    private float slowUntil = -10f;
    private bool slowing;
    private const float SlowSeconds = 0.9f;
    private ProficiencyDefinition easedSkill;
    private int easedLevel;
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

        // A milestone lights the edges of the screen for a moment: a wash of the accent, under everything else.
        hud.screenGlow = Rect("Milestone Glow", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        var glowRect = (RectTransform)hud.screenGlow.transform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = glowRect.offsetMax = Vector2.zero;
        hud.screenGlow.sprite = UISprites.Vignette;
        hud.screenGlow.color = new Color(1f, 1f, 1f, 0f);
        hud.screenGlow.raycastTarget = false;
        OnTop(hud.screenGlow.gameObject, 245);

        RectTransform bar = Rect("Bar", root.transform, new Vector2(0.5f, 0f), new Vector2(560f, 8f), new Vector2(0f, 8f));
        hud.bar = bar;
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        hud.fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        hud.fill.anchorMin = new Vector2(0f, 0f);
        hud.fill.anchorMax = new Vector2(0f, 1f);
        hud.fill.pivot = new Vector2(0f, 0.5f);
        hud.fill.offsetMin = hud.fill.offsetMax = Vector2.zero;
        hud.fillImage = hud.fill.gameObject.AddComponent<Image>();
        hud.fillImage.color = Gold;

        // Above the bar's right end, reading leftwards, so a long line never runs off the screen.
        hud.level = Label("Level", bar, new Vector2(1f, 1f), new Vector2(560f, 24f), Vector2.zero, 17f, TextAlignmentOptions.BottomRight);
        hud.level.rectTransform.pivot = new Vector2(1f, 0f);
        hud.level.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        hud.gain = Label("Gain", bar, new Vector2(0.5f, 1f), new Vector2(320f, 28f), new Vector2(0f, 112f), 19f, TextAlignmentOptions.Center);

        // The banner: type on nothing, like the rest of the HUD, between two thin lines.
        RectTransform bannerRoot = Rect("Level Banner", root.transform, new Vector2(0.5f, 0.74f), new Vector2(900f, 110f), Vector2.zero);
        hud.bannerRect = bannerRoot;
        // A level is worth interrupting for: the banner draws above any open window (the station's, as the craft lands).
        OnTop(bannerRoot.gameObject, 250);
        hud.banner = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        hud.banner.alpha = 0f;
        // A soft dark band under the type, so it reads over a sunlit meadow as well as at night.
        Image backing = Rect("Backing", bannerRoot, new Vector2(0.5f, 0.5f), new Vector2(980f, 150f), new Vector2(0f, 4f)).gameObject.AddComponent<Image>();
        backing.sprite = UISprites.Band;
        backing.color = new Color(0f, 0f, 0f, 0.55f);
        backing.raycastTarget = false;
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
        Techniques.FirstUsed += hud.OnTechniqueFirstUsed;
        Preload();
        return hud;
    }

    private void OnEnable() => Current = this;

    private void OnDestroy()
    {
        ExperienceTracker.Gained -= OnGained;
        ExperienceTracker.LevelReached -= OnLevelReached;
        Techniques.FirstUsed -= OnTechniqueFirstUsed;
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
        UIMotion.Punch(gain.transform, same ? 0.12f : 0.3f, 0.3f);
        FlyMote(skill);
        if (skill != null)
        {
            shownSkill = skill;
        }
    }

    // What was learned flies from the player into the bar, which takes it in with a flash: dozens of small
    // moments a session, so they are quick and never more than a few a second.
    private void FlyMote(ProficiencyDefinition skill)
    {
        Camera view = Camera.main;
        if (skill == null || Player.Current == null || view == null || Time.unscaledTime - lastMote < 0.15f || GamePause.IsPaused)
        {
            return;
        }

        lastMote = Time.unscaledTime;
        var canvas = (RectTransform)transform;
        Vector3 screen = view.WorldToScreenPoint(Player.Current.transform.position + Vector3.up * 1.9f);
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 from))
        {
            return;
        }

        Vector3 barEnd = bar.TransformPoint(new Vector3(bar.rect.xMin + bar.rect.width * Mathf.Clamp01(shownShare), 0f, 0f));
        Vector2 to = canvas.InverseTransformPoint(barEnd);
        UIMotion.Fly(canvas, from, to, Color.Lerp(Gold, Color.white, 0.35f), 0.55f, () =>
        {
            if (fillImage != null)
            {
                UIMotion.Flash(fillImage, Color.white, 0.25f);
                UIMotion.Punch(bar, 0.12f, 0.25f);
            }
        });
    }

    private void OnLevelReached(ProficiencyDefinition skill, int reached, bool milestone)
    {
        bannerTitle.text = $"{skill.DisplayName.ToUpperInvariant()}  {reached}";
        bannerLine.text = milestone
            ? GameInput.WithRebinds($"A milestone: <color={UIStyle.Current.GoodHex}>a perk to choose ({{key:Talents}})</color>")
            : NextMilestone(skill, reached) is int next ? $"Next milestone at {next}" : "Mastered";
        bannerAt = Time.unscaledTime;
        bannerFor = milestone ? MilestoneBannerSeconds : BannerSeconds;

        // The pop: the banner springs in, its title flashes white, sparks fly; a milestone is bigger in every way.
        UIStyle style = UIStyle.Current;
        UIMotion.Pop(bannerRect, milestone ? 0.45f : 0.7f, milestone ? 0.55f : 0.4f);
        UIMotion.Flash(bannerTitle, Color.white, milestone ? 0.9f : 0.5f);
        UIMotion.Burst(bannerRect, new Vector2(0f, 14f), milestone ? 34 : 14, style.Accent, milestone ? 460f : 260f, milestone ? 1.4f : 0.9f, milestone ? 13f : 9f);
        if (milestone)
        {
            // At home and out of a fight, the world holds its breath a moment as a milestone lands.
            if (GameSession.ActiveRaid == null && Player.Current != null && (Player.Current.Stealth == null || !Player.Current.Stealth.IsFighting))
            {
                slowUntil = Time.unscaledTime + SlowSeconds;
                slowing = true;
            }

            UIMotion.Burst(bannerRect, new Vector2(0f, 14f), 18, Color.white, 300f, 1.1f, 7f);
            Color shaft = style.Accent;
            shaft.a = 0.55f;
            UIMotion.Rays(bannerRect, new Vector2(0f, 14f), 12, shaft, 620f, 1.6f);
            Color glow = style.Accent;
            glow.a = 0.55f;
            UIMotion.Flash(screenGlow, glow, 1.6f);
        }

        if (skill == shownSkill)
        {
            UIMotion.Flash(fillImage, Color.white, 0.6f);
            UIMotion.Punch(bar, 0.25f, 0.5f);
        }
    }

    // Eases time from a third back to full over the milestone's first moment; never touches a paused or frozen game.
    private void SlowMotion()
    {
        if (!slowing || GamePause.IsTimeFrozen)
        {
            return;
        }

        float left = slowUntil - Time.unscaledTime;
        if (left <= 0f)
        {
            slowing = false;
            Time.timeScale = 1f;
            return;
        }

        float t = 1f - left / SlowSeconds;
        Time.timeScale = Mathf.Lerp(0.3f, 1f, t * t);
    }

    private void OnDisable()
    {
        if (slowing && !GamePause.IsTimeFrozen)
        {
            Time.timeScale = 1f;
        }

        slowing = false;
    }

    // The first time a technique comes into play it is named, so the player knows what just happened and why.
    private void OnTechniqueFirstUsed(PerkDefinition perk)
    {
        if (Time.unscaledTime - bannerAt < bannerFor)
        {
            GameMessages.Post($"Technique: {perk.DisplayName}. {perk.Description}", MessageKind.Gain);
            return;
        }

        bannerTitle.text = perk.DisplayName.ToUpperInvariant();
        bannerLine.text = $"<color={UIStyle.Current.AccentHex}>Technique</color>   {perk.Description}";
        bannerAt = Time.unscaledTime;
        bannerFor = BannerSeconds + 1.2f;
        UIMotion.Pop(bannerRect, 0.75f, 0.35f);
        UIMotion.Flash(bannerTitle, Color.white, 0.5f);
        UIMotion.Burst(bannerRect, new Vector2(0f, 14f), 10, UIStyle.Current.Accent, 220f, 0.8f, 8f);
        UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.PerkChosen : null);
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
        SlowMotion();
        float shown = Time.unscaledTime - bannerAt;
        banner.alpha = shown >= bannerFor ? 0f : Mathf.Clamp01(shown / 0.25f) * Mathf.Clamp01((bannerFor - shown) / 0.6f);

        group.alpha = GamePause.IsPaused ? 0f : 1f;
        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int needed = 0;
        int towards = shownSkill != null ? progress.TowardsNext(shownSkill, settings, out needed) : 0;
        float share = shownSkill == null ? 0f : needed <= 0 ? 1f : towards / (float)needed;
        // The bar eases to where it should be; after a level it starts again from empty rather than draining back.
        int shownLevel = shownSkill != null ? progress.LevelOf(shownSkill, settings) : 0;
        if (shownSkill != easedSkill || shownLevel != easedLevel)
        {
            shownShare = shownSkill != easedSkill ? share : 0f;
            easedSkill = shownSkill;
            easedLevel = shownLevel;
        }

        shownShare = Mathf.Lerp(shownShare, share, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        fill.anchorMax = new Vector2(Mathf.Clamp01(shownShare), 1f);
        int skillLevel = shownSkill != null ? progress.LevelOf(shownSkill, settings) : 0;
        // Something to do in the skills window: a perk to choose, or Insight to put into a skill.
        int points = progress.PendingChoices(settings) + (progress.Insight > 0 ? 1 : 0);
        int held = ExperienceTracker.HeldBonus;
        if (shownSkill != writtenSkill || skillLevel != writtenLevel || points != writtenPoints || GameInput.KeysVersion != writtenKeys
            || held != writtenHeld)
        {
            writtenSkill = shownSkill;
            writtenLevel = skillLevel;
            writtenPoints = points;
            writtenKeys = GameInput.KeysVersion;
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

            level.text = points > 0 ? GameInput.WithRebinds($"{name}  <color={UIStyle.Current.GoodHex}>({{key:Talents}})</color>") : name;
        }

        // A perk waiting to be chosen keeps calling softly.
        level.color = points > 0 ? Color.Lerp(Gold, Color.white, UIMotion.Breath(0.8f) * 0.6f) : Gold;

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
            gain.rectTransform.anchoredPosition = new Vector2(0f, 112f + age * 14f);
        }
    }

    // The moments' sounds are loaded now, so the first level of a session doesn't stall the frame it lands on.
    private static void Preload()
    {
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        var moments = new[] { LevelingSettings.Current.LevelUpSound, sounds?.Milestone, sounds?.PerkChosen, sounds?.CraftFine, sounds?.CraftMasterwork };
        foreach (SoundDefinition sound in moments)
        {
            if (sound == null)
            {
                continue;
            }

            foreach (AudioClip clip in sound.Clips)
            {
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }
            }
        }
    }

    private static void OnTop(GameObject part, int order)
    {
        var canvas = part.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
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
