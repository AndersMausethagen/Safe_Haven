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
        content.transform.localScale = Vector3.one * Mathf.Lerp(0.965f, 1f, eased);
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
        uiState?.SetWindowOpen(this, open);
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
            content.transform.localScale = Vector3.one;
            if (content.transform is RectTransform rect)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, settledY);
            }
        }

        fadeShown = -1f;
    }

    private void OnToggleRequested(UIWindowId requested)
    {
        if (requested == id)
        {
            Toggle();
        }
    }
}
