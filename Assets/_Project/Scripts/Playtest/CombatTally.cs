using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counts how the player fights, for the playtest log (PlaytestLogger writes it as Combat rows each played minute): swings by
/// kind, blows landed and whiffed, parries, blocks, broken guards, dodges and perfect dodges, flinches and the swings they cost,
/// counters, backstabs, interrupts. It only listens to the player's own events, so no fighting code knows it exists. Docs/CombatResearch.md
/// §3.3 #10: measure the loop (how much of a fight is parrying) before and after changing it.
/// </summary>
public sealed class CombatTally
{
    public const string LightSwing = "Swing.Light", HeavySwing = "Swing.Heavy", RushSwing = "Swing.Rush", ArtSwing = "Swing.Art", BashSwing = "Swing.ShieldBash",
        GuardCounterSwing = "Swing.GuardCounter";
    public const string Landed = "Landed", Whiffed = "Whiffed", Parry = "Parry", Block = "Block", GuardBroken = "GuardBroken";
    public const string Dodge = "Dodge", PerfectDodge = "PerfectDodge", Flinch = "Flinch", LostSwing = "LostSwing", Counter = "Counter";
    public const string Backstab = "Backstab", Finisher = "FinishingBlow", Interrupt = "Interrupt", Hurt = "Hurt";

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private PlayerCombat combat;
    private PlayerGuard guard;
    private PlayerDodge dodge;
    private Damageable health;

    /// <summary>The weapon the player held when the last count was made (the rows' detail).</summary>
    public string Weapon { get; private set; } = "";

    public IReadOnlyDictionary<string, int> Counts => counts;

    public void Note(string what, int amount = 1)
    {
        if (amount <= 0 || string.IsNullOrEmpty(what))
        {
            return;
        }

        counts.TryGetValue(what, out int sum);
        counts[what] = sum + amount;
        Weapon = combat != null && combat.Weapon != null ? combat.Weapon.name : "";
    }

    public void Clear() => counts.Clear();

    /// <summary>Listens to this player (null stops listening).</summary>
    public void Follow(GameObject player)
    {
        Unfollow();
        if (player == null)
        {
            return;
        }

        combat = player.GetComponent<PlayerCombat>();
        guard = player.GetComponent<PlayerGuard>();
        dodge = player.GetComponent<PlayerDodge>();
        health = player.GetComponent<Damageable>();
        if (combat != null)
        {
            combat.SwingStarted += OnSwingStarted;
            combat.SwingLanded += OnSwingLanded;
            combat.Flinched += OnFlinched;
            combat.PerfectDodge += OnPerfectDodge;
            combat.Backstabbed += OnBackstabbed;
            combat.FinishStarted += OnFinishStarted;
        }

        if (guard != null)
        {
            guard.Parried += OnParried;
            guard.Blocked += OnBlocked;
            guard.Broken += OnBroken;
        }

        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        if (health != null)
        {
            health.Damaged += OnHurt;
        }

        MonsterAI.AnyInterrupted += OnInterrupted;
    }

    public void Unfollow()
    {
        if (combat != null)
        {
            combat.SwingStarted -= OnSwingStarted;
            combat.SwingLanded -= OnSwingLanded;
            combat.Flinched -= OnFlinched;
            combat.PerfectDodge -= OnPerfectDodge;
            combat.Backstabbed -= OnBackstabbed;
            combat.FinishStarted -= OnFinishStarted;
        }

        if (guard != null)
        {
            guard.Parried -= OnParried;
            guard.Blocked -= OnBlocked;
            guard.Broken -= OnBroken;
        }

        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (health != null)
        {
            health.Damaged -= OnHurt;
        }

        MonsterAI.AnyInterrupted -= OnInterrupted;
        combat = null;
        guard = null;
        dodge = null;
        health = null;
    }

    /// <summary>The count a swing of this kind is kept under.</summary>
    public static string SwingName(SwingKind kind) => kind switch
    {
        SwingKind.Heavy => HeavySwing,
        SwingKind.Rush => RushSwing,
        SwingKind.Art => ArtSwing,
        SwingKind.ShieldBash => BashSwing,
        SwingKind.GuardCounter => GuardCounterSwing,
        _ => LightSwing,
    };

    private void OnSwingStarted(SwingInfo swing)
    {
        Note(SwingName(swing.Kind));
        if (swing.IsCounter)
        {
            Note(Counter);
        }
    }

    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        if (swing.Attack != null && (swing.Attack.Motion == ArtMotion.Throw || swing.Attack.CastProjectile != null))
        {
            return;
        }

        if (hits > 0) Note(Landed, hits);
        else Note(Whiffed);
    }

    private void OnFlinched(float seconds, bool lostSwing)
    {
        Note(Flinch);
        if (lostSwing) Note(LostSwing);
    }

    private void OnPerfectDodge(DamageInfo damage) => Note(PerfectDodge);
    private void OnBackstabbed(IDamageable target) => Note(Backstab);
    private void OnFinishStarted(MonsterAI monster) => Note(Finisher);
    private void OnParried(DamageInfo damage) => Note(Parry);
    private void OnBlocked(DamageInfo damage) => Note(Block);
    private void OnBroken(float seconds) => Note(GuardBroken);
    private void OnDodged(Vector3 direction) => Note(Dodge);

    private void OnHurt(DamageInfo damage, DamageResult result)
    {
        if (!result.Blocked && result.Damage > 0f && !damage.IsOverTime)
        {
            Note(Hurt);
        }
    }

    // Only the player's own blows count: a companion's hit cancelling a wind-up is not the player trading.
    private void OnInterrupted(MonsterAI monster, Combatant by)
    {
        if (combat != null && by != null && by.gameObject == combat.gameObject)
        {
            Note(Interrupt);
        }
    }
}
