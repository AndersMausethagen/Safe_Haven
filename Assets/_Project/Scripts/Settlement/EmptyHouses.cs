/// <summary>
/// An empty house for someone lost (GDD §15.6; Docs/ColonyModeResearch_2026-11-21.md §4.9): the house a raid companion lived in keeps
/// their bed while the forest has them (FallenRecord.Home; HavenHomes gives it to nobody else), stands dark at night (NightFlame) and, in
/// the Keeper's view, says whose it is and that a rumour on a raid may lead to them (RaidRumourDefinition › Seeks Lost). Found again,
/// they come home to it. Plain reads of the settlement, cheap enough for every torch twice a second.
/// </summary>
public static class EmptyHouses
{
    /// <summary>The lost one whose house <paramref name="houseId"/> was, or null.</summary>
    public static FallenRecord WaitingFor(IHavenView home, string houseId)
    {
        if (home == null || string.IsNullOrEmpty(houseId))
        {
            return null;
        }

        foreach (FallenRecord lost in home.Fallen)
        {
            if (lost.Resident != null && lost.Home == houseId)
            {
                return lost;
            }
        }

        return null;
    }

    /// <summary>Beds of <paramref name="houseId"/> kept for those lost.</summary>
    public static int BedsKept(IHavenView home, string houseId)
    {
        int kept = 0;
        if (home == null || string.IsNullOrEmpty(houseId))
        {
            return kept;
        }

        foreach (FallenRecord lost in home.Fallen)
        {
            kept += lost.Resident != null && lost.Home == houseId ? 1 : 0;
        }

        return kept;
    }

    /// <summary>Whether <paramref name="house"/> stands empty for someone lost: their bed kept, and nobody else lives in it.</summary>
    public static bool StandsEmpty(BuildingObject house)
    {
        IHavenView home = GameSession.Here;
        if (house == null || home == null || home.Fallen.Count == 0)
        {
            return false;
        }

        string id = house.Id;
        if (WaitingFor(home, id) == null)
        {
            return false;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Home == id)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>What the view says of a house kept for <paramref name="lost"/>: "Tor's, empty · lost in the Misty Woods".</summary>
    public static string Tell(FallenRecord lost) =>
        lost == null ? null : $"{lost.Resident.DisplayName}'s, empty · lost {(string.IsNullOrWhiteSpace(lost.Where) ? "on a raid" : "in " + lost.Where)}";
}
