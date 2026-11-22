/// <summary>
/// Whether what is gathered grows back where it stood: at home, after its own seconds; never in a raid's scene (the user, 2026-11-22:
/// "I want the player to move around and explore as much as possible of the raid. not stay in one safe space and circle back"). A
/// stage's veins sprout new ore there instead (StageReveal). Keyed to the scene the node lives in (a raid's director is up), not to
/// the raid's run: the run begins at the portal's step while the Haven is still saved on the way out, and its rocks must still grow back.
/// </summary>
public static class Regrowth
{
    /// <summary>In a raid's scene: nothing grows back.</summary>
    public static bool Stops => RaidDirector.Current != null;

    /// <summary><paramref name="seconds"/> at home, 0 (never) in a raid's scene.</summary>
    public static float After(float seconds) => Stops ? 0f : seconds;
}
