using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The kind of controller whose buttons the game draws.</summary>
public enum PadFamily
{
    Xbox,
    PlayStation,
    Switch,
}

/// <summary>
/// What the player is playing with: keyboard and mouse, or a controller, and which kind. Follows the last device that did
/// something through the game's actions (InputSystem.onActionChange), so prompts ("[E] Talk", the A button) switch the
/// moment a controller is picked up and back when the mouse moves or a key is pressed. A mouse has to travel a little
/// first: a nudged desk is no reason to swap every prompt on the screen.
/// <para>
/// GameInput reads this for key labels; <see cref="GameInput.KeysChanged"/> tells text that names keys to look again. The
/// Controller Buttons option can pin the kind of pad, for when Steam Input presents every controller as an Xbox pad.
/// </para>
/// </summary>
public static class InputDevices
{
    // How far the mouse must move, in pixels, to take over from a controller.
    private const float MouseTravel = 12f;

    private static Gamepad lastPad;
    private static PadFamily detected;
    private static float mouseTravelled;
    private static bool listening;

    /// <summary>The controller last used, or null (ControllerRumble shakes this one).</summary>
    public static Gamepad LastPad => lastPad;

    /// <summary>True while the player is on a controller.</summary>
    public static bool UsingGamepad { get; private set; }

    /// <summary>The control scheme in use, as the Input Actions name it (GameInput.KeyboardMouse or GameInput.Gamepad).</summary>
    public static string Scheme => UsingGamepad ? GameInput.Gamepad : GameInput.KeyboardMouse;

    /// <summary>Whose buttons to draw: the Controller Buttons option, or the controller last used.</summary>
    public static PadFamily Family
    {
        get
        {
            int pinned = GameOptions.ControllerButtons.Index;
            return pinned > 0 ? (PadFamily)(pinned - 1) : detected;
        }
    }

    /// <summary>Raised when the device in use or the kind of pad changes.</summary>
    public static event Action Changed;

    /// <summary>Goes up on every change, for text that checks rather than listens.</summary>
    public static int Changes { get; private set; }

    /// <summary>Switches to keyboard and mouse or to a controller of this kind. Tests use it to put prompts in a known state.</summary>
    public static void Use(bool gamepad, PadFamily family)
    {
        mouseTravelled = 0f;
        if (gamepad == UsingGamepad && family == detected)
        {
            return;
        }

        UsingGamepad = gamepad;
        detected = family;
        Changes++;
        Changed?.Invoke();
        GameInput.NotifyKeysChanged();
    }

    /// <summary>The kind of pad a device is: Sony's are PlayStation, Nintendo's Switch, the rest drawn as Xbox.</summary>
    public static PadFamily FamilyOf(InputDevice device)
    {
        if (device == null)
        {
            return PadFamily.Xbox;
        }

        string layout = device.layout;
        string maker = device.description.manufacturer ?? "";
        if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "DualShockGamepad") || maker.IndexOf("Sony", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return PadFamily.PlayStation;
        }

        if (layout.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0 || maker.IndexOf("Nintendo", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return PadFamily.Switch;
        }

        return PadFamily.Xbox;
    }

    /// <summary>Takes note of input from a control: called for every action that starts or performs.</summary>
    public static void Noticed(InputControl control)
    {
        InputDevice device = control?.device;
        // The window pointer is a mouse device fed by the controller (PadCursor): its moves are the controller's.
        if (PadCursor.IsPointer(device))
        {
            return;
        }

        if (device is Gamepad pad)
        {
            if (!UsingGamepad || pad != lastPad)
            {
                lastPad = pad;
                Use(true, FamilyOf(pad));
            }

            return;
        }

        if (!UsingGamepad || !(device is Keyboard || device is Pointer))
        {
            return;
        }

        // Moving the mouse takes a little travel; a key, a click or the wheel takes over at once.
        if (device is Mouse mouse && (control == mouse.position || control == mouse.delta || control.parent == mouse.position || control.parent == mouse.delta))
        {
            mouseTravelled += mouse.delta.ReadValue().magnitude;
            if (mouseTravelled < MouseTravel)
            {
                return;
            }
        }

        Use(false, detected);
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so the state is reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Stop();
        lastPad = null;
        UsingGamepad = false;
        detected = PadFamily.Xbox;
        mouseTravelled = 0f;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Begin()
    {
        if (listening)
        {
            return;
        }

        listening = true;
        InputSystem.onActionChange += OnActionChange;
        InputSystem.onDeviceChange += OnDeviceChange;
        GameOption.AnyChanged += OnOptionChanged;
        Application.quitting += Stop;
    }

    // Leaving play mode in the Editor: edit-mode tools and tests read keyboard labels again.
    private static void Stop()
    {
        if (!listening)
        {
            return;
        }

        listening = false;
        InputSystem.onActionChange -= OnActionChange;
        InputSystem.onDeviceChange -= OnDeviceChange;
        GameOption.AnyChanged -= OnOptionChanged;
        Application.quitting -= Stop;
        UsingGamepad = false;
    }

    private static void OnActionChange(object changed, InputActionChange change)
    {
        if ((change == InputActionChange.ActionPerformed || change == InputActionChange.ActionStarted) && changed is InputAction action)
        {
            Noticed(action.activeControl);
        }
    }

    private static void OnOptionChanged(GameOption option)
    {
        if (option == GameOptions.ControllerButtons)
        {
            Changes++;
            Changed?.Invoke();
            GameInput.NotifyKeysChanged();
        }
    }

    // A controller unplugged while in use hands back to the keyboard.
    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (UsingGamepad && device == lastPad && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected))
        {
            lastPad = null;
            Use(false, detected);
        }
    }
}
