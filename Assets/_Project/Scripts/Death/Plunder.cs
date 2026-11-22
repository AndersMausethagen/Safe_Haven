using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>What a lost breach's band carried off, and which of them carries it (SettlementState.Plunder, saved).</summary>
public sealed class PlunderRecord
{
    /// <summary>The band's leader, by monster id: it walks the next raid with what was taken.</summary>
    public string Leader;
    public readonly List<ItemAmount> Items = new List<ItemAmount>();

    public string Describe() => string.Join(", ", Items.Select(item => $"{item.Amount} {item.Item.DisplayName}"));
}

/// <summary>
/// A lost ward breach plundered (the death rules, DeathRuleSet › Plunder; Docs/DeathPenaltyResearch_2026-11-22.md option D): the band
/// carries off a share of the stash's most valuable material stacks (never gear, tools, unique or quest items), at most so many of
/// each. Its leader walks the next raid with it, out of sight of the way in (PlunderCarrier); laying it low gives it all back.
/// Buildings and people are never taken.
/// </summary>
public static class Plunder
{
    /// <summary>
    /// What the band takes from <paramref name="stash"/>: <paramref name="share"/> of each of the <paramref name="stacks"/> most valuable
    /// material stacks (by value of the whole stack), rounded up, at most <paramref name="most"/>. Pure, apart from taking it.
    /// </summary>
    public static List<ItemAmount> Take(ItemContainer stash, float share, int stacks, int most)
    {
        var taken = new List<ItemAmount>();
        if (stash == null || share <= 0f || stacks <= 0 || most <= 0)
        {
            return taken;
        }

        var totals = new Dictionary<ItemDefinition, int>();
        for (int i = 0; i < stash.SlotCount; i++)
        {
            ItemStack stack = stash.GetSlot(i);
            if (!stack.IsEmpty && Takeable(stack.Item))
            {
                totals.TryGetValue(stack.Item, out int count);
                totals[stack.Item] = count + stack.Count;
            }
        }

        foreach (KeyValuePair<ItemDefinition, int> worth in totals.OrderByDescending(pair => pair.Key.Value * pair.Value).Take(stacks))
        {
            int amount = Mathf.Min(most, Mathf.CeilToInt(worth.Value * share));
            int removed = amount > 0 ? stash.Remove(worth.Key, amount) : 0;
            if (removed > 0)
            {
                taken.Add(new ItemAmount(worth.Key, removed));
            }
        }

        return taken;
    }

    /// <summary>What a band can carry off: plain materials, nothing worn, unique or wanted by a quest.</summary>
    public static bool Takeable(ItemDefinition item) =>
        item != null && item.Category == ItemCategory.Material && !item.HasDurability && item.Value > 0;
}

/// <summary>
/// The band's leader in a raid, carrying a lost breach's plunder (SettlementState.Plunder): made by the RaidDirector as the raid begins,
/// well away from the way in. Laid low, it drops everything it carried at its feet and the plunder is cleared; if the player falls
/// or leaves first, it walks the next raid again.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlunderCarrier : MonoBehaviour
{
    private const float Nearest = 60f;
    private const float Farthest = 130f;

    private Damageable health;

    /// <summary>The carrier in this raid, or null.</summary>
    public static PlunderCarrier Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Brings the band's leader into <paramref name="raid"/> when one carries plunder. Host only, not in a boss encounter.</summary>
    public static void Summon(RaidDirector raid, Vector3 from)
    {
        PlunderRecord plunder = GameSession.Home.Plunder;
        if (raid == null || plunder == null || raid.Session.Definition.IsEncounter || CreatureSource.Remote != null)
        {
            return;
        }

        MonsterDefinition leader = JournalSettings.Current.FindCreature(plunder.Leader);
        if (leader == null || leader.Prefab == null)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(raid.Session.Seed, "The Band's Leader"));
        if (!RaidSpots.TryFindSpot(from, Nearest, Farthest, random, new NavMeshPath(), out Vector3 spot, 24))
        {
            return;
        }

        GameObject root = Instantiate(leader.Prefab, spot, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
        foreach (MonsterAI monster in root.GetComponentsInChildren<MonsterAI>())
        {
            monster.ResetHome();
        }

        raid.Creatures.Prepare(root, false, true);
        Current = root.AddComponent<PlunderCarrier>();
        GameMessages.Post($"The {leader.DisplayName} that led the attack on your Haven is somewhere in these woods, carrying {plunder.Describe()}.", MessageKind.Warning);
    }

    private void Awake() => health = GetComponent<Damageable>();

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    // Everything it carried, at its feet; the Haven's plunder is cleared.
    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        PlunderRecord plunder = GameSession.Home.Plunder;
        if (plunder == null)
        {
            return;
        }

        foreach (ItemAmount item in plunder.Items)
        {
            DroppedItem.Drop(transform, new ItemStack(item.Item, item.Amount));
        }

        GameMessages.Post($"It falls, and what it took from your stores spills out: {plunder.Describe()}.", MessageKind.Gain);
        PlaytestLogger.Note(PlaytestLog.Plundered, "Recovered", plunder.Items.Sum(item => item.Amount), plunder.Describe());
        GameSession.Home.SetPlunder(null);
    }
}
