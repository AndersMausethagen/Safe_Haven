using UnityEngine;

/// <summary>
/// Whether what is gathered grows back where it stood. Never in a raid's scene (the user, 2026-11-22: "I want the player to move around
/// and explore as much as possible of the raid. not stay in one safe space and circle back"): a stage's veins sprout new ore there
/// instead (StageReveal). At home not by the clock (unless HavenSettings › Nodes Regrow At Home), but by days and raids: a node gathered
/// in the Haven comes back after HavenSettings › Regrow After Days in-game days or Regrow After Raids raids, whichever is first (the user,
/// 2026-11-27: "Resource nodes should respawn after three days in the haven, or after three raids. Right now only bushes respawn after
/// three days"), never where a building now stands. Saplings brought from raids are still how felled trees come back (PlantedTrees).
/// Keyed to the scene the node lives in (a raid's director is up), not to the raid's run. The prologue grows back as before: its
/// gathering steps must never run dry.
/// </summary>
public static class Regrowth
{
    /// <summary>Nothing grows back here now.</summary>
    public static bool Stops => !Grows(RaidDirector.Current != null, HavenSettings.Current.NodesRegrowAtHome || PrologueDirector.Current != null);

    /// <summary><paramref name="seconds"/> where things grow back, 0 (never) where they don't.</summary>
    public static float After(float seconds) => Stops ? 0f : seconds;

    /// <summary>Whether gathered things grow back: never in a raid, at home only when the Haven allows it. Pure, for tests.</summary>
    public static bool Grows(bool inARaid, bool homeRegrows) => !inARaid && homeRegrows;

    /// <summary>The raids run in this game (the journal's lifetime count, not the last ones it remembers): what a node gathered at home
    /// waits on.</summary>
    public static int RaidsRun => GameSession.Progress.Journal.RaidsEver;

    /// <summary>The home days a node of <paramref name="definition"/> waits once gathered: its own (berry bushes), else the Haven's; 0 for
    /// one that never comes back (Respawn Seconds 0: the overgrowth) and for a tree (felled with an axe: saplings bring trees back, and a
    /// cleared stretch stays cleared).</summary>
    public static int HomeDays(GatheringNodeDefinition definition) =>
        definition == null ? 0 : definition.RegrowsAtHomeAfterDays > 0 ? definition.RegrowsAtHomeAfterDays
        : definition.RespawnSeconds > 0f && definition.GatherTool != ToolType.Axe ? HavenSettings.Current.RegrowAfterDays : 0;

    /// <summary>The raids it waits, the Haven's for any node that comes back by days at all.</summary>
    public static int HomeRaids(GatheringNodeDefinition definition) => HomeDays(definition) > 0 ? HavenSettings.Current.RegrowAfterRaids : 0;

    /// <summary>
    /// True once a node waiting for home day <paramref name="day"/> or raid count <paramref name="raid"/> (0: not waiting on it) may grow
    /// back, with <paramref name="today"/> and <paramref name="raids"/> as they are now. Pure, for tests.
    /// </summary>
    public static bool HomeDue(int day, int raid, int today, int raids) => (day > 0 && today >= day) || (raid > 0 && raids >= raid);

    /// <summary>True when a building (or its scaffold) stands within <paramref name="radius"/> of <paramref name="at"/>: nothing grows back
    /// inside a house.</summary>
    public static bool BuiltOver(Vector3 at, float radius = 0.75f)
    {
        var spot = new FootprintRect(new Vector2(at.x, at.z), new Vector2(radius, radius), 0f);
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building != null && BuildPlacement.Overlaps(building.Footprint, spot))
            {
                return true;
            }
        }

        return false;
    }
}
