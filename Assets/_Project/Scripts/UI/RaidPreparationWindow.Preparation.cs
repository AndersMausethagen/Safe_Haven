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
        companionSelf.gameObject.SetActive(any && plan.Companion != null);
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
            ResidentRecord along = GameSession.Here.Find(plan.Companion);
            int raids = along?.RaidsAlong ?? 0;
            string rank = PeopleBond.Rank(along)?.name;
            companionBond.text = raids == 0 ? "No raids together yet" : $"{(string.IsNullOrEmpty(rank) ? "" : rank + ", ")}{raids} raid{(raids == 1 ? "" : "s")} together";
        }

        if (plan.Companion != null)
        {
            ResidentRecord along = GameSession.Here.Find(plan.Companion);
            (companionSelf.text, companionSelfDetail) = CompanionSelf(along, plan.Destination);
            y = Stack(companionSelf, y, 24f);
        }

        float bleed = plan.Companion != null ? plan.Companion.RaidCompanion.BleedOutSeconds : 0f;
        companionRisk.text = plan.Companion == null ? "One person can go with you." : $"If they fall, help them up within {bleed:0} seconds.";
        companionRiskDetail = plan.Companion == null ? null
            : $"Help them up within {bleed:0} seconds if they fall, or the forest has {plan.Companion.DisplayName}, and they will forget you.";
        return Stack(companionRisk, y, 24f, SectionGap);
    }

    // Who the one going along is as a fighter: their skill, fighting traits, marks (those against this raid's creatures lit), kit,
    // spirits and a fighting heart; a short line and the whole on hover.
    private static (string line, string detail) CompanionSelf(ResidentRecord record, RaidDefinition raid)
    {
        if (record == null)
        {
            return ("", null);
        }

        UIStyle style = UIStyle.Current;
        ProficiencyDefinition skill = ResidentTalents.FightSkill(record);
        var line = new List<string> { $"{skill?.DisplayName} {ResidentTalents.LevelOf(record, skill)}" };
        var detail = new List<string> { $"{skill?.DisplayName} {ResidentTalents.LevelOf(record, skill)}: +{ResidentTalents.CompanionDamage(record) * 100f:0}% damage." };
        foreach (ResidentTraitDefinition trait in ResidentTraits.Of(record))
        {
            if (trait != null && IsFighting(trait))
            {
                line.Add(trait.DisplayName);
                detail.Add($"{trait.DisplayName}: {trait.Description}");
            }
        }

        string good = ColorUtility.ToHtmlStringRGB(style.Good);
        foreach (MarkDefinition mark in ResidentMarks.Of(record))
        {
            bool here = raid != null && !string.IsNullOrEmpty(mark.Creature) && Meets(raid, mark.Creature);
            line.Add(here ? $"<color=#{good}>{mark.DisplayName}</color>" : mark.DisplayName);
            detail.Add($"{mark.DisplayName}{(here ? " (it counts here)" : "")}: {mark.Description}");
        }

        if (!record.KitWeapon.IsEmpty)
        {
            detail.Add($"{CompanionKit.NameOf(record, true)}, {Mathf.RoundToInt(record.KitWeapon.Durability * 100f)}% sound.");
        }

        Spirits spirits = ResidentSpirits.Of(record);
        if (spirits != Spirits.Steady)
        {
            line.Add(ResidentSpirits.NameOf(spirits));
        }

        if (record.Inspiration == Inspiration.FightingHeart)
        {
            line.Add("spoiling for a fight");
            detail.Add($"Spoiling for a fight: +{PeopleSettings.Current.FightingHeart * 100f:0}% damage this raid.");
        }

        return (string.Join(" · ", line), string.Join("\n", detail));
    }

    private static bool IsFighting(ResidentTraitDefinition trait)
    {
        foreach (TraitEffect effect in trait.Effects)
        {
            if (effect.kind == TraitEffectKind.FightDamage || effect.kind == TraitEffectKind.FightDodge || effect.kind == TraitEffectKind.FightHealth
                || effect.kind == TraitEffectKind.NightSight || effect.kind == TraitEffectKind.DamageAgainst)
            {
                return true;
            }
        }

        return false;
    }

    // Whether the raid's creatures include one whose id holds the word (Troll-wary on a raid with trolls).
    private static bool Meets(RaidDefinition raid, string word)
    {
        foreach (MonsterDefinition monster in raid.KnownEnemies)
        {
            if (monster != null && monster.Id.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // The right column, stacked the same way: what can't be chosen here takes no room.
    private void DrawPreparation(RaidDefinition raid, bool sparksUsable)
    {
        float y = BodyTop;
        bool tiers = raid.Difficulties.Count > 0;
        tierHeading.gameObject.SetActive(tiers);
        gearLabel.gameObject.SetActive(tiers);
        tierBriefing.gameObject.SetActive(false);
        growthChip.Root.SetActive(false);
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
        // A boss encounter takes no runes (RaidLauncher drops them): none are offered.
        bool runes = !raid.IsEncounter;
        bool any = runes && modifierContent.sizeDelta.y > 0f;
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
            noModifiers.text = HearthKit.InlineIcon("Info") + (runes ? "  None in your bag" : "  No runes for a boss fight");
            noModifiersDetail = runes ? EmptyBag : NoRunesForBosses;
            Stack(noModifiers, y, 26f);
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
        // A boss encounter's first fight is at its own tier (RaidPlan.Settle): the other tiers wait for the rematches, so they can't be picked.
        bool fixedTier = raid.IsEncounter && !plan.IsRematch;
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
            button.Label.color = shown ? Tiers.ColorOf(tier.tier) : fixedTier ? dim
                : (score >= tier.minimumGearScore && RaidLauncher.CheckWard(tier) == null) || Cheats.NoRequirements ? style.Text : dim;
            button.Button.interactable = shown || !fixedTier;
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
        // What the tier is like is on its button's hover (no paragraph under the tiers): only a boss's fixed first fight is said here.
        bool note = fixedTier && plan.Difficulty != null;
        tierBriefing.gameObject.SetActive(note);
        if (note)
        {
            tierBriefing.text = $"<color={style.AccentHex}>The first fight is at tier {Tiers.ToRoman(plan.Difficulty.tier)}. Once it is won, rematches at any tier.</color>";
            y = Stack(tierBriefing, y, Measure(tierBriefing, ColumnWidth), 6f);
        }

        // What the time at home came to, at the door to the next raid: one chip, the skills on hover.
        growthChip.Root.SetActive(false);
        if (ExperienceTracker.SinceLastRaid() is string grown)
        {
            string[] parts = grown.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            growthChip.Show("Star", parts.Length > 1 ? $"{parts[0]}  <color={style.MutedHex}>+{parts.Length - 1} more</color>" : parts[0], RightColumn, y);
            growthDetail = string.Join("\n", parts) + ".";
            y += ChipRowHeight;
        }

        return y + SectionGap - 6f;
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
        BossEncounterDefinition encounter = raid.Encounter;
        // A boss encounter's raid time is only a hidden safety net (RaidDirector) and it has no elites: its clock is the enrage, and
        // in the elites' place stands what the gate asks.
        NameFigure(Figure.Time, encounter != null ? "Enrages after" : null, null);
        NameFigure(Figure.Elites, encounter != null ? "The gate asks" : null, encounter != null ? "Bags" : null);
        if (encounter != null)
        {
            figures[(int)Figure.Time].text = RaidStats.FormatTime(encounter.EnrageSeconds);
            figures[(int)Figure.Time].color = UIStyle.Current.Text;
        }
        else
        {
            ShowFigure(Figure.Time, RaidStats.FormatTime(rules.Duration), raid, rules, RaidStat.Duration, true);
        }

        ShowFigure(Figure.Extraction, $"{rules.ExtractionTime:0.#}s", raid, rules, RaidStat.ExtractionTime, false);
        ShowFigure(Figure.Loot, $"×{rules.LootMultiplier:0.##}", raid, rules, RaidStat.LootQuantity, true);
        ShowFigure(Figure.EnemyHealth, $"×{rules.EnemyHealthMultiplier:0.##}", raid, rules, RaidStat.EnemyHealth, false);
        ShowFigure(Figure.EnemyPower, $"×{rules.EnemyPowerMultiplier:0.##}", raid, rules, RaidStat.EnemyPower, false);
        string price = encounter != null ? encounter.GatePrice : null;
        // A long price is set smaller so it fits its column; the gate's own menu says it in full.
        figures[(int)Figure.Elites].text = encounter == null ? DescribeElites(plan.Difficulty)
            : price.Length == 0 ? "Nothing" : price.Length > 14 ? $"<size=60%>{price}</size>" : price;
        figures[(int)Figure.Elites].color = UIStyle.Current.Text;
    }

    // The figures' names (made by Building), found once: a boss encounter renames two and puts them back for an expedition.
    // Not serialized: an Editor reload restored it as an empty array, and the first figure was out of range.
    [System.NonSerialized] private TMP_Text[] figureNames;

    private void NameFigure(Figure figure, string name, string icon)
    {
        if (figureNames == null || figureNames.Length != FigureNames.Length)
        {
            figureNames = new TMP_Text[FigureNames.Length];
            for (int i = 0; i < FigureNames.Length; i++)
            {
                Transform found = stats.transform.Find(FigureNames[i] + " Label");
                figureNames[i] = found != null ? found.GetComponent<TMP_Text>() : null;
            }
        }

        int at = (int)figure;
        if (figureNames[at] != null)
        {
            figureNames[at].text = HearthKit.InlineIcon(icon ?? FigureIcons[at]) + " " + (name ?? FigureNames[at]);
        }
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
        // Harsh death rules: nothing comes home from a fall, worn gear and the safe pocket included.
        string rules = DeathSettings.Now.raidTakesEverything
            ? $"<color={UIStyle.Hex(UIStyle.Current.Danger)}>Harsh death rules: if you fall or time runs out, everything you carry and wear is lost, the safe pocket too.</color> Your home remains safe."
            : lossRules;
        riskDetail = $"{rules}\n\n{carrying}";
        if (problem != null)
        {
            riskLabel.text = $"<color={warning}>{problem}</color>";
            return;
        }

        string cart = PlayerTransport.Current != null ? PlayerTransport.Current.RaidWarning() : "";
        string horse = HorseTravel.RaidWarning(Player.Current != null ? Player.Current.gameObject : null);
        cart = cart.Length > 0 && horse.Length > 0 ? cart + " " + horse : cart + horse;
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
