using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Keeper's view (Docs/ColonyModeResearch_2026-11-21.md): when it opens and gives the player back their feet (KeeperViewRules),
/// how its camera moves (KeeperCameraRig), its keys (the Keeper map and System › Keeper View, on virtual devices), which keys may
/// share a key with them (InputRebinds.CanMeet) and the window keys it keeps working (PlayerControl.WindowKeyPaths).
/// </summary>
public sealed class KeeperViewTests : InputTestFixture
{
    private const string ActionsPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";
    private InputActionAsset actions;
    private bool wasEnabled;

    public override void Setup()
    {
        base.Setup();
        actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        if (InputSystem.actions != actions)
        {
            InputSystem.actions = actions;
        }

        wasEnabled = actions.enabled;
        actions.Enable();
    }

    public override void TearDown()
    {
        if (!wasEnabled)
        {
            actions.Disable();
        }

        base.TearDown();
        InputSystem.actions = actions;
    }

    private static KeeperSituation Calm(bool atHome = true, bool unlocked = true, bool breach = false, bool cinematic = false,
        bool dialogue = false, bool afloat = false, bool hunted = false, bool hurt = false, bool held = false) =>
        new KeeperSituation(atHome, unlocked, breach, cinematic, dialogue, afloat, hunted, hurt, held);

    // ----- Rules -----

    [Test]
    public void Opens_OnlyAtHome_OnceTheStoryHasOpenedIt()
    {
        Assert.IsNull(KeeperViewRules.WhyNot(Calm()), "at home, unlocked and calm: it opens");
        StringAssert.Contains("only at home", KeeperViewRules.WhyNot(Calm(atHome: false)));
        StringAssert.Contains("later in the story", KeeperViewRules.WhyNot(Calm(unlocked: false)));
    }

    [Test]
    public void DoesNotOpen_InAFight_Afloat_OrWhileSomethingElseHasThePlayer()
    {
        Assert.IsNotEmpty(KeeperViewRules.WhyNot(Calm(breach: true)));
        Assert.IsNotEmpty(KeeperViewRules.WhyNot(Calm(hunted: true)));
        Assert.IsNotEmpty(KeeperViewRules.WhyNot(Calm(hurt: true)));
        Assert.IsNotEmpty(KeeperViewRules.WhyNot(Calm(afloat: true)));
        // A cinematic, a conversation or another hold: refused without a word.
        Assert.AreEqual(string.Empty, KeeperViewRules.WhyNot(Calm(cinematic: true)));
        Assert.AreEqual(string.Empty, KeeperViewRules.WhyNot(Calm(dialogue: true)));
        Assert.AreEqual(string.Empty, KeeperViewRules.WhyNot(Calm(held: true)));
    }

    [Test]
    public void GivesTheFeetBack_WhenHuntedHurtBreachedOrACinematicBegins()
    {
        Assert.IsNull(KeeperViewRules.WhyLeave(Calm()), "calm: it stays open");
        Assert.IsNull(KeeperViewRules.WhyLeave(Calm(afloat: true)), "afloat can't happen in the view and is no reason to leave");
        StringAssert.Contains("breached", KeeperViewRules.WhyLeave(Calm(breach: true)));
        StringAssert.Contains("after you", KeeperViewRules.WhyLeave(Calm(hunted: true)));
        StringAssert.Contains("hurt", KeeperViewRules.WhyLeave(Calm(hurt: true)));
        Assert.AreEqual(string.Empty, KeeperViewRules.WhyLeave(Calm(cinematic: true)));
        Assert.AreEqual(string.Empty, KeeperViewRules.WhyLeave(Calm(dialogue: true)));
        Assert.AreEqual(string.Empty, KeeperViewRules.WhyLeave(Calm(atHome: false)), "leaving the Haven closes it");
    }

    // ----- The camera rig -----

    [Test]
    public void Pan_MovesAlongTheGroundAsTheViewFaces()
    {
        Vector3 ahead = KeeperCameraRig.PanDelta(0f, Vector2.up, 2f);
        Assert.That(ahead.z, Is.EqualTo(2f).Within(1e-4f));
        Assert.That(ahead.x, Is.EqualTo(0f).Within(1e-4f));
        Vector3 turned = KeeperCameraRig.PanDelta(90f, Vector2.up, 2f);
        Assert.That(turned.x, Is.EqualTo(2f).Within(1e-4f), "facing east, forward is +x");
        Vector3 right = KeeperCameraRig.PanDelta(0f, Vector2.right, 1f);
        Assert.That(right.x, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(ahead.y + turned.y + right.y, Is.EqualTo(0f).Within(1e-4f), "panning never lifts the view");
    }

    [Test]
    public void Rig_KeepsWithinTheHaven_AndPansFasterFurtherOut()
    {
        Vector3 kept = KeeperCameraRig.ClampToCircle(new Vector3(300f, 7f, 0f), Vector3.zero, 200f);
        Assert.That(kept.x, Is.EqualTo(200f).Within(1e-3f));
        Assert.AreEqual(7f, kept.y, "its height is kept");
        Assert.AreEqual(new Vector3(10f, 0f, 10f), KeeperCameraRig.ClampToCircle(new Vector3(10f, 0f, 10f), Vector3.zero, 200f));

        var settings = ScriptableObject.CreateInstance<KeeperViewSettings>();
        try
        {
            var rig = new KeeperCameraRig(Vector3.zero, 0f, 0f);
            float near = rig.PanSpeed(settings);
            float nearPitch = rig.Pitch(settings);
            rig.ZoomBy(5f);
            Assert.AreEqual(1f, rig.Zoom, "zoom stops at fully out");
            Assert.Greater(rig.PanSpeed(settings), near, "further out pans faster");
            Assert.Greater(rig.Pitch(settings), nearPitch, "steeper from higher up");
            Assert.AreEqual(settings.MaxDistance, rig.Distance(settings));
            rig.Turn(370f);
            Assert.That(rig.Yaw, Is.EqualTo(10f).Within(1e-3f));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    // ----- Keys -----

    [Test]
    public void Backquote_OpensTheView_AndTheKeeperKeysReachTheirActions()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        (Key key, InputAction action)[] cases =
        {
            (Key.Backquote, GameInput.KeeperView), (Key.Space, GameInput.KeeperFindMe), (Key.F, GameInput.KeeperFollow),
        };

        foreach ((Key key, InputAction action) in cases)
        {
            Assert.IsNotNull(action);
            Press(keyboard[key]);
            Assert.IsTrue(action.WasPressedThisFrame(), $"{key} should press {action.actionMap.name}/{action.name}");
            Release(keyboard[key]);
        }

        Press(keyboard.wKey);
        Assert.AreEqual(Vector2.up, GameInput.KeeperPan.ReadValue<Vector2>());
        Release(keyboard.wKey);
        Press(keyboard.qKey);
        Assert.Less(GameInput.KeeperTurn.ReadValue<float>(), 0f, "Q turns left");
        Release(keyboard.qKey);
        Press(keyboard.eKey);
        Assert.Greater(GameInput.KeeperTurn.ReadValue<float>(), 0f, "E turns right");
        Release(keyboard.eKey);
        Press(mouse.leftButton);
        Assert.IsTrue(GameInput.KeeperSelect.WasPressedThisFrame(), "the left button picks");
        Release(mouse.leftButton);
        Press(mouse.middleButton);
        Assert.IsTrue(GameInput.KeeperDrag.IsPressed(), "the middle button drags the ground");
        Release(mouse.middleButton);
    }

    [Test]
    public void OnAPad_TheDpadWalksTheOrdersAndThePeople_AndTheBumpersTheAlerts()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        (UnityEngine.InputSystem.Controls.ButtonControl button, InputAction action, float sign)[] cases =
        {
            (pad.dpad.left, GameInput.KeeperCommand, -1f), (pad.dpad.right, GameInput.KeeperCommand, 1f),
            (pad.dpad.up, GameInput.KeeperPerson, -1f), (pad.dpad.down, GameInput.KeeperPerson, 1f),
            (pad.leftShoulder, GameInput.KeeperAlert, -1f), (pad.rightShoulder, GameInput.KeeperAlert, 1f),
        };

        foreach (var (button, action, sign) in cases)
        {
            Assert.IsNotNull(action);
            Press(button);
            Assert.IsTrue(action.WasPressedThisFrame(), $"{button.name} presses {action.name}");
            Assert.AreEqual(sign, Mathf.Sign(action.ReadValue<float>()), $"{button.name} goes the right way along {action.name}");
            Release(button);
        }

        // The keyboard and mouse don't reach them: their HUD is clicked.
        foreach (InputAction action in new[] { GameInput.KeeperCommand, GameInput.KeeperPerson, GameInput.KeeperAlert })
        {
            foreach (InputBinding binding in action.bindings)
            {
                StringAssert.DoesNotStartWith("<Keyboard>", binding.path);
            }
        }
    }

    [Test]
    public void KeeperKeys_ShareKeysWithWalking_ButNotWithWindowOrSystemKeys()
    {
        RebindRow Row(string path) => new RebindRow("", "", path);
        Assert.IsFalse(InputRebinds.CanMeet(Row("Keeper/Pan"), Row("Player/Move")), "WASD pans the view and walks: never at once");
        Assert.IsFalse(InputRebinds.CanMeet(Row("Keeper/Turn"), Row("Player/Interact")), "E turns the view; Interact is held meanwhile");
        Assert.IsTrue(InputRebinds.CanMeet(Row("Keeper/FindMe"), Row("Player/Haven")), "the Haven key works in the view");
        Assert.IsTrue(InputRebinds.CanMeet(Row("Keeper/Select"), Row("System/QuickSave")), "System keys always work");
        Assert.IsTrue(InputRebinds.CanMeet(Row("Keeper/Pan"), Row("Keeper/Follow")));
        Assert.IsFalse(InputRebinds.CanMeet(Row("Keeper/Pan"), Row("Build/Rotate")), "the view's keys wait while a building is placed");
        Assert.IsTrue(InputRebinds.CanMeet(Row("Keeper/Drag"), Row("Camera/Orbit")), "the view turns on the camera's own button");
        Assert.IsFalse(InputRebinds.CanMeet(Row("Keeper/Drag"), Row("Camera/ToggleStyle")), "the style key does nothing in the view");
        Assert.IsTrue(InputRebinds.CanMeet(Row("Player/Move"), Row("Player/Dodge")), "the rest as before");
        Assert.IsFalse(InputRebinds.CanMeet(Row("Player/Move"), Row("Build/Rotate")), "build keys as before");
    }

    [Test]
    public void EveryKeptWindowKey_IsAnsweredInTheView()
    {
        foreach (string path in PlayerControl.WindowKeyPaths)
        {
            Assert.IsTrue(WindowKeys.Answers(path), $"{path} is kept in the view but nothing there opens it (WindowKeys)");
        }

        Assert.IsFalse(WindowKeys.Answers("Player/Attack"), "the canary: a key that isn't a window's");
        CollectionAssert.Contains(PlayerControl.WindowKeyPaths, "Player/WindowsWheel", "a controller reaches the windows from the view");
    }

    [Test]
    public void BuildersGoToAScaffold_InStepWithTheWorkLeft()
    {
        Assert.AreEqual(1, ResidentLife.BuildersFor(6f), "a flower bed takes one");
        Assert.AreEqual(2, ResidentLife.BuildersFor(25f));
        Assert.AreEqual(3, ResidentLife.BuildersFor(100f), "a hall takes three");
    }

    [Test]
    public void EveryKeptWindowKey_IsARealActionAndARebindRow()
    {
        Assert.Greater(PlayerControl.WindowKeyPaths.Length, 5);
        foreach (string path in PlayerControl.WindowKeyPaths)
        {
            Assert.IsNotNull(actions.FindAction(path), path);
            bool listed = false;
            foreach (RebindRow row in InputRebinds.Rows)
            {
                listed |= row.ActionPath == path;
            }

            Assert.IsTrue(listed, $"{path} can be rebound");
        }
    }
}
