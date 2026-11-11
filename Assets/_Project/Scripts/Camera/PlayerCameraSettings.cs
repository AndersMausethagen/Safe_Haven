using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCameraSettings", menuName = "Safe Haven/Player Camera Settings")]
public sealed class PlayerCameraSettings : ScriptableObject
{
    [Serializable]
    public struct Profile
    {
        public GameState state;

        [Header("Framing")]
        [Range(20f, 85f)] public float pitch;
        [Min(1f)] public float minDistance;
        [Min(1f)] public float maxDistance;
        [Range(10f, 90f)] public float fieldOfView;

        [Header("Follow")]
        [Tooltip("Cinemachine follow damping in seconds (x, y, z). Higher is looser.")]
        public Vector3 positionDamping;

        [Tooltip("Fraction of the player-to-cursor offset the camera leans toward (0 disables).")]
        [Range(0f, 1f)] public float cursorLookAhead;
        [Min(0f)] public float maxLookAhead;

        [Header("Input")]
        public bool allowOrbit;
    }

    [Header("Blending")]
    [Tooltip("Seconds to settle after the game state changes.")]
    [SerializeField, Min(0.01f)] private float transitionTime = 0.6f;
    [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.15f;
    [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.25f;
    [Tooltip("Seconds the view takes to settle on a conversation and to return from it.")]
    [SerializeField, Min(0.01f)] private float dialogueBlendTime = 1.6f;
    [Tooltip("Seconds the view stays framed after a line ends, so it doesn't pull out and back in between lines.")]
    [SerializeField, Min(0f)] private float dialogueHoldTime = 1.2f;
    [Tooltip("How gently the view glides when the speaker changes, or a story turns it to something else (seconds, about).")]
    [SerializeField, Min(0.01f)] private float dialogueTargetSmoothTime = 0.9f;
    [Tooltip("The furthest a conversation pulls the view from the player, in metres: someone far off is only leaned towards.")]
    [SerializeField, Min(0f)] private float dialogueMaxLean = 4f;
    [Tooltip("Seconds the view takes to turn to something a story points at (Focus Camera) and back.")]
    [SerializeField, Min(0.01f)] private float focusBlendTime = 1.5f;

    [Header("Close up (a fish on the line)")]
    [Tooltip("Seconds the view takes to come down close to something the player is busy with, and to go back.")]
    [SerializeField, Min(0.01f)] private float closeUpBlendTime = 1.1f;
    [Tooltip("Pitch of the close view, lower than the usual framing so the water and the ground fill more of it.")]
    [SerializeField, Range(15f, 80f)] private float closeUpPitch = 34f;
    [Tooltip("Distance of the close view, in metres; it pulls back as far as it must to keep the player and the subject in view.")]
    [SerializeField, Min(2f)] private float closeUpDistance = 9f;
    [Tooltip("Room kept round the player and the subject at the screen's edges, in metres.")]
    [SerializeField, Min(0f)] private float closeUpMargin = 3f;

    [Header("Input")]
    [Tooltip("Zoom change (0-1 of the state's range) per mouse wheel notch.")]
    [SerializeField, Range(0.01f, 0.5f)] private float zoomStep = 0.1f;
    [SerializeField, Range(0f, 1f)] private float initialZoom = 0.5f;
    [SerializeField, Min(0f)] private float orbitSensitivity = 0.12f;
    [Tooltip("Seconds the view takes to catch up with a turn of the right-mouse orbit: smooths the mouse's steps. 0 turns at once.")]
    [SerializeField, Range(0f, 0.5f)] private float orbitSmoothTime = 0.2f;

    [Header("Action camera (Enshrouded style)")]
    [Tooltip("Seconds to settle when switching between the top-down and action styles.")]
    [SerializeField, Min(0.01f)] private float styleTransitionTime = 0.35f;
    [Tooltip("Pitch when the action camera is first used. Positive looks down.")]
    [SerializeField, Range(-30f, 80f)] private float actionStartPitch = 12f;
    [SerializeField, Range(-80f, 0f)] private float actionMinPitch = -35f;
    [SerializeField, Range(0f, 85f)] private float actionMaxPitch = 70f;
    [SerializeField, Min(0.5f)] private float actionMinDistance = 2.5f;
    [SerializeField, Min(0.5f)] private float actionMaxDistance = 7f;
    [SerializeField, Range(10f, 90f)] private float actionFieldOfView = 55f;
    [Tooltip("How far right of the player the camera looks, so the player stands left of the crosshair.")]
    [SerializeField] private float actionShoulderOffset = 0.7f;
    [Tooltip("Extra height of the look point, on top of the Rotation Composer's target offset.")]
    [SerializeField] private float actionLookHeight = 0.6f;
    [Tooltip("Degrees per pixel of mouse movement.")]
    [SerializeField, Min(0f)] private float actionLookSensitivity = 0.12f;
    [SerializeField] private bool actionInvertY;
    [Tooltip("Follow damping in seconds (x, y, z). Keep low so the view answers the mouse at once.")]
    [SerializeField] private Vector3 actionPositionDamping = Vector3.zero;
    [Tooltip("What the camera may not pass through; it moves in closer instead. Moving things (characters, rigidbodies) never block.")]
    [SerializeField] private LayerMask actionObstructionLayers = 1;
    [SerializeField, Min(0f)] private float actionCollisionRadius = 0.25f;
    [Tooltip("With the crosshair, building placement and similar picks reach at most this far along the view.")]
    [SerializeField, Min(1f)] private float actionPointerReach = 14f;

    [Tooltip("A state without an entry uses the first profile.")]
    [SerializeField] private Profile[] profiles =
    {
        new Profile
        {
            state = GameState.Exploration,
            pitch = 50f, minDistance = 14f, maxDistance = 28f, fieldOfView = 40f,
            positionDamping = new Vector3(0.8f, 1f, 0.8f),
            cursorLookAhead = 0f, maxLookAhead = 0f,
            allowOrbit = true,
        },
        new Profile
        {
            state = GameState.Combat,
            pitch = 58f, minDistance = 12f, maxDistance = 20f, fieldOfView = 40f,
            positionDamping = new Vector3(0.3f, 0.4f, 0.3f),
            cursorLookAhead = 0.35f, maxLookAhead = 4f,
            allowOrbit = false,
        },
    };

    public float DialogueBlendTime => dialogueBlendTime;
    public float DialogueHoldTime => dialogueHoldTime;
    public float DialogueTargetSmoothTime => dialogueTargetSmoothTime;
    public float DialogueMaxLean => dialogueMaxLean;
    public float FocusBlendTime => focusBlendTime;
    public float CloseUpBlendTime => closeUpBlendTime;
    public float CloseUpPitch => closeUpPitch;
    public float CloseUpDistance => closeUpDistance;
    public float CloseUpMargin => closeUpMargin;
    public float TransitionTime => transitionTime;
    public float ZoomSmoothTime => zoomSmoothTime;
    public float LookAheadSmoothTime => lookAheadSmoothTime;
    public float ZoomStep => zoomStep;
    public float InitialZoom => initialZoom;
    public float OrbitSensitivity => orbitSensitivity;
    public float OrbitSmoothTime => orbitSmoothTime;

    public float StyleTransitionTime => styleTransitionTime;
    public float ActionStartPitch => actionStartPitch;
    public float ActionMinPitch => actionMinPitch;
    public float ActionMaxPitch => actionMaxPitch;
    public float ActionMinDistance => actionMinDistance;
    public float ActionMaxDistance => Mathf.Max(actionMinDistance, actionMaxDistance);
    public float ActionFieldOfView => actionFieldOfView;
    public float ActionShoulderOffset => actionShoulderOffset;
    public float ActionLookHeight => actionLookHeight;
    public float ActionLookSensitivity => actionLookSensitivity;
    public bool ActionInvertY => actionInvertY;
    public Vector3 ActionPositionDamping => Vector3.Max(actionPositionDamping, Vector3.zero);
    public LayerMask ActionObstructionLayers => actionObstructionLayers;
    public float ActionCollisionRadius => actionCollisionRadius;
    public float ActionPointerReach => actionPointerReach;

    public Profile GetProfile(GameState state)
    {
        for (int i = 0; i < profiles.Length; i++)
        {
            if (profiles[i].state == state)
            {
                return profiles[i];
            }
        }

        return profiles[0];
    }

    private void OnValidate()
    {
        if (profiles == null || profiles.Length == 0)
        {
            Debug.LogWarning("PlayerCameraSettings needs at least one profile.", this);
            return;
        }

        for (int i = 0; i < profiles.Length; i++)
        {
            Profile profile = profiles[i];
            profile.maxDistance = Mathf.Max(profile.minDistance, profile.maxDistance);
            profile.positionDamping = Vector3.Max(profile.positionDamping, Vector3.zero);
            profiles[i] = profile;
        }
    }
}
