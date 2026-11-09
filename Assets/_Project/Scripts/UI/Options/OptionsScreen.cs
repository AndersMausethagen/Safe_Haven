using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Options screen, opened from the Escape menu and the main menu: Gameplay, Graphics, Audio, Controls and
/// Accessibility. It is drawn from the settings themselves (GameOptions, AudioManager, CameraPreferences, InputRebinds) and
/// holds no game rules. Changes apply at once and are remembered in PlayerPrefs. A change to the window asks to be kept
/// within ten seconds and otherwise goes back, so a resolution the screen can't show can't strand the player. Built in code
/// when first opened in a scene; Escape or the gamepad's back button close it.
/// </summary>
public sealed class OptionsScreen : MonoBehaviour
{
    private const float ConfirmSeconds = 10f;
    private const float KeyColumn = 210f;

    private static readonly string[] TabNames = { "Gameplay", "Graphics", "Audio", "Controls", "Accessibility" };

    private static OptionsScreen current;
    private static int closedFrame = -1;

    private readonly List<Action> refreshers = new List<Action>();
    private readonly List<(Button button, TMP_Text label)> tabs = new List<(Button, TMP_Text)>();
    private GameObject window;
    private RectTransform content;
    private ScrollRect scroll;
    private TMP_Text helpLine;
    private GameObject confirmBar;
    private TMP_Text confirmText;
    private int tab;
    private Action closed;
    private float confirmUntil = -1f;
    private int keptMode;
    private Vector2Int keptResolution;
    private int writtenCountdown = -1;

    /// <summary>True while the screen is up, or was until this frame: the same Escape press must not close the menu under it.</summary>
    public static bool HasEscape => (current != null && current.window.activeSelf) || closedFrame == Time.frameCount;

    /// <summary>Shows the screen. <paramref name="onClosed"/> runs when it goes away.</summary>
    public static void Open(Action onClosed = null)
    {
        if (current == null)
        {
            Create();
        }

        current.closed = onClosed;
        current.window.SetActive(true);
        current.ShowTab(current.tab);
    }

    private static void Create()
    {
        var root = new GameObject("Options Screen");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var screen = root.AddComponent<OptionsScreen>();
        screen.Build(root.transform);
        root.SetActive(true);
        current = screen;
    }

    private void OnEnable()
    {
        if (GameInput.Pause != null) GameInput.Pause.performed += OnBackPressed;
        if (GameInput.Cancel != null) GameInput.Cancel.performed += OnBackPressed;
    }

    private void OnDisable()
    {
        if (GameInput.Pause != null) GameInput.Pause.performed -= OnBackPressed;
        if (GameInput.Cancel != null) GameInput.Cancel.performed -= OnBackPressed;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private void OnBackPressed(InputAction.CallbackContext context)
    {
        if (window.activeSelf && !InputRebinds.IsListening)
        {
            Close();
        }
    }

    public void Close()
    {
        // Leaving with the question open keeps what the player can evidently see.
        confirmUntil = -1f;
        confirmBar.SetActive(false);
        PlayerPrefs.Save();
        window.SetActive(false);
        closedFrame = Time.frameCount;
        Action then = closed;
        closed = null;
        then?.Invoke();
    }

    private void Update()
    {
        if (confirmUntil < 0f)
        {
            return;
        }

        float left = confirmUntil - Time.unscaledTime;
        int countdown = Mathf.CeilToInt(Mathf.Max(0f, left));
        if (countdown != writtenCountdown)
        {
            writtenCountdown = countdown;
            confirmText.text = $"Keep these display settings? Going back in {countdown} s.";
        }
        if (left <= 0f)
        {
            RevertDisplay();
        }
    }

    // ----- Building -----

    private void Build(Transform root)
    {
        RectTransform dim = OptionsWidgets.Rect("Dim", root);
        OptionsWidgets.Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        window = dim.gameObject;

        RectTransform panel = OptionsWidgets.Place(OptionsWidgets.Rect("Panel", dim), new Vector2(0.5f, 0.5f), new Vector2(1280f, 900f), Vector2.zero);
        panel.gameObject.AddComponent<FitInsideParent>();
        panel.gameObject.AddComponent<Image>().color = OptionsWidgets.Panel;
        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = OptionsWidgets.WindowRim;
        rim.effectDistance = new Vector2(2f, -2f);
        HearthlineArt.AddFrame(panel, UIStyle.Current, RaidDirector.Current != null);

        TMP_Text title = OptionsWidgets.Text("Title", panel, "Options", 38f, OptionsWidgets.Title);
        OptionsWidgets.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(400f, 56f), new Vector2(36f, -22f));
        title.fontStyle = FontStyles.Bold;

        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            Button button = OptionsWidgets.Button(TabNames[i], panel, TabNames[i], 22f, () => ShowTab(index), out TMP_Text label);
            OptionsWidgets.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(232f, 48f), new Vector2(36f + i * 244f, -88f));
            tabs.Add((button, label));
        }

        RectTransform view = OptionsWidgets.Rect("View", panel);
        OptionsWidgets.Stretch(view, new Vector2(36f, 132f), new Vector2(-36f, -150f));
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        view.gameObject.AddComponent<RectMask2D>();
        content = OptionsWidgets.Rect("Content", view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(-18f, 0f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.verticalScrollbar = ScrollBar(panel);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        helpLine = OptionsWidgets.Text("Help", panel, "", 21f, OptionsWidgets.Muted);
        OptionsWidgets.Place((RectTransform)helpLine.transform, new Vector2(0f, 0f), new Vector2(1208f, 54f), new Vector2(36f, 76f));

        Button reset = OptionsWidgets.Button("Reset", panel, "Reset This Tab", 22f, ResetTab, out _);
        OptionsWidgets.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(240f, 50f), new Vector2(36f, 20f));
        Button back = OptionsWidgets.Button("Back", panel, "Back", 22f, Close, out _);
        OptionsWidgets.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(240f, 50f), new Vector2(-36f, 20f));

        RectTransform bar = OptionsWidgets.Place(OptionsWidgets.Rect("Confirm", panel), new Vector2(0.5f, 0f), new Vector2(700f, 50f), new Vector2(0f, 20f));
        confirmBar = bar.gameObject;
        confirmText = OptionsWidgets.Text("Text", bar, "", 20f, OptionsWidgets.Title);
        OptionsWidgets.Stretch((RectTransform)confirmText.transform, Vector2.zero, new Vector2(-220f, 0f));
        Button keep = OptionsWidgets.Button("Keep", bar, "Keep", 20f, KeepDisplay, out _);
        OptionsWidgets.Place((RectTransform)keep.transform, new Vector2(1f, 0.5f), new Vector2(100f, 44f), new Vector2(-110f, 0f));
        Button revert = OptionsWidgets.Button("Revert", bar, "Revert", 20f, RevertDisplay, out _);
        OptionsWidgets.Place((RectTransform)revert.transform, new Vector2(1f, 0.5f), new Vector2(100f, 44f), Vector2.zero);
        confirmBar.SetActive(false);
        window.SetActive(false);
    }

    private static Scrollbar ScrollBar(RectTransform panel)
    {
        RectTransform track = OptionsWidgets.Rect("Scrollbar", panel);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = new Vector2(1f, 1f);
        track.pivot = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(-46f, 132f);
        track.offsetMax = new Vector2(-34f, -150f);
        track.gameObject.AddComponent<Image>().color = OptionsWidgets.Track;
        RectTransform area = OptionsWidgets.Rect("Area", track);
        OptionsWidgets.Stretch(area);
        RectTransform handle = OptionsWidgets.Rect("Handle", area);
        handle.sizeDelta = Vector2.zero;
        var image = handle.gameObject.AddComponent<Image>();
        image.color = OptionsWidgets.Rim;
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle;
        bar.targetGraphic = image;
        bar.direction = Scrollbar.Direction.BottomToTop;
        return bar;
    }

    // ----- Tabs -----

    private void ShowTab(int index)
    {
        tab = index;
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i].label.color = i == tab ? OptionsWidgets.Title : OptionsWidgets.Body;
            tabs[i].label.fontStyle = i == tab ? FontStyles.Bold : FontStyles.Normal;
        }

        Clear();
        ShowHelp("");
        switch (index)
        {
            case 0:
                BuildGameplay();
                break;
            case 1:
                BuildGraphics();
                break;
            case 2:
                BuildAudio();
                break;
            case 3:
                BuildControls();
                break;
            default:
                Rows(GameOptions.Accessibility);
                break;
        }

        scroll.verticalNormalizedPosition = 1f;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(tabs[tab].button.gameObject);
        }
    }

    private void Clear()
    {
        refreshers.Clear();
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }

    private void Rows(IEnumerable<GameOption> options)
    {
        foreach (GameOption option in options)
        {
            refreshers.Add(OptionsWidgets.Row(content, option, ShowHelp));
        }
    }

    private void RefreshRows()
    {
        foreach (Action refresh in refreshers)
        {
            refresh();
        }
    }

    private void ShowHelp(string line)
    {
        helpLine.text = line;
    }

    private void BuildGameplay()
    {
        OptionsWidgets.Section(content, "Camera");
        refreshers.Add(OptionsWidgets.ChoiceRow(content, "Camera Style", "The style the camera key switches between. Both work everywhere.",
            () => new[] { CameraStyleRules.Label(CameraStyle.TopDown), CameraStyleRules.Label(CameraStyle.Action) },
            () => CameraPreferences.Chosen == CameraStyle.Action ? 1 : 0,
            index => CameraPreferences.Chosen = index == 1 ? CameraStyle.Action : CameraStyle.TopDown, ShowHelp));
        var choices = new string[3];
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i] = CameraStyleRules.Label((CameraStyleOverride)i);
        }

        for (int i = 0; i < CameraStyleRules.ContextCount; i++)
        {
            var context = (CameraContext)i;
            refreshers.Add(OptionsWidgets.ChoiceRow(content, context.ToString(), $"Always use one style in this situation: {context}, whatever the camera key says.", () => choices,
                () => (int)CameraPreferences.GetOverride(context), index => CameraPreferences.SetOverride(context, (CameraStyleOverride)index), ShowHelp));
        }

        OptionsWidgets.Section(content, "Play");
        Rows(GameOptions.Gameplay);
    }

    private void BuildGraphics()
    {
        OptionsWidgets.Section(content, "Display");
        refreshers.Add(OptionsWidgets.ChoiceRow(content, "Display Mode", "How the game fills the screen.", () => DisplayOptions.ModeNames, () => DisplayOptions.Mode,
            index => ChangeDisplay(() => DisplayOptions.Mode = index), ShowHelp));
        List<Vector2Int> sizes = DisplayOptions.Resolutions();
        var names = new string[sizes.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = $"{sizes[i].x} x {sizes[i].y}";
        }

        refreshers.Add(OptionsWidgets.ChoiceRow(content, "Resolution", "The size of the picture on screen.", () => names, () => sizes.IndexOf(DisplayOptions.Resolution),
            index => ChangeDisplay(() => DisplayOptions.Resolution = sizes[index]), ShowHelp));
        Rows(new[] { GameOptions.VSync, GameOptions.FrameLimit });

        OptionsWidgets.Section(content, "Quality");
        Rows(GameOptions.Graphics);
        OptionsWidgets.Section(content, "Look");
        Rows(GameOptions.Look);
    }

    private void BuildAudio()
    {
        OptionsWidgets.Section(content, "Volume");
        Volume("Master Volume", "Everything together.", () => AudioManager.MasterVolume, value => AudioManager.MasterVolume = value);
        Volume("Music", "The score.", () => AudioManager.GetVolume(AudioChannel.Music), value => AudioManager.SetVolume(AudioChannel.Music, value));
        Volume("Ambience", "Wind, rain, water and the forest.", () => AudioManager.GetVolume(AudioChannel.Ambience), value => AudioManager.SetVolume(AudioChannel.Ambience, value));
        Volume("Sound Effects", "Fights, footsteps, work and the interface.", () => AudioManager.GetVolume(AudioChannel.Effects), value => AudioManager.SetVolume(AudioChannel.Effects, value));
        OptionsWidgets.Section(content, "Other");
        Rows(new[] { GameOptions.MuteUnfocused });
    }

    private void Volume(string name, string help, Func<float> get, Action<float> set)
    {
        refreshers.Add(OptionsWidgets.SliderRow(content, name, help, 0f, 1f, 0.01f, get, set, value => Mathf.RoundToInt(value * 100f) + "%", ShowHelp));
    }

    private void BuildControls()
    {
        OptionsWidgets.Section(content, "Camera");
        Rows(GameOptions.Controls);
        OptionsWidgets.Note(content, "Below: keyboard and mouse on the left, gamepad on the right. Pick one, then press the new key. Escape cancels.");
        string section = null;
        foreach (RebindRow row in InputRebinds.Rows)
        {
            if (row.Section != section)
            {
                section = row.Section;
                OptionsWidgets.Section(content, section);
            }

            KeyRow(row);
        }
    }

    private void KeyRow(RebindRow row)
    {
        RectTransform control = OptionsWidgets.LabelledRow(content, row.Label, "Pick a key, then press the new one. If it's in use, the two swap.", ShowHelp);
        Action keyboard = KeyButton(control, row, GameInput.KeyboardMouse, new Vector2(0f, 0.5f), new Vector2(0f, 0f));
        Action gamepad = KeyButton(control, row, GameInput.Gamepad, new Vector2(0f, 0.5f), new Vector2(KeyColumn + 12f, 0f));
        refreshers.Add(keyboard);
        refreshers.Add(gamepad);
    }

    private Action KeyButton(RectTransform control, RebindRow row, string scheme, Vector2 anchor, Vector2 position)
    {
        TMP_Text label = null;
        Button button = OptionsWidgets.Button(scheme, control, "", 21f, () =>
        {
            if (InputRebinds.IsListening)
            {
                return;
            }

            label.text = "Press a key...";
            InputRebinds.Listen(row, scheme, note =>
            {
                ShowHelp(note ?? "");
                RefreshRows();
            });
        }, out label);
        OptionsWidgets.Place((RectTransform)button.transform, anchor, new Vector2(KeyColumn, 42f), position);
        button.interactable = InputRebinds.HasBinding(row, scheme);
        void Refresh() => label.text = InputRebinds.Describe(row, scheme);
        Refresh();
        return Refresh;
    }

    private void ResetTab()
    {
        switch (tab)
        {
            case 0:
                ResetAll(GameOptions.Gameplay);
                CameraPreferences.Chosen = CameraStyle.TopDown;
                for (int i = 0; i < CameraStyleRules.ContextCount; i++)
                {
                    CameraPreferences.SetOverride((CameraContext)i, 0);
                }

                break;
            case 1:
                ResetAll(GameOptions.Look);
                GameOptions.Upscaling.ResetToDefault();
                GameOptions.Preset.ResetToDefault();
                GameOptions.ApplyPreset(GameOptions.PresetUltra);
                GameOptions.VSync.ResetToDefault();
                GameOptions.FrameLimit.ResetToDefault();
                break;
            case 2:
                ResetAudio();
                break;
            case 3:
                ResetAll(GameOptions.Controls);
                InputRebinds.ResetAll();
                break;
            default:
                ResetAll(GameOptions.Accessibility);
                break;
        }

        RefreshRows();
    }

    private static void ResetAll(IEnumerable<GameOption> options)
    {
        foreach (GameOption option in options)
        {
            option.ResetToDefault();
        }
    }

    private static void ResetAudio()
    {
        GameOptions.MuteUnfocused.ResetToDefault();
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        if (sounds == null)
        {
            return;
        }

        AudioManager.MasterVolume = sounds.MasterVolume;
        foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
        {
            AudioManager.SetVolume(channel, sounds.DefaultVolume(channel));
        }
    }

    // ----- The window: a change has to be kept, or it goes back -----

    private void ChangeDisplay(Action change)
    {
        if (confirmUntil < 0f)
        {
            keptMode = DisplayOptions.Mode;
            keptResolution = DisplayOptions.Resolution;
        }

        change();
        DisplayOptions.Apply();
        // The Editor's Game view doesn't change size, so there is nothing to confirm.
        if (!Application.isEditor)
        {
            confirmUntil = Time.unscaledTime + ConfirmSeconds;
            confirmBar.SetActive(true);
        }

        RefreshRows();
    }

    private void KeepDisplay()
    {
        confirmUntil = -1f;
        confirmBar.SetActive(false);
    }

    private void RevertDisplay()
    {
        DisplayOptions.Mode = keptMode;
        DisplayOptions.Resolution = keptResolution;
        DisplayOptions.Apply();
        KeepDisplay();
        RefreshRows();
    }
}
