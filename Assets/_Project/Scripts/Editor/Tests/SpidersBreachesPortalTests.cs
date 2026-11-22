using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The user's batch of 2026-11-22: the two black widows (the giant's web lasso, the small one at half size), legs a blade can take, the
/// Misty Woods' signature creatures; ward breaches everyone fights in, with more attackers, their own look and music; and the portal's
/// tier II raise introduced by Steady the Door before it is offered.
/// </summary>
public sealed class SpidersBreachesPortalTests
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object made in created)
        {
            Object.DestroyImmediate(made);
        }

        created.Clear();
    }

    private static MonsterDefinition Monster(string name) => AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + name + ".asset");

    // ----- The widows -----

    [Test]
    public void TheGiantLassoes_TheSmallOneDoesnt_AndIsHalfItsSize()
    {
        MonsterDefinition giant = Monster("GiantBlackWidow");
        MonsterDefinition small = Monster("BlackWidow");
        Assert.IsNotNull(giant);
        Assert.IsNotNull(small);
        Assert.AreEqual("Giant Black Widow", giant.DisplayName);
        Assert.AreEqual("Black Widow", small.DisplayName);
        Assert.AreNotEqual(giant.Id, small.Id);
        Assert.AreEqual("black_widow", giant.Id, "the giant keeps the id saves know");

        Assert.IsTrue(giant.Attacks.Any(a => a.attack != null && a.attack.LassoSeconds > 0f), "the giant's web lasso");
        // The lasso flies: it first had no projectile, so it never fired (the review, 2026-11-22).
        Assert.IsTrue(giant.Attacks.Where(a => a.attack != null && a.attack.LassoSeconds > 0f).All(a => a.projectilePrefab != null), "the lasso has its shot");
        Assert.IsFalse(small.Attacks.Any(a => a.attack != null && a.attack.LassoSeconds > 0f), "the small one only shoots its web");
        Assert.AreEqual(0.5f, small.Prefab.transform.localScale.y, 1e-3f, "half the giant's size");
        Assert.AreEqual(1f, giant.Prefab.transform.localScale.y, 1e-3f);
        Assert.AreSame(small, small.Prefab.GetComponent<MonsterAI>().Definition, "its prefab is its own");
        Assert.Less(Health(small), Health(giant) * 0.6f, "less health");
        // What it fights with is its prefab's: the copy first came up with the giant's 320 health (its Combatant kept the giant's stats).
        foreach (MonsterDefinition widow in new[] { giant, small })
        {
            var combatant = new SerializedObject(widow.Prefab.GetComponent<Combatant>());
            Assert.AreSame(widow.CombatStats, combatant.FindProperty("baseStats").objectReferenceValue, $"{widow.name}'s prefab fights with its own stats");
        }
        Assert.IsNotNull(GrabSettings.Current.LineMaterial, "the lasso's line is drawn");
    }

    private static float Health(MonsterDefinition monster)
    {
        var stats = new SerializedObject(monster.CombatStats).FindProperty("stats");
        return stats.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue;
    }

    [Test]
    public void TheMistyWoods_TheSmallWidowFromTierI_TheGiantFromTierII()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        GameObject giant = Monster("GiantBlackWidow").Prefab;
        GameObject small = Monster("BlackWidow").Prefab;
        List<SpawnTable.Entry> Creatures(int tier) => raid.SpawnLists.Where(list => list.category == SpawnCategory.Enemies)
            .SelectMany(list => list.EntriesFor(tier)).ToList();
        Assert.IsTrue(Creatures(1).Any(e => e.prefab == small), "the small widow at tier I");
        Assert.IsFalse(Creatures(1).Any(e => e.prefab == giant), "no giant at tier I");
        Assert.IsTrue(Creatures(2).Any(e => e.prefab == giant) && Creatures(2).Any(e => e.prefab == small), "both at tier II");

        // Nowhere else either: every spawn table and raid list (the roamers held the old widow at tier I, 2026-11-22).
        foreach (string guid in AssetDatabase.FindAssets("t:SpawnTable"))
        {
            var table = AssetDatabase.LoadAssetAtPath<SpawnTable>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsFalse(table.Entries.Any(e => e.prefab == giant && e.AppearsAt(1)), $"{table.name}: no giant at tier I");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var any = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsFalse(any.SpawnLists.SelectMany(list => list.EntriesFor(1)).Any(e => e.prefab == giant), $"{any.name}: no giant at tier I");
        }

        // The signature creatures are the most common at tier I.
        var tierOne = Creatures(1).GroupBy(e => e.prefab.name).ToDictionary(g => g.Key, g => g.Sum(e => e.weight));
        float signature = new[] { "WolfPack", "BlackWidow", "MarshLight", "SmallMossTroll" }.Sum(name => tierOne.TryGetValue(name, out float w) ? w : 0f);
        Assert.Greater(signature, tierOne.Values.Sum() * 0.8f, string.Join(", ", tierOne.Select(p => $"{p.Key} {p.Value}")));
    }

    // ----- Legs a blade can take -----

    private SpiderLegs Spider()
    {
        var root = new GameObject("Spider");
        created.Add(root);
        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        var body = new GameObject("Body").transform;
        body.SetParent(model, false);
        body.localPosition = Vector3.up;
        SpiderLegs legs = root.AddComponent<SpiderLegs>();
        Mesh mesh = SpiderLegs.SegmentMesh();
        created.Add(mesh);
        legs.Build(model, body, mesh, null, 2f, 1.5f);
        return legs;
    }

    [Test]
    public void ALegCutOff_FallsAway_AndASideKeepsHalfItsLegs()
    {
        SpiderLegs spider = Spider();
        int left = System.Array.FindIndex(spider.Legs, leg => leg.rest.x < 0f);
        Assert.IsTrue(spider.Sever(left), "the canary: a leg can go");
        Assert.AreEqual(1, spider.Severed);
        Assert.IsTrue(spider.Legs[left].segments.All(segment => segment == null), "its parts are gone from the body");
        Assert.IsFalse(spider.Sever(left), "not twice");

        var leftLegs = Enumerable.Range(0, spider.Legs.Length).Where(i => spider.Legs[i].rest.x < 0f).ToList();
        int cut = leftLegs.Count(i => spider.Sever(i));
        Assert.AreEqual(1, cut, "a side keeps half its legs");
        Assert.AreEqual(2, spider.Severed);
        foreach (GameObject piece in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Where(g => g.name == "Severed Leg"))
        {
            created.Add(piece);
        }
    }

    // ----- Breaches -----

    [Test]
    public void EveryoneFightsABreach_ButOnlyGuardsOtherwise()
    {
        DefenceSettings settings = DefenceSettings.Current;
        Assert.IsTrue(settings.EveryoneFightsBreaches);
        Assert.IsNotNull(settings.BreachFighter);
        var person = ScriptableObject.CreateInstance<ResidentDefinition>();
        created.Add(person);
        var record = new ResidentRecord(person);
        Assert.IsNull(GuardPosts.FightsAs(record, false, settings), "the canary: a horn or a night calls guards only");
        Assert.AreSame(settings.BreachFighter, GuardPosts.FightsAs(record, true, settings), "a breach calls everyone");
        record.IsAway = true;
        Assert.IsNull(GuardPosts.FightsAs(record, true, settings), "not those away");
    }

    [Test]
    public void MoreAttackers_AndMoreForEachDefender_WithinTheMost()
    {
        WardBreachSettings settings = WardBreachSettings.Current;
        int alone = settings.Fielded(6, 0, 0f);
        Assert.Greater(alone, 6, "more than the roster");
        Assert.Greater(settings.Fielded(6, 8, 0f), alone, "more with more defenders");
        Assert.Less(settings.Fielded(6, 0, 0.5f), alone, "a weakened source sends fewer");
        Assert.LessOrEqual(settings.Fielded(40, 40, 0f), 24);
        Assert.AreEqual(0, settings.Fielded(0, 5, 0f));
        // Past the roster, only what comes in numbers comes again: a wave's lone troll stays one.
        var wolf = new GameObject("Wolf");
        var troll = new GameObject("Troll");
        created.Add(wolf);
        created.Add(troll);
        var roster = new List<GameObject> { wolf, wolf, troll };
        Assert.IsTrue(WardBreachSettings.RankAndFile(roster).All(prefab => prefab == wolf));
        Assert.AreEqual(1, WardBreachSettings.RankAndFile(new List<GameObject> { troll }).Count, "the canary: a roster of ones comes round whole");
        Assert.IsNotNull(settings.BreachLook, "the Haven looks different under attack");
    }

    [Test]
    public void ABreach_HasItsOwnMusic()
    {
        var audio = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>("Assets/_Project/Resources/AudioSettings.asset");
        Assume.That(audio.Breach.HasTracks);
        MusicChoice calm = MusicRules.Choose(null, audio, AudioMood.Exploring, float.PositiveInfinity);
        MusicChoice breach = MusicRules.Choose(null, audio, AudioMood.Exploring, float.PositiveInfinity, false, true);
        Assert.AreSame(audio.Breach, breach.Playlist);
        Assert.AreNotSame(audio.Breach, calm.Playlist, "the canary");
        Assert.IsFalse(breach.Playlist.SameMusicAs(audio.DefaultCombat), "not the fight music it already had");
    }

    // ----- The portal's tier II -----

    [Test]
    public void TierII_IsOfferedOnlyOnceThePortalHasToldOfIt()
    {
        SettlementSettings.WardLevel first = SettlementSettings.Current.WardLevels[0];
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/SteadyTheDoor.asset");
        Assert.AreSame(quest, first.introducedBy);
        Assert.AreEqual(ObjectiveType.QuestEvent, quest.Objectives[0].Type, "it is heard first");
        Assert.AreEqual(ObjectiveType.RaisePortal, quest.Objectives[quest.Objectives.Count - 1].Type);
        GameSession.StartNewGame();
        try
        {
            Assert.IsFalse(PortalTiers.Introduced(first), "not before the quest");
            StringAssert.Contains("hear the portal out", PortalTiers.Missing(first, GameSession.Here), "shown, greyed, with why");
            GameSession.Quests.TryStart(quest, true);
            Assert.IsFalse(PortalTiers.Introduced(first), "not while the portal hasn't spoken");
            GameSession.Quests.AddProgress(quest, 0, 1);
            Assert.IsTrue(PortalTiers.Introduced(first), "once heard");
            Assert.IsTrue(PortalTiers.Introduced(SettlementSettings.Current.WardLevels[1]), "later tiers aren't held");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void RaisingThePortal_CountsTheWardsTier_NotThePocket()
    {
        QuestObjective raise = QuestObjective.Create(ObjectiveType.RaisePortal, 1);
        new SerializedObjectFor(raise).Tier(2);
        Assert.AreEqual(1, raise.Progress(new GameEvent(GameEventKind.PortalRaised, null, 2)));
        Assert.AreEqual(0, raise.Progress(new GameEvent(GameEventKind.PortalImproved, null, 1)), "the pocket's improvement isn't a tier");
    }

    // QuestObjective is a plain class: its tier is set the way the Inspector would.
    private sealed class SerializedObjectFor
    {
        private readonly QuestObjective objective;
        public SerializedObjectFor(QuestObjective objective) => this.objective = objective;
        public void Tier(int tier) => typeof(QuestObjective).GetField("tier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(objective, tier);
    }
}
