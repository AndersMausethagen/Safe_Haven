using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts a raid from Safe Haven: pays for the chosen Portal Sparks (extra time, GDD §7.3) exactly once, uses up the
/// chosen raid modifier items, works out the raid's rules, records what the player takes along (the loadout: its tools,
/// weapons and backpacks are the deployed equipment kept if the raid fails) and loads the raid scene. Everything else the
/// player carries is at risk until they extract.
/// </summary>
public static class RaidLauncher
{
    // The launch's own hold on the player's controls while the raid's scene loads (PlayerControl).
    private static readonly object Launching = new object();

    /// <summary>Why the raid can't start right now, in words for the player, or null when it can.</summary>
    public static string CheckLaunch(RaidDefinition raid)
    {
        if (raid == null)
        {
            return "This portal doesn't lead anywhere.";
        }

        // A guest goes on raids by following the host (TryFollow), not by opening the portal themselves.
        if (HavenVisit.IsVisiting)
        {
            return HavenVisit.RaidRefusal;
        }

        if (raid.IsLocked && !Cheats.NoRequirements)
        {
            return raid.LockedText;
        }

        if (GameSession.ActiveRaid != null)
        {
            return "A raid is already under way.";
        }

        // A scene change under way would refuse the raid's load after the Sparks were paid and the input switched off.
        if (SceneTransition.IsBusy)
        {
            return "The way isn't open yet: wait a moment.";
        }

        // Not out of a fight for the Haven: leaving unloaded the attackers with nothing settled (no strain, no recovery, the
        // breach still pending), and a corrupted night skipped its dawn and the blight it spreads.
        if (WardBreachDirector.Current != null && WardBreachDirector.Current.IsUnderWay)
        {
            return "The ward is breached: defend the Haven first.";
        }

        if (CorruptionNights.Current != null && CorruptionNights.Current.IsActive)
        {
            return "The corrupted night is on the Haven: hold until dawn.";
        }

        if (string.IsNullOrWhiteSpace(raid.SceneName) || !Application.CanStreamedLevelBeLoaded(raid.SceneName))
        {
            return $"{raid.DisplayName} can't be reached: its scene isn't in the build.";
        }

        // A boss encounter's rematch waits a day after a kill (the user: "so the player can't spam it").
        string rematch = CheckRematch(raid, DateTime.UtcNow);
        if (rematch != null && !Cheats.NoRequirements)
        {
            return rematch;
        }

        // The raid sends the player back here, so this scene has to be loadable too.
        if (!Application.CanStreamedLevelBeLoaded(SceneManager.GetActiveScene().name))
        {
            return "This place isn't in the build, so there would be no way back from the raid.";
        }

        return null;
    }

    /// <summary>Why a boss encounter can't be fought again yet (its boss fell less than Rematch Hours ago), or null.</summary>
    public static string CheckRematch(RaidDefinition raid, DateTime utcNow)
    {
        if (raid == null || !raid.IsEncounter)
        {
            return null;
        }

        TimeSpan wait = raid.Encounter.RematchWait(GameSession.Progress.LastKill(raid.Id), utcNow);
        return wait > TimeSpan.Zero
            ? $"{raid.Encounter.BossName} is gone to ground. It can be fought again in {BossEncounterDefinition.Describe(wait)}."
            : null;
    }

    /// <summary>Why the player's gear keeps them out of a tier, or null when it doesn't.</summary>
    public static string CheckGear(RaidDifficulty difficulty, GameObject player)
    {
        if (difficulty == null || Cheats.NoRequirements)
        {
            return null;
        }

        int score = GearScore.Of(player);
        return score >= difficulty.minimumGearScore
            ? null
            : $"Tier {Tiers.ToRoman(difficulty.tier)} needs a gear score of at least {difficulty.minimumGearScore}. Yours is {score}.";
    }

    /// <summary>Why the portal can't yet hold the way open to a tier, or null when it can.</summary>
    public static string CheckWard(RaidDifficulty difficulty)
    {
        return difficulty == null || Cheats.NoRequirements || GameSession.Here.WardLevel >= difficulty.wardLevelNeeded
            ? null
            : $"The ward isn't strong enough to hold the way open to tier {Tiers.ToRoman(difficulty.tier)}. Strengthen it at the portal.";
    }

    /// <summary>Share off the extraction time, from what has been done to the portal.</summary>
    public static float ExtractionCut => SettlementSettings.Current.ExtractionCutAt(GameSession.Here.PortalLevel);

    /// <summary>Why the player can't spend this many Portal Sparks, or null when they can.</summary>
    public static string CheckSparks(GameObject player, int sparks)
    {
        if (sparks <= 0)
        {
            return null;
        }

        SettlementSettings settings = SettlementSettings.Current;
        if (settings.PortalSpark == null || sparks > settings.MaxSparksPerRaid)
        {
            return $"A raid can use at most {settings.MaxSparksPerRaid} Portal Sparks.";
        }

        return MaterialPool.For(player).CanAfford(SparkCost(sparks), null, out string missing) ? null : missing;
    }

    /// <summary>The seconds a raid gets on top of its rules: Portal Sparks, and what residents' jobs add.</summary>
    public static float ExtraSeconds(int sparks) =>
        sparks * SettlementSettings.Current.SecondsPerSpark + GameSession.Here.JobBonus(job => job.RaidSeconds) + HavenReadiness.RaidSeconds;

    /// <summary>What a number of Portal Sparks costs, as resources.</summary>
    public static List<ResourceAmount> SparkCost(int sparks)
    {
        var cost = new List<ResourceAmount>();
        ResourceDefinition spark = SettlementSettings.Current.PortalSpark;
        if (spark != null && sparks > 0)
        {
            cost.Add(new ResourceAmount(spark, sparks));
        }

        return cost;
    }

    /// <summary>
    /// Sends the player into the raid with the chosen modifier items (each kind once, one of it used up) and Portal Sparks.
    /// Returns false, with a reason for the player, when it can't.
    /// </summary>
    /// <summary>True for someone who lives in the Haven and knows how to fight on a raid.</summary>
    public static bool CanGoAlong(ResidentDefinition person) =>
        person != null && person.RaidCompanion != null && GameSession.Here.Find(person) != null;

    /// <summary>Raised when the player goes through the portal, with what friends need to follow (co-op).</summary>
    public static event Action<RaidInvite> Launched;

    /// <summary>The raid, mood and shape of the last run started this session, so the next briefing can offer a different one.</summary>
    public static (string raid, string mood, RaidLayoutShape shape) LastRun { get; private set; }

    public static bool TryLaunch(RaidDefinition raid, GameObject player, IEnumerable<ItemDefinition> modifierItems, out string problem,
        int sparks = 0, int? seed = null, RaidDifficulty difficulty = null, ResidentDefinition companion = null, bool large = false)
    {
        problem = CheckLaunch(raid);
        difficulty ??= raid != null ? raid.LowestDifficulty : null;
        if (raid != null && raid.IsEncounter)
        {
            // A boss encounter: the first fight at its own tier, rematches at the chosen one; no Portal Sparks, large maps or raid
            // modifiers (it has no clock to extend and no map to widen).
            difficulty = raid.Encounter.DifficultyFor(raid, raid.Encounter.IsBeatenIn(GameSession.Here), difficulty);
            sparks = 0;
            large = false;
            modifierItems = null;
        }

        problem ??= CheckWard(difficulty);
        problem ??= CheckGear(difficulty, player);
        ItemInventory inventory = player != null ? player.GetComponent<ItemInventory>() : null;
        if (problem == null && inventory == null)
        {
            problem = "There is nobody to send into the raid.";
        }

        problem ??= CheckSparks(player, sparks);
        if (problem != null)
        {
            return false;
        }

        var chosen = new List<ItemDefinition>();
        if (modifierItems != null)
        {
            foreach (ItemDefinition item in modifierItems)
            {
                if (item == null || !item.IsRaidModifier || chosen.Contains(item))
                {
                    continue;
                }

                if (!inventory.Container.Contains(item, 1))
                {
                    problem = $"{item.DisplayName} is no longer in the bag.";
                    return false;
                }

                chosen.Add(item);
            }
        }

        // Paid once, here, before anything else changes (GDD §23.4: an extension costs exactly once).
        if (sparks > 0 && !MaterialPool.For(player).TrySpend(SparkCost(sparks)))
        {
            problem = "Not enough Portal Sparks.";
            return false;
        }

        HavenReadiness.GiveRest(player);

        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in chosen)
        {
            inventory.Container.Remove(item, 1);
            modifiers.Add(item.RaidModifier);
        }

        // The seed is usually rolled when the briefing opens, so the hour it shows is the hour the raid gets.
        int runSeed = seed ?? RaidSession.NewSeed();
        bool isLarge = large && raid.CanBeLarge;
        float extra = ExtraSeconds(sparks) + (isLarge ? raid.LargeRaid.extraSeconds : 0f);
        float cut = ExtractionCut;
        int foretold = HavenReadiness.ForetoldEvents;
        // The quests under way now decide the rumours that wait on one, here and, through the invite, in every friend's game.
        RaidSession session = Go(raid, modifiers, player, runSeed, difficulty, sparks, isLarge, extra, cut, foretold, CanGoAlong(companion) ? companion : null, 0f,
            RaidRumours.QuestsUnderWay(GameSession.Quests));
        if (session == null)
        {
            return false;
        }

        Launched?.Invoke(RaidInvite.Of(session, extra, cut, foretold));
        return true;
    }

    /// <summary>
    /// Follows a friend (co-op) into the raid they went through the portal to: the same raid, seed, tier and modifiers,
    /// so the same map, with this player's own gear and loot. The friend paid for the way; following costs nothing. The
    /// clock starts where the friend's stands now (<paramref name="elapsed"/>). Returns false, with a reason, when it can't.
    /// </summary>
    public static bool TryFollow(RaidInvite invite, RaidDefinition raid, GameObject player, ContentDatabase content, float elapsed, out string problem)
    {
        problem = invite == null || raid == null ? "That way has closed." : null;
        if (problem == null && GameSession.ActiveRaid != null)
        {
            problem = "A raid is already under way.";
        }

        if (problem == null && (string.IsNullOrWhiteSpace(raid.SceneName) || !Application.CanStreamedLevelBeLoaded(raid.SceneName)))
        {
            problem = $"{raid.DisplayName} can't be reached: its scene isn't in the build.";
        }

        RaidDifficulty difficulty = raid != null ? invite.DifficultyIn(raid) : null;
        problem ??= CheckGear(difficulty, player);
        if (problem == null && (player == null || !player.TryGetComponent(out ItemInventory _)))
        {
            problem = "There is nobody to send into the raid.";
        }

        if (problem != null)
        {
            return false;
        }

        HavenReadiness.GiveRest(player);
        // Their own rematch still waiting: they may help, but the boss drops nothing for them (BossLootWithheld).
        bool withheld = CheckRematch(raid, DateTime.UtcNow) != null && !Cheats.NoRequirements;
        RaidSession followed = Go(raid, invite.ModifiersFrom(content), player, invite.seed, difficulty, invite.sparks, invite.large && raid.CanBeLarge,
            invite.extraSeconds, invite.extractionCut, invite.foretoldEvents, null, elapsed, invite.questsUnderWay);
        if (followed != null)
        {
            followed.BossLootWithheld = withheld;
        }

        return followed != null;
    }

    /// <summary>The run a friend's invite describes, as <see cref="TryFollow"/> builds it (without going anywhere).</summary>
    public static RaidSession FromInvite(RaidInvite invite, RaidDefinition raid, ContentDatabase content, PlayerSnapshot loadout, string returnScene, float elapsed) =>
        Build(raid, invite.ModifiersFrom(content), loadout, returnScene, invite.seed, invite.DifficultyIn(raid), invite.sparks, invite.large && raid.CanBeLarge,
            invite.extraSeconds, invite.extractionCut, invite.foretoldEvents, null, elapsed, invite.questsUnderWay);

    /// <summary>
    /// Builds a run from everything that decides it. Pure apart from reading the content: launching and following both use it.
    /// <paramref name="questsUnderWay"/> (RaidSession.QuestsUnderWay) is the launcher's quests, or the host's from an invite.
    /// </summary>
    public static RaidSession Build(RaidDefinition raid, List<RaidModifierDefinition> modifiers, PlayerSnapshot loadout, string returnScene, int runSeed,
        RaidDifficulty difficulty, int sparks, bool large, float extraSeconds, float extractionCut, int foretoldEvents, ResidentDefinition companion, float elapsed,
        IReadOnlyList<string> questsUnderWay = null)
    {
        RaidParameters parameters = RaidParameters.Calculate(raid, modifiers, extraSeconds, RaidTimeOfDay.For(raid, runSeed), difficulty, extractionCut);
        var session = new RaidSession(raid, modifiers, parameters, loadout, returnScene, sparks, runSeed, difficulty)
        {
            Companion = companion,
            IsLarge = large,
            Foretold = RaidForecast.For(raid, runSeed, foretoldEvents),
            ExtraRumours = HavenReadiness.ExtraRumours,
            QuestsUnderWay = questsUnderWay,
        };
        if (elapsed > 0f)
        {
            session.Tick(elapsed);
        }

        return session;
    }

    // Saves, builds the run and loads its scene: the part a launch and a follow share.
    private static RaidSession Go(RaidDefinition raid, List<RaidModifierDefinition> modifiers, GameObject player, int runSeed, RaidDifficulty difficulty,
        int sparks, bool large, float extraSeconds, float extractionCut, int foretoldEvents, ResidentDefinition companion, float elapsed,
        IReadOnlyList<string> questsUnderWay)
    {
        // Crafts under way give their materials back before the save and the kit copied for the raid (not when the scene unloads).
        if (player.TryGetComponent(out HandCraftQueue crafts))
        {
            crafts.Stop();
        }

        SaveGameService.SaveBeforeRaid(raid);
        player.TryGetComponent(out PlayerTransport transport);
        if (raid.IsEncounter)
        {
            // A boss encounter is walked into on foot: the horse and the cart wait at home.
            if (player.TryGetComponent(out PlayerRiding riding) && riding.Riding != null)
            {
                riding.Dismount(thrown: false);
            }

            if (transport != null && transport.Using != null && transport.Using.Driver != null)
            {
                transport.Using.Driver.LetGo();
            }
        }
        else
        {
            if (transport != null)
            {
                transport.LeaveForRaid();
            }

            // The horse ridden (or one following) goes through too.
            HorseTravel.LeaveForRaid(player);
        }

        PlayerSnapshot loadout = PlayerSnapshot.Capture(player);
        RaidSession session = Build(raid, modifiers, loadout, SceneManager.GetActiveScene().name, runSeed, difficulty, sparks, large,
            extraSeconds, extractionCut, foretoldEvents, companion, elapsed, questsUnderWay);
        session.GearScoreAtEntry = GearScore.Of(player);

        if (raid.LayoutRules != null)
        {
            (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, runSeed);
            LastRun = (raid.Id, mood.name, shape.shape);
        }

        GameSession.BeginRaid(session);
        GameSession.CarryPlayerState(loadout);

        // The player stays in this scene until the raid scene has loaded, so keep them from wandering off meanwhile.
        PlayerControl.Hold(player, Launching);

        // However the load fails (CheckLaunch rules out a refusal; a load can still break off on the way): no raid under way, the
        // controls back, rather than a player frozen at the portal with a raid that never comes and saving refused.
        bool failed = false;
        SceneLoader.Load(raid.SceneName, $"Through the portal: {raid.DisplayName}", ifItFails: () =>
        {
            failed = true;
            GameSession.AbandonRaid(session);
            GameSession.TakePlayerState();
            PlayerControl.Release(player, Launching);
            GameMessages.Post("The way didn't open. Try the portal again.", MessageKind.Warning);
        });

        return failed ? null : session;
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        LastRun = default;
    }
}