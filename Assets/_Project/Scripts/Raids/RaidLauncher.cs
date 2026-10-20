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

        if (raid.IsLocked)
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
    public static bool TryLaunch(RaidDefinition raid, GameObject player, IEnumerable<ItemDefinition> modifierItems, out string problem, int sparks = 0)
    {
        problem = CheckLaunch(raid);
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

        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in chosen)
        {
            inventory.Container.Remove(item, 1);
            modifiers.Add(item.RaidModifier);
        }

        RaidParameters parameters = RaidParameters.Calculate(raid, modifiers, sparks * SettlementSettings.Current.SecondsPerSpark);
        PlayerSnapshot loadout = PlayerSnapshot.Capture(player);
        GameSession.BeginRaid(new RaidSession(raid, modifiers, parameters, loadout, SceneManager.GetActiveScene().name, sparks));
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