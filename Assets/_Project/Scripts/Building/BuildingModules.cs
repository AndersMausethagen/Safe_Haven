using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Fitting modules to buildings (the user, 2026-11-21; Docs/BuildingUpgradesResearch_2026-11-21.md, with the user's answers: placed
/// extensions stay and share the slots, a removed module goes back as a spare, one module of a group). A building that takes modules
/// has one slot per tier (at least what its placed extensions had), at most <see cref="MostSlots"/>; its placed extensions and fitted
/// modules share them. A module needs its tier, fits once per building and one of its Group, and is known from the start or taught by a
/// quest as it starts (QuestDefinition › Unlocks Modules) or held as a spare. Fitting pays from the bag and the chests (MaterialPool) or
/// takes a spare for free, at once; removing puts it in the Haven's store (SettlementState.SpareModules). The Upgrade page lists it all
/// (<see cref="AddChoices"/>). The bonuses count through Construction.Sum like a tier's.
/// </summary>
public static class BuildingModules
{
    public const int MostSlots = 5;
    /// <summary>The most luck (a chance of one more) a station gets from its modules and extensions together.</summary>
    public const float MostLuck = 0.15f;

    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    /// <summary>Every module in the game.</summary>
    public static IReadOnlyList<BuildingModuleDefinition> All => Content != null ? Content.Modules : (IReadOnlyList<BuildingModuleDefinition>)System.Array.Empty<BuildingModuleDefinition>();

    /// <summary>True when some module fits this kind of building.</summary>
    public static bool TakesModules(BuildingDefinition building)
    {
        foreach (BuildingModuleDefinition module in All)
        {
            if (module != null && module.Fits(building))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The slots used: the placed extensions working for it and the modules fitted.</summary>
    public static int Used(BuildingObject building) =>
        StationExtensions.Of(building).Count + (building != null && building.TryGetComponent(out Construction c) ? c.Fitted.Count : 0);

    public static int Free(BuildingObject building) => Mathf.Max(0, StationExtensions.Slots(building) - Used(building));

    /// <summary>True when the player may fit it: known from the start, taught by a quest under way or done, or a spare in store.</summary>
    public static bool IsKnown(BuildingModuleDefinition module) => IsKnown(module, null);

    /// <summary>The same, leaving <paramref name="except"/> out of the quests that teach it.</summary>
    public static bool IsKnown(BuildingModuleDefinition module, QuestDefinition except)
    {
        if (module == null)
        {
            return false;
        }

        if (module.KnownFromStart || GameSession.Home.SpareCount(module.Id) > 0)
        {
            return true;
        }

        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        ContentDatabase content = Content;
        // A tier that teaches it, standing (BuildingLevel › Unlocks Modules): the best of each kind as last seen at home.
        foreach (BuildingDefinition building in content != null ? content.Buildings : (IReadOnlyList<BuildingDefinition>)System.Array.Empty<BuildingDefinition>())
        {
            int standing = building != null ? GameSession.Home.BuiltTier(building) : 0;
            for (int level = 2; level <= standing; level++)
            {
                BuildingLevel tier = building.UpgradeTo(level);
                if (tier != null && tier.unlocksModules.Contains(module))
                {
                    return true;
                }
            }
        }

        foreach (QuestDefinition quest in content != null ? content.Quests : (IReadOnlyList<QuestDefinition>)System.Array.Empty<QuestDefinition>())
        {
            if (quest != null && quest != except && quest.UnlocksModules.Contains(module) && log != null && log.StatusOf(quest) != QuestStatus.NotStarted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells the player of modules just learned (a quest starting, a tier finished): what each is and where it goes. Not one known some
    /// other way already (<paramref name="teacher"/>, the quest just started, aside).
    /// </summary>
    public static void AnnounceLearned(IEnumerable<BuildingModuleDefinition> modules, QuestDefinition teacher)
    {
        if (modules == null || HavenVisit.InHostsHaven)
        {
            return;
        }

        foreach (BuildingModuleDefinition module in modules)
        {
            if (module != null && !IsKnown(module, teacher))
            {
                GameMessages.Post($"New module: the {module.DisplayName}, for {module.FitsText()} at Tier {Tiers.ToRoman(module.Tier)} ({WhereToFit(module)}): {module.Effect()}.",
                    MessageKind.Info);
            }
        }
    }

    /// <summary>
    /// Where the player fits modules to <paramref name="building"/>, in words: a station's window has an Upgrade tab; any other building's
    /// Upgrade page opens from the build menu's Manage tab.
    /// </summary>
    public static string WhereToFit(BuildingDefinition building) =>
        building != null && building.Station != CraftingStationType.None ? $"the {building.DisplayName}'s Upgrade tab" : "build menu › Manage › Upgrade";

    /// <summary>The same for a module that may fit several kinds: the station tab when it fits stations only.</summary>
    public static string WhereToFit(BuildingModuleDefinition module)
    {
        bool stationsOnly = true;
        foreach (BuildingDefinition building in Content != null ? Content.Buildings : (IReadOnlyList<BuildingDefinition>)System.Array.Empty<BuildingDefinition>())
        {
            if (building != null && module.Fits(building) && building.Station == CraftingStationType.None)
            {
                stationsOnly = false;
            }
        }

        return stationsOnly ? "its Upgrade tab" : "its Upgrade tab, or build menu › Manage › Upgrade";
    }

    /// <summary>Why <paramref name="module"/> can't be fitted to <paramref name="building"/> now (materials aside), or null.</summary>
    public static string WhyNot(BuildingObject building, BuildingModuleDefinition module)
    {
        if (building == null || module == null || !building.TryGetComponent(out Construction construction))
        {
            return "That can't take a module.";
        }

        if (HavenVisit.IsVisiting)
        {
            return "Only your own buildings take modules.";
        }

        if (!module.Fits(building.Definition))
        {
            return $"The {module.DisplayName} doesn't fit the {building.Definition.DisplayName}.";
        }

        if (!construction.IsStanding || construction.IsBuilding)
        {
            return $"Finish the {building.Definition.DisplayName} first.";
        }

        if (construction.Level < module.Tier)
        {
            return $"Needs the {building.Definition.DisplayName} at Tier {Tiers.ToRoman(module.Tier)}.";
        }

        if (construction.Fitted.Contains(module))
        {
            return "Fitted already.";
        }

        if (!string.IsNullOrEmpty(module.Group))
        {
            foreach (BuildingModuleDefinition other in construction.Fitted)
            {
                if (other != null && other.Group == module.Group)
                {
                    return $"Instead of the {other.DisplayName}: one {module.Group} at a time.";
                }
            }

            foreach (BuildingObject placed in StationExtensions.Of(building))
            {
                if (placed.Definition != null && placed.Definition.ExtensionGroup == module.Group)
                {
                    return $"Instead of the {placed.Definition.DisplayName}: one {module.Group} at a time.";
                }
            }
        }

        return Free(building) <= 0 ? "No slot free. A higher tier opens another." : null;
    }

    /// <summary>Fits <paramref name="module"/>: a spare in store goes in for free, otherwise its cost is paid. False, with a message, when it can't.</summary>
    public static bool Fit(BuildingObject building, BuildingModuleDefinition module, GameObject user)
    {
        string refused = WhyNot(building, module) ?? (IsKnown(module) ? null : $"You don't know the {module.DisplayName} yet.");
        if (refused != null)
        {
            GameMessages.Post(refused, MessageKind.Warning);
            return false;
        }

        bool spare = GameSession.Home.TakeSpareModule(module.Id);
        if (!spare && !MaterialPool.For(user).TrySpend(module.Essence, module.Cost))
        {
            GameMessages.Post($"Not enough for the {module.DisplayName}.", MessageKind.Warning);
            return false;
        }

        building.GetComponent<Construction>().AddModule(module);
        GameMessages.Post($"Fitted the {module.DisplayName} to the {building.Definition.DisplayName}: {module.Effect()}.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.ModuleFitted, module, 1, user, detail: building.Definition));
        return true;
    }

    /// <summary>Takes <paramref name="module"/> off into the Haven's store, to fit again for free.</summary>
    public static bool Remove(BuildingObject building, BuildingModuleDefinition module)
    {
        if (building == null || !building.TryGetComponent(out Construction construction) || !construction.RemoveModule(module))
        {
            return false;
        }

        GameSession.Home.AddSpareModule(module.Id);
        GameMessages.Post($"Took the {module.DisplayName} off. It waits in store: fit it again here or elsewhere for free.", MessageKind.Info);
        return true;
    }

    /// <summary>The modules part of a building's Upgrade page: slots, what is fitted (to remove), and what fits (to fit).</summary>
    public static void AddChoices(MenuPage page, BuildingObject building, GameObject user)
    {
        if (building == null || building.Definition == null || !TakesModules(building.Definition) || !building.TryGetComponent(out Construction construction))
        {
            return;
        }

        int slots = StationExtensions.Slots(building);
        var placed = StationExtensions.Of(building);
        var lines = new List<string> { $"<b>Modules: {Used(building)} of {slots} slots</b>" + (slots < MostSlots ? " (a higher tier opens another)" : "") };
        foreach (BuildingObject extension in placed)
        {
            lines.Add($"• {extension.Definition.DisplayName} (placed beside it)");
        }

        foreach (BuildingModuleDefinition module in construction.Fitted)
        {
            lines.Add($"• {module.DisplayName}: {module.Effect()}");
        }

        page.Body = (string.IsNullOrEmpty(page.Body) ? "" : page.Body + "\n\n") + string.Join("\n", lines);
        foreach (BuildingModuleDefinition module in new List<BuildingModuleDefinition>(construction.Fitted))
        {
            BuildingModuleDefinition chosen = module;
            page.Add(new MenuAction($"Remove {module.DisplayName}", "Into the store, to fit again for free", true,
                () => Remove(building, chosen)) { Icon = "Hammer" });
        }

        MaterialPool pool = MaterialPool.For(user);
        foreach (BuildingModuleDefinition module in All)
        {
            if (module == null || !module.Fits(building.Definition) || construction.Fitted.Contains(module) || !IsKnown(module))
            {
                continue;
            }

            BuildingModuleDefinition chosen = module;
            string refused = WhyNot(building, module);
            bool spare = GameSession.Home.SpareCount(module.Id) > 0;
            bool affordable = spare || pool.CanAfford(module.Essence, module.Cost, out _);
            string cost = spare ? "From the store, free" : AmountText.Describe(module.Cost);
            string missing = affordable ? null : MissingText(pool, module);
            string detail = refused ?? (affordable ? $"{module.Effect()}. {cost}" : $"{module.Effect()}. {cost}   <color={UIStyle.Current.DangerHex}>{missing}</color>");
            page.Add(new MenuAction($"Fit {module.DisplayName}", detail, refused == null && affordable, () => Fit(building, chosen, user)) { Icon = "Gear" });
        }
    }

    private static string MissingText(MaterialPool pool, BuildingModuleDefinition module)
    {
        pool.CanAfford(module.Essence, module.Cost, out string missing);
        return missing;
    }
}
