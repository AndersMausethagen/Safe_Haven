using System;
using UnityEngine;

/// <summary>
/// The player's dodge (Space / gamepad South): a quick dash in the direction being moved, or toward the cursor when
/// standing still, that ignores hits for a moment and leaves a trail. It cancels a swing in progress, so the player can
/// always get out of trouble, then waits out a cooldown. Read an enemy's telegraph, dodge out of it.
/// <para>
/// Where it belongs: Player, on the PlayerArmature prefab. Distance, timing, invulnerability and cooldown are in
/// PlayerMovementSettings (Dodge); the trail's look is in Resources/CombatFeel. Animation, audio or a stamina cost can
/// hook into <see cref="Dodged"/> without changing this class.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStateMachine), typeof(Damageable))]
public sealed class PlayerDodge : MonoBehaviour
{
    [Tooltip("Height of the trail above the feet, in meters.")]
    [SerializeField, Min(0f)] private float trailHeight = 0.9f;
    [Tooltip("Optional: a trail to use instead of the one created from the CombatFeel settings.")]
    [SerializeField] private TrailRenderer trail;
    [Tooltip("What a heavy roll (heavy gear) does to enemies at its end: a shoulder barge. Empty: it barges no one.")]
    [SerializeField] private AttackDefinition shoulderBarge;
    [Tooltip("Layers the barge can hit.")]
    [SerializeField] private LayerMask bargeLayers = ~0;

    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;
    private Damageable health;
    private PlayerStamina stamina;
    private float readyAt;
    private float invulnerableUntil = -1f;
    private PlayerEquipment equipment;
    private float bargeAt = -1f;
    private Vector3 bargeDirection;

    /// <summary>True from the start of a dodge until its dash ends.</summary>
    public bool IsDodging => movement.IsDashing;
    /// <summary>Seconds until the next dodge is possible; 0 when ready.</summary>
    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);

    /// <summary>Share of the last dodge's cooldown still to wait: 1 just after dodging, 0 when ready.</summary>
    public float CooldownFraction => cooldownLength > 0f ? Mathf.Clamp01(CooldownRemaining / cooldownLength) : 0f;

    private float cooldownLength;

    /// <summary>Raised when a dodge starts, with its direction.</summary>
    public event Action<Vector3> Dodged;

    /// <summary>Raised as a dodge starts, with its style and shape (animation plays the shape's state).</summary>
    public event Action<DodgeStyle, DodgeShape> DodgeStarted;

    /// <summary>The dodge the player's current gear allows.</summary>
    public DodgeStyle Style => movement.Settings != null ? movement.Settings.StyleFor(equipment != null ? equipment.EquipLoad : 0f) : DodgeStyle.Roll;

    // A dodge pressed during a swing's wind-up waits this long for the swing to land.
    private const float DodgeBuffer = 0.35f;
    private float bufferedUntil = -1f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        stamina = GetComponent<PlayerStamina>();
        equipment = GetComponent<PlayerEquipment>();
    }

    private void OnEnable()
    {
        stateMachine.DodgeRequested += OnDodgeRequested;
    }

    private void OnDisable()
    {
        stateMachine.DodgeRequested -= OnDodgeRequested;
        EndInvulnerability();
        SetTrail(false);
    }

    private void OnDodgeRequested() => TryDodge();

    /// <summary>Dodges if alive, free to move, off cooldown and able to pay the full stamina cost. Returns true when the dodge started.</summary>
    public bool TryDodge()
    {
        PlayerMovementSettings settings = movement.Settings;
        if (settings == null || !health.IsAlive || !stateMachine.CanMove || IsDodging || Time.time < readyAt || settings.DodgeDistance <= 0f)
        {
            return false;
        }

        // A swing is a commitment until it lands, and a flinching player can't throw themselves clear.
        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null && !combat.CanDodge)
        {
            bufferedUntil = Time.time + DodgeBuffer; // it goes the moment the swing lands
            return false;
        }

        bufferedUntil = -1f;

        DodgeStyle style = Style;
        DodgeShape shape = settings.ShapeOf(style);
        if (stamina != null && !stamina.TrySpend(stamina.DodgeCost * shape.stamina))
        {
            return false;
        }

        float distance = settings.DodgeDistance * shape.distance;
        float duration = settings.DodgeDuration * shape.duration;
        Vector3 direction = movement.MovementInput.sqrMagnitude > 0.01f ? movement.MovementInput.normalized : movement.AimDirection;
        movement.Dash(direction * (distance / duration), duration);
        cooldownLength = settings.DodgeCooldown * shape.cooldown;
        readyAt = Time.time + cooldownLength;

        if (settings.DodgeInvulnerability * shape.invulnerability > 0f)
        {
            health.AddInvulnerability(this);
            invulnerableUntil = Time.time + settings.DodgeInvulnerability * shape.invulnerability;
        }

        if (style == DodgeStyle.HeavyRoll && shoulderBarge != null)
        {
            bargeAt = Time.time + duration * 0.85f;
            bargeDirection = direction;
        }

        DodgeStarted?.Invoke(style, shape);

        SetTrail(true);
        Dodged?.Invoke(direction);
        return true;
    }

    private void Update()
    {
        // The heavy roll ends in a shoulder barge into whatever it reached.
        if (bargeAt >= 0f && Time.time >= bargeAt)
        {
            bargeAt = -1f;
            if (TryGetComponent(out Combatant combatant))
            {
                AttackResolver.Execute(combatant, shoulderBarge, 0f, transform.position, bargeDirection, bargeLayers);
            }
        }

        // Heavier gear moves slower.
        if (movement.Settings != null && equipment != null)
        {
            movement.SetSpeedScale(equipment, movement.Settings.ShapeOf(Style).moveSpeed);
        }

        if (bufferedUntil >= 0f)
        {
            if (Time.time > bufferedUntil)
            {
                bufferedUntil = -1f;
            }
            else
            {
                TryDodge();
            }
        }

        if (invulnerableUntil >= 0f && Time.time >= invulnerableUntil)
        {
            EndInvulnerability();
        }

        if (trail != null && trail.emitting && !IsDodging)
        {
            SetTrail(false);
        }
    }

    private void EndInvulnerability()
    {
        invulnerableUntil = -1f;
        if (health != null)
        {
            health.RemoveInvulnerability(this);
        }
    }

    private void SetTrail(bool on)
    {
        if (on && trail == null)
        {
            trail = CreateTrail();
        }

        if (trail != null)
        {
            if (on)
            {
                trail.Clear();
            }

            trail.emitting = on;
        }
    }

    private TrailRenderer CreateTrail()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (feel.TelegraphMaterial == null || feel.DodgeTrailWidth <= 0f)
        {
            return null;
        }

        var holder = new GameObject("Dodge Trail");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = Vector3.up * trailHeight;
        var created = holder.AddComponent<TrailRenderer>();
        created.sharedMaterial = feel.TelegraphMaterial;
        created.colorGradient = feel.DodgeTrailColor;
        created.widthMultiplier = feel.DodgeTrailWidth;
        created.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        created.time = 0.25f;
        created.minVertexDistance = 0.05f;
        created.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        created.receiveShadows = false;
        created.alignment = LineAlignment.View;
        created.emitting = false;
        return created;
    }
}
