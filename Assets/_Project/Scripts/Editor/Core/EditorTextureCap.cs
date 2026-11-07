using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps the Editor's memory down: textures are imported at most <see cref="EditorMaxSize"/> for working in the Editor
/// (Unity's Asset Import Override, local to this machine's Library, never in the project). The forest and mountain packs
/// bring hundreds of 4K and 8K textures and the Editor keeps a full copy of each it has loaded; capped, the Haven needs
/// about 4 GB less. From the top-down camera 2K and 4K look the same.
/// <para>
/// Builds always get full-size textures: <see cref="SafeHavenBuild.BuildWindows"/> lifts the cap for the build and puts it
/// back after, and any other build started with the cap on stops with an explanation. Toggle under Safe Haven › Editor.
/// </para>
/// </summary>
public sealed class EditorTextureCap : IPreprocessBuildWithReport
{
    public const int EditorMaxSize = 2048;
    private const string MenuPath = "Safe Haven/Editor/Light Textures in the Editor";

    public int callbackOrder => -1000;

    /// <summary>True while the Editor imports textures capped.</summary>
    public static bool IsOn => EditorUserBuildSettings.overrideMaxTextureSize > 0;

    [MenuItem(MenuPath)]
    private static void Toggle() => Set(!IsOn);

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, IsOn);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    /// <summary>Caps (or lifts the cap on) the Editor's textures and reimports the ones that change.</summary>
    public static void Set(bool on)
    {
        int size = on ? EditorMaxSize : 0;
        if (EditorUserBuildSettings.overrideMaxTextureSize == size)
        {
            return;
        }

        EditorUserBuildSettings.overrideMaxTextureSize = size;
        AssetDatabase.Refresh();
        Debug.Log(on ? $"Editor textures capped at {EditorMaxSize} (builds stay full size)." : "Editor textures at full size.");
    }

    /// <summary>Runs <paramref name="build"/> with full-size textures, then restores the cap if it was on.</summary>
    public static T WithFullTextures<T>(Func<T> build)
    {
        bool wasOn = IsOn;
        Set(false);
        try
        {
            return build();
        }
        finally
        {
            Set(wasOn);
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (IsOn)
        {
            throw new BuildFailedException(
                $"The Editor's textures are capped at {EditorMaxSize} to save memory, and a build would ship them that way. " +
                $"Build with SafeHavenBuild.BuildWindows (it lifts the cap for the build), or turn off {MenuPath} first.");
        }
    }
}
