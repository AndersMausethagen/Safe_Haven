using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The root of a scene's screen UI: the one screen-space canvas whose CanvasScaler decides how big all the UI is. Every
/// HUD and window in the scene is a child of it with its own nested Canvas (with Override Sorting for its draw order), so
/// a change inside one window only rebuilds that window, while scaling stays in one place.
/// <para>
/// In gameplay scenes use the UI Root prefab (Prefabs/UI/UIRoot), which also holds the UIStateManager, the EventSystem
/// and the Escape menu, and put the scene's HUD prefabs under its canvas. World-space canvases (health bars, interaction
/// prompts) and the loading screen are not part of it. Menu scenes can put this component on their own canvas.
/// </para>
/// <para>
/// UI scale: <see cref="Scale"/> multiplies the whole UI (1 = designed size at 1920x1080). It is the player's UI and Text
/// Size setting (GameOptions.UiScale), times the value set here on the root.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public sealed class UIRoot : MonoBehaviour
{
    public const float MinScale = 0.5f;
    public const float MaxScale = 2f;

    [Tooltip("The size the UI is designed for. Everything is laid out in these units.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("Multiplies the size of all UI in the scene, on top of the player's UI and Text Size setting.")]
    [SerializeField, Range(MinScale, MaxScale)] private float scale = 1f;

    private CanvasScaler scaler;

    /// <summary>The screen UI root of the loaded scene, or null.</summary>
    public static UIRoot Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public Canvas Canvas { get; private set; }

    public float Scale
    {
        get => scale;
        set
        {
            scale = Mathf.Clamp(value, MinScale, MaxScale);
            Apply();
        }
    }

    private void Awake()
    {
        Canvas = GetComponent<Canvas>();
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void OnEnable()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("More than one UIRoot is active; the UI scale option only reaches the newest.", this);
        }

        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnValidate()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    /// <summary>
    /// Sets up the scaler of a screen canvas made in code outside the root (a HUD, the dialogue box, the loading screen)
    /// the way the root's own is: designed at 1920x1080, at the current UI scale.
    /// </summary>
    public static void ConfigureScaler(CanvasScaler scaler)
    {
        Configure(scaler, DesignResolution, (Current != null ? Current.scale : 1f) * GameOptions.UiScale.Value);
    }

    /// <summary>The player changed the UI size: every screen canvas follows, including those made in code.</summary>
    public static void ApplyScaleEverywhere()
    {
        foreach (CanvasScaler each in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
        {
            if (each.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                continue;
            }

            if (each.TryGetComponent(out UIRoot root))
            {
                root.Apply();
            }
            else
            {
                ConfigureScaler(each);
            }
        }
    }

    private static readonly Vector2 DesignResolution = new Vector2(1920f, 1080f);

    // A smaller reference resolution makes every UI unit cover more screen pixels, so scale 1.5 means 1.5x bigger UI.
    // Anchored windows keep their place at the screen edges; only their size changes.
    // Expand keeps the whole reference layout on screen at any aspect: on an ultra-wide screen the height sets the size
    // and the extra width is room at the sides. (Matching width and height halfway grew the UI with the width until its
    // top and bottom fell off a 32:9 screen.)
    private static void Configure(CanvasScaler scaler, Vector2 reference, float uiScale)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referenceResolution = reference / Mathf.Max(MinScale, uiScale);
    }

    private void Apply()
    {
        if (scaler == null)
        {
            return;
        }

        Configure(scaler, referenceResolution, Application.isPlaying ? scale * GameOptions.UiScale.Value : scale);
    }
}
