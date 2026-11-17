using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What makes a raid a boss encounter (Docs/BossEncounterResearch_2026-11-17.md, option C, with the user's decisions): one boss in a
/// small hand-built arena, opened by the story, beyond a gate of mist with no way back but winning or dying. The raid it hangs on
/// (RaidDefinition › Encounter) stays the destination, so launching, saving, the results, co-op and quests are the raid's own; the
/// arena scene's <see cref="EncounterArena"/> runs the fight. Read only while playing.
/// </summary>
[CreateAssetMenu(fileName = "BossEncounter", menuName = "Safe Haven/Raids/Boss Encounter")]
public sealed class BossEncounterDefinition : ScriptableObject
{
    [Header("The boss")]
    [SerializeField] private BossDefinition boss;
    [Tooltip("The HUD's line before the gate, and told on arrival: \"Beyond the mist: the Brukam.\" Empty: made from the boss's name.")]
    [SerializeField] private string arrivalLine;

    [Header("Opening")]
    [Tooltip("The encounter is open once this holds (the story's way there: a quest started). Empty: as soon as the raid itself is.")]
    [SerializeField] private QuestCondition opensWhen;
    [Tooltip("The tier of the first fight, whatever the raid offers. Once the boss has fallen, rematches come at the raid's own tiers.")]
    [SerializeField, Range(1, Tiers.Max)] private int firstTier = 1;
    [Tooltip("Hours of real time after a kill before the boss can be fought again (the user: \"only ever 24 hours after a completed " +
             "kill, so the player can't spam it\"). 0: at once.")]
    [SerializeField, Min(0f)] private float rematchHours = 24f;

    [Header("The gate")]
    [Tooltip("The gate's prompt.")]
    [SerializeField] private string gateVerb = "Step through the mist";
    [Tooltip("What the gate says before the player commits; what is at stake is added under it.")]
    [SerializeField, TextArea] private string gateText = "Beyond the mist there is no way back but through it.";
    [Tooltip("Materials paid at the gate, every try: an offering or a toll. Empty: free.")]
    [SerializeField] private List<ResourceAmount> gateResources = new List<ResourceAmount>();
    [Tooltip("Items paid at the gate, every try. Empty: none.")]
    [SerializeField] private List<ItemAmount> gateItems = new List<ItemAmount>();

    [Header("The fight's clock")]
    [Tooltip("Seconds from the gate until the boss enrages: the fight's clock, shown on the HUD (a damage check). The user: design an " +
             "encounter to last about ten minutes in all.")]
    [SerializeField, Min(30f)] private float enrageSeconds = 480f;
    [Tooltip("Damage the boss adds when it enrages, as a share (0.5 = +50%).")]
    [SerializeField, Min(0f)] private float enrageDamage = 0.5f;
    [Tooltip("Damage it adds again every Enrage Interval while it stays enraged, as a share.")]
    [SerializeField, Min(0f)] private float enrageRamp = 0.15f;
    [Tooltip("Seconds between the enraged boss's further damage steps.")]
    [SerializeField, Min(5f)] private float enrageInterval = 30f;
    [Tooltip("Told when it enrages. Empty: made from the boss's name.")]
    [SerializeField] private string enrageLine;

    [Header("Failing")]
    [Tooltip("Durability each piece of deployed equipment loses when the encounter fails (the user: 10%). An expedition's is Durability " +
             "Settings › Loss On Failed Raid.")]
    [SerializeField, Range(0f, 1f)] private float wearOnFailure = 0.1f;

    public BossDefinition Boss => boss;
    public string BossName => boss != null ? boss.DisplayName : name;
    public string ArrivalLine => string.IsNullOrWhiteSpace(arrivalLine) ? $"Beyond the mist: {BossName}." : arrivalLine;
    public QuestCondition OpensWhen => opensWhen;
    public int FirstTier => firstTier;
    public float RematchHours => rematchHours;
    public string GateVerb => string.IsNullOrWhiteSpace(gateVerb) ? "Step through the mist" : gateVerb;
    public string GateText => gateText;
    public IReadOnlyList<ResourceAmount> GateResources => gateResources;
    public IReadOnlyList<ItemAmount> GateItems => gateItems;
    public bool HasGateCost => gateResources.Count > 0 || gateItems.Count > 0;
    public float EnrageSeconds => enrageSeconds;
    public float EnrageDamage => enrageDamage;
    public float EnrageRamp => enrageRamp;
    public float EnrageInterval => enrageInterval;
    public string EnrageLine => string.IsNullOrWhiteSpace(enrageLine) ? $"{BossName} is enraged: its patience is gone." : enrageLine;
    public float WearOnFailure => wearOnFailure;

    /// <summary>True once the story has opened the encounter (Opens When), for this player's quest log.</summary>
    public bool IsOpenBy(QuestLog log) => opensWhen.IsMetBy(log);

    /// <summary>True when the boss has fallen in this Haven's story (the first fight is behind them).</summary>
    public bool IsBeatenIn(IHavenView haven) => boss != null && haven != null && haven.IsBossDefeated(boss.Id);

    /// <summary>
    /// How long until a rematch, from the last kill (UTC) and now (UTC): zero when the boss can be fought, as it can before any kill.
    /// </summary>
    public TimeSpan RematchWait(DateTime? lastKill, DateTime now)
    {
        if (lastKill == null || rematchHours <= 0f)
        {
            return TimeSpan.Zero;
        }

        TimeSpan wait = lastKill.Value.AddHours(rematchHours) - now;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    /// <summary>The difficulty a launch uses: the first fight's tier until the boss has fallen, then the one the player chose.</summary>
    public RaidDifficulty DifficultyFor(RaidDefinition raid, bool beaten, RaidDifficulty chosen)
    {
        if (beaten || raid == null)
        {
            return chosen;
        }

        foreach (RaidDifficulty difficulty in raid.Difficulties)
        {
            if (difficulty != null && difficulty.tier == firstTier)
            {
                return difficulty;
            }
        }

        return chosen;
    }

    /// <summary>"18 h 20 min", "40 min": a wait in words.</summary>
    public static string Describe(TimeSpan wait)
    {
        int hours = (int)wait.TotalHours;
        int minutes = Mathf.Max(1, (int)Math.Ceiling(wait.TotalMinutes - hours * 60));
        if (minutes == 60)
        {
            hours++;
            minutes = 0;
        }

        return hours > 0 ? (minutes > 0 ? $"{hours} h {minutes} min" : $"{hours} h") : $"{minutes} min";
    }
}
