using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mending what a breach broke (GDD §6.9: limited repair costs): all of it at once, at the portal, for a share of what the
/// damaged part of each building cost to build. So one bad night can't leave a home that can't be rebuilt.
/// </summary>
public static class BuildingRepairs
{
    /// <summary>What repairing part of a cost comes to: never nothing for a building that is hurt and cost something.</summary>
    // A carpenter makes mending cheaper.
    private static float MendShare => WardBreachSettings.Current.RepairShare
        * Mathf.Clamp01(1f - GameSession.Here.JobBonus(job => job.BuildingRepairDiscount))
        * (Techniques.Has(PerkTechnique.PatchUp) ? TechniqueSettings.Current.PatchUpCost : 1f);

    /// <summary>The share of a damaged part's cost that mending it takes now (a carpenter and Patch Up lower it).</summary>
    public static float CurrentShare => MendShare;

    public static int Share(int amount, float missing, float share) =>
        amount <= 0 || missing <= 0f || share <= 0f ? 0 : Mathf.CeilToInt(amount * Mathf.Clamp01(missing) * share);

    /// <summary>The buildings standing hurt in the loaded scene.</summary>
    public static List<Damageable> Damaged()
    {
        var damaged = new List<Damageable>();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building.TryGetComponent(out Damageable health) && health.IsAlive && health.HealthFraction < 0.999f)
            {
                damaged.Add(health);
            }
        }

        return damaged;
    }

    /// <summary>What mending them all costs together.</summary>
    public static void Cost(IEnumerable<Damageable> damaged, float share, out List<ResourceAmount> resources, out List<ItemAmount> items)
    {
        var owedResources = new Dictionary<ResourceDefinition, int>();
        var owedItems = new Dictionary<ItemDefinition, int>();
        foreach (Damageable health in damaged)
        {
            BuildingDefinition building = health.GetComponent<BuildingObject>().Definition;
            // Iron Bands and the like make mending dearer (mendCostShare): what they gain in health they cost in repairs.
            float missing = 1f - health.HealthFraction;
            float dearer = share * (1f + (health.TryGetComponent(out Construction construction) ? construction.Sum(bonus => bonus.mendCostShare) : 0f));
            foreach (ResourceAmount cost in building.ConstructionCost)
            {
                owedResources.TryGetValue(cost.Resource, out int owed);
                owedResources[cost.Resource] = owed + Share(cost.Amount, missing, dearer);
            }

            foreach (ItemAmount cost in building.ConstructionItems)
            {
                owedItems.TryGetValue(cost.Item, out int owed);
                owedItems[cost.Item] = owed + Share(cost.Amount, missing, dearer);
            }
        }

        resources = new List<ResourceAmount>(owedResources.Count);
        foreach (KeyValuePair<ResourceDefinition, int> owed in owedResources)
        {
            resources.Add(new ResourceAmount(owed.Key, owed.Value));
        }

        items = new List<ItemAmount>(owedItems.Count);
        foreach (KeyValuePair<ItemDefinition, int> owed in owedItems)
        {
            items.Add(new ItemAmount(owed.Key, owed.Value));
        }
    }

    /// <summary>The choice to mend everything, for a menu; null when nothing is hurt.</summary>
    public static MenuAction MendAction(GameObject user)
    {
        List<Damageable> damaged = Damaged();
        if (damaged.Count == 0)
        {
            return null;
        }

        Cost(damaged, MendShare, out List<ResourceAmount> resources, out List<ItemAmount> items);
        MaterialPool pool = MaterialPool.For(user);
        bool affordable = pool.CanAfford(resources, items, out string missing);
        string costs = resources.Count > 0 ? AmountText.Describe(resources) + (items.Count > 0 ? ", " : "") : "";
        string detail = $"{damaged.Count} damaged: {costs}{(items.Count > 0 ? AmountText.Describe(items) : "")}";
        string lacking = affordable ? "" : $"   <color={UIStyle.Hex(UIStyle.Current.Danger)}>{missing}</color>";
        // Not in the middle of a fight for the Haven (the user, 2026-11-16): every wall made whole at once mid-breach.
        if (UnderAttack)
        {
            return new MenuAction("Mend the clearing", "Not while the Haven is under attack", false, () => { });
        }

        return new MenuAction("Mend the clearing", detail + lacking, affordable, () => Mend(user));
    }

    private static bool UnderAttack => (WardBreachDirector.Current != null && WardBreachDirector.Current.IsUnderWay)
                                       || (CorruptionNights.Current != null && CorruptionNights.Current.IsActive);

    private static void Mend(GameObject user)
    {
        if (UnderAttack)
        {
            GameMessages.Post("Not while the Haven is under attack.", MessageKind.Warning);
            return;
        }

        List<Damageable> damaged = Damaged();
        Cost(damaged, MendShare, out List<ResourceAmount> resources, out List<ItemAmount> items);
        if (!MaterialPool.For(user).TrySpend(resources, items))
        {
            return;
        }

        foreach (Damageable health in damaged)
        {
            health.Heal(health.MaxHealth);
        }

        GameMessages.Post($"Mended {damaged.Count} building{(damaged.Count == 1 ? "" : "s")}.", MessageKind.Gain);
        if (Techniques.Has(PerkTechnique.PatchUp))
        {
            Techniques.Used(PerkTechnique.PatchUp);
        }
        AudioManager.Play(AudioManager.Sounds?.Repaired, user.transform.position);
    }
}
