using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The combat round after the user's playtest of 2026-11-25 (deaths nobody could answer, a crowd from behind, healing never drunk, the
/// Copper Sword at tier II, a log too thin to say how a death went): each rule here can fail, and none restates a number from data.
/// </summary>
public sealed class CombatSafetyTests
{
    private CombatTechniqueSettings rules;

    [SetUp]
    public void SetUp() => rules = ScriptableObject.CreateInstance<CombatTechniqueSettings>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(rules);

    // An Old Moss Troll's stomp took two thirds of the player's health from behind.
    [Test]
    public void ACreaturesBlow_TakesNoMoreThanItsShare_ButABossesAndAFallKeepTheirs()
    {
        const float max = 100f;
        float most = max * rules.HeaviestBlowShare;
        Assert.Less(most, max * 0.5f, "no lesser foe takes half the bar in one blow");
        Assert.AreEqual(most, BlowShaping.Shape(89f, max, true, false, rules), 1e-4f, "a creature's great blow is held to the share");
        Assert.AreEqual(20f, BlowShaping.Shape(20f, max, true, false, rules), 1e-4f, "an ordinary blow is untouched");
        Assert.AreEqual(89f, BlowShaping.Shape(89f, max, false, false, rules), 1e-4f, "a boss's, a fall's or the cold's is its own");
    }

    // Three Heartroot eruptions, 28, 28 and 33, landed inside a second.
    [Test]
    public void AfterABigBlow_TheNextOnesTakeLess_ForAMoment()
    {
        const float max = 100f;
        Assert.IsTrue(BlowShaping.StartsGrace(max * rules.BigBlowShare, max, rules), "a big blow starts the grace");
        Assert.IsFalse(BlowShaping.StartsGrace(max * rules.BigBlowShare * 0.5f, max, rules), "a small one doesn't");
        Assert.Greater(rules.GraceSeconds, 0f);
        float eased = BlowShaping.Shape(30f, max, false, true, rules);
        Assert.Less(eased, 30f, "in the grace a blow takes less");
        Assert.Greater(eased, 0f, "but still something: not invulnerability");
    }

    [Test]
    public void TheCap_HoldsCreaturesOnly_NotBossesOrTheWorld()
    {
        var creature = new GameObject("Capped Creature", typeof(Combatant), typeof(MonsterAI));
        var boss = new GameObject("Uncapped Boss", typeof(Combatant), typeof(MonsterAI), typeof(BossEncounter));
        var thing = new GameObject("Uncapped Thing", typeof(Combatant));
        try
        {
            Assert.IsTrue(PlayerBlowGrace.IsCapped(creature.GetComponent<Combatant>()));
            Assert.IsFalse(PlayerBlowGrace.IsCapped(boss.GetComponent<Combatant>()), "a boss's blows are its fight");
            Assert.IsFalse(PlayerBlowGrace.IsCapped(thing.GetComponent<Combatant>()), "not a creature");
            Assert.IsFalse(PlayerBlowGrace.IsCapped(null), "a fall or the cold");
        }
        finally
        {
            Object.DestroyImmediate(creature);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(thing);
        }
    }

    // On a real body: the grace starts after a big blow and the next is eased; the blow inside it doesn't lengthen it.
    [Test]
    public void PlayerBlowGrace_EasesTheBlowAfterABigOne()
    {
        var body = new GameObject("Grace Probe", typeof(Combatant));
        try
        {
            var grace = body.AddComponent<PlayerBlowGrace>();
            CombatTechniqueSettings live = CombatTechniqueSettings.Current;
            var hit = new DamageInfo(null, null, 10f, DamageType.Physical, false, Vector3.zero, Vector3.forward, 0f, HitTechnique.None);
            Assert.AreEqual(5f, grace.Shape(hit, 5f, 100f), 1e-4f, "a small blow passes as it is");
            Assert.IsFalse(grace.InGrace);
            grace.Shape(hit, 100f * live.BigBlowShare, 100f);
            Assert.IsTrue(grace.InGrace, "a big blow starts it");
            Assert.Less(grace.Shape(hit, 30f, 100f), 30f, "the next is eased");
        }
        finally
        {
            Object.DestroyImmediate(body);
        }
    }

    // Marsh Lights shooting from behind a wolf pack ended the run.
    [Test]
    public void InACrowd_FewerMayShoot()
    {
        Assert.AreEqual(rules.RangedAtOnce, rules.RangedAtOnceWith(0));
        Assert.AreEqual(rules.RangedAtOnce, rules.RangedAtOnceWith(rules.CrowdedAt - 1), "no crowd yet");
        Assert.Less(rules.RangedAtOnceWith(rules.CrowdedAt), rules.RangedAtOnce, "a crowd on the player: fewer shoot");
        Assert.GreaterOrEqual(rules.RangedAtOnceWith(50), 1, "someone may still shoot");
    }

    // The last minute's two peaks came a minute and a half apart; a walk had ended the breather at once.
    [Test]
    public void TheBreather_CannotBeWalkedOutOfAtOnce()
    {
        var pacing = new RaidIntensity();
        var random = new System.Random(3);
        float now = 0f;
        pacing.Hurt(1.2f);
        pacing.Tick(0.5f, 0, Vector3.zero, now += 0.5f, random);
        for (int i = 0; i < 80 && pacing.Now != RaidIntensity.Phase.Relax; i++)
        {
            pacing.Tick(1f, 0, Vector3.zero, now += 1f, random);
        }

        Assert.AreEqual(RaidIntensity.Phase.Relax, pacing.Now);
        float relaxed = now;
        for (int i = 0; i < 60 && pacing.Now == RaidIntensity.Phase.Relax; i++)
        {
            pacing.Tick(1f, 0, new Vector3(100f, 0f, 0f), now += 1f, random);
        }

        Assert.GreaterOrEqual(now - relaxed, RaidIntensity.MinRelax, "a walk ends it only after its least");
    }

    [Test]
    public void AStrikeFromBehind_IsTold_OneInFrontIsNot()
    {
        Assert.IsTrue(OffScreenWarnings.IsBehind(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -3f)), "straight behind");
        Assert.IsTrue(OffScreenWarnings.IsBehind(Vector3.zero, Vector3.forward, new Vector3(2f, 0f, -2.5f)), "behind, to one side");
        Assert.IsFalse(OffScreenWarnings.IsBehind(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 3f)), "in front");
        Assert.IsFalse(OffScreenWarnings.IsBehind(Vector3.zero, Vector3.forward, new Vector3(3f, 0f, 0f)), "at the side");
    }

    // Whirling Steel and Trollsbane's Wrath clear room in a pack: an ability shoves further than a heavy swing of the same weight.
    [Test]
    public void AnAbilitysBlow_ShovesFurtherThanAHeavySwing()
    {
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/Skills/WhirlingSteel.asset");
        var heavy = new DamageInfo(null, attack, 10f, DamageType.Physical, false, Vector3.zero, Vector3.forward, 0f, HitTechnique.Melee | HitTechnique.Heavy);
        var ability = new DamageInfo(null, attack, 10f, DamageType.Physical, false, Vector3.zero, Vector3.forward, 0f,
            HitTechnique.Melee | HitTechnique.Heavy | HitTechnique.Ability);
        var spell = new DamageInfo(null, attack, 10f, DamageType.Frost, false, Vector3.zero, Vector3.forward, 0f, HitTechnique.Spell | HitTechnique.Ability);
        var result = new DamageResult(10f, 10f, 10f, false, false, DamageType.Physical, GuardOutcome.None);
        float swing = HitReaction.KnockbackFor(heavy, result, null), skill = HitReaction.KnockbackFor(ability, result, null);
        Assert.Greater(skill, swing, "the ability shoves further");
        Assert.LessOrEqual(skill, Mathf.Max(CombatFeelSettings.Current.KnockbackMost, CombatFeelSettings.Current.KnockbackAbilityMost) + 1e-4f, "within its most");
        Assert.LessOrEqual(HitReaction.KnockbackFor(spell, result, null), CombatFeelSettings.Current.KnockbackMost + 1e-4f,
            "a spell's blow doesn't get the ability's shove (Winter's Breath would blow foes out of its cone)");
    }

    // Two healing draughts drunk in five hours.
    [Test]
    public void LowHealth_SaysWhichKeyHeals_OrThatTheBagHoldsHealing()
    {
        var draught = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HealingDraught.asset");
        Assert.IsTrue(LowHealthHint.Heals(draught));
        var bar = new SkillLoadout();
        Assert.IsNull(LowHealthHint.Words(bar, null, 100f), "nothing carried: nothing said");
        bar.EquipItem(6, draught);
        string words = LowHealthHint.Words(bar, null, 100f);
        StringAssert.Contains(draught.DisplayName, words);
        StringAssert.Contains(PlayerInputHandler.SkillKeyLabel(6), words, "the key on the bar");

        // A lesser healing earlier on the bar doesn't hide the draught (the review: "press 6 for your Baked Turnip").
        ItemDefinition lesser = null;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (LowHealthHint.Heals(item) && item.HealTotalFor(100f) < draught.HealTotalFor(100f))
            {
                lesser = item;
                break;
            }
        }

        Assert.IsNotNull(lesser, "some lesser healing exists to try");
        bar.EquipItem(5, lesser);
        StringAssert.Contains(draught.DisplayName, LowHealthHint.Words(bar, null, 100f), "the strongest is named");
    }

    // Through the real Damageable: an Old Moss Troll's 89 lands as the cap; a tick and a blow from nowhere pass as they are.
    [Test]
    public void ThroughTheDamageable_ACreaturesGreatBlowIsCapped_ATickIsNot()
    {
        var body = new GameObject("Shaped Player", typeof(Combatant));
        var troll = new GameObject("Probe Troll", typeof(Combatant), typeof(MonsterAI));
        try
        {
            var health = body.AddComponent<Damageable>();
            body.AddComponent<PlayerBlowGrace>();
            typeof(Damageable).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(health, null);
            float max = health.MaxHealth;
            CombatTechniqueSettings live = CombatTechniqueSettings.Current;
            DamageResult tick = health.ApplyDamage(new DamageInfo(troll.GetComponent<Combatant>(), null, max * 0.2f, DamageType.Fire, false,
                Vector3.zero, Vector3.forward, 0f, HitTechnique.OverTime));
            Assert.AreEqual(max * 0.2f, tick.HealthLost, max * 0.02f, "a tick passes as it is (no cap, no grace)");
            DamageResult stomp = health.ApplyDamage(new DamageInfo(troll.GetComponent<Combatant>(), null, max * 0.7f, DamageType.Physical, false,
                Vector3.zero, Vector3.forward, 0f, HitTechnique.None));
            Assert.LessOrEqual(stomp.HealthLost, max * live.HeaviestBlowShare + 0.01f, "the creature's great blow is held to the cap");
            Assert.IsTrue(health.IsAlive);
        }
        finally
        {
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(troll);
        }
    }

    [Test]
    public void TheHomecoming_PointsToTheNextWeapon_NotTheLast()
    {
        var copper = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Combat/Weapons/CopperSword.asset");
        var recipes = new List<RecipeDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition"))
        {
            recipes.Add(AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        RecipeDefinition next = Homecoming.BetterWeapon(copper, recipes);
        Assert.IsNotNull(next, "a stronger sword exists to make");
        WeaponDefinition made = next.Output.Weapon;
        Assert.AreEqual(copper.Stance, made.Stance, "of the same grip");
        Assert.AreEqual(copper.Signature, made.Signature, "and the same kind: a sword for a sword, not a staff or an axe");
        Assert.GreaterOrEqual(made.Damage, copper.Damage * Homecoming.BetterBy);
        foreach (RecipeDefinition recipe in recipes)
        {
            WeaponDefinition other = recipe != null && recipe.Output != null ? recipe.Output.Weapon : null;
            if (other != null && other.Kind == WeaponKind.Melee && other.Stance == copper.Stance && other.Signature == copper.Signature
                && other.Damage >= copper.Damage * Homecoming.BetterBy)
            {
                Assert.LessOrEqual(made.Damage, other.Damage, $"the next step, not {other.name}");
            }
        }

        Assert.IsNull(Homecoming.BetterWeapon(copper, new RecipeDefinition[0]), "none known: no line");
        Assert.AreNotEqual(next, Homecoming.BetterWeapon(copper, recipes, item => item == next.Output), "one already owned is not named");
    }

    [Test]
    public void TheDeathRecap_KeepsEveryBlowOfTheLastSeconds()
    {
        var recap = new DeathRecap();
        recap.Note(0f, "Old", 50f, 100f, 100f, false);
        recap.Note(20f, "ForestBittenWolf", 23f, 60f, 100f, false);
        recap.Note(21f, "ForestBittenWolf", 3f, 37f, 100f, false);
        recap.Note(22f, "MarshLight", 15f, 34f, 100f, false);
        recap.Note(22.5f, "unknown", 2f, 19f, 100f, true);
        Assert.AreEqual(43f, recap.LostWithin(23f), 1e-4f, "the blow 23 s before is gone");
        string words = recap.Describe(23f, 6);
        StringAssert.Contains("4 blows (1 small, 1 tick)", words);
        StringAssert.StartsWith("10 s: 4 blows", words);
        Assert.Less(words.IndexOf("ForestBittenWolf 26", System.StringComparison.Ordinal), words.IndexOf("MarshLight 15", System.StringComparison.Ordinal), "the most first");
        StringAssert.Contains("from 60/100", words);
        StringAssert.Contains("crowd 6", words);
    }
}
