using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one owner of "is the game paused". Anything that wants a pause (the Escape menu today, maybe a cutscene later)
/// calls <see cref="Request"/> with itself as the key and <see cref="Release"/> when done; the game stays paused while
/// any request is held, so two systems can never unpause each other.
/// <para>
/// Pausing and freezing time are separate on purpose. In single-player a pause also freezes time (Time.timeScale = 0),
/// which stops enemies, the raid timer and the player. A future online session should set
/// <see cref="FreezesTime"/> to false: the menu still opens and player input is still blocked, but the world keeps running.
/// Anything else that changes Time.timeScale (such as hit-stop) must check <see cref="IsTimeFrozen"/> first.
/// </para>
/// </summary>
public static class GamePause
{
    private static readonly HashSet<object> requests = new HashSet<object>();
    private static bool freezesTime = true;

    /// <summary>True while at least one system holds a pause request. Player input should ignore gameplay actions.</summary>
    public static bool IsPaused => requests.Count > 0;

    /// <summary>
    /// True while the Options screen previews a HUD setting over the paused game: the HUD shows though the game stays paused.
    /// </summary>
    public static bool HudPreview { get; set; }

    /// <summary>Whether the HUD hides for the pause: paused, and no HUD setting being previewed.</summary>
    public static bool HidesHud => IsPaused && !HudPreview;

    /// <summary>True while paused and time is frozen. Systems that change Time.timeScale must leave it alone then.</summary>
    public static bool IsTimeFrozen => IsPaused && freezesTime;

    /// <summary>Whether pausing freezes time. Turn off for multiplayer, where one player can't stop the world.</summary>
    public static bool FreezesTime
    {
        get => freezesTime;
        set
        {
            freezesTime = value;
            ApplyTimeScale();
        }
    }

    /// <summary>True while other players' games depend on this game's time (hosting friends in co-op: its creatures are
    /// theirs too). Nothing may slow time then, as hit-stop would stall the world they fight in.</summary>
    public static bool TimeIsShared { get; set; }

    /// <summary>Raised when the game becomes paused (true) or unpaused (false).</summary>
    public static event Action<bool> Changed;

    public static void Request(object owner)
    {
        if (owner == null || !requests.Add(owner) || requests.Count != 1)
        {
            return;
        }

        ApplyTimeScale();
        Changed?.Invoke(true);
    }

    public static void Release(object owner)
    {
        if (owner == null || !requests.Remove(owner) || requests.Count != 0)
        {
            return;
        }

        ApplyTimeScale();
        Changed?.Invoke(false);
    }

    private static void ApplyTimeScale()
    {
        Time.timeScale = IsTimeFrozen ? 0f : 1f;
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        requests.Clear();
        Changed = null;
        freezesTime = true;
        TimeIsShared = false;
        Time.timeScale = 1f;
    }
}
