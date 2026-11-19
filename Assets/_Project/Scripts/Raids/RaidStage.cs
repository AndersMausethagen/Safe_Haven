using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One step of a raid's escalation (GDD §7.3 "final danger phase"): when the clock falls to <see cref="StartsAtSecondsLeft"/>,
/// the player is warned, enemies get stronger and spawn groups waiting for this stage appear. Thresholds are time
/// remaining, so Portal Sparks give more calm time first instead of secretly speeding the danger up.
/// </summary>
[Serializable]
public sealed class RaidStage
{
    /// <summary>What the stage shows the player to stay for (StageReveal).</summary>
    public enum RevealKind
    {
        None,
        /// <summary>A cache glints beyond the player: the first temptation.</summary>
        Glint,
        /// <summary>A hoard lies open further on: the last.</summary>
        Hoard,
    }

    [SerializeField] private string displayName = "Stage";
    [Tooltip("The stage begins when this many seconds are left on the raid clock.")]
    [SerializeField, Min(1f)] private float startsAtSecondsLeft = 180f;
    [Tooltip("Shown to the player when the stage begins.")]
    [SerializeField, TextArea] private string message;
    [Tooltip("Extra enemy health while this stage lasts, on top of the raid's own rules. 0.2 is +20%.")]
    [SerializeField, Min(0f)] private float enemyHealthBonus;
    [Tooltip("Extra enemy physical and spell power while this stage lasts. 0.2 is +20%.")]
    [SerializeField, Min(0f)] private float enemyPowerBonus;
    [Tooltip("Extra loot while this stage lasts: what caches and events hold, and the essence creatures drop. 0.25 is +25%. " +
             "What staying on is worth, against the stronger creatures.")]
    [SerializeField, Min(0f)] private float lootBonus;
    [Header("Music (Docs/GameDesignResearch_2026-11-17.md T1)")]
    [Tooltip("Played as the stage begins: a horn far off as the mist stirs, nearer and lower as the hunt begins. Empty: none.")]
    [SerializeField] private SoundDefinition stinger;
    [Tooltip("Seconds the exploring music holds its breath after the stage begins, leaving the ambience: the quiet before the hunt, " +
             "so footfalls and crows are the music. A fight's music still plays. 0: none.")]
    [SerializeField, Min(0f)] private float hushSeconds;
    [Tooltip("A cache this stage reveals 70-140 m from the player, away from the exits, marked on the minimap (StageReveal): a reason " +
             "to stay (Docs/SystemsGapReview_2026-11-19.md §4). None: nothing.")]
    [SerializeField] private RevealKind reveal;
    [Tooltip("How much more the revealed cache holds: 1.5 is +50%, on top of the stage's own loot.")]
    [SerializeField, Min(1f)] private float revealLoot = 1.5f;
    [Tooltip("The cache placed (a prefab with a Loot Cache).")]
    [SerializeField] private GameObject revealCache;

    public RaidStage() { }

    public RaidStage(string displayName, float startsAtSecondsLeft, float enemyHealthBonus = 0f, float enemyPowerBonus = 0f, string message = null)
    {
        this.displayName = displayName;
        this.startsAtSecondsLeft = startsAtSecondsLeft;
        this.enemyHealthBonus = enemyHealthBonus;
        this.enemyPowerBonus = enemyPowerBonus;
        this.message = message;
    }

    public string DisplayName => displayName;
    public SoundDefinition Stinger => stinger;
    public float HushSeconds => hushSeconds;
    public float StartsAtSecondsLeft => startsAtSecondsLeft;
    public string Message => message;
    public float EnemyHealthBonus => enemyHealthBonus;
    public float EnemyPowerBonus => enemyPowerBonus;
    public float LootBonus => lootBonus;
    public RevealKind Reveal => reveal;
    public float RevealLoot => revealLoot;
    public GameObject RevealCache => revealCache;

    public void SetLootBonus(float bonus) => lootBonus = Mathf.Max(0f, bonus);
}

/// <summary>Stage lookups, kept free of scene objects so they can be tested. Stage 0 is the calm before the first one.</summary>
public static class RaidStages
{
    /// <summary>How many stages have begun with this much time left: 0 before the first, stages.Count in the last.</summary>
    public static int IndexAt(IReadOnlyList<RaidStage> stages, float secondsLeft)
    {
        int begun = 0;
        if (stages == null)
        {
            return begun;
        }

        foreach (RaidStage stage in stages)
        {
            if (stage != null && secondsLeft <= stage.StartsAtSecondsLeft)
            {
                begun++;
            }
        }

        return begun;
    }

    /// <summary>The stage with this index (1 is the first), or null for the calm before (0) and out-of-range values.</summary>
    public static RaidStage Get(IReadOnlyList<RaidStage> stages, int index)
    {
        return stages != null && index >= 1 && index <= stages.Count ? stages[index - 1] : null;
    }

    /// <summary>Orders stages by when they begin, earliest (most time left) first, which IndexAt and Get rely on.</summary>
    public static void Sort(List<RaidStage> stages)
    {
        stages?.Sort((a, b) => (b?.StartsAtSecondsLeft ?? 0f).CompareTo(a?.StartsAtSecondsLeft ?? 0f));
    }
}
