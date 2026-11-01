using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Picking a chest's lock, and the player prefab and animator these features stand on.</summary>
public sealed class LockpickingTests
{
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";

    private static LockSession Session(int pins = 3, int slips = 3, float narrowing = 0.1f) =>
        new LockSession(pins, slips, 1f, 0.4f, 0.1f, narrowing, 0f, new System.Random(1));

    // Puts the marker at a place on the bar: half a cycle reaches the top.
    private static void MoveMarkerTo(LockSession session, float position) => session.Tick(position * 0.5f);

    private static void PressAt(LockSession session, float position)
    {
        MoveMarkerTo(session, position);
        session.Press();
    }

    [Test]
    public void PinsSetInTheZones_PickTheLock()
    {
        LockSession session = Session(2);
        PressAt(session, 0.5f);
        Assert.AreEqual(1, session.PinsSet);
        Assert.IsFalse(session.IsDone);
        session.Resolve(SwingResult.Normal);
        Assert.IsTrue(session.IsPicked);
        Assert.IsFalse(session.IsForced);
    }

    [Test]
    public void Green_IsCriticalAndYellow_IsNormal()
    {
        LockSession session = Session();
        MoveMarkerTo(session, 0.5f);
        Assert.AreEqual(SwingResult.Critical, session.Zones.Evaluate(session.Position));
        Assert.AreEqual(SwingResult.Normal, session.Zones.Evaluate(0.65f));
        Assert.AreEqual(SwingResult.Glancing, session.Zones.Evaluate(0.95f));
    }

    [Test]
    public void ASlip_DropsAPin_ButNeverBelowNone()
    {
        LockSession session = Session();
        session.Resolve(SwingResult.Normal);
        session.Resolve(SwingResult.Normal);
        session.Resolve(SwingResult.Glancing);
        Assert.AreEqual(1, session.PinsSet);
        Assert.AreEqual(1, session.Slips);
        session.Resolve(SwingResult.Glancing);
        Assert.AreEqual(0, session.PinsSet);
    }

    [Test]
    public void TooManySlips_ForceTheLock_SoAChestAlwaysOpens()
    {
        LockSession session = Session(slips: 2);
        session.Resolve(SwingResult.Glancing);
        Assert.IsFalse(session.IsDone);
        session.Resolve(SwingResult.Glancing);
        Assert.IsTrue(session.IsForced);
        Assert.IsFalse(session.IsPicked);
        Assert.IsTrue(session.IsDone);
    }

    [Test]
    public void EveryPinSet_NarrowsTheZones()
    {
        LockSession session = Session(5, narrowing: 0.2f);
        float first = session.Zones.NormalWidth;
        session.Resolve(SwingResult.Normal);
        session.Resolve(SwingResult.Normal);
        Assert.AreEqual(first * 0.6f, session.Zones.NormalWidth, 1e-4f);
    }

    [Test]
    public void TheMarker_NeverTimesOut()
    {
        LockSession session = Session();
        for (int i = 0; i < 100; i++)
        {
            session.Tick(0.37f);
        }

        Assert.AreEqual(0, session.Slips, "waiting for the right moment costs only time");
        Assert.AreEqual(0, session.PinsSet);
        Assert.That(session.Position, Is.InRange(0f, 1f));
    }

    [Test]
    public void ALootCache_IsLockedUntilOpened_UnlessItHasNoPins()
    {
        var chest = new GameObject("Chest", typeof(Interactable), typeof(LootCache));
        var pack = new GameObject("Pack", typeof(Interactable), typeof(LootCache));
        try
        {
            var cache = chest.GetComponent<LootCache>();
            Assert.IsTrue(cache.IsLocked, "a chest is locked unless it says otherwise");
            Assert.AreEqual("Pick the lock", cache.Verb);
            cache.Unlock(false);
            Assert.IsFalse(cache.IsLocked);
            Assert.AreEqual("Search", cache.Verb);

            var serialized = new SerializedObject(pack.GetComponent<LootCache>());
            serialized.FindProperty("lockPins").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(pack.GetComponent<LootCache>().IsLocked, "a pack without a lock");
        }
        finally
        {
            Object.DestroyImmediate(chest);
            Object.DestroyImmediate(pack);
        }
    }

    [Test]
    public void TheSupplyCache_IsAWorkInteraction_ThatTheLockCanHoldOpen()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/SupplyCache.prefab");
        var cache = prefab.GetComponentInChildren<LootCache>(true);
        Assert.Greater(cache.LockPins, 0);
        Assert.AreEqual(InteractionKind.Work, cache.GetComponent<Interactable>().Kind, "a Use completes on the press, skipping the lock");
    }

    [Test]
    public void ThePlayerPrefab_HasTheNewParts()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        Assert.IsNotNull(player.GetComponent<PlayerNoise>());
        Assert.IsNotNull(player.GetComponent<PlayerLockpicking>());
    }

    [Test]
    public void TheSettingsAsset_ExistsWithItsSounds()
    {
        var settings = Resources.Load<LockpickingSettings>(LockpickingSettings.ResourceName);
        Assert.IsNotNull(settings, "without the asset the sounds would be missing");
        Assert.IsNotNull(settings.SoundOf(SwingResult.Normal));
        Assert.IsNotNull(settings.SoundOf(SwingResult.Glancing));
        Assert.IsNotNull(settings.ForcedSound);
        Assert.IsNotNull(settings.OpenSound);
        Assert.Less(settings.NoiseOf(SwingResult.Critical), settings.NoiseOf(SwingResult.Normal));
        Assert.Less(settings.NoiseOf(SwingResult.Normal), settings.NoiseOf(SwingResult.Glancing));
        Assert.Less(settings.NoiseOf(SwingResult.Glancing), settings.ForcedNoise);
    }

    [Test]
    public void TheSneakLayer_SitsRightAboveBase_AndBlendsTheCrouchClips()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        Assert.AreEqual(CombatAnimationSetup.SneakLayer, controller.layers[1].name, "under every action layer");
        CollectionAssert.Contains(controller.parameters.Select(p => p.name).ToList(), "Sneaking");

        AnimatorControllerLayer layer = controller.layers[1];
        Assert.AreEqual(1f, layer.defaultWeight);
        Assert.IsNull(layer.avatarMask, "the whole body crouches");
        Assert.AreEqual("Empty", layer.stateMachine.defaultState.name, "nothing shows unless sneaking");

        AnimatorState sneak = layer.stateMachine.states.Select(s => s.state).First(s => s.name == "Sneak");
        var tree = (BlendTree)sneak.motion;
        Assert.AreEqual("Speed", tree.blendParameter);
        CollectionAssert.AreEqual(new[] { "Idle_Stealth", "Walk_Stealth_Forward" }, tree.children.Select(c => c.motion.name.Replace("Armature|", "")).ToArray(),
            "the modular human pack's stealth clips");
        Assert.IsTrue(tree.children.All(c => ((AnimationClip)c.motion).isLooping), "both loop");
        Assert.IsTrue(sneak.transitions.Any(t => t.destinationState.name == "Empty" && t.conditions.Any(c => c.parameter == "Sneaking")));
    }
}
