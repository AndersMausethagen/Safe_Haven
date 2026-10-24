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

    [Header("Dodge (Space)")]
    [Tooltip("Meters covered by one dodge.")]
    [SerializeField, Min(0f)] private float dodgeDistance = 4.5f;
    [Tooltip("Seconds the dodge takes. Shorter is snappier.")]
    [SerializeField, Min(0.05f)] private float dodgeDuration = 0.22f;
    [Tooltip("Seconds from the start of a dodge during which hits are ignored.")]
    [SerializeField, Min(0f)] private float dodgeInvulnerability = 0.28f;
    [Tooltip("Seconds from the start of a dodge until the next one.")]
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.9f;

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
    [Header("Equipment load (armor, rings and weapon weight)")]
    [Tooltip("At or below this load the dodge is a quickstep; at or above Heavy Load it is a heavy roll that barges.")]
    [SerializeField, Min(0f)] private float lightLoad = 6f;
    [SerializeField, Min(0f)] private float heavyLoad = 13f;
    [SerializeField] private DodgeShape quickstep = new DodgeShape(0.6f, 0.65f, 0.8f, 0.6f, 0.8f, 1.05f, "Quickstep");
    [SerializeField] private DodgeShape roll = new DodgeShape(1f, 1f, 1f, 1f, 1f, 1f, "Roll");
    [SerializeField] private DodgeShape heavyRoll = new DodgeShape(0.85f, 1.35f, 0.9f, 1.4f, 1.25f, 0.9f, "Heavy Roll");

    public float LightLoad => lightLoad;
    public float HeavyLoad => heavyLoad;

    /// <summary>The kind of dodge a given equipment load allows.</summary>
    public DodgeStyle StyleFor(float load) => load <= lightLoad ? DodgeStyle.Quickstep : load >= heavyLoad ? DodgeStyle.HeavyRoll : DodgeStyle.Roll;

    public DodgeShape ShapeOf(DodgeStyle style) => style == DodgeStyle.Quickstep ? quickstep : style == DodgeStyle.HeavyRoll ? heavyRoll : roll;

    public float DodgeDistance => dodgeDistance;
    public float DodgeDuration => dodgeDuration;
    public float DodgeInvulnerability => dodgeInvulnerability;
    public float DodgeCooldown => dodgeCooldown;

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
