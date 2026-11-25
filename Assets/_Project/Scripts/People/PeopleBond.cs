/// <summary>
/// A resident's bond with the player (Docs/PeopleSystemReview_2026-11-23.md §1.1, the user's go-ahead of 2026-11-23): not only raids along
/// but a lesson taken, an ask answered, a wish met, kit given, a gift they love, a hard mark healed, help in a breach and a friend brought
/// home from the forest all grow it (PeopleSettings › Bond). The bond ranks (SettlementSettings › Bond Ranks: Trusted, Sworn; their
/// thresholds read as bond, a raid along being one) follow it, and each rank reached owes a passion pick once. Found again after the forest,
/// they come home strangers: the bond starts again (their picks are not paid twice).
/// </summary>
public static class PeopleBond
{
    /// <summary>Their bond rank now, or null below the first.</summary>
    public static CompanionBondRank Rank(ResidentRecord record) =>
        record == null ? null : CompanionBond.RankFor(UnityEngine.Mathf.FloorToInt(record.Bond + 0.0001f), SettlementSettings.Current.BondRanks);

    /// <summary>
    /// The bond grows by <paramref name="amount"/> (<paramref name="why"/> for the log). A rank reached is told and owes a passion pick.
    /// True when it rose into a new rank.
    /// </summary>
    public static bool Grow(ResidentRecord record, float amount, string why)
    {
        if (record == null || record.Resident == null || amount <= 0f)
        {
            return false;
        }

        CompanionBondRank before = Rank(record);
        record.Bond += amount;
        CompanionBondRank after = Rank(record);
        PlaytestLogger.Note(PlaytestLog.Bond, record.Resident.Id, UnityEngine.Mathf.RoundToInt(record.Bond * 100f), why);
        if (after == null || after == before)
        {
            return false;
        }

        bool owed = OweReachedRanks(record) > 0;
        // The passion is promised only when a pick was owed now (found again and rising through a rank a second time owes none).
        PeopleNews.Tell($"{record.Resident.DisplayName} is {after.name} with you now" + (owed ? ": there is a passion they would grow." : "."),
            MessageKind.Gain);
        return true;
    }

    /// <summary>
    /// Owes the passion pick of every bond rank the bond reaches and has not paid yet: the one just reached, one passed on the way (a
    /// bond that jumped two ranks), and on load the ranks a save from before picks already held (bug hunt, 2026-11-25: a companion at 3
    /// raids in an old save was Trusted for good and never owed Trusted's pick, as a pick was only owed on crossing a rank). How many.
    /// </summary>
    public static int OweReachedRanks(ResidentRecord record)
    {
        int owed = 0;
        if (record == null || record.Resident == null)
        {
            return owed;
        }

        int bond = UnityEngine.Mathf.FloorToInt(record.Bond + 0.0001f);
        foreach (CompanionBondRank rank in SettlementSettings.Current.BondRanks)
        {
            if (rank != null && bond >= rank.raids && ResidentTalents.OwePick(record, rank.name))
            {
                owed++;
            }
        }

        return owed;
    }

    /// <summary>A friend of theirs (Friendly or better) was brought home from the forest by the player: their bond grows.</summary>
    public static void FriendBroughtHome(SettlementState home, ResidentDefinition found)
    {
        if (home == null || found == null)
        {
            return;
        }

        foreach (ResidentRecord other in home.Residents)
        {
            if (other.Resident != found && PeopleStandings.LevelOf(home.StandingOf(found.Id, other.Resident.Id)) >= 1)
            {
                Grow(other, PeopleSettings.Current.BondFriendHome, "friend brought home");
            }
        }
    }

    /// <summary>True when <paramref name="record"/> is one of the player's own people (in their own Haven's state), not a copy of a friend's:
    /// what quests count (GameEventKind.PersonRanked and the like).</summary>
    public static bool IsOwn(ResidentRecord record) =>
        record?.Resident != null && GameSession.Home != null && GameSession.Home.Find(record.Resident) == record;
}
