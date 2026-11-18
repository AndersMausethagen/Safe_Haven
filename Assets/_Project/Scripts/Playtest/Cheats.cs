using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// What the cheat menu (F1) can do, as plain calls: the switches that stay on across scenes, and the one-off actions. No UI here, so
/// the menu, the CLI's playtest commands and tests share it. Each call says what happened in words for the player, and
/// <see cref="Used"/> lets the playtest log mark a run as helped. The switches last for the session and are never saved.
/// </summary>
public static class Cheats
{
    private const float OneHitBonus = 1000f;
    private const float FastBonus = 1f;
    private const float KillReach = 45f;
    private const int FarAway = 1000;

    private static readonly LeaseKey Source = new LeaseKey("the cheat menu");

    /// <summary>Raised with a cheat's name each time one is used.</summary>
    public static event Action<string> Used;

    public static bool Invincible { get; private set; }
    public static bool OneHitKills { get; private set; }
    public static bool Fast { get; private set; }
    public static bool EndlessStamina { get; private set; }
    public static bool NoNeeds { get; private set; }
    /// <summary>Building and crafting cost nothing (MaterialPool).</summary>
    public static bool FreeCosts { get; private set; }
    /// <summary>Building requirements, station tiers and raid locks hold nothing back (BuildingUnlocks, StationCrafting, RaidLauncher).</summary>
    public static bool NoRequirements { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Invincible = OneHitKills = Fast = EndlessStamina = NoNeeds = FreeCosts = NoRequirements = false;
        Used = null;
    }

    // ----- Switches -----

    public static void SetInvincible(bool on) => Set(() => Invincible = on, "Invincible", on);

    public static void SetOneHitKills(bool on) => Set(() => OneHitKills = on, "One-hit kills", on);

    public static void SetFast(bool on) => Set(() => Fast = on, "Fast", on);

    public static void SetEndlessStamina(bool on) => Set(() => EndlessStamina = on, "Endless stamina", on);

    public static void SetNoNeeds(bool on) => Set(() => NoNeeds = on, "No hunger or thirst", on);

    public static void SetFreeCosts(bool on) => Set(() => FreeCosts = on, "Free building and crafting", on);

    public static void SetNoRequirements(bool on) => Set(() => NoRequirements = on, "No requirements", on);

    private static void Set(Action change, string name, bool on)
    {
        change();
        ApplyToPlayer();
        if (on)
        {
            Used?.Invoke(name);
        }
    }

    /// <summary>Puts the switches on the player in this scene: each scene has its own player object.</summary>
    public static void ApplyToPlayer()
    {
        Player player = Player.Current;
        if (player == null)
        {
            return;
        }

        if (player.TryGetComponent(out Damageable health))
        {
            health.RemoveInvulnerability(Source);
            if (Invincible)
            {
                health.AddInvulnerability(Source);
            }
        }

        if (player.TryGetComponent(out Combatant combatant))
        {
            combatant.Stats.RemoveModifiersFrom(Source);
            if (OneHitKills)
            {
                combatant.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, OneHitBonus, Source));
            }

            if (Fast)
            {
                combatant.Stats.AddModifier(new StatModifier(StatType.MoveSpeed, StatModifierOp.Flat, FastBonus, Source));
            }
        }
    }

    /// <summary>What the switches keep doing while they are on. Called every frame by CheatSystem.</summary>
    public static void Tick()
    {
        if ((!EndlessStamina && !NoNeeds) || Player.Current == null)
        {
            return;
        }

        if (EndlessStamina && Player.Current.TryGetComponent(out PlayerStamina stamina))
        {
            stamina.Restore(stamina.Max);
        }

        if (NoNeeds && Player.Current.TryGetComponent(out PlayerNeeds needs))
        {
            needs.Hunger.Set(needs.Hunger.Max);
            needs.Thirst.Set(needs.Thirst.Max);
        }
    }

    // ----- The player -----

    public static string Heal()
    {
        GameObject player = PlayerObject();
        if (player == null)
        {
            return NoPlayer;
        }

        if (player.TryGetComponent(out Damageable health))
        {
            if (!health.IsAlive)
            {
                health.Revive();
            }
            else
            {
                health.Heal(health.MaxHealth);
            }
        }

        if (player.TryGetComponent(out PlayerNeeds needs))
        {
            needs.Hunger.Set(needs.Hunger.Max);
            needs.Thirst.Set(needs.Thirst.Max);
        }

        if (player.TryGetComponent(out PlayerStamina stamina))
        {
            stamina.Restore(stamina.Max);
        }

        return Did("Heal", "Health, stamina, hunger and thirst filled.");
    }

    /// <summary>A wild horse a little way ahead, out of its fright distance, to try the lasso on.</summary>
    public static string WildHorse() => WildHorse(HorseSettings.Current.WildHorse);

    /// <summary>The bog's toxic horse a little way ahead, whatever the hour.</summary>
    public static string ToxicHorse()
    {
        foreach (HorseDefinition kind in HorseSettings.Current.SpecialHorses)
        {
            if (kind != null && kind.Home == HorseHome.Bog)
            {
                return WildHorse(kind);
            }
        }

        return "No bog horse in this game.";
    }

    /// <summary>Every horse of yours nearby is as fond of you as a horse can be: it comes to the whistle and may foal.</summary>
    public static string BondHorses()
    {
        int count = 0;
        Player player = Player.Current;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsTamed && player != null && (horse.transform.position - player.transform.position).sqrMagnitude < 60f * 60f)
            {
                horse.Befriend(HorseBondRules.Most * 2f);
                count++;
            }
        }

        return count > 0 ? Did("Horse bond", $"{count} horse(s) at full bond.") : "No horse of yours within 60 m.";
    }

    private static string WildHorse(HorseDefinition kind)
    {
        Player player = Player.Current;
        if (player == null || kind == null)
        {
            return "No horse to bring.";
        }

        Vector3 at = player.transform.position + player.transform.forward * 8f;
        if (!UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return "No ground for a horse ahead.";
        }

        HorseHerd.SpawnAt(kind, hit.position);
        return Did(kind.DisplayName, $"A {kind.DisplayName.ToLowerInvariant()} grazes ahead. Sneak closer and use the lasso.");
    }

    /// <summary>What taming and keeping a horse takes: a lasso, barley and a hide saddle, into the bag.</summary>
    public static string HorseKit()
    {
        HorseDefinition horse = HorseSettings.Current.WildHorse;
        if (horse == null)
        {
            return "No horses in this game.";
        }

        Give(horse.Lasso, 1, false);
        Give(horse.Barley, 8, false);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        Give(content != null ? content.FindItem("hide_saddle") : null, 1, false);
        return Did("Horse kit", "A lasso, 8 barley and a hide saddle.");
    }

    public static string GrantExperience(int amount)
    {
        ExperienceTracker.GrantInsightExactly(amount);
        return Did("Insight", $"{amount} Insight to put into a skill ({{key:Talents}}).");
    }

    /// <summary>Gives items, to the bag or the stash. What doesn't fit the bag (weight, slots) goes to the stash. Gear isn't equipped.</summary>
    public static string Give(ItemDefinition item, int amount, bool toStash)
    {
        if (item == null || amount <= 0)
        {
            return "Nothing to give.";
        }

        GameObject player = PlayerObject();
        int inBag = 0;
        if (!toStash && player != null && player.TryGetComponent(out ItemInventory inventory))
        {
            inBag = inventory.Container.Add(item, amount);
        }

        int inStash = inBag < amount ? GameSession.Stash.Items.Add(item, amount - inBag) : 0;
        int lost = amount - inBag - inStash;
        string where = inBag > 0 && inStash > 0 ? $"{inBag} in the bag, {inStash} in the stash" : inBag > 0 ? "in the bag" : "in the stash";
        return Did("Give", lost > 0 ? $"{inBag + inStash} {item.DisplayName} ({where}); {lost} didn't fit." : $"{amount} {item.DisplayName} {where}.");
    }

    /// <summary>Gives an essence to what the player carries, or to the stash.</summary>
    public static string Give(ResourceDefinition resource, int amount, bool toStash)
    {
        if (resource == null || amount <= 0)
        {
            return "Nothing to give.";
        }

        GameObject player = PlayerObject();
        ResourceStorage storage = !toStash && player != null && player.TryGetComponent(out ResourceInventory carried) ? carried.Storage : GameSession.Stash.Resources;
        int added = storage.Add(resource, amount);
        return Did("Give", $"{added} {resource.DisplayName}{(added < amount ? ", all there was room for" : "")}.");
    }

    /// <summary>A stack of every material of a tier, to the stash, for testing what a tier builds and makes.</summary>
    public static string GiveMaterials(int tier, int amount)
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        int kinds = 0;
        int shortOf = 0;
        foreach (ItemDefinition item in content.Items)
        {
            if (item != null && item.Category == ItemCategory.Material && item.Tier == tier)
            {
                kinds++;
                if (GameSession.Stash.Items.Add(item, amount) < amount)
                {
                    shortOf++;
                }
            }
        }

        string full = shortOf > 0 ? $" The stash is full: {shortOf} of them got less, or none." : "";
        return Did("Materials", $"{amount} each of {kinds} tier {Tiers.ToRoman(tier)} materials in the stash.{full}");
    }

    public static string GiveEssences(int amount)
    {
        GameObject player = PlayerObject();
        if (player == null || !player.TryGetComponent(out ResourceInventory carried))
        {
            return NoPlayer;
        }

        foreach (ResourceDefinition resource in CheatSettings.Current.Essences)
        {
            if (resource != null)
            {
                carried.Storage.Add(resource, amount);
            }
        }

        return Did("Essences", $"{amount} of every essence, as far as they fit.");
    }

    public static string UnlockEverything()
    {
        GameObject player = PlayerObject();
        if (player == null || !player.TryGetComponent(out PlayerCrafting crafting))
        {
            return NoPlayer;
        }

        int recipes = crafting.Unlock(crafting.Recipes, false);
        int buildings = crafting.UnlockBuildings(crafting.Buildings, false);
        return Did("Unlock", $"Learned {recipes} recipes and {buildings} buildings.");
    }

    public static string GoTo(Vector3 position, string name)
    {
        GameObject player = PlayerObject();
        if (player == null || !player.TryGetComponent(out PlayerPersistence persistence))
        {
            return NoPlayer;
        }

        Vector3 spot = NavMesh.SamplePosition(position, out NavMeshHit hit, 12f, NavMesh.AllAreas) ? hit.position : position;
        persistence.MoveTo(new PlayerPose(spot, player.transform.eulerAngles.y));
        return Did("Go to", $"Went to {name}.");
    }

    // ----- Raids -----

    /// <summary>
    /// Starts a raid from anywhere at home. On a raid with generated maps it can be of a chosen mood (by name) and shape, and large or
    /// normal; left open, the run rolls them as usual and its size follows the mood (RaidMapMood.largeByDefault). The run is an ordinary
    /// one, found by its seed, so its briefing, map and spawns all agree. False, with a reason, when it can't.
    /// </summary>
    public static bool StartRaid(RaidDefinition raid, out string result, string mood = null, RaidLayoutShape? shape = null, bool? large = null)
    {
        GameObject player = PlayerObject();
        if (player == null)
        {
            result = NoPlayer;
            return false;
        }

        RaidLayoutRules rules = raid != null ? raid.LayoutRules : null;
        int seed = RaidSession.NewSeed();
        if (rules != null && (!string.IsNullOrEmpty(mood) || shape.HasValue))
        {
            int? found = RaidLayoutPlanner.FindSeed(rules, (rolledShape, rolledMood) =>
                (string.IsNullOrEmpty(mood) || rolledMood.name == mood) && (!shape.HasValue || rolledShape.shape == shape.Value), seed);
            if (found == null)
            {
                result = $"{raid.DisplayName} has no {(string.IsNullOrEmpty(mood) ? "" : mood + " ")}{(shape.HasValue ? shape.Value + " " : "")}maps.";
                return false;
            }

            seed = found.Value;
        }

        (RaidShapeRule chosenShape, RaidMapMood chosenMood) = rules != null ? RaidLayoutPlanner.Choose(rules, seed) : (null, null);
        bool goLarge = large ?? (chosenMood != null && chosenMood.largeByDefault);
        if (!RaidLauncher.TryLaunch(raid, player, Array.Empty<ItemDefinition>(), out string problem, 0, seed, null, null, goLarge))
        {
            result = problem;
            return false;
        }

        string kind = chosenMood != null ? $": {chosenMood.name}, {chosenShape.shape}, {(goLarge && raid.CanBeLarge ? "large" : "normal")}, seed {seed}" : "";
        result = Did("Start raid", $"Into {raid.DisplayName}{kind}.");
        return true;
    }

    public static string ExtractFromRaid()
    {
        RaidDirector director = RaidDirector.Current;
        if (director == null || !director.IsRunning)
        {
            return NoRaid;
        }

        return director.Extract(director.Player) ? Did("Extract", "Extracted.") : "Extraction refused.";
    }

    /// <summary>Ends the raid as a loss, as dying does, to test what a failed raid keeps.</summary>
    public static string FailRaid()
    {
        RaidDirector director = RaidDirector.Current;
        if (director == null || !director.IsRunning)
        {
            return NoRaid;
        }

        if (director.Player.TryGetComponent(out Damageable health))
        {
            health.RemoveInvulnerability(Source);
            Kill(health, director.Player.transform.position);
        }

        return Did("Fail raid", "The raid is lost.");
    }

    public static string AddRaidTime(float seconds)
    {
        RaidDirector director = RaidDirector.Current;
        if (director == null || !director.IsRunning)
        {
            return NoRaid;
        }

        director.Session.AddTime(seconds);
        return Did("Raid time", $"{RaidStats.FormatTime(director.Session.TimeRemaining)} left.");
    }

    /// <summary>Runs the clock down to a few seconds, to test the end of a raid. The director still finishes it its own way.</summary>
    public static string EndRaidClock(float secondsLeft)
    {
        RaidDirector director = RaidDirector.Current;
        if (director == null || !director.IsRunning)
        {
            return NoRaid;
        }

        director.Session.Tick(Mathf.Max(0f, director.Session.TimeRemaining - secondsLeft));
        return Did("Raid clock", $"{secondsLeft:0} seconds left.");
    }

    public static string RevealMap()
    {
        ExplorationMap map = ExplorationMap.Current;
        if (map == null || PlayerObject() == null)
        {
            return "Nothing to reveal here.";
        }

        map.RevealAround(PlayerObject().transform.position, FarAway);
        return Did("Reveal map", "The whole map is revealed.");
    }

    // ----- Creatures -----

    /// <summary>Spawns creatures in front of the player, fanned out, on walkable ground.</summary>
    public static List<GameObject> Spawn(GameObject prefab, int count, float distance)
    {
        var spawned = new List<GameObject>();
        GameObject player = PlayerObject();
        if (player == null || prefab == null)
        {
            return spawned;
        }

        int total = Mathf.Max(1, count);
        for (int i = 0; i < total; i++)
        {
            float angle = (i - (total - 1) * 0.5f) * 25f;
            Vector3 position = player.transform.position + Quaternion.Euler(0f, angle, 0f) * player.transform.forward * distance;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                position = hit.position;
            }

            Vector3 facing = player.transform.position - position;
            facing.y = 0f;
            spawned.Add(Object.Instantiate(prefab, position, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity));
        }

        Used?.Invoke("Spawn");
        return spawned;
    }

    public static string KillNearby()
    {
        GameObject player = PlayerObject();
        if (player == null)
        {
            return NoPlayer;
        }

        int killed = 0;
        foreach (MonsterAI monster in Object.FindObjectsByType<MonsterAI>())
        {
            if (monster.TryGetComponent(out Damageable health) && health.IsAlive
                && (monster.transform.position - player.transform.position).sqrMagnitude <= KillReach * KillReach)
            {
                Kill(health, monster.transform.position);
                killed++;
            }
        }

        return Did("Kill nearby", $"{killed} creatures killed.");
    }

    private static void Kill(Damageable health, Vector3 at)
    {
        health.ApplyDamage(new DamageInfo(null, null, health.MaxHealth * 100f, DamageType.Physical, false, at, Vector3.forward));
    }

    // ----- The world -----

    public static string SetHour(float hour)
    {
        DayNightCycle cycle = Object.FindAnyObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            return "This place has no sky to change.";
        }

        cycle.Hour = hour;
        return Did("Time of day", $"The hour is {hour:0}.");
    }

    public static string SetWeather(WeatherDefinition weather)
    {
        WeatherSystem system = Object.FindAnyObjectByType<WeatherSystem>();
        if (system == null || weather == null)
        {
            return "This place has no weather to change.";
        }

        system.SetWeather(weather, 1f);
        return Did("Weather", $"{weather.name}.");
    }

    public static string FinishBuildings()
    {
        GameObject player = PlayerObject();
        int finished = 0;
        foreach (BuildingObject building in new List<BuildingObject>(BuildingObject.Active))
        {
            if (building.TryGetComponent(out Construction construction) && construction.IsBuilding)
            {
                construction.Finish(player);
                finished++;
            }
        }

        return Did("Finish buildings", $"{finished} buildings finished.");
    }

    public static string FinishCrafts()
    {
        int done = GameSession.Here.Workshops.Tick(FarAway * 1000f, GameSession.Stash.Resources, (station, recipe) =>
            GameEvents.Raise(new GameEvent(GameEventKind.Crafted, recipe.OutputDefinition, recipe.OutputAmount)));
        return Did("Finish crafts", $"{done} crafts finished.");
    }

    public static string BringHome(ResidentDefinition person)
    {
        if (person == null || !GameSession.Home.AddResident(person))
        {
            return "They are already home.";
        }

        ResidentNpc.TeachWhatTheyKnow(person, Player.Current != null ? Player.Current.gameObject : null);
        return Did("Bring home", $"{person.DisplayName} is home.");
    }

    public static string BeginCorruption()
    {
        CorruptionNights corruption = CorruptionNights.Current;
        if (corruption == null || corruption.IsActive)
        {
            return corruption == null ? "Corruption only reaches Safe Haven." : "The corruption is already coming.";
        }

        corruption.Begin(5f);
        return Did("Corruption", "Corruption creeps in from the Misty Woods.");
    }

    public static string BeginBreach()
    {
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach == null || breach.IsActive)
        {
            return breach == null ? "Only Safe Haven can be breached." : "A breach is already running.";
        }

        GameSession.Home.SetBreach(BreachStatus.Pending);
        breach.Begin();
        return breach.IsActive ? Did("Breach", "The ward is under attack.") : "No breach could start here.";
    }

    /// <summary>Finishes every quest under way, to get past a part of the story.</summary>
    public static string CompleteActiveQuests()
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        var active = new List<QuestDefinition>();
        foreach (QuestLog.Entry entry in log.Entries)
        {
            if (entry.Status == QuestStatus.Active)
            {
                active.Add(entry.Quest);
            }
        }

        foreach (QuestDefinition quest in active)
        {
            log.ForceComplete(quest);
        }

        return Did("Complete quests", $"{active.Count} quests completed.");
    }

    private const string NoPlayer = "There is no player here.";
    private const string NoRaid = "No raid is running.";

    private static GameObject PlayerObject() => Player.Current != null ? Player.Current.gameObject : null;

    private static string Did(string cheat, string words)
    {
        Used?.Invoke(cheat);
        return words;
    }
}
