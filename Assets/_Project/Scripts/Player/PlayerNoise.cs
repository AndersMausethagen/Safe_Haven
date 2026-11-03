using UnityEngine;

/// <summary>
/// Every sound the player makes, and how far they can be heard right now. Footsteps (quieter sneaking, louder in heavy
/// gear), swimming strokes, hard landings, dodges and every hit landed or taken are made here; gathering, the bow and
/// lockpicking hand theirs to <see cref="Make"/>. Each goes out through WorldNoise, after rain has hidden some of it and
/// the NoiseMeter has added the build-up of the sounds before it. <see cref="Level"/> is what NoiseMeterView shows.
/// Numbers are in PerceptionSettings (Systems Editor › World › Senses & Stealth).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerNoise : MonoBehaviour
{
    private readonly NoiseMeter meter = new NoiseMeter();
    private PlayerMovement movement;
    private PlayerStealth stealth;
    private PlayerDodge dodge;
    private PlayerSwimming swimming;
    private PlayerEquipment equipment;
    private Damageable health;
    private Combatant combatant;
    private NoiseMeterView view;
    private float nextStepAt;
    private float nextCombatNoiseAt;
    private float fallSpeed;

    /// <summary>How many metres the player can be heard right now.</summary>
    public float Level => meter.Level;

    public NoiseBand Band => NoiseMeter.BandOf(Level, PerceptionSettings.Current);

    /// <summary>True while sneaking, for the meter to say so.</summary>
    public bool IsSneaking => stealth != null && stealth.IsSneaking;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stealth = GetComponent<PlayerStealth>();
        dodge = GetComponent<PlayerDodge>();
        swimming = GetComponent<PlayerSwimming>();
        equipment = GetComponent<PlayerEquipment>();
        burden = GetComponent<PlayerBurden>();
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
    }

    private void Start()
    {
        view = NoiseMeterView.Attach(this);
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            Destroy(view.gameObject);
        }
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
    }

    /// <summary>How far every sound the player makes carries, as a share of normal. Relics change it.</summary>
    public float Reach { get; set; } = 1f;

    /// <summary>The player makes a sound of <paramref name="radius"/> metres at <paramref name="position"/>.</summary>
    private PlayerBurden burden;

    public void Make(Vector3 position, float radius)
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        float muted = radius * Reach * PerceptionRules.RainNoiseScale(AtmosphereConditions.WeatherRain, rules);
        WorldNoise.Emit(position, meter.Add(muted, rules));
    }

    private void Update()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        meter.Tick(Time.deltaTime, rules);
        UpdateLanding(rules);
        if (!movement.IsMoving || movement.IsDashing || Time.time < nextStepAt)
        {
            return;
        }

        if (swimming != null && swimming.IsSwimming)
        {
            nextStepAt = Time.time + rules.FootstepInterval;
            Make(transform.position, rules.SwimNoise * GearScale(rules));
        }
        else if (movement.IsGrounded)
        {
            nextStepAt = Time.time + rules.FootstepInterval;
            Make(transform.position, (IsSneaking ? rules.SneakingFootstepNoise : rules.FootstepNoise) * GearScale(rules) + (burden != null ? burden.StepNoise(IsSneaking) : 0f));
        }
    }

    // The speed of a fall is only known in the air, so it is kept until the feet are down.
    private void UpdateLanding(PerceptionSettings rules)
    {
        if (!movement.IsGrounded)
        {
            fallSpeed = movement.IsFloating ? 0f : Mathf.Max(fallSpeed, -movement.VerticalVelocity);
            return;
        }

        if (fallSpeed >= rules.LandingSpeed)
        {
            Make(transform.position, rules.LandingNoise * GearScale(rules));
        }

        fallSpeed = 0f;
    }

    private float GearScale(PerceptionSettings rules)
    {
        PlayerMovementSettings settings = movement.Settings;
        float bag = burden != null ? burden.NoiseScale : 1f;
        return bag * (equipment != null ? PerceptionRules.GearNoiseScale(equipment.EquipLoad, settings.LightLoad, settings.HeavyLoad, rules) : 1f);
    }

    private void OnDodgeStarted(DodgeStyle style, DodgeShape shape)
    {
        Make(transform.position, PerceptionSettings.Current.DodgeNoise);
    }

    // Hits the player lands or takes are heard, at most every interval.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        bool involved = victim == health || (combatant != null && damage.Source == combatant);
        if (!involved || Time.time < nextCombatNoiseAt)
        {
            return;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        nextCombatNoiseAt = Time.time + rules.CombatNoiseInterval;
        Make(victim.transform.position, rules.CombatNoise);
    }
}
