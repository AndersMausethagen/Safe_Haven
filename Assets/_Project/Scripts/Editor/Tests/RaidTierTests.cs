using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Gear score and raid tiers: the score adds up as promised, and each tier asks for and hands over what it should.</summary>
public sealed class RaidTierTests
{
    private static RaidDefinition Raid => AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");

    private static RaidMapDefinition Map => AssetDatabase.LoadAssetAtPath<RaidMapDefinition>("Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset");

    private static ItemDefinition Item(string name)
    {
        string guid = AssetDatabase.FindAssets("t:ItemDefinition " + name).First(found => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(found)) == name);
        return AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
    }

    [Test]
    public void TheSlotsShares_AddUpToAFullKit()
    {
        Assert.AreEqual(1f, GearScoreSettings.Current.TotalShare, 0.001f);
    }

    [Test]
    public void AWeapon_CountsForMoreThanAnyArmor_AndAHigherTierForMoreThanALower()
    {
        Assert.Greater(GearScore.Of(Item("IronSword")), GearScore.Of(Item("IronMailHauberk")));
        Assert.Greater(GearScore.Of(Item("IronMailHauberk")), GearScore.Of(Item("IronMailHelm")));
        Assert.Greater(GearScore.Of(Item("IronSword")), GearScore.Of(Item("CopperSword")));
        Assert.Greater(GearScore.Of(Item("CopperSword")), GearScore.Of(Item("KnottedClub")));
    }

    [Test]
    public void ATwoHandedWeapon_IsNotBehindASwordAndShield()
    {
        int pair = GearScore.Of(Item("IronSword")) + GearScore.Of(Item("IronKiteShield"));
        Assert.AreEqual(pair, GearScore.Of(Item("BeardedGreataxe")), 2, "carrying a greataxe costs nothing against a sword and a shield");
    }

    [Test]
    public void WhatIsNoGear_AddsNothing_AndATorchNextToNothing()
    {
        Assert.AreEqual(0, GearScore.Of(Item("BarkPack")), "a backpack");
        Assert.AreEqual(0, GearScore.Of(Item("OakWood")), "a material");
        Assert.AreEqual(1, GearScore.Of(Item("HandTorch")));
        Assert.AreEqual(1, GearScore.Of(Item("FishingRod")));
    }

    [Test]
    public void Empowering_RaisesAPiecesScore()
    {
        ItemDefinition chest = Item("IronMailHauberk");
        Assert.Greater(GearScore.Of(new ItemStack(chest, 1).WithEmpower(3)), GearScore.Of(new ItemStack(chest, 1)));
    }

    [Test]
    public void EmptySlots_NeverBeatFilledOnes()
    {
        var gear = new GearSet();
        Assert.AreEqual(0, GearScore.Of(gear));
        gear.Slots.SetSlot((int)GearSlot.Chest, new ItemStack(Item("WovenHideJerkin"), 1));
        int chest = GearScore.Of(gear);
        Assert.Greater(chest, 0);
        gear.Slots.SetSlot((int)GearSlot.Head, new ItemStack(Item("WovenHideCap"), 1));
        Assert.Greater(GearScore.Of(gear), chest);
    }

    [Test]
    public void EachTier_RecommendsMoreThanTheLast_AndAFullKitOfItsOwnTierReachesIt()
    {
        List<ItemDefinition> gear = GearBalance.AllGear();
        int last = -1;
        foreach (RaidDifficulty tier in Raid.Difficulties)
        {
            Assert.Greater(tier.recommendedGearScore, last);
            last = tier.recommendedGearScore;
            GearBalance.Kit kit = GearBalance.KitOf(tier.tier, gear);
            Assert.GreaterOrEqual(kit.Score, tier.recommendedGearScore, $"tier {tier.tier}: the gear there is to wear must reach what is recommended");
            Assert.LessOrEqual(tier.minimumGearScore, GearBalance.KitOf(Mathf.Max(0, tier.tier - 1), gear).Score, $"tier {tier.tier}: the tier below's kit is let in");
        }

        Assert.AreEqual(0, Raid.Difficulties[0].minimumGearScore, "nobody is kept out of the first raid");
    }

    [Test]
    public void EachKit_IsStrongerThanTheLast()
    {
        List<ItemDefinition> gear = GearBalance.AllGear();
        GearBalance.Kit before = GearBalance.KitOf(1, gear);
        for (int tier = 2; tier <= 4; tier++)
        {
            GearBalance.Kit kit = GearBalance.KitOf(tier, gear);
            Assert.Greater(kit.Damage, before.Damage, $"tier {tier} hits harder");
            Assert.Greater(kit.Toughness, before.Toughness, $"tier {tier} takes more");
            before = kit;
        }
    }

    [Test]
    public void AtTheRecommendedKit_FightsStayAlike_AndSkippingATierHurts()
    {
        List<ItemDefinition> gear = GearBalance.AllGear();
        GearBalance.Kit first = GearBalance.KitOf(1, gear);
        float kill = GearBalance.HitsToKill(first, 100f, 10f, Raid.Difficulties[0].enemyHealth);
        float die = GearBalance.HitsToDie(first, 15f, Raid.Difficulties[0].enemyPower);
        foreach (RaidDifficulty tier in Raid.Difficulties)
        {
            GearBalance.Kit kit = GearBalance.KitOf(tier.tier, gear);
            Assert.AreEqual(1f, GearBalance.HitsToKill(kit, 100f, 10f, tier.enemyHealth) / kill, 0.2f, $"tier {tier.tier}: hits to kill");
            Assert.AreEqual(1f, GearBalance.HitsToDie(kit, 15f, tier.enemyPower) / die, 0.2f, $"tier {tier.tier}: hits to die");
            if (tier.tier > 1)
            {
                GearBalance.Kit before = GearBalance.KitOf(tier.tier - 1, gear);
                Assert.Greater(GearBalance.HitsToKill(before, 100f, 10f, tier.enemyHealth), GearBalance.HitsToKill(kit, 100f, 10f, tier.enemyHealth) * 1.1f, $"tier {tier.tier}: kit before kills slower");
                Assert.Less(GearBalance.HitsToDie(before, 15f, tier.enemyPower), GearBalance.HitsToDie(kit, 15f, tier.enemyPower) * 0.9f, $"tier {tier.tier}: kit before dies sooner");
            }
        }
    }

    [Test]
    public void EachTier_BringsSomethingNew()
    {
        var seen = new HashSet<string>();
        var enemies = new List<string>();
        foreach (RaidDifficulty tier in Raid.Difficulties)
        {
            var here = new HashSet<string>(Raid.SpawnLists.SelectMany(list => list.EntriesFor(tier.tier)).Where(entry => entry.prefab != null).Select(entry => entry.prefab.name));
            bool newPrefab = here.Any(name => !seen.Contains(name));
            bool newDrop = tier.tier >= 4 && tier.Allows(Item("Trollstone"));
            Assert.IsTrue(newPrefab || newDrop, $"tier {tier.tier} has nothing new");
            seen.UnionWith(here);
        }
    }

    [Test]
    public void TheFirstTier_HoldsNothingAboveTheNextTiersMaterials()
    {
        List<string> first = Raid.SpawnLists.SelectMany(list => list.EntriesFor(1)).Where(entry => entry.prefab != null).Select(entry => entry.prefab.name).ToList();
        CollectionAssert.Contains(first, "CopperOreNode");
        CollectionAssert.DoesNotContain(first, "IronOreNode");
        CollectionAssert.DoesNotContain(first, "MoonsilverOreNode");
        CollectionAssert.DoesNotContain(first, "Draug");
        CollectionAssert.Contains(Raid.SpawnLists.SelectMany(list => list.EntriesFor(2)).Where(entry => entry.prefab != null).Select(entry => entry.prefab.name).ToList(), "IronOreNode");
        CollectionAssert.Contains(Raid.SpawnLists.SelectMany(list => list.EntriesFor(3)).Where(entry => entry.prefab != null).Select(entry => entry.prefab.name).ToList(), "Draug");
    }

    [Test]
    public void DropsAreCappedByTier_ButFoodIsNot()
    {
        RaidDifficulty first = Raid.Difficulties[0];
        Assert.IsTrue(first.Allows(Item("CopperOre")));
        Assert.IsFalse(first.Allows(Item("IronOre")));
        Assert.IsFalse(first.Allows(Item("Trollstone")), "a tier I troll doesn't drop tier V stone");
        Assert.IsTrue(first.Allows(Item("Bandage")));
        Assert.IsTrue(Raid.Difficulties[3].Allows(Item("Trollstone")));
    }

    [Test]
    public void NoNodeOutlastsItsTier_WhateverPlacedIt()
    {
        GatheringNodeDefinition Node(string name) => AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>($"Assets/_Project/Data/Gathering/{name}.asset");
        RaidDifficulty first = Raid.Difficulties[0];
        Assert.IsTrue(ResourceNode.RaidAllows(Node("CopperOre"), first));
        Assert.IsFalse(ResourceNode.RaidAllows(Node("IronOre"), first), "the Ore Collapse event holds iron veins of its own");
        Assert.IsFalse(ResourceNode.RaidAllows(Node("MoonsilverOre"), first));
        Assert.IsTrue(ResourceNode.RaidAllows(Node("IronOre"), Raid.Difficulties[1]));
        Assert.IsFalse(ResourceNode.RaidAllows(Node("MoonsilverOre"), Raid.Difficulties[1]));
        Assert.IsTrue(ResourceNode.RaidAllows(Node("MoonsilverOre"), Raid.Difficulties[2]));
        Assert.IsTrue(ResourceNode.RaidAllows(Node("IronOre"), null), "a raid without tiers, or home");
    }

    [Test]
    public void ATiersScaling_ReachesTheRaidsRules()
    {
        RaidDifficulty third = Raid.Difficulties[2];
        RaidParameters rules = RaidParameters.Calculate(Raid, null, 0f, null, third);
        Assert.AreEqual(third.enemyHealth, rules.EnemyHealthMultiplier, 0.001f);
        Assert.AreEqual(third.enemyPower, rules.EnemyPowerMultiplier, 0.001f);
        Assert.AreEqual(third.loot, rules.LootMultiplier, 0.001f);
    }

    [Test]
    public void ASession_PlaysAtItsTier_OrTheLowest()
    {
        var none = new RaidModifierDefinition[0];
        Assert.AreEqual(1, new RaidSession(Raid, none, RaidParameters.Calculate(Raid, none), null, "SampleScene").Tier);
        var session = new RaidSession(Raid, none, RaidParameters.Calculate(Raid, none), null, "SampleScene", 0, 1, Raid.Difficulties[2]);
        Assert.AreEqual(3, session.Tier);
        Assert.IsTrue(session.Allows(Item("MoonsilverOre")));
    }

    [Test]
    public void ThePortal_StartsOnTheTierTheGearIsReadyFor()
    {
        Assert.AreEqual(1, Raid.DifficultyFor(0).tier);
        Assert.AreEqual(2, Raid.DifficultyFor(30).tier);
        Assert.AreEqual(2, Raid.DifficultyFor(49).tier);
        Assert.AreEqual(4, Raid.DifficultyFor(500).tier);
    }

    [Test]
    public void ABriefing_OnlyNamesCreaturesOfItsTier()
    {
        string guid = AssetDatabase.FindAssets("t:MonsterDefinition Draug").First();
        var draug = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
        Assert.IsFalse(Raid.CanMeet(draug, 1));
        Assert.IsTrue(Raid.CanMeet(draug, 3));
    }

    private static MonsterDefinition Monster(string name) =>
        AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{name}.asset");

    private static bool Learns(string monster, int tier) => Monster(monster).Attacks.Any(action => action.fromTier == tier);

    [Test]
    public void AWolfPounces_FromTierTwo()
    {
        MonsterAttack pounce = Monster("ForestBittenWolf").Attacks.First(action => action.kind == MonsterAttackKind.Charge);
        Assert.AreEqual(2, pounce.fromTier);
        Assert.Greater(pounce.leapHeight, 0f, "it leaps");
        Assert.Greater(pounce.chargeSpeed, Monster("ForestBittenWolf").MoveSpeed * 2f, "and closes the distance far faster than it runs");
        IReadOnlyList<MonsterAttack> actions = Monster("ForestBittenWolf").Attacks;
        var cooldowns = new float[actions.Count];
        int atTierOne = MonsterAttacks.SelectAttack(actions, cooldowns, 6f, 0f, false, 1);
        int atTierTwo = MonsterAttacks.SelectAttack(actions, cooldowns, 6f, 0f, false, 2);
        Assert.IsTrue(atTierOne < 0 || actions[atTierOne].kind != MonsterAttackKind.Charge, "a tier I wolf has to run in");
        Assert.AreEqual(MonsterAttackKind.Charge, actions[atTierTwo].kind);
        Assert.IsTrue(MonsterAttacks.SelectAttack(actions, cooldowns, 6f, 0f, false) < 0 || actions[MonsterAttacks.SelectAttack(actions, cooldowns, 6f, 0f, false)].kind != MonsterAttackKind.Charge, "nor do wolves pounce at home");
    }

    [Test]
    public void ASpentPounce_SendsTheWolfIn_ButASpentBiteDoesnt()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        var pounce = new MonsterAttack { attack = attack, kind = MonsterAttackKind.Charge, minimumRange = 3f, maximumRange = 8f, fromTier = 2 };
        var bite = new MonsterAttack { attack = attack, kind = MonsterAttackKind.Melee, maximumRange = 1.5f };
        var wolf = new[] { pounce, bite };
        try
        {
            Assert.IsTrue(MonsterAttacks.HoldsGround(wolf, new[] { 0f, 0f }, 5f, 0f, 2), "ready: it stops to spring");
            Assert.IsFalse(MonsterAttacks.HoldsGround(wolf, new[] { 7f, 0f }, 5f, 0f, 2), "spent: it runs");
            Assert.IsFalse(MonsterAttacks.HoldsGround(wolf, new[] { 0f, 0f }, 5f, 0f, 1), "a tier I wolf can't spring, so it runs");
            Assert.IsTrue(MonsterAttacks.HoldsGround(wolf, new[] { 7f, 5f }, 1f, 0f, 2), "between bites it doesn't crowd its target");
        }
        finally
        {
            Object.DestroyImmediate(attack);
        }
    }

    [Test]
    public void ASpentWideSlam_DoesntLeaveTheGuardianStandingOffToTheSide()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        var slam = new MonsterAttack { attack = attack, kind = MonsterAttackKind.Melee, maximumRange = 2f };
        var wide = new MonsterAttack { attack = attack, kind = MonsterAttackKind.Melee, maximumRange = 3.6f, fromTier = 3 };
        var guardian = new[] { slam, wide };
        try
        {
            Assert.IsTrue(MonsterAttacks.HoldsGround(guardian, new[] { 0f, 0f }, 3f, 0f, 3), "ready: it slams from here");
            Assert.IsFalse(MonsterAttacks.HoldsGround(guardian, new[] { 0f, 9f }, 3f, 0f, 3), "spent: it walks up");
            Assert.IsTrue(MonsterAttacks.HoldsGround(guardian, new[] { 9f, 9f }, 1.5f, 0f, 3), "and stays once close");
        }
        finally
        {
            Object.DestroyImmediate(attack);
        }
    }

    [Test]
    public void ACreatureWithOnlyACharge_KeepsItsDistanceBetweenCharges()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        var dive = new[] { new MonsterAttack { attack = attack, kind = MonsterAttackKind.Charge, minimumRange = 2f, maximumRange = 8f } };
        try
        {
            Assert.IsTrue(MonsterAttacks.HoldsGround(dive, new[] { 9f }, 5f, 0f, 1), "nothing to walk up with");
            Assert.IsFalse(MonsterAttacks.HoldsGround(dive, new[] { 9f }, 12f, 0f, 1), "but it follows from further off");
        }
        finally
        {
            Object.DestroyImmediate(attack);
        }
    }

    [Test]
    public void EachHigherTier_TeachesItsCreaturesSomethingNew()
    {
        foreach (string name in new[] { "ForestBittenWolf", "BogSpitter", "MarshLight" })
        {
            Assert.IsTrue(Learns(name, 2), $"{name} at tier II");
        }

        foreach (string name in new[] { "BlightBoar", "RootGuardian", "SmallMossTroll", "MossShaman", "OldMossTroll" })
        {
            Assert.IsTrue(Learns(name, 3), $"{name} at tier III");
        }

        foreach (string name in new[] { "Draug", "RootGuardian", "OldMossTroll" })
        {
            Assert.IsTrue(Learns(name, 4), $"{name} at tier IV");
        }
    }

    [Test]
    public void EveryCreature_CanStillFightAtTheLowestTier()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (monster.Attacks.Count > 0)
            {
                Assert.IsTrue(monster.Attacks.Any(action => action.fromTier == 0 && action.kind != MonsterAttackKind.Howl && action.kind != MonsterAttackKind.Summon),
                    $"{monster.name} has actions, so its plain attack is never used: one of them has to be for every tier");
            }
        }
    }

    [Test]
    public void EveryAction_IsComplete()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (MonsterAttack action in monster.Attacks)
            {
                Assert.IsNotNull(action.attack, monster.name);
                Assert.IsTrue(action.kind != MonsterAttackKind.Projectile || action.projectilePrefab != null, $"{monster.name} {action.attack.name} shoots nothing");
                Assert.IsTrue(action.kind != MonsterAttackKind.Summon || action.summonPrefab != null, $"{monster.name} {action.attack.name} calls nothing");
                Assert.Greater(action.maximumRange, action.minimumRange, $"{monster.name} {action.attack.name}");
            }
        }
    }

    [Test]
    public void LateRaidHunters_MatchTheTier()
    {
        List<RaidSpawnList> hunters = Raid.SpawnLists.Where(list => list.category == SpawnCategory.Escalation).ToList();
        Assert.IsNotEmpty(hunters);
        foreach (RaidSpawnList list in hunters)
        {
            CollectionAssert.AreEquivalent(new[] { "ForestBittenWolf" }, list.EntriesFor(1).Select(entry => entry.prefab.name).ToArray(), $"{list.name}: tier I is wolves");
            Assert.IsTrue(list.EntriesFor(2).Any(entry => entry.prefab.name != "ForestBittenWolf"), $"{list.name}: tier II brings another");
            Assert.IsFalse(list.EntriesFor(4).Any(entry => entry.prefab.name == "ForestBittenWolf"), $"{list.name}: tier IV has no plain wolves");
            Assert.IsTrue(list.EntriesFor(4).Any(entry => entry.prefab.name == "Draug"), $"{list.name}: tier IV sends the dead");
            Assert.IsTrue(list.SpawnsEnemies, "enemy density still scales them");
        }
    }

    // What a run places, on average, over many seeds: spawn lists at raid start, as rolled in play.
    private static Dictionary<string, float> Creatures(RaidDifficulty tier)
    {
        var counts = new Dictionary<string, float>();
        const int Runs = 300;
        foreach (RaidSpawnList list in Raid.SpawnLists.Where(list => list.category == SpawnCategory.Enemies))
        {
            for (int seed = 1; seed <= Runs; seed++)
            {
                var random = new System.Random(RaidLayout.SeedFor(seed, "List:" + list.name));
                int amount = random.Next(Mathf.RoundToInt(list.MinCount * tier.enemyDensity), Mathf.RoundToInt(list.MaxCount * tier.enemyDensity) + 1);
                foreach (GameObject prefab in SpawnTable.Roll(list.EntriesFor(tier.tier), random, amount))
                {
                    foreach (MonsterAI creature in prefab.GetComponentsInChildren<MonsterAI>(true))
                    {
                        counts.TryGetValue(creature.Definition.name, out float now);
                        counts[creature.Definition.name] = now + 1f / Runs;
                    }
                }
            }
        }

        return counts;
    }

    [Test]
    public void TierOne_IsNotMostlyWolves_AndHigherTiersHoldMore()
    {
        Dictionary<string, float> first = Creatures(Raid.Difficulties[0]);
        Assert.Less(first["ForestBittenWolf"] / first.Values.Sum(), 0.4f, "a tier I raid shows the whole forest");
        float before = first.Values.Sum();
        foreach (RaidDifficulty tier in Raid.Difficulties.Skip(1))
        {
            float here = Creatures(tier).Values.Sum();
            Assert.Greater(here, before, $"tier {tier.tier} holds more creatures than the tier before");
            before = here;
        }
    }

    [Test]
    public void Roamers_MatchTheTier()
    {
        var table = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Project/Data/Raids/SpawnTables/MistyWoods_Roamers.asset");
        var random = new System.Random(5);
        var atFirst = new HashSet<string>();
        var atFourth = new HashSet<string>();
        for (int i = 0; i < 400; i++)
        {
            atFirst.Add(table.PickAt(random, 1).name);
            atFourth.Add(table.PickAt(random, 4).name);
        }

        CollectionAssert.DoesNotContain(atFirst, "Draug");
        CollectionAssert.Contains(atFourth, "Draug");
    }

    [Test]
    public void TheLog_CountsRaidsByTier()
    {
        PlaytestEntry Row(string kind, int amount, string detail) => new PlaytestEntry { Event = kind, Amount = amount, Detail = detail };
        PlaytestRunSummary summary = PlaytestLog.Summarise("run", new[]
        {
            Row(PlaytestLog.RaidTier, 2, PlaytestLog.TierDetail(25, 30, "Day")),
            Row(nameof(GameEventKind.RaidFinished), 300, nameof(RaidOutcome.Died)),
            Row(PlaytestLog.RaidTier, 2, PlaytestLog.TierDetail(40, 30, "Night")),
            Row(nameof(GameEventKind.RaidFinished), 400, nameof(RaidOutcome.Extracted)),
            Row(nameof(GameEventKind.RaidFinished), 400, nameof(RaidOutcome.Extracted)),
        }, new PlaytestMilestone[0]);

        Assert.AreEqual(3, summary.Raids, "a raid from before tiers still counts");
        Assert.AreEqual(2, summary.TierRaids[2]);
        Assert.AreEqual(1, summary.TierExtractions[2]);
        Assert.AreEqual(5, summary.TierScoreMargin[2], "5 under, then 10 over");
    }

    private static List<EliteAffixDefinition> AllAffixes => Raid.EliteAffixes.ToList();

    [Test]
    public void TheRaid_HasAffixesForEveryTier_EachWithAColourAndAName()
    {
        Assert.GreaterOrEqual(AllAffixes.Count, 6);
        foreach (RaidDifficulty tier in Raid.Difficulties)
        {
            Assert.GreaterOrEqual(AllAffixes.Count(affix => affix.FromTier <= tier.tier), tier.eliteAffixes + 1, $"tier {tier.tier} has a choice");
        }

        foreach (EliteAffixDefinition affix in AllAffixes)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(affix.DisplayName), affix.name);
            Assert.Greater(affix.Colour.maxColorComponent, 0.2f, affix.name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(affix.Description), affix.name);
        }

        Assert.AreEqual(AllAffixes.Count, AllAffixes.Select(affix => affix.Colour).Distinct().Count(), "each can be told apart by its ring");
    }

    [Test]
    public void HigherTiers_HaveMoreElites_WithMoreAffixes_AndBetterLoot()
    {
        RaidDifficulty before = null;
        foreach (RaidDifficulty tier in Raid.Difficulties)
        {
            Assert.Greater(tier.eliteCount.y, 0, $"tier {tier.tier} has elites");
            Assert.IsNotEmpty(tier.eliteLoot.Entries, $"and a reason to kill them at tier {tier.tier}");
            if (before != null)
            {
                Assert.GreaterOrEqual(tier.eliteCount.y, before.eliteCount.y);
                Assert.GreaterOrEqual(tier.eliteAffixes, before.eliteAffixes);
            }

            before = tier;
        }

        Assert.Greater(Raid.Difficulties[3].eliteAffixes, Raid.Difficulties[0].eliteAffixes);
        foreach (RaidDifficulty tier in Raid.Difficulties)
        {
            foreach (ItemDropEntry entry in tier.eliteLoot.Entries)
            {
                Assert.IsTrue(entry.Item == null || tier.Allows(entry.Item), $"tier {tier.tier} elites can't promise {entry.DropName}, as the tier wouldn't hand it over");
            }
        }
    }

    [Test]
    public void PickingAffixes_RespectsTheirTier_AndNeverRepeats()
    {
        var random = new System.Random(3);
        for (int i = 0; i < 200; i++)
        {
            List<EliteAffixDefinition> first = RaidElites.PickAffixes(AllAffixes, 1, 1, random);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, first[0].FromTier, "tier I gets only tier I affixes");
            List<EliteAffixDefinition> fourth = RaidElites.PickAffixes(AllAffixes, 4, 2, random);
            Assert.AreEqual(2, fourth.Distinct().Count());
        }

        Assert.AreEqual(AllAffixes.Count, RaidElites.PickAffixes(AllAffixes, 4, 99, random).Count, "asking for too many gives what there is");
    }

    [Test]
    public void GreatBeastsAndSwarms_AreNeverElites()
    {
        Assert.IsTrue(Monster("OldMossTroll").NeverElite);
        Assert.IsTrue(Monster("Rootling").NeverElite);
        Assert.IsFalse(Monster("ForestBittenWolf").NeverElite);
    }

    [Test]
    public void AnElite_IsNamedStrongerAndOutlined()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/ForestBittenWolf.prefab");
        GameObject wolf = Object.Instantiate(prefab);

        // The label's text asks for its own material, which Unity warns about outside play. It's fine there.
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try
        {
            var ai = wolf.GetComponent<MonsterAI>();
            EliteAffixDefinition ironhide = AllAffixes.First(affix => affix.name.Contains("Ironhide"));
            var stats = wolf.GetComponent<Combatant>().Stats;
            float baseHealth = stats.Get(StatType.MaxHealth), baseArmor = stats.Get(StatType.Armor);
            Vector3 size = wolf.transform.localScale;
            stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, 1f, this));
            EliteCreature elite = EliteCreature.Make(ai, new[] { ironhide }, Raid.Difficulties[0], 2f, 1f);
            Assert.AreEqual(baseHealth * 2f * ironhide.Health, stats.Get(StatType.MaxHealth), 0.5f, "its multiplier multiplies the raid's rather than adding to it");
            Assert.AreEqual(baseArmor + ironhide.Armor, stats.Get(StatType.Armor), 0.01f);
            Assert.AreEqual(size.x * ironhide.Size, wolf.transform.localScale.x, 0.001f);
            StringAssert.Contains(ironhide.DisplayName, elite.Title);
            StringAssert.Contains(ai.Definition.DisplayName, elite.Title);
            Assert.IsNotEmpty(wolf.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.gameObject.name == "Elite Ring"), "a ring to see it by");
        }
        finally
        {
            Object.DestroyImmediate(wolf);
        }
    }

    [Test]
    public void TheForest_ChangesWithTheTier()
    {
        RaidMapDefinition copy = Object.Instantiate(Map);
        try
        {
            copy.UseGenerated(16, new byte[256], new byte[256], new byte[256], new RaidMapMood(), 1, 1);
            Assert.AreEqual(0f, copy.Trees.First(tree => tree.prefab.name.StartsWith("Dead Spruce")).weight, "no dead spruce at tier I");
            Assert.Greater(copy.Trees.First(tree => tree.prefab.name.StartsWith("Spruce A")).weight, 0f);
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
    }
}
