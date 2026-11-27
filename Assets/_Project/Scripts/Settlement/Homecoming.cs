using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fuller homecoming (Docs/SystemsGapReview_2026-11-19.md §2; TODO "A fuller homecoming"): what the Haven has for the player the moment
/// they are back from a raid, on one card (HomecomingCard), about two seconds after arriving: orders ready to collect at the stations,
/// what their people made while they were gone, what is now within reach (the portal's next tier or a tracked level that everything
/// home now covers) and asks they can answer. The raid pays off the moment you are home. Only after a raid's return, never in a
/// friend's Haven; when there is nothing to say, nothing shows, and the workers' line is told as before.
/// </summary>
public static class Homecoming
{
    public const int MostLines = 8;

    /// <summary>True from a raid's end until the Haven has told its homecoming.</summary>
    public static bool Pending { get; private set; }

    // The raid just ended: who went along (no gift from them), how long it lasted and how it ended.
    private static ResidentDefinition along;
    private static float awaySeconds;
    private static RaidOutcome outcome = RaidOutcome.InProgress;
    private static int homecomings;
    private static readonly HashSet<ResidentDefinition> RaidTold = new HashSet<ResidentDefinition>();
    // Who was out on a party or at an outpost this raid, as the territory's turn settled them (Territory.Turn, NoteOut). Asked once home,
    // the raid was gone and an outpost's keeper or a party's leader counted as at home, and handed over a "while you were away" gift
    // as well as their haul (bug hunt, 2026-11-25). Those on a foray are still away when home is told (ResidentsOut sees them).
    private static readonly HashSet<ResidentDefinition> WereOut = new HashSet<ResidentDefinition>();

    /// <summary>The territory's turn at a raid's end: who was out of the Haven on this raid, taken before the turn changed the lands.</summary>
    public static void NoteOut(IEnumerable<ResidentRecord> records)
    {
        WereOut.Clear();
        foreach (ResidentRecord record in records)
        {
            if (record != null && record.Resident != null)
            {
                WereOut.Add(record.Resident);
            }
        }
    }

    /// <summary>Raids come home from since the game was loaded (a fresh pick of words each time).</summary>
    public static int Homecomings => homecomings;

    /// <summary>True the first time <paramref name="resident"/> speaks of the raid just come home from (once each, per raid).</summary>
    public static bool FirstWordOnTheRaid(ResidentDefinition resident) => resident != null && RaidTold.Add(resident);

    /// <summary>How the raid the player last came home from ended; In Progress when none has since the game was loaded.</summary>
    public static RaidOutcome LastOutcome => outcome;

    /// <summary>Who went along on that raid, or null.</summary>
    public static ResidentDefinition LastAlong => along;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ForgetGame();

    // After GameEvents clears its listeners (SubsystemRegistration), listen again.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Listen()
    {
        GameEvents.Raised -= OnRaised;
        GameEvents.Raised += OnRaised;
    }

    /// <summary>Another game loaded (GameSession.Reset): a raid's end in the last one is no homecoming of this one.</summary>
    public static void ForgetGame()
    {
        Pending = false;
        along = null;
        awaySeconds = 0f;
        outcome = RaidOutcome.InProgress;
        homecomings = 0;
        RaidTold.Clear();
        WereOut.Clear();
    }

    private static void OnRaised(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.RaidFinished)
        {
            Pending = true;
            awaySeconds = gameEvent.Seconds;
            outcome = gameEvent.Outcome;
            homecomings++;
            RaidTold.Clear();
            RaidReport report = GameSession.PendingReport;
            along = report != null ? report.Companion : null;
        }
    }

    /// <summary>Called as the Haven starts (HavenProgress): the card, when coming home from a raid and there is something to say.
    /// False when nothing was shown (the caller tells the workers' line itself).</summary>
    public static bool Tell()
    {
        // This homecoming's, read once: never a later raid's.
        var wereOut = new List<ResidentDefinition>(WereOut);
        WereOut.Clear();
        if (!Pending)
        {
            return false;
        }

        Pending = false;
        if (HavenVisit.IsVisiting || Player.Current == null)
        {
            return false;
        }

        List<string> lines = Compose(Player.Current, GameSession.Home);
        // What happened among the people while away (PeopleNews: ranks, inspired mornings, marks, friends made), two lines at most.
        int at = System.Math.Min(lines.Count, 1);
        foreach (string news in PeopleNews.TakeAway(2))
        {
            lines.Insert(System.Math.Min(lines.Count, at++), news);
        }

        if (lines.Count > MostLines)
        {
            lines.RemoveRange(MostLines, lines.Count - MostLines);
        }

        // One good thing that happened while they were away (HomecomingSettings), heading the card.
        HomecomingSettings gifts = HomecomingSettings.Current;
        string gift = gifts != null && awaySeconds >= gifts.MinAwaySeconds
            ? AwayGifts.Draw(gifts, GameSession.Home, GameSession.Stash, UnityEngine.Random.value, UnityEngine.Random.value, along,
                RaidPacingSettings.Current.TimeAwayShare(awaySeconds), wereOut)
            : null;
        if (gift != null)
        {
            lines.Insert(0, gift);
            if (lines.Count > MostLines)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        JournalRecord journal = GameSession.Progress.Journal;
        // What is going on out in the land this span (HavenHappenings), told once: a reason to walk out before the next raid.
        string foretold = HavenHappenings.Foretell(journal);
        if (foretold != null)
        {
            lines.Insert(System.Math.Min(1, lines.Count), foretold);
            if (lines.Count > MostLines)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        // Someone met out there came by the gate (a raid choice's mark), heading the card above all.
        string visit = AwayGifts.Visit(gifts, GameSession.Stash, journal.HasNoticed, mark => journal.NoteNoticed(mark));
        if (visit != null)
        {
            lines.Insert(0, visit);
            if (lines.Count > MostLines)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        if (lines.Count == 0)
        {
            return false;
        }

        GameSession.Home.MadeByWorkers.Clear();
        HomecomingCard.Show(lines);
        return true;
    }

    /// <summary>The card's lines, in order: ready to collect, made by your people, now within reach, asks you can answer.</summary>
    public static List<string> Compose(Player player, SettlementState home)
    {
        var lines = new List<string>();
        if (home == null)
        {
            return lines;
        }

        foreach (StationQueue queue in home.Workshops.Queues)
        {
            if (queue.Output is IDisplayName made && queue.OutputAmount > 0)
            {
                lines.Add($"Ready at the {StationName(queue.StationId)}: {queue.OutputAmount} {made.DisplayName}.");
            }
        }

        if (home.MadeByWorkers.Count > 0)
        {
            var made = new List<string>();
            foreach (KeyValuePair<IStackDefinition, int> entry in home.MadeByWorkers)
            {
                made.Add($"{entry.Value} {entry.Key.DisplayName}");
            }

            lines.Add($"Your people made {string.Join(", ", made)}.");
        }

        // Within reach: a target everything home now covers (WantedAtHome's targets with nothing wanted).
        List<WantedAtHome.Target> targets = WantedAtHome.Targets(home, GameSession.Progress, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
        MaterialPool pool = MaterialPool.For(player.gameObject);
        foreach (WantedAtHome.Target target in targets)
        {
            if (WantedAtHome.Compute(new[] { target }, pool).Count == 0)
            {
                lines.Add(GameInput.WithRebinds(target.Name.StartsWith("the portal")
                    ? $"Within reach: {target.Name}. Everything it takes is home: strengthen it at the portal."
                    // A plan is paid from the chests by itself (BuildPlans); with it in the bag, the player hammers it.
                    : target.Name.StartsWith("the planned")
                    ? $"Within reach: {target.Name}. Everything it takes is home: in the chests the builders start on it, or hammer it yourself."
                    : $"Within reach: {target.Name}. Everything it takes is home ({{key:Build}})."));
            }
        }

        // A stronger weapon of the kind in hand that the player knows how to make (the user's playtest, 2026-11-25: still on the Copper
        // Sword at tier II, and the abilities' blows grow with the weapon now).
        if (player.TryGetComponent(out PlayerCombat combat) && player.TryGetComponent(out PlayerCrafting crafting))
        {
            RecipeDefinition better = BetterWeapon(combat.Weapon, crafting.KnownRecipes, item => pool.Have(item) > 0);
            if (better != null)
            {
                WeaponDefinition made = better.Output.Weapon;
                string where = better.Station == CraftingStationType.None ? "by hand" : $"at the {better.Station.DisplayName()}";
                lines.Add($"Stronger blade: {better.Output.DisplayName} ({made.Damage:0} vs {combat.Weapon.Damage:0}), {where}. Abilities strike by it too.");
            }
        }

        ItemInventory bag = player.GetComponent<ItemInventory>();
        foreach (HavenRequestDefinition request in HavenRequestBoard.Standing())
        {
            if (request.Wanted != null && HavenRequestBoard.AtHome(request, bag) >= request.Count)
            {
                string who = request.NameIn(GameSession.Here);
                lines.Add($"{who} asked for {request.Count} {request.Wanted.DisplayName}: you have them.");
            }
        }

        if (lines.Count > MostLines)
        {
            lines.RemoveRange(MostLines, lines.Count - MostLines);
        }

        return lines;
    }

    /// <summary>Share more damage a known weapon must carry to be worth a line on the card.</summary>
    public const float BetterBy = 1.2f;

    /// <summary>
    /// The next step up from <paramref name="held"/> among <paramref name="known"/>: a melee weapon of the same kind (stance and signature:
    /// a sword for a sword, not a staff or an axe of the same grip) carrying at least <see cref="BetterBy"/> times its damage, the least
    /// such (the next tier, not the last), and not one already <paramref name="owned"/>. Null when none, or nothing melee is held.
    /// </summary>
    public static RecipeDefinition BetterWeapon(WeaponDefinition held, IEnumerable<RecipeDefinition> known, System.Func<ItemDefinition, bool> owned = null)
    {
        if (held == null || held.Kind != WeaponKind.Melee || known == null)
        {
            return null;
        }

        RecipeDefinition best = null;
        foreach (RecipeDefinition recipe in known)
        {
            WeaponDefinition weapon = recipe != null && recipe.Output != null ? recipe.Output.Weapon : null;
            if (weapon == null || weapon == held || weapon.Kind != WeaponKind.Melee || weapon.Stance != held.Stance
                || weapon.Signature != held.Signature || weapon.Damage < held.Damage * BetterBy
                || (owned != null && owned(recipe.Output)))
            {
                continue;
            }

            if (best == null || weapon.Damage < best.Output.Weapon.Damage)
            {
                best = recipe;
            }
        }

        return best;
    }

    private static string StationName(string stationId)
    {
        foreach (CraftingStation station in CraftingStation.Active)
        {
            if (station != null && station.Id == stationId && station.TryGetComponent(out BuildingObject building) && building.Definition != null)
            {
                return building.Definition.DisplayName;
            }
        }

        return "workshop";
    }
}
