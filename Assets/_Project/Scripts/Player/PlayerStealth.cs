using UnityEngine;

/// <summary>
/// How noticeable the player is to the eye: sneaking (hold Left Ctrl / left bumper) slows them down and makes them harder
/// to see; standing still helps a little; fighting makes them stand out. The sounds they make are PlayerNoise's. Numbers
/// are in PerceptionSettings (Systems Editor › World › Senses & Stealth).
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
    private float fightingUntil = -1f;

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
                // The Sneaking skill: a beginner crouches badly, a master hardly shows.
                float skill = combatant != null ? Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.SneakVisibility)) : 1f;
                return rules.SneakingVisibility * skill;
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
    }

    private void OnDisable()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
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

    // Hits the player lands or takes make them stand out for a while.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (victim == health || (combatant != null && damage.Source == combatant))
        {
            fightingUntil = Time.time + PerceptionSettings.Current.FightingSeconds;
        }
    }
}
