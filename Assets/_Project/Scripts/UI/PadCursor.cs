using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A pointer for the controller in the game's windows. The windows are made for the mouse (drag an item, double-click
/// to use it, right-click a recipe), so rather than teach each one the d-pad, the left stick moves a virtual mouse: the
/// UI sees an ordinary mouse, and every window works with A as the left button, X as the right one, the right stick as the
/// wheel, and the triggers as the split and discard modifiers (UI map). B still goes back.
/// <para>
/// It shows while the player is on a controller and a window, the Escape menu, the map or a menu scene is up, but not in
/// a conversation (answers are picked with the stick) or while a wheel or rebinding has the stick. While it shows, the
/// Event System sends no navigation events, so the stick doesn't move a selection as well; it starts on the selected
/// button, if any. On a controller the system pointer hides everywhere: in the top-down view it would only lie where it
/// was left. Speeds are in ControllerSettings.
/// </para>
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class PadCursor : MonoBehaviour, IGameSystem
{
    private static PadCursor current;

    private InputAction move;
    private InputAction click;
    private InputAction context;
    private InputAction scroll;
    private Mouse device;
    private Canvas canvas;
    private RectTransform pointer;
    private Sprite sprite;
    private Vector2 position;
    private bool active;
    private bool navigationWas = true;
    private bool usedPad;

    /// <summary>True while the controller drives the pointer in a window: gameplay leaves the controller alone meanwhile.</summary>
    public static bool IsActive => current != null && current.active;

    /// <summary>True for the pointer's own mouse device, whose input is the controller's, not a mouse's.</summary>
    public static bool IsPointer(InputDevice candidate) => current != null && candidate != null && candidate == current.device;

    public void Initialise()
    {
        current = this;
        move = new InputAction("Pad Cursor", InputActionType.Value, "<Gamepad>/leftStick", processors: "stickDeadzone", expectedControlType: "Vector2");
        click = new InputAction("Pad Click", InputActionType.Button, "<Gamepad>/buttonSouth");
        context = new InputAction("Pad Right Click", InputActionType.Button, "<Gamepad>/buttonWest");
        scroll = new InputAction("Pad Scroll", InputActionType.Value, "<Gamepad>/rightStick", processors: "stickDeadzone", expectedControlType: "Vector2");
        BuildView();
    }

    public void OnSceneEntered(Scene scene)
    {
    }

    private void Update()
    {
        bool want = Wanted();
        if (want != active)
        {
            if (want)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        if (active)
        {
            Drive();
        }
    }

    // After the camera has locked or freed the cursor for the frame (it shows it whenever it lets go).
    private void LateUpdate() => UpdateSystemPointer();

    private static bool Wanted()
    {
        if (!InputDevices.UsingGamepad || DialogueBox.IsOpen || CommandWheel.IsHeld || InputRebinds.IsListening || SceneTransition.IsBusy
            || CinematicScreen.IsPlaying)
        {
            return false;
        }

        return Player.Current == null || (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen)
               || OptionsScreen.HasEscape || SavesScreen.HasEscape || WorldMapHud.IsOpen;
    }

    private void Show()
    {
        active = true;
        if (device == null || !device.added)
        {
            device = InputSystem.AddDevice<Mouse>("Pad Cursor");
        }

        // From the selected button, when a screen chose one for the stick; else the middle of the screen.
        position = new Vector2(Screen.width, Screen.height) * 0.5f;
        EventSystem events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform is RectTransform selected)
        {
            Canvas owner = selected.GetComponentInParent<Canvas>();
            Camera eye = owner != null && owner.renderMode != RenderMode.ScreenSpaceOverlay ? owner.worldCamera : null;
            position = RectTransformUtility.WorldToScreenPoint(eye, selected.TransformPoint(selected.rect.center));
        }

        if (events != null)
        {
            navigationWas = events.sendNavigationEvents;
            events.sendNavigationEvents = false;
            events.SetSelectedGameObject(null);
        }

        move.Enable();
        click.Enable();
        context.Enable();
        scroll.Enable();
        pointer.gameObject.SetActive(true);
        Write(Vector2.zero, Vector2.zero);
    }

    private void Hide()
    {
        active = false;
        move.Disable();
        click.Disable();
        context.Disable();
        scroll.Disable();
        pointer.gameObject.SetActive(false);
        if (device != null && device.added)
        {
            // Buttons up first, so nothing stays held in the UI; then the device goes, and the UI forgets its pointer.
            Write(Vector2.zero, Vector2.zero);
            InputSystem.RemoveDevice(device);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = navigationWas;
        }
    }

    private void Drive()
    {
        ControllerSettings settings = ControllerSettings.Current;
        float scale = Screen.height / 1080f;
        float dt = Time.unscaledDeltaTime;
        Vector2 stick = move.ReadValue<Vector2>();
        Vector2 delta = Vector2.zero;
        if (stick.sqrMagnitude > 0f)
        {
            delta = stick.normalized * Mathf.Pow(Mathf.Clamp01(stick.magnitude), settings.CursorResponse) * settings.CursorSpeed * scale * dt;
            position = new Vector2(Mathf.Clamp(position.x + delta.x, 0f, Screen.width - 1f), Mathf.Clamp(position.y + delta.y, 0f, Screen.height - 1f));
        }

        // A mouse wheel notch is 120; the stick turns it smoothly.
        Vector2 wheel = scroll.ReadValue<Vector2>();
        Write(delta, new Vector2(0f, wheel.y * settings.ScrollSpeed * 120f * dt));

        float size = settings.CursorSize * scale * GameOptions.UiScale.Value;
        pointer.sizeDelta = new Vector2(size, size);
        pointer.anchoredPosition = position;
    }

    private void Write(Vector2 delta, Vector2 wheel)
    {
        if (device == null || !device.added)
        {
            return;
        }

        var state = new MouseState { position = position, delta = delta, scroll = wheel }
            .WithButton(MouseButton.Left, active && click.IsPressed())
            .WithButton(MouseButton.Right, active && context.IsPressed());
        InputState.Change(device, state);
    }

    // The system pointer: hidden on a controller (the drawn one stands in, in windows), back when the mouse is used.
    private void UpdateSystemPointer()
    {
        bool pad = InputDevices.UsingGamepad;
        if (pad)
        {
            Cursor.visible = false;
        }
        else if (usedPad && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.visible = true;
        }

        usedPad = pad;
    }

    private void BuildView()
    {
        var root = new GameObject("Pad Cursor", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        // No scaler: positions are screen pixels, as the device's are.
        var go = new GameObject("Pointer", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        pointer = (RectTransform)go.transform;
        pointer.anchorMin = pointer.anchorMax = Vector2.zero;
        // The tip is the picture's top-left corner, as the system pointer's is.
        pointer.pivot = new Vector2(0.03f, 0.97f);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        Texture2D texture = UIStyle.Current.Cursor;
        if (texture != null)
        {
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0f, 1f));
            image.sprite = sprite;
        }

        go.SetActive(false);
    }

    private void OnDisable()
    {
        if (active)
        {
            Hide();
        }

        if (!InputDevices.UsingGamepad || Cursor.lockState == CursorLockMode.Locked)
        {
            return;
        }

        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        move?.Dispose();
        click?.Dispose();
        context?.Dispose();
        scroll?.Dispose();
        if (sprite != null)
        {
            Destroy(sprite);
        }

        if (current == this)
        {
            current = null;
        }
    }
}
