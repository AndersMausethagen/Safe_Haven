using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

public sealed class DynamicQuestTests
{
    private readonly List<Object> created = new List<Object>();
    private T Create<T>() where T : ScriptableObject
    {
        var value = ScriptableObject.CreateInstance<T>();
        created.Add(value);
        return value;
    }

    private static void Edit(Object value, System.Action<SerializedObject> apply)
    {
        var data = new SerializedObject(value);
        apply(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private ItemDefinition Weapon(WeaponStance stance, WeaponKind kind = WeaponKind.Melee)
    {
        var weapon = Create<WeaponDefinition>();
        Edit(weapon, d => {
            d.FindProperty("stance").enumValueIndex = (int)stance;
            d.FindProperty("kind").enumValueIndex = (int)kind;
        });
        var item = Create<ItemDefinition>();
        Edit(item, d => d.FindProperty("weapon").objectReferenceValue = weapon);
        return item;
    }

    [TearDown]
    public void Cleanup() => TestCleanup.Destroy(created);

    [Test]
    public void GearIndices_PreserveExistingSaves()
    {
        Assert.AreEqual(12, (int)GearSlot.MainHand);
        Assert.AreEqual(16, (int)GearSlot.SickleTool);
        Assert.AreEqual(17, (int)GearSlot.OffHand);
        Assert.AreEqual(18, (int)GearSlot.Light, "new slots go at the end");
        Assert.AreEqual(19, GearSlots.Count);
    }

    [TestCase(WeaponStance.TwoHanded)]
    [TestCase(WeaponStance.Polearm)]
    public void TorchAndBothHandWeapon_AreRejectedInEitherEquipOrderAndDrag(WeaponStance stance)
    {
        var torch = Weapon(WeaponStance.OneHanded, WeaponKind.Torch);
        var large = Weapon(stance);
        var bag = new ItemContainer(4, 4);
        var gear = new GearSet();
        bag.SetSlot(0, new ItemStack(torch, 1));
        bag.SetSlot(1, new ItemStack(large, 1));
        Assert.IsTrue(gear.EquipTo(bag, 0, GearSlot.OffHand, out _));
        Assert.IsFalse(gear.EquipTo(bag, 1, GearSlot.MainHand, out string reason));
        StringAssert.Contains("off-hand", reason);
        Assert.IsFalse(bag.MoveTo(1, gear.Slots, (int)GearSlot.MainHand));
        Assert.AreEqual(large, bag.GetSlot(1).Item);
        Assert.IsTrue(gear.Unequip(GearSlot.OffHand, bag, out _));
        Assert.IsTrue(gear.EquipTo(bag, 1, GearSlot.MainHand, out _));
        Assert.IsFalse(gear.EquipTo(bag, 0, GearSlot.OffHand, out reason));
        StringAssert.Contains("two-handed", reason);
        Assert.IsFalse(bag.MoveTo(0, gear.Slots, (int)GearSlot.OffHand));
        Assert.AreEqual(torch, bag.GetSlot(0).Item);
    }

    [Test]
    public void AutoEquip_KeepsTorchInBagBesideTwoHandedWeapon()
    {
        var bag = new ItemContainer(4, 4);
        var gear = new GearSet();
        var torch = Weapon(WeaponStance.OneHanded, WeaponKind.Torch);
        bag.SetSlot(0, new ItemStack(Weapon(WeaponStance.TwoHanded), 1));
        bag.SetSlot(1, new ItemStack(torch, 1));
        Assert.AreEqual(1, gear.AutoEquipFrom(bag));
        Assert.IsTrue(gear.Get(GearSlot.OffHand).IsEmpty);
        Assert.AreEqual(torch, bag.GetSlot(1).Item);
    }

    [Test]
    public void AutoEquip_OneHandedAxeAndTorchUseDifferentHands()
    {
        var gear = new GearSet();
        var bag = new ItemContainer(4, 4);
        var axe = Weapon(WeaponStance.OneHanded);
        var torch = Weapon(WeaponStance.OneHanded, WeaponKind.Torch);
        bag.SetSlot(0, new ItemStack(axe, 1));
        bag.SetSlot(1, new ItemStack(torch, 1));
        Assert.AreEqual(2, gear.AutoEquipFrom(bag));
        Assert.AreSame(axe, gear.Get(GearSlot.MainHand).Item);
        Assert.AreSame(torch, gear.Get(GearSlot.OffHand).Item);
        Assert.IsFalse(GearSlots.Accepts(GearSlot.OffHand, axe));
    }

    [Test]
    public void HeldTorch_PreservesOnlyItsSafeLightComponents()
    {
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(model);
        var light = model.AddComponent<Light>();
        light.type = LightType.Point;
        model.AddComponent<LightFlicker>();
        model.AddComponent<GrassInteractor>();
        HeldItemView.StripToVisual(model, true);
        Assert.NotNull(model.GetComponent<Light>());
        Assert.NotNull(model.GetComponent<LightFlicker>());
        Assert.NotNull(model.GetComponent<AtmosphereLight>());
        HeldItemView.StripToVisual(model, true);
        Assert.AreEqual(1, model.GetComponents<AtmosphereLight>().Length);
        Assert.IsNull(model.GetComponent<GrassInteractor>());
        Assert.IsNull(model.GetComponent<Collider>());
        HeldItemView.StripToVisual(model);
        Assert.IsNull(model.GetComponent<AtmosphereLight>());
        Assert.IsNull(model.GetComponent<LightFlicker>());
        Assert.IsNull(model.GetComponent<Light>());
    }

    [Test]
    public void SilentScene_BlocksFallbackCombatAndFinalMusicUntilAnEvent()
    {
        var host = new GameObject("Silent music test");
        created.Add(host);
        var audio = host.AddComponent<SceneAudio>();
        var defaults = Create<AudioSettingsDefinition>();
        var clip = AudioClip.Create("test", 100, 1, 22050, false);
        created.Add(clip);
        defaults.DefaultExploring.tracks.Add(new MusicTrack(clip, 1f));
        defaults.DefaultCombat.tracks.Add(new MusicTrack(clip, 1f));
        audio.FinalStretch.tracks.Add(new MusicTrack(clip, 1f));
        Edit(audio, d => d.FindProperty("startSilent").boolValue = true);
        Assert.IsNull(MusicRules.Choose(audio, defaults, AudioMood.Exploring, float.PositiveInfinity).Playlist);
        Assert.IsNull(MusicRules.Choose(audio, defaults, AudioMood.Exploring, 1f).Playlist, "Silence also holds the final stretch.");
        Assert.AreSame(defaults.DefaultCombat, MusicRules.Choose(audio, defaults, AudioMood.Combat, 1f).Playlist,
            "A fight brings combat music even in a silent scene.");
        var eventMusic = new MusicPlaylist();
        eventMusic.tracks.Add(new MusicTrack(clip, .7f));
        audio.PlayEventMusic(eventMusic);
        Assert.AreSame(eventMusic, MusicRules.Choose(audio, defaults, AudioMood.Combat, 1f).Playlist);
        // A place's mood music (the prologue's dark forest) lets a fight's music take over, and comes back after it.
        audio.PlayEventMusic(eventMusic, true);
        Assert.AreSame(defaults.DefaultCombat, MusicRules.Choose(audio, defaults, AudioMood.Combat, 1f).Playlist);
        Assert.AreSame(eventMusic, MusicRules.Choose(audio, defaults, AudioMood.Exploring, 1f).Playlist);
        audio.SilenceMusic();
        Assert.IsNull(MusicRules.Choose(audio, defaults, AudioMood.Exploring, 1f).Playlist);
        Assert.AreSame(defaults.DefaultCombat, MusicRules.Choose(audio, defaults, AudioMood.Combat, 1f).Playlist);
        audio.ResumeMusic();
        Assert.AreSame(defaults.DefaultExploring, MusicRules.Choose(audio, defaults, AudioMood.Exploring, float.PositiveInfinity).Playlist);
    }

    [Test]
    public void CustomEvent_AppliesAuthoredObjectAndParameterActions()
    {
        var host = new GameObject("Test listener");
        var target = new GameObject("Test target");
        created.Add(host);
        created.Add(target);
        var light = target.AddComponent<Light>();
        var gameEvent = Create<GameEventDefinition>();
        var listener = host.AddComponent<GameEventListener>();
        Edit(listener, d => {
            d.FindProperty("gameEvent").objectReferenceValue = gameEvent;
            var actions = d.FindProperty("actions");
            actions.arraySize = 2;
            var action = actions.GetArrayElementAtIndex(0);
            action.FindPropertyRelative("kind").enumValueIndex = (int)SceneEventActionKind.SetObjectActive;
            action.FindPropertyRelative("target").objectReferenceValue = target;
            action.FindPropertyRelative("active").boolValue = false;
            actions.GetArrayElementAtIndex(1).FindPropertyRelative("kind").enumValueIndex = (int)SceneEventActionKind.Parameters;
        });
        var field = typeof(GameEventListener).GetField("actions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var actionsValue = (SceneEventAction[])field.GetValue(listener);
        var parameterField = typeof(SceneEventAction).GetField("parameters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var response = (UnityEvent)parameterField.GetValue(actionsValue[1]);
        var setter = (UnityAction<float>)System.Delegate.CreateDelegate(typeof(UnityAction<float>), light,
            typeof(Light).GetProperty("intensity").SetMethod);
        UnityEventTools.AddFloatPersistentListener(response, setter, 4f);
        response.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
        // Edit Mode does not run MonoBehaviour enable callbacks.
        typeof(GameEventListener).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(listener, null);
        try
        {
            gameEvent.Raise();
            Assert.IsFalse(target.activeSelf);
            Assert.AreEqual(4f, light.intensity);
        }
        finally
        {
            typeof(GameEventListener).GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(listener, null);
        }
    }

    [Test]
    public void AuthoredPrologue_UsesRegisteredCraftableTorchAndOneHandedAxe()
    {
        // The weapon and the torch come in the scene's story at the forest's edge: the axe leaves a hand for the torch, the bow does not (L swaps them).
        var axe = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/WoodsmansAxe.asset");
        Assert.AreEqual(WeaponStance.OneHanded, axe.Weapon.Stance);
        Assert.IsTrue(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeWoodenBow.asset").Weapon.RequiresBothHands);
        var torch = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HandTorch.asset");
        Assert.AreEqual(WeaponKind.Torch, torch.Weapon.Kind);
        Assert.IsTrue(torch.Weapon.CanUseOffHand);
        Assert.IsEmpty(ItemPrefabBuilder.FindProblems(torch.Prefab, torch));
        var light = torch.HeldModel.GetComponentInChildren<Light>(true);
        Assert.GreaterOrEqual(light.intensity, 4f);
        Assert.GreaterOrEqual(light.range, 9f);
        Assert.NotNull(light.GetComponent<LightFlicker>());
        Assert.NotNull(torch.Icon);
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/HandTorch.asset");
        Assert.AreSame(torch, recipe.Output);
        Assert.IsTrue(recipe.KnownFromStart && recipe.IsHandcrafted);
        Assert.AreEqual(2, recipe.ItemCost.Sum(c => c.Amount));
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Assert.NotNull(book);
        Assert.Contains(recipe, book.Recipes.ToList());
    }

    [Test]
    public void SavedPrologue_HasSilentOpeningAndPortalMusicReactionNotPlayerTorchWorkaround()
    {
        var preview = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity");
        try
        {
            var components = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            var audio = components.OfType<SceneAudio>().Single();
            Assert.IsTrue(new SerializedObject(audio).FindProperty("startSilent").boolValue);
            Assert.AreEqual(7, components.OfType<ExpeditionTorch>().Count());
            Assert.IsFalse(components.OfType<Player>().Single().GetComponentsInChildren<ExpeditionTorch>(true).Any());
            var definition = AssetDatabase.LoadAssetAtPath<PrologueDefinition>("Assets/_Project/Data/Prologue/ForestPrologue.asset");
            var hold = definition.Chapters.Single(c => c.Behaviour == PrologueStep.HoldPortal).Quest;
            var reaction = components.OfType<GameEventListener>().Single(l =>
                new SerializedObject(l).FindProperty("actions").arraySize > 0 && new SerializedObject(l).FindProperty("quest").objectReferenceValue == hold);
            var data = new SerializedObject(reaction);
            Assert.IsTrue(data.FindProperty("respondIfAlreadyHappened").boolValue, "Restore countdown music on checkpoint loads.");
            Assert.AreEqual((int)ListenFor.QuestStarted, data.FindProperty("listenFor").enumValueIndex);
            var action = data.FindProperty("actions").GetArrayElementAtIndex(0);
            Assert.AreEqual((int)SceneEventActionKind.PlayMusic, action.FindPropertyRelative("kind").enumValueIndex);
            Assert.AreSame(audio, action.FindPropertyRelative("sceneAudio").objectReferenceValue);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
