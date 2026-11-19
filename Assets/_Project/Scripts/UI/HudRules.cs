using UnityEngine;

/// <summary>
/// What the HUD shows when nothing is happening (Options › Gameplay › HUD; the HUD review of 2026-11-18, the user: "right now its
/// cluttered"). Full is the HUD as it always was: everything all the time. Contextual, the default, shows a thing when it matters
/// (bars that aren't full or are in a fight, the noise meter when something can hear, the day's timeline before nightfall and dawn,
/// filled slots) and the rest small. Minimal is Contextual with the quest list kept to the quest followed. Each HUD piece asks here
/// rather than reading the option, so the three mean the same everywhere.
/// </summary>
public static class HudRules
{
    public enum Density
    {
        Full = 0,
        Contextual = 1,
        Minimal = 2,
    }

    /// <summary>The density chosen in the options.</summary>
    public static Density Current => (Density)Mathf.Clamp(GameOptions.Hud.Index, 0, 2);

    /// <summary>Everything all the time, as before the review.</summary>
    public static bool ShowsEverything => Current == Density.Full;

    /// <summary>The quest list kept to the quest followed.</summary>
    public static bool IsMinimal => Current == Density.Minimal;

    /// <summary>Something hunts the player's side: what a fight needs (bars, the noise meter) stays up.</summary>
    public static bool InAFight => MonsterAI.AnyHuntingPlayerSide();

    /// <summary>The minimap's size against the one it was built at (Full keeps it as it was).</summary>
    public static float MinimapScale => ShowsEverything ? 1f : 0.75f;
}
