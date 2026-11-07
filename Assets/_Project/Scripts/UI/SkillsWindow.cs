using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The skills screen (N): every skill by group down the left with its level and how far to the next; on the right the
/// chosen skill, what trains it, what its level gives now and at the top, buttons to put Insight into it, and its
/// milestones, each a choice of two perks (click one to take it). A milestone's first choice can be made anywhere;
/// changing one, or choosing all again, only at home beside the portal. Built in code for every scene with a player
/// (ExperienceTracker); it only reads and changes GameSession.Progress. Once the talent screen; the script keeps its GUID.
/// </summary>
public sealed class SkillsWindow : MonoBehaviour
{
    private const float Width = 1380f;
    private const float Height = 820f;
    private const float ScreenMargin = 24f;
    private const float ListX = 30f;
    private const float ListWidth = 440f;
    private const float RowHeight = 34f;
    private const float DetailX = 500f;
    private const float DetailWidth = 850f;
    private const float MilestoneTop = 395f;
    private const float MilestoneHeight = 64f;
    private const float MilestoneGap = 6f;
    private const float CardWidth = 375f;
    private const int MilestoneRows = 5;

    private sealed class SkillRow
    {
        public ProficiencyDefinition Skill;
        public Image Background;
        public Image Selected;
        public TMP_Text Name;
        public TMP_Text Level;
        public RectTransform Fill;
    }

    private sealed class PerkCard
    {
        public PerkDefinition Perk;
        public Image Background;
        public Image Owned;
        public TMP_Text Text;
        public Button Button;
    }

    private readonly List<SkillRow> rows = new List<SkillRow>();
    private readonly List<(TMP_Text level, PerkCard first, PerkCard second)> milestones = new List<(TMP_Text, PerkCard, PerkCard)>();
    private UIWindow window;
    private RectTransform panel;
    private TMP_Text header;
    private TMP_Text skillName;
    private TMP_Text skillExperience;
    private TMP_Text skillAbout;
    private TMP_Text skillBonuses;
    private TMP_Text milestoneHeading;
    private Button insightStep;
    private Button insightAll;
    private TMP_Text insightStepLabel;
    private Button resetButton;
    private ProficiencyDefinition selected;
    private CharacterProgress watched;

    public static SkillsWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static SkillsWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Skills Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<SkillsWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    /// <summary>The skill shown on the right.</summary>
    public ProficiencyDefinition Selected => selected;

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDestroy()
    {
        if (watched != null)
        {
            watched.Changed -= Refresh;
        }
    }

    private void Update()
    {
        Breathe();
        if (watched != GameSession.Progress)
        {
            if (watched != null)
            {
                watched.Changed -= Refresh;
            }

            watched = GameSession.Progress;
            watched.Changed += Refresh;
            Refresh();
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        panel = CodeUI.Box("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text title = CodeUI.Label("Title", panel, 0f, 14f, Width, 54f, 38f, style.Text, TextAlignmentOptions.Center);
        title.text = "Skills";
        style.Title(title);
        header = CodeUI.Label("Header", panel, 0f, 70f, Width, 36f, 21f, style.Text, TextAlignmentOptions.Center);

        BuildList(style);
        BuildDetail(style);

        CodeUI.AddButton("Choose Again", panel, Width * 0.5f - 230f, Height - 63f, 220f, UIRole.Kind.Button, ResetPerks, out resetButton).text = "Choose Perks Again";
        CodeUI.AddButton("Close", panel, Width * 0.5f + 10f, Height - 63f, 220f, UIRole.Kind.Button, () => window.Close(), out _).text = "Close";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Proficiencies, panel.gameObject);
        window.OpenChanged += open =>
        {
            if (open)
            {
                Refresh();
            }
        };
    }

    private void BuildList(UIStyle style)
    {
        LevelingSettings settings = LevelingSettings.Current;
        float y = 120f;
        foreach (ProficiencyGroup group in System.Enum.GetValues(typeof(ProficiencyGroup)))
        {
            var inGroup = new List<ProficiencyDefinition>();
            foreach (ProficiencyDefinition skill in settings.Skills)
            {
                if (skill != null && skill.Group == group)
                {
                    inGroup.Add(skill);
                }
            }

            if (inGroup.Count == 0)
            {
                continue;
            }

            CodeUI.Heading(group.ToString(), panel, ListX, y, ListWidth, style);
            y += 30f;
            foreach (ProficiencyDefinition skill in inGroup)
            {
                rows.Add(AddRow(skill, y, style));
                y += RowHeight;
            }

            y += 8f;
        }

        selected = rows.Count > 0 ? rows[0].Skill : null;
    }

    private SkillRow AddRow(ProficiencyDefinition skill, float y, UIStyle style)
    {
        RectTransform rect = CodeUI.At(skill.DisplayName, panel, ListX, y, ListWidth, RowHeight - 4f);
        var row = new SkillRow { Skill = skill, Background = rect.gameObject.AddComponent<Image>() };
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = row.Background;
        button.onClick.AddListener(() => Select(skill));
        rect.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Button);
        var hover = rect.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        AddTrigger(hover, UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => ShowNext(skill, rect));
        AddTrigger(hover, UnityEngine.EventSystems.EventTriggerType.PointerExit, HideNext);
        row.Name = CodeUI.Label("Name", rect, 14f, 2f, ListWidth - 120f, RowHeight - 10f, 18f, style.Text);
        row.Level = CodeUI.Label("Level", rect, ListWidth - 104f, 2f, 90f, RowHeight - 10f, 18f, style.Accent, TextAlignmentOptions.Right);
        Image track = CodeUI.At("Track", rect, 14f, RowHeight - 9f, ListWidth - 28f, 3f).gameObject.AddComponent<Image>();
        track.color = style.Track;
        track.raycastTarget = false;
        row.Fill = CodeUI.At("Fill", track.transform, 0f, 0f, 0f, 3f);
        Image fill = row.Fill.gameObject.AddComponent<Image>();
        fill.color = style.Accent;
        fill.raycastTarget = false;
        // Amber down the edge marks the skill shown on the right.
        row.Selected = CodeUI.At("Selected", rect, 0f, 0f, 4f, RowHeight - 4f).gameObject.AddComponent<Image>();
        row.Selected.color = style.Accent;
        row.Selected.raycastTarget = false;
        return row;
    }

    private RectTransform nextPanel;
    private TMP_Text nextText;

    private static void AddTrigger(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, System.Action action)
    {
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    // Beside a hovered skill: its next milestone and both of what it offers, so the player has something to aim at.
    private void ShowNext(ProficiencyDefinition skill, RectTransform row)
    {
        LevelingSettings settings = LevelingSettings.Current;
        int level = GameSession.Progress.LevelOf(skill, settings);
        UIStyle style = UIStyle.Current;
        string text = null;
        foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
        {
            if (milestone.level > level)
            {
                text = $"<color={style.AccentHex}>{skill.DisplayName} {milestone.level}</color>  <color={style.MutedHex}>in {milestone.level - level} level{(milestone.level - level == 1 ? "" : "s")}</color>\n"
                       + $"{Offer(milestone.first, style)}\n<color={style.MutedHex}>or</color>  {Offer(milestone.second, style)}";
                break;
            }
        }

        if (text == null)
        {
            text = $"<color={style.AccentHex}>{skill.DisplayName} {level}</color>\nEvery milestone reached.";
        }

        if (nextPanel == null)
        {
            nextPanel = CodeUI.At("Next Milestone", panel, 0f, 0f, 380f, 82f);
            nextPanel.gameObject.AddComponent<Image>().color = style.Panel;
            nextPanel.gameObject.AddComponent<Outline>().effectColor = style.Accent;
            nextText = CodeUI.Paragraph("Text", nextPanel, 12f, 8f, 356f, 70f, 16f, style.Text);
            nextText.richText = true;
        }

        nextText.text = text;
        nextPanel.gameObject.SetActive(true);
        nextPanel.SetAsLastSibling();
        nextPanel.anchoredPosition = new Vector2(ListX + ListWidth + 8f, row.anchoredPosition.y);
        UIMotion.Pop(nextPanel, 0.9f, 0.18f);
    }

    private static string Offer(PerkDefinition perk, UIStyle style) => perk == null ? "-"
        : perk.IsTechnique ? $"<b>{perk.DisplayName}</b> <size=80%><color={style.AccentHex}>TECHNIQUE</color></size>"
        : $"<b>{perk.DisplayName}</b> <color={style.GoodHex}>{perk.DescribeBonuses()}</color>";

    private void HideNext()
    {
        if (nextPanel != null)
        {
            nextPanel.gameObject.SetActive(false);
        }
    }

    private void BuildDetail(UIStyle style)
    {
        skillName = CodeUI.Label("Skill Name", panel, DetailX, 116f, DetailWidth, 40f, 30f, style.Accent);
        skillExperience = CodeUI.Label("Skill Experience", panel, DetailX, 156f, DetailWidth, 28f, 18f, style.Muted);
        skillAbout = CodeUI.Paragraph("Skill About", panel, DetailX, 186f, DetailWidth, 72f, 17f, style.Text);
        skillAbout.richText = true;
        skillBonuses = CodeUI.Paragraph("Skill Bonuses", panel, DetailX, 258f, DetailWidth, 44f, 17f, style.Text);
        skillBonuses.richText = true;
        insightStepLabel = CodeUI.AddButton("Insight Step", panel, DetailX, 304f, 260f, UIRole.Kind.Button, () => PutInsight(LevelingSettings.Current.InsightStep), out insightStep);
        CodeUI.AddButton("Insight All", panel, DetailX + 272f, 304f, 220f, UIRole.Kind.Button, () => PutInsight(int.MaxValue), out insightAll).text = "Put In All Insight";
        milestoneHeading = CodeUI.Heading("Milestones", panel, DetailX, 365f, DetailWidth, style);

        for (int i = 0; i < MilestoneRows; i++)
        {
            float y = MilestoneTop + i * (MilestoneHeight + MilestoneGap);
            TMP_Text level = CodeUI.Label("Milestone " + (i + 1), panel, DetailX, y, 80f, MilestoneHeight, 24f, style.Accent, TextAlignmentOptions.Center);
            PerkCard first = AddCard("First " + (i + 1), DetailX + 90f, y, style);
            PerkCard second = AddCard("Second " + (i + 1), DetailX + 90f + CardWidth + 10f, y, style);
            milestones.Add((level, first, second));
        }
    }

    private PerkCard AddCard(string name, float x, float y, UIStyle style)
    {
        RectTransform rect = CodeUI.At(name, panel, x, y, CardWidth, MilestoneHeight);
        var card = new PerkCard { Background = rect.gameObject.AddComponent<Image>() };
        card.Button = rect.gameObject.AddComponent<Button>();
        card.Button.targetGraphic = card.Background;
        card.Button.onClick.AddListener(() => Choose(card.Perk));
        rect.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Button);
        card.Text = CodeUI.Paragraph("Text", rect, 12f, 6f, CardWidth - 20f, MilestoneHeight - 10f, 15f, style.Text);
        card.Text.richText = true;
        card.Owned = CodeUI.At("Owned", rect, 0f, 0f, 4f, MilestoneHeight).gameObject.AddComponent<Image>();
        card.Owned.color = style.Accent;
        card.Owned.raycastTarget = false;
        return card;
    }

    // Milestones reached and waiting for a choice breathe in the accent, so the eye goes to them.
    private void Breathe()
    {
        if (window == null || !window.IsOpen || selected == null)
        {
            return;
        }

        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int level = progress.LevelOf(selected, settings);
        UIStyle style = UIStyle.Current;
        float breath = UIMotion.Breath(0.7f) * 0.35f;
        IReadOnlyList<ProficiencyDefinition.Milestone> reached = selected.Milestones;
        for (int i = 0; i < milestones.Count && i < reached.Count; i++)
        {
            bool waiting = reached[i].level <= level && progress.ChosenAt(reached[i]) == null;
            if (!waiting)
            {
                continue;
            }

            (TMP_Text label, PerkCard first, PerkCard second) = milestones[i];
            first.Background.color = Color.Lerp(style.ControlLit, style.Accent, breath);
            second.Background.color = Color.Lerp(style.ControlLit, style.Accent, breath);
            label.color = Color.Lerp(style.Accent, Color.white, breath * 2f);
        }
    }

    /// <summary>Shows a skill on the right.</summary>
    public void Select(ProficiencyDefinition skill)
    {
        selected = skill;
        Refresh();
    }

    private void PutInsight(int amount)
    {
        if (selected == null)
        {
            return;
        }

        if (GameSession.Progress.Insight <= 0)
        {
            GameMessages.Post("No Insight to put in. Quests, raids and discoveries bring it.", MessageKind.Warning);
            return;
        }

        int spent = ExperienceTracker.SpendInsight(selected, amount);
        if (spent <= 0)
        {
            GameMessages.Post($"{selected.DisplayName} can't grow any further.", MessageKind.Warning);
            UIMotion.Shake(insightStep.transform);
            return;
        }

        UIMotion.Punch(skillName.transform, 0.12f, 0.35f);
        UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.Insight : null);
    }

    private void Choose(PerkDefinition perk)
    {
        LevelingSettings settings = LevelingSettings.Current;
        if (!GameSession.Progress.CanChoose(selected, perk, settings, AtHome, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            foreach ((TMP_Text _, PerkCard first, PerkCard second) in milestones)
            {
                if (first.Perk == perk) UIMotion.Shake(first.Background.transform);
                if (second.Perk == perk) UIMotion.Shake(second.Background.transform);
            }

            return;
        }

        GameSession.Progress.Choose(selected, perk, settings, AtHome);
        foreach ((TMP_Text _, PerkCard first, PerkCard second) in milestones)
        {
            PerkCard card = first.Perk == perk ? first : second.Perk == perk ? second : null;
            if (card != null)
            {
                UIStyle style = UIStyle.Current;
                UIMotion.Punch(card.Background.transform, 0.08f, 0.4f);
                UIMotion.Flash(card.Background, style.Accent, 0.6f);
                UIMotion.Burst((RectTransform)card.Background.transform, Vector2.zero, 16, style.Accent, 200f, 0.9f, 9f);
            }
        }

        UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.PerkChosen : null);
    }

    private void ResetPerks()
    {
        if (!AtHome)
        {
            GameMessages.Post("Perks can only be chosen again at home, beside the portal.", MessageKind.Warning);
            return;
        }

        GameSession.Progress.ResetPerks();
        GameMessages.Post("Every perk is yours to choose again.");
    }

    // Free, but only at home: a change of mind needs a quiet place.
    private static bool AtHome => HomePortal.Current != null;

    private void Refresh()
    {
        if (header == null)
        {
            return;
        }

        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);

        UIStyle style = UIStyle.Current;
        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int pending = progress.PendingChoices(settings);
        string insight = progress.Insight > 0
            ? $"<color={style.AccentHex}>{progress.Insight} Insight</color> to put into a skill"
            : "No Insight to spend";
        string choices = pending > 0 ? $"   ·   <color={style.GoodHex}>{pending} perk{(pending == 1 ? "" : "s")} to choose</color>" : "";
        int totalLevel = progress.TotalLevel(settings);
        string title = settings.TitleFor(totalLevel);
        title = string.IsNullOrEmpty(title) ? "" : $"<color={style.AccentHex}>{title}</color>   ·   ";
        header.text = $"{title}Total level {totalLevel}   ·   {insight}{choices}";

        foreach (SkillRow row in rows)
        {
            int level = progress.LevelOf(row.Skill, settings);
            int towards = progress.TowardsNext(row.Skill, settings, out int needed);
            row.Name.text = row.Skill.DisplayName + (HasPending(row.Skill, level, progress) ? $"  <color={style.GoodHex}>•</color>" : "");
            row.Level.text = level.ToString();
            row.Fill.sizeDelta = new Vector2((ListWidth - 28f) * (needed > 0 ? Mathf.Clamp01(towards / (float)needed) : 1f), 3f);
            row.Background.color = row.Skill == selected ? style.ControlLit : style.Filled;
            row.Selected.enabled = row.Skill == selected;
        }

        RefreshDetail(style, settings, progress);
        resetButton.interactable = AtHome && progress.PerkCount > 0;
    }

    // Melee's Weapon Art flourishes, waiting or won.
    private static string Flourishes(ProficiencyDefinition skill, int level, UIStyle style)
    {
        if (skill.Training != ProficiencyTraining.Melee)
        {
            return "";
        }

        int from = TechniqueSettings.Current.FlourishLevel;
        string what = "one-handed arts strike harder and a kill gives Focus back, two-handed arts sunder, polearm arts pin";
        return level >= from
            ? $"\n<color={style.AccentHex}>Weapon flourishes:</color> {what}."
            : $"\n<color={style.MutedHex}>From {from}, weapon flourishes: {what}.</color>";
    }

    // What speeds the skill now: its mentor building at home, and being rested.
    private static string Boosts(ProficiencyDefinition skill, LevelingSettings settings, UIStyle style)
    {
        var parts = new List<string>();
        if (skill.Mentor != null)
        {
            int tier = GameSession.Settlement.BuiltTier(skill.Mentor);
            float bonus = settings.MentorBonus(tier);
            parts.Add(bonus > 0f
                ? $"{skill.Mentor.DisplayName} {Tiers.ToRoman(tier)}: <color={style.GoodHex}>+{Mathf.RoundToInt(bonus * 100f)}% experience</color>"
                : $"<color={style.MutedHex}>A {skill.Mentor.DisplayName} upgraded past tier I teaches it faster</color>");
        }

        float gain = Player.Current != null && Player.Current.Combatant != null ? Player.Current.Combatant.Stats.Get(StatType.ExperienceGain) : 0f;
        if (gain > 0f)
        {
            parts.Add($"<color={style.GoodHex}>Rested: +{Mathf.RoundToInt(gain * 100f)}% experience</color>");
        }

        return parts.Count > 0 ? "\n" + string.Join("   ·   ", parts) : "";
    }

    private static bool HasPending(ProficiencyDefinition skill, int level, CharacterProgress progress)
    {
        foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
        {
            if (milestone.level <= level && progress.ChosenAt(milestone) == null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshDetail(UIStyle style, LevelingSettings settings, CharacterProgress progress)
    {
        bool shown = selected != null;
        skillName.gameObject.SetActive(shown);
        skillExperience.gameObject.SetActive(shown);
        skillAbout.gameObject.SetActive(shown);
        skillBonuses.gameObject.SetActive(shown);
        insightStep.gameObject.SetActive(shown);
        insightAll.gameObject.SetActive(shown);
        milestoneHeading.gameObject.SetActive(shown && selected.Milestones.Count > 0);
        if (!shown)
        {
            foreach ((TMP_Text level, PerkCard first, PerkCard second) in milestones)
            {
                level.gameObject.SetActive(false);
                first.Background.gameObject.SetActive(false);
                second.Background.gameObject.SetActive(false);
            }

            return;
        }

        int skillLevel = progress.LevelOf(selected, settings);
        int towards = progress.TowardsNext(selected, settings, out int needed);
        skillName.text = $"{selected.DisplayName}  {skillLevel}";
        skillExperience.text = needed > 0
            ? $"{towards} / {needed} to level {skillLevel + 1}   ·   {progress.ExperienceOf(selected)} experience in all"
            : $"Highest level   ·   {progress.ExperienceOf(selected)} experience in all";
        string trained = string.IsNullOrWhiteSpace(selected.TrainedBy) ? "" : $"<color={style.MutedHex}>Trained by {selected.TrainedBy}.</color>  ";
        skillAbout.text = trained + selected.Description + Boosts(selected, settings, style) + Flourishes(selected, skillLevel, style);
        skillBonuses.text = $"Now: {DescribeLevel(selected, skillLevel, settings, style)}\n" +
                            $"<color={style.MutedHex}>At {settings.MaxLevel}: {DescribeLevel(selected, settings.MaxLevel, settings, style)}</color>";

        bool canGrow = skillLevel < settings.MaxLevel;
        insightStep.interactable = canGrow && progress.Insight > 0;
        insightAll.interactable = canGrow && progress.Insight > 0;
        insightStepLabel.text = $"Put In {(progress.Insight > 0 ? Mathf.Min(settings.InsightStep, progress.Insight) : settings.InsightStep)} Insight";
        if (canGrow && settings.InsightRate(skillLevel) < 1f)
        {
            skillExperience.text += $"   ·   Insight counts {Mathf.RoundToInt(settings.InsightRate(skillLevel) * 100f)}% from level {settings.InsightFullBelow}";
        }

        IReadOnlyList<ProficiencyDefinition.Milestone> reached = selected.Milestones;
        for (int i = 0; i < milestones.Count; i++)
        {
            (TMP_Text level, PerkCard first, PerkCard second) = milestones[i];
            bool exists = i < reached.Count;
            level.gameObject.SetActive(exists);
            first.Background.gameObject.SetActive(exists);
            second.Background.gameObject.SetActive(exists);
            if (!exists)
            {
                continue;
            }

            ProficiencyDefinition.Milestone milestone = reached[i];
            bool open = skillLevel >= milestone.level;
            PerkDefinition chosen = progress.ChosenAt(milestone);
            level.text = milestone.level.ToString();
            level.color = open ? style.Accent : style.Muted;
            ShowCard(first, milestone.first, open, chosen, style);
            ShowCard(second, milestone.second, open, chosen, style);
        }
    }

    private static void ShowCard(PerkCard card, PerkDefinition perk, bool open, PerkDefinition chosen, UIStyle style)
    {
        card.Perk = perk;
        bool owned = perk != null && perk == chosen;
        // The lit colour: the button role tints it down at rest, which leaves a locked card a step below an open one.
        card.Background.color = open ? style.ControlLit : style.Filled;
        card.Owned.enabled = owned;
        card.Button.interactable = perk != null;
        if (perk == null)
        {
            card.Text.text = "";
            return;
        }

        string name = owned ? $"<color={style.AccentHex}>{perk.DisplayName}</color>" : perk.DisplayName;
        if (perk.IsTechnique)
        {
            // A technique is a new thing to do, not a number: its words are the point, so they read in full colour.
            string tag = $"<size=75%><color={(open ? style.AccentHex : style.MutedHex)}>TECHNIQUE</color></size>";
            card.Text.text = $"<b>{name}</b>  {tag}\n<size=88%><color={(open ? style.TextHex : style.MutedHex)}>{perk.Description}</color></size>";
            return;
        }

        string bonuses =$"<color={(open ? style.GoodHex : style.MutedHex)}>{perk.DescribeBonuses()}</color>";
        card.Text.text = $"<b>{name}</b>  <size=90%>{bonuses}</size>\n<size=85%><color={style.MutedHex}>{perk.Description}</color></size>";
    }

    // What a skill's level bonuses come to at a level: "-10% Swing Stamina Cost, +4% Melee Damage", or a word when none apply.
    private static string DescribeLevel(ProficiencyDefinition skill, int level, LevelingSettings settings, UIStyle style)
    {
        var parts = new List<string>();
        foreach (ProficiencyDefinition.LevelBonus bonus in skill.LevelBonuses)
        {
            float value = ProficiencyDefinition.ValueAt(bonus, level, settings);
            if (!Mathf.Approximately(value, 0f))
            {
                // A beginner's penalties stand out, so it reads plainly what practice will take away.
                string text = new StatBonus(bonus.stat, value).Describe();
                parts.Add(StatNames.IsPenalty(bonus.stat, value) ? $"<color={style.WarningHex}>{text}</color>" : text);
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : $"<color={style.MutedHex}>as a seasoned hand: no beginner's penalties left</color>";
    }
}
