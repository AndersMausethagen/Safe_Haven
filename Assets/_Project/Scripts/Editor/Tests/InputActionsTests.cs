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
    }

    public override void TearDown()
    {
        if (!wasEnabled)
        {
            actions.Disable();
        }

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
            (Key.Q, GameInput.Skill(0)), (Key.R, GameInput.Skill(1)), (Key.F, GameInput.Skill(2)), (Key.X, GameInput.Skill(3)),
            (Key.T, GameInput.Skill(4)), (Key.Digit1, GameInput.Skill(0)), (Key.Digit5, GameInput.Skill(4)),
            (Key.I, GameInput.Inventory), (Key.B, GameInput.Items), (Key.K, GameInput.Skills), (Key.C, GameInput.Character),
            (Key.G, GameInput.Build), (Key.N, GameInput.Talents), (Key.H, GameInput.Haven), (Key.M, GameInput.Minimap),
            (Key.V, GameInput.CameraToggleStyle), (Key.LeftShift, GameInput.BuildFineRotate), (Key.LeftAlt, GameInput.BuildFreePlace),
            (Key.Escape, GameInput.Pause), (Key.F5, GameInput.QuickSave), (Key.F6, GameInput.QuickLoad),
            (Key.E, GameInput.Advance), (Key.Enter, GameInput.Advance), (Key.Digit3, GameInput.Answer(2)),
            (Key.LeftShift, GameInput.SplitModifier), (Key.LeftCtrl, GameInput.DiscardModifier),
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
        };

        foreach ((var button, InputAction action) in cases)
        {
            Press(button);
            Assert.IsTrue(action.WasPressedThisFrame(), $"{button.name} should press {action.actionMap.name}/{action.name}");
            Release(button);
        }
    }

    [Test]
    public void KeyLabels_ComeFromTheBindings()
    {
        InputSystem.AddDevice<Keyboard>();
        InputSystem.AddDevice<Mouse>();

        Assert.AreEqual("Q", PlayerInputHandler.SkillKeyLabel(0));
        Assert.AreEqual("T", PlayerInputHandler.SkillKeyLabel(4));
        Assert.AreEqual("Z", GameInput.KeyLabel(GameInput.SwapWeapon));
        Assert.AreEqual("", GameInput.KeyLabel(null));
    }
}
