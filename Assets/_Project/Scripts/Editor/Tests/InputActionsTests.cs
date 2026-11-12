using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Edit Mode tests for the project-wide Input Actions asset, run on virtual devices (Input System's InputTestFixture, so
/// the real keyboard and mouse are untouched): every GameInput action exists, and the game's keys, buttons and sticks
/// reach the actions they should.
/// </summary>
public sealed class InputActionsTests : InputTestFixture
{
    private const string ActionsPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";
    private InputActionAsset actions;
    private bool wasEnabled;
    private float controllerButtons;

    // The asset itself (the project-wide one must live on disk); only enabled for the test, as in play.
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
        // Prompts start from the keyboard, with the pad's kind followed rather than pinned.
        controllerButtons = GameOptions.ControllerButtons.Value;
        GameOptions.ControllerButtons.Value = 0f;
        InputDevices.Use(false, PadFamily.Xbox);
    }

    public override void TearDown()
    {
        if (!wasEnabled)
        {
            actions.Disable();
        }

        InputDevices.Use(false, PadFamily.Xbox);
        GameOptions.ControllerButtons.Value = controllerButtons;
        base.TearDown();
        // The fixture's teardown puts the input system back without the project-wide actions, and nothing sets them again
        // until the next domain reload: every GameInput lookup in the Editor would come back empty until then.
        InputSystem.actions = actions;
    }

    [Test]
    public void ActionsAsset_IsTheProjectWideOne()
    {
        var registered = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        Assert.IsNotNull(registered);
        Assert.IsTrue(EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out InputActionAsset projectWide));
        Assert.AreSame(registered, projectWide, "Project Settings › Input System Package › Project-wide Actions");
    }

    [Test]
    public void EveryGameInputAction_IsInTheAsset()
    {
        foreach (PropertyInfo property in typeof(GameInput).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType == typeof(InputAction))
            {
                Assert.IsNotNull(property.GetValue(null), $"GameInput.{property.Name}");
            }
        }

        for (int slot = 0; slot < SkillLoadout.SlotCount; slot++)
        {
            Assert.IsNotNull(GameInput.Skill(slot), $"Skill {slot + 1}");
        }

        for (int answer = 0; answer < GameInput.AnswerCount; answer++)
        {
            Assert.IsNotNull(GameInput.Answer(answer), $"Answer {answer + 1}");
        }

        Assert.IsNotNull(GameInput.PlayerMap);
    }

    [Test]
    public void KeyboardKeys_ReachTheirActions()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        (Key key, InputAction action)[] cases =
        {
            (Key.E, GameInput.Interact), (Key.Space, GameInput.Dodge), (Key.LeftCtrl, GameInput.Sneak),
            (Key.Tab, GameInput.ToggleCombat), (Key.Z, GameInput.SwapWeapon), (Key.L, GameInput.ToggleTorch),
            (Key.Digit1, GameInput.Skill(0)), (Key.Digit2, GameInput.Skill(1)), (Key.Digit3, GameInput.Skill(2)),
            (Key.Digit4, GameInput.Skill(3)), (Key.Digit5, GameInput.Skill(4)), (Key.Numpad1, GameInput.Skill(0)),
            (Key.Numpad5, GameInput.Skill(4)), (Key.F, GameInput.QuickItems), (Key.LeftAlt, GameInput.CommandWheel),
            (Key.I, GameInput.Inventory), (Key.B, GameInput.Items), (Key.K, GameInput.Skills), (Key.C, GameInput.Character),
            (Key.G, GameInput.Build), (Key.N, GameInput.Talents), (Key.H, GameInput.Haven), (Key.M, GameInput.WorldMap),
            (Key.V, GameInput.CameraToggleStyle), (Key.LeftShift, GameInput.BuildFineRotate), (Key.LeftAlt, GameInput.BuildFreePlace),
            (Key.Escape, GameInput.Pause), (Key.F5, GameInput.QuickSave), (Key.F6, GameInput.QuickLoad), (Key.F1, GameInput.CheatMenu),
            (Key.E, GameInput.Advance), (Key.Enter, GameInput.Advance), (Key.Digit3, GameInput.Answer(2)),
            (Key.LeftShift, GameInput.SplitModifier), (Key.LeftCtrl, GameInput.DiscardModifier), (Key.Y, GameInput.Ping),
            (Key.X, GameInput.Whistle), (Key.R, GameInput.HorseKick),
        };

        foreach ((Key key, InputAction action) in cases)
        {
            Press(keyboard[key]);
            Assert.IsTrue(action.WasPressedThisFrame(), $"{key} should press {action.actionMap.name}/{action.name}");
            Assert.IsTrue(action.IsPressed(), $"{key} holds {action.name}");
            Release(keyboard[key]);
            Assert.IsFalse(action.IsPressed(), $"{key} let go releases {action.name}");
        }
    }

    [Test]
    public void Wasd_MovesAndTheMouseAimsAttacksAndScrolls()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();

        Press(keyboard.wKey);
        Assert.AreEqual(Vector2.up, GameInput.Move.ReadValue<Vector2>());
        Press(keyboard.dKey);
        Assert.Greater(GameInput.Move.ReadValue<Vector2>().x, 0f);
        Release(keyboard.wKey);
        Release(keyboard.dKey);
        Assert.AreEqual(Vector2.zero, GameInput.Move.ReadValue<Vector2>());

        Move(mouse.position, new Vector2(321f, 123f));
        Assert.AreEqual(new Vector2(321f, 123f), GameInput.PointerPosition);

        Press(mouse.leftButton);
        Assert.IsTrue(GameInput.Attack.IsPressed());
        Assert.IsTrue(GameInput.BuildPlace.WasPressedThisFrame());
        Assert.IsTrue(GameInput.Click.IsPressed());
        Release(mouse.leftButton);
        Assert.IsTrue(GameInput.BuildPlace.WasReleasedThisFrame());

        Press(mouse.rightButton);
        Assert.IsTrue(GameInput.Guard.IsPressed());
        Assert.IsTrue(GameInput.CameraOrbit.IsPressed());
        Assert.IsTrue(GameInput.BuildCancel.WasPressedThisFrame());
        Release(mouse.rightButton);

        Press(mouse.middleButton);
        Assert.IsTrue(GameInput.CycleArrows.WasPressedThisFrame());
        Release(mouse.middleButton);

        Set(mouse.scroll, new Vector2(0f, 120f));
        Assert.Greater(GameInput.CameraZoom.ReadValue<float>(), 0f);
        Assert.Greater(GameInput.BuildRotate.ReadValue<float>(), 0f);
    }

    [Test]
    public void Gamepad_PlaysTheGame()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();

        Set(gamepad.leftStick, new Vector2(0f, 1f));
        Assert.Greater(GameInput.Move.ReadValue<Vector2>().y, 0.5f);
        Set(gamepad.leftStick, Vector2.zero);

        (UnityEngine.InputSystem.Controls.ButtonControl button, InputAction action)[] cases =
        {
            (gamepad.rightTrigger, GameInput.Attack), (gamepad.leftTrigger, GameInput.Guard), (gamepad.buttonSouth, GameInput.Dodge),
            (gamepad.buttonWest, GameInput.Interact), (gamepad.buttonEast, GameInput.CancelInteraction),
            (gamepad.leftShoulder, GameInput.Sneak), (gamepad.buttonNorth, GameInput.Skill(0)),
            (gamepad.dpad.down, GameInput.Skill(3)), (gamepad.selectButton, GameInput.Inventory), (gamepad.buttonSouth, GameInput.Submit),
            // Start is the Escape menu, B goes back, A moves a conversation on (2026-11-12).
            (gamepad.startButton, GameInput.Pause), (gamepad.buttonEast, GameInput.Back), (gamepad.buttonSouth, GameInput.Advance),
        };

        foreach ((var button, InputAction action) in cases)
        {
            Press(button);
            Assert.IsTrue(action.WasPressedThisFrame(), $"{button.name} should press {action.actionMap.name}/{action.name}");
            Release(button);
        }

        // The bag is on View alone: Start opening it as well as the menu would open both at once.
        Press(gamepad.startButton);
        Assert.IsFalse(GameInput.Items.WasPressedThisFrame(), "Start no longer opens the bag");
        Release(gamepad.startButton);

        // The right stick turns the camera, at a rate (PlayerCameraController.ReadStick).
        Set(gamepad.rightStick, new Vector2(1f, 0f));
        Assert.Greater(GameInput.CameraLookStick.ReadValue<Vector2>().x, 0.5f);
        Set(gamepad.rightStick, Vector2.zero);
    }

    [Test]
    public void TheDeviceInUse_IsTheLastOneThatDidSomething()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var xbox = InputSystem.AddDevice<Gamepad>();
        var playstation = InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualShock4GamepadHID>();

        Assert.AreEqual(PadFamily.Xbox, InputDevices.FamilyOf(xbox));
        Assert.AreEqual(PadFamily.PlayStation, InputDevices.FamilyOf(playstation));

        int changes = GameInput.KeysVersion;
        InputDevices.Noticed(playstation.buttonSouth);
        Assert.IsTrue(InputDevices.UsingGamepad);
        Assert.AreEqual(GameInput.Gamepad, InputDevices.Scheme);
        Assert.AreEqual(PadFamily.PlayStation, InputDevices.Family);
        Assert.Greater(GameInput.KeysVersion, changes, "text that names keys looks again");

        InputDevices.Noticed(xbox.leftStick);
        Assert.AreEqual(PadFamily.Xbox, InputDevices.Family, "the pad last used decides");

        // A nudge of the mouse is not enough to take over; a real move, a key or a click is.
        Set(mouse.delta, new Vector2(4f, 0f));
        InputDevices.Noticed(mouse.position);
        Assert.IsTrue(InputDevices.UsingGamepad);
        Set(mouse.delta, new Vector2(30f, 0f));
        InputDevices.Noticed(mouse.position);
        Assert.IsFalse(InputDevices.UsingGamepad);

        InputDevices.Noticed(xbox.buttonWest);
        InputDevices.Noticed(keyboard.eKey);
        Assert.IsFalse(InputDevices.UsingGamepad);

        // Pinned in the options, the pad's kind no longer follows the device.
        InputDevices.Noticed(xbox.buttonWest);
        GameOptions.ControllerButtons.Value = 2f;
        Assert.AreEqual(PadFamily.PlayStation, InputDevices.Family);
    }

    [Test]
    public void KeyLabels_FollowTheDeviceInUse()
    {
        Assert.AreEqual("buttonSouth", ControllerGlyphs.ControlOf("*/{Submit}"));
        Assert.AreEqual("dpadUp", ControllerGlyphs.ControlOf("<Gamepad>/dpad/up"));
        Assert.AreEqual("leftStick", ControllerGlyphs.ControlOf("<Gamepad>/leftStick/up"));
        Assert.IsNull(ControllerGlyphs.ControlOf("<Keyboard>/e"));
        Assert.AreEqual("ZL", ControllerGlyphs.NameOf("leftTrigger", PadFamily.Switch));
        Assert.AreEqual("B", ControllerGlyphs.NameOf("buttonSouth", PadFamily.Switch), "Nintendo's bottom button is B");

        Assert.AreEqual("[E] Talk", GameInput.Keys("[{key:Interact}] Talk"));
        Assert.AreEqual("Esc to return", GameInput.Keys("{key:Back} to return"));

        InputDevices.Use(true, PadFamily.Xbox);
        string interact = GameInput.KeyLabel(GameInput.Interact);
        Assert.IsTrue(interact.Contains("xbox_buttonWest") || interact == "X", interact);
        if (ControllerGlyphs.IsGlyph(interact))
        {
            // A glyph stands without the brackets a key name needs.
            Assert.AreEqual(interact + " Talk", GameInput.Keys("[{key:Interact}] Talk"));
            Assert.AreEqual("Open it " + interact, GameInput.Keys("Open it ({key:Interact})"));
        }

        string back = GameInput.Keys("{key:Back}");
        Assert.IsTrue(back.Contains("xbox_buttonEast") || back == "B", back);
        Assert.AreEqual("J", GameInput.KeyLabel(GameInput.Journal), "no button reaches the journal yet: its key is shown");
        Assert.AreEqual("E", GameInput.KeyLabel(GameInput.Interact, GameInput.KeyboardMouse));
        // A voice line says the keyboard's key whatever is in the player's hands.
        Assert.AreEqual("press E", GameInput.DefaultKeys("press {key:Interact}"));

        InputDevices.Use(true, PadFamily.PlayStation);
        string square = GameInput.KeyLabel(GameInput.Interact);
        Assert.IsTrue(square.Contains("playstation_buttonWest") || square == "Square", square);

        InputDevices.Use(false, PadFamily.PlayStation);
        Assert.AreEqual("E", GameInput.KeyLabel(GameInput.Interact));
    }

    [Test]
    public void KeyLabels_ComeFromTheBindings()
    {
        InputSystem.AddDevice<Keyboard>();
        InputSystem.AddDevice<Mouse>();
        InputDevices.Use(false, PadFamily.Xbox);

        // The skill bar is on the number keys (2026-11-09); F is the quickbar's.
        Assert.AreEqual("1", PlayerInputHandler.SkillKeyLabel(0));
        Assert.AreEqual("5", PlayerInputHandler.SkillKeyLabel(4));
        Assert.AreEqual("F", GameInput.KeyLabel(GameInput.QuickItems));
        Assert.AreEqual("Z", GameInput.KeyLabel(GameInput.SwapWeapon));
        Assert.AreEqual("", GameInput.KeyLabel(null));
    }
}
