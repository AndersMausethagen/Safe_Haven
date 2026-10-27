using UnityEngine;

/// <summary>
/// How noticeable the player is: sneaking (hold Left Ctrl / left bumper) slows them down and makes them harder to see
/// and quieter; standing still helps a little; fighting makes them stand out. Also makes the player's noise: footsteps,
/// dodges and every hit they land or take go out through WorldNoise for monsters to hear. Numbers are in
/// PerceptionSettings (Systems Editor › World › Senses & Stealth).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerStealth : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerDodge dodge;
    private Damageable health;
    private Combatant combatant;
    private bool sneakRequested;
    private float nextStepAt;
    private float fightingUntil = -1f;
    private float nextCombatNoiseAt;

    /// <summary>True while the player sneaks (held, and not dodging).</summary>
    public bool IsSneaking => sneakRequested && (dodge == null || !dodge.IsDodging);

    /// <summary>True while fighting, or shortly after a hit landed or was taken.</summary>
    public bool IsFighting => Time.time < fightingUntil;

    /// <summary>How visible the player is right now (1 = plainly). Monsters see further and faster the higher it is.</summary>
    public float Visibility
    {
        get
        {
            PerceptionSettings rules = PerceptionSettings.Current;
            if (IsFighting)
            {
                return rules.FightingVisibility;
            }

            if (IsSneaking)
            {
                return rules.SneakingVisibility;
            }

            return movement.IsMoving ? rules.MovingVisibility : rules.StillVisibility;
        }
    }

    /// <summary>Raised when sneaking starts or stops.</summary>
    public event System.Action<bool> SneakChanged;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        dodge = GetComponent<PlayerDodge>();
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
    }

    private void OnEnable()
    {
        Damageable.AnyDamaged += OnAnyDamaged;
        if (dodge != null)
        {
            dodge.DodgeStarted += OnDodgeStarted;
        }
    }

    private void OnDisable()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        if (dodge != null)
        {
            dodge.DodgeStarted -= OnDodgeStarted;
        }

        SetSneaking(false);
    }

    /// <summary>Starts or stops sneaking (the input holds it).</summary>
    public void SetSneaking(bool sneaking)
    {
        if (sneakRequested == sneaking)
        {
            return;
        }

        sneakRequested = sneaking;
        if (movement != null)
        {
            movement.SetSpeedScale(this, sneaking ? PerceptionSettings.Current.SneakSpeedScale : 1f);
        }

        SneakChanged?.Invoke(sneaking);
    }

    private void Update()
    {
        if (!movement.IsMoving || !movement.IsGrounded || movement.IsDashing || Time.time < nextStepAt)
        {
            return;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        nextStepAt = Time.time + rules.FootstepInterval;
        WorldNoise.Emit(transform.position, IsSneaking ? rules.SneakingFootstepNoise : rules.FootstepNoise);
    }

    private void OnDodgeStarted(DodgeStyle style, DodgeShape shape)
    {
        WorldNoise.Emit(transform.position, PerceptionSettings.Current.DodgeNoise);
    }

    // Hits the player lands or takes: they stand out for a while, and the fight is heard (at most every interval).
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        bool involved = victim == health || (combatant != null && damage.Source == combatant);
        if (!involved)
        {
            return;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        fightingUntil = Time.time + rules.FightingSeconds;
        if (Time.time < nextCombatNoiseAt)
        {
            return;
        }

        nextCombatNoiseAt = Time.time + rules.CombatNoiseInterval;
        WorldNoise.Emit(victim.transform.position, rules.CombatNoise);
    }
}
