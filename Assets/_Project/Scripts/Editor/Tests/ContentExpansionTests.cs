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
        // A rumour's event has weight 0: it is placed by the rumour, not drawn (RaidReasonsTests covers those).
        List<RaidEventDefinition> events = All<RaidEventDefinition>().Where(definition => definition.Weight > 0f).ToList();
        Assert.GreaterOrEqual(events.Count, 8);
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        RaidEventsSystem system = raid.Systems.OfType<RaidEventsSystem>().FirstOrDefault();
        Assert.IsNotNull(system, "Misty Woods has the Raid Events system");
        // A raid's own event (Troll Valley's Jaw-keeper, Act II) is drawn from its pool only: every event is in some raid's.
        var pooled = All<RaidDefinition>().SelectMany(r => r.Systems.OfType<RaidEventsSystem>()).SelectMany(s => s.Settings.pool).ToList();
        foreach (RaidEventDefinition definition in events)
        {
            Assert.IsNotEmpty(definition.Warning, definition.name);
            Assert.Contains(definition, pooled, definition.name + " is in a raid's pool");
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
    // One of each family works at a time (BoonFamily, 2026-11-22): the slot tests use boons of different families.
    private static readonly StatBonus[] Hearty = { new StatBonus(StatType.MaxHealth, 10f) };
    private static readonly StatBonus[] Swift = { new StatBonus(StatType.MaxStamina, 10f) };
    private static readonly StatBonus[] Warding = { new StatBonus(StatType.FireResistance, 0.1f) };

    [Test]
    public void AMealAddsItsBonusesAndATonicTakesNoSlot()
    {
        PlayerBoons boons = Boons(out StatSheet stats);
        float before = stats.Get(StatType.Strength);
        Assert.IsTrue(boons.Add("stew", "Stew", Strong, 60f, true));
        Assert.AreEqual(before + 3f, stats.Get(StatType.Strength), 0.01f);
        boons.Add("bread", "Bread", Hearty, 60f, true);
        boons.Add("tonic", "Tonic", Swift, 60f, false, true);
        Assert.AreEqual(3, boons.Count, "two meals in two slots, and a tonic in its own");
        Assert.AreEqual(before + 3f, stats.Get(StatType.Strength), 0.01f);
    }

    [Test]
    public void AThirdMealPushesOutTheOneClosestToEnding()
    {
        PlayerBoons boons = Boons(out StatSheet stats);
        Assert.AreEqual(2, SurvivalSettings.Current.MealSlots);
        boons.Add("long", "Long", Hearty, 300f, true);
        boons.Add("short", "Short", Swift, 30f, true);
        boons.Add("new", "New", Warding, 120f, true);
        List<BoonRecord> left = boons.Capture();
        CollectionAssert.AreEquivalent(new[] { "long", "new" }, left.Select(boon => boon.Id));
    }

    // The user, 2026-11-22: "one of each boon". A second boon of a family takes the first one's place, meal or tonic, slots or not.
    [Test]
    public void OneBoonOfEachFamily_AMealOrATonic()
    {
        PlayerBoons boons = Boons(out StatSheet stats);
        float before = stats.Get(StatType.Strength);
        boons.Add("roast", "Roast", Strong, 300f, true);
        boons.Add("stew", "Stew", Hearty, 300f, true);
        Assert.AreEqual(2, boons.Count);
        boons.Add("cursed", "Cursed", Strong, 60f, false, true);
        CollectionAssert.AreEquivalent(new[] { "stew", "cursed" }, boons.Capture().Select(boon => boon.Id), "the fierce tonic takes the fierce meal's place");
        Assert.AreEqual(before + 3f, stats.Get(StatType.Strength), 0.01f, "one fierce boon's strength, not two");
        Assert.AreEqual("Fierce", BoonFamily.Of(Strong));
        Assert.AreEqual("Hearty", BoonFamily.Of(Hearty));
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
        // Ingredients ground at the Kitchen (Flour, 2026-11-22) are no meal.
        foreach (RecipeDefinition recipe in All<RecipeDefinition>().Where(found => found.Station == CraftingStationType.Kitchen
                     && (found.Output == null || found.Output.Category != ItemCategory.Material)))
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

    // The creatures MonsterPrefabBuilder made, whose prefabs its FindProblems describes.
    private static readonly System.Collections.Generic.HashSet<string> BuiltByTheMonsterBuilder =
        new System.Collections.Generic.HashSet<string> { "MarshLight", "Draug", "BogSpitter", "BlightBoar", "Rootling", "MossShaman" };

    // Every creature with a body, not a list of the ones added at one time: a prefab nobody looked at again is the one that breaks.
    [Test]
    public void EveryCreatureIsSetUp()
    {
        var problems = new List<string>();
        int creatures = 0;
        foreach (MonsterDefinition monster in All<MonsterDefinition>().Where(found => found != null && found.Prefab != null))
        {
            creatures++;
            // The builder's own check, on the creatures it built: on every creature it told what play never reads (a prefab's agent speed,
            // which MonsterMovement sets from the data; an animator on the spider, which walks on IK legs).
            if (BuiltByTheMonsterBuilder.Contains(monster.name))
            {
                problems.AddRange(MonsterPrefabBuilder.FindProblems(monster.Prefab, monster).Select(problem => $"{monster.name}: {problem}"));
            }
            // Something that hunts needs something to hit with: its list of attacks, or the single attack older creatures keep.
            if (monster.Faction == Faction.Enemy && !monster.Timid && monster.Attack == null && monster.Attacks.Count == 0)
            {
                problems.Add($"{monster.name} hunts with nothing to strike");
            }

            foreach (MonsterAttack action in monster.Attacks)
            {
                if (action.attack == null)
                {
                    problems.Add($"{monster.name} has an attack without its definition");
                }

                if (action.kind == MonsterAttackKind.Projectile && action.projectilePrefab == null)
                {
                    problems.Add($"{monster.name} needs a projectile");
                }

                if (action.kind == MonsterAttackKind.Summon && action.summonPrefab == null)
                {
                    problems.Add($"{monster.name} needs a summon");
                }
            }
        }

        Assert.Greater(creatures, 10, "the creatures were found");
        Assert.IsEmpty(problems, string.Join("\n", problems));
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

    // Docs/PlayerStatsResearch.md #6: light means quickstep and sneaking, heavy means barging and armour. The archer's leather stays
    // a quickstep with any bow; Copper Scale with a sword is a roll, and a shield on top makes it heavy.
    [Test]
    public void LeatherWithAnyBowIsAQuickstep_AndCopperScaleWithASwordIsARoll()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/_Project/Data/Player/PlayerMovementSettings.asset");
        ItemDefinition Item(string folder, string file) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/{folder}/{file}.asset");
        float Armour(params string[] files) => files.Sum(file => PlayerEquipment.LoadOf(Item("Armor", file), GearSlot.Chest));
        float leather = Armour("CuredLeatherCap", "CuredLeatherJack", "CuredLeatherGloves", "CuredLeatherBoots", "CuredLeatherBelt");
        float copper = Armour("CopperScaleHelm", "CopperScaleHauberk", "CopperScaleGauntlets", "CopperScaleGreaves", "CopperScaleGirdle");

        List<ItemDefinition> bows = All<ItemDefinition>().Where(item => item.Weapon != null && item.Weapon.IsBow).ToList();
        Assert.Greater(bows.Count, 2);
        foreach (ItemDefinition bow in bows)
        {
            Assert.AreEqual(DodgeStyle.Quickstep, settings.StyleFor(leather + PlayerEquipment.LoadOf(bow, GearSlot.MainHand)), bow.name);
        }

        float sword = PlayerEquipment.LoadOf(Item("Weapons", "CopperSword"), GearSlot.MainHand);
        Assert.AreEqual(DodgeStyle.Roll, settings.StyleFor(copper + sword));
        Assert.AreEqual(DodgeStyle.HeavyRoll, settings.StyleFor(copper + sword + Item("Weapons", "CopperBoundShield").Weight));
        Assert.AreEqual(DodgeStyle.Roll, settings.StyleFor(leather + sword), "a sword is not a bow: leather with one rolls");
    }
}
