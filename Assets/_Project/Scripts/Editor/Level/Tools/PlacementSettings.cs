using UnityEditor;
using UnityEngine;

/// <summary>How objects are put down in the Scene view (the World Tools' Place tab and overlay): kept per user in
/// EditorPrefs, so they survive restarts and are not shared through the project.</summary>
public static class PlacementSettings
{
    private const string Prefix = "SafeHaven.Place.";

    /// <summary>Objects dragged in from the Project window, duplicated or moved with the Move tool settle on the
    /// ground under them (by the bottom of their meshes).</summary>
    public static bool StickToGround
    {
        get => EditorPrefs.GetBool(Prefix + "Stick", true);
        set => EditorPrefs.SetBool(Prefix + "Stick", value);
    }

    /// <summary>Tilt placed objects with the slope they stand on.</summary>
    public static bool AlignToSlope
    {
        get => EditorPrefs.GetBool(Prefix + "Align", false);
        set => EditorPrefs.SetBool(Prefix + "Align", value);
    }

    /// <summary>Turn each placed object to a random direction.</summary>
    public static bool RandomTurn
    {
        get => EditorPrefs.GetBool(Prefix + "RandomTurn", true);
        set => EditorPrefs.SetBool(Prefix + "RandomTurn", value);
    }

    /// <summary>Random size between these for each placed object (1 = as made).</summary>
    public static Vector2 ScaleRange
    {
        get => new Vector2(EditorPrefs.GetFloat(Prefix + "ScaleMin", 0.9f), EditorPrefs.GetFloat(Prefix + "ScaleMax", 1.15f));
        set
        {
            EditorPrefs.SetFloat(Prefix + "ScaleMin", Mathf.Max(0.05f, Mathf.Min(value.x, value.y)));
            EditorPrefs.SetFloat(Prefix + "ScaleMax", Mathf.Max(0.05f, Mathf.Max(value.x, value.y)));
        }
    }

    /// <summary>Metres sunk into the ground, so nothing hovers over a bump.</summary>
    public static float Sink
    {
        get => EditorPrefs.GetFloat(Prefix + "Sink", 0.05f);
        set => EditorPrefs.SetFloat(Prefix + "Sink", Mathf.Max(0f, value));
    }
}
