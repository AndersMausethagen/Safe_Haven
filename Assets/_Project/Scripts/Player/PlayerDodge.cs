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

    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;
    private Damageable health;
    private PlayerStamina stamina;
    private float readyAt;
    private float invulnerableUntil = -1f;

    /// <summary>True from the start of a dodge until its dash ends.</summary>
    public bool IsDodging => movement.IsDashing;
    /// <summary>Seconds until the next dodge is possible; 0 when ready.</summary>
    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);

    /// <summary>Raised when a dodge starts, with its direction.</summary>
    public event Action<Vector3> Dodged;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        stamina = GetComponent<PlayerStamina>();
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

        if (stamina != null && !stamina.TrySpend(stamina.DodgeCost))
        {
            return false;
        }

        Vector3 direction = movement.MovementInput.sqrMagnitude > 0.01f ? movement.MovementInput.normalized : movement.AimDirection;
        movement.Dash(direction * (settings.DodgeDistance / settings.DodgeDuration), settings.DodgeDuration);
        readyAt = Time.time + settings.DodgeCooldown;

        if (settings.DodgeInvulnerability > 0f)
        {
            health.AddInvulnerability(this);
            invulnerableUntil = Time.time + settings.DodgeInvulnerability;
        }

        SetTrail(true);
        Dodged?.Invoke(direction);
        return true;
    }

    private void Update()
    {
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
