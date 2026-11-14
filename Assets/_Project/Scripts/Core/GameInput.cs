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
    public static InputAction Journal => Find("Player/Journal");
    public static InputAction Tutorial => Find("Player/Tutorial");
    public static InputAction Haven => Find("Player/Haven");
    public static InputAction WorldMap => Find("Player/WorldMap");
    public static InputAction CommandWheel => Find("Player/CommandWheel");
    /// <summary>Hold for the quickbar wheel; in the bag, tap over an item to put it on the quickbar or take it off.</summary>
    public static InputAction QuickItems => Find("Player/QuickItems");
    /// <summary>Co-op: mark what is under the crosshair for friends (CoopPings).</summary>
    public static InputAction Ping => Find("Player/Ping");
    /// <summary>Whistle for a horse of yours (HorseWhistle).</summary>
    public static InputAction Whistle => Find("Player/Whistle");
    /// <summary>In the saddle: a tap canters, a hold gallops (Shift, or the left stick's click). Space (Dodge) jumps.</summary>
    public static InputAction HorseGallop => Find("Player/HorseGallop");
    /// <summary>In the saddle: the horse rears and strikes out ahead (PlayerRiding).</summary>
    public static InputAction HorseKick => Find("Player/HorseKick");
    /// <summary>A controller only (hold View): the wheel of the game's windows. The keyboard has a key for each.</summary>
    public static InputAction WindowsWheel => Find("Player/WindowsWheel");

    /// <summary>The action that casts skill bar slot <paramref name="slot"/> (0-based).</summary>
    public static InputAction Skill(int slot) => Find(SkillPaths[slot]);

    // Camera
    public static InputAction CameraZoom => Find("Camera/Zoom");
    public static InputAction CameraLook => Find("Camera/Look");
    public static InputAction CameraOrbit => Find("Camera/Orbit");
    public static InputAction CameraToggleStyle => Find("Camera/ToggleStyle");
    /// <summary>The right stick: turns the camera at a rate, where Look is the mouse's movement since the last frame.</summary>
    public static InputAction CameraLookStick => Find("Camera/LookStick");

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
    public static InputAction CheatMenu => Find("System/CheatMenu");
    /// <summary>A controller's back button (B): closes what Escape closes, but never opens the Escape menu.</summary>
    public static InputAction Back => Find("System/Back");

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

    // Actions a wheel chose this frame (a controller reaches the whistle, the horse's kick and the ping through one).
    private static readonly Dictionary<InputAction, int> PressedBy = new Dictionary<InputAction, int>();

    /// <summary>Counts as a press of <paramref name="action"/> this frame, for code that asks <see cref="WasPressed"/>.</summary>
    public static void Press(InputAction action)
    {
        if (action != null)
        {
            PressedBy[action] = Time.frameCount;
        }
    }

    /// <summary>The action's key or button went down this frame, or a wheel chose it (<see cref="Press"/>).</summary>
    public static bool WasPressed(InputAction action)
    {
        return action != null && (action.WasPressedThisFrame() || (PressedBy.TryGetValue(action, out int frame) && frame == Time.frameCount));
    }

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

    // What a line of text may name a key by, besides an action's own name ({key:Interact}): the thing it does. Text says
    // "{key:Dismount}", and which action gets the rider down is decided here, once, not in every journal page and line.
    // Read-only tables (this and PadVerbs), not state: nothing for the play-session reset to clear.
    private static readonly IReadOnlyDictionary<string, string> Verbs = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Dismount", "Whistle" }, { "GetDown", "Whistle" }, { "Gallop", "Dodge" }, { "Canter", "Dodge" }, { "Rear", "Dodge" },
        { "Walk", "Sneak" }, { "Crouch", "Sneak" }, { "Feed", "Interact" }, { "Mount", "Interact" }, { "Use", "Interact" },
        { "Hammer", "Interact" }, { "PickUp", "Interact" }, { "Talk", "Interact" }, { "Kick", "HorseKick" }, { "Bag", "Items" },
        { "BuildMenu", "Build" }, { "Map", "WorldMap" }, { "Perks", "Talents" }, { "Abilities", "Skills" }, { "Light", "ToggleTorch" },
        { "Lasso", "Attack" }, { "Roll", "Dodge" }, { "Dodge", "Dodge" }, { "Guard", "Guard" }, { "Attack", "Attack" },
    };

    // A verb that is another button on a controller: in the saddle, B gets you down (PlayerRiding).
    private static readonly IReadOnlyDictionary<string, string> PadVerbs = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Dismount", "System/Back" }, { "GetDown", "System/Back" },
    };

    private static readonly System.Text.RegularExpressions.Regex KeyToken = new System.Text.RegularExpressions.Regex(@"\{key:([A-Za-z0-9/]+)\}");
    // A token with the brackets round it, if any: "[{key:Interact}] Talk" shows a controller's button without them.
    private static readonly System.Text.RegularExpressions.Regex BracketedKeyToken =
        new System.Text.RegularExpressions.Regex(@"([\[(]?)\{key:([A-Za-z0-9/]+)\}([\])]?)");

    // "{key:Back}": what goes back or closes, Esc on the keyboard and B on a controller (System › Back).
    private const string BackToken = "Back";

    /// <summary>Raised when the keys shown change: a rebinding, or the player switching between keyboard and controller.</summary>
    public static event System.Action KeysChanged;

    /// <summary>Goes up whenever the keys shown change, for text that checks rather than listens.</summary>
    public static int KeysVersion => InputRebinds.Changes + InputDevices.Changes;

    /// <summary>True while the player is on a controller (see <see cref="InputDevices"/>).</summary>
    public static bool UsingGamepad => InputDevices.UsingGamepad;

    internal static void NotifyKeysChanged() => KeysChanged?.Invoke();

    /// <summary>
    /// Text that names keys by what they do ("Open your bag ({key:Items})", "{key:Dismount} gets you down"): each token becomes the
    /// key bound to that action now ("B", "X"), so a rebinding, or a change of which action does a thing, never needs the text
    /// changed. An action is named as in the Input Actions ("Interact", "Player/Whistle") or by a verb (Verbs: "Dismount").
    /// </summary>
    public static string Keys(string text) => Resolve(text, asShipped: false);

    /// <summary>
    /// The same text with each token as the key the game ships with, whatever the player has rebound: how the words were
    /// spoken for a voice line ("press L to swap them"), so a line keeps its recording after a rebind.
    /// </summary>
    public static string DefaultKeys(string text) => Resolve(text, asShipped: true);

    private static string Resolve(string text, bool asShipped)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf("{key:", System.StringComparison.Ordinal) < 0)
        {
            return text;
        }

        // A voice line was recorded saying the keyboard's keys, so the shipped keys are always the keyboard's.
        string scheme = asShipped ? KeyboardMouse : InputDevices.Scheme;
        return BracketedKeyToken.Replace(text, match =>
        {
            string name = match.Groups[2].Value;
            string label = LabelForToken(name, asShipped, scheme);
            if (label.Length == 0)
            {
                label = name;
            }

            string open = match.Groups[1].Value, close = match.Groups[3].Value;
            bool paired = (open == "[" && close == "]") || (open == "(" && close == ")");
            return paired && ControllerGlyphs.IsGlyph(label) ? label : open + label + close;
        });
    }

    private static string LabelForToken(string name, bool asShipped, string scheme)
    {
        if (string.Equals(name, BackToken, System.StringComparison.OrdinalIgnoreCase))
        {
            return scheme == Gamepad ? KeyLabel(Back, asShipped, scheme) : "Esc";
        }

        if (scheme == Gamepad && PadVerbs.TryGetValue(name, out string padAction))
        {
            return KeyLabel(Find(padAction, quiet: true), asShipped, scheme);
        }

        InputAction action = ActionNamed(Verbs.TryGetValue(name, out string verb) ? verb : name);
        return action != null ? KeyLabel(action, asShipped, scheme) : "";
    }

    /// <summary>The first key token in the text that names no action or verb (a typo), or null when they all do.</summary>
    public static string UnknownKey(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (System.Text.RegularExpressions.Match match in KeyToken.Matches(text))
        {
            string name = match.Groups[1].Value;
            if (!string.Equals(name, BackToken, System.StringComparison.OrdinalIgnoreCase) && ActionNamed(Verbs.TryGetValue(name, out string verb) ? verb : name) == null)
            {
                return name;
            }
        }

        return null;
    }

    private static InputAction ActionNamed(string name)
    {
        if (name.IndexOf('/') >= 0)
        {
            return Find(name, quiet: true);
        }

        InputAction action = Find("Player/" + name, quiet: true);
        if (action != null)
        {
            return action;
        }

        foreach (string map in new[] { "Camera", "Build", "System", "UI" })
        {
            action = Find(map + "/" + name, quiet: true);
            if (action != null)
            {
                return action;
            }
        }

        return null;
    }

    /// <summary>
    /// Text with keys in it as they are for this player: tokens ({key:Interact}, see <see cref="Keys"/>) become the bound key, and a
    /// key written out the old way ("Open your bag (B)", "[E] Interact") shows as the new one when it has been rebound. Costs
    /// nothing without either.
    /// </summary>
    public static string WithRebinds(string text)
    {
        text = Keys(text);
        // Keys written out the old way are keyboard keys: on a controller they stay as written.
        if (string.IsNullOrEmpty(text) || InputDevices.UsingGamepad || (text.IndexOf('(') < 0 && text.IndexOf('[') < 0))
        {
            return text;
        }

        // Through a stand-in, as two keys can swap: E to Q must not then go on to Q's new key.
        InputBinding group = InputBinding.MaskByGroup(KeyboardMouse);
        foreach (InputAction action in PlayerMap.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (string.IsNullOrEmpty(binding.overridePath) || binding.isComposite || binding.isPartOfComposite || !group.Matches(binding))
                {
                    continue;
                }

                string was = InputControlPath.ToHumanReadableString(binding.path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                string now = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                text = text.Replace($"({was})", $"(\u0001{now})").Replace($"[{was}]", $"[\u0001{now}]");
            }
        }

        return text.Replace("\u0001", "");
    }

    /// <summary>
    /// The key or button that triggers <paramref name="action"/> on what the player is using now (<see cref="InputDevices"/>):
    /// a key for keyboard and mouse ("Q", "MMB"), a button glyph for a controller, of the kind in their hands. Its first
    /// binding in that scheme, so it follows a rebinding. An action no controller button reaches shows its key. Empty when
    /// there is none.
    /// </summary>
    public static string KeyLabel(InputAction action) => KeyLabel(action, asShipped: false, InputDevices.Scheme);

    /// <summary>The label in a given control scheme (GameInput.KeyboardMouse or GameInput.Gamepad), whatever is in use.</summary>
    public static string KeyLabel(InputAction action, string scheme) => KeyLabel(action, asShipped: false, scheme);

    private static string KeyLabel(InputAction action, bool asShipped, string scheme)
    {
        if (action == null)
        {
            return "";
        }

        if (scheme == Gamepad)
        {
            string button = PadLabel(action, asShipped);
            if (button.Length > 0)
            {
                return button;
            }
        }

        InputBinding group = InputBinding.MaskByGroup(KeyboardMouse);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isComposite && !binding.isPartOfComposite && group.Matches(binding))
            {
                return action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions
                                                          | (asShipped ? InputBinding.DisplayStringOptions.IgnoreBindingOverrides : 0));
            }
        }

        return "";
    }

    /// <summary>A controller button's label for one binding (see <see cref="ControllerGlyphs"/>): the glyph, or the button's name.</summary>
    public static string PadLabel(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return "";
        }

        InputBinding binding = action.bindings[bindingIndex];
        return ControllerGlyphs.For(binding.effectivePath, InputDevices.Family)
               ?? action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
    }

    private static string PadLabel(InputAction action, bool asShipped)
    {
        InputBinding group = InputBinding.MaskByGroup(Gamepad);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || !group.Matches(binding))
            {
                continue;
            }

            string glyph = ControllerGlyphs.For(asShipped ? binding.path : binding.effectivePath, InputDevices.Family);
            if (glyph != null)
            {
                return glyph;
            }
        }

        return "";
    }

    // Lookups are by name, and some actions are read every frame, so each is found once per asset.
    private static readonly Dictionary<string, InputAction> Found = new Dictionary<string, InputAction>();
    private static InputActionAsset foundIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Found.Clear();
        foundIn = null;
        Suspended.Clear();
        PressedBy.Clear();
    }

    // Quiet when only looking: a key token's action is searched for in each map in turn.
    private static InputAction Find(string path, bool quiet = false)
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
            if (quiet)
            {
                return null;
            }

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
