using UnityEngine;

/// <summary>
/// The Keeper's view (Docs/ColonyModeResearch_2026-11-21.md, approved by the user 2026-11-21): at home a key lifts the camera
/// off the player into a free view over the Haven, to read the town, click buildings and people and place buildings from
/// above. When it opens, how the camera moves, what can be picked and where it is heard from. Lives at
/// Resources/KeeperViewSettings; built-in defaults are used without it.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Keeper View Settings", fileName = "KeeperViewSettings")]
public sealed class KeeperViewSettings : GameSettings<KeeperViewSettings>
{
    [Header("Opening")]
    [Tooltip("The view opens once this quest has started: The Road North, when the Heartroot names the player Hearth-keeper. " +
             "Empty: it opens at home from the start.")]
    [SerializeField] private QuestDefinition unlockedBy;
    [Tooltip("Seconds the camera takes to rise from the player into the view, and to come back down.")]
    [SerializeField, Min(0.05f)] private float glideSeconds = 0.6f;
    [Tooltip("Seconds after the player is hurt in which the view won't open.")]
    [SerializeField, Min(0f)] private float hurtGraceSeconds = 4f;

    [Header("Camera")]
    [Tooltip("Metres from the camera to the ground it looks at, zoomed fully in.")]
    [SerializeField, Min(5f)] private float minDistance = 18f;
    [Tooltip("Metres from the camera to the ground it looks at, zoomed fully out. Measured in Phase 0 (rendering at 35-55 m).")]
    [SerializeField, Min(5f)] private float maxDistance = 50f;
    [Tooltip("Where the zoom starts, 0 (in) to 1 (out).")]
    [SerializeField, Range(0f, 1f)] private float startZoom = 0.35f;
    [Tooltip("Degrees down from level, zoomed fully in.")]
    [SerializeField, Range(20f, 89f)] private float pitchNear = 50f;
    [Tooltip("Degrees down from level, zoomed fully out: steeper from higher up, as in Gord.")]
    [SerializeField, Range(20f, 89f)] private float pitchFar = 64f;
    [SerializeField, Range(20f, 80f)] private float fieldOfView = 40f;
    [Tooltip("Metres a second the view pans zoomed fully in; it pans faster further out, in step with the distance.")]
    [SerializeField, Min(1f)] private float panSpeed = 16f;
    [Tooltip("Degrees a second the view turns on Q and E (or the right stick).")]
    [SerializeField, Min(1f)] private float turnSpeed = 100f;
    [Tooltip("Degrees per unit of mouse movement while the right button turns the view.")]
    [SerializeField, Min(0.01f)] private float dragTurnSensitivity = 0.2f;
    [Tooltip("How much one notch of the wheel zooms, of the whole range.")]
    [SerializeField, Range(0.01f, 0.5f)] private float zoomStep = 0.08f;
    [Tooltip("How fast the right stick zooms, of the whole range a second.")]
    [SerializeField, Min(0.01f)] private float stickZoomSpeed = 0.8f;
    [Tooltip("Seconds the view takes to settle on where it is sent (smoothing of pan and zoom).")]
    [SerializeField, Min(0f)] private float settleSeconds = 0.12f;
    [Tooltip("Metres from the portal the view may look: Haven Meadows and a margin.")]
    [SerializeField, Min(20f)] private float boundsRadius = 210f;

    [Header("Picking")]
    [Tooltip("Metres along the view a click reaches.")]
    [SerializeField, Min(10f)] private float pickRange = 400f;
    [Tooltip("Controller: metres on the ground round the middle of the screen in which a person or building is picked.")]
    [SerializeField, Min(0f)] private float padSnapRadius = 4f;

    [Header("Hearing")]
    [Tooltip("Metres over the ground at the middle of the view that sounds are heard from while the view is open.")]
    [SerializeField, Min(0f)] private float earHeight = 7f;
    [Header("The HUD")]
    [Tooltip("The stores shown along the top of the view, each its picture and how many the chests hold: what building and work use.")]
    [SerializeField] private System.Collections.Generic.List<ItemDefinition> stores = new System.Collections.Generic.List<ItemDefinition>();

    public QuestDefinition UnlockedBy => unlockedBy;
    public float GlideSeconds => glideSeconds;
    public float HurtGraceSeconds => hurtGraceSeconds;
    public float MinDistance => minDistance;
    public float MaxDistance => Mathf.Max(minDistance, maxDistance);
    public float StartZoom => startZoom;
    public float PitchNear => pitchNear;
    public float PitchFar => pitchFar;
    public float FieldOfView => fieldOfView;
    public float PanSpeed => panSpeed;
    public float TurnSpeed => turnSpeed;
    public float DragTurnSensitivity => dragTurnSensitivity;
    public float ZoomStep => zoomStep;
    public float StickZoomSpeed => stickZoomSpeed;
    public float SettleSeconds => settleSeconds;
    public float BoundsRadius => boundsRadius;
    public float PickRange => pickRange;
    public float PadSnapRadius => padSnapRadius;
    public float EarHeight => earHeight;
    public System.Collections.Generic.IReadOnlyList<ItemDefinition> Stores => stores;
}
