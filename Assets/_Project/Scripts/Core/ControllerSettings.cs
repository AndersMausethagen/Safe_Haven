using UnityEngine;

/// <summary>
/// How the game feels on a controller: the cursor in windows (PadCursor), rumble (ControllerRumble), the wheels' stick
/// and placing buildings with the sticks (BuildPlacer). Resources/ControllerSettings; the bindings themselves are in the
/// Input Actions asset's Gamepad scheme.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Controller Settings", fileName = "ControllerSettings")]
public sealed class ControllerSettings : GameSettings<ControllerSettings>
{
    [Header("Cursor in windows (left stick)")]
    [Tooltip("Pixels a second at 1080p, the stick fully over.")]
    [SerializeField, Min(50f)] private float cursorSpeed = 1100f;
    [Tooltip("Response curve: the stick's tilt raised to this power, so a small tilt places finely. 1 is linear.")]
    [SerializeField, Range(1f, 3f)] private float cursorResponse = 2f;
    [Tooltip("Lines a second the right stick scrolls, fully over.")]
    [SerializeField, Min(0f)] private float scrollSpeed = 14f;
    [Tooltip("Size of the drawn pointer, in UI pixels.")]
    [SerializeField, Min(8f)] private float cursorSize = 46f;

    [Header("Rumble")]
    [Tooltip("Motor strength per unit of camera shake: hits, hurts, kills, shots, falling trees, heavy footfalls all shake the camera.")]
    [SerializeField, Range(0f, 3f)] private float rumblePerShake = 1.6f;
    [Tooltip("The most either motor runs at.")]
    [SerializeField, Range(0f, 1f)] private float rumbleMax = 0.85f;
    [Tooltip("How fast rumble from shake fades, per second.")]
    [SerializeField, Min(0.1f)] private float rumbleFade = 4f;
    [Tooltip("A parry: a short, sharp buzz (low motor, high motor, seconds).")]
    [SerializeField] private Vector3 parryPulse = new Vector3(0.25f, 0.9f, 0.12f);
    [Tooltip("A fish on the line: a tug (low motor, high motor, seconds).")]
    [SerializeField] private Vector3 bitePulse = new Vector3(0.55f, 0.35f, 0.22f);

    [Header("Wheels")]
    [Tooltip("How far the right stick must lean to pick a choice on a wheel; letting go keeps the last one picked.")]
    [SerializeField, Range(0.1f, 0.9f)] private float wheelDeadZone = 0.45f;

    [Header("Placing buildings (top-down view)")]
    [Tooltip("Where the spot starts, metres ahead of the player (away from the camera).")]
    [SerializeField, Min(0.5f)] private float buildStartDistance = 5f;
    [Tooltip("The spot stays within this many metres of the player, who carries it along while walking.")]
    [SerializeField, Min(1f)] private float buildReach = 14f;
    [Tooltip("Metres a second the right stick moves the spot, fully over.")]
    [SerializeField, Min(0.5f)] private float buildSpotSpeed = 9f;

    public float CursorSpeed => cursorSpeed;
    public float CursorResponse => cursorResponse;
    public float ScrollSpeed => scrollSpeed;
    public float CursorSize => cursorSize;
    public float RumblePerShake => rumblePerShake;
    public float RumbleMax => rumbleMax;
    public float RumbleFade => rumbleFade;
    public Vector3 ParryPulse => parryPulse;
    public Vector3 BitePulse => bitePulse;
    public float WheelDeadZone => wheelDeadZone;
    public float BuildStartDistance => buildStartDistance;
    public float BuildReach => buildReach;
    public float BuildSpotSpeed => buildSpotSpeed;
}
