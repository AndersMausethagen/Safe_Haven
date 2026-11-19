using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Carts, barrows and boats (GDD §8.5): what they cost in speed, what they hold, and that the content is wired up.</summary>
public sealed class TransportTests
{
    private const string Data = "Assets/_Project/Data/Transport/";
    private static readonly string[] Kinds = { "Wheelbarrow", "Handcart", "Rowboat" };

    private readonly List<Object> created = new List<Object>();

    private static TransportDefinition Authored(string name) => AssetDatabase.LoadAssetAtPath<TransportDefinition>($"{Data}{name}.asset");

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    [Test]
    public void Speed_IsSlightlyLessEmpty_LessStillFull_AndMuchLessOverfilled()
    {
        foreach (string name in Kinds)
        {
            TransportDefinition definition = Authored(name);
            float empty = definition.SpeedScale(0f);
            float full = definition.SpeedScale(definition.Capacity);
            float limit = definition.SpeedScale(definition.OverloadLimit);
            Assert.Less(empty, 1f, $"{name}: even empty it slows the player");
            Assert.Greater(empty, 0.85f, $"{name}: but only slightly");
            Assert.Less(full, empty, name);
            Assert.Less(limit, full * 0.8f, $"{name}: overfilling is a real price");

            float perWeightUnder = (empty - full) / definition.Capacity;
            float perWeightOver = (full - limit) / (definition.OverloadLimit - definition.Capacity);
            Assert.Greater(perWeightOver, perWeightUnder * 3f, $"{name}: the penalty steepens past the capacity");
        }
    }

    [Test]
    public void Speed_NeverRisesWithLoad()
    {
        TransportDefinition definition = Authored("Handcart");
        float last = 1f;
        for (float weight = 0f; weight <= definition.OverloadLimit + 20f; weight += 5f)
        {
            float scale = definition.SpeedScale(weight);
            Assert.LessOrEqual(scale, last + 0.0001f, $"at {weight}");
            last = scale;
        }
    }

    [Test]
    public void Overfilled_StartsJustPastTheCapacity()
    {
        TransportDefinition definition = Authored("Wheelbarrow");
        Assert.IsFalse(definition.IsOverfilled(definition.Capacity));
        Assert.IsTrue(definition.IsOverfilled(definition.Capacity + 1f));
    }

    [Test]
    public void Cargo_TakesWeightOffTheBag_UpToItsLimit()
    {
        ItemDefinition stone = Stone(5f);
        var bag = new ItemContainer(4, 1);
        bag.Add(stone, 8);
        var cargo = new ItemContainer(4, 1);
        cargo.SetWeightCapacity(25f);

        int moved = bag.TransferTo(0, cargo);

        Assert.AreEqual(5, moved, "only what the limit lets in");
        Assert.AreEqual(25f, cargo.CurrentWeight, 0.001f);
        Assert.AreEqual(15f, bag.CurrentWeight, 0.001f, "the bag is that much lighter");
        Assert.AreEqual(5, cargo.CountItems());
    }

    [Test]
    public void Cargo_SavesThroughSlots_WithWhatWasDoneToGear()
    {
        ItemDefinition stone = Stone(1f);
        ItemDefinition helm = Create<ItemDefinition>("helm");
        var data = new SerializedObject(helm);
        data.FindProperty("armorSlot").intValue = (int)ArmorSlot.Head;
        data.ApplyModifiedPropertiesWithoutUndo();
        var lookup = new Lookup();
        lookup.Items["stone"] = stone;
        lookup.Items["helm"] = helm;
        var cargo = new ItemContainer(4, 1);
        cargo.SetSlot(1, new ItemStack(stone, 7));
        cargo.SetSlot(3, new ItemStack(helm, 1, 0.5f, 2));

        string json = JsonUtility.ToJson(new Wrapper { slots = SaveMapper.ToSlots(cargo) });
        var read = new ItemContainer(4, 1);
        SaveMapper.ReadSlots(JsonUtility.FromJson<Wrapper>(json).slots, read, lookup, new List<string>());

        Assert.AreEqual(7, read.GetSlot(1).Count, "kept in its slot");
        Assert.AreEqual(helm, read.GetSlot(3).Item);
        Assert.AreEqual(0.5f, read.GetSlot(3).Durability, 0.001f);
        Assert.AreEqual(2, read.GetSlot(3).Empower);
    }

    [Test]
    public void NearbyCargo_CountsAsMaterials_AndIsSpentAfterTheBag()
    {
        ItemDefinition plank = Stone(1f);
        var bag = new ItemContainer(2, 1);
        bag.Add(plank, 2);
        var cart = new ItemContainer(2, 1);
        cart.Add(plank, 10);
        var stash = new ItemContainer(2, 1);
        stash.Add(plank, 5);
        var pool = new MaterialPool(null, bag, null, stash, new List<ItemContainer> { cart });

        Assert.AreEqual(17, pool.Have(plank));
        Assert.IsTrue(pool.TrySpend(null, new[] { new ItemAmount(plank, 8) }));
        Assert.AreEqual(0, bag.Count(plank), "the bag goes first");
        Assert.AreEqual(4, cart.Count(plank), "then the cargo");
        Assert.AreEqual(5, stash.Count(plank), "storage is left alone while the cargo can pay");

        Assert.IsTrue(pool.TrySpend(null, new[] { new ItemAmount(plank, 6) }));
        Assert.AreEqual(0, cart.Count(plank));
        Assert.AreEqual(3, stash.Count(plank), "and storage pays the rest");

        Assert.IsFalse(pool.TrySpend(null, new[] { new ItemAmount(plank, 4) }), "never half paid");
        Assert.AreEqual(3, stash.Count(plank));
    }

    [Test]
    public void RaidHud_SaysWhetherACartComesHome()
    {
        StringAssert.Contains("Comes home", TransportHud.RaidLine(true, 40f, 12f), "the hands always bring it");
        StringAssert.Contains("In reach", TransportHud.RaidLine(false, 12f, 12f), "right at the edge still counts");
        StringAssert.Contains("stays behind", TransportHud.RaidLine(false, 12.5f, 12f));
        StringAssert.Contains("40 m", TransportHud.RaidLine(false, 40f, 12f), "and it says how far");
    }

    [Test]
    public void TheRaidReport_SaysWhatBecameOfTheCart()
    {
        Assert.AreEqual("The Handcart came home with 30 items.", PlayerTransport.RaidEnding("Handcart", 30, true));
        Assert.AreEqual("The Handcart came home.", PlayerTransport.RaidEnding("Handcart", 0, true));
        Assert.AreEqual("The Wheelbarrow stayed behind, and 1 item with it.", PlayerTransport.RaidEnding("Wheelbarrow", 1, false));
        Assert.AreEqual("The Handcart stayed behind.", PlayerTransport.RaidEnding("Handcart", 0, false));
    }

    [Test]
    public void ACartsLoad_CountsAsAtRisk()
    {
        RaidRisk risk = new RaidRisk(2, 10, 5).WithCargo(30, 60);
        Assert.AreEqual(32, risk.Items);
        Assert.AreEqual(70, risk.Value);
        Assert.AreEqual(5, risk.Resources, "essences don't ride in the cart");
    }

    [Test]
    public void EveryTransport_HasItsSounds()
    {
        TransportSettings settings = TransportSettings.Current;
        foreach (SoundDefinition sound in new[] { settings.Rolling, settings.HullWater, settings.Creak, settings.OarStroke, settings.Bump })
        {
            Assert.IsNotNull(sound);
            Assert.IsTrue(sound.HasClips, sound.name);
            Assert.IsTrue(sound.Positional, $"{sound.name}: heard from where the cart is");
        }

        foreach (string name in Kinds)
        {
            Assert.IsNotNull(Authored(name).Prefab.GetComponent<TransportAudio>(), name);
        }
    }

    [Test]
    public void ThePile_ShowsTheLoad_AndHeapsWhenOverfilled()
    {
        Assert.AreEqual(0, CargoPile.PiecesFor(0f, 200f, 320f, 10, 15), "an empty cart looks empty");
        Assert.AreEqual(1, CargoPile.PiecesFor(0.5f, 200f, 320f, 10, 15), "anything at all shows");
        Assert.AreEqual(5, CargoPile.PiecesFor(100f, 200f, 320f, 10, 15));
        Assert.AreEqual(10, CargoPile.PiecesFor(200f, 200f, 320f, 10, 15), "the heap starts past the capacity");
        Assert.AreEqual(11, CargoPile.PiecesFor(201f, 200f, 320f, 10, 15));
        Assert.AreEqual(15, CargoPile.PiecesFor(320f, 200f, 320f, 10, 15));
        Assert.AreEqual(15, CargoPile.PiecesFor(900f, 200f, 320f, 10, 15), "never more than there are");
    }

    [Test]
    public void EveryTransport_HasAPile()
    {
        foreach (string name in Kinds)
        {
            Assert.IsNotNull(Authored(name).Prefab.GetComponent<CargoPile>(), name);
        }
    }

    [Test]
    public void ABite_TakesFromOneStack_AndNeverMoreThanThere()
    {
        ItemDefinition stone = Stone(1f);
        ItemDefinition wood = Stone(1f);
        var cargo = new ItemContainer(4, 1);
        cargo.SetSlot(1, new ItemStack(stone, 10));
        cargo.SetSlot(3, new ItemStack(wood, 2));

        Assert.AreEqual(3, CartScavenging.Bite(cargo, 3, 0f));
        Assert.AreEqual(7, cargo.GetSlot(1).Count, "the first stack, at a low pick");
        Assert.AreEqual(2, CartScavenging.Bite(cargo, 3, 0.99f), "the last stack holds only two");
        Assert.IsTrue(cargo.GetSlot(3).IsEmpty);
        Assert.AreEqual(3, CartScavenging.Bite(cargo, 3, 1f), "a pick of exactly 1 still lands on a stack");
        Assert.AreEqual(0, CartScavenging.Bite(new ItemContainer(2, 1), 3, 0.5f), "nothing to take");
    }

    [Test]
    public void OnlyCarts_AreScavenged()
    {
        Assert.IsNotNull(Authored("Handcart").Prefab.GetComponent<CartScavenging>());
        Assert.IsNotNull(Authored("Wheelbarrow").Prefab.GetComponent<CartScavenging>());
        Assert.IsNull(Authored("Rowboat").Prefab.GetComponent<CartScavenging>(), "a boat stays out on the water");
    }

    [Test]
    public void Hauling_CostsNothingOnTheFlat_UntilOverfilledOrUphill()
    {
        TransportSettings settings = TransportSettings.Current;
        Assert.AreEqual(0f, settings.HaulEffort(0f, 1f, 0f), "a full cart on the flat is free: only effort is paid for");
        Assert.AreEqual(0f, settings.HaulEffort(0f, 0.3f, 0f));
        Assert.Greater(settings.HaulEffort(0.5f, 1.2f, 0f), 0f, "overfilled costs even on the flat");
        Assert.Greater(settings.HaulEffort(1f, 1.5f, 0f), settings.HaulEffort(0.5f, 1.2f, 0f), "and more the further past it");

        float light = settings.HaulEffort(0f, 0.1f, 0.3f);
        float heavy = settings.HaulEffort(0f, 1f, 0.3f);
        Assert.Greater(light, 0f, "a slope costs anything");
        Assert.Greater(heavy, light * 2f, "and a full load far more");
        Assert.Greater(settings.HaulEffort(0f, 1f, 0.6f), heavy, "the steeper the dearer");
    }

    [Test]
    public void ARiver_RunsAlongItsLine_AndSlowsAtTheBanks()
    {
        var line = new[] { new Vector4(0f, 5f, 0f, 4f), new Vector4(0f, 4f, 10f, 4f), new Vector4(10f, 3f, 10f, 4f) };

        Vector2 middle = WaterSurface.FlowOnCentreline(line, new Vector2(0f, 5f));
        Assert.AreEqual(0f, middle.x, 0.001f);
        Assert.AreEqual(1f, middle.y, 0.001f, "full speed in the middle, the way the line runs");

        Vector2 after = WaterSurface.FlowOnCentreline(line, new Vector2(5f, 10f));
        Assert.AreEqual(1f, after.x, 0.001f, "round the bend it runs the new way");

        float bank = WaterSurface.FlowOnCentreline(line, new Vector2(4f, 5f)).magnitude;
        Assert.AreEqual(1f / 3f, bank, 0.001f, "a third of it at the bank");
        float between = WaterSurface.FlowOnCentreline(line, new Vector2(2f, 5f)).magnitude;
        Assert.That(between, Is.GreaterThan(bank).And.LessThan(1f));
    }

    [Test]
    public void AuthoredTransports_AreWiredUp()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (string name in Kinds)
        {
            TransportDefinition definition = Authored(name);
            Assert.IsNotNull(definition.Prefab, name);
            Assert.IsNotNull(definition.Kit, name);
            Assert.AreEqual(definition, definition.Kit.Transport, $"{name}: the kit sets down the same thing it packs into");
            Assert.IsTrue(definition.Kit.IsUsable, name);
            Assert.AreEqual(1, definition.Kit.MaximumStack, $"{name}: a kit is one object");
            Assert.Greater(definition.OverloadLimit, definition.Capacity, $"{name}: there has to be room to overfill it");

            GameObject prefab = definition.Prefab.gameObject;
            Assert.IsNotNull(prefab.GetComponent<Rigidbody>(), name);
            Assert.IsNotNull(prefab.GetComponent<Transport>().Definition, name);
            Assert.IsNotNull(prefab.GetComponent<ITransportDriver>(), name);
            Assert.IsNotEmpty(definition.Prefab.PrefabId, $"{name}: without it a placed transport can't come back from a save");
            Assert.AreEqual(definition.Prefab, content.FindPrefab(definition.Prefab.PrefabId), $"{name}: saves recreate it from here");
            Assert.IsNotNull(content.FindItem(definition.Kit.Id), name);
            Assert.AreEqual(InteractionKind.Use, prefab.GetComponent<Interactable>().Kind, name);
            foreach (Collider part in prefab.GetComponentsInChildren<Collider>(true))
            {
                Assert.AreEqual(2, part.gameObject.layer, $"{name}/{part.name}: its own wheels must not find it as ground");
            }
        }
    }

    [Test]
    public void WheeledTransports_RollOnFrictionlessWheels()
    {
        foreach (string name in new[] { "Wheelbarrow", "Handcart" })
        {
            GameObject prefab = Authored(name).Prefab.gameObject;
            SphereCollider[] wheels = prefab.GetComponentsInChildren<SphereCollider>(true);
            Assert.IsNotEmpty(wheels, name);
            foreach (SphereCollider wheel in wheels)
            {
                Assert.AreEqual(0f, wheel.sharedMaterial.dynamicFriction, $"{name}: Haulable gives the wheels their grip");
            }
        }
    }

    [Test]
    public void TheYard_BuildsThemFromPlanks()
    {
        var yard = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/WainwrightsYard.asset");
        Assert.IsTrue(yard.ShowInBuildMenu);
        Assert.AreEqual(CraftingStationType.Wainwright, yard.Prefab.GetComponent<CraftingStation>().Type);
        Assert.AreEqual("Wainwright's Yard", CraftingStationType.Wainwright.DisplayName());
        foreach (string name in Kinds)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>($"Assets/_Project/Data/Crafting/Recipes/{name}.asset");
            Assert.AreEqual(CraftingStationType.Wainwright, recipe.Station, name);
            Assert.AreEqual(Authored(name).Kit, recipe.Output, name);
            Assert.IsTrue(recipe.ItemCost.Any(cost => cost.Item != null && (cost.Item.name == "RoughPlank" || cost.Item.name == "OakPlank")), $"{name} is made of planks");
        }
    }

    [Test]
    public void ThePlayer_CanMoveTransport()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        Assert.IsNotNull(player.GetComponent<PlayerTransport>());
        Assert.IsNotNull(player.GetComponent<PlayerTransportPose>());
    }

    [Test]
    public void AtTheOars_NothingFights_AndInteractGetsOut()
    {
        var holder = new GameObject("player");
        created.Add(holder);
        var state = holder.AddComponent<PlayerStateMachine>();
        int attacks = 0;
        int dodges = 0;
        int skills = 0;
        int exits = 0;
        int boards = 0;
        state.AttackRequested += () => attacks++;
        state.DodgeRequested += () => dodges++;
        state.SkillRequested += slot => skills++;
        state.AboardInteraction += () => exits++;
        state.SwimmingInteraction += () => boards++;

        state.SetAboard(true);
        state.RequestAttack();
        state.RequestDodge();
        state.RequestSkill(0);
        state.RequestInteraction();
        Assert.AreEqual(0, attacks + dodges + skills);
        Assert.AreEqual(1, exits);

        state.SetAboard(false);
        state.RequestAttack();
        Assert.AreEqual(1, attacks, "out of the boat, the hands are free again");

        state.SetSwimming(true);
        state.RequestInteraction();
        Assert.AreEqual(1, boards, "a swimmer can reach a boat that drifted off");
    }

    [System.Serializable]
    private struct Wrapper
    {
        public List<SlotSaveData> slots;
    }

    private sealed class Lookup : IContentLookup
    {
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();

        public ItemDefinition FindItem(string id) => id != null && Items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
    }

    private ItemDefinition Stone(float weight)
    {
        ItemDefinition item = Create<ItemDefinition>("stone");
        var data = new SerializedObject(item);
        data.FindProperty("weight").floatValue = weight;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
