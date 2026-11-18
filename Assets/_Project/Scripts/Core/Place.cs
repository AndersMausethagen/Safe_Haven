/// <summary>
/// Where the player is, said once: every "at home", "on a raid", "in a friend's Haven" in the game reads it here instead of putting it
/// together from its parts (Docs/BugPatterns.md, fourth round). Built by hand at each call site, "at home" had four meanings: one
/// forgot the raid's own load (a raid set out, its scene not yet up), one forgot a friend's Haven (a guest's cargo went into a stash
/// that wasn't theirs and was lost), one paid rewards held for home in the host's Haven. ConventionTests fails on a line that combines
/// two of the parts outside this class.
/// </summary>
public static class Place
{
    /// <summary>A raid is under way: from the portal's step (the run set out, its scene loading) through its end, until home.</summary>
    public static bool OnRaid => GameSession.ActiveRaid != null || RaidDirector.Current != null;

    /// <summary>The raid's scene is up and its director running it (not its load, not the way home).</summary>
    public static bool InRaidScene => GameSession.ActiveRaid != null && RaidDirector.Current != null;

    /// <summary>The raid is on and still open: nothing has decided its end.</summary>
    public static bool RaidRunning => RaidDirector.Current != null && !RaidDirector.Current.Session.IsOver;

    /// <summary>The raid's end is decided (extracted, lost, out of time): what is taken home is settled.</summary>
    public static bool RaidDecided => RaidDirector.Current != null && RaidDirector.Current.Session.IsOver;

    /// <summary>In one's own Haven (or the prologue's camp): no raid, no visit.</summary>
    public static bool AtHome => !OnRaid && !HavenVisit.IsVisiting;

    /// <summary>In a friend's Haven (co-op, a guest), not on a raid with them.</summary>
    public static bool InFriendsHaven => HavenVisit.IsVisiting && !OnRaid;

    /// <summary>A guest on a raid with the host.</summary>
    public static bool FollowingAHost => HavenVisit.IsVisiting && OnRaid;

    /// <summary>In a Haven, one's own or a friend's: no raid, and a portal stands here.</summary>
    public static bool InAHaven => !OnRaid && HomePortal.Current != null;

    /// <summary>Where the stash may be used: one's own Haven, or a friend's with their leave.</summary>
    public static bool StashUsable => !OnRaid && (!HavenVisit.IsVisiting || HavenVisit.MayUseStash);
}
