using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The skills window's Overview (the user, 2026-11-13: the skills screen felt cluttered; "an overview tab with the
/// overall state of the skills and which ones we are closest to unlocking"): three figures (total level, Insight to put
/// in, perks to choose), the five skills nearest their next milestone with how far along, and each group's total with
/// its skills. A click on a skill or group opens it in the Skills tab. Above both tabs, the window's painting
/// (Resources/WindowArt/Skills) with its title and line on it. Opens on the Overview; built with the window.
/// </summary>
public sealed partial class SkillsWindow
{
    private const float TabsTop = 152f;
    private const float PageShift = 92f;
    private const int Closest = 5;
    private static readonly string[] TabNames = { "Overview", "Skills" };

    private RectTransform skillsPage;
    private RectTransform overviewPage;
    private KitTabs tabs;
    private int tab;
    private TMP_Text levelFigure;
    private TMP_Text insightFigure;
    private TMP_Text perkFigure;
    private readonly List<(KitProgressRow row, Image icon, Button button)> closestRows = new List<(KitProgressRow, Image, Button)>();
    private readonly List<ProficiencyDefinition> closestSkills = new List<ProficiencyDefinition>();
    private readonly List<(ProficiencyGroup group, TMP_Text total, TMP_Text skills)> groupRows = new List<(ProficiencyGroup, TMP_Text, TMP_Text)>();

    private static float PanelHeight => Height + PageShift;

    // A page laid over the panel as it was first built (top-anchored, the panel's width and first height), so what is on it
    // keeps its place, all of it moved down together by PageShift.
    private RectTransform Page(string name)
    {
        RectTransform page = CodeUI.Box(name, panel, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        page.anchorMax = new Vector2(1f, 1f);
        page.pivot = new Vector2(0.5f, 1f);
        page.offsetMin = new Vector2(0f, -Height - PageShift);
        page.offsetMax = new Vector2(0f, -PageShift);
        return page;
    }

    /// <summary>Moves what BuildList and BuildDetail made (children from <paramref name="first"/> on) onto the Skills page.</summary>
    private void MakeSkillsPage(int first)
    {
        var made = new List<Transform>();
        for (int i = first; i < panel.childCount; i++)
        {
            made.Add(panel.GetChild(i));
        }

        skillsPage = Page("Skills Page");
        foreach (Transform child in made)
        {
            child.SetParent(skillsPage, false);
        }
    }

    private void BuildOverview(UIStyle style)
    {
        panel.sizeDelta = new Vector2(Width, PanelHeight);
        KitBanner banner = KitBanner.Show(panel, "Skills", TabsTop - 4f, 100000f);
        banner?.Darken(0.35f);
        tabs = new KitTabs(panel, ListX, TabsTop, 170f, 46f, TabNames, ShowTab);
        HearthKit.Rule(panel, ListX, TabsTop + 52f, Width - ListX * 2f, 0.35f);

        overviewPage = Page("Overview Page");
        float top = 120f;
        string[] names = { "Total level", "Insight to put in", "Perks to choose" };
        string[] icons = { "Star", "Spark", "Crit" };
        float tileWidth = (Width - ListX * 2f - 24f) / 3f;
        var figures = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform tile = HearthKit.Card(overviewPage, names[i], ListX + i * (tileWidth + 12f), top, tileWidth, 84f, 0.5f);
            HearthKit.Icon(tile, icons[i], 22f, 22f, 40f);
            figures[i] = CodeUI.Label("Figure", tile, 78f, 8f, 160f, 48f, 38f, style.AccentBright);
            figures[i].fontStyle = FontStyles.Bold;
            CodeUI.Label("Name", tile, 78f, 52f, tileWidth - 90f, 24f, 17f, style.Muted).text = names[i];
        }

        levelFigure = figures[0];
        insightFigure = figures[1];
        perkFigure = figures[2];

        float columnTop = top + 104f;
        float leftWidth = 760f;
        RectTransform near = HearthKit.Card(overviewPage, "Closest", ListX, columnTop, leftWidth, Height - columnTop - 90f);
        HearthKit.Section(near, "Closest milestones", "Star", 20f, 16f);
        for (int i = 0; i < Closest; i++)
        {
            var row = new KitProgressRow(near, 18f, 56f + i * 82f, leftWidth - 36f);
            RectTransform rect = (RectTransform)near.GetChild(near.childCount - 1);
            int index = i;
            Image fill = rect.GetComponent<Image>();
            fill.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            HearthKit.Tint(button);
            UIHighlight.On(rect.gameObject);
            button.onClick.AddListener(() => Open(index < closestSkills.Count ? closestSkills[index] : null));
            Image icon = rect.Find("Star Icon")?.GetComponent<Image>();
            closestRows.Add((row, icon, button));
        }

        float rightX = ListX + leftWidth + 16f;
        float rightWidth = Width - rightX - ListX;
        RectTransform groups = HearthKit.Card(overviewPage, "Groups", rightX, columnTop, rightWidth, Height - columnTop - 90f);
        HearthKit.Section(groups, "By group", "Stats", 20f, 16f);
        string[] groupIcons = { "Axe", "Hammer", "Sword", "Eye", "HorseHead" };
        int g = 0;
        foreach (ProficiencyGroup group in System.Enum.GetValues(typeof(ProficiencyGroup)))
        {
            ProficiencyGroup shown = group;
            RectTransform rect = HearthKit.Card(groups, group.ToString(), 18f, 56f + g * 82f, rightWidth - 36f, 74f, 0.25f, style.Panel, 1.3f);
            Image fill = rect.GetComponent<Image>();
            fill.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            HearthKit.Tint(button);
            UIHighlight.On(rect.gameObject);
            button.onClick.AddListener(() => OpenGroup(shown));
            HearthKit.Icon(rect, groupIcons[Mathf.Min(g, groupIcons.Length - 1)], 16f, 20f, 34f);
            TMP_Text name = CodeUI.Label("Name", rect, 62f, 8f, 220f, 30f, 21f, style.Text);
            style.Title(name);
            name.text = group.ToString();
            TMP_Text total = CodeUI.Label("Total", rect, rightWidth - 36f - 170f, 8f, 150f, 30f, 18f, style.AccentBright, TextAlignmentOptions.Right);
            TMP_Text skills = CodeUI.Label("Skills", rect, 62f, 40f, rightWidth - 36f - 80f, 26f, 15f, style.Muted);
            groupRows.Add((group, total, skills));
            g++;
        }

        PadTabs.Register(this, () => this != null && window != null && window.IsOpen, step => ShowTab(PadTabs.Next(tab, TabNames.Length, step)), () => tabs.Rects);
        ShowTab(0);
    }

    private void ShowTab(int shown)
    {
        tab = shown;
        overviewPage.gameObject.SetActive(shown == 0);
        skillsPage.gameObject.SetActive(shown == 1);
        tabs.Show(shown);
        HideNext();
        Refresh();
    }

    private void Open(ProficiencyDefinition skill)
    {
        if (skill == null)
        {
            return;
        }

        selected = skill;
        ShowTab(1);
    }

    private void OpenGroup(ProficiencyGroup group)
    {
        foreach (ProficiencyDefinition skill in LevelingSettings.Current.Skills)
        {
            if (skill != null && skill.Group == group)
            {
                Open(skill);
                return;
            }
        }
    }

    // Where a skill stands against its next milestone: how many levels to go, and how far from the last one it has come.
    private static bool NextMilestone(ProficiencyDefinition skill, CharacterProgress progress, LevelingSettings settings,
        out ProficiencyDefinition.Milestone next, out float along, out float toGo)
    {
        int level = progress.LevelOf(skill, settings);
        int towards = progress.TowardsNext(skill, settings, out int needed);
        float exact = level + (needed > 0 ? Mathf.Clamp01(towards / (float)needed) : 0f);
        int previous = 0;
        foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
        {
            if (milestone.level > level)
            {
                next = milestone;
                toGo = milestone.level - exact;
                along = Mathf.Clamp01((exact - previous) / Mathf.Max(1f, milestone.level - previous));
                return true;
            }

            previous = milestone.level;
        }

        next = default;
        along = 1f;
        toGo = float.MaxValue;
        return false;
    }

    private void RefreshOverview(UIStyle style, LevelingSettings settings, CharacterProgress progress)
    {
        if (overviewPage == null || !overviewPage.gameObject.activeSelf)
        {
            return;
        }

        levelFigure.text = progress.TotalLevel(settings).ToString();
        insightFigure.text = progress.Insight.ToString();
        int pending = progress.PendingChoices(settings);
        perkFigure.text = pending.ToString();
        perkFigure.color = pending > 0 ? style.Good : style.AccentBright;

        var ranked = new List<(ProficiencyDefinition skill, float toGo)>();
        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            if (skill != null && NextMilestone(skill, progress, settings, out _, out _, out float toGo))
            {
                ranked.Add((skill, toGo));
            }
        }

        ranked.Sort((a, b) => a.toGo.CompareTo(b.toGo));
        closestSkills.Clear();
        for (int i = 0; i < closestRows.Count; i++)
        {
            bool used = i < ranked.Count;
            closestRows[i].button.gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }

            ProficiencyDefinition skill = ranked[i].skill;
            closestSkills.Add(skill);
            NextMilestone(skill, progress, settings, out ProficiencyDefinition.Milestone next, out float along, out float toGo);
            int level = progress.LevelOf(skill, settings);
            string offers = next.first != null && next.second != null ? $"{next.first.DisplayName} or {next.second.DisplayName}" : "a perk";
            int levels = Mathf.CeilToInt(toGo);
            closestRows[i].row.Set($"{skill.DisplayName} {next.level}", offers, false, along,
                stateText: HearthKit.ToneNumbers($"{levels} level{(levels == 1 ? "" : "s")} to go"));
            if (closestRows[i].icon != null && skill.Icon != null)
            {
                closestRows[i].icon.sprite = skill.Icon;
                closestRows[i].icon.color = Color.white;
                closestRows[i].icon.enabled = true;
            }
        }

        foreach ((ProficiencyGroup group, TMP_Text total, TMP_Text skills) in groupRows)
        {
            int sum = 0;
            var parts = new List<string>();
            foreach (ProficiencyDefinition skill in settings.Skills)
            {
                if (skill != null && skill.Group == group)
                {
                    int level = progress.LevelOf(skill, settings);
                    sum += level;
                    bool waiting = HasPending(skill, level, progress);
                    parts.Add($"{skill.DisplayName} <color={(waiting ? style.GoodHex : style.AccentHex)}>{level}</color>");
                }
            }

            total.text = $"level {sum}";
            skills.text = string.Join("  ·  ", parts);
        }
    }
}
