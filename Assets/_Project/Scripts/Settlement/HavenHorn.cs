using UnityEngine;

/// <summary>
/// The Haven's horn (Docs/ColonyModeResearch_2026-11-21.md §4.8-4.9: Sigrid brings it): sounded from the Keeper's view when the player
/// sees trouble coming, it sends the guards to their posts (GuardPosts.CalledOut, as many as the defences allow) and everyone else to the
/// portal (ResidentLife.InDanger; those asleep indoors stay in), for DefenceSettings › Horn Seconds or until it is sounded again to stand
/// them down. It works once its keeper (DefenceSettings › Horn Keeper) lives in the Haven, only at home and in one's own Haven. Not saved:
/// a load or a raid ends it.
/// </summary>
public static class HavenHorn
{
    private static float until = -1f;
    // The Haven scene it was sounded in: leaving it (a raid) ends the call, so coming home never finds everyone still at the portal.
    private static UnityEngine.SceneManagement.Scene soundedIn;

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ForgetGame();

    /// <summary>Another game loaded (GameSession.Reset): a horn in the last one sounds no more.</summary>
    public static void ForgetGame()
    {
        until = -1f;
        soundedIn = default;
    }

    /// <summary>True while the horn's call holds: the guards out, everyone else in.</summary>
    public static bool IsSounding => until >= 0f && Time.time < until && Place.AtHome
        && UnityEngine.SceneManagement.SceneManager.GetActiveScene() == soundedIn;

    /// <summary>Why the horn can't be sounded now, or null.</summary>
    public static string WhyNot()
    {
        if (HavenVisit.IsVisiting)
        {
            return "The horn is your own Haven's.";
        }

        if (!Place.AtHome)
        {
            return "The horn is sounded at home.";
        }

        ResidentDefinition keeper = DefenceSettings.Current.HornKeeper;
        if (keeper != null && !GameSession.Home.HasResident(keeper) && !Cheats.NoRequirements)
        {
            return $"Nobody here keeps a horn yet: {keeper.DisplayName} would, once home.";
        }

        return null;
    }

    /// <summary>A few words of <see cref="WhyNot"/> for a button, or null when it can be sounded.</summary>
    public static string ShortWhyNot()
    {
        string why = WhyNot();
        ResidentDefinition keeper = DefenceSettings.Current.HornKeeper;
        return why == null ? null : HavenVisit.IsVisiting ? "your Haven only" : !Place.AtHome ? "at home" : keeper != null ? $"needs {keeper.DisplayName}" : "not yet";
    }

    /// <summary>Sounds the horn, or says why not. True when it sounded.</summary>
    public static bool Sound()
    {
        string why = WhyNot();
        if (why != null)
        {
            GameMessages.Post(why, MessageKind.Warning);
            return false;
        }

        DefenceSettings settings = DefenceSettings.Current;
        until = Time.time + settings.HornSeconds;
        soundedIn = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Vector3 at = HomePortal.Current != null ? HomePortal.Current.transform.position : Player.Current != null ? Player.Current.transform.position : Vector3.zero;
        if (settings.HornSound != null)
        {
            AudioManager.Play(settings.HornSound, at);
        }

        string who = settings.HornKeeper != null ? settings.HornKeeper.DisplayName : "Someone";
        int guards = Mathf.Min(GuardPosts.Defenders(GameSession.Home), GuardPosts.Cap(settings));
        GameMessages.Post(guards > 0
            ? $"{who} sounds the horn: {guards} guard{(guards == 1 ? "" : "s")} to the posts, everyone else to the portal."
            : $"{who} sounds the horn: everyone to the portal. Nobody here keeps guard yet.", MessageKind.Warning);
        return true;
    }

    /// <summary>Stands everyone down before the call runs out.</summary>
    public static void StandDown()
    {
        if (!IsSounding)
        {
            return;
        }

        until = -1f;
        GameMessages.Post("The horn again, twice: everyone back to their day.", MessageKind.Info);
    }
}
