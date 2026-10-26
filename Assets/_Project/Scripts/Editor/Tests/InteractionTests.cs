using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("GatheringProgression")]
public sealed class InteractionTests
{
    private readonly List<Object> created = new List<Object>();
    private readonly List<Interactable> registered = new List<Interactable>();

    [TearDown]
    public void TearDown()
    {
        foreach (Interactable target in registered)
        {
            if (target != null) Invoke(target, "OnDisable");
        }
        registered.Clear();
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void PreviewDoesNotStartWorkOrRaiseEvents()
    {
        Interactable target = Target(Vector3.zero);
        int started = 0;
        target.Started.AddListener(() => started++);
        for (int i = 0; i < 5; i++) Assert.IsTrue(target.CanInteract(null, out _));
        Assert.AreEqual(0, started);
        Assert.IsFalse(target.IsInUse);
        Assert.IsNull(target.Interactor);
        Assert.AreEqual(0f, target.Progress);
    }

    [Test]
    public void MissingToolPreviewAndTryBeginReturnTheSameReason()
    {
        ItemInventory bag = NewObject("User").AddComponent<ItemInventory>();
        Interactable target = ToolTarget();
        Assert.IsFalse(target.CanInteract(bag.gameObject, out string previewReason));
        Assert.IsFalse(target.TryBegin(bag.gameObject, out string startReason));
        Assert.AreEqual(previewReason, startReason);
        StringAssert.Contains("Tier II Pickaxe", previewReason);
        Assert.IsFalse(target.IsInUse);
    }

    [Test]
    public void CorrectToolInBagUnlocksAndRemovingItBlocksImmediately()
    {
        ItemInventory bag = NewObject("User").AddComponent<ItemInventory>();
        Interactable target = ToolTarget();
        ItemDefinition pick = Tool(ToolType.Pickaxe, 2);
        Assert.IsFalse(target.CanInteract(bag.gameObject, out _));
        Assert.AreEqual(1, bag.Container.Add(pick, 1));
        Assert.IsTrue(target.CanInteract(bag.gameObject, out _));
        Assert.IsFalse(target.IsInUse);
        bag.Container.RemoveAt(0, 1);
        Assert.IsFalse(target.CanInteract(bag.gameObject, out _));
    }

    [Test]
    public void WrongToolAndLowTierDoNotUnlockTheNode()
    {
        ItemInventory bag = NewObject("User").AddComponent<ItemInventory>();
        Interactable target = ToolTarget();
        bag.Container.Add(Tool(ToolType.Axe, 3), 1);
        bag.Container.Add(Tool(ToolType.Pickaxe, 1), 1);
        Assert.IsFalse(target.CanInteract(bag.gameObject, out _));
    }

    [TestCase(ToolType.Axe)]
    [TestCase(ToolType.Pickaxe)]
    public void TierOneNodesRequireAnActualTool(ToolType type)
    {
        ItemInventory bag = NewObject("User").AddComponent<ItemInventory>();
        Interactable target = ToolTarget(type, 1);
        Assert.AreEqual(0, ItemInventory.GatheringTier(bag.gameObject, type));
        Assert.IsFalse(target.CanInteract(bag.gameObject, out _));
        Assert.IsFalse(target.TryBegin(bag.gameObject, out _));
        bag.Container.Add(Tool(type, 1), 1);
        Assert.IsTrue(target.CanInteract(bag.gameObject, out _));
        bag.Container.RemoveAt(0, 1);
        Assert.IsFalse(target.CanInteract(bag.gameObject, out _));
    }

    [Test]
    public void BareHandsCanGatherToolFreeTierZeroNodes()
    {
        Interactable target = ToolTarget(ToolType.None, 1);
        Assert.IsTrue(target.CanInteract(null, out _));
    }

    [Test]
    public void TierOneAxeDoesNotUnlockTierTwoWood()
    {
        ItemInventory bag = NewObject("User").AddComponent<ItemInventory>();
        Interactable target = ToolTarget(ToolType.Axe, 2);
        bag.Container.Add(Tool(ToolType.Axe, 1), 1);
        Assert.IsFalse(target.CanInteract(bag.gameObject, out _));
        bag.Container.Add(Tool(ToolType.Axe, 2), 1);
        Assert.IsTrue(target.CanInteract(bag.gameObject, out _));
    }

    [Test]
    public void TierZeroHasItsOwnLabel()
    {
        Assert.AreEqual("0", Tiers.ToRoman(0));
        StringAssert.Contains("Tier 0", Tiers.RichLabel(0));
        Assert.AreEqual("I", Tiers.ToRoman(1));
    }

    [Test]
    public void ToolBeltMustContainAnEquippedToolForTierOneNodes()
    {
        GameObject user = NewObject("Equipped user");
        var equipment = user.AddComponent<PlayerEquipment>();
        var bag = user.GetComponent<ItemInventory>();
        Interactable target = ToolTarget(ToolType.Axe, 1);
        Assert.IsFalse(target.CanInteract(user, out _));
        bag.Container.Add(Tool(ToolType.Axe, 1), 1);
        Assert.IsFalse(target.CanInteract(user, out _));
        Assert.IsTrue(equipment.Gear.Equip(bag.Container, 0, out _));
        Assert.IsTrue(target.CanInteract(user, out _));
    }

    [Test]
    public void DisabledAndInUseTargetsAreNotReady()
    {
        Interactable target = Target(Vector3.zero);
        target.enabled = false;
        Assert.IsFalse(target.CanInteract(null, out _));
        target.enabled = true;
        Assert.IsTrue(target.TryBegin(null));
        Assert.IsFalse(target.CanInteract(null, out string reason));
        Assert.AreEqual("Already in use.", reason);
        target.End(InteractionResult.Cancelled);
        Assert.IsTrue(target.CanInteract(null, out _));
    }

    [Test]
    public void FocusPreviewsNearbyObjectsButDoesNotAllowOutOfReachInteraction()
    {
        PlayerInteractor user = Interactor();
        Interactable target = Target(new Vector3(4f, 0f, 0f));
        Assert.AreSame(target, user.FindFocus());
        Assert.IsFalse(user.CanInteractWith(target, out string reason));
        Assert.AreEqual("Move closer to interact.", reason);
        target.transform.position = new Vector3(1f, 0f, 0f);
        Assert.IsTrue(user.CanInteractWith(target, out _));
    }

    [Test]
    public void ReachableTargetWinsOverCloserPreviewOnlyTarget()
    {
        PlayerInteractor user = Interactor();
        Interactable preview = Target(new Vector3(2f, 0f, 0f));
        Interactable reachable = Target(new Vector3(3f, 0f, 0f));
        var data = new SerializedObject(reachable);
        data.FindProperty("range").floatValue = 4f;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreSame(reachable, user.FindFocus());
        reachable.enabled = false;
        Assert.AreSame(preview, user.FindFocus());
        preview.transform.position = new Vector3(10f, 0f, 0f);
        Assert.IsNull(user.FindFocus());
    }

    private Interactable ToolTarget(ToolType type = ToolType.Pickaxe, int tier = 2)
    {
        var definition = ScriptableObject.CreateInstance<GatheringNodeDefinition>();
        created.Add(definition);
        definition.name = "Test Ore";
        var data = new SerializedObject(definition);
        data.FindProperty("tier").intValue = type == ToolType.None ? 0 : tier;
        data.FindProperty("gatherTool").intValue = (int)type;
        data.FindProperty("requiredToolTier").intValue = tier;
        data.ApplyModifiedPropertiesWithoutUndo();
        Interactable target = Target(Vector3.zero);
        ResourceNode node = target.gameObject.AddComponent<ResourceNode>();
        var nodeData = new SerializedObject(node);
        nodeData.FindProperty("node").objectReferenceValue = definition;
        nodeData.ApplyModifiedPropertiesWithoutUndo();
        Invoke(node, "Awake");
        Invoke(target, "Awake");
        return target;
    }

    private ItemDefinition Tool(ToolType type, int tier)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(item);
        item.name = type + " " + tier;
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)type;
        data.FindProperty("toolTier").intValue = tier;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private Interactable Target(Vector3 position)
    {
        GameObject go = NewObject("Test Target");
        go.transform.position = position;
        Interactable target = go.AddComponent<Interactable>();
        Invoke(target, "Awake");
        Invoke(target, "OnEnable");
        registered.Add(target);
        return target;
    }

    private PlayerInteractor Interactor()
    {
        GameObject go = NewObject("Test Interactor");
        PlayerInteractor user = go.AddComponent<PlayerInteractor>();
        Invoke(user, "Awake");
        return user;
    }

    private GameObject NewObject(string name)
    {
        var go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
