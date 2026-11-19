using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Safe Haven/Player Movement Settings")]
public sealed class PlayerMovementSettings : ScriptableObject
{
    [Serializable]
    public struct GameStateModifier
    {
        public GameState state;
        [Min(0f)] public float speedMultiplier;
        [Min(0f)] public float turnSpeedMultiplier;
        [Tooltip("Face the cursor instead of the movement direction while in this state.")]
        public bool faceCursor;
    }

    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -15.81f;
    [SerializeField] private float groundedVerticalVelocity = -2f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;
    [Tooltip("Speed, as a share of normal, moving straight away from where the player faces (cursor-facing combat, aiming). " +
             "Full speed up to sideways, easing to this by 45° behind. The backpedal animation is a short-stepped one.")]
    [SerializeField, Range(0.3f, 1f)] private float backpedalSpeed = 0.7f;

    [Header("Dodge (Space)")]
    [Tooltip("Meters covered by one dodge.")]
    [SerializeField, Min(0f)] private float dodgeDistance = 4.5f;
    [Tooltip("Seconds the dodge takes. Shorter is snappier.")]
    [SerializeField, Min(0.05f)] private float dodgeDuration = 0.22f;
    [Tooltip("Seconds from the start of a dodge during which hits are ignored.")]
    [SerializeField, Min(0f)] private float dodgeInvulnerability = 0.28f;
    [Tooltip("Seconds from the start of a dodge until the next one.")]
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.9f;
    [Tooltip("A dodge pressed this close to the cooldown's end is kept and goes the moment it ends; one pressed earlier is refused with " +
             "a sound. It was dropped silently (Docs/CombatFeelReview_2026-11-19.md §6).")]
    [SerializeField, Range(0f, 0.5f)] private float dodgeCooldownBuffer = 0.2f;

    [Tooltip("Per game state multipliers. A state without an entry uses 1.")]
    [SerializeField] private GameStateModifier[] stateModifiers =
    {
        new GameStateModifier { state = GameState.Exploration, speedMultiplier = 1f, turnSpeedMultiplier = 1f },
        new GameStateModifier { state = GameState.Combat,         speedMultiplier = 1f, turnSpeedMultiplier = 1f, faceCursor = true },
    };

    public float MoveSpeed => moveSpeed;
    public float Gravity => gravity;
    public float GroundedVerticalVelocity => groundedVerticalVelocity;
    public float TurnSpeed => turnSpeed;

    /// <summary>Speed share for moving at <paramref name="angle"/> degrees off the facing: 1 up to 90°, easing to
    /// Backpedal Speed at 135° and beyond.</summary>
    public float BackpedalShare(float angle) => Mathf.Lerp(1f, backpedalSpeed, Mathf.InverseLerp(90f, 135f, angle));
    [Header("Equipment load (armor, rings and weapon weight)")]
    [Tooltip("At or below this load the dodge is a quickstep; at or above Heavy Load it is a heavy roll that barges.")]
    [SerializeField, Min(0f)] private float lightLoad = 6f;
    [SerializeField, Min(0f)] private float heavyLoad = 13f;
    [SerializeField] private DodgeShape quickstep = new DodgeShape(0.6f, 0.65f, 0.8f, 0.6f, 0.8f, 1.05f, "Quickstep");
    [SerializeField] private DodgeShape roll = new DodgeShape(1f, 1f, 1f, 1f, 1f, 1f, "Roll");
    [SerializeField] private DodgeShape heavyRoll = new DodgeShape(0.85f, 1.35f, 0.9f, 1.4f, 1.25f, 0.9f, "Heavy Roll");

    [Header("Carrying (what is in the bag)")]
    [Tooltip("Share of the bag's weight budget where it starts to tell.")]
    [SerializeField, Range(0.1f, 1f)] private float heavyFromShare = 0.8f;
    [Tooltip("Speed with the bag full, as a share of normal. It eases in from Heavy From Share.")]
    [SerializeField, Range(0.3f, 1f)] private float speedAtFullBag = 0.8f;
    [Tooltip("What stamina costs with the bag full, times normal.")]
    [SerializeField, Range(1f, 3f)] private float staminaAtFullBag = 1.4f;
    [Tooltip("How far footsteps carry with the bag full, times normal.")]
    [SerializeField, Range(1f, 3f)] private float noiseAtFullBag = 1.35f;

    public float HeavyFromShare => heavyFromShare;
    public float SpeedAtFullBag => speedAtFullBag;
    public float StaminaAtFullBag => staminaAtFullBag;
    public float NoiseAtFullBag => noiseAtFullBag;
    public float LightLoad => lightLoad;
    public float HeavyLoad => heavyLoad;

    /// <summary>The kind of dodge a given equipment load allows.</summary>
    public DodgeStyle StyleFor(float load) => load <= lightLoad ? DodgeStyle.Quickstep : load >= heavyLoad ? DodgeStyle.HeavyRoll : DodgeStyle.Roll;

    public DodgeShape ShapeOf(DodgeStyle style) => style == DodgeStyle.Quickstep ? quickstep : style == DodgeStyle.HeavyRoll ? heavyRoll : roll;

    public float DodgeDistance => dodgeDistance;
    public float DodgeDuration => dodgeDuration;
    public float DodgeInvulnerability => dodgeInvulnerability;
    public float DodgeCooldown => dodgeCooldown;
    public float DodgeCooldownBuffer => dodgeCooldownBuffer;

    [Header("Water (wading and swimming, see PlayerSwimming)")]
    [Tooltip("Water this deep at the feet (metres) starts to slow the player.")]
    [SerializeField, Min(0f)] private float wadeFrom = 0.2f;
    [Tooltip("Speed, as a share of normal, in water just too shallow to swim. Between Wade From and Swim Depth the " +
             "player slows down evenly the deeper it gets.")]
    [SerializeField, Range(0.1f, 1f)] private float deepWadeSpeed = 0.55f;
    [Tooltip("Water this deep at the feet (metres) lifts the player off the bottom: they swim, held with the water at " +
             "their chest. Keep it below a water surface's Depth, or that water never gets deep enough to swim in.")]
    [SerializeField, Min(0.3f)] private float swimDepth = 1.3f;
    [Tooltip("Swimming speed, as a share of normal movement speed.")]
    [SerializeField, Range(0.1f, 1f)] private float swimSpeed = 0.45f;
    [Tooltip("Share of Swim Speed left when out of stamina (swimming costs stamina: PlayerStaminaSettings).")]
    [SerializeField, Range(0.1f, 1f)] private float exhaustedSwimSpeed = 0.5f;
    [Tooltip("Turning speed while swimming, as a share of normal.")]
    [SerializeField, Range(0.1f, 1f)] private float swimTurnSpeed = 0.4f;
    [Tooltip("How hard the water pushes a swimmer back to the surface (m/s²): low lets a jump in dip deep.")]
    [SerializeField, Min(1f)] private float buoyancy = 20f;

    public float SwimDepth => swimDepth;
    public float SwimSpeed => swimSpeed;
    public float ExhaustedSwimSpeed => exhaustedSwimSpeed;
    public float SwimTurnSpeed => swimTurnSpeed;
    public float Buoyancy => buoyancy;

    /// <summary>Speed share while wading with the feet <paramref name="depth"/> metres under water: 1 down to Wade
    /// From, falling evenly to Deep Wade Speed at Swim Depth.</summary>
    public float WadeSpeedShare(float depth)
    {
        float deep = Mathf.InverseLerp(wadeFrom, Mathf.Max(wadeFrom + 0.01f, swimDepth), depth);
        return Mathf.Lerp(1f, deepWadeSpeed, deep);
    }

    public float GetSpeedMultiplier(GameState state)
    {
        return TryGetModifier(state, out GameStateModifier modifier) ? modifier.speedMultiplier : 1f;
    }

    public float GetTurnSpeedMultiplier(GameState state)
    {
        return TryGetModifier(state, out GameStateModifier modifier) ? modifier.turnSpeedMultiplier : 1f;
    }

    public bool FacesCursor(GameState state)
    {
        return TryGetModifier(state, out GameStateModifier modifier) && modifier.faceCursor;
    }

    private bool TryGetModifier(GameState state, out GameStateModifier modifier)
    {
        if (stateModifiers != null)
        {
            for (int i = 0; i < stateModifiers.Length; i++)
            {
                if (stateModifiers[i].state == state)
                {
                    modifier = stateModifiers[i];
                    return true;
                }
            }
        }

        modifier = default;
        return false;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        gravity = Mathf.Min(-0.01f, gravity);
        turnSpeed = Mathf.Max(0f, turnSpeed);
    }
}

/// <summary>How the player dodges, by how much they carry on their body (PlayerMovementSettings › Equipment load).</summary>
public enum DodgeStyle
{
    /// <summary>Light gear: a short, quick, cheap sidestep.</summary>
    Quickstep,
    /// <summary>A full roll.</summary>
    Roll,
    /// <summary>Heavy gear: a slow, costly shoulder charge that barges enemies it reaches.</summary>
    HeavyRoll,
}

/// <summary>One dodge style, as multipliers on the dodge settings, plus how fast the wearer moves and the animation.</summary>
[System.Serializable]
public sealed class DodgeShape
{
    [Min(0.1f)] public float distance = 1f;
    [Min(0.1f)] public float duration = 1f;
    [Min(0f)] public float invulnerability = 1f;
    [Min(0f)] public float stamina = 1f;
    [Min(0f)] public float cooldown = 1f;
    [Tooltip("Movement speed with this much gear on, as a share of normal.")]
    [Range(0.5f, 1.2f)] public float moveSpeed = 1f;
    [Tooltip("Animator state it plays (Combat Full Body layer).")]
    public string animationState;
    [Tooltip("Playback speed of that animation (set by Set Up Combat Animations to fit the dodge).")]
    [Min(0.1f)] public float animationSpeed = 1f;

    public DodgeShape()
    {
    }

    public DodgeShape(float distance, float duration, float invulnerability, float stamina, float cooldown, float moveSpeed, string state)
    {
        this.distance = distance;
        this.duration = duration;
        this.invulnerability = invulnerability;
        this.stamina = stamina;
        this.cooldown = cooldown;
        this.moveSpeed = moveSpeed;
        animationState = state;
    }
}
