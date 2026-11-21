using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// What an <see cref="InGameCinematic"/> puts on screen while it plays: its own Cinemachine camera above the player's (the
/// brain glides into it and back out), black bars, the HUD faded away, the player held and safe, and a hold-to-skip hint.
/// Made when a cinematic begins and gone after it has glided back to the game's camera; it puts back everything it took.
/// </summary>
public sealed class CinematicView : MonoBehaviour
{
    private const float BarShare = 0.11f;
    private const float FadeSeconds = 0.6f;
    private const int BarsSortOrder = 240; // under the spoken lines (DialogueBox, 250), over the HUD

    // The brain's own blends, kept while any view has changed them (a second cinematic can begin as the first glides out).
    private static int alive;
    private static CinemachineBlendDefinition keptBlend;
    private static CinemachineBlenderSettings keptCustomBlends;
    private static CinemachineBrain keptBrain;

    private CinemachineCamera rig;
    private RectTransform topBar;
    private RectTransform bottomBar;
    private CanvasGroup hint;
    private bool hideHud;
    private readonly List<Canvas> hidden = new List<Canvas>();
    private GameObject heldPlayer;
    private Damageable health;
    private bool ending;
    private float endAt;
    private float show;      // 0..1: bars in, HUD out
    private float hintUntil = -1f;

    /// <summary>Takes the screen for a cinematic, gliding in over <paramref name="glideIn"/> seconds.</summary>
    public static CinematicView Begin(bool letterbox, bool hideHud, bool holdPlayer, bool skippable, float glideIn)
    {
        CloseWindows();
        var view = new GameObject("Cinematic Camera").AddComponent<CinematicView>();
        view.Setup(letterbox, hideHud, holdPlayer, skippable, glideIn);
        return view;
    }

    /// <summary>
    /// Closes every window and the world map as a cinematic begins (the user, 2026-11-21: a window open when an in-game cinematic
    /// started stayed up and couldn't be closed, since the cinematic holds the keys that would close it).
    /// </summary>
    public static void CloseWindows()
    {
        if (UIStateManager.Current != null)
        {
            UIStateManager.Current.CloseAll();
        }

        if (WorldMapHud.IsOpen)
        {
            WorldMapHud.Toggle();
        }
    }

    private void Setup(bool letterbox, bool hideHud, bool holdPlayer, bool skippable, float glideIn)
    {
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            if (alive == 0 || keptBrain != brain)
            {
                keptBrain = brain;
                keptBlend = brain.DefaultBlend;
                keptCustomBlends = brain.CustomBlends;
            }

            brain.CustomBlends = null;
            brain.DefaultBlend = Blend(glideIn);
        }

        alive++;
        // The rig starts where the game's camera is, so the brain's glide begins from there however it is set.
        Transform eye = Camera.main != null ? Camera.main.transform : null;
        if (eye != null)
        {
            transform.SetPositionAndRotation(eye.position, eye.rotation);
        }

        rig = gameObject.AddComponent<CinemachineCamera>();
        PlayerCameraController game = PlayerCameraController.Current;
        if (game != null && game.TryGetComponent(out CinemachineCamera gameCamera))
        {
            rig.Lens = gameCamera.Lens;
        }

        rig.Priority = 1000;
        gameObject.AddComponent<CinematicShake>();

        if (letterbox || skippable)
        {
            BuildScreen(letterbox, skippable);
        }

        this.hideHud = hideHud;
        if (hideHud)
        {
            HideHud();
        }

        if (holdPlayer && Player.Current != null)
        {
            // Held in the cinematic's own name: a scene change or a fall holding them too keeps them held after it.
            heldPlayer = Player.Current.gameObject;
            PlayerControl.Hold(heldPlayer, this);

            health = Player.Current.Health;
            if (health != null)
            {
                health.AddInvulnerability(this);
            }
        }
    }

    private static CinemachineBlendDefinition Blend(float seconds) => seconds > 0f
        ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, seconds)
        : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

    /// <summary>The camera this frame. <paramref name="cut"/>: jump there, without the brain smoothing from the last frame.</summary>
    public void Show(CameraShot.Pose pose, bool cut)
    {
        if (ending || rig == null)
        {
            return;
        }

        transform.SetPositionAndRotation(pose.Position, pose.Rotation);
        LensSettings lens = rig.Lens;
        lens.FieldOfView = pose.FieldOfView;
        rig.Lens = lens;
        if (cut)
        {
            rig.PreviousStateIsValid = false;
        }
    }

    /// <summary>Counts the skip key; true once it has been held long enough.</summary>
    public bool ReadSkip(HoldToSkip skip, float holdSeconds)
    {
        // Space and A also step a conversation shown in the shot (UI › Advance): while one is up they don't count to the skip,
        // which flashed its hint at every line and ended the shot when a line was held.
        bool talking = DialogueBox.IsOpen;
        bool held = !talking && ((Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed));
        bool anything = held || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (anything && !talking)
        {
            hintUntil = Time.unscaledTime + 3f;
        }

        return skip.Step(held && !GamePause.IsPaused, Time.unscaledDeltaTime, holdSeconds);
    }

    /// <summary>Gives the screen back, gliding to the game's camera over <paramref name="glideOut"/> seconds, then goes.</summary>
    public void End(float glideOut)
    {
        if (ending)
        {
            return;
        }

        ending = true;
        endAt = Time.time + Mathf.Max(glideOut, FadeSeconds);
        if (keptBrain != null)
        {
            keptBrain.DefaultBlend = Blend(glideOut);
        }

        if (rig != null)
        {
            rig.Priority = -1000;
        }

        ShowHud();
        if (heldPlayer != null)
        {
            PlayerControl.Release(heldPlayer, this);
            heldPlayer = null;
        }

        if (health != null)
        {
            health.RemoveInvulnerability(this);
            health = null;
        }

        if (glideOut <= 0f || !isActiveAndEnabled)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        float target = ending ? 0f : 1f;
        show = Mathf.MoveTowards(show, target, Time.unscaledDeltaTime / FadeSeconds);
        float eased = show * show * (3f - 2f * show);
        if (topBar != null)
        {
            topBar.anchorMin = new Vector2(0f, 1f - BarShare * eased);
            bottomBar.anchorMax = new Vector2(1f, BarShare * eased);
        }

        // The fog of war is laid on the ground the player stands on: seen from a camera far from the player it shows as
        // black bands, so it fades out while the cinematic has the camera.
        AtmosphereFeature.WarScale = 1f - eased;

        // Paused, the menu (in the UI root) must show; the cinematic waits meanwhile. So must a window the player opens during a
        // cinematic that leaves them free (the morning's introductions): it opened unseen, and the conversation hid under it.
        if (hideHud && !ending)
        {
            bool shown = GamePause.IsPaused || HudGivesWay.Covered;
            if (shown && hidden.Count > 0) ShowHud();
            else if (!shown && hidden.Count == 0) HideHud();
        }

        if (hint != null)
        {
            hint.alpha = Mathf.MoveTowards(hint.alpha, !ending && Time.unscaledTime < hintUntil ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }

        if (ending && Time.time >= endAt)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (heldPlayer != null) PlayerControl.Release(heldPlayer, this);
        if (health != null) health.RemoveInvulnerability(this);
        ShowHud();
        if (topBar != null) Destroy(topBar.root.gameObject);

        alive = Mathf.Max(0, alive - 1);
        if (alive == 0 && keptBrain != null)
        {
            keptBrain.DefaultBlend = keptBlend;
            keptBrain.CustomBlends = keptCustomBlends;
            keptBrain = null;
        }

        if (alive == 0)
        {
            AtmosphereFeature.WarScale = 1f;
        }
    }

    // Every screen canvas under the bars (the UI root, the minimap, the quest arrows, messages, hints), but not the spoken
    // lines (Dialogue Box, drawn over the bars). Nested canvases too: switching off only the root left the HUD showing.
    private void HideHud()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            Canvas root = canvas.rootCanvas;
            if (canvas.enabled && root.renderMode != RenderMode.WorldSpace && root.sortingOrder < BarsSortOrder
                && root.gameObject.name != "Dialogue Box")
            {
                canvas.enabled = false;
                hidden.Add(canvas);
            }
        }
    }

    private void ShowHud()
    {
        foreach (Canvas canvas in hidden)
        {
            if (canvas != null) canvas.enabled = true;
        }

        hidden.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        alive = 0;
        keptBrain = null;
        AtmosphereFeature.WarScale = 1f;
        keptBlend = default;
    }

    private void BuildScreen(bool letterbox, bool skippable)
    {
        var root = new GameObject("Cinematic Bars");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = BarsSortOrder;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());

        topBar = Bar("Top", root.transform, new Vector2(0f, 1f), Vector2.one);
        bottomBar = Bar("Bottom", root.transform, Vector2.zero, new Vector2(1f, 0f));
        if (!letterbox)
        {
            topBar.gameObject.SetActive(false);
            bottomBar.gameObject.SetActive(false);
        }

        if (!skippable)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        RectTransform corner = CodeUI.Box("Skip Hint", root.transform, new Vector2(1f, 0f), new Vector2(300f, 34f), new Vector2(-180f, 36f));
        hint = corner.gameObject.AddComponent<CanvasGroup>();
        hint.alpha = 0f;
        hint.blocksRaycasts = false;
        TMP_Text label = CodeUI.Label("Label", corner, 0f, 0f, 300f, 34f, 18f, style != null ? style.Muted : Color.gray, TextAlignmentOptions.Right);
        label.text = InputDevices.UsingGamepad ? "Hold A to skip" : "Hold Space to skip";
        if (style != null)
        {
            style.OnWorld(label);
        }
    }

    private static RectTransform Bar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform bar = CodeUI.Stretch(name + " Bar", parent);
        bar.anchorMin = anchorMin;
        bar.anchorMax = anchorMax;
        var image = bar.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return bar;
    }
}
