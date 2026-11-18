using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BossTests
{
    private static BossDefinition Boss => AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/_Project/Data/Monsters/Boss The Heartroot.asset");
    private static RaidRumourDefinition Rumour => AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/TheHeartroot.asset");

    // A rule over every boss rather than one boss's marks: whole it fights its first stage, each stage comes in turn as its health
    // falls and none goes back, and a blow past every mark reaches the last (the Heartroot's Hollow's Hold).
    [Test]
    public void EveryBossFight_MovesOnThroughEachStage_AsItsHealthFalls()
    {
        int staged = 0;
        foreach (BossDefinition boss in SettlementSettings.Current.Bosses)
        {
            BossPhase[] phases = boss.Phases;
            if (phases == null || phases.Length == 0)
            {
                continue;
            }

            staged++;
            Assert.AreEqual(0, BossDefinition.PhaseAt(1f, phases), $"{boss.name}: whole, its first stage");
            var seen = new System.Collections.Generic.HashSet<int>();
            int last = 0;
            for (float health = 1f; health > 0f; health -= 0.005f)
            {
                int phase = BossDefinition.PhaseAt(health, phases);
                Assert.GreaterOrEqual(phase, last, $"{boss.name}: at {health:0.000} it went back a stage");
                seen.Add(phase);
                last = phase;
            }

            Assert.AreEqual(phases.Length, BossDefinition.PhaseAt(0.0001f, phases), $"{boss.name}: a blow past every mark reaches the last");
            Assert.AreEqual(phases.Length + 1, seen.Count, $"{boss.name}: every stage comes in turn");
        }

        Assert.Greater(staged, 0, "a boss with stages was checked");
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
        GameSession.StartNewGame();
        try
        {
            Assert.IsTrue(Valley.IsLocked, "Shut until then");
            GameSession.Home.MarkBossDefeated(Boss.Id);
            Assert.IsFalse(Valley.IsLocked, "Open once the Heartroot falls");
        }
        finally
        {
            GameSession.StartNewGame();
        }
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
    public void TheLastBossBeaten_IsNoted_WithTheRaidItFellOn()
    {
        // What a save keeps of it is SaveCoverageTests' (every saved field of SettlementState through SaveMapper).
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
        // It points the way only while The Breathing Hollow is under way (the quest audit, 2026-11-17).
        var finding = new[] { "thebreathinghollow" };
        Assert.IsFalse(Rumour.CanComeUp(null, settlement, null, new string[0]), "not before the find step");
        Assert.IsTrue(Rumour.CanComeUp(null, settlement, null, finding));
        settlement.MarkBossDefeated(Boss.Id);
        Assert.IsFalse(Rumour.CanComeUp(null, settlement, null, finding), "It is dead");
    }

    [Test]
    public void EveryMapHasTheHollow()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset");
        Assert.IsTrue(Rumour.SetPiece.AlwaysThere, "the hollow stands on every map, not only when its rumour comes up");
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
    public void TheBrukam_FightsInHisOwnEncounter_AndTeachesMountainLeap()
    {
        var brukam = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/_Project/Data/Monsters/Boss Brukam.asset");
        Assert.Greater(brukam.WakeRange, 0f, "placed anywhere outside an arena, it must still sleep until someone comes");
        Assert.AreEqual("Mountain Leap", brukam.RewardSkill.DisplayName);
        Assert.Contains(brukam, (System.Collections.ICollection)new System.Collections.Generic.List<BossDefinition>(SettlementSettings.Current.Bosses));
        // Since 2026-11-17 (the user: move him out of the expeditions): Brukam's Bridge, a boss encounter, is where he is fought.
        var bridge = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/BrukamsBridge.asset");
        Assert.IsNotNull(bridge, "Brukam's Bridge");
        Assert.IsTrue(bridge.IsEncounter);
        Assert.AreEqual(brukam, bridge.Encounter.Boss);
        Assert.AreEqual(2, bridge.Encounter.FirstTier);
        string arena = System.IO.File.ReadAllText("Assets/_Project/Scenes/Raids/" + bridge.SceneName + ".unity");
        Assert.IsTrue(arena.Contains(AssetDatabase.AssetPathToGUID("Assets/_Project/Prefabs/Enemies/Brukam.prefab")), "the Brukam stands in his arena");
        var valley = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset");
        Assert.IsTrue(valley.Valley.bossBridge == null || valley.Valley.bossBridge.Guards.Count == 0, "and no longer on a valley bridge");
    }
}
