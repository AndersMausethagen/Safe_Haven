using System;
using UnityEngine;

/// <summary>
/// Shows and hides one UI window and reports its open state to the UIStateManager. Put it on an object that stays
/// active (such as the canvas) and point <see cref="content"/> at the part that is shown and hidden. Hiding the content
/// deactivates it, so anything inside rebuilds itself when the window opens again. A window fades in as it opens
/// (UIStyle › Window Fade Seconds) and closes at once, so nothing behind a closed window is left waiting on a fade.
/// </summary>
public sealed class UIWindow : MonoBehaviour
{
    // The next line of a conversation or a step through a menu closes and opens the same window: that isn't one arriving.
    private const float ReopenSeconds = 0.1f;
    private const float LongestFadeStep = 1f / 30f;

    [SerializeField] private UIWindowId id;
    [Tooltip("The object that is shown and hidden. Must be a different object from the one this component is on.")]
    [SerializeField] private GameObject content;
    [SerializeField] private bool startOpen;

    private UIStateManager uiState;
    private bool initialised;
    private CanvasGroup fade;
    private float fadeShown = -1f;
    private float closedAt = float.NegativeInfinity;

    public UIWindowId Id => id;
    public bool IsOpen { get; private set; }

    /// <summary>Raised after the window opens (true) or closes (false).</summary>
    public event Action<bool> OpenChanged;

    /// <summary>Sets up a window made in code. Call it while its object is still inactive (before OnEnable).</summary>
    public void Configure(UIWindowId windowId, GameObject windowContent)
    {
        id = windowId;
        content = windowContent;
    }

    private void OnEnable()
    {
        if (content == null || content == gameObject)
        {
            Debug.LogError("UIWindow needs a content object that is not the object it is on.", this);
            enabled = false;
            return;
        }

        uiState = UIStateManager.Current;
        if (uiState != null)
        {
            uiState.WindowToggleRequested += OnToggleRequested;
            uiState.Register(this, true);
        }
        else
        {
            Debug.LogWarning("UIWindow found no UIStateManager; opening it won't lock the camera.", this);
        }

        Apply(initialised ? IsOpen : startOpen);
        initialised = true;
    }

    private void Update()
    {
        // The screen or the UI size changed while open (a window dragged, the option moved): fit again.
        if (IsOpen && (Screen.width != fittedWidth || Screen.height != fittedHeight || !Mathf.Approximately(fittedRoom, RoomHeight())))
        {
            FitToScreen();
        }

        if (fadeShown < 0f)
        {
            return;
        }

        // By the step, with a limit: filling a big window can take a long first frame, and the fade would be over before it is seen.
        float seconds = UIStyle.Current.WindowFadeSeconds;
        fadeShown = seconds > 0f ? Mathf.Min(1f, fadeShown + Mathf.Min(Time.unscaledDeltaTime, LongestFadeStep) / seconds) : 1f;
        fade.alpha = fadeShown;
        // Hearthline 3: it settles in as it fades in, from a touch smaller and lower.
        float eased = 1f - (1f - fadeShown) * (1f - fadeShown);
        content.transform.localScale = Vector3.one * Mathf.Lerp(0.965f, 1f, eased) * contentFit;
        if (content.transform is RectTransform rect)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Lerp(-14f, 0f, eased) + settledY);
        }

        if (fadeShown >= 1f)
        {
            fadeShown = -1f;
        }
    }

    private void OnDisable()
    {
        EndFade();
        if (uiState != null)
        {
            uiState.WindowToggleRequested -= OnToggleRequested;
            uiState.Register(this, false);
            // A disabled window can't be open, or the camera would stay locked.
            uiState.SetWindowOpen(this, false);
            uiState = null;
        }
    }

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        if (open == IsOpen)
        {
            return;
        }

        if (open)
        {
            BeginFade();
        }
        else
        {
            EndFade();
            closedAt = Time.unscaledTime;
        }

        Apply(open);
    }

    private void Apply(bool open)
    {
        IsOpen = open;
        content.SetActive(open);
        if (open)
        {
            FitToScreen();
        }

        if (uiState != null)
        {
            uiState.SetWindowOpen(this, open);
        }

        UIBackdrop.Track(this, open);
        OpenChanged?.Invoke(open);
    }

    private void BeginFade()
    {
        if (UIStyle.Current.WindowFadeSeconds <= 0f || Time.unscaledTime - closedAt < ReopenSeconds)
        {
            return;
        }

        if (fade == null && !content.TryGetComponent(out fade))
        {
            fade = content.AddComponent<CanvasGroup>();
        }

        fade.alpha = 0f;
        fadeShown = 0f;
        settledY = content.transform is RectTransform rect ? rect.anchoredPosition.y : 0f;
    }

    // Where its content rests (it rises into place as it opens).
    private float settledY;

    private void EndFade()
    {
        if (fadeShown >= 0f && fade != null)
        {
            fade.alpha = 1f;
            content.transform.localScale = Vector3.one * contentFit;
            if (content.transform is RectTransform rect)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, settledY);
            }
        }

        fadeShown = -1f;
    }

    // ----- Fitting the screen -----
    // A window is laid out at 1920x1080 units, and the player's UI and Text Size (up to 2x) makes those units bigger: a tall window
    // (the character sheet, the skills) ran off the screen at 1.5 (UX pass 2026-11-28). On opening, the window's panel (its content
    // when that is a fixed-size box, else each fixed-size box directly inside it) is shrunk whole, in proportion, to fit; nothing
    // stretched is touched, and a box that fits again goes back to its own size.
    private const float ScreenMargin = 24f;
    private float contentFit = 1f;
    private int fittedWidth = -1;
    private int fittedHeight = -1;
    private float fittedRoom = -1f;
    private readonly System.Collections.Generic.List<RectTransform> shrunk = new System.Collections.Generic.List<RectTransform>();

    private RectTransform Room()
    {
        Canvas canvas = content != null ? content.GetComponentInParent<Canvas>(true) : null;
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }

    private float RoomHeight()
    {
        RectTransform room = Room();
        return room != null ? room.rect.height : 0f;
    }

    private void FitToScreen()
    {
        fittedWidth = Screen.width;
        fittedHeight = Screen.height;
        fittedRoom = RoomHeight();
        RectTransform room = Room();
        if (room == null || !(content.transform is RectTransform box))
        {
            return;
        }

        Rect space = room.rect;
        if (IsFixed(box))
        {
            contentFit = Fit(box.rect.size, space);
            if (fadeShown < 0f)
            {
                box.localScale = Vector3.one * contentFit;
            }

            return;
        }

        contentFit = 1f;
        foreach (Transform child in box)
        {
            if (!(child is RectTransform panel) || !IsFixed(panel) || !child.gameObject.activeSelf)
            {
                continue;
            }

            float fit = Fit(panel.rect.size, space);
            if (fit < 1f)
            {
                panel.localScale = Vector3.one * fit;
                if (!shrunk.Contains(panel))
                {
                    shrunk.Add(panel);
                }
            }
            else if (shrunk.Remove(panel))
            {
                panel.localScale = Vector3.one;
            }
        }
    }

    // A box of its own size (anchored at a point), not one stretched with its parent.
    private static bool IsFixed(RectTransform rect) => rect.anchorMin == rect.anchorMax;

    private static float Fit(Vector2 size, Rect space)
    {
        if (size.x <= 1f || size.y <= 1f || space.width <= 1f || space.height <= 1f)
        {
            return 1f;
        }

        return Mathf.Clamp(Mathf.Min((space.width - ScreenMargin) / size.x, (space.height - ScreenMargin) / size.y), 0.3f, 1f);
    }

    private void OnToggleRequested(UIWindowId requested)
    {
        if (requested == id)
        {
            Toggle();
        }
    }
}
