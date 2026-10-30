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

    /// <summary>Carries out the window, VSync and frame limit. The Editor's Game view keeps its own size.</summary>
    public static void Apply()
    {
        QualitySettings.vSyncCount = GameOptions.VSync.On ? 1 : 0;
        Application.targetFrameRate = GameOptions.VSync.On ? -1 : GameOptions.FrameRate;
        if (Application.isEditor)
        {
            return;
        }

        Vector2Int size = Resolution;
        Screen.SetResolution(size.x, size.y, Modes[Mode]);
    }
}
