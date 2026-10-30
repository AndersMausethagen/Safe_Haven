using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Raid events, meals and tonics, status effects, shields, summons and the content built with them.</summary>
public sealed class ContentExpansionTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void CleanUp()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private T Own<T>(T thing) where T : Object
    {
        made.Add(thing);
        return thing;
    }

    private static IEnumerable<T> All<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private RaidEventDefinition Event(float weight = 1f, float earliest = 0f, float latestLeft = 0f, int stage = 0)
    {
        var definition = Own(ScriptableObject.CreateInstance<RaidEventDefinition>());
        definition.ConfigureTiming(weight, earliest, latestLeft, stage);
        return definition;
    }

    private static RaidSession Session(float seconds = 300f)
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var parameters = RaidParameters.Calculate(raid, new RaidModifierDefinition[0]);
        var session = new RaidSession(raid, new RaidModifierDefinition[0], parameters, null, "SafeHaven", 0, 7);
        session.AddTime(seconds - parameters.Duration);
        return session;
    }

    // ----- Raid events -----

    [Test]
    public void AnEventHappensOnlyInsideItsWindowAndStage()
    {
        RaidEventDefinition definition = Event(1f, 60f, 90f, 1);
        Assert.IsFalse(definition.CanHappen(30f, 300f, 1), "too early");
        Assert.IsFalse(definition.CanHappen(100f, 60f, 1), "too late to reach");
        Assert.IsFalse(definition.CanHappen(100f, 300f, 0), "too calm");
        Assert.IsTrue(definition.CanHappen(100f, 300f, 1));
    }

    [Test]
    public void ThePickerNeverRepeatsAnEventInARaid()
    {
        RaidEventDefinition first = Event();
        RaidEventDefinition second = Event();
        var pool = new[] { first, second };
        var used = new HashSet<RaidEventDefinition> { first };
        var random = new System.Random(3);
        for (int i = 0; i < 50; i++)
        {
            Assert.AreSame(second, RaidEventPicker.Pick(pool, random, 100f, 300f, 0, used));
        }

        used.Add(second);
        Assert.IsNull(RaidEventPicker.Pick(pool, random, 100f, 300f, 0, used), "every event has happened");
    }

    [Test]
    public void TheSameSeedPicksTheSameEvents()
    {
        var pool = new[] { Event(), Event(), Event(), Event() };
        var one = new System.Random(11);
        var two = new System.Random(11);
        var none = new HashSet<RaidEventDefinition>();
        for (int i = 0; i < 20; i++)
        {
            Assert.AreSame(RaidEventPicker.Pick(pool, one, 100f, 300f, 0, none), RaidEventPicker.Pick(pool, two, 100f, 300f, 0, none));
        }
    }

    [Test]
    public void TimeEventsMoveTheClockButNeverEndTheRaid()
    {
        RaidSession session = Session(300f);
        session.Tick(100f);
        Assert.AreEqual(200f, session.TimeRemaining, 0.01f);
        session.AddTime(60f);
        Assert.AreEqual(260f, session.TimeRemaining, 0.01f);
        session.AddTime(-1000f);
        Assert.Greater(session.TimeRemaining, 0f, "a price in time can't end the raid on the spot");
        Assert.IsFalse(session.IsOver);
    }

    [Test]
    public void TheReportNamesAnEventOnceWithTheChoiceMade()
    {
        RaidSession session = Session();
        session.RecordEvent("The Toll Troll");
        session.RecordEvent("Old Beacon");
        session.RecordEvent("The Toll Troll: Pay the toll", "The Toll Troll");
        CollectionAssert.AreEqual(new[] { "The Toll Troll: Pay the toll", "Old Beacon" }, session.EventLog);
    }

    [Test]
    public void AChanceTakesOneBranchByItsRoll()
    {
        RaidSession session = Session(300f);
        var context = new RaidEventContext { Session = session, Random = new System.Random(1) };
        new ChanceOutcome(1f, new List<RaidEventOutcome> { new ChangeTimeOutcome(60f) }, new List<RaidEventOutcome> { new ChangeTimeOutcome(-60f) }).Apply(context);
        Assert.AreEqual(360f, session.TimeRemaining, 0.01f);
        new ChanceOutcome(0f, new List<RaidEventOutcome> { new ChangeTimeOutcome(60f) }, new List<RaidEventOutcome> { new ChangeTimeOutcome(-60f) }).Apply(context);
        Assert.AreEqual(300f, session.TimeRemaining, 0.01f);
    }

    [Test]
    public void EveryEventIsCompleteAndInTheRaidsPool()
    {
        List<RaidEventDefinition> events = All<RaidEventDefinition>().ToList();
        Assert.GreaterOrEqual(events.Count, 8);
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        RaidEventsSystem system = raid.Systems.OfType<RaidEventsSystem>().FirstOrDefault();
        Assert.IsNotNull(system, "Misty Woods has the Raid Events system");
        foreach (RaidEventDefinition definition in events)
        {
            Assert.IsNotEmpty(definition.Warning, definition.name);
            Assert.Contains(definition, system.Settings.pool);
            Assert.IsTrue(definition.SitePrefab != null || definition.OnStart.Count > 0, definition.name + " does something");
            if (definition.SitePrefab != null)
            {
                Assert.IsNotNull(definition.SitePrefab.GetComponent<RaidEventSite>(), definition.name);
                Assert.AreEqual(definition.Choices.Count > 0, definition.SitePrefab.GetComponent<Interactable>() != null, definition.name + ": choices need an Interactable");
            }

            foreach (RaidEventChoice choice in definition.Choices)
            {
                Assert.IsNotEmpty(choice.label, definition.name);
                Assert.IsFalse(choice.outcomes.Contains(null), $"{definition.name}: {choice.label} has an empty outcome");
                Assert.IsFalse(choice.cost.Any(cost => cost.Item == null), $"{definition.name}: {choice.label} costs nothing known");
            }
        }
    }

    // ----- Boons -----

    private PlayerBoons Boons(out StatSheet stats)
    {
        var user = Own(new GameObject("Eater"));
        var combatant = user.AddComponent<Combatant>();
        stats = combatant.Stats;
        return user.AddComponent<PlayerBoons>();
    }

    private static readonly StatBonus[] Strong = { new StatBonus(StatType.Strength, 3f) };

    [Test]
    public void AMealAddsItsBonusesAndATonicTakesNoSlot()
    {
        PlayerBoons boons = Boons(out StatSheet stats);
        float before = stats.Get(StatType.Strength);
        Assert.IsTrue(boons.Add("stew", "Stew", Strong, 60f, true));
        Assert.AreEqual(before + 3f, stats.Get(StatType.Strength), 0.01f);
        boons.Add("bread", "Bread", Strong, 60f, true);
        boons.Add("tonic", "Tonic", Strong, 60f, false);
        Assert.AreEqual(3, boons.Count);
        Assert.AreEqual(before + 9f, stats.Get(StatType.Strength), 0.01f);
    }

    [Test]
    public void AThirdMealPushesOutTheOneClosestToEnding()
    {
        PlayerBoons boons = Boons(out StatSheet stats);
        Assert.AreEqual(2, SurvivalSettings.Current.MealSlots);
        boons.Add("long", "Long", Strong, 300f, true);
        boons.Add("short", "Short", Strong, 30f, true);
        boons.Add("new", "New", Strong, 120f, true);
        List<BoonRecord> left = boons.Capture();
        CollectionAssert.AreEquivalent(new[] { "long", "new" }, left.Select(boon => boon.Id));
    }

    [Test]
    public void EatingTheSameMealAgainRestartsIt()
    {
        PlayerBoons boons = Boons(out StatSheet stats);
        float before = stats.Get(StatType.Strength);
        boons.Add("stew", "Stew", Strong, 30f, true);
        boons.Add("stew", "Stew", Strong, 90f, true);
        Assert.AreEqual(1, boons.Count);
        Assert.AreEqual(90f, boons.Capture()[0].SecondsLeft, 0.01f);
        Assert.AreEqual(before + 3f, stats.Get(StatType.Strength), 0.01f, "the bonus doesn't stack");
    }

    [Test]
    public void BoonsGoAcrossAScene()
    {
        PlayerBoons from = Boons(out _);
        from.Add("stew", "Stew", Strong, 30f, true);
        PlayerBoons to = Boons(out StatSheet stats);
        float before = stats.Get(StatType.Strength);
        to.Restore(from.Capture());
        Assert.AreEqual(1, to.Count);
        Assert.AreEqual(before + 3f, stats.Get(StatType.Strength), 0.01f);
    }

    [Test]
    public void EveryCookedMealHasBonusesWorthTheKitchen()
    {
        foreach (RecipeDefinition recipe in All<RecipeDefinition>().Where(found => found.Station == CraftingStationType.Kitchen))
        {
            Assert.IsNotNull(recipe.Output, recipe.name);
            Assert.IsTrue(recipe.Output.HasBoon, recipe.name);
            Assert.IsFalse(recipe.Output.IsTonic, recipe.name + " takes a meal slot");
        }
    }

    // ----- Status effects -----

    private StatusEffectDefinition Effect()
    {
        var effect = Own(ScriptableObject.CreateInstance<StatusEffectDefinition>());
        return effect;
    }

    private Damageable Victim()
    {
        var body = Own(new GameObject("Victim"));
        body.AddComponent<Combatant>();
        return body.AddComponent<Damageable>();
    }

    [Test]
    public void AnEffectIsAppliedOnceAndCuredByName()
    {
        Damageable victim = Victim();
        StatusEffectDefinition poison = Effect();
        StatusEffectDefinition chill = Effect();
        StatusEffects effects = StatusEffects.Apply(victim, poison, null);
        StatusEffects.Apply(victim, poison, null);
        StatusEffects.Apply(victim, chill, null);
        Assert.AreEqual(2, effects.Current.Count, "the same effect restarts rather than stacks");
        Assert.AreEqual(1, effects.Cure(new[] { poison }));
        Assert.IsFalse(effects.Has(poison));
        Assert.IsTrue(effects.Has(chill));
    }

    [Test]
    public void ATickOfPoisonIsNoPerfectDodge()
    {
        Damageable victim = Victim();
        bool evaded = false;
        victim.Evaded += _ => evaded = true;
        victim.AddInvulnerability(this);
        victim.ApplyDamage(new DamageInfo(null, null, 1f, DamageType.Holy, false, Vector3.zero, Vector3.up, 0f, HitTechnique.OverTime));
        Assert.IsFalse(evaded);
        victim.ApplyDamage(new DamageInfo(null, null, 1f, DamageType.Physical, false, Vector3.zero, Vector3.up));
        Assert.IsTrue(evaded, "a blow still is");
    }

    [Test]
    public void ABlockedHitLeavesNoStatus()
    {
        Damageable victim = Victim();
        var attack = Own(ScriptableObject.CreateInstance<AttackDefinition>());
        var so = new SerializedObject(attack);
        so.FindProperty("appliesStatus").objectReferenceValue = Effect();
        so.ApplyModifiedPropertiesWithoutUndo();
        var blocked = new DamageResult(10f, 2f, 2f, false, false, DamageType.Physical, GuardOutcome.Blocked);
        StatusEffects.AfterHit(attack, victim, null, blocked);
        Assert.IsNull(victim.GetComponent<StatusEffects>());
        var hit = new DamageResult(10f, 8f, 8f, false, false, DamageType.Physical);
        StatusEffects.AfterHit(attack, victim, null, hit);
        Assert.IsNotNull(victim.GetComponent<StatusEffects>());
    }

    // ----- Shields -----

    [Test]
    public void AShieldGoesInTheOffHandOnly()
    {
        ItemDefinition shield = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Weapons/RoundShield.asset");
        Assert.IsNotNull(shield);
        Assert.IsTrue(shield.Weapon.IsShield);
        Assert.IsTrue(GearSlots.Accepts(GearSlot.OffHand, shield));
        Assert.IsFalse(GearSlots.Accepts(GearSlot.MainHand, shield));
        Assert.Less(shield.Weapon.GuardLetThrough, 0.3f, "a shield blocks better than a weapon");
    }

    // ----- The content itself -----

    [Test]
    public void EveryCreatureIsSetUp()
    {
        foreach (string file in new[] { "MarshLight", "Draug", "BogSpitter", "BlightBoar", "Rootling", "MossShaman" })
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{file}.asset");
            Assert.IsNotNull(monster, file);
            Assert.IsNotNull(monster.Prefab, file);
            CollectionAssert.IsEmpty(MonsterPrefabBuilder.FindProblems(monster.Prefab, monster), file);
            Assert.IsNotEmpty(monster.Attacks, file);
            foreach (MonsterAttack action in monster.Attacks)
            {
                Assert.IsNotNull(action.attack, file);
                Assert.IsTrue(action.kind != MonsterAttackKind.Projectile || action.projectilePrefab != null, file + " needs a projectile");
                Assert.IsTrue(action.kind != MonsterAttackKind.Summon || action.summonPrefab != null, file + " needs a summon");
            }
        }
    }

    [Test]
    public void EveryNewBuildingIsSetUpAndOnSale()
    {
        foreach (string file in new[] { "Kitchen", "TanningRack", "FarmPlot", "Beehive", "Well", "Sauna", "WatchPost", "SpikeTrap", "Cottage" })
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{file}.asset");
            Assert.IsNotNull(building, file);
            Assert.IsNotNull(building.Prefab, file);
            Assert.IsTrue(building.ShowInBuildMenu, file);
            Assert.IsNotNull(building.Icon, file);
            List<string> problems = BuildingPrefabBuilder.FindProblems(building.Prefab, building);
            // The trap has none on purpose: creatures must be able to walk onto it.
            CollectionAssert.IsEmpty(problems.Where(problem => file != "SpikeTrap" || !problem.Contains("Nav Mesh Obstacle")), file);
            Assert.IsNotNull(building.Prefab.GetComponent<Construction>(), file);
        }
    }

    [Test]
    public void EveryStationHasABuildingAndEveryRecipeAStation()
    {
        var stations = new HashSet<CraftingStationType>(All<BuildingDefinition>().Select(building => building.Station));
        foreach (RecipeDefinition recipe in All<RecipeDefinition>())
        {
            if (recipe.Station != CraftingStationType.None && recipe.Station != CraftingStationType.Portal)
            {
                Assert.IsTrue(stations.Contains(recipe.Station), $"{recipe.name}: nothing builds {recipe.Station}");
            }
        }
    }

    [Test]
    public void EveryIngredientCanBeGot()
    {
        var sources = new HashSet<ItemDefinition>();
        foreach (GatheringNodeDefinition node in AssetDatabase.FindAssets("t:GatheringNodeDefinition").Select(guid =>
                     AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid))))
        {
            sources.UnionWith(node.Yields.Entries.Select(entry => entry.Item).Where(item => item != null));
        }

        foreach (MonsterDefinition monster in All<MonsterDefinition>())
        {
            sources.UnionWith(monster.Drops.Entries.Select(entry => entry.Item).Where(item => item != null));
        }

        foreach (RecipeDefinition recipe in All<RecipeDefinition>())
        {
            if (recipe.Output != null)
            {
                sources.Add(recipe.Output);
            }
        }

        foreach (string name in new[] { "CuredLeather", "BoarTusk", "BogVenom", "WispEmber", "GraveIron", "RootlingBark", "Chanterelle", "WildTurnip", "Barley", "Cloudberries", "Honey", "BoarMeat" })
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/Materials/{name}.asset");
            Assert.IsNotNull(item, name);
            Assert.IsTrue(sources.Contains(item), name + " comes from nothing");
        }
    }

    [Test]
    public void EveryNewThingHasAnIconAndAUniqueId()
    {
        List<ItemDefinition> items = All<ItemDefinition>().ToList();
        foreach (ItemDefinition item in items.Where(found => AssetDatabase.GetAssetPath(found).Contains("/Armor/")
                     || AssetDatabase.GetAssetPath(found).Contains("/Jewelry/") || AssetDatabase.GetAssetPath(found).Contains("/Weapons/") || AssetDatabase.GetAssetPath(found).Contains("/Food/")))
        {
            Assert.IsNotNull(item.Icon, item.name);
        }

        var duplicates = items.GroupBy(item => item.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        CollectionAssert.IsEmpty(duplicates);
        var recipes = All<RecipeDefinition>().GroupBy(recipe => recipe.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        CollectionAssert.IsEmpty(recipes);
    }

    [Test]
    public void HeavyArmourMakesAHeavyDodgeAndLeatherStaysLight()
    {
        float Load(params string[] files) => files.Sum(file => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/Armor/{file}.asset").Weight);
        Assert.Less(Load("CuredLeatherCap", "CuredLeatherJack", "CuredLeatherGloves", "CuredLeatherBoots", "CuredLeatherBelt"), 6f);
        Assert.Greater(Load("IronMailHelm", "IronMailHauberk", "IronMailGauntlets", "IronMailBoots", "IronMailBelt"), 13f);
    }
}
