using System;
using UnityEngine;

/// <summary>
/// Focus: what fighting well earns and skills spend. Landing hits builds it (finishers and heavy swings more, counters
/// more still), a parry or a perfect dodge builds a lot, blocking a little; it holds for a while after the last gain and
/// then drains away, so it can't be saved up between fights. Skills with a Focus Cost need it on top of their cooldown.
/// Rules are in <see cref="CombatTechniqueSettings"/> (Focus).
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public sealed class PlayerFocus : MonoBehaviour
{
    private PlayerCombat combat;
    private PlayerGuard guard;
    private Combatant combatant;
    private float lastGain = float.NegativeInfinity;

    public float Current { get; private set; }
    public float Max => CombatTechniqueSettings.Current.FocusMax;
    public float Fraction => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;

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

    private void Update()
    {
        if (Current > 0f && Time.time - lastGain > Rules.FocusHoldSeconds)
        {
            Set(Current - Rules.FocusDrainPerSecond * Time.deltaTime);
        }
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
        else if (swing.Kind == SwingKind.Heavy)
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

    public bool CanSpend(float cost) => cost <= 0f || Current >= cost - 0.001f;

    /// <summary>Spends Focus if there is enough; otherwise spends nothing and raises <see cref="SpendRefused"/>.</summary>
    public bool TrySpend(float cost)
    {
        if (!CanSpend(cost))
        {
            SpendRefused?.Invoke();
            return false;
        }

        if (cost > 0f)
        {
            Set(Current - cost);
        }

        return true;
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
