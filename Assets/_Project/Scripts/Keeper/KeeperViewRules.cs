/// <summary>What the Keeper's view asks of the moment, gathered by KeeperView so the rules stay plain and testable.</summary>
public readonly struct KeeperSituation
{
    /// <summary>In the Haven (a HomePortal stands in the scene) with a living player.</summary>
    public readonly bool AtHome;
    /// <summary>The story has opened the view (KeeperViewSettings › Unlocked By has started), or a cheat says so.</summary>
    public readonly bool Unlocked;
    /// <summary>A ward breach's fight is on (not its warning or preparation).</summary>
    public readonly bool BreachFight;
    /// <summary>A painted or in-game cinematic is playing.</summary>
    public readonly bool Cinematic;
    /// <summary>A conversation is open.</summary>
    public readonly bool Dialogue;
    /// <summary>Swimming or on horseback.</summary>
    public readonly bool Afloat;
    /// <summary>Something hunts this player (not a friend: co-op).</summary>
    public readonly bool Hunted;
    /// <summary>This player was hurt a moment ago (KeeperViewSettings › Hurt Grace Seconds).</summary>
    public readonly bool HurtLately;
    /// <summary>Someone else holds this player's controls (a cinematic, a scene change, being downed).</summary>
    public readonly bool HeldByOthers;

    public KeeperSituation(bool atHome, bool unlocked, bool breachFight, bool cinematic, bool dialogue, bool afloat, bool hunted,
        bool hurtLately, bool heldByOthers)
    {
        AtHome = atHome;
        Unlocked = unlocked;
        BreachFight = breachFight;
        Cinematic = cinematic;
        Dialogue = dialogue;
        Afloat = afloat;
        Hunted = hunted;
        HurtLately = hurtLately;
        HeldByOthers = heldByOthers;
    }
}

/// <summary>
/// When the Keeper's view may open and when it must give the player back their feet (Docs/ColonyModeResearch_2026-11-21.md
/// §4.2): only at home and once the story has opened it; never in a breach's fight, a cinematic, a conversation, swimming or
/// riding; and it closes the moment the player is hunted or hurt, a breach's fight starts or a cinematic begins. Plain C#
/// (KeeperViewTests).
/// </summary>
public static class KeeperViewRules
{
    /// <summary>Why the view can't open now, said to the player; null when it can.</summary>
    public static string WhyNot(in KeeperSituation now)
    {
        if (!now.AtHome)
        {
            return "The Keeper's view is only at home.";
        }

        if (!now.Unlocked)
        {
            return "The Keeper's view opens later in the story.";
        }

        if (now.BreachFight)
        {
            return "Not while the ward is breached.";
        }

        if (now.Hunted || now.HurtLately)
        {
            return "Not while something is after you.";
        }

        if (now.Afloat)
        {
            return "Get to your feet on land first.";
        }

        // A cinematic, a conversation or someone else's hold on the controls: nothing to say, the moment is elsewhere.
        return now.Cinematic || now.Dialogue || now.HeldByOthers ? string.Empty : null;
    }

    /// <summary>Why the open view gives the player back their feet now, said to them (empty: closes without a word); null to stay.</summary>
    public static string WhyLeave(in KeeperSituation now)
    {
        if (!now.AtHome)
        {
            return string.Empty;
        }

        if (now.BreachFight)
        {
            return "The ward is breached: to your feet!";
        }

        if (now.Hunted)
        {
            return "Something is after you!";
        }

        if (now.HurtLately)
        {
            return "You were hurt!";
        }

        return now.Cinematic || now.Dialogue ? string.Empty : null;
    }
}
