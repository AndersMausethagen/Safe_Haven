using System.Collections.Generic;
using UnityEngine;
using static CodeUI;

/// <summary>
/// The left column, which reads: this run's hour and map (with a large map's other regions and a long way round), its
/// rumours, what the Watch Post foretold and what the Haven asked for, the known dangers and the likely finds. Part of
/// RaidPreparationWindow, in its own file.
/// </summary>
public sealed partial class RaidPreparationWindow
{
    // A large map's other regions, named after the run's own.
    private string Beyond(RaidDefinition raid)
    {
        if (!plan.Large || !raid.CanBeLarge)
        {
            return "";
        }

        List<RaidMapMood> moods = RaidLayoutPlanner.RegionMoods(raid.LayoutRules, plan.Seed, raid.LayoutRules.LargeRegions);
        if (moods.Count <= 1)
        {
            return "";
        }

        var others = new List<string>();
        for (int i = 1; i < moods.Count; i++)
        {
            others.Add(moods[i].name);
        }

        return $" Further off: {string.Join(" and ", others)}.";
    }

    // Once the run is laid out ahead (RaidLayoutPrefetch), whether there is a long way round as well as the short one.
    private string RoutesLine(RaidDefinition raid)
    {
        RaidLayoutPlan layout = RaidLayoutPrefetch.Peek(raid, plan.Seed);
        return layout != null && layout.LongRoute.Count > 0 ? " Two ways lead to the beacon ahead: the short one is watched, the long one quieter." : "";
    }

    // The left column. Each part sits under the last, so a run with one rumour and a tier with few monsters leave no gaps.
    // A place that can't be reached has no run to tell of, only what is known of it.
    private void DrawBriefing(RaidDefinition raid, RaidTimeOfDay timeOfDay, bool closed)
    {
        UIStyle style = UIStyle.Current;
        float y = BodyTop;
        float x = Margin;
        y = Stack(thisRunHeading, y, HeadingHeight);
        float chips = y;
        bool anyChip = false;
        hourChip.Root.SetActive(false);
        moodChip.Root.SetActive(false);
        if (timeOfDay != null && !closed)
        {
            int hours = Mathf.FloorToInt(timeOfDay.hour) % 24;
            int minutes = Mathf.FloorToInt((timeOfDay.hour - Mathf.Floor(timeOfDay.hour)) * 60f);
            string reward = timeOfDay.lootMultiplier > 1.001f ? $"  <color={UIStyle.Hex(style.Good)}>+{timeOfDay.lootMultiplier - 1f:P0} loot</color>" : "";
            x = hourChip.Show($"{timeOfDay.DisplayName} {hours:00}:{minutes:00}{reward}", x, chips);
            hourDetail = timeOfDay.briefing;
            anyChip = true;
        }

        if (raid.LayoutRules != null && !closed)
        {
            (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, plan.Seed);
            moodChip.Show(mood.name, x, chips);
            moodDetail = $"{mood.briefing} {shape.briefing}{Beyond(raid)}{RoutesLine(raid)}";
            anyChip = true;
        }

        y = anyChip ? chips + 44f : chips;

        List<RaidEventDefinition> foretold = closed ? new List<RaidEventDefinition>() : RaidForecast.For(raid, plan.Seed, HavenReadiness.ForetoldEvents);
        List<RaidRumourDefinition> rumours = closed ? new List<RaidRumourDefinition>() : RaidRumours.For(raid, plan.Seed, GameSession.Settlement, plan.Player != null ? plan.Player.GetComponent<PlayerCrafting>() : null, plan.Large ? raid.LargeRaid.widthScale : 1f);
        List<string> asked = closed ? new List<string>() : HavenRequestBoard.BriefingLines(plan.Player);
        int lines = rumours.Count + foretold.Count + asked.Count;
        while (rumourLines.Count < lines)
        {
            rumourLines.Add(AddRumourLine());
        }

        for (int i = 0; i < rumourLines.Count; i++)
        {
            RumourLine line = rumourLines[i];
            line.Rect.gameObject.SetActive(i < lines);
            if (i < lines)
            {
                line.Label.text = i < rumours.Count ? rumours[i].BriefingFor(GameSession.Settlement)
                    : i < rumours.Count + foretold.Count ? $"Seen from the Watch Post: {foretold[i - rumours.Count].DisplayName}."
                    : asked[i - rumours.Count - foretold.Count];
                float height = Measure(line.Label, ColumnWidth - 16f);
                line.Label.rectTransform.sizeDelta = new Vector2(ColumnWidth - 16f, height);
                y = Stack(line.Rect, y, height, 6f);
            }
        }

        thisRunHeading.gameObject.SetActive(anyChip || lines > 0);
        y = (anyChip || lines > 0) ? y + SectionGap - 6f : BodyTop;

        var enemies = new List<string>();
        foreach (MonsterDefinition monster in raid.KnownEnemies)
        {
            if (monster != null && (plan.Difficulty == null || raid.CanMeet(monster, plan.Difficulty.tier)))
            {
                enemies.Add(monster.DisplayName);
            }
        }

        dangersHeading.gameObject.SetActive(enemies.Count > 0);
        dangersLabel.gameObject.SetActive(enemies.Count > 0);
        if (enemies.Count > 0)
        {
            y = Stack(dangersHeading, y, HeadingHeight);
            // What hurts beyond blows, and the food or resistance that answers it (Docs/PlayerStatsResearch.md #8-9).
            List<RaidHarms.Harm> harms = RaidHarms.For(raid, plan.Difficulty != null ? plan.Difficulty.tier : Tiers.Max);
            dangersLabel.text = $"{string.Join("  ·  ", enemies)}\n{RaidHarms.Describe(harms, RaidHarms.IsCold(PlayerWarmth.ColdNow()), style.MutedHex)}";
            y = Stack(dangersLabel, y, Measure(dangersLabel, ColumnWidth), SectionGap);
        }

        DrawFinds(raid, y);
    }

    private void DrawFinds(RaidDefinition raid, float y)
    {
        var finds = new List<(Sprite icon, UnityEngine.Object definition)>();
        foreach (ItemDefinition item in raid.LikelyItems)
        {
            if (item != null && (plan.Difficulty == null || plan.Difficulty.Allows(item)))
            {
                finds.Add((item.Icon, item));
            }
        }

        foreach (ResourceDefinition resource in raid.LikelyResources)
        {
            if (resource != null)
            {
                finds.Add((resource.Icon, resource));
            }
        }

        while (findSlots.Count < finds.Count)
        {
            findSlots.Add(AddFindSlot());
        }

        findsHeading.gameObject.SetActive(finds.Count > 0);
        if (finds.Count > 0)
        {
            y = Stack(findsHeading, y, HeadingHeight);
        }

        const float step = FindSize + 6f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth + 6f) / step));
        for (int i = 0; i < findSlots.Count; i++)
        {
            FindSlot slot = findSlots[i];
            slot.Rect.gameObject.SetActive(i < finds.Count);
            if (i < finds.Count)
            {
                slot.Definition = finds[i].definition;
                slot.Icon.sprite = finds[i].icon;
                slot.Icon.enabled = finds[i].icon != null;
                slot.Rect.anchoredPosition = new Vector2(Margin + i % perRow * step, -(y + i / perRow * step));
            }
        }
    }
}
