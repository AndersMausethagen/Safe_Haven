using UnityEngine;

/// <summary>
/// A dawn for the people of one's own Haven (HavenDawn; Docs/PeopleResearch_2026-11-23.md §3.4-3.5): feelings that ran their course are
/// forgotten, those who worked near each other grow a little closer, and someone in Bright spirits may wake inspired, told by name (a
/// story at the fire cheers everyone at home).
/// </summary>
public static class PeopleDay
{
    public static void Dawn(SettlementState home)
    {
        if (home == null)
        {
            return;
        }

        int today = home.DaysPassed;
        foreach (ResidentRecord record in home.Residents)
        {
            record.ForgetFeelingsBefore(today);
        }

        // Away from the Haven (a raid, a friend's), its buildings aren't here to read (near-work friendships, a content home for spirits):
        // the morning waits for home, once however many dawns passed (Tick).
        if (!Place.InOwnHaven)
        {
            waiting = true;
            return;
        }

        Morning(home);
    }

    // A dawn passed away, its morning still to come at home.
    private static bool waiting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => waiting = false;

    /// <summary>Forgets a morning still waiting (a new game or a load: GameSession.Reset).</summary>
    public static void ForgetGame() => waiting = false;

    /// <summary>Home again with a morning waiting: it comes once the Haven has settled (SettlementSystem.Update).</summary>
    public static void Tick(SettlementState home, float secondsHere)
    {
        if (waiting && secondsHere > 3f && Place.InOwnHaven)
        {
            waiting = false;
            Morning(home);
        }
    }

    /// <summary>The people's morning now (a cheat for playtests: PeopleCheats), as a dawn at home brings it.</summary>
    public static void MorningNow(SettlementState home) => Morning(home);

    private static void Morning(SettlementState home)
    {
        PeopleStandings.Dawn(home);
        Stablehands.Morning(home);
        int bright = 0, low = 0, steady = 0;
        foreach (ResidentRecord record in home.Residents)
        {
            Spirits spirits = ResidentSpirits.Of(record, home);
            bright += spirits == Spirits.Bright ? 1 : 0;
            low += spirits == Spirits.Low ? 1 : 0;
            steady += spirits == Spirits.Steady ? 1 : 0;
        }

        if (home.Residents.Count > 0)
        {
            PlaytestLogger.Note(PlaytestLog.Spirits, "haven", bright, $"low {low} steady {steady}");
        }

        foreach (ResidentRecord inspired in Inspirations.Dawn(home, () => Random.value))
        {
            PlaytestLogger.Note(PlaytestLog.PersonInspired, inspired.Resident.Id, 0, inspired.Inspiration.ToString());
            string news = Inspirations.News(inspired);
            if (!string.IsNullOrEmpty(news))
            {
                PeopleNews.Tell(news, MessageKind.Gain);
            }

            if (inspired.Inspiration == Inspiration.Story)
            {
                SmallScenes.CheerAll(home.Residents, PeopleSettings.Current.StoryCheerSeconds);
            }
        }

        ResidentSpirits.ForgetGame();
    }
}
