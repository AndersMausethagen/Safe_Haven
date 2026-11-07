using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for whether any UI window is open. It is separate from GameState on purpose: opening the
/// inventory doesn't change the gameplay mode, it only means systems like the camera should leave the player's mouse
/// alone. Systems poll <see cref="IsAnyWindowOpen"/> or listen to <see cref="AnyWindowOpenChanged"/>; windows report in
/// through UIWindow, and input asks for one with <see cref="RequestToggle"/>. The manager never touches the systems.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class UIStateManager : MonoBehaviour
{
    private readonly HashSet<UIWindow> openWindows = new HashSet<UIWindow>();
    private readonly Dictionary<UIWindowId, UIWindow> windows = new Dictionary<UIWindowId, UIWindow>();

    /// <summary>The manager of the current scene, or null in scenes without one.</summary>
    public static UIStateManager Current { get; private set; }

    public bool IsAnyWindowOpen => openWindows.Count > 0;

    /// <summary>Raised when the first window opens (true) and when the last one closes (false).</summary>
    public event Action<bool> AnyWindowOpenChanged;

    /// <summary>Raised when input wants a window toggled. The window with that id answers it.</summary>
    public event Action<UIWindowId> WindowToggleRequested;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one UIStateManager can exist at a time.", this);
            Destroy(this);
            return;
        }

        Current = this;
    }

    // Listeners unsubscribe through the reference they took when they subscribed, so none of them need this one to outlive the scene.
    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    private void OnEnable()
    {
        BackHandlers.Register(BackHandlers.WindowsPriority, CloseAll);
    }

    private void OnDisable()
    {
        BackHandlers.Unregister(CloseAll);
    }

    /// <summary>Closes every open window. Returns true if any was open. Escape uses it before opening the Escape menu.</summary>
    public bool CloseAll()
    {
        if (openWindows.Count == 0)
        {
            return false;
        }

        foreach (UIWindow window in new List<UIWindow>(openWindows))
        {
            window.Close();
        }

        return true;
    }

    public void RequestToggle(UIWindowId id)
    {
        WindowToggleRequested?.Invoke(id);
    }

    /// <summary>Called by UIWindow when it becomes available or goes away.</summary>
    public void Register(UIWindow window, bool available)
    {
        if (available)
        {
            windows[window.Id] = window;
        }
        else if (windows.TryGetValue(window.Id, out UIWindow known) && known == window)
        {
            windows.Remove(window.Id);
        }
    }

    /// <summary>
    /// The script of a window, such as the StashWindow for <see cref="UIWindowId.Stash"/>. It sits on or under the
    /// window's UIWindow, which is always active, so this works while the window is closed. Null when there is none.
    /// </summary>
    public T Find<T>(UIWindowId id) where T : Component
    {
        return windows.TryGetValue(id, out UIWindow window) && window != null ? window.GetComponentInChildren<T>(true) : null;
    }

    public bool IsOpen(UIWindowId id)
    {
        foreach (UIWindow window in openWindows)
        {
            if (window.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Called by UIWindow whenever it opens or closes.</summary>
    public void SetWindowOpen(UIWindow window, bool isOpen)
    {
        bool wasAnyOpen = IsAnyWindowOpen;
        bool changed = isOpen ? openWindows.Add(window) : openWindows.Remove(window);

        // The skill book occupies the same screen area as the other windows. Register the newly opened window first
        // so closing the old one never briefly unlocks the camera. Existing non-skill window combinations are unchanged.
        if (isOpen)
        {
            var conflicts = new List<UIWindow>();
            foreach (UIWindow other in openWindows)
            {
                if (other != window && (window.Id == UIWindowId.Skills || other.Id == UIWindowId.Skills))
                {
                    conflicts.Add(other);
                }
            }

            foreach (UIWindow other in conflicts)
            {
                other.Close();
            }
        }

        if (changed && wasAnyOpen != IsAnyWindowOpen)
        {
            AnyWindowOpenChanged?.Invoke(IsAnyWindowOpen);
        }
    }
}
