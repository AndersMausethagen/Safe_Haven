using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts a raid from Safe Haven: pays for the chosen Portal Sparks (extra time, GDD §7.3) exactly once, uses up the
/// chosen raid modifier items, works out the raid's rules, records what the player takes along (the loadout: its tools,
/// weapons and backpacks are the deployed equipment kept if the raid fails) and loads the raid scene. Everything else the
/// player carries is at risk until they extract.
/// </summary>
public static class RaidLauncher
{
    /// <summary>Why the raid can't start right now, in words for the player, or null when it can.</summary>
    public static string CheckLaunch(RaidDefinition raid)
    {
        if (raid == null)
        {
            return "This portal doesn't lead anywhere.";
        }

        if (raid.IsLocked && !Cheats.NoRequirements)
        {
            return raid.LockedText;
        }

        if (GameSession.ActiveRaid != null)
        {
            return "A raid is already under way.";
        }

        if (string.IsNullOrWhiteSpace(raid.SceneName) || !Application.CanStreamedLevelBeLoaded(raid.SceneName))
        {
            return $"{raid.DisplayName} can't be reached: its scene isn't in the build.";
        }

        // The raid sends the player back here, so this scene has to be loadable too.
        if (!Application.CanStreamedLevelBeLoaded(SceneManager.GetActiveScene().name))
        {
            return "This place isn't in the build, so there would be no way back from the raid.";
        }

        return null;
    }

    /// <summary>Why the player's gear keeps them out of a tier, or null when it doesn't.</summary>
    public static string CheckGear(RaidDifficulty difficulty, GameObject player)
    {
        if (difficulty == null || Cheats.NoRequirements)
        {
            return null;
        }

        int score = GearScore.Of(player);
        return score >= difficulty.minimumGearScore
            ? null
            : $"Tier {Tiers.ToRoman(difficulty.tier)} needs a gear score of at least {difficulty.minimumGearScore}. Yours is {score}.";
    }

    /// <summary>Why the portal can't yet hold the way open to a tier, or null when it can.</summary>
    public static string CheckWard(RaidDifficulty difficulty)
    {
        return difficulty == null || Cheats.NoRequirements || GameSession.Settlement.WardLevel >= difficulty.wardLevelNeeded
            ? null
            : $"The ward isn't strong enough to hold the way open to tier {Tiers.ToRoman(difficulty.tier)}. Strengthen it at the portal.";
    }

    /// <summary>Share off the extraction time, from what has been done to the portal.</summary>
    public static float ExtractionCut => SettlementSettings.Current.ExtractionCutAt(GameSession.Settlement.PortalLevel);

    /// <summary>Why the player can't spend this many Portal Sparks, or null when they can.</summary>
    public static string CheckSparks(GameObject player, int sparks)
    {
        if (sparks <= 0)
        {
            return null;
        }

        SettlementSettings settings = SettlementSettings.Current;
        if (settings.PortalSpark == null || sparks > settings.MaxSparksPerRaid)
        {
            return $"A raid can use at most {settings.MaxSparksPerRaid} Portal Sparks.";
        }

        return MaterialPool.For(player).CanAfford(SparkCost(sparks), null, out string missing) ? null : missing;
    }

    /// <summary>The seconds a raid gets on top of its rules: Portal Sparks, and what residents' jobs add.</summary>
    public static float ExtraSeconds(int sparks) =>
        sparks * SettlementSettings.Current.SecondsPerSpark + GameSession.Settlement.JobBonus(job => job.RaidSeconds) + HavenReadiness.RaidSeconds;

    /// <summary>What a number of Portal Sparks costs, as resources.</summary>
    public static List<ResourceAmount> SparkCost(int sparks)
    {
        var cost = new List<ResourceAmount>();
        ResourceDefinition spark = SettlementSettings.Current.PortalSpark;
        if (spark != null && sparks > 0)
        {
            cost.Add(new ResourceAmount(spark, sparks));
        }

        return cost;
    }

    /// <summary>
    /// Sends the player into the raid with the chosen modifier items (each kind once, one of it used up) and Portal Sparks.
    /// Returns false, with a reason for the player, when it can't.
    /// </summary>
    public static bool TryLaunch(RaidDefinition raid, GameObject player, IEnumerable<ItemDefinition> modifierItems, out string problem,
        int sparks = 0, int? seed = null, RaidDifficulty difficulty = null)
    {
        problem = CheckLaunch(raid);
        difficulty ??= raid != null ? raid.LowestDifficulty : null;
        problem ??= CheckWard(difficulty);
        problem ??= CheckGear(difficulty, player);
        ItemInventory inventory = player != null ? player.GetComponent<ItemInventory>() : null;
        if (problem == null && inventory == null)
        {
            problem = "There is nobody to send into the raid.";
        }

        problem ??= CheckSparks(player, sparks);
        if (problem != null)
        {
            return false;
        }

        var chosen = new List<ItemDefinition>();
        if (modifierItems != null)
        {
            foreach (ItemDefinition item in modifierItems)
            {
                if (item == null || !item.IsRaidModifier || chosen.Contains(item))
                {
                    continue;
                }

                if (!inventory.Container.Contains(item, 1))
                {
                    problem = $"{item.DisplayName} is no longer in the bag.";
                    return false;
                }

                chosen.Add(item);
            }
        }

        // Paid once, here, before anything else changes (GDD §23.4: an extension costs exactly once).
        if (sparks > 0 && !MaterialPool.For(player).TrySpend(SparkCost(sparks)))
        {
            problem = "Not enough Portal Sparks.";
            return false;
        }

        HavenReadiness.GiveRest(player);

        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in chosen)
        {
            inventory.Container.Remove(item, 1);
            modifiers.Add(item.RaidModifier);
        }

        // The seed is usually rolled when the briefing opens, so the hour it shows is the hour the raid gets.
        int runSeed = seed ?? RaidSession.NewSeed();
        RaidParameters parameters = RaidParameters.Calculate(raid, modifiers, ExtraSeconds(sparks), RaidTimeOfDay.For(raid, runSeed), difficulty, ExtractionCut);
        if (player.TryGetComponent(out PlayerTransport transport))
        {
            transport.LeaveForRaid();
        }

        PlayerSnapshot loadout = PlayerSnapshot.Capture(player);
        var session = new RaidSession(raid, modifiers, parameters, loadout, SceneManager.GetActiveScene().name, sparks, runSeed, difficulty)
        {
            GearScoreAtEntry = GearScore.Of(player),
        };
        GameSession.BeginRaid(session);
        GameSession.CarryPlayerState(loadout);

        // The player stays in this scene until the raid scene has loaded, so keep them from wandering off meanwhile.
        if (player.TryGetComponent(out PlayerInputHandler input))
        {
            input.enabled = false;
        }

        SceneLoader.Load(raid.SceneName, $"Through the portal: {raid.DisplayName}");
        return true;
    }
}