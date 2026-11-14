using System.Collections.Generic;
using TMPro;

/// <summary>
/// How a controller button is shown in text: an inline TextMesh Pro sprite from the Controller Glyphs sprite asset
/// (TMP Settings' default sprite asset; Tools/UI/controller_glyphs.py draws it, Safe Haven › UI › Build Controller Glyphs
/// builds it), named &lt;family&gt;_&lt;control&gt; after the Gamepad controls. Without the asset the button's name is written
/// instead ("A", "Cross", "ZL"), so a prompt never shows a raw tag.
/// </summary>
public static class ControllerGlyphs
{
    public const string SpriteAssetName = "Controller Glyphs";

    // Gamepad controls by their names in a binding path; usages ({Submit}) and aliases are the button they stand for.
    // Read-only: a table, not state (nothing for the play-session reset to clear).
    private static readonly IReadOnlyDictionary<string, string> Controls = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "buttonSouth", "buttonSouth" }, { "a", "buttonSouth" }, { "cross", "buttonSouth" }, { "{Submit}", "buttonSouth" },
        { "buttonEast", "buttonEast" }, { "b", "buttonEast" }, { "circle", "buttonEast" }, { "{Cancel}", "buttonEast" }, { "{Back}", "buttonEast" },
        { "buttonWest", "buttonWest" }, { "x", "buttonWest" }, { "square", "buttonWest" },
        { "buttonNorth", "buttonNorth" }, { "y", "buttonNorth" }, { "triangle", "buttonNorth" },
        { "leftShoulder", "leftShoulder" }, { "rightShoulder", "rightShoulder" },
        { "leftTrigger", "leftTrigger" }, { "rightTrigger", "rightTrigger" },
        { "start", "start" }, { "{Menu}", "start" }, { "select", "select" },
        { "leftStick", "leftStick" }, { "rightStick", "rightStick" },
        { "leftStickPress", "leftStickPress" }, { "rightStickPress", "rightStickPress" },
        { "dpad", "dpad" }, { "dpad/up", "dpadUp" }, { "dpad/down", "dpadDown" }, { "dpad/left", "dpadLeft" }, { "dpad/right", "dpadRight" },
    };

    private static readonly Dictionary<string, string>[] Names =
    {
        // Xbox
        new Dictionary<string, string>
        {
            { "buttonSouth", "A" }, { "buttonEast", "B" }, { "buttonWest", "X" }, { "buttonNorth", "Y" }, { "leftShoulder", "LB" }, { "rightShoulder", "RB" },
            { "leftTrigger", "LT" }, { "rightTrigger", "RT" }, { "start", "Menu" }, { "select", "View" }, { "leftStick", "LS" }, { "rightStick", "RS" },
            { "leftStickPress", "LS" }, { "rightStickPress", "RS" },
        },
        // PlayStation
        new Dictionary<string, string>
        {
            { "buttonSouth", "Cross" }, { "buttonEast", "Circle" }, { "buttonWest", "Square" }, { "buttonNorth", "Triangle" }, { "leftShoulder", "L1" },
            { "rightShoulder", "R1" }, { "leftTrigger", "L2" }, { "rightTrigger", "R2" }, { "start", "Options" }, { "select", "Create" },
            { "leftStick", "L" }, { "rightStick", "R" }, { "leftStickPress", "L3" }, { "rightStickPress", "R3" },
        },
        // Switch: the bottom button is B and the right one A.
        new Dictionary<string, string>
        {
            { "buttonSouth", "B" }, { "buttonEast", "A" }, { "buttonWest", "Y" }, { "buttonNorth", "X" }, { "leftShoulder", "L" }, { "rightShoulder", "R" },
            { "leftTrigger", "ZL" }, { "rightTrigger", "ZR" }, { "start", "+" }, { "select", "-" }, { "leftStick", "L Stick" }, { "rightStick", "R Stick" },
            { "leftStickPress", "L Stick" }, { "rightStickPress", "R Stick" },
        },
    };

    private static readonly string[] FamilyPrefix = { "xbox_", "playstation_", "switch_" };

    // Tags are built once each: a HUD may ask for a label every frame.
    private static readonly Dictionary<string, string> Tags = new Dictionary<string, string>();
    private static bool? spritesAvailable;

    /// <summary>The control a Gamepad binding path names ("&lt;Gamepad&gt;/dpad/up" is "dpadUp"), or null when it is not a pad's.</summary>
    public static string ControlOf(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        int slash = path.StartsWith("<") ? path.IndexOf('/') : -1;
        string control = slash >= 0 ? path.Substring(slash + 1) : path.StartsWith("*/") ? path.Substring(2) : path;
        if (Controls.TryGetValue(control, out string found))
        {
            return found;
        }

        // A direction of a stick ("leftStick/up") is the stick.
        int part = control.IndexOf('/');
        return part > 0 && Controls.TryGetValue(control.Substring(0, part), out found) ? found : null;
    }

    /// <summary>The control as shown in text for this kind of pad: an inline glyph, or its name. Null when the path names no pad control.</summary>
    public static string For(string path, PadFamily family)
    {
        string control = ControlOf(path);
        if (control == null)
        {
            return null;
        }

        return HasSprites ? Tag(FamilyPrefix[(int)family] + control) : NameOf(control, family);
    }

    /// <summary>The button's printed name on this kind of pad ("A", "Cross", "ZL").</summary>
    public static string NameOf(string control, PadFamily family)
    {
        if (Names[(int)family].TryGetValue(control, out string name))
        {
            return name;
        }

        return control.StartsWith("dpad") ? (control.Length > 4 ? "D-Pad " + control.Substring(4) : "D-Pad") : control;
    }

    /// <summary>True when the text is a glyph rather than a key's name.</summary>
    public static bool IsGlyph(string label) => label != null && label.StartsWith("<sprite", System.StringComparison.Ordinal);

    private static string Tag(string name)
    {
        if (!Tags.TryGetValue(name, out string tag))
        {
            tag = $"<sprite name=\"{name}\">";
            Tags[name] = tag;
        }

        return tag;
    }

    /// <summary>Looks for the sprite asset again (it was just built), and on every play session.</summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Refresh()
    {
        spritesAvailable = null;
        Tags.Clear();
    }

    // The glyphs are TMP Settings' default sprite asset; looked up once.
    private static bool HasSprites
    {
        get
        {
            if (!spritesAvailable.HasValue)
            {
                TMP_SpriteAsset asset = TMP_Settings.instance != null ? TMP_Settings.defaultSpriteAsset : null;
                spritesAvailable = asset != null && asset.name == SpriteAssetName;
            }

            return spritesAvailable.Value;
        }
    }
}
