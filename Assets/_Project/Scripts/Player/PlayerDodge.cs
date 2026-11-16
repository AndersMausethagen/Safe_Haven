using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's dodge (Space / gamepad South): a quick dash in the direction being moved, or toward the cursor when
/// standing still, that ignores hits for a moment and leaves a trail. It cancels a swing in progress, so the player can
/// always get out of trouble, then waits out a cooldown. Read an enemy's telegraph, dodge out of it. With an enemy in focus
/// (<see cref="PlayerFocusTarget"/>) it can be steered: toward or past the enemy it curves around to the spot behind (rules in
/// <see cref="DodgeRules"/>), from where an attack is a backstab if the enemy is open; with no direction held it steps straight
/// back. Nothing alive blocks a dodge: it passes through bodies.
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

    private float dashEndsAt = float.NegativeInfinity;

    // A dodge going around the enemy in focus: who, how fast and which way it is going now.
    private Damageable rounding;
    private float roundingRadius;
    private float dashSpeed;
    private Vector3 dashDirection;
    private PlayerFocusTarget focus;
    private CharacterController body;
    // Monsters' bodies a dodge is passing through, given back when it ends.
    private readonly List<Collider> passedThrough = new List<Collider>();
    private const float PassThroughRange = 7f;

    private PlayerCombat combat;
    // What the last dodge cost, for a backstab straight out of it to give part back; once only.
    private float paidForDodge;

    /// <summary>True when the dodge has done its job and an attack may cut the rest short: its invulnerability and barge are over, and it
    /// isn't still on its way around an enemy (it ends by itself as it gets there).</summary>
    public bool CanBeCutShort => IsDodging && invulnerableUntil < 0f && bargeAt < 0f && rounding == null;

    /// <summary>How fast this dodge's animation plays, as a share of its shape's speed: under 1 when it has been given longer to go around.</summary>
    public float AnimationPace { get; private set; } = 1f;

    /// <summary>True for <paramref name="seconds"/> after a dodge ended.</summary>
    public bool JustDodged(float seconds) => !IsDodging && Time.time - dashEndsAt <= seconds;

    /// <summary>Seconds until the dodge in progress ends (0 when none): presses are only kept in its last moments.</summary>
    public float TimeLeft => IsDodging ? Mathf.Max(0f, dashEndsAt - Time.time) : 0f;

    /// <summary>Ends the dodge here (a rush attack takes over).</summary>
    public void CutShort()
    {
        if (IsDodging)
        {
            movement.EndDash();
            dashEndsAt = Time.time;
        }
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        stamina = GetComponent<PlayerStamina>();
        equipment = GetComponent<PlayerEquipment>();
        focus = GetComponent<PlayerFocusTarget>();
        body = GetComponent<CharacterController>();
        combat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        stateMachine.DodgeRequested += OnDodgeRequested;
        if (combat != null)
        {
            combat.Backstabbed += OnBackstabbed;
        }
    }

    private void OnDisable()
    {
        stateMachine.DodgeRequested -= OnDodgeRequested;
        if (combat != null)
        {
            combat.Backstabbed -= OnBackstabbed;
        }

        EndInvulnerability();
        SetTrail(false);
        rounding = null;
        GiveBodiesBack();
    }

    private void OnDodgeRequested() => TryDodge();

    // A dodge that earned its backstab pays back part of what it cost.
    private void OnBackstabbed(IDamageable target)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        if (stamina != null && paidForDodge > 0f && Time.time - dashEndsAt <= rules.BackstabRefundWithin)
        {
            stamina.Restore(paidForDodge * rules.BackstabRefund);
        }

        paidForDodge = 0f;
    }

    /// <summary>Dodges if alive, free to move, off cooldown and able to pay the full stamina cost. Returns true when the dodge started.</summary>
    public bool TryDodge()
    {
        PlayerMovementSettings settings = movement.Settings;
        if (settings == null || !health.IsAlive || !stateMachine.CanMove || IsDodging || Time.time < readyAt || settings.DodgeDistance <= 0f)
        {
            return false;
        }

        // A swing is a commitment until it lands, and a flinching player can't throw themselves clear.
        if (combat != null && !combat.CanDodge)
        {
            // It goes the moment the swing lands, if that is soon: set on the press, not again each frame it waits (Update asks
            // again), or a press during a long knockdown was kept for good.
            if (bufferedUntil < 0f)
            {
                bufferedUntil = Time.time + DodgeBuffer;
            }

            return false;
        }

        bufferedUntil = -1f;

        DodgeStyle style = Style;
        DodgeShape shape = settings.ShapeOf(style);
        float cost = stamina != null ? stamina.DodgeCost * shape.stamina * (combat != null ? combat.DodgeCostScale : 1f) : 0f;
        if (stamina != null && !stamina.TrySpend(cost))
        {
            return false;
        }

        // What the bar really gave (a dagger's lighter dodge, the burden): a backstab out of it refunds half of that.
        paidForDodge = stamina != null ? stamina.Scaled(cost) : 0f;

        float distance = settings.DodgeDistance * shape.distance;
        float duration = settings.DodgeDuration * shape.duration;
        Damageable enemy = focus != null && focus.HasFocus ? focus.Target : null;
        Vector3 toEnemy = enemy != null ? enemy.transform.position - transform.position : Vector3.zero;
        toEnemy.y = 0f;
        Vector3 direction = DodgeRules.StartDirection(movement.MovementInput, movement.AimDirection, toEnemy);

        // A heavy roll is a shoulder charge: it goes straight in. The others can go around.
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        rounding = enemy != null && style != DodgeStyle.HeavyRoll && toEnemy.magnitude <= rules.DodgeAroundRange
                   && DodgeRules.GoesAround(movement.MovementInput, toEnemy) ? enemy : null;
        roundingRadius = 0.5f;
        if (rounding != null && rounding.TryGetComponent(out Collider enemyBody))
        {
            Vector3 extents = enemyBody.bounds.extents;
            roundingRadius = Mathf.Max(extents.x, extents.z);
        }

        dashDirection = direction;
        dashSpeed = distance / duration * (rounding != null ? rules.DodgeAroundSpeed : 1f);
        // Going around takes longer than going straight, so it gets the time, and its animation is spread over it.
        float lasts = rounding != null ? Mathf.Max(duration, rules.DodgeAroundSeconds) : duration;
        AnimationPace = duration / lasts;
        movement.Dash(direction * dashSpeed, lasts);
        PassThroughBodies();
        dashEndsAt = Time.time + lasts;
        cooldownLength = settings.DodgeCooldown * shape.cooldown;
        readyAt = Time.time + cooldownLength;

        if (settings.DodgeInvulnerability * shape.invulnerability > 0f)
        {
            health.AddInvulnerability(this);
            invulnerableUntil = Time.time + settings.DodgeInvulnerability * shape.invulnerability * GameOptions.TimingShare;
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

    // Steered every frame, toward the spot behind whichever way the enemy has turned.
    private void SteerAround()
    {
        if (rounding == null || !rounding.IsAlive || !movement.IsDashing)
        {
            rounding = null;
            return;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        Vector3 enemy = rounding.transform.position;
        Vector3 spot = DodgeRules.SpotBehind(enemy, rounding.transform.forward, roundingRadius + rules.DodgeBehindDistance);
        if (DodgeRules.HasArrived(transform.position, spot))
        {
            // There: no sliding on past it. Turned to face the enemy, ready to strike.
            CutShort();
            Vector3 toEnemy = enemy - transform.position;
            movement.SnapFacing(toEnemy);
            rounding = null;
            return;
        }

        Vector3 heading = DodgeRules.Heading(transform.position, enemy, spot, movement.MovementInput, roundingRadius + rules.DodgeClearance);
        dashDirection = Vector3.RotateTowards(dashDirection, heading, rules.DodgeAroundTurn * Mathf.Deg2Rad * Time.deltaTime, 0f);
        movement.SteerDash(dashDirection * dashSpeed);
    }

    private void PassThroughBodies()
    {
        if (body == null)
        {
            return;
        }

        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && (monster.transform.position - transform.position).sqrMagnitude <= PassThroughRange * PassThroughRange
                && monster.TryGetComponent(out Collider solid) && solid.enabled)
            {
                BodyPassThrough.Ignore(body, solid);
                passedThrough.Add(solid);
            }
        }
    }

    private void GiveBodiesBack()
    {
        foreach (Collider solid in passedThrough)
        {
            if (solid != null && body != null)
            {
                BodyPassThrough.Restore(body, solid);
            }
        }

        passedThrough.Clear();
    }

    private void Update()
    {
        SteerAround();
        if (passedThrough.Count > 0 && !IsDodging)
        {
            GiveBodiesBack();
        }

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
