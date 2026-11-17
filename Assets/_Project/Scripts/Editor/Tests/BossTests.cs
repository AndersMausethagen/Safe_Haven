using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BossTests
{
    private static BossDefinition Boss => AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/_Project/Data/Monsters/Boss The Heartroot.asset");
    private static RaidRumourDefinition Rumour => AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/TheHeartroot.asset");

    [TestCase(1f, 0)]
    [TestCase(0.7f, 0)]
    [TestCase(0.6f, 1)]
    [TestCase(0.34f, 1)]
    [TestCase(0.2f, 2)]
    [TestCase(0.01f, 2)]
    public void TheFightMovesOnAsItsHealthFalls(float health, int phase)
    {
        Assert.AreEqual(phase, BossDefinition.PhaseAt(health, Boss.Phases));
    }

    [Test]
    public void AHugeBlowCanOnlyGoForwardThroughThePhases()
    {
        Assert.AreEqual(2, BossDefinition.PhaseAt(0.05f, Boss.Phases), "A blow past two marks reaches the last");
    }

    [Test]
    public void AnAttackWaitsForItsPhase()
    {
        var later = new MonsterAttack { attack = ScriptableObject.CreateInstance<AttackDefinition>(), maximumRange = 5f, fromPhase = 1 };
        var attacks = new[] { later };
        Assert.AreEqual(-1, MonsterAttacks.SelectAttack(attacks, new float[1], 2f, 0f, false, 0, 0), "Not yet");
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, new float[1], 2f, 0f, false, 0, 1));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, new float[1], 2f, 0f, false, 0, 2));
        Object.DestroyImmediate(later.attack);
    }

    private static RaidDefinition Valley => AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/TrollValley.asset");

    [Test]
    public void TrollValleyIsARaidOfItsOwn()
    {
        var misty = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        Assert.AreEqual("troll_valley", Valley.Id);
        Assert.AreEqual("wildlands", misty.Id, "The Woods keep their own id");
        Assert.IsFalse(string.IsNullOrEmpty(Valley.SceneName));
        Assert.AreNotEqual(misty.LayoutRules, Valley.LayoutRules);
        Assert.GreaterOrEqual(Valley.LowestDifficulty.tier, 2, "It is for those past the first tier");
        Assert.IsFalse(System.Linq.Enumerable.Contains(Valley.GetSystem<RaidRumoursSystem>().Pool, Rumour), "The Heartroot sleeps in the Woods");
        Assert.IsTrue(System.Linq.Enumerable.Contains(misty.GetSystem<RaidRumoursSystem>().Pool, Rumour), "Its rumour must not have gone from them");
    }

    [Test]
    public void BeatingTheHeartrootOpensTrollValley()
    {
        var saved = GameSession.Here;
        Assert.IsTrue(saved.IsBossDefeated(Boss.Id) || Valley.IsLocked, "Shut until then");
    }

    [Test]
    public void ItRisesAndHasMusicAndATrophy()
    {
        Assert.Greater(Boss.WakeSeconds, 0f);
        Assert.IsNotNull(Boss.WakeSound);
        Assert.IsTrue(Boss.Music.HasTracks);
        Assert.IsNotNull(Boss.Trophy);
        Assert.IsNotEmpty(Boss.HomeLines);
        CollectionAssert.Contains(SettlementSettings.Current.Bosses, Boss);
    }

    [Test]
    public void TheBossIsRememberedAcrossASave()
    {
        var settlement = new SettlementState();
        settlement.RecordExtraction();
        settlement.MarkBossDefeated("x");
        Assert.AreEqual("x", settlement.LastBossBeaten);
        Assert.AreEqual(1, settlement.LastBossBeatenAt);
    }

    [Test]
    public void BeatingItIsGivenOnce()
    {
        var settlement = new SettlementState();
        Assert.IsFalse(settlement.IsBossDefeated("x"));
        Assert.IsTrue(settlement.MarkBossDefeated("x"));
        Assert.IsFalse(settlement.MarkBossDefeated("x"), "A second kill teaches nothing new");
        Assert.IsTrue(settlement.IsBossDefeated("x"));
    }

    [Test]
    public void TheRumourWaitsForTheThirdRaidAndEndsWhenItIsBeaten()
    {
        var settlement = new SettlementState();
        Assert.IsFalse(Rumour.CanComeUp(null, settlement, null), "Not on a first raid");
        settlement.RecordExtraction();
        settlement.RecordExtraction();
        Assert.IsTrue(Rumour.CanComeUp(null, settlement, null));
        settlement.MarkBossDefeated(Boss.Id);
        Assert.IsFalse(Rumour.CanComeUp(null, settlement, null), "It is dead");
    }

    [Test]
    public void TheRumourIsInThePoolAndAsksForTheHollow()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        CollectionAssert.Contains(raid.GetSystem<RaidRumoursSystem>().Pool, Rumour);
        Assert.AreEqual("Guardian's Hollow", Rumour.SetPiece.DisplayName);
        Assert.IsTrue(Rumour.SetPiece.AlwaysThere);
    }

    [Test]
    public void EveryMapHasTheHollow()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset");
        for (int seed = 1; seed <= 10; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, 500f, seed);
            Assert.IsTrue(plan.Pieces.Exists(piece => piece.Definition == Rumour.SetPiece), $"Seed {seed}");
        }
    }

    [Test]
    public void TheBossIsWiredUpAndGivesASkill()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/TheHeartroot.prefab");
        var encounter = prefab.GetComponent<BossEncounter>();
        Assert.IsNotNull(encounter);
        Assert.AreEqual(Boss, encounter.Definition);
        Assert.IsNotNull(Boss.RewardSkill);
        Assert.AreEqual("the_heartroot", prefab.GetComponent<MonsterAI>().Definition.Id);
        Assert.AreEqual("TheHeartrootStats", prefab.GetComponent<MonsterAI>().Definition.CombatStats.name);
    }

    [Test]
    public void EveryBoss_IsHeard_AndItsAttacksComeWithinItsStages()
    {
        // Review 2026-11-17: the Heartroot's and the Brukam's music had volume 0; an attack gated on a stage the fight never reaches is dead.
        foreach (BossDefinition boss in SettlementSettings.Current.Bosses)
        {
            foreach (MusicTrack track in boss.Music.tracks)
            {
                Assert.Greater(track.volume, 0f, $"{boss.name}'s music is silent");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                BossEncounter encounter = prefab != null ? prefab.GetComponent<BossEncounter>() : null;
                if (encounter == null || encounter.Definition != boss)
                {
                    continue;
                }

                foreach (MonsterAttack attack in prefab.GetComponent<MonsterAI>().Definition.Attacks)
                {
                    Assert.LessOrEqual(attack.fromPhase, boss.Phases.Length, $"{prefab.name}'s {attack.attack?.name} waits for a stage it never reaches");
                }
            }
        }
    }

    [Test]
    public void TheBrukam_SleepsOnItsBridge_UntilSomeoneComes_AndTeachesMountainLeap()
    {
        var brukam = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/_Project/Data/Monsters/Boss Brukam.asset");
        Assert.Greater(brukam.WakeRange, 0f, "it stands on the map from the start: without a range it wakes the raid wherever the player is");
        Assert.AreEqual("Mountain Leap", brukam.RewardSkill.DisplayName);
        Assert.Contains(brukam, (System.Collections.ICollection)new System.Collections.Generic.List<BossDefinition>(SettlementSettings.Current.Bosses));
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset");
        Assert.IsNotNull(rules.Valley.bossBridge, "the valley's head bridge is the Brukam's");
        Assert.AreEqual("brukam", rules.Valley.bossBridge.Guards[0].prefab.GetComponent<MonsterAI>().Definition.Id);
    }
}
