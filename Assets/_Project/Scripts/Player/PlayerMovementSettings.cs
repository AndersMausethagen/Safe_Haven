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
