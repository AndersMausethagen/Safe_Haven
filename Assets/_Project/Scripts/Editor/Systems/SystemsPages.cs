using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// The Systems Editor's pages, one per game system. Each one tunes a settings asset (or the player prefab) and shows the
// numbers that follow from it. See SystemsPage for the helpers and how to add a page.

/// <summary>Hunger, thirst and food (GDD §11).</summary>
public sealed class SurvivalPage : SystemsPage
{
    public override string Title => "Survival";
    public override string Group => "Player";
    public override string Help => "How fast hunger and thirst drain at home and in raids, what running out does, and what each food restores. " +
                                   "Nothing here can kill the player.";

    public override void Draw()
    {
        var settings = Settings<SurvivalSettings>(SurvivalSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Survival settings", "Resources/SurvivalSettings");
        BeginDerived();
        Derived("A full belly lasts at home", Minutes(SurvivalSettings.MinutesToEmpty(settings.MaxHunger, settings.HungerPerMinute)));
        Derived("A full belly lasts in a raid", Minutes(SurvivalSettings.MinutesToEmpty(settings.MaxHunger, settings.HungerPerMinute, settings.RaidMultiplier)));
        Derived("Full thirst lasts at home", Minutes(SurvivalSettings.MinutesToEmpty(settings.MaxThirst, settings.ThirstPerMinute)));
        Derived("Full thirst lasts in a raid", Minutes(SurvivalSettings.MinutesToEmpty(settings.MaxThirst, settings.ThirstPerMinute, settings.RaidMultiplier)));
        Derived("A new game wakes up hungry after", Minutes(SurvivalSettings.MinutesToEmpty(settings.MaxHunger * (settings.StartingHunger - settings.LowWarning), settings.HungerPerMinute)));
        Derived("Starving", $"max stamina ×{settings.StarvingMaxStamina:0.##}, regeneration ×{settings.StarvingRegeneration:0.##}");
        Derived("Parched", $"regeneration ×{settings.ParchedRegeneration:0.##}, in combat ×{settings.ParchedRegeneration * settings.ParchedCombatRegeneration:0.##}");
        EndDerived();

        var foods = LoadAll<ItemDefinition>().FindAll(item => item.IsConsumable);
        Table("Food, drink and potions (items)", "Right-click in the bag to use. Health, hunger and thirst restored per use.", foods,
            new[] { new Column("healAmount", "Health"), new Column("hungerRestore", "Hunger"), new Column("thirstRestore", "Thirst"), new Column("maximumStack", "Stack") },
            item => item.HungerRestore > 0f ? $"{Mathf.CeilToInt(settings.MaxHunger / item.HungerRestore)} fill an empty belly" : "", "");
    }
}

/// <summary>The bag's weight budget, item weights, essence caps and backpacks (GDD §12.4).</summary>
public sealed class CarryingPage : SystemsPage
{
    public override string Title => "Carrying";
    public override string Group => "Player";
    public override string Help => "Everything in the bag has weight and must fit the bag's weight budget; gear worn and the safe pocket don't count. " +
                                   "Essences are weightless and capped per bar. Backpacks add weight and bag slots. At home, crafting and " +
                                   "building also use what is in storage.";

    public override void Draw()
    {
        GameObject player = PlayerPrefab;
        if (player == null)
        {
            EditorGUILayout.HelpBox("PlayerArmature prefab not found.", MessageType.Warning);
            return;
        }

        var resources = player.GetComponent<ResourceInventory>();
        var items = player.GetComponent<ItemInventory>();
        SettingsPanel(resources, "Essence bars (player)", "PlayerArmature > Resource Inventory", "baseCapacity");
        SettingsPanel(items, "Bag (player)", "PlayerArmature > Item Inventory", "carryWeight", "baseUnlockedSlots", "safePocketSlots");

        var all = LoadAll<ItemDefinition>();
        all.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
        float budget = items.CarryWeight;
        Table("Item weights", "Weight per item. The last column is how many fit the bag's budget on their own.", all,
            new[] { new Column("weight", "Weight"), new Column("maximumStack", "Stack"), new Column("tier", "Tier", 50f) },
            item => budget <= 0f ? "no limit" : item.Weight <= 0f ? "weightless"
                : $"{Mathf.FloorToInt(budget / item.Weight)} ({item.Category})", "Fits alone");

        var packs = LoadAll<ItemDefinition>().FindAll(item => item.IsBackpack);
        Table("Backpacks", "Only the best backpack carried counts.", packs,
            new[] { new Column("carryCapacityBonus", "+Weight"), new Column("bagSlotBonus", "+Slots") },
            item => $"{budget + item.CarryCapacityBonus:0} weight, {items.UnlockedSlotsWith(item.BagSlotBonus)} slots", "With it");
    }
}

/// <summary>What a failed raid costs and what repairs cost (GDD §8.3, §13.4).</summary>
public sealed class DeathAndRepairPage : SystemsPage
{
    public override string Title => "Death & Repair";
    public override string Group => "Player";
    public override string Help => "A failed raid loses what was carried but keeps the tools, weapons and backpacks taken through the portal, " +
                                   "worn down. Broken gear is kept but useless until repaired at the repair station.";

    public override void Draw()
    {
        var settings = Settings<DurabilitySettings>(DurabilitySettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Durability settings", "Resources/DurabilitySettings");
        BeginDerived();
        Derived("New gear breaks after", settings.LossOnFailedRaid > 0f ? $"{settings.FailedRaidsUntilBroken} failed raids" : "never");
        Derived("Repairs are made at", settings.RepairStation.WithArticle());
        EndDerived();

        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        IReadOnlyList<RecipeDefinition> recipes = book != null ? book.Recipes : new List<RecipeDefinition>();
        var gear = LoadAll<ItemDefinition>().FindAll(item => item.HasDurability);
        ContentUtility.BeginPanel("Repair costs", "What repairing costs after one failed raid, and from broken. Change the recipe to change these.");
        foreach (ItemDefinition item in gear)
        {
            RecipeDefinition recipe = Repairs.RecipeFor(item, recipes);
            string worn = Repairs.Cost(new ItemStack(item, 1, 1f - settings.LossOnFailedRaid), recipe, settings).Describe();
            string broken = Repairs.Cost(new ItemStack(item, 1, 0f), recipe, settings).Describe();
            EditorGUILayout.LabelField(item.DisplayName, recipe == null ? "no recipe: repairs are free" : $"after one failed raid: {worn}     from broken: {broken}",
                ContentUtility.WrappedMiniLabel);
        }

        ContentUtility.EndPanel();
    }
}

/// <summary>How Quest Givers show their quest's state: the floating "!", "…" and "?" over their heads.</summary>
public sealed class QuestMarkersPage : SystemsPage
{
    public override string Title => "Quest Markers";
    public override string Group => "World";
    public override string Help => "Every Quest Giver floats a marker over its head: \"!\" when its quest can be taken, \"…\" while the player is on it " +
                                   "and \"?\" when it is ready to hand in (quests with \"Turn In To Giver\"). Set the symbols, colors and look " +
                                   "for all givers here; a giver can hide its own marker with Show Marker. Quests themselves are made in the " +
                                   "Content Editor (Quests).";

    public override void Draw()
    {
        var settings = Settings<QuestMarkerSettings>(QuestMarkerSettings.ResourceName);
        if (settings != null)
        {
            SettingsPanel(settings, "Quest markers", "Resources/QuestMarkerSettings");
        }
    }
}

/// <summary>The fonts and colours of all the game's UI.</summary>
public sealed class UIStylePage : SystemsPage
{
    public override string Title => "UI Style";
    public override string Group => "Settings";
    public override string Help => "Hearthline: the look of every window and HUD. Nothing frames the world: the HUD is type and thin lines with a soft " +
                                   "shadow, and amber is the one accent. Windows are solid. Colours changed here reach the whole game when it next " +
                                   "runs; new UI prefabs get their roles from Safe Haven › Setup › UI › Apply Hearthline.";

    public override void Draw()
    {
        var style = Settings<UIStyle>(UIStyle.ResourceName);
        if (style != null)
        {
            SettingsPanel(style, "UI style", "Resources/UIStyle");
        }
    }
}

/// <summary>The room the game leaves between quests, fights and lines.</summary>
public sealed class StoryPacingPage : SystemsPage
{
    public override string Title => "Story Pacing";
    public override string Group => "World";
    public override string Help => "A quest that starts by itself (automatically, or on a Quest Event) waits its turn: a pause after the last quest, " +
                                   "the end of any fight and a little more, and until nobody is talking. Story lines (Say, Choice) wait out the " +
                                   "same pause after a fight; a line said during a fight stays a subtitle. A quest's own Start Delay replaces the " +
                                   "pause after the last quest, for a beat before a big moment. Someone asking (a Quest Giver, a story's Start Quest) " +
                                   "starts a quest at once. Add Wait For Quiet to a story for cutscenes that have no line.";

    public override void Draw()
    {
        var settings = Settings<StoryPacingSettings>(StoryPacingSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Story pacing", "Resources/StoryPacingSettings");
        BeginDerived();
        Derived("Next quest in a chain", $"{settings.BetweenQuestsSeconds:0.#} s after the last, once the coast is clear");
        Derived("After the last enemy falls", $"{settings.AfterFightSeconds:0.#} s before anything new begins");
        Derived("Never later than", $"{settings.LongestHoldSeconds:0.#} s after it was due, even with a fight still on");
        EndDerived();
    }
}

/// <summary>How monsters notice the player (sight cone, close sense, hearing) and how the player stays unnoticed.</summary>
public sealed class SensesAndStealthPage : SystemsPage
{
    public override string Title => "Senses & Stealth";
    public override string Group => "World";
    public override string Help => "Monsters see in a cone that cover blocks, notice anything very close from any direction and hear noise " +
                                   "(footsteps, dodges, fights, gathering). A glimpse or a sound makes them suspicious (\"?\"): they come to " +
                                   "look. Seeing you long enough spots you (\"!\"). A target they can't see for a while is lost: they search " +
                                   "where they last saw it. Sneak with Left Ctrl / left bumper. Each monster's own eyes and ears are below " +
                                   "(Sight = its Detection Range).";

    public override void Draw()
    {
        var rules = Settings<PerceptionSettings>(PerceptionSettings.ResourceName);
        if (rules != null)
        {
            SettingsPanel(rules, "Perception", "Resources/PerceptionSettings: noticing, searching, hearing, the player's visibility and noise");
            var eyes = new SenseProfile(10f, 120f, 2.5f, 1f, 1f);
            BeginDerived("For a monster with 10 m sight");
            foreach ((string label, float visibility) in new[] { ("Walking", rules.MovingVisibility), ("Standing still", rules.StillVisibility),
                         ("Sneaking", rules.SneakingVisibility), ("Fighting", rules.FightingVisibility) })
            {
                float range = PerceptionRules.EffectiveSightRange(eyes, visibility, rules);
                float near = PerceptionRules.NoticeRate(eyes.CloseRange + 0.5f, 0f, eyes, visibility, rules);
                float far = PerceptionRules.NoticeRate(range * 0.95f, 0f, eyes, visibility, rules);
                Derived(label, range <= 0f ? "never seen" : $"seen up to {range:0.#} m; spotted in {Seconds(near)} up close, {Seconds(far)} at the edge");
            }
            Derived("Footsteps heard", $"{rules.FootstepNoise:0.#} m walking, {rules.SneakingFootstepNoise:0.#} m sneaking (× a monster's Hearing), up to {rules.HeavyGearNoise:0.#}× in heavy gear");
            Derived("Cover and rain", $"cover leaves {rules.HeardThroughCover:P0} of a sound's reach; the heaviest rain {rules.NoiseInRain:P0} of the player's noise");
            Derived("Longest a fight carries", $"{rules.CombatNoise + rules.NoiseBuildMax:0.#} m, once {rules.NoiseBuildMax:0.#} m of build-up has gathered");
            Derived("Lost target", $"after {rules.LoseTrackSeconds:0.#} s out of sight; searched for {rules.SearchSeconds:0.#} s");
            Derived("Ambush", rules.AmbushDamageTaken > 1f ? $"unaware monsters take {rules.AmbushDamageTaken - 1f:P0} more damage" : "no bonus");
            EndDerived();
        }

        Table("Monster senses", "Sight is the Detection Range. Angle is the sight cone, Close is the all-around sense.",
            LoadAll<MonsterDefinition>(),
            new[] { new Column("detectionRange", "Sight"), new Column("sightAngle", "Angle"), new Column("closeSenseRange", "Close"),
                new Column("hearing", "Hearing"), new Column("alertness", "Alertness"), new Column("chaseRange", "Chase") });
    }

    private static string Seconds(float rate) => rate > 0f ? $"{1f / rate:0.#} s" : "never";
}

/// <summary>Picking the lock on a chest in a raid.</summary>
public sealed class LockpickingPage : SystemsPage
{
    public override string Title => "Lockpicking";
    public override string Group => "World";
    public override string Help => "A chest in a raid has to be picked before it can be searched. A marker runs along a bar and each press tries a " +
                                   "pin: green sets it silently, yellow with a click, anywhere else slips, drops a pin and rattles the lock. Enough " +
                                   "slips and it gives way by force, loudly, so a chest always opens in the end. The lid is heard either way. Pins " +
                                   "are set on each Loot Cache; 0 means no lock. Sneaking doesn't help here: only clean presses are quiet.";

    public override void Draw()
    {
        var settings = Settings<LockpickingSettings>(LockpickingSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Lockpicking", "Resources/LockpickingSettings");
        BeginDerived();
        Derived("Quietest chest (3 pins)", $"{settings.NoiseOf(SwingResult.Critical):0.#} m per pin, then the lid at {settings.OpenNoise:0.#} m");
        Derived("Worst chest", $"{settings.SlipsAllowed - 1} slips at {settings.NoiseOf(SwingResult.Glancing):0.#} m, then forced open at {settings.ForcedNoise:0.#} m");
        EndDerived();
    }
}

/// <summary>The Lost: named, mist-taken settlers who hunt the Keeper and grow when they win (LostOneSettings).</summary>
public sealed class LostOnesPage : SystemsPage
{
    public override string Title => "The Lost";
    public override string Group => "World";
    public override string Help => "Named settlers the mist took, who hunt the Keeper. Once the story gate is met and enough raids have come home, a few rise " +
                                   "(a roster per character). Each raid may have one walk it, an elite with its own name, chosen from the run's seed so the " +
                                   "briefing can tell it once it has been met. Killing the player raises it a rank and it keeps the best of what the death " +
                                   "took; wounded below the flee line it runs into the mist, to come back scarred, and the player sees through one of its " +
                                   "traits; laid to rest, it drops what it carried. Bodies by rank (cloth, leather, mail), the Draug while a grade is empty.";

    public override void Draw()
    {
        var settings = Settings<LostOneSettings>(LostOneSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "The Lost", "Resources/LostOneSettings");
        BeginDerived();
        for (int tier = 1; tier <= Tiers.Max; tier++)
        {
            Derived($"Chance one walks a tier {Tiers.ToRoman(tier)} raid", $"{settings.ChanceFor(tier, 1):P0} at rank I, {settings.ChanceFor(tier, settings.MaxRank):P0} at rank {Tiers.ToRoman(settings.MaxRank)}");
        }

        for (int rank = 1; rank <= settings.MaxRank; rank++)
        {
            LostOneSettings.Rank row = settings.RankAt(rank);
            MonsterDefinition body = settings.BodyFor(new LostOneRecord { Id = "lost-1", Rank = rank });
            Derived($"Rank {Tiers.ToRoman(rank)} ({row.title})",
                $"health ×{row.health:0.##}, damage ×{row.damage:0.##} on top of {row.traits} trait(s); walks as {(body != null ? body.DisplayName : "nothing: no body set")}");
        }

        Derived("Scars", $"+{settings.ScarPerEscape:P0} health per escape, at most +{settings.MostScar:P0}");
        Derived("Traits to choose from", settings.Traits.Count == 0 ? "none: add elite affixes" : settings.Traits.Count.ToString());
        EndDerived();
    }
}

/// <summary>The timed swing minigame for axe, pickaxe and sickle gathering.</summary>
public sealed class GatheringMinigamePage : SystemsPage
{
    public override string Title => "Gathering Minigame";
    public override string Group => "Player";
    public override string Help => "Chopping, mining and cutting are a series of timed swings, one per bar. Stop the marker in the green zone for a " +
                                   "                                   critical (+bonus yield, quiet, a brief freeze), in the yellow zone for a normal swing; missing or not pressing is a " +
                                   "glancing blow that wears the tool. Each tool has a profile: bar speed, zone widths and loudness (the sickle is " +
                                   "quick and quiet). In raids every swing makes noise that wakes and draws monsters, and feeds the Forest " +
                                                                      "Annoyance raid system. Swings per node are set on each gathering node. Sounds, particles and node shake come " +
                                                                      "from Harvest Feedback profiles (Content › Harvest Feedback); the Default profile here covers nodes without one.";

    public override void Draw()
    {
        var settings = Settings<GatheringMinigameSettings>(GatheringMinigameSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Gathering minigame", "Resources/GatheringMinigameSettings");
        float wear = DurabilitySettings.Current.WearPerGlancingBlow;
        BeginDerived();
        foreach (GatheringToolProfile profile in settings.Profiles)
        {
            if (profile == null)
            {
                continue;
            }

            float maxCrit = GatheringSession.CriticalWidthFor(profile.CriticalZoneWidth, settings.CriticalWidthPerCombo, settings.MaxCombo, settings.MaxCombo);
            Derived($"{profile.Tool}: critical zone (base / max combo)", $"{profile.CriticalZoneWidth:P0} / {maxCrit:P0} of the bar");
            Derived($"{profile.Tool}: critical window / normal noise",
                $"{profile.RiseSeconds * profile.CriticalZoneWidth:0.00} s per swing / {settings.NoiseRadius(SwingResult.Normal) * profile.Loudness:0} m");
        }

        Derived("Glancing blows until a new tool breaks", wear > 0f ? Mathf.CeilToInt(1f / wear).ToString() : "never");
        EndDerived();
    }
}

/// <summary>What felling a tree gives: logs by axe tier and tree size, and the chance of better wood.</summary>
public sealed class TreeChoppingPage : SystemsPage
{
    public override string Title => "Tree Chopping";
    public override string Group => "Player";
    public override string Help => "Every tree in the world can be felled with an axe (hatchet). A better axe and a bigger tree give more logs, " +
                                   "and each log may come out as a better wood (Hardwood, Elderwood): a wood at most one tier above the axe. " +
                                   "The trees grow back (the Tree gathering node's respawn time).";

    public override void Draw()
    {
        var settings = Settings<TreeChoppingSettings>(TreeChoppingSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Tree chopping", "Resources/TreeChoppingSettings");
        BeginDerived();
        foreach (int axe in new[] { 1, 3, 4 })
        {
            foreach (float size in new[] { 0.6f, 1f, 2f })
            {
                string woods = "";
                for (int tier = 2; tier <= settings.WoodByTier.Count; tier++)
                {
                    ItemDefinition wood = settings.WoodByTier[tier - 1];
                    woods += $", {(wood != null ? wood.DisplayName : "?")} {settings.ChanceOf(tier, axe, size):P0}";
                }

                Derived($"Tier {Tiers.ToRoman(axe)} axe, tree size {size:0.0}", $"{settings.Logs(axe, size)} logs{woods} each");
            }
        }

        EndDerived();
    }
}

/// <summary>Movement, dodge, stamina, combat rules and feel, and the camera.</summary>
public sealed class MovementAndCombatPage : SystemsPage
{
    public override string Title => "Movement & Combat";
    public override string Group => "Player";
    public override string Help => "The player's movement and dodge (GDD §10.3: about 0.35 s with a 2–3 s cooldown), stamina costs, the combat " +
                                   "formulas, hit feedback and the camera.";

    public override void Draw()
    {
        var movement = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/_Project/Data/Player/PlayerMovementSettings.asset");
        var stamina = AssetDatabase.LoadAssetAtPath<PlayerStaminaSettings>("Assets/_Project/Data/Player/PlayerStaminaSettings.asset");
        SettingsPanel(movement, "Movement and dodge", "Data/Player/PlayerMovementSettings");
        if (movement != null)
        {
            BeginDerived();
            Derived("Dodge", $"{movement.DodgeDuration:0.##} s, ready again after {movement.DodgeCooldown:0.##} s");
            foreach (DodgeStyle style in new[] { DodgeStyle.Quickstep, DodgeStyle.Roll, DodgeStyle.HeavyRoll })
            {
                DodgeShape shape = movement.ShapeOf(style);
                string load = style == DodgeStyle.Quickstep ? $"gear load up to {movement.LightLoad:0.#}"
                    : style == DodgeStyle.HeavyRoll ? $"gear load {movement.HeavyLoad:0.#} and up" : "in between";
                Derived(ObjectNames.NicifyVariableName(style.ToString()), $"{load}: {movement.DodgeDistance * shape.distance:0.#} m in " +
                    $"{movement.DodgeDuration * shape.duration:0.##} s, {shape.stamina:P0} stamina, moves at {shape.moveSpeed:P0}");
            }
            if (stamina != null)
            {
                Derived("Dodges from full stamina", stamina.DodgeCost > 0f ? $"{Mathf.FloorToInt(stamina.MaxStamina / stamina.DodgeCost)}" : "unlimited");
                Derived("Attacks from full stamina", stamina.AttackCost > 0f ? $"{Mathf.FloorToInt(stamina.MaxStamina / stamina.AttackCost)}" : "unlimited");
            }

            EndDerived();
        }

        SettingsPanel(stamina, "Stamina", "Data/Player/PlayerStaminaSettings");
        SettingsPanel(AssetDatabase.LoadAssetAtPath<CombatRules>("Assets/_Project/Data/Combat/CombatRules.asset"), "Combat rules", "Data/Combat/CombatRules");
        var technique = Settings<CombatTechniqueSettings>(CombatTechniqueSettings.ResourceName);
        SettingsPanel(technique, "Combat technique", "Resources/CombatTechniqueSettings: combo timing, heavy attack, guard and parry, flinch, poise and stagger");
        if (technique != null)
        {
            BeginDerived();
            Derived("Rhythm window", $"{(technique.RhythmEarly + technique.RhythmLate) * 1000f:0} ms around each swing's recovery");
            Derived("Parry window", $"{technique.ParryWindow * 1000f:0} ms after raising the guard; then {technique.GuardRelock:0.##} s before it can go up again");
            Derived("Best timed chain bonus", $"+{technique.MaxRhythm * technique.RhythmDamagePerStack:P0} damage, {(technique.RhythmSpeedPerStack > 0f ? $"+{technique.MaxRhythm * technique.RhythmSpeedPerStack:P0} speed" : "no speed-up")}");
            if (stamina != null && technique.BlockStaminaPerDamage > 0f)
            {
                Derived("Guard holds against", $"{stamina.MaxStamina / technique.BlockStaminaPerDamage:0} damage from full stamina, counted after the player's armor");
            }

            Derived("Swings", technique.DodgeCancelsWindup ? "can be dodged out of at any time" : "are commitments: no dodging until they land");
            Derived("Focus", $"a parry is worth {technique.FocusOnParry / Mathf.Max(0.01f, technique.FocusPerHit):0.#} hits, a perfect dodge " +
                $"{technique.FocusOnPerfectDodge / Mathf.Max(0.01f, technique.FocusPerHit):0.#}; it drains after {technique.FocusHoldSeconds:0} s without a gain");
            Derived("Eating", $"{technique.EatSeconds:0.#} s, interrupted by hits and dodges; healing arrives over {technique.HealOverSeconds:0} s");
            EndDerived();
        }

        SettingsPanel(Settings<CombatFeelSettings>(CombatFeelSettings.ResourceName), "Combat feel", "Resources/CombatFeel: hit stop, shake, flashes, effects, slash arcs, vignette");
        SettingsPanel(AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>("Assets/_Project/Data/Camera/PlayerCameraSettings.asset"), "Camera", "Data/Camera/PlayerCameraSettings");
    }
}

/// <summary>
/// The Raids page, in two tabs. Global Rules: what every raid shares (spawn distances, density, the creature limit,
/// sleeping), the portal and its sparks, and each destination's base rules at a glance. Raid Systems: pick a raid and
/// add, switch off or tune its optional systems (roaming spawns, ...). A raid's own content (spawn lists, time,
/// strength) is edited in the Content Editor.
/// </summary>
public sealed class RaidsAndPortalPage : SystemsPage
{
    private static readonly string[] Tabs = { "Global Rules", "Raid Systems" };
    private const string TabKey = "SafeHaven.SystemsEditor.RaidTab";
    private const string RaidKey = "SafeHaven.SystemsEditor.Raid";

    public override string Title => "Raids & Portal";
    public override string Group => "World";
    public override string Help => "Global Rules apply to every raid. Raid Systems adds optional systems to one raid. What a single raid " +
                                   "spawns, its time limit and its strength are edited in the Content Editor (Raids).";

    /// <summary>Opens this page on the Raid Systems tab with the raid selected.</summary>
    public static void ShowSystemsOf(RaidDefinition raid)
    {
        EditorPrefs.SetInt(TabKey, 1);
        EditorPrefs.SetString(RaidKey, AssetDatabase.GetAssetPath(raid));
        SystemsEditorWindow.ShowPage<RaidsAndPortalPage>();
    }

    public override void Draw()
    {
        int tab = Mathf.Clamp(EditorPrefs.GetInt(TabKey, 0), 0, Tabs.Length - 1);
        int picked = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(24f));
        if (picked != tab)
        {
            EditorPrefs.SetInt(TabKey, picked);
            GUI.FocusControl(null);
        }

        EditorGUILayout.Space(4f);
        if (picked == 0)
        {
            DrawGlobalRules();
        }
        else
        {
            DrawRaidSystems();
        }
    }

    private static void DrawGlobalRules()
    {
        SettingsPanel(Settings<RaidGlobalSettings>(RaidGlobalSettings.ResourceName), "Raid rules",
            "Resources/RaidGlobalSettings: apply to every raid. A raid can use its own near/far distances (Content Editor > Rules).");

        var settings = Settings<SettlementSettings>(SettlementSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Portal", "Resources/SettlementSettings", "portalRepairCost", "portalSpark", "secondsPerSpark", "maxSparksPerRaid");
        var raids = LoadAll<RaidDefinition>();
        Table("Destinations", "Each raid's base rules before modifiers and sparks, side by side. Edit everything else in the Content Editor.", raids,
            new[] { new Column("durationSeconds", "Seconds"), new Column("extractionSeconds", "Extract s"), new Column("lootMultiplier", "Loot ×"),
                    new Column("enemyHealthMultiplier", "Enemy HP ×"), new Column("enemyPowerMultiplier", "Enemy dmg ×"),
                    new Column("enemyDensity", "Density"), new Column("locked", "Locked", 50f) },
            raid => DescribeTimes(raid, settings), "With sparks");
        var modifiers = LoadAll<RaidModifierDefinition>();
        ContentUtility.BeginPanel("Raid modifiers", "Made from items in the Content Editor (Raid Modifiers).");
        foreach (RaidModifierDefinition modifier in modifiers)
        {
            EditorGUILayout.LabelField(modifier.DisplayName, modifier.DescribeEffects(), ContentUtility.WrappedMiniLabel);
        }

        ContentUtility.EndPanel();
        DrawSpawnTables();
        SettingsPanel(Settings<SceneTransitionSettings>(SceneTransitionSettings.ResourceName), "Loading screen",
            "Resources/SceneTransitionSettings: fades, minimum time, artwork and tips shown between scenes");
    }

    private static void DrawRaidSystems()
    {
        List<RaidDefinition> raids = LoadAll<RaidDefinition>();
        if (raids.Count == 0)
        {
            EditorGUILayout.HelpBox("There are no raids yet. Make one in the Content Editor.", MessageType.Info);
            return;
        }

        string savedPath = EditorPrefs.GetString(RaidKey, "");
        int index = Mathf.Max(0, raids.FindIndex(raid => AssetDatabase.GetAssetPath(raid) == savedPath));
        string[] names = raids.ConvertAll(raid => string.IsNullOrEmpty(raid.DisplayName) ? raid.name : raid.DisplayName).ToArray();
        int pickedRaid = EditorGUILayout.Popup("Raid", index, names);
        if (pickedRaid != index || savedPath == "")
        {
            EditorPrefs.SetString(RaidKey, AssetDatabase.GetAssetPath(raids[pickedRaid]));
        }

        RaidDefinition selected = raids[pickedRaid];
        var data = new SerializedObject(selected);
        data.Update();
        SerializedProperty systems = data.FindProperty("systems");

        EditorGUILayout.Space(4f);
        if (systems.arraySize == 0)
        {
            EditorGUILayout.HelpBox("This raid uses no extra systems. Add one below.", MessageType.Info);
        }

        for (int i = 0; i < systems.arraySize; i++)
        {
            SerializedProperty element = systems.GetArrayElementAtIndex(i);
            if (!(element.managedReferenceValue is RaidSystem system))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Missing system (its class was renamed or removed)");
                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    {
                        systems.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                continue;
            }

            ContentUtility.BeginPanel(system.DisplayName, system.Description);
            SerializedProperty end = element.GetEndProperty();
            SerializedProperty child = element.Copy();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(child, true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove System", GUILayout.Width(110f))
                    && EditorUtility.DisplayDialog("Remove raid system", $"Remove {system.DisplayName} from this raid? Its settings are lost.", "Remove", "Cancel"))
                {
                    systems.DeleteArrayElementAtIndex(i);
                    ContentUtility.EndPanel();
                    break;
                }
            }

            ContentUtility.EndPanel();
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("+ Add System", GUILayout.Height(24f)))
        {
            var menu = new GenericMenu();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<RaidSystem>())
            {
                if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                var sample = (RaidSystem)Activator.CreateInstance(type);
                bool present = selected.Systems.Any(existing => existing != null && existing.GetType() == type);
                if (present)
                {
                    menu.AddDisabledItem(new GUIContent(sample.DisplayName + " (already added)"));
                    continue;
                }

                menu.AddItem(new GUIContent(sample.DisplayName), false, () =>
                {
                    var target = new SerializedObject(selected);
                    SerializedProperty list = target.FindProperty("systems");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(type);
                    target.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        data.ApplyModifiedProperties();
    }

    // Raid layouts are rolled per raid from these tables by the Spawn Groups under "Raid Layout" in each raid scene.
    private static void DrawSpawnTables()
    {
        ContentUtility.BeginPanel("Scene spawn tables", "Used only by Spawn Groups in a raid scene that no raid spawn list names " +
                                                       "(spawn lists carry their own entries in the Content Editor).");
        foreach (SpawnTable table in LoadAll<SpawnTable>())
        {
            var builder = new StringBuilder();
            foreach (SpawnTable.Entry entry in table.Entries)
            {
                builder.Append(builder.Length > 0 ? ",  " : "").Append(entry.prefab != null ? entry.prefab.name : "(none)")
                    .Append($" {table.ChanceOf(entry):P0}");
                if (entry.minPerGroup > 0)
                {
                    builder.Append($" min {entry.minPerGroup}");
                }

                if (entry.maxPerGroup > 0)
                {
                    builder.Append($" max {entry.maxPerGroup}");
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(table.name, builder.ToString(), ContentUtility.WrappedMiniLabel);
            if (GUILayout.Button("Select", GUILayout.Width(60f)))
            {
                Selection.activeObject = table;
                EditorGUIUtility.PingObject(table);
            }

            EditorGUILayout.EndHorizontal();
        }

        ContentUtility.EndPanel();
    }

    private static string DescribeTimes(RaidDefinition raid, SettlementSettings settings)
    {
        var builder = new StringBuilder();
        for (int sparks = 0; sparks <= settings.MaxSparksPerRaid; sparks++)
        {
            float seconds = RaidParameters.Calculate(raid, null, sparks * settings.SecondsPerSpark).Duration;
            builder.Append(sparks > 0 ? "   " : "").Append($"{sparks}: {RaidStats.FormatTime(seconds)}");
        }

        return builder.ToString();
    }
}

/// <summary>The portal's ward, its upgrades and what buildings cost (GDD §6.4, §14).</summary>
public sealed class HomePortalAndBuildingPage : SystemsPage
{
    public override string Title => "Portal & Building";
    public override string Group => "World";
    public override string Help => "The ward is the area around the portal where the player may build; Purified Essence grows it. Building " +
                                   "costs are paid when a building is placed. Change costs and models in the Content Editor (Buildings).";

    public override void Draw()
    {
        var settings = Settings<SettlementSettings>(SettlementSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Ward", "Resources/SettlementSettings", "baseWardRadius", "wardLevels");
        BeginDerived("Ward levels");
        for (int level = 0; level <= settings.WardLevels.Count; level++)
        {
            float radius = settings.WardRadiusAt(level);
            string cost = level == 0 ? "start" : AmountText.Describe(settings.WardLevels[level - 1].cost);
            Derived(level == 0 ? "Base ward" : $"Level {level}", $"{radius:0} m radius, {Mathf.PI * radius * radius:0} m²   ({cost})");
        }

        EndDerived();
        var buildings = LoadAll<BuildingDefinition>();
        buildings.Sort(RecipeBook.CompareForDisplay);
        Table("Buildings", "Menu order, whether it is in the build menu and known from the start. Unknown buildings are taught by blueprints or jobs. " +
                           "Stations: Output is how much the output slot holds, Queue how many crafts can wait.", buildings,
            new[]
            {
                new Column("sortOrder", "Order", 50f), new Column("showInBuildMenu", "Menu", 45f), new Column("knownFromStart", "Known", 50f),
                new Column("station", "Station", 120f), new Column("outputCapacity", "Output", 50f), new Column("queueLength", "Queue", 50f),
            },
            building => AmountText.Describe(building.ConstructionCost), "Cost");
    }
}

/// <summary>Where things are crafted and how long stations take (GDD §13.3).</summary>
public sealed class CraftingPage : SystemsPage
{
    public override string Title => "Crafting";
    public override string Group => "World";
    public override string Help => "By Hand recipes are instant from the bag window; keep them few and basic. Every other recipe is queued at its " +
                                   "station, paid when queued, and takes Seconds per craft, also while the player is raiding. Output size and " +
                                   "queue length per station are on the portal & Building page. Change costs and results in the Content Editor (Recipes).";

    public override void Draw()
    {
        var recipes = LoadAll<RecipeDefinition>();
        recipes.Sort((a, b) => a.Station != b.Station ? a.Station.CompareTo(b.Station) : RecipeBook.CompareForDisplay(a, b));
        Table("Recipes", "Station, seconds per craft (ignored By Hand) and how many one craft makes.", recipes,
            new[] { new Column("station", "Station", 120f), new Column("craftSeconds", "Seconds", 60f), new Column("outputAmount", "Makes", 50f) },
            Summary, "Cost  ·  per minute at a station");

        BeginDerived("Stations");
        foreach (CraftingStationType type in (CraftingStationType[])System.Enum.GetValues(typeof(CraftingStationType)))
        {
            int count = recipes.FindAll(recipe => recipe.Station == type).Count;
            if (count > 0)
            {
                Derived(type.DisplayName(), $"{count} recipe{(count == 1 ? "" : "s")}");
            }
        }

        EndDerived();
    }

    private static string Summary(RecipeDefinition recipe)
    {
        string cost = AmountText.Describe(recipe.ResourceCost);
        string items = AmountText.Describe(recipe.ItemCost);
        cost = string.IsNullOrEmpty(items) ? cost : string.IsNullOrEmpty(cost) ? items : cost + ", " + items;
        return recipe.IsHandcrafted ? $"{cost}  ·  instant" : $"{cost}  ·  {60f / recipe.CraftSeconds * recipe.OutputAmount:0.#} {recipe.OutputName}/min";
    }
}

/// <summary>Residents, jobs and the Purification Altar (GDD §13.5, §15).</summary>
public sealed class ResidentsAndPurificationPage : SystemsPage
{
    public override string Title => "Residents & Purification";
    public override string Group => "World";
    public override string Help => "Rescued residents take jobs that teach buildings and run stations. The Purification Altar turns Cursed " +
                                   "Essence into Purified Essence while someone holds the purifier job.";

    public override void Draw()
    {
        var settings = Settings<SettlementSettings>(SettlementSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Purification", "Resources/SettlementSettings", "cursedEssence", "purifiedEssence", "cursedPerBatch",
            "purifiedPerBatch", "secondsPerBatch", "maxQueuedBatches", "purifierJob");
        BeginDerived();
        Derived("Ratio", $"{settings.CursedPerBatch} : {settings.PurifiedPerBatch}");
        Derived("10 Purified Essence take", $"{Mathf.CeilToInt(10f / settings.PurifiedPerBatch) * settings.CursedPerBatch} Cursed Essence and " +
                                            RaidStats.FormatTime(Mathf.CeilToInt(10f / settings.PurifiedPerBatch) * settings.SecondsPerBatch));
        EndDerived();
        foreach (ResidentDefinition resident in LoadAll<ResidentDefinition>())
        {
            SettingsPanel(resident, "Resident: " + resident.DisplayName, null);
        }

        foreach (JobDefinition job in LoadAll<JobDefinition>())
        {
            SettingsPanel(job, "Job: " + job.DisplayName, null);
        }
    }
}

/// <summary>What Essence buys at the altar: empowering gear, enchantments and skill ranks (GDD §13.5, §16.4).</summary>
public sealed class EssencePage : SystemsPage
{
    public override string Title => "Essence";
    public override string Group => "World";
    public override string Help => "Every upgrade at the altar is deterministic: it costs what it says and never fails. Purified Essence is the safe " +
                                   "way; Cursed Essence is used raw, for enchantments that state their price as a negative bonus.";

    public override void Draw()
    {
        var settings = Settings<EssenceSettings>(EssenceSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Empowering gear", "Resources/EssenceSettings", "empowerLevels", "purifiedPerTier", "statShare", "weaponDamage", "toolYield", "toolCare");
        SettingsPanel(settings, "Skills", null, "skillRanks");
        SettingsPanel(settings, "Enchantments", null, "enchantments");
        int empower = 0;
        foreach (EssenceSettings.EmpowerLevel level in settings.EmpowerLevels)
        {
            empower += level.purified;
        }

        int skill = 0;
        foreach (EssenceSettings.SkillRank rank in settings.SkillRanks)
        {
            skill += rank.purified;
        }

        BeginDerived();
        Derived("One tier I item to the top", $"{empower} Purified Essence");
        Derived("One skill to the top", $"{skill} Purified Essence");
        Derived("Weapon at the top", $"+{settings.WeaponDamage * settings.EmpowerLevels.Count * 100f:0}% damage");
        Derived("Enchantments", settings.Enchantments.Count.ToString());
        EndDerived();
    }
}

/// <summary>Gear no one at home can make, found only in raids (GDD §13.6).</summary>
public sealed class RelicsPage : SystemsPage
{
    public override string Title => "Relics";
    public override string Group => "World";
    public override string Help => "A relic is any listed gear with any power that fits it. A power is an enchantment of the Relic school, and what it does " +
                                   "beyond numbers is its Effects. The altar can neither make nor replace one.";

    public override void Draw()
    {
        var settings = Settings<RelicSettings>(RelicSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Finding them", "Resources/RelicSettings", "cacheChance", "tiersAboveRaid", "glow");
        SettingsPanel(settings, "Powers and gear", null, "powers", "gear");
        BeginDerived();
        Derived("Powers", settings.Powers.Count.ToString());
        Derived("Gear they can turn up on", settings.Gear.Count.ToString());
        EndDerived();
    }
}

/// <summary>Carts, barrows and boats: what they carry and what that costs in speed (GDD §8.5).</summary>
public sealed class TransportPage : SystemsPage
{
    public override string Title => "Transport";
    public override string Group => "World";
    public override string Help => "Transport is moved by physics. Cargo weight is real mass and also sets the player's speed: a little slower empty, " +
                                   "more when full, and steeply when overfilled past the rated capacity.";

    public override void Draw()
    {
        var settings = Settings<TransportSettings>(TransportSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "All transport", "Resources/TransportSettings", "extractionRange", "rumbleRadius", "rumbleWhenFull", "rumbleSeconds", "strainedSpeed", "stuckSeconds");
        foreach (TransportDefinition definition in LoadAll<TransportDefinition>())
        {
            SettingsPanel(definition, definition.DisplayName, null);
            BeginDerived();
            Derived("Slower when empty", $"{(1f - definition.SpeedScale(0f)) * 100f:0}%");
            Derived("Slower at capacity", $"{(1f - definition.SpeedScale(definition.Capacity)) * 100f:0}%");
            Derived("Slower at the limit", $"{(1f - definition.SpeedScale(definition.OverloadLimit)) * 100f:0}%");
            Derived("Mass at capacity", $"{definition.EmptyMass + definition.Capacity * definition.MassPerWeight:0} kg");
            EndDerived();
        }
    }
}

/// <summary>Playtest run logging and the first-hours milestones runs are compared on (GDD §21).</summary>
public sealed class PlaytestPage : SystemsPage
{
    public override string Title => "Playtest Log";
    public override string Group => "Settings";
    public override string Help => "Every new game is a run with its own CSV log; loading a save continues that run's log as a new session. " +
                                   "Milestones are the moments compared across runs in the Run Comparison window.";

    public override void Draw()
    {
        var settings = Settings<PlaytestSettings>(PlaytestSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Run log", "Resources/PlaytestSettings");
        var known = new HashSet<string>(PlaytestLog.KnownEvents(), StringComparer.OrdinalIgnoreCase);
        BeginDerived();
        Derived("Logs saved to", PlaytestLog.Folder);
        foreach (PlaytestMilestone milestone in settings.Milestones)
        {
            if (!known.Contains(milestone.eventName ?? string.Empty))
            {
                Derived("Unknown event", $"'{milestone.label}' waits for '{milestone.eventName}', which is never logged");
            }
        }

        EndDerived();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Run Comparison"))
            {
                PlaytestRunsWindow.Open();
            }

            if (GUILayout.Button("Open Folder"))
            {
                System.IO.Directory.CreateDirectory(PlaytestLog.Folder);
                EditorUtility.RevealInFinder(PlaytestLog.Folder + System.IO.Path.DirectorySeparatorChar);
            }
        }

        EditorGUILayout.HelpBox("Known events: " + string.Join(", ", PlaytestLog.KnownEvents()), MessageType.None);
    }
}

/// <summary>Ward breaches (GDD §6.6–6.9).</summary>
public sealed class WardBreachPage : SystemsPage
{
    public override string Title => "Ward Breaches";
    public override string Group => "World";
    public override string Help => "Announced defenses of Safe Haven. The first is gentle: enemies come along the marked path and attack only the player, and falling ends it. " +
                                   "Later ones come in waves down several lanes for the buildings and the ward; buildings break but are never lost, and a fallen ward strains for a while.";

    public override void Draw()
    {
        SettingsPanel(Settings<WardBreachSettings>(WardBreachSettings.ResourceName), "All breaches", "Resources/WardBreachSettings");
        List<WardBreachDefinition> breaches = LoadAll<WardBreachDefinition>();
        if (breaches.Count == 0)
        {
            EditorGUILayout.HelpBox("No ward breaches yet. Create one with Create > Safe Haven > Settlement > Ward Breach.", MessageType.Info);
        }

        foreach (WardBreachDefinition breach in breaches)
        {
            SettingsPanel(breach, breach.DisplayName, AssetDatabase.GetAssetPath(breach));
            BeginDerived();
            float health = EnemyHealth(breach.EnemyPrefab);
            Derived("Each enemy's health", health > 0f ? $"{health * breach.EnemyHealthMultiplier:0} (normally {health:0})" : "unknown");
            Derived("All enemies have arrived after", $"{(breach.EnemyCount - 1) * breach.SecondsBetweenEnemies:0.#} s");
            Derived("Offered after", $"{breach.AvailableAfterExtractions} successful extraction(s)");
            EndDerived();
        }
    }

    private static float EnemyHealth(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent(out Combatant combatant))
        {
            return 0f;
        }

        var stats = new SerializedObject(combatant).FindProperty("baseStats").objectReferenceValue as CombatStatsDefinition;
        return stats != null ? stats.GetValue(StatType.MaxHealth) : 0f;
    }
}

/// <summary>How gear adds up to a gear score, and what a full kit of each tier comes to.</summary>
public sealed class GearScorePage : SystemsPage
{
    public override string Title => "Gear Score";
    public override string Group => "Settings";
    public override string Help => "How gear adds up to a gear score, the number raid tiers are tuned to. The tiers themselves are in the Content Editor (Raids › Rules).";

    public override void Draw()
    {
        GearScoreSettings settings = Settings<GearScoreSettings>(GearScoreSettings.ResourceName);
        SettingsPanel(settings, "Gear score", "Resources/GearScoreSettings");
        if (settings != null && !Mathf.Approximately(settings.TotalShare, 1f))
        {
            EditorGUILayout.HelpBox($"The slots' shares add up to {settings.TotalShare:0.##}, not 1, so a full kit won't come to Points Per Tier x tier.", MessageType.Warning);
        }

        var gear = GearBalance.AllGear();
        ContentUtility.BeginPanel("A full kit of each tier", "The best of each tier in every slot, as the game's assets stand now. If a higher tier is not clearly stronger than the one below, the gear needs tuning.");
        for (int tier = 1; tier <= Tiers.Max; tier++)
        {
            GearBalance.Kit kit = GearBalance.KitOf(tier, gear);
            EditorGUILayout.LabelField($"Tier {Tiers.ToRoman(tier)}", $"score {kit.Score}, damage {kit.Damage:0.#}, armor {kit.Armor:0} ({kit.Reduction:P0}), health {kit.Health:0}, toughness {kit.Toughness:0}");
        }

        ContentUtility.EndPanel();
    }
}

/// <summary>Rarity colors, names and pickup sounds.</summary>
public sealed class LootAndRarityPage : SystemsPage
{
    public override string Title => "Loot & Rarity";
    public override string Group => "Settings";
    public override string Help => "How rare finds look and sound. Each item's rarity and value are set in the Content Editor (Items).";

    public override void Draw()
    {
        SettingsPanel(Settings<RaritySettingsDefinition>("RaritySettings"), "Rarity", "Resources/RaritySettings");
        SettingsPanel(Settings<DroppedItemSettings>(DroppedItemSettings.ResourceName), "Dropped items", "Resources/DroppedItemSettings");
        SettingsPanel(Settings<NightSettings>(NightSettings.ResourceName), "Night", "Resources/NightSettings");
        SettingsPanel(Settings<MinimapSettings>(MinimapSettings.ResourceName), "Minimap", "Resources/MinimapSettings");
    }
}

/// <summary>Co-op: session size, how often friends hear where you are, and how smoothly they are drawn.</summary>
public sealed class MultiplayerPage : SystemsPage
{
    public override string Title => "Co-op";
    public override string Group => "Settings";
    public override string Help => "Up to Max Players play together; one hosts. Each player sends where they are a few times a second and " +
                                   "friends are drawn slightly in the past so they move smoothly. Raise Protocol Version whenever what " +
                                   "is sent changes, so old and new builds don't join each other.";

    public override void Draw()
    {
        var settings = Settings<MultiplayerSettings>(MultiplayerSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Co-op", "Resources/MultiplayerSettings");
        BeginDerived();
        Derived("Pose messages per friend", $"{1f / settings.PoseInterval:0} per second");
        Derived("Friends are drawn", $"{settings.InterpolationDelay * 1000f:0} ms behind (plus the connection's own delay)");
        Derived("Upload while hosting", $"about {(settings.MaxPlayers - 1) * (1f / settings.PoseInterval) * 0.3f:0.0} KB/s of poses");
        EndDerived();
    }
}

/// <summary>Controller: the pointer in windows, rumble, the wheels' stick and placing buildings with the sticks.</summary>
public sealed class ControllerPage : SystemsPage
{
    public override string Title => "Controller";
    public override string Group => "Settings";
    public override string Help => "How the game feels on a controller. The buttons are in the Input Actions asset's Gamepad scheme; this is " +
                                   "the pointer the left stick moves in windows, how hard the pad rumbles (camera shake becomes rumble), and " +
                                   "the spot the right stick moves when placing a building top-down.";

    public override void Draw()
    {
        var settings = Settings<ControllerSettings>(ControllerSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Controller", "Resources/ControllerSettings");
        BeginDerived();
        Derived("Pointer across a 1080p screen", $"{1920f / settings.CursorSpeed:0.0} s, the stick fully over");
        Derived("A full camera shake", $"rumbles at {Mathf.Min(settings.RumbleMax, settings.RumblePerShake):P0}, gone in {1f / settings.RumbleFade:0.00} s");
        Derived("Building spot", $"starts {settings.BuildStartDistance:0.#} m ahead, reaches {settings.BuildReach:0.#} m");
        EndDerived();
    }
}
