using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The window: fullscreen or not, and at what resolution. Stored in PlayerPrefs like the other settings, but by size
/// rather than as an index, because the list of resolutions depends on the screen. VSync and the frame limit are
/// GameOptions; <see cref="Apply"/> carries them out too.
/// </summary>
public static class DisplayOptions
{
    private const string ModeKey = "SafeHaven.Display.Mode";
    private const string WidthKey = "SafeHaven.Display.Width";
    private const string HeightKey = "SafeHaven.Display.Height";

    public static readonly string[] ModeNames = { "Fullscreen", "Borderless Window", "Window" };
    private static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

    /// <summary>Index into <see cref="ModeNames"/>.</summary>
    public static int Mode
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 1), 0, Modes.Length - 1);
        set => PlayerPrefs.SetInt(ModeKey, Mathf.Clamp(value, 0, Modes.Length - 1));
    }

    /// <summary>The window size asked for: the saved one, or the screen's own the first time.</summary>
    public static Vector2Int Resolution
    {
        get => new Vector2Int(PlayerPrefs.GetInt(WidthKey, Display.main.systemWidth), PlayerPrefs.GetInt(HeightKey, Display.main.systemHeight));
        set
        {
            PlayerPrefs.SetInt(WidthKey, value.x);
            PlayerPrefs.SetInt(HeightKey, value.y);
        }
    }

    /// <summary>The sizes this screen offers, largest last, each once. Always holds the size in use.</summary>
    public static List<Vector2Int> Resolutions()
    {
        var sizes = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (!sizes.Contains(size))
            {
                sizes.Add(size);
            }
        }

        Vector2Int current = Resolution;
        if (!sizes.Contains(current))
        {
            sizes.Add(current);
        }

        sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return sizes;
    }

    /// <summary>
    /// How the frame limit is kept. Without VSync, by the frame rate alone. With VSync, Unity ignores a target frame rate, so
    /// before 2026-11-11 the limit did nothing and the game drew as fast as the screen refreshes (240 a second on a 240 Hz
    /// screen, whatever was chosen: a player's GPU ran flat out at "30"). Now it presents every Nth refresh (VSync count, up to
    /// 4) and, past that, draws every Nth frame, never above the limit. When no whole fraction of the refresh rate comes within
    /// a tenth of the limit (60 on a 144 Hz screen would be 48), the limit is kept without VSync instead.
    /// </summary>
    public static (int vSyncCount, int renderEvery, int targetFrameRate) Pacing(bool vSync, int limit, double refreshRate)
    {
        if (!vSync)
        {
            return (0, 1, limit);
        }

        if (limit <= 0 || refreshRate <= 0 || refreshRate <= limit * 1.02)
        {
            return (1, 1, -1);
        }

        int every = Mathf.CeilToInt((float)(refreshRate / limit) - 0.01f);
        if (refreshRate / every < limit * 0.9)
        {
            return (0, 1, limit);
        }

        // Every Nth refresh exactly: the most refreshes VSync can wait (up to 4) that divide N, the rest by drawing every Mth frame.
        int count = Mathf.Min(4, every);
        while (every % count != 0)
        {
            count--;
        }

        return (count, every / count, -1);
    }

    /// <summary>Carries out the window, VSync and frame limit. The Editor's Game view keeps its own size.</summary>
    public static void Apply()
    {
        ApplyPacing();
        if (Application.isEditor)
        {
            return;
        }

        Vector2Int size = Resolution;
        Screen.SetResolution(size.x, size.y, Modes[Mode]);
    }

    /// <summary>VSync and the frame limit alone: changing them set the window again, which can flicker in exclusive fullscreen.</summary>
    public static void ApplyPacing()
    {
        (int vSyncCount, int renderEvery, int targetFrameRate) = Pacing(GameOptions.VSync.On, GameOptions.FrameRate, Screen.currentResolution.refreshRateRatio.value);
        QualitySettings.vSyncCount = vSyncCount;
        UnityEngine.Rendering.OnDemandRendering.renderFrameInterval = renderEvery;
        Application.targetFrameRate = targetFrameRate;
    }
}
