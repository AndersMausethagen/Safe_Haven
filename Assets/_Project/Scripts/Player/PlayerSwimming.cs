using System;
using UnityEngine;

/// <summary>
/// What water does to the player. Wading slows them the deeper it gets; water as deep as the settings' Swim Depth lifts
/// them off the bottom and they swim: they float with the water at their chest, treading water when standing still and
/// swimming the way they are steered, slower and turning slower than on land. Swimming forward costs stamina (treading
/// water doesn't); out of stamina they swim slower, but never drown. They can't fight, dodge or interact while swimming
/// (PlayerStateMachine.IsSwimming), and put away what they hold (HeldItemView). They stand again once the bottom comes
/// up under them near the bank.
/// <para>
/// The water is found through <see cref="WaterSurface"/> (ponds and rivers, not waterfalls); the numbers are in
/// PlayerMovementSettings › Water. Runs before PlayerMovement so this frame's speed and float height are used.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1)]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStateMachine))]
public sealed class PlayerSwimming : MonoBehaviour
{
    // Standing again needs the bottom under the feet with the water at least this much shallower than Swim Depth, so
    // floating on the spot doesn't flicker between swimming and standing.
    private const float StandMargin = 0.1f;
    // Out of the water this much shallower than Swim Depth stands the player even without the bottom (a failsafe).
    private const float LeaveMargin = 0.5f;

    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;
    private PlayerStamina stamina;

    /// <summary>True while swimming (deep water); false wading or on land.</summary>
    public bool IsSwimming { get; private set; }
    /// <summary>How deep the water is at the feet in metres (0 out of the water).</summary>
    public float Depth { get; private set; }
    /// <summary>The height of the water's surface over the player (meaningful only while <see cref="Depth"/> &gt; 0).</summary>
    public float SurfaceHeight { get; private set; }
    /// <summary>True while swimming with no stamina left: slower.</summary>
    public bool IsExhausted => IsSwimming && stamina != null && stamina.IsEmpty;
    /// <summary>The speed of swimming flat out, in m/s, before talents and gear (for the swim animation's pace).</summary>
    public float SwimSpeed => movement.Settings.MoveSpeed * movement.Settings.SwimSpeed;

    /// <summary>Raised with true on starting to swim and false on standing again.</summary>
    public event Action<bool> SwimmingChanged;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        stamina = GetComponent<PlayerStamina>();
    }

    private void OnDisable()
    {
        SetSwimming(false);
        Depth = 0f;
        movement.SetSpeedScale(this, 1f);
    }

    private void Update()
    {
        PlayerMovementSettings settings = movement.Settings;
        if (settings == null)
        {
            return;
        }

        // Probed a Swim Depth above the feet, so a swimmer who dips deep on jumping in is still found in the water.
        Vector3 feet = transform.position;
        float swimDepth = settings.SwimDepth;
        Depth = WaterSurface.TryFind(feet + Vector3.up * swimDepth, swimDepth, out float surface) ? Mathf.Max(0f, surface - feet.y) : 0f;
        SurfaceHeight = Depth > 0f ? surface : feet.y;

        SetSwimming(ShouldSwim(IsSwimming, Depth, movement.IsGrounded, swimDepth));
        if (IsSwimming)
        {
            movement.FloatAt(SurfaceHeight - swimDepth);
        }

        float swimShare = settings.SwimSpeed * (IsExhausted ? settings.ExhaustedSwimSpeed : 1f);
        movement.SetSpeedScale(this, IsSwimming ? swimShare : settings.WadeSpeedShare(Depth));
        if (IsSwimming && stamina != null && stateMachine.CanMove)
        {
            stamina.Drain(stamina.SwimCostPerSecond * movement.InputMagnitude * Time.deltaTime);
        }
    }

    /// <summary>Whether the player swims this frame: from <paramref name="swimDepth"/> of water at the feet; a swimmer
    /// stands again once the bottom is under them in shallower water, or the water is far too shallow.</summary>
    public static bool ShouldSwim(bool swimming, float depth, bool grounded, float swimDepth)
    {
        if (!swimming)
        {
            return depth >= swimDepth;
        }

        bool bottomUnder = grounded && depth < swimDepth - StandMargin;
        return !bottomUnder && depth >= swimDepth - LeaveMargin;
    }

    private void SetSwimming(bool swimming)
    {
        if (IsSwimming == swimming)
        {
            return;
        }

        IsSwimming = swimming;
        stateMachine.SetSwimming(swimming);
        if (!swimming)
        {
            movement.StopFloating();
        }

        SwimmingChanged?.Invoke(swimming);
    }
}
