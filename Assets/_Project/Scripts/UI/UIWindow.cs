using System;
using UnityEngine;

/// <summary>
/// Shows and hides one UI window and reports its open state to the UIStateManager. Put it on an object that stays
/// active (such as the canvas) and point <see cref="content"/> at the part that is shown and hidden. Hiding the content
/// deactivates it, so anything inside rebuilds itself when the window opens again.
/// </summary>
public sealed class UIWindow : MonoBehaviour
{
    [SerializeField] private UIWindowId id;
    [Tooltip("The object that is shown and hidden. Must be a different object from the one this component is on.")]
    [SerializeField] private GameObject content;
    [SerializeField] private bool startOpen;

    private UIStateManager uiState;
    private bool initialised;

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

        uiState = UIStateManager.Instance;
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

    private void OnDisable()
    {
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
        if (open != IsOpen)
        {
            Apply(open);
        }
    }

    private void Apply(bool open)
    {
        IsOpen = open;
        content.SetActive(open);
        uiState?.SetWindowOpen(this, open);
        OpenChanged?.Invoke(open);
    }

    private void OnToggleRequested(UIWindowId requested)
    {
        if (requested == id)
        {
            Toggle();
        }
    }
}
