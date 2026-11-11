using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a guest's help on a raid followed with the host does for the host's Haven (co-op).</summary>
public enum HavenHelpKind : byte
{
    WeakenBreach = 1,
    FoundAgain = 2,
}

/// <summary>
/// The host's Haven helped from a raid followed with the host (co-op). A guest's settlement only mirrors the host's, so its
/// raids show the host's coming ward breach and the host's lost people. What the guest does about them is sent to the host's
/// game (<see cref="ForHost"/>, CoopLands) and applied there (<see cref="FromFriend"/>): a breach's source struck (once a run,
/// however many players strike it) and one of the host's lost led home. At home, alone or as the host, it applies at once.
/// Like Territory's: before 2026-11-11 a guest's strike or rescue changed only their copy, and the host's next word undid it.
/// </summary>
public static class HavenHelp
{
    // Runs whose breach source has been struck: every player on the run has the rumour, but it is one blow.
    private static readonly HashSet<int> struckOnRuns = new HashSet<int>();

    /// <summary>Set by co-op while this game may be a guest: sends the help (what, who, share, most, run) to the host's game.</summary>
    public static Action<HavenHelpKind, string, float, float, int> ForHost { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        struckOnRuns.Clear();
        ForHost = null;
    }

    /// <summary>A blow against what is behind the next ward breach (WeakenBreachOutcome).</summary>
    public static void WeakenBreach(float share, float most)
    {
        int run = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed : 0;
        if (HavenVisit.IsVisiting)
        {
            ForHost?.Invoke(HavenHelpKind.WeakenBreach, string.Empty, share, most, run);
            return;
        }

        Strike(share, most, run);
    }

    /// <summary>One of the Haven's lost people led out of a raid (RaidCompanions). True when they come home (to the host's, for a guest).</summary>
    public static bool FoundAgain(ResidentDefinition resident)
    {
        if (resident == null)
        {
            return false;
        }

        if (HavenVisit.IsVisiting)
        {
            ForHost?.Invoke(HavenHelpKind.FoundAgain, resident.Id, 0f, 0f, 0);
            return ForHost != null;
        }

        return GameSession.Settlement.FindAgain(resident);
    }

    /// <summary>The host's game applies a friend's help (CoopLands).</summary>
    public static void FromFriend(HavenHelpKind kind, string id, float share, float most, int run, string friend, IContentLookup content)
    {
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        friend = string.IsNullOrEmpty(friend) ? "A friend" : friend;
        switch (kind)
        {
            case HavenHelpKind.WeakenBreach:
                if (Strike(share, most, run))
                {
                    GameMessages.Post($"{friend} struck at what drives the next breach. It will come weaker.", MessageKind.Gain);
                }

                break;
            case HavenHelpKind.FoundAgain:
                ResidentDefinition resident = content != null ? content.FindResident(id) : null;
                if (resident != null && GameSession.Settlement.FindAgain(resident))
                {
                    GameMessages.Post($"{friend} brought {resident.DisplayName} home.", MessageKind.Gain);
                }

                break;
        }
    }

    // Once a run (run 0: not on a raid, always counts).
    private static bool Strike(float share, float most, int run)
    {
        if (run != 0 && !struckOnRuns.Add(run))
        {
            return false;
        }

        GameSession.Settlement.WeakenBreach(share, most);
        return true;
    }
}
