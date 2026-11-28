/// <summary>How the player camera frames the game.</summary>
public enum CameraStyle
{
    /// <summary>The V Rising style high-angle view aimed with the mouse cursor.</summary>
    TopDown = 0,
    /// <summary>The Enshrouded style over-the-shoulder view: the mouse turns the camera and the crosshair aims.</summary>
    Action = 1,
}

/// <summary>Situations the player can give their own camera style in the options.</summary>
public enum CameraContext
{
    Exploration = 0,
    Combat = 1,
    Building = 2,
    Raid = 3,
}

/// <summary>What a <see cref="CameraContext"/> does to the style the player toggled.</summary>
public enum CameraStyleOverride
{
    /// <summary>Use whatever the player toggled with the camera key.</summary>
    PlayerChoice = 0,
    TopDown = 1,
    Action = 2,
}

/// <summary>What is going on right now, as far as the camera style cares.</summary>
public readonly struct CameraSituation
{
    public readonly bool Building;
    public readonly bool Combat;
    public readonly bool Raid;

    public CameraSituation(bool building, bool combat, bool raid)
    {
        Building = building;
        Combat = combat;
        Raid = raid;
    }
}

/// <summary>
/// Picks the camera style from the player's toggle and the per-situation overrides. Several situations can apply at
/// once (combat inside a raid), so they are checked from the most specific to the most general: Building, Combat, Raid,
/// then Exploration (any time out of combat). The first one that overrides decides; when none do, the toggle decides.
/// </summary>
public static class CameraStyleRules
{
    public const int ContextCount = 4;

    private static readonly CameraContext[] Priority =
    {
        CameraContext.Building,
        CameraContext.Combat,
        CameraContext.Raid,
        CameraContext.Exploration,
    };

    public static CameraStyle Resolve(CameraStyle chosen, CameraStyleOverride[] overrides, CameraSituation situation, out CameraContext? decidedBy)
    {
        decidedBy = null;
        if (overrides == null)
        {
            return chosen;
        }

        foreach (CameraContext context in Priority)
        {
            int index = (int)context;
            if (!Applies(context, situation) || index >= overrides.Length || overrides[index] == CameraStyleOverride.PlayerChoice)
            {
                continue;
            }

            decidedBy = context;
            return overrides[index] == CameraStyleOverride.Action ? CameraStyle.Action : CameraStyle.TopDown;
        }

        return chosen;
    }

    public static bool Applies(CameraContext context, CameraSituation situation)
    {
        switch (context)
        {
            case CameraContext.Building: return situation.Building;
            case CameraContext.Combat: return situation.Combat;
            case CameraContext.Raid: return situation.Raid;
            default: return !situation.Combat;
        }
    }

    public static string Label(CameraStyle style) => style == CameraStyle.Action ? "Action" : "Top-down";

    public static string Label(CameraStyleOverride value)
    {
        switch (value)
        {
            case CameraStyleOverride.TopDown: return "Top-down";
            case CameraStyleOverride.Action: return "Action";
            // It follows Camera Style above (design review 2026-11-28: "Your choice" said nothing of what it does).
            default: return "As Camera Style";
        }
    }
}
