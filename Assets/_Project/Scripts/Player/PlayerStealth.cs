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
    private float veiledUntil = -1f;

    /// <summary>
    /// Hides the player for <paramref name="seconds"/> whatever they do: seen at a sliver of their sneaking visibility, so creatures
    /// lose sight of them and an ambush is possible again (Mistcloaked, a great enchantment).
    /// </summary>
    public void Veil(float seconds) => veiledUntil = Mathf.Max(veiledUntil, Time.time + seconds);

    /// <summary>True while a veil (Veil) hides the player.</summary>
    public bool IsVeiled => Time.time < veiledUntil;

    /// <summary>True while the player sneaks (held, and not dodging).</summary>
    public bool IsSneaking => sneakRequested && (dodge == null || !dodge.IsDodging);

    private float cover = 1f;
    private float coverReadAt = -1f;

    /// <summary>How deep in cover the player stands (Concealment), as a visibility share: read a few times a second, as senses scan.</summary>
    public float Cover
    {
        get
        {
            if (Time.time >= coverReadAt)
            {
                coverReadAt = Time.time + 0.2f;
                cover = Concealment.ScaleAt(transform.position);
            }

            return cover;
        }
    }

    /// <summary>True while fighting, or shortly after a hit landed or was taken.</summary>
    public bool IsFighting => Time.time < fightingUntil;

    /// <summary>How visible the player is right now (1 = plainly). Monsters see further and faster the higher it is.</summary>
    public float Visibility
    {
        get
        {
            PerceptionSettings rules = PerceptionSettings.Current;
            if (IsVeiled)
            {
                return rules.SneakingVisibility * 0.15f;
            }

            if (IsFighting)
            {
                return rules.FightingVisibility;
            }

            if (IsSneaking)
            {
                // The Sneaking skill: a beginner crouches badly, a master hardly shows.
                float skill = combatant != null ? Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.SneakVisibility)) : 1f;
                return rules.SneakingVisibility * skill * Cover;
            }

            return movement.IsMoving ? rules.MovingVisibility : rules.StillVisibility;
        }
    }

    /// <summary>Raised when sneaking starts or stops.</summary>
    public event System.Action<bool> SneakChanged;

    private void Awake()
    {
        // Reading stealth from above (the local player's own: it checks it is theirs).
        if (!TryGetComponent(out StealthReadout _))
        {
            gameObject.AddComponent<StealthReadout>();
        }

        if (!TryGetComponent(out CastAimLine _) && TryGetComponent(out PlayerCombat _))
        {
            gameObject.AddComponent<CastAimLine>();
        }

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
            float shadowstep = Techniques.Has(PerkTechnique.Shadowstep) ? TechniqueSettings.Current.Shadowstep : 1f;
            if (sneaking && shadowstep > 1f)
            {
                Techniques.Used(PerkTechnique.Shadowstep);
            }
            movement.SetSpeedScale(this, sneaking ? Mathf.Min(1f, PerceptionSettings.Current.SneakSpeedScale * shadowstep) : 1f);
        }

        SneakChanged?.Invoke(sneaking);
    }

    // Hits the player lands or takes make them stand out for a while.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (victim == health || (combatant != null && damage.Source == combatant))
        {
            // A clean kill from the shadows can leave the player hidden (Perception Settings › Ambush Kill Fighting).
            PlayerNoise sounds = GetComponent<PlayerNoise>();
            if (!PerceptionSettings.Current.AmbushKillFighting && sounds != null && sounds.IsAmbushKill(victim, damage, result))
            {
                return;
            }

            fightingUntil = Time.time + PerceptionSettings.Current.FightingSeconds;
        }
    }
}
