using System;
using UnityEngine;

/// <summary>
/// The player's guard (hold right mouse / left trigger). Hits from the front are blocked: part gets through (less with
/// a shield) and each costs stamina, and without the stamina the guard breaks and the player reels. Raising the guard
/// just before a hit lands parries it: nothing gets through, the attacker staggers and the next swing is a critical
/// counter. Tapping the guard is no free parry: once lowered it can't go up again for a moment. Rules are in
/// <see cref="CombatTechniqueSettings"/>; the decision itself is <see cref="GuardRules.Resolve"/>.
/// <para>
/// Animation, sounds and effects listen to <see cref="GuardChanged"/>, <see cref="Blocked"/>, <see cref="Parried"/> and
/// <see cref="Broken"/>; none of them are known here.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(Damageable))]
public sealed class PlayerGuard : MonoBehaviour, IHitGuard
{
    [Tooltip("Share of a blocked hit that gets through when a shield is held (the weapon's own value applies otherwise).")]
    [SerializeField, Range(0f, 1f)] private float shieldLetThrough = 0.1f;

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerStamina stamina;
    private PlayerDodge dodge;
    private PlayerEating eating;
    private bool held;
    private float raisedAt = float.NegativeInfinity;
    private float loweredAt = float.NegativeInfinity;
    private float stunnedUntil = float.NegativeInfinity;

    /// <summary>True while the guard is up.</summary>
    public bool IsGuarding { get; private set; }

    /// <summary>True while reeling from a broken guard: no attacking, guarding or moving.</summary>
    public bool IsStunned => Time.time < stunnedUntil;

    /// <summary>Raised when the guard goes up (true) or down (false).</summary>
    public event Action<bool> GuardChanged;

    /// <summary>Raised when a hit is caught on the guard, with the hit as it arrived.</summary>
    public event Action<DamageInfo> Blocked;

    /// <summary>Raised when a hit is parried, with the hit as it arrived.</summary>
    public event Action<DamageInfo> Parried;

    /// <summary>Raised when the guard breaks, with how long the player reels.</summary>
    public event Action<float> Broken;

    private CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        stamina = GetComponent<PlayerStamina>();
        dodge = GetComponent<PlayerDodge>();
        eating = GetComponent<PlayerEating>();
    }

    private void OnEnable()
    {
        stateMachine.GuardRequested += OnGuardRequested;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }
    }

    private void OnDisable()
    {
        stateMachine.GuardRequested -= OnGuardRequested;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        held = false;
        SetGuarding(false);
        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }
    }

    // With a bow the guard button aims instead (PlayerArchery).
    private void OnGuardRequested(bool raised) => held = raised && (combat == null || combat.Weapon == null || !combat.Weapon.TakesAttackInput);

    // A dodge drops the guard; holding the button raises it again once the dodge is over.
    private void OnDodged(Vector3 direction) => SetGuarding(false);

    private void Update()
    {
        bool stunned = IsStunned;
        bool canRaise = !stunned && stateMachine.CanMove && (dodge == null || !dodge.IsDodging)
                        && (combat == null || combat.CanGuard) && (eating == null || !eating.IsEating)
                        && Time.time - loweredAt >= Rules.GuardRelock;
        if (held && !IsGuarding && canRaise)
        {
            if (combat != null)
            {
                combat.CancelRecovery();
            }
            SetGuarding(true);
        }
        else if (IsGuarding && (!held || stunned || !stateMachine.CanMove))
        {
            SetGuarding(false);
        }

        if (movement != null)
        {
            movement.SetSpeedScale(this, stunned ? 0f : IsGuarding ? Rules.GuardMoveSpeed : 1f);
        }
    }

    /// <summary>Drops the guard now (attacking out of it); it goes up again by itself while the button is still held.</summary>
    public void Lower() => SetGuarding(false);

    private void SetGuarding(bool guarding)
    {
        if (guarding == IsGuarding)
        {
            return;
        }

        IsGuarding = guarding;
        if (guarding)
        {
            raisedAt = Time.time;
        }
        else
        {
            loweredAt = Time.time;
        }

        GuardChanged?.Invoke(guarding);
    }

    /// <summary>How much of a blocked hit gets through right now.</summary>
    public float LetThrough
    {
        get
        {
            if (combat != null && combat.HasShield)
            {
                return combat.Shield != null ? combat.Shield.GuardLetThrough : shieldLetThrough;
            }

            return combat != null && combat.Weapon != null ? combat.Weapon.GuardLetThrough : Rules.UnarmedLetThrough;
        }
    }

    // The Melee skill's share more or less stamina a block costs (a beginner's shield arm tires sooner).
    private float BlockCostScale() => TryGetComponent(out Combatant self) ? Mathf.Max(0.1f, 1f + self.Stats.Get(StatType.BlockStaminaCost)) : 1f;

    public GuardOutcome Guard(in DamageInfo damage, out float letThrough)
    {
        letThrough = 1f;
        if (!isActiveAndEnabled || damage.IsOverTime)
        {
            return GuardOutcome.None;
        }

        float cost = damage.Amount * Rules.BlockStaminaPerDamage * BlockCostScale();
        GuardOutcome outcome = GuardRules.Resolve(IsGuarding, Time.time - raisedAt, transform.forward, damage.Direction,
            Rules.BlockArc, Rules.ParryWindow * GameOptions.TimingShare, stamina != null ? stamina.Current : float.PositiveInfinity, cost);
        switch (outcome)
        {
            case GuardOutcome.Parried:
                letThrough = 0f;
                OnParried(damage);
                break;
            case GuardOutcome.Blocked:
                letThrough = LetThrough;
                if (stamina != null)
                {
                    stamina.TrySpend(cost);
                }
                Blocked?.Invoke(damage);
                break;
            case GuardOutcome.GuardBroken:
                if (stamina != null)
                {
                    stamina.TrySpend(stamina.Current);
                }
                stunnedUntil = Time.time + Rules.GuardBreakStun;
                SetGuarding(false);
                Broken?.Invoke(Rules.GuardBreakStun);
                break;
        }

        return outcome;
    }

    private void OnParried(in DamageInfo damage)
    {
        // Only an attacker within reach of its own swing staggers: parrying an arrow doesn't stun the archer.
        Combatant attacker = damage.Source;
        if (attacker != null && attacker.TryGetComponent(out IStaggerable staggerable))
        {
            float reach = damage.Attack != null ? damage.Attack.Range + 1.5f : 3f;
            if (Vector3.Distance(attacker.transform.position, transform.position) <= reach)
            {
                staggerable.Stagger(Rules.ParryStagger);
            }
        }

        if (combat != null)
        {
            combat.GrantCounter();
        }
        Parried?.Invoke(damage);
    }
}
