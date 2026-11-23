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

        // Visiting a friend, the buildings and people about are theirs: one's own people's day waits for home (near-work friendships,
        // inspirations read the Haven in the scene).
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        PeopleStandings.Dawn(home);
        foreach (ResidentRecord inspired in Inspirations.Dawn(home, () => Random.value))
        {
            string news = Inspirations.News(inspired);
            if (!string.IsNullOrEmpty(news))
            {
                GameMessages.Post(news, MessageKind.Gain);
            }

            if (inspired.Inspiration == Inspiration.Story)
            {
                SmallScenes.CheerAll(home.Residents, PeopleSettings.Current.StoryCheerSeconds);
            }
        }

        ResidentSpirits.ForgetGame();
    }
}
