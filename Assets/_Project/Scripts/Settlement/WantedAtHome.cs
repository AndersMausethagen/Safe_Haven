using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the next step at home still needs (Docs/SystemsGapReview_2026-11-19.md §1): the player tracks up to three building levels (H ›
/// Buildings, "Track"), and the portal's next tier is always among them, so there is a goal on the spine from the first day. For each
/// material, what the tracked targets cost less what is home (the bag and the stash) is wanted: the tooltip says so, a pickup on a raid
/// counts it ("Copper Ore 7/10 for the Smithy II"), and the raid's briefing lists it. It reads the costs that exist and adds no
/// goal-giver; nothing is hidden or scaled. Plain C#; the tracked list is the character's (CharacterProgress), the costs the data's.
/// </summary>
public static class WantedAtHome
{
    public const int MostTracked = 3;
    // A pickup's count is said once per material in this many seconds (a stack of ten picked up is one line).
    private const float SaySeconds = 2f;
    private static readonly Dictionary<Object, float> lastSaid = new Dictionary<Object, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => lastSaid.Clear();

    // After GameEvents clears its listeners at the start of a play session (SubsystemRegistration), listen again.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Listen()
    {
        GameEvents.Raised -= OnRaised;
        GameEvents.Raised += OnRaised;
        Construction.Finished -= OnFinished;
        Construction.Finished += OnFinished;
    }

    // A level finished in the player's own Haven is done with: its key goes, so it neither counts on a raid nor holds a slot.
    private static void OnFinished(Construction construction)
    {
        if (construction == null || HavenVisit.IsVisiting || GameSession.Progress == null
            || construction.Definition == null)
        {
            return;
        }

        for (int tier = 1; tier <= construction.Level; tier++)
        {
            GameSession.Progress.RemoveTracked(BuildingKey(construction.Definition, tier));
        }
    }

    // On a raid, a wanted material picked up says how far it has come: "Copper Ore 7/10 for the Smithy II". None once it is all home.
    private static void OnRaised(GameEvent gameEvent)
    {
        if ((gameEvent.Kind != GameEventKind.ItemObtained && gameEvent.Kind != GameEventKind.ResourceGathered) || RaidDirector.Current == null
            || gameEvent.Subject == null || !gameEvent.ByPlayer || Player.Current == null || gameEvent.Actor != Player.Current.gameObject)
        {
            return;
        }

        Object material = gameEvent.Subject;
        if (lastSaid.TryGetValue(material, out float at) && Time.unscaledTime - at < SaySeconds)
        {
            return;
        }

        foreach (Want want in Now(Player.Current))
        {
            if (want.Item == material || want.Resource == material)
            {
                lastSaid[material] = Time.unscaledTime;
                GameMessages.Post($"{want.Name} {want.Have}/{want.Need} for {want.For}", MessageKind.Gain);
                return;
            }
        }
    }

    /// <summary>One material a target still wants.</summary>
    public readonly struct Want
    {
        public readonly ItemDefinition Item;
        public readonly ResourceDefinition Resource;
        public readonly int Have;
        public readonly int Need;
        public readonly string For;

        public Want(ItemDefinition item, ResourceDefinition resource, int have, int need, string target)
        {
            Item = item;
            Resource = resource;
            Have = have;
            Need = need;
            For = target;
        }

        public string Name => Item != null ? Item.DisplayName : Resource != null ? Resource.DisplayName : "";
        public int Short => Mathf.Max(0, Need - Have);
    }

    /// <summary>One target's cost: what it is called and what it takes.</summary>
    public sealed class Target
    {
        public string Name;
        public readonly List<ItemAmount> Items = new List<ItemAmount>();
        public readonly List<ResourceAmount> Resources = new List<ResourceAmount>();
    }

    public static string BuildingKey(BuildingDefinition building, int tier) => building != null ? $"building:{building.Id}:{tier}" : null;

    /// <summary>Tracks a building's level, or stops; true when tracked now.</summary>
    public static bool Toggle(BuildingDefinition building, int tier)
    {
        bool now = GameSession.Progress.ToggleTracked(BuildingKey(building, tier), MostTracked);
        GameMessages.Post(now ? $"Tracking {building.DisplayName} {Tiers.ToRoman(tier)}: what it still needs is marked as wanted." : $"No longer tracking {building.DisplayName} {Tiers.ToRoman(tier)}.", MessageKind.Info);
        return now;
    }

    /// <summary>The targets being gathered for: the portal's next tier, then each tracked building level not yet reached.</summary>
    public static List<Target> Targets(IHavenView settlement, CharacterProgress progress, ContentDatabase content)
    {
        var targets = new List<Target>();
        SettlementSettings.WardLevel ward = PortalTiers.Next(settlement);
        if (ward != null)
        {
            var portal = new Target { Name = $"the portal's tier {Tiers.ToRoman(PortalTiers.Of(settlement) + 1)}" };
            portal.Resources.AddRange(ward.cost);
            targets.Add(portal);
        }

        if (progress == null || content == null)
        {
            return targets;
        }

        foreach (string key in progress.Tracked)
        {
            string[] parts = key.Split(':');
            if (parts.Length != 3 || parts[0] != "building" || !int.TryParse(parts[2], out int tier))
            {
                continue;
            }

            BuildingDefinition building = content.FindBuilding(parts[1]);
            BuildingLevel level = building != null ? building.UpgradeTo(tier) : null;
            // What stands is read from the scene, so only at the player's own home; elsewhere a built level was dropped when it finished.
            bool ownHome = Place.AtHome && !HavenVisit.IsVisiting;
            if (level == null || (ownHome && BuildingUnlocks.HighestTier(building) >= tier))
            {
                continue;
            }

            var target = new Target { Name = $"{building.DisplayName} {Tiers.ToRoman(tier)}" };
            target.Items.AddRange(level.cost);
            target.Resources.AddRange(level.essence);
            targets.Add(target);
        }

        // Plans laid out at home (BuildPlans): what each still needs is wanted, on a raid too (noted on the settlement).
        if (!HavenVisit.IsVisiting)
        {
            foreach (BuildingDefinition plan in GameSession.Home.Plans)
            {
                if (plan == null)
                {
                    continue;
                }

                var target = new Target { Name = $"the planned {plan.DisplayName}" };
                target.Items.AddRange(plan.ConstructionItems);
                target.Resources.AddRange(plan.ConstructionCost);
                targets.Add(target);
            }
        }

        return targets;
    }

    /// <summary>What the targets still want, given what is home (<paramref name="home"/>: the bag and the stash), each material once
    /// with the first target that wants it named.</summary>
    public static List<Want> Compute(IEnumerable<Target> targets, MaterialPool home)
    {
        var needItems = new Dictionary<ItemDefinition, (int need, string first)>();
        var needResources = new Dictionary<ResourceDefinition, (int need, string first)>();
        foreach (Target target in targets)
        {
            foreach (ItemAmount cost in target.Items)
            {
                if (cost.Item != null && cost.Amount > 0)
                {
                    needItems[cost.Item] = needItems.TryGetValue(cost.Item, out var had) ? (had.need + cost.Amount, had.first) : (cost.Amount, target.Name);
                }
            }

            foreach (ResourceAmount cost in target.Resources)
            {
                if (cost.Resource != null && cost.Amount > 0)
                {
                    needResources[cost.Resource] = needResources.TryGetValue(cost.Resource, out var had) ? (had.need + cost.Amount, had.first) : (cost.Amount, target.Name);
                }
            }
        }

        var wants = new List<Want>();
        foreach (KeyValuePair<ItemDefinition, (int need, string first)> pair in needItems)
        {
            int have = home != null ? home.Have(pair.Key) : 0;
            if (have < pair.Value.need)
            {
                wants.Add(new Want(pair.Key, null, have, pair.Value.need, pair.Value.first));
            }
        }

        foreach (KeyValuePair<ResourceDefinition, (int need, string first)> pair in needResources)
        {
            int have = home != null ? home.Have(pair.Key) : 0;
            if (have < pair.Value.need)
            {
                wants.Add(new Want(null, pair.Key, have, pair.Value.need, pair.Value.first));
            }
        }

        return wants;
    }

    // What is home for this player: what they carry and the stash, wherever they are (a raid's pickups count toward home).
    private static MaterialPool Home(Player player)
    {
        ResourceInventory resources = player != null ? player.GetComponent<ResourceInventory>() : null;
        ItemInventory items = player != null ? player.GetComponent<ItemInventory>() : null;
        return new MaterialPool(resources != null ? resources.Storage : null, items != null ? items.Container : null,
            GameSession.Stash != null ? GameSession.Stash.Resources : null, GameSession.Stash != null ? GameSession.Stash.Items : null);
    }

    /// <summary>What this game's player's targets still want now.</summary>
    public static List<Want> Now(Player player)
    {
        if (player == null || GameSession.Progress == null)
        {
            return new List<Want>();
        }

        return Compute(Targets(GameSession.Home, GameSession.Progress, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName)), Home(player));
    }

    /// <summary>The tooltip's line for a material, or null when nothing tracked wants it.</summary>
    public static string LineFor(Object material, Player player)
    {
        foreach (Want want in Now(player))
        {
            if ((want.Item != null && want.Item == material) || (want.Resource != null && want.Resource == material))
            {
                return $"Wanted at home: {want.Short} more for {want.For} ({want.Have}/{want.Need})";
            }
        }

        return null;
    }

    /// <summary>The briefing's line, "Wanted at home: Copper Ore ×4, Wolf Fang ×2", each marked when the raid is likely to give it.</summary>
    public static string BriefingLine(Player player, RaidDefinition raid)
    {
        List<Want> wants = Now(player);
        if (wants.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (Want want in wants)
        {
            bool likely = raid != null && ((want.Item != null && Contains(raid.LikelyItems, want.Item)) || (want.Resource != null && Contains(raid.LikelyResources, want.Resource)));
            parts.Add($"{want.Name} ×{want.Short}{(likely ? " (likely here)" : "")}");
            if (parts.Count == 6)
            {
                break;
            }
        }

        return $"Wanted at home: {string.Join(", ", parts)}.";
    }

    private static bool Contains<T>(IEnumerable<T> list, T one) where T : Object
    {
        if (list == null)
        {
            return false;
        }

        foreach (T entry in list)
        {
            if (entry == one)
            {
                return true;
            }
        }

        return false;
    }
}
