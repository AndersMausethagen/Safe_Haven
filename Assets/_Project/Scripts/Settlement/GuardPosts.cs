using UnityEngine;

/// <summary>
/// Guards and their posts (Docs/ColonyModeResearch_2026-11-21.md §4.8; the Keeper's view, phase 7): those whose job defends
/// (JobDefinition › Defends As) turn out in a ward breach and, with DefenceSettings › Corruption Nights, on a corruption night; as
/// many as the defences allow (Cap: Base Posts and each standing Watch Post's tier), in the order they live in the Haven. Each stands
/// at a post the player set in the Keeper's view (SettlementState.Posts, saved, in order), else as before (the Watch Post, or a ring
/// round the portal; on a corruption night, toward the side it comes from). Plain rules where they can be (GuardPostsTests).
/// </summary>
public static class GuardPosts
{
    /// <summary>How many guards may stand with <paramref name="watchPostTiers"/> tiers of Watch Posts standing.</summary>
    public static int Cap(int watchPostTiers, DefenceSettings settings) =>
        Mathf.Min(settings.MostPosts, settings.BasePosts + Mathf.Max(0, watchPostTiers) * settings.PostsPerWatchPostTier);

    /// <summary>How many guards may stand now, from the Watch Posts standing in the loaded scene.</summary>
    public static int Cap(DefenceSettings settings)
    {
        int tiers = 0;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building != null && building.TryGetComponent(out WatchPostArcher _) && building.TryGetComponent(out Construction construction)
                && construction.IsStanding)
            {
                tiers += construction.Level;
            }
        }

        return Cap(tiers, settings);
    }

    /// <summary>Whether <paramref name="record"/>'s job keeps guard.</summary>
    public static bool Defends(ResidentRecord record) => record != null && record.Job != null && record.Job.DefendsAs != null;

    /// <summary>Where <paramref name="record"/> stands among those who keep guard (0 first, as they live in the Haven), or -1.</summary>
    public static int Rank(IHavenView home, ResidentRecord record)
    {
        if (!Defends(record))
        {
            return -1;
        }

        int rank = 0;
        foreach (ResidentRecord one in home.Residents)
        {
            if (one == record)
            {
                return rank;
            }

            rank += Defends(one) ? 1 : 0;
        }

        return -1;
    }

    /// <summary>Whether the guards are called out now: a ward breach, or (when DefenceSettings says so) a corruption night.</summary>
    public static bool CalledOut(out bool night)
    {
        night = false;
        if (WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive)
        {
            return true;
        }

        night = DefenceSettings.Current.CorruptionNights && CorruptionNights.Current != null && CorruptionNights.Current.IsActive;
        return night;
    }

    /// <summary>Why no post can be set now, or null: a friend's Haven, nobody who keeps guard, or as many posts as may stand.</summary>
    public static string WhyNoPost(IHavenView home, DefenceSettings settings, bool visiting)
    {
        if (visiting)
        {
            return "Posts are set in your own Haven.";
        }

        bool anyone = false;
        foreach (ResidentRecord record in home.Residents)
        {
            anyone |= Defends(record);
        }

        if (!anyone)
        {
            return "Nobody here keeps guard yet: a Warden's or a Hunter's work.";
        }

        int cap = Cap(settings);
        return home.Posts.Count >= cap ? $"At most {cap} posts: each tier of a Watch Post adds one." : null;
    }

    /// <summary>On a corruption night with no post set: the <paramref name="rank"/>th guard's place toward the side it comes from.</summary>
    public static Vector3 TowardTheNight(Vector3 portal, float bearing, int rank, DefenceSettings settings)
    {
        // Spread either side of the bearing: 0, +1, -1, +2...
        int side = rank == 0 ? 0 : (rank % 2 == 1 ? (rank + 1) / 2 : -(rank / 2));
        float degrees = (bearing + side * settings.NightPostSpread) * Mathf.Deg2Rad;
        return portal + new Vector3(Mathf.Sin(degrees), 0f, Mathf.Cos(degrees)) * settings.NightPostDistance;
    }
}
