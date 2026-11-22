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

    /// <summary>Whether <paramref name="record"/>'s job keeps guard, and they are here to keep it (not away on a foray): the ranks, the
    /// count and the horn's word all ask this.</summary>
    public static bool Defends(ResidentRecord record) => record != null && !record.IsAway && record.Job != null && record.Job.DefendsAs != null;

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

    /// <summary>A ward breach is being fought now.</summary>
    public static bool InBreach => WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive;

    /// <summary>
    /// How <paramref name="record"/> fights when called out, or null when they shelter: a guard as their job has it; in a ward breach
    /// (DefenceSettings › Everyone Fights Breaches) anyone else at home too, as they fight on a raid, else as the Breach Fighter.
    /// </summary>
    public static CompanionDefinition FightsAs(ResidentRecord record, bool breach, DefenceSettings settings)
    {
        if (record == null || record.IsAway)
        {
            return null;
        }

        if (Defends(record))
        {
            return record.Job.DefendsAs;
        }

        if (!breach || settings == null || !settings.EveryoneFightsBreaches)
        {
            return null;
        }

        return record.Resident != null && record.Resident.RaidCompanion != null ? record.Resident.RaidCompanion : settings.BreachFighter;
    }

    /// <summary>How many of the Haven's people fight in a ward breach.</summary>
    public static int BreachFighters(IHavenView home, DefenceSettings settings)
    {
        int count = 0;
        foreach (ResidentRecord record in home.Residents)
        {
            count += FightsAs(record, true, settings) != null ? 1 : 0;
        }

        return count;
    }

    /// <summary>Whether the guards are called out now: a ward breach, (when DefenceSettings says so) a corruption night, or the horn.</summary>
    public static bool CalledOut(out bool night)
    {
        night = false;
        if (WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive)
        {
            return true;
        }

        night = DefenceSettings.Current.CorruptionNights && CorruptionNights.Current != null && CorruptionNights.Current.IsActive;
        return night || HavenHorn.IsSounding;
    }

    /// <summary>How many of the Haven's people keep guard.</summary>
    public static int Defenders(IHavenView home)
    {
        int count = 0;
        foreach (ResidentRecord record in home.Residents)
        {
            count += Defends(record) ? 1 : 0;
        }

        return count;
    }

    /// <summary>The name of the guard who takes post <paramref name="index"/> (0 first), or null.</summary>
    public static string GuardFor(IHavenView home, int index)
    {
        foreach (ResidentRecord record in home.Residents)
        {
            if (Defends(record) && index-- == 0)
            {
                return record.Resident != null ? record.Resident.DisplayName : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Why no post can be set now, or null: a friend's Haven, nobody who keeps guard, or a post for every guard who may stand (as many
    /// as keep guard, and no more than the defences hold).
    /// </summary>
    public static string WhyNoPost(IHavenView home, DefenceSettings settings, bool visiting)
    {
        if (visiting)
        {
            return "Posts are set in your own Haven.";
        }

        int guards = Defenders(home);
        if (guards == 0)
        {
            return "Nobody here keeps guard yet: a Warden's or a Hunter's work.";
        }

        int cap = Cap(settings);
        if (home.Posts.Count < Mathf.Min(cap, guards))
        {
            return null;
        }

        return guards < cap ? $"A post for each guard: {guards} keep{(guards == 1 ? "s" : "")} guard."
            : $"At most {cap} posts: each tier of a Watch Post adds one.";
    }

    /// <summary>
    /// The posts guards take: all of them in a breach; on a corruption night only those on its side (within Night Post Side of its
    /// bearing, seen from the portal), so posts set for a breach's lanes don't send guards away from the band.
    /// </summary>
    public static System.Collections.Generic.List<Vector3> PostsFor(System.Collections.Generic.IReadOnlyList<Vector3> posts, Vector3 portal,
        bool night, float bearing, DefenceSettings settings)
    {
        var taken = new System.Collections.Generic.List<Vector3>();
        foreach (Vector3 post in posts)
        {
            Vector3 way = post - portal;
            way.y = 0f;
            float degrees = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            if (!night || way.sqrMagnitude < 1f || Mathf.Abs(Mathf.DeltaAngle(degrees, bearing)) <= settings.NightPostSide)
            {
                taken.Add(post);
            }
        }

        return taken;
    }

    /// <summary>The ground a guard can stand on nearest <paramref name="at"/> (out of deep water), or the point itself when none is near.</summary>
    public static Vector3 Walkable(Vector3 at) =>
        UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, DeepWater.MaskFor(UnityEngine.AI.NavMesh.AllAreas, false))
            ? hit.position : at;

    /// <summary>On a corruption night with no post set: the <paramref name="rank"/>th guard's place toward the side it comes from.</summary>
    public static Vector3 TowardTheNight(Vector3 portal, float bearing, int rank, DefenceSettings settings)
    {
        // Spread either side of the bearing: 0, +1, -1, +2...
        int side = rank == 0 ? 0 : (rank % 2 == 1 ? (rank + 1) / 2 : -(rank / 2));
        float degrees = (bearing + side * settings.NightPostSpread) * Mathf.Deg2Rad;
        return portal + new Vector3(Mathf.Sin(degrees), 0f, Mathf.Cos(degrees)) * settings.NightPostDistance;
    }
}
