using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The right column, which acts: the tier and the gear it asks for, Portal Sparks, the map's size, a companion and the
/// rune list; then the strip of figures (the run's rules against the raid's own) and the footer that says what can be
/// lost. Part of RaidPreparationWindow, in its own file.
/// </summary>
public sealed partial class RaidPreparationWindow
{
    private float DrawSize(RaidDefinition raid, float y)
    {
        bool can = raid.CanBeLarge;
        sizeHeading.gameObject.SetActive(can);
        sizeRow.SetActive(can);
        if (!can)
        {
            return y;
        }

        y = Stack(sizeHeading, y, HeadingHeight);
        sizeName.text = plan.Large ? "Large" : "Normal";
        RaidSizeOption option = raid.LargeRaid;
        float area = option.widthScale * option.widthScale;
        sizeDetail.text = plan.Large ? $"{area:0} times the ground and what is on it, {RaidStats.FormatTime(option.extraSeconds)} more time" : "The usual forest";
        return Stack(sizeRowRect, y, 46f, SectionGap);
    }

    private float DrawCompanion(float y)
    {
        bool any = plan.Companions.Count > 0;
        companionHeading.gameObject.SetActive(any);
        companionRow.SetActive(any);
        companionRisk.gameObject.SetActive(any);
        if (!any)
        {
            return y;
        }

        y = Stack(companionHeading, y, HeadingHeight);
        y = Stack(companionRowRect, y, 46f);
        companionName.text = plan.Companion != null ? plan.Companion.DisplayName : "Going alone";
        companionBond.text = "";
        if (plan.Companion != null)
        {
            int raids = GameSession.Settlement.Find(plan.Companion)?.RaidsAlong ?? 0;
            string rank = CompanionBond.RankFor(raids, SettlementSettings.Current.BondRanks)?.name;
            companionBond.text = raids == 0 ? "No raids together yet" : $"{(string.IsNullOrEmpty(rank) ? "" : rank + ", ")}{raids} raid{(raids == 1 ? "" : "s")} together";
        }

        companionRisk.text = plan.Companion == null ? "One person can go with you."
            : $"Help them up within {plan.Companion.RaidCompanion.BleedOutSeconds:0} seconds if they fall, or the forest has {plan.Companion.DisplayName}, and they will forget you.";
        return Stack(companionRisk, y, 26f, SectionGap);
    }

    // The right column, stacked the same way: what can't be chosen here takes no room.
    private void DrawPreparation(RaidDefinition raid, bool sparksUsable)
    {
        float y = BodyTop;
        bool tiers = raid.Difficulties.Count > 0;
        tierHeading.gameObject.SetActive(tiers);
        gearLabel.gameObject.SetActive(tiers);
        tierBriefing.gameObject.SetActive(tiers);
        while (tierButtons.Count < raid.Difficulties.Count)
        {
            tierButtons.Add(AddTierButton(tierButtons.Count));
        }

        for (int i = 0; i < tierButtons.Count; i++)
        {
            tierButtons[i].Button.gameObject.SetActive(i < raid.Difficulties.Count);
        }

        if (tiers)
        {
            y = DrawTiers(raid, y);
        }

        sparksHeading.gameObject.SetActive(sparksUsable);
        sparksRow.SetActive(sparksUsable);
        if (sparksUsable)
        {
            y = Stack(sparksHeading, y, HeadingHeight);
            y = Stack(sparksRowRect, y, 46f, SectionGap);
            DrawSparks();
        }

        y = DrawSize(raid, y);
        y = DrawCompanion(y);
        bool any = modifierContent.sizeDelta.y > 0f;
        Stack(modifiersHeading, y, HeadingHeight);
        Stack(modifiersNote, y + 2f, 26f);
        y += HeadingHeight;
        modifiersNote.gameObject.SetActive(any);
        noModifiers.gameObject.SetActive(!any);
        modifierView.gameObject.SetActive(any);
        if (any)
        {
            Stack(modifierView, y, BodyBottom - y);
        }
        else
        {
            noModifiers.text = EmptyBag;
            Stack(noModifiers, y, Measure(noModifiers, ColumnWidth));
        }
    }

    // The best fighting skill against the level the tier was tuned for (LevelingSettings.ExpectedLevel): gear isn't all of it.
    private static string FightingSkillLine(int tier, UIStyle style)
    {
        LevelingSettings settings = LevelingSettings.Current;
        int best = 0;
        foreach (ProficiencyTraining training in new[] { ProficiencyTraining.Melee, ProficiencyTraining.Archery, ProficiencyTraining.Spells })
        {
            ProficiencyDefinition skill = settings.SkillTrainedBy(training);
            best = skill != null ? Mathf.Max(best, GameSession.Progress.LevelOf(skill, settings)) : best;
        }

        int expected = settings.ExpectedLevel(tier);
        Color colour = best >= expected ? style.Good : best >= expected - 10 ? style.Accent : style.Warning;
        return $"<size=85%>Fighting skill <color={UIStyle.Hex(colour)}>{best}</color>  ·  the tier expects {expected}</size>";
    }

    private float DrawTiers(RaidDefinition raid, float y)
    {
        UIStyle style = UIStyle.Current;
        int score = GearScore.Of(plan.Player);
        y = Stack(tierHeading, y, HeadingHeight);
        for (int i = 0; i < raid.Difficulties.Count; i++)
        {
            TierButton button = tierButtons[i];
            RaidDifficulty tier = raid.Difficulties[i];
            button.Tier = tier;
            bool shown = tier == plan.Difficulty;
            Color dim = style.Muted;
            dim.a = 0.5f;
            button.Label.text = Tiers.ToRoman(tier.tier);
            button.Label.color = shown ? Tiers.ColorOf(tier.tier) : (score >= tier.minimumGearScore && RaidLauncher.CheckWard(tier) == null) || Cheats.NoRequirements ? style.Text : dim;
            button.Mark.SetActive(shown);
            var rect = (RectTransform)button.Button.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        }

        if (plan.Difficulty != null)
        {
            Color colour = score >= plan.Difficulty.recommendedGearScore ? style.Good : score >= plan.Difficulty.minimumGearScore ? style.Accent : style.Warning;
            gearLabel.text = $"Your gear <color={UIStyle.Hex(colour)}>{score}</color>  ·  recommended {plan.Difficulty.recommendedGearScore}"
                             + "\n" + FightingSkillLine(plan.Difficulty.tier, style);
            Stack(gearLabel, y, 46f);
        }

        y += 54f;
        tierBriefing.text = plan.Difficulty != null ? plan.Difficulty.briefing : "";
        return Stack(tierBriefing, y, Measure(tierBriefing, ColumnWidth), SectionGap);
    }

    private void DrawSparks()
    {
        UIStyle style = UIStyle.Current;
        SettlementSettings settings = SettlementSettings.Current;
        int owned = MaterialPool.For(plan.Player).Have(settings.PortalSpark);
        sparksValue.text = $"{plan.Sparks} / {settings.MaxSparksPerRaid}";
        sparksAdded.text = plan.Sparks > 0 ? $"+{RaidStats.FormatTime(plan.Sparks * settings.SecondsPerSpark)}" : "";
        sparksAdded.color = style.Good;
        sparksIcon.sprite = settings.PortalSpark.Icon;
        sparksIcon.enabled = sparksIcon.sprite != null;
        sparksOwned.text = owned > 0 ? $"You have {owned}" : "You have none";
        sparksOwned.color = owned > 0 ? style.Text : style.Muted;
        fewerSparks.interactable = plan.Sparks > 0;
        moreSparks.interactable = plan.Sparks < settings.MaxSparksPerRaid && plan.Sparks < owned;
    }

    private void DrawFigures(RaidDefinition raid, RaidTimeOfDay timeOfDay)
    {
        RaidParameters rules = plan.Rules(timeOfDay);
        ShowFigure(Figure.Time, RaidStats.FormatTime(rules.Duration), raid, rules, RaidStat.Duration, true);
        ShowFigure(Figure.Extraction, $"{rules.ExtractionTime:0.#}s", raid, rules, RaidStat.ExtractionTime, false);
        ShowFigure(Figure.Loot, $"×{rules.LootMultiplier:0.##}", raid, rules, RaidStat.LootQuantity, true);
        ShowFigure(Figure.EnemyHealth, $"×{rules.EnemyHealthMultiplier:0.##}", raid, rules, RaidStat.EnemyHealth, false);
        ShowFigure(Figure.EnemyPower, $"×{rules.EnemyPowerMultiplier:0.##}", raid, rules, RaidStat.EnemyPower, false);
        figures[(int)Figure.Elites].text = DescribeElites(plan.Difficulty);
        figures[(int)Figure.Elites].color = UIStyle.Current.Text;
    }

    // Against the raid's base rules: green where the choices made here help, amber where they make it harder.
    private void ShowFigure(Figure figure, string value, RaidDefinition raid, RaidParameters rules, RaidStat stat, bool moreIsBetter)
    {
        UIStyle style = UIStyle.Current;
        float was = raid.GetBase(stat);
        float now = rules.Get(stat);
        TMP_Text label = figures[(int)figure];
        label.text = value;
        label.color = Mathf.Approximately(was, now) ? style.Text : now > was == moreIsBetter ? style.Good : style.Warning;
    }

    private static string DescribeElites(RaidDifficulty tier)
    {
        if (tier == null || tier.eliteCount.y <= 0)
        {
            return "None";
        }

        return tier.eliteCount.x == tier.eliteCount.y ? tier.eliteCount.x.ToString() : $"{tier.eliteCount.x}–{tier.eliteCount.y}";
    }

    private void ShowRisk(string problem)
    {
        UIStyle style = UIStyle.Current;
        string warning = UIStyle.Hex(style.Warning);
        string carrying = DescribeCarried();
        riskDetail = $"{lossRules}\n\n{carrying}";
        if (problem != null)
        {
            riskLabel.text = $"<color={warning}>{problem}</color>";
            return;
        }

        string cart = PlayerTransport.Current != null ? PlayerTransport.Current.RaidWarning() : "";
        riskLabel.text = cart.Length > 0
            ? $"<color={warning}>{cart}</color>\n{lossSummary}"
            : $"<color={warning}>{lossSummary}</color>\n{carrying}";
    }

    private string DescribeCarried()
    {
        int items = 0;
        int resources = 0;
        if (plan.Player != null)
        {
            PlayerSnapshot carried = PlayerSnapshot.Capture(plan.Player);
            foreach (KeyValuePair<ItemDefinition, int> entry in carried.CountItems())
            {
                items += entry.Value;
            }

            foreach (KeyValuePair<ResourceDefinition, int> entry in carried.CountResources())
            {
                resources += entry.Value;
            }
        }

        return items == 0 && resources == 0
            ? "You carry nothing that can be lost."
            : $"You carry {items} item{(items == 1 ? "" : "s")} and {resources} resource{(resources == 1 ? "" : "s")}. Leave what you can't risk in storage.";
    }

    private void BindModifier(ModifierRow row, ItemDefinition item, int count)
    {
        row.Item = item;
        row.Icon.sprite = item.Icon;
        row.Icon.enabled = item.Icon != null;
        row.Name.text = count > 1 ? $"{item.DisplayName}  <color={UIStyle.Hex(UIStyle.Current.Muted)}>×{count}</color>" : item.DisplayName;
        row.Effect.text = item.RaidModifier.DescribeEffects();
        ShowPicked(row);
    }

    private void ShowPicked(ModifierRow row)
    {
        UIStyle style = UIStyle.Current;
        bool on = plan.IsPicked(row.Item);
        row.Mark.SetActive(on);
        row.State.text = on ? "Using" : "Use";
        row.State.color = on ? style.Good : style.Text;
        ColorBlock colors = row.Button.colors;
        colors.normalColor = colors.selectedColor = on ? style.Filled : style.Inset;
        colors.highlightedColor = style.Filled;
        colors.pressedColor = style.ControlLit;
        colors.colorMultiplier = 1f;
        row.Button.colors = colors;
    }
}
