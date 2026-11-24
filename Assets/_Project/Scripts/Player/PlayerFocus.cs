using System;
using UnityEngine;

/// <summary>
/// Focus: what fighting well earns and skills spend. Landing hits builds it (finishers and heavy swings more, counters
/// more still), a parry or a perfect dodge builds a lot, blocking a little. It is kept in circles (the user, 2026-11-24): when one
/// fills it lights above the skill bar and stays lit, never draining, until an ability, a spell or a weapon art spends it; up to
/// three are kept. Only the circle still filling holds for a while after the last gain and then drains away. The player is made
/// anew with each scene, so the circles start empty on entering or leaving a raid. Rules are in
/// <see cref="CombatTechniqueSettings"/> (Focus).
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public sealed class PlayerFocus : MonoBehaviour
{
    private PlayerCombat combat;
    private PlayerGuard guard;
    private Combatant combatant;
    private float lastGain = float.NegativeInfinity;

    /// <summary>All the Focus held: the full circles' and the one filling.</summary>
    public float Current { get; private set; }
    public float Max => Rules.FocusMax;
    public float PerCircle => Rules.FocusPerCircle;
    public int MaxCircles => Rules.FocusCircles;

    /// <summary>Full circles, ready to spend.</summary>
    public int Circles => CirclesIn(Current, PerCircle, MaxCircles);

    /// <summary>How full the next circle is, 0-1: 1 once every circle is full.</summary>
    public float Filling => FillingOf(Current, PerCircle, MaxCircles);

    public bool HasCircle => Circles > 0;

    /// <summary>Raised when Focus changes. Arguments: current, max.</summary>
    public event Action<float, float> FocusChanged;

    /// <summary>Raised when something asks for more Focus than there is.</summary>
    public event Action SpendRefused;

    private CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        guard = GetComponent<PlayerGuard>();
        combatant = GetComponent<Combatant>();
    }

    // An arrow that strikes earns as a swing does, and a perfect release as a finisher: an archer has skills to pay for too.
    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        WeaponDefinition weapon = combat.Weapon;
        if (weapon != null && weapon.IsBow && damage.Attack == weapon.ShotAttack && result.Damage > 0f && !damage.IsOverTime)
        {
            Gain(Rules.FocusPerHit * (result.IsCritical ? 2f : 1f));
        }
    }

    private void OnEnable()
    {
        combat.SwingLanded += OnSwingLanded;
        combat.PerfectDodge += OnPerfectDodge;
        if (combatant != null)
        {
            combatant.DamageDealt += OnDamageDealt;
        }

        if (guard != null)
        {
            guard.Parried += OnParried;
            guard.Blocked += OnBlocked;
        }
    }

    private void OnDisable()
    {
        combat.SwingLanded -= OnSwingLanded;
        combat.PerfectDodge -= OnPerfectDodge;
        if (combatant != null)
        {
            combatant.DamageDealt -= OnDamageDealt;
        }

        if (guard != null)
        {
            guard.Parried -= OnParried;
            guard.Blocked -= OnBlocked;
        }
    }

    // Only the circle still filling drains; full circles are kept.
    private void Update()
    {
        float kept = Circles * PerCircle;
        if (Current > kept && Time.time - lastGain > Rules.FocusHoldSeconds)
        {
            Set(Mathf.Max(kept, Current - Rules.FocusDrainPerSecond * Time.deltaTime));
        }
    }

    /// <summary>Full circles in that much Focus.</summary>
    public static int CirclesIn(float focus, float perCircle, int maxCircles) =>
        perCircle > 0f ? Mathf.Clamp(Mathf.FloorToInt((focus + 0.001f) / perCircle), 0, maxCircles) : 0;

    /// <summary>How full the circle after the full ones is, 0-1; 1 when all are full.</summary>
    public static float FillingOf(float focus, float perCircle, int maxCircles)
    {
        int full = CirclesIn(focus, perCircle, maxCircles);
        return full >= maxCircles ? 1f : perCircle > 0f ? Mathf.Clamp01((focus - full * perCircle) / perCircle) : 0f;
    }

    /// <summary>Focus a swing earns: per target hit (two at most), more for finishers, heavy swings and counters.</summary>
    public static float ForSwing(SwingInfo swing, int hits, CombatTechniqueSettings rules)
    {
        if (hits <= 0 || swing.Kind == SwingKind.ShieldBash || swing.Kind == SwingKind.Art)
        {
            return 0f;
        }

        float gain = rules.FocusPerHit * Mathf.Min(hits, 2);
        if (swing.IsFinisher)
        {
            gain *= 2f;
        }
        else if (swing.Kind == SwingKind.Heavy || swing.Kind == SwingKind.GuardCounter)
        {
            gain *= 1.5f;
        }

        return swing.IsCounter ? gain * 1.5f : gain;
    }

    private void OnSwingLanded(SwingInfo swing, int hits) => Gain(ForSwing(swing, hits, Rules));

    private void OnParried(DamageInfo damage) => Gain(Rules.FocusOnParry);

    private void OnPerfectDodge(DamageInfo damage) => Gain(Rules.FocusOnPerfectDodge);

    private void OnBlocked(DamageInfo damage) => Gain(Rules.FocusOnBlock);

    public void Gain(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        lastGain = Time.time;
        Set(Current + amount);
    }

    /// <summary>
    /// Spends one full circle, keeping what the next had filled; with none full, spends nothing and raises <see cref="SpendRefused"/>.
    /// Returns the Focus spent (0 when refused).
    /// </summary>
    public float TrySpendCircle() => TrySpendCircles(1);

    /// <summary>Spends that many full circles at once, or none (raising <see cref="SpendRefused"/>). Returns the Focus spent.</summary>
    public float TrySpendCircles(int count)
    {
        if (count <= 0)
        {
            return 0f;
        }

        if (Circles < count)
        {
            SpendRefused?.Invoke();
            return 0f;
        }

        float spent = PerCircle * count;
        Set(Current - spent);
        return spent;
    }

    private void Set(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, Max);
        if (Mathf.Approximately(clamped, Current))
        {
            return;
        }

        Current = clamped;
        FocusChanged?.Invoke(Current, Max);
    }
}
