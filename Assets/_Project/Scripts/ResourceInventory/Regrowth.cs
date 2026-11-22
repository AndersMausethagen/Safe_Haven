/// <summary>
/// Whether what is gathered grows back where it stood. Never in a raid's scene (the user, 2026-11-22: "I want the player to move around
/// and explore as much as possible of the raid. not stay in one safe space and circle back"): a stage's veins sprout new ore there
/// instead (StageReveal). Not at home either, unless HavenSettings › Nodes Regrow At Home (the user, 2026-11-22: "I dont think that
/// resources should respawn in the haven... it's not really needed to mine for stone or wood in raids. I think that should be the
/// core loop"): saplings brought from raids are how trees come back (PlantedTrees), and a felled or broken node stays gone in the
/// save (ResourceNode, TerrainTreeMemory). Keyed to the scene the node lives in (a raid's director is up), not to the raid's run.
/// </summary>
public static class Regrowth
{
    /// <summary>Nothing grows back here now.</summary>
    public static bool Stops => !Grows(RaidDirector.Current != null, HavenSettings.Current.NodesRegrowAtHome);

    /// <summary><paramref name="seconds"/> where things grow back, 0 (never) where they don't.</summary>
    public static float After(float seconds) => Stops ? 0f : seconds;

    /// <summary>Whether gathered things grow back: never in a raid, at home only when the Haven allows it. Pure, for tests.</summary>
    public static bool Grows(bool inARaid, bool homeRegrows) => !inARaid && homeRegrows;
}
