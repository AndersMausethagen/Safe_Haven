using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The game's input: the project-wide Input Actions asset (Project Settings › Input System Package ›
/// Assets/_Project/Input/InputSystem_Actions.inputactions), named here once so code never spells a binding or an action
/// path. Bindings live in the asset, per control scheme (Keyboard&amp;Mouse, Gamepad); change them there, not in code.
/// <para>
/// Maps: Player (moving, fighting, the hands, skills, the game's windows), Camera, Build, System (Escape menu, quick
/// save) and UI (the InputSystemUIInputModule's actions plus the game's own). The Input System enables the project-wide
/// actions when the game starts; PlayerInputHandler turns the Player map off while the game is paused. Listeners
/// subscribe in OnEnable and unsubscribe in OnDisable: the actions are shared, so a callback left behind outlives its
/// object.
/// </para>
/// </summary>
public static class GameInput
{
    public const string KeyboardMouse = "Keyboard&Mouse";
    public const string Gamepad = "Gamepad";

    public static InputActionMap PlayerMap => Map("Player");

    // Player
    public static InputAction Move => Find("Player/Move");
    public static InputAction Attack => Find("Player/Attack");
    public static InputAction Guard => Find("Player/Guard");
    public static InputAction Interact => Find("Player/Interact");
    public static InputAction CancelInteraction => Find("Player/CancelInteraction");
    public static InputAction Dodge => Find("Player/Dodge");
    public static InputAction Sneak => Find("Player/Sneak");
    public static InputAction ToggleCombat => Find("Player/ToggleCombat");
    public static InputAction SwapWeapon => Find("Player/SwapWeapon");
    public static InputAction ToggleTorch => Find("Player/ToggleTorch");
    public static InputAction CycleArrows => Find("Player/CycleArrows");
    public static InputAction Inventory => Find("Player/Inventory");
    public static InputAction Items => Find("Player/Items");
    public static InputAction Skills => Find("Player/Skills");
    public static InputAction Character => Find("Player/Character");
    public static InputAction Build => Find("Player/Build");
    public static InputAction Talents => Find("Player/Talents");
    public static InputAction Haven => Find("Player/Haven");

    /// <summary>The action that casts skill bar slot <paramref name="slot"/> (0-based).</summary>
    public static InputAction Skill(int slot) => Find(SkillPaths[slot]);

    // Camera
    public static InputAction CameraZoom => Find("Camera/Zoom");
    public static InputAction CameraLook => Find("Camera/Look");
    public static InputAction CameraOrbit => Find("Camera/Orbit");
    public static InputAction CameraToggleStyle => Find("Camera/ToggleStyle");

    // Build
    public static InputAction BuildPlace => Find("Build/Place");
    public static InputAction BuildCancel => Find("Build/Cancel");
    public static InputAction BuildRotate => Find("Build/Rotate");
    public static InputAction BuildFineRotate => Find("Build/FineRotate");
    public static InputAction BuildFreePlace => Find("Build/FreePlace");

    // System
    public static InputAction Pause => Find("System/Pause");
    public static InputAction QuickSave => Find("System/QuickSave");
    public static InputAction QuickLoad => Find("System/QuickLoad");

    // UI
    public static InputAction Point => Find("UI/Point");
    public static InputAction Click => Find("UI/Click");
    public static InputAction Submit => Find("UI/Submit");
    public static InputAction Cancel => Find("UI/Cancel");
    public static InputAction Advance => Find("UI/Advance");
    public static InputAction SplitModifier => Find("UI/SplitModifier");
    public static InputAction DiscardModifier => Find("UI/DiscardModifier");

    /// <summary>The action that picks dialogue answer <paramref name="index"/> (0-based; the number keys 1-9).</summary>
    public static InputAction Answer(int index) => Find(AnswerPaths[index]);
    public const int AnswerCount = 9;

    // Built once: dialogue reads the answers every frame.
    private static readonly string[] SkillPaths = { "Player/Skill1", "Player/Skill2", "Player/Skill3", "Player/Skill4", "Player/Skill5" };
    private static readonly string[] AnswerPaths =
        { "UI/Answer1", "UI/Answer2", "UI/Answer3", "UI/Answer4", "UI/Answer5", "UI/Answer6", "UI/Answer7", "UI/Answer8", "UI/Answer9" };

    private static readonly List<InputActionMap> Suspended = new List<InputActionMap>();

    /// <summary>
    /// Turns off every map but UI until <see cref="ResumeGameplay"/>, for when a key means something else for a moment:
    /// choosing a new key, typing a name. The screen stays alive; everything that plays the game waits.
    /// </summary>
    public static void SuspendGameplay()
    {
        foreach (InputActionMap map in InputSystem.actions.actionMaps)
        {
            if (map.enabled && map.name != "UI")
            {
                Suspended.Add(map);
                map.Disable();
            }
        }
    }

    public static void ResumeGameplay()
    {
        foreach (InputActionMap map in Suspended)
        {
            map.Enable();
        }

        Suspended.Clear();
    }

    /// <summary>Where the pointer (mouse, pen, touch) is on the screen.</summary>
    public static Vector2 PointerPosition => Point.ReadValue<Vector2>();

    /// <summary>The key or button that triggers <paramref name="action"/> for keyboard and mouse, as shown to the player
    /// ("Q", "MMB"): its first binding in that scheme, so it follows a rebinding. Empty when there is none.</summary>
    public static string KeyLabel(InputAction action)
    {
        if (action == null)
        {
            return "";
        }

        InputBinding group = InputBinding.MaskByGroup(KeyboardMouse);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isComposite && !binding.isPartOfComposite && group.Matches(binding))
            {
                return action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            }
        }

        return "";
    }

    // Lookups are by name, and some actions are read every frame, so each is found once per asset.
    private static readonly Dictionary<string, InputAction> Found = new Dictionary<string, InputAction>();
    private static InputActionAsset foundIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Found.Clear();
        foundIn = null;
        Suspended.Clear();
    }

    private static InputAction Find(string path)
    {
        InputActionAsset asset = InputSystem.actions;
        if (asset != foundIn)
        {
            Found.Clear();
            foundIn = asset;
        }

        if (Found.TryGetValue(path, out InputAction cached))
        {
            return cached;
        }

        InputAction action = asset != null ? asset.FindAction(path) : null;
        if (action == null)
        {
            Debug.LogError($"Input action \"{path}\" is missing: check the project-wide Input Actions asset (Project Settings › Input System Package).");
            return null;
        }

        Found[path] = action;
        return action;
    }

    private static InputActionMap Map(string name)
    {
        InputActionAsset asset = InputSystem.actions;
        InputActionMap map = asset != null ? asset.FindActionMap(name) : null;
        if (map == null)
        {
            Debug.LogError($"Input action map \"{name}\" is missing from the project-wide Input Actions asset.");
        }

        return map;
    }
}
