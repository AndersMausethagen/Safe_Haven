/// <summary>
/// Which of the Haven's rings can be remembered now (Docs/HavenWorldStory_2026-11-26.md §5.4): home always; the near ring with
/// the Cold Stones (Territory.Unlocked); the outer ring after the Heartroot and the last at Act II's end, as HavenRegionSettings ›
/// Openings say. A region in a ring not yet open stays forgotten: its stone says "not yet". Lit stones of old saves stay lit.
/// </summary>
public static class HavenRings
{
    /// <summary>True when regions of <paramref name="ring"/> can be remembered.</summary>
    public static bool IsOpen(int ring)
    {
        if (ring <= 0)
        {
            return true;
        }

        if (ring == 1)
        {
            return Territory.Unlocked;
        }

        HavenRegionSettings settings = HavenRegionSettings.Current;
        bool any = false;
        foreach (HavenRegionSettings.RingOpening opening in settings.Openings)
        {
            if (opening == null || opening.ring != ring)
            {
                continue;
            }

            any = true;
            if (Opened(opening))
            {
                return true;
            }
        }

        // A ring nothing opens (no opening written for it) is open with the territory, as before rings.
        return !any && Territory.Unlocked;
    }

    private static bool Opened(HavenRegionSettings.RingOpening opening)
    {
        if (opening.questStarted != null && GameSession.Quests.StatusOf(opening.questStarted) != QuestStatus.NotStarted)
        {
            return true;
        }

        return opening.residentHome != null && GameSession.Home != null && GameSession.Home.HasResident(opening.residentHome);
    }

    /// <summary>True when the region's ring is open (a region with no look is in no ring: always).</summary>
    public static bool CanRemember(string region) => IsOpen(HavenRegionSettings.Current.RingOf(region));
}
