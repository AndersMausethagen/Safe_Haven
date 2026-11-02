using System;
using UnityEngine;

/// <summary>
/// One difficulty a raid can be entered at, picked at the portal: its tier, the gear score it expects, how much stronger the creatures
/// are and how far up the tiers its loot reaches. What spawns at each tier is set on each entry of the raid's spawn lists and on the
/// map's trees (From Tier, To Tier). Tiers mean what they mean everywhere (Docs/BaseProgression.md): a raid of tier N hands over what
/// the gear of tier N+1 is made from, and expects the player to wear about tier N. Authored data; never changes while playing.
/// </summary>
[Serializable]
public sealed class RaidDifficulty
{
    [Tooltip("Which tier this is. Each tier may appear once in a raid.")]
    [Min(1)] public int tier = 1;
    [Tooltip("The gear score this tier is balanced for, shown to the player at the portal.")]
    [Min(0)] public int recommendedGearScore = 10;
    [Tooltip("The portal refuses anyone below this score, so nobody strips down and runs for the top tier's loot. 0: anyone may go.")]
    [Min(0)] public int minimumGearScore;
    [Tooltip("Multiplies the enemies' health. Set so that the time to kill stays the same at the recommended score: the Rules tab's table shows it.")]
    [Min(0.1f)] public float enemyHealth = 1f;
    [Tooltip("Multiplies the enemies' damage. Set so that the hits it takes to die stay the same at the recommended score.")]
    [Min(0f)] public float enemyPower = 1f;
    [Tooltip("Multiplies how many creatures the raid places.")]
    [Min(0f)] public float enemyDensity = 1f;
    [Tooltip("Multiplies what caches and events hold.")]
    [Min(0f)] public float loot = 1f;
    [Tooltip("The highest tier of gear and material that can drop here. 0: one above this tier, so a raid pays for the next tier's gear.")]
    [Min(0)] public int dropsUpToTier;
    [Tooltip("Told in the briefing: what is new at this tier.")]
    [TextArea] public string briefing;

    [Header("Elites")]
    [Tooltip("How many creatures a run starts with as elites, least and most.")]
    public Vector2Int eliteCount = Vector2Int.zero;
    [Tooltip("How many affixes an elite gets at this tier.")]
    [Min(1)] public int eliteAffixes = 1;
    [Tooltip("Chance that a group of hunters sent later brings an elite.")]
    [Range(0f, 1f)] public float hunterEliteChance;
    [Tooltip("Chance that killing an elite also gives a relic.")]
    [Range(0f, 1f)] public float eliteRelicChance;
    [Tooltip("What killing an elite gives, on top of what the creature drops. Best kept to this tier's prize: what the next tier's gear is made from.")]
    public ItemDropTable eliteLoot = new ItemDropTable();

    public int DropCap => dropsUpToTier > 0 ? dropsUpToTier : tier + 1;

    /// <summary>
    /// Whether this tier can hand over the item. Only what climbs the tiers is capped (gear, tools, materials): a troll's Trollstone is
    /// for those who meet it at a tier that can use it. Food and potions come at any tier.
    /// </summary>
    public bool Allows(ItemDefinition item)
    {
        bool ladder = item.Category == ItemCategory.Material || item.Category == ItemCategory.Equipment || item.Category == ItemCategory.Tool;
        return !ladder || item.Tier <= DropCap;
    }
}
