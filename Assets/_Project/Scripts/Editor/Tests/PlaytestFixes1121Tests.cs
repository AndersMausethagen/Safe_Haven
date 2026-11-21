using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// The user's playtest list of 2026-11-21, each rule checked where it lives: horses retuned (slower gallop, dearer, for horses tamed
/// before too), armour worn by blows, deep water kept to by swimmers only, "Purify all" paying only for what can be paid, decorations
/// learned from plans (found in raids or given by quests), the horse's tale asking for its lasso and barley, a lighter carry.
/// </summary>
public sealed class PlaytestFixes1121Tests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in made)
        {
            if (o != null)
            {
                Object.DestroyImmediate(o);
            }
        }

        made.Clear();
        GameSession.StartNewGame();
    }

    private static T Asset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/" + path);

    [Test]
    public void AHorseTamedBeforeTheRetune_GallopsAFifthSlower_AndOnlyOnce()
    {
        var old = new HorseStats { maxHealth = 160f, gallopSpeed = 12f, tuning = 0 };
        HorseStats retuned = HorseStats.Retuned(old);
        Assert.AreEqual(9.6f, retuned.gallopSpeed, 0.05f);
        Assert.AreEqual(HorseStats.CurrentTuning, retuned.tuning);
        Assert.AreEqual(retuned.gallopSpeed, HorseStats.Retuned(retuned).gallopSpeed, 0.001f, "retuned once, not on every load");

        var wild = Asset<HorseDefinition>("Data/Horses/Wild Horse.asset");
        HorseStats rolled = HorseStats.Roll(wild, new System.Random(3));
        Assert.AreEqual(HorseStats.CurrentTuning, rolled.tuning, "a new roll is already at the current tuning");
        Assert.AreEqual(rolled.gallopSpeed, HorseStats.Retuned(rolled).gallopSpeed, 0.001f);
        Assert.LessOrEqual(wild.GallopSpeed.y, 10.01f, "the gallop a fifth slower (12.5 → 10)");
        Assert.Less(wild.CanterSpeed, Mathf.Min(wild.GallopSpeed.x, wild.GallopSpeed.y), "a canter stays slower than any gallop");
        Assert.AreEqual(10.8f, wild.GallopCost, 0.01f, "a gallop costs a fifth more breath (9 → 10.8)");
    }

    [Test]
    public void TheBaseCarry_IsFortyPercentLighter()
    {
        var player = Asset<GameObject>("Prefabs/Characters/PlayerArmature.prefab");
        Assert.AreEqual(90f, player.GetComponent<ItemInventory>().CarryWeight, 0.01f, "150 less 40%");
    }

    [Test]
    public void ABlow_WearsTheArmourWorn_ABlockTheShield_AndBrokenArmourSaysSo()
    {
        DurabilitySettings settings = DurabilitySettings.Current;
        Assert.Greater(ArmourWear.WearFor(settings.ArmourWearReferenceDamage, settings), 0f);
        Assert.AreEqual(settings.ArmourWearPerHit, ArmourWear.WearFor(settings.ArmourWearReferenceDamage, settings), 1e-5f);
        Assert.AreEqual(settings.ArmourWearPerHit * 3f, ArmourWear.WearFor(settings.ArmourWearReferenceDamage * 10f, settings), 1e-5f, "a hard blow up to 3x");
        Assert.AreEqual(0f, ArmourWear.WearFor(0f, settings));

        ItemDefinition armour = ContentArmour();
        Assert.IsNotNull(armour, "some armour with durability ships");
        var slots = new ItemContainer(4, 1);
        Assert.AreEqual(-1, ArmourWear.PieceStruck(slots, 0.5f), "no armour worn: no piece is struck");
        slots.SetSlot(0, new ItemStack(armour, 1, 0.5f));
        Assert.AreEqual(0.4f, ArmourWear.Wear(slots, 0, 0.1f, false), 1e-4f);
        Assert.AreEqual(-1f, ArmourWear.Wear(slots, 0, 0.1f, true), "armour isn't a shield");
        Assert.AreEqual(0f, ArmourWear.Wear(slots, 0, 1f, false), 1e-4f, "worn to nothing");
        Assert.IsTrue(slots.GetSlot(0).IsBroken);
        Assert.AreEqual(-1f, ArmourWear.Wear(slots, 0, 0.1f, false), "broken armour wears no further");
    }

    private static ItemDefinition ContentArmour() =>
        Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).Items.FirstOrDefault(i => i != null && i.IsArmor && i.HasDurability);

    [Test]
    public void DeepWater_IsAnAreaOfItsOwn_KeptOutOfByThoseWhoDontSwim()
    {
        int area = NavMesh.GetAreaFromName(DeepWater.AreaName);
        Assert.GreaterOrEqual(area, 3, "Project Settings › Navigation › Areas has Water");
        Assert.AreEqual(0, DeepWater.MaskFor(NavMesh.AllAreas, false) & (1 << area), "a walker leaves deep water out");
        Assert.AreEqual(NavMesh.AllAreas, DeepWater.MaskFor(NavMesh.AllAreas, true), "a swimmer keeps it");
    }

    [Test]
    public void DeepWater_IsMarkedOverDeepGround_NotTheShallows()
    {
        // A flat terrain at 0 and a pond 2 m deep over half of it, 0.3 m (wading) over the other half; far off, clear of the open
        // scene's own terrain and water.
        Vector3 far = new Vector3(50000f, 0f, 50000f);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(64f, 10f, 64f) };
        made.Add(data);
        GameObject ground = Terrain.CreateTerrainGameObject(data);
        ground.transform.position = far;
        made.Add(ground);
        GameObject pond = MakeWater(far + new Vector3(16f, 2f, 32f), 24f);
        GameObject shallow = MakeWater(far + new Vector3(48f, 0.3f, 32f), 24f);
        var sources = new List<NavMeshBuildSource>();
        int boxes = DeepWater.AddSources(sources, new Bounds(far + new Vector3(32f, 0f, 32f), new Vector3(64f, 20f, 64f)));
        Assert.Greater(boxes, 0, "the deep pond is marked");
        Assert.IsTrue(sources.All(s => s.shape == NavMeshBuildSourceShape.ModifierBox && s.area == DeepWater.Area));
        Assert.IsTrue(sources.All(s => s.transform.GetColumn(3).x < far.x + 32f), "nothing over the wading water");
        Object.DestroyImmediate(pond);
        Object.DestroyImmediate(shallow);
    }

    private GameObject MakeWater(Vector3 centre, float size)
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        made.Add(water);
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.transform.position = centre;
        water.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        water.AddComponent<WaterSurface>();
        return water;
    }

    [Test]
    public void EveryDecoration_IsLearnedFromItsPlans_FoundOrGiven()
    {
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var decorations = AssetDatabase.FindAssets("t:BuildingDefinition", new[] { "Assets/_Project/Data" })
            .Select(g => AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(b => b != null && b.Decoration && b.name != "HavenBanner").ToList();
        Assert.GreaterOrEqual(decorations.Count, 12);
        var loot = AssetDatabase.FindAssets("t:LootTable", new[] { "Assets/_Project/Data" })
            .Select(g => AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(g))).Where(t => t != null).ToList();
        var given = content.Quests.Where(q => q != null).SelectMany(q => q.RewardItems).Select(r => r.Item).ToList();
        foreach (BuildingDefinition decoration in decorations)
        {
            Assert.IsFalse(decoration.KnownFromStart, decoration.name + " isn't known from the start (the user, 2026-11-21)");
            ItemDefinition plan = content.Items.FirstOrDefault(i => i != null && i.TeachesBuildings.Contains(decoration));
            Assert.IsNotNull(plan, decoration.name + " has plans");
            Assert.IsTrue(plan.IsLearnedAtHome, plan.name + " is read at home: a find must be carried out of the raid");
            bool found = loot.Any(t => t.Drops.Entries.Any(e => e.Item == plan));
            Assert.IsTrue(found || given.Contains(plan), plan.name + " can be found in a raid or is given by a quest");
        }
    }

    [Test]
    public void TheHorsesTale_AsksForItsLassoAndBarley_BeforeTheTaming()
    {
        var quest = Asset<QuestDefinition>("Data/Quests/AHorseOfYourOwn.asset");
        var lasso = Asset<ItemDefinition>("Data/Items/Lasso.asset");
        var barley = Asset<ItemDefinition>("Data/Items/Materials/Barley.asset");
        List<QuestObjective> steps = quest.Objectives.ToList();
        int rope = steps.FindIndex(o => o.Type == ObjectiveType.CollectItem && o.Item == lasso);
        int grain = steps.FindIndex(o => o.Type == ObjectiveType.CollectItem && o.Item == barley);
        int tame = steps.FindIndex(o => o.Type == ObjectiveType.QuestEvent && o.QuestEvent == HorseSettings.Current.HorseTamed);
        Assert.GreaterOrEqual(rope, 0);
        Assert.GreaterOrEqual(grain, 0);
        Assert.Greater(tame, Mathf.Max(rope, grain), "the lasso and the barley come first");
        Assert.IsTrue(steps[rope].CountAlreadyOwned && steps[grain].CountAlreadyOwned, "what is already carried counts");
        Assert.AreEqual(HorseSettings.Current.WildHorseMet, quest.StartEvent, "it starts as the first horse is told of (HorseHerd raises it)");
        StringAssert.Contains("Lasso", HorseSettings.Current.FirstHorseHowTo);
        StringAssert.Contains("Barley", HorseSettings.Current.FirstHorseHowTo);
    }

    [Test]
    public void TheHorsesTutorial_HasAPaintingOfItsOwn()
    {
        string listed = System.IO.File.ReadAllText("Tools/LocalSpawn/tutorial_art.json");
        StringAssert.Contains("\"Horses\"", listed, "a horse painting is listed for the tutorial (Tools/LocalSpawn/tutorial_art.json)");
    }
}
