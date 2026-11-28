using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Options screen, opened from the Escape menu and the main menu: Gameplay, Graphics, Audio, Controls and Accessibility. It is drawn
/// from the settings themselves (GameOptions, AudioManager, CameraPreferences, DisplayOptions, InputRebinds) and holds no game rules.
/// <para>
/// Changes wait (the user, 2026-11-20: "Give us the option to Save changes and not automatically change settings when we press the
/// button. Prompt the player that he has unsaved settings"): every row edits an <see cref="OptionsDraft"/>; a changed row is marked in
/// gold and the header counts what is unsaved; Save Changes applies it all, Discard forgets it, and leaving with changes asks Save,
/// Leave Without Saving or Keep Editing. Keys are the one exception: a new key is kept as soon as it is pressed, as the Controls tab says.
/// The panel on the right explains the row in focus: what it does, its value now, saved and by default.
/// </para>
/// <para>
/// A saved change to the window asks to be kept within ten seconds and otherwise goes back, so a resolution the screen can't show can't
/// strand the player. Built in code when first opened in a scene; Escape or the gamepad's back button close it (or the question).
/// </para>
/// </summary>
public sealed class OptionsScreen : MonoBehaviour
{
    private const float ConfirmSeconds = 10f;
    private const float KeyColumn = 180f;
    private const float PanelWidth = 1280f;
    private const float PanelHeight = 900f;
    private const float DetailsWidth = 400f;
    private const float Margin = 36f;
    private const float ListTop = 150f;
    private const float ListBottom = 96f;

    private const string DisplayModeKey = "display.mode";
    private const string ResolutionKey = "display.resolution";

    private static readonly string[] TabNames = { "Gameplay", "Graphics", "Audio", "Controls", "Accessibility" };

    private static readonly string[] TabIntros =
    {
        "How the game plays: the camera, help with gathering and drinking, and what the screen shows while you play.",
        "The window and how the world is drawn. Ultra is the game as it was made; lower settings run faster on slower machines.",
        "How loud each part of the game is.",
        "How the camera turns, and every key. A new key is kept as soon as you press it; everything else waits for Save Changes.",
        "Sizes, motion, timing and assists, for an easier or clearer game.",
    };

    private static OptionsScreen current;
    private static int closedFrame = -1;

    private readonly OptionsDraft draft = new OptionsDraft();
    private readonly List<Action> refreshers = new List<Action>();
    private readonly List<(Button button, TMP_Text label)> tabs = new List<(Button, TMP_Text)>();
    private GameObject window;
    private RectTransform content;
    private ScrollRect scroll;
    private TMP_Text status;
    private TMP_Text detailName;
    private TMP_Text detailValue;
    private TMP_Text detailHelp;
    private Button saveButton;
    private TMP_Text saveLabel;
    private Button discardButton;
    private GameObject confirmBar;
    private TMP_Text confirmText;
    private GameObject askBox;
    private TMP_Text askText;
    private Button askSave;
    private OptionsFocus focused;
    private int tab;
    private Action closed;
    private float confirmUntil = -1f;
    private int keptMode;
    private Vector2Int keptResolution;
    private int writtenCountdown = -1;
    // Escape is both Pause and Cancel: one press calls back twice in a frame, which opened the question and closed it again.
    private int backFrame = -1;

    /// <summary>True while the screen is up, or was until this frame: the same Escape press must not close the menu under it.</summary>
    public static bool HasEscape => (current != null && current.window.activeSelf) || closedFrame == Time.frameCount;

    /// <summary>The changes not saved yet on the open screen (0 when it is closed): for tests and probes.</summary>
    public static int UnsavedChanges => current != null ? current.draft.Count : 0;

    /// <summary>Shows the screen. <paramref name="onClosed"/> runs when it goes away.</summary>
    public static void Open(Action onClosed = null)
    {
        if (current == null)
        {
            Create();
        }

        current.closed = onClosed;
        current.draft.Discard();
        current.askBox.SetActive(false);
        current.window.SetActive(true);
        current.ShowTab(current.tab);
        current.ShowStatus();
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
        draft.Changed += OnDraftChanged;
        PadTabs.Register(this, () => window != null && window.activeSelf && !askBox.activeSelf, direction => ShowTab(PadTabs.Next(tab, tabs.Count, direction)),
            () => tabs.ConvertAll(entry => (RectTransform)entry.button.transform));
    }

    private void OnDisable()
    {
        EndPreview();
        InputRebinds.CancelListening();
        if (GameInput.Pause != null) GameInput.Pause.performed -= OnBackPressed;
        if (GameInput.Cancel != null) GameInput.Cancel.performed -= OnBackPressed;
        draft.Changed -= OnDraftChanged;
        PadTabs.Unregister(this);
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
        if (backFrame == Time.frameCount)
        {
            return;
        }

        backFrame = Time.frameCount;
        // Back while a key is awaited stops the wait: a pad player on a keyboard row could press nothing that ended it.
        if (InputRebinds.IsListening)
        {
            InputRebinds.CancelListening();
            return;
        }

        if (!window.activeSelf)
        {
            return;
        }

        // Back during a preview only ends the preview.
        if (previewUntil >= 0f)
        {
            EndPreview();
            return;
        }

        // Back with the question up is "Keep Editing".
        if (askBox.activeSelf)
        {
            KeepEditing();
            return;
        }

        Close();
    }

    /// <summary>Closes the screen, or, with unsaved changes, asks first whether to save them.</summary>
    public void Close()
    {
        // Leaving while a key is awaited binds nothing, and gives gameplay its input back.
        InputRebinds.CancelListening();
        if (draft.HasChanges)
        {
            AskBeforeLeaving();
            return;
        }

        Leave();
    }

    private void Leave()
    {
        EndPreview();
        // Leaving with the window's question open keeps what the player can evidently see.
        confirmUntil = -1f;
        confirmBar.SetActive(false);
        askBox.SetActive(false);
        draft.Discard();
        PlayerPrefs.Save();
        window.SetActive(false);
        closedFrame = Time.frameCount;
        Action then = closed;
        closed = null;
        then?.Invoke();
    }

    private void Update()
    {
        UpdatePreview();
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
        UIStyle style = UIStyle.Current;
        RectTransform dim = OptionsWidgets.Rect("Dim", root);
        OptionsWidgets.Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        window = dim.gameObject;

        RectTransform panel = OptionsWidgets.Place(OptionsWidgets.Rect("Panel", dim), new Vector2(0.5f, 0.5f), new Vector2(PanelWidth, PanelHeight), Vector2.zero);
        panel.gameObject.AddComponent<FitInsideParent>();
        panel.gameObject.AddComponent<Image>().color = OptionsWidgets.Panel;
        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = OptionsWidgets.WindowRim;
        rim.effectDistance = new Vector2(2f, -2f);
        HearthlineArt.AddFrame(panel, style, RaidDirector.Current != null);

        TMP_Text title = OptionsWidgets.Text("Title", panel, "Options", 40f, OptionsWidgets.Title);
        OptionsWidgets.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(400f, 56f), new Vector2(Margin, -22f));
        // The window's name carved in the display face, as every window's is (Hearthline 2); never bold (specks round the letters).
        style.Display(title);
        title.overflowMode = TextOverflowModes.Overflow;
        status = OptionsWidgets.Text("Status", panel, "", 22f, OptionsWidgets.Muted, TextAlignmentOptions.MidlineRight);
        OptionsWidgets.Place((RectTransform)status.transform, new Vector2(1f, 1f), new Vector2(560f, 56f), new Vector2(-Margin, -22f));

        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            Button button = OptionsWidgets.Button(TabNames[i], panel, TabNames[i], 22f, () => ShowTab(index), out TMP_Text label);
            OptionsWidgets.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(232f, 48f), new Vector2(Margin + i * 244f, -88f));
            tabs.Add((button, label));
        }

        // The list on the left, the details of the row in focus on the right.
        float listRight = Margin + DetailsWidth + 24f;
        RectTransform view = OptionsWidgets.Rect("View", panel);
        OptionsWidgets.Stretch(view, new Vector2(Margin, ListBottom), new Vector2(-listRight, -ListTop));
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
        scroll.verticalScrollbar = ScrollBar(panel, listRight);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        BuildDetails(panel);

        Button reset = OptionsWidgets.Button("Reset", panel, "Reset This Tab", 22f, ResetTab, out _);
        OptionsWidgets.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(220f, 50f), new Vector2(Margin, 26f));
        Button back = OptionsWidgets.Button("Back", panel, "Back", 22f, Close, out _);
        OptionsWidgets.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(170f, 50f), new Vector2(-Margin, 26f));
        saveButton = OptionsWidgets.Button("Save", panel, "Save Changes", 22f, Save, out saveLabel);
        OptionsWidgets.Place((RectTransform)saveButton.transform, new Vector2(1f, 0f), new Vector2(240f, 50f), new Vector2(-Margin - 182f, 26f));
        discardButton = OptionsWidgets.Button("Discard", panel, "Discard", 22f, Discard, out _);
        OptionsWidgets.Place((RectTransform)discardButton.transform, new Vector2(1f, 0f), new Vector2(170f, 50f), new Vector2(-Margin - 434f, 26f));

        RectTransform bar = OptionsWidgets.Place(OptionsWidgets.Rect("Confirm", panel), new Vector2(0.5f, 0f), new Vector2(760f, 50f), new Vector2(-60f, ListBottom - 60f));
        Image barFace = HearthKit.Sliced(bar.gameObject.AddComponent<Image>(), style.Card, 1.2f);
        barFace.color = style.Inset;
        HearthKit.Rim(bar, 0.6f);
        confirmBar = bar.gameObject;
        confirmText = OptionsWidgets.Text("Text", bar, "", 20f, OptionsWidgets.Title);
        OptionsWidgets.Stretch((RectTransform)confirmText.transform, new Vector2(12f, 0f), new Vector2(-220f, 0f));
        Button keep = OptionsWidgets.Button("Keep", bar, "Keep", 20f, KeepDisplay, out _);
        OptionsWidgets.Place((RectTransform)keep.transform, new Vector2(1f, 0.5f), new Vector2(100f, 44f), new Vector2(-110f, 0f));
        Button revert = OptionsWidgets.Button("Revert", bar, "Revert", 20f, RevertDisplay, out _);
        OptionsWidgets.Place((RectTransform)revert.transform, new Vector2(1f, 0.5f), new Vector2(100f, 44f), Vector2.zero);
        confirmBar.SetActive(false);

        BuildQuestion(panel);
        window.SetActive(false);
    }

    private void BuildDetails(RectTransform panel)
    {
        UIStyle style = UIStyle.Current;
        RectTransform box = OptionsWidgets.Rect("Details", panel);
        box.anchorMin = new Vector2(1f, 0f);
        box.anchorMax = new Vector2(1f, 1f);
        box.pivot = new Vector2(1f, 0.5f);
        box.offsetMin = new Vector2(-Margin - DetailsWidth, ListBottom);
        box.offsetMax = new Vector2(-Margin, -ListTop);
        // Hearthline 3: a card with a gold rim and the kit's mark, as the other windows' panels are (the user, 2026-11-21: "proper art work").
        Image face = HearthKit.Sliced(box.gameObject.AddComponent<Image>(), style.Card, 1.2f);
        face.color = style.Inset;
        face.raycastTarget = false;
        HearthKit.Rim(box, 0.45f);
        HearthKit.Icon(box, "Info", 20f, 20f, 30f);
        detailName = OptionsWidgets.Text("Name", box, "", 28f, OptionsWidgets.Title, TextAlignmentOptions.TopLeft);
        style.Title(detailName);
        detailName.fontStyle = FontStyles.Bold;
        OptionsWidgets.Place((RectTransform)detailName.transform, new Vector2(0f, 1f), new Vector2(DetailsWidth - 82f, 76f), new Vector2(62f, -18f));
        detailValue = OptionsWidgets.Text("Value", box, "", 20f, style.Accent, TextAlignmentOptions.TopLeft);
        OptionsWidgets.Place((RectTransform)detailValue.transform, new Vector2(0f, 1f), new Vector2(DetailsWidth - 40f, 96f), new Vector2(20f, -96f));
        RectTransform rule = OptionsWidgets.Place(OptionsWidgets.Rect("Rule", box), new Vector2(0f, 1f), new Vector2(DetailsWidth - 40f, 2f), new Vector2(20f, -196f));
        rule.gameObject.AddComponent<Image>().color = HearthKit.Faded(style.Accent, 0.35f);
        detailHelp = OptionsWidgets.Text("Help", box, "", 21f, OptionsWidgets.Body, TextAlignmentOptions.TopLeft);
        RectTransform help = (RectTransform)detailHelp.transform;
        help.anchorMin = new Vector2(0f, 0f);
        help.anchorMax = new Vector2(1f, 1f);
        help.offsetMin = new Vector2(20f, 76f);
        // One setting back to its default (the systems review, 2026-11-21: reset was per tab only), staged like any change.
        resetRow = OptionsWidgets.Button("Reset Row", box, "Reset to Default", 19f, ResetFocused, out _);
        OptionsWidgets.Place((RectTransform)resetRow.transform, new Vector2(0.5f, 0f), new Vector2(DetailsWidth - 40f, 46f), new Vector2(0f, 18f));
        ((RectTransform)resetRow.transform).pivot = new Vector2(0.5f, 0f);
        resetRow.gameObject.SetActive(false);
        previewRow = OptionsWidgets.Button("Preview Row", box, "Preview", 19f, StartPreview, out _);
        ((RectTransform)previewRow.transform).pivot = new Vector2(0.5f, 0f);
        previewRow.gameObject.SetActive(false);
        help.offsetMax = new Vector2(-20f, -212f);
    }

    // "You have unsaved changes": Save and Leave, Leave Without Saving, Keep Editing.
    private void BuildQuestion(RectTransform panel)
    {
        RectTransform shade = OptionsWidgets.Rect("Unsaved Question", panel);
        OptionsWidgets.Stretch(shade);
        shade.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        askBox = shade.gameObject;
        RectTransform box = OptionsWidgets.Place(OptionsWidgets.Rect("Box", shade), new Vector2(0.5f, 0.5f), new Vector2(860f, 360f), Vector2.zero);
        // The gilded panel every window has: its ground, rim and scroll corners (clear of them: the words and buttons inset).
        HearthKit.Dress(box);
        TMP_Text heading = OptionsWidgets.Text("Heading", box, "Unsaved changes", 32f, OptionsWidgets.Title, TextAlignmentOptions.TopLeft);
        UIStyle.Current.Title(heading);
        heading.fontStyle = FontStyles.Bold;
        OptionsWidgets.Place((RectTransform)heading.transform, new Vector2(0f, 1f), new Vector2(700f, 48f), new Vector2(80f, -34f));
        HearthKit.Rule(box, 60f, 88f, 740f);
        askText = OptionsWidgets.Text("Text", box, "", 22f, OptionsWidgets.Body, TextAlignmentOptions.TopLeft);
        OptionsWidgets.Place((RectTransform)askText.transform, new Vector2(0f, 1f), new Vector2(720f, 140f), new Vector2(70f, -104f));
        askSave = OptionsWidgets.Button("Save", box, "Save and Leave", 21f, SaveAndLeave, out _);
        OptionsWidgets.Place((RectTransform)askSave.transform, new Vector2(0f, 0f), new Vector2(220f, 52f), new Vector2(70f, 44f));
        Button leave = OptionsWidgets.Button("Leave", box, "Leave Without Saving", 21f, () =>
        {
            draft.Discard();
            Leave();
        }, out _);
        OptionsWidgets.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(250f, 52f), new Vector2(0f, 44f));
        Button keep = OptionsWidgets.Button("Keep Editing", box, "Keep Editing", 21f, KeepEditing, out _);
        OptionsWidgets.Place((RectTransform)keep.transform, new Vector2(1f, 0f), new Vector2(220f, 52f), new Vector2(-70f, 44f));
        askBox.SetActive(false);
    }

    private static Scrollbar ScrollBar(RectTransform panel, float listRight)
    {
        RectTransform track = OptionsWidgets.Rect("Scrollbar", panel);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = new Vector2(1f, 1f);
        track.pivot = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(-listRight - 12f, ListBottom);
        track.offsetMax = new Vector2(-listRight, -ListTop);
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

    // ----- The draft: what is changed, saving it, forgetting it -----

    private void OnDraftChanged()
    {
        ShowStatus();
        ShowDetails();
    }

    private void ShowStatus()
    {
        UIStyle style = UIStyle.Current;
        int count = draft.Count;
        status.text = count == 0 ? "All changes saved" : $"{count} unsaved change{(count == 1 ? "" : "s")}";
        status.color = count == 0 ? OptionsWidgets.Muted : style.Accent;
        saveButton.interactable = count > 0;
        discardButton.interactable = count > 0;
        saveLabel.text = count > 0 ? $"Save Changes ({count})" : "Save Changes";
        saveLabel.color = count > 0 ? style.AccentBright : OptionsWidgets.Muted;
        // While changes wait, Save is the one lit: a gold rim round it.
        Transform saveRim = saveButton.transform.Find("Rim");
        if (saveRim != null)
        {
            saveRim.GetComponent<Image>().color = HearthKit.Faded(style.Accent, count > 0 ? 0.95f : 0.3f);
        }
    }

    private void Save()
    {
        if (!draft.HasChanges)
        {
            return;
        }

        bool display = draft.IsChanged(DisplayModeKey) || draft.IsChanged(ResolutionKey);
        if (display && confirmUntil < 0f)
        {
            keptMode = DisplayOptions.Mode;
            keptResolution = DisplayOptions.Resolution;
        }

        draft.Apply();
        if (display)
        {
            DisplayOptions.Apply();
            // The Editor's Game view doesn't change size, so there is nothing to confirm.
            if (!Application.isEditor)
            {
                confirmUntil = Time.unscaledTime + ConfirmSeconds;
                confirmBar.SetActive(true);
            }
        }

        PlayerPrefs.Save();
        RefreshRows();
        ShowStatus();
    }

    // A new window mode or size asks to be kept first (Keep or Revert, ten seconds): leaving at once would keep a picture the screen
    // can't show, with no way back.
    private void SaveAndLeave()
    {
        bool display = draft.IsChanged(DisplayModeKey) || draft.IsChanged(ResolutionKey);
        Save();
        if (display && confirmUntil >= 0f)
        {
            askBox.SetActive(false);
            return;
        }

        Leave();
    }

    private void Discard()
    {
        draft.Discard();
        RefreshRows();
    }

    private void AskBeforeLeaving()
    {
        List<string> names = draft.Labels();
        const int shown = 6;
        string list = string.Join(", ", names.GetRange(0, Mathf.Min(shown, names.Count)));
        if (names.Count > shown)
        {
            list += $" and {names.Count - shown} more";
        }

        askText.text = $"You changed {names.Count} setting{(names.Count == 1 ? "" : "s")}: <color={UIStyle.Current.AccentHex}>{list}</color>.\n\n" +
                       "Save them before you leave?";
        askBox.SetActive(true);
        askBox.transform.SetAsLastSibling();
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(askSave.gameObject);
        }
    }

    private void KeepEditing()
    {
        askBox.SetActive(false);
        if (EventSystem.current != null && tab >= 0 && tab < tabs.Count)
        {
            EventSystem.current.SetSelectedGameObject(tabs[tab].button.gameObject);
        }
    }

    // ----- The details panel -----

    private void Focus(OptionsFocus focus)
    {
        focused = focus;
        ShowDetails();
    }

    private void ShowDetails()
    {
        if (detailName == null)
        {
            return;
        }

        if (focused == null)
        {
            if (resetRow != null)
            {
                resetRow.gameObject.SetActive(false);
            }

            // The tab's own line where a setting's value goes, the how-to under the rule: the space under the name stood empty
            // (UX pass 2026-11-28).
            detailName.text = TabNames[Mathf.Clamp(tab, 0, TabNames.Length - 1)];
            detailValue.text = $"<color={UIStyle.Current.TextHex}>{TabIntros[Mathf.Clamp(tab, 0, TabIntros.Length - 1)]}</color>";
            detailHelp.text = $"<color={UIStyle.Current.MutedHex}>Point at a setting to read what it does. Changed settings turn gold and wait until you press Save Changes.</color>";
            return;
        }

        detailName.text = focused.Name;
        string now = focused.Value != null ? focused.Value() : "";
        string saved = focused.Saved != null ? focused.Saved() : now;
        var lines = new List<string>();
        if (focused.IsPending)
        {
            lines.Add($"New: <b>{now}</b>  <size=80%>(not saved)</size>");
            lines.Add($"<color={UIStyle.Current.MutedHex}>Saved: {saved}</color>");
        }
        else if (!string.IsNullOrEmpty(now))
        {
            lines.Add($"Now: <b>{now}</b>");
        }

        if (!string.IsNullOrEmpty(focused.Default))
        {
            lines.Add($"<color={UIStyle.Current.MutedHex}>Default: {focused.Default}</color>");
        }

        detailValue.text = string.Join("\n", lines);
        detailHelp.text = focused.Help ?? "";
        if (resetRow != null)
        {
            bool reset = focused.Reset != null && !string.IsNullOrEmpty(focused.Default) && now != focused.Default;
            // Only over a game: from the main menu there is no HUD to show.
            bool preview = focused.Option != null && System.Array.IndexOf(HudOptions, focused.Option) >= 0 && Player.Current != null;
            resetRow.gameObject.SetActive(reset);
            previewRow.gameObject.SetActive(preview);
            // Side by side when both show, otherwise the one fills the row.
            float full = DetailsWidth - 40f;
            float half = (full - 12f) * 0.5f;
            OptionsWidgets.Place((RectTransform)previewRow.transform, new Vector2(0.5f, 0f), new Vector2(reset ? half : full, 46f),
                new Vector2(reset ? -(half + 12f) * 0.5f : 0f, 18f));
            OptionsWidgets.Place((RectTransform)resetRow.transform, new Vector2(0.5f, 0f), new Vector2(preview ? half : full, 46f),
                new Vector2(preview ? (half + 12f) * 0.5f : 0f, 18f));
        }
    }

    private Button resetRow;
    private Button previewRow;

    // ----- Preview: a HUD setting shown on the game behind for a few seconds, unsaved (the systems review, 2026-11-21: the UI scale
    // and HUD density were judged blind, the HUD hidden under the paused menu). Any key, click or the time running out ends it. -----

    private const float PreviewSeconds = 5f;

    /// <summary>The settings that change the HUD, which a preview can show.</summary>
    private static readonly GameOption[] HudOptions =
    {
        GameOptions.UiScale, GameOptions.Hud, GameOptions.SkillBarSize, GameOptions.LargeTimer, GameOptions.AlwaysShowVitals,
        GameOptions.VitalsOverPlayer,
    };

    private float previewUntil = -1f;
    private float previewFrom;
    private CanvasGroup windowGroup;

    /// <summary>True while a HUD setting is being previewed with this screen stepped aside.</summary>
    public static bool IsPreviewing => current != null && current.previewUntil >= 0f;

    private void StartPreview()
    {
        if (previewUntil >= 0f)
        {
            return;
        }

        // Every unsaved HUD change shows together, as it would once saved.
        foreach (GameOption option in HudOptions)
        {
            if (draft.IsChanged(option))
            {
                option.Preview(draft.Get(option));
            }
        }

        // TryGetComponent, not ??: Unity's missing component isn't null to C#.
        if (windowGroup == null && !window.TryGetComponent(out windowGroup))
        {
            windowGroup = window.AddComponent<CanvasGroup>();
        }

        // Still catching the pointer (its dim covers the screen), but pressing nothing: the click or key that ends the preview must not
        // reach the menu under it (the main menu's Quit) nor submit the hidden row.
        windowGroup.alpha = 0f;
        windowGroup.blocksRaycasts = true;
        windowGroup.interactable = false;
        GamePause.HudPreview = true;
        previewFrom = Time.unscaledTime;
        previewUntil = previewFrom + PreviewSeconds;
        UIRoot.ApplyScaleEverywhere();
    }

    private void UpdatePreview()
    {
        if (previewUntil < 0f)
        {
            return;
        }

        // A short grace so the click that started it doesn't end it.
        bool pressed = Time.unscaledTime - previewFrom > 0.35f && UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.Keyboard>() is { } keys
                       && keys.anyKey.wasPressedThisFrame;
        pressed |= Time.unscaledTime - previewFrom > 0.35f && UnityEngine.InputSystem.Mouse.current != null
                   && (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame || UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame);
        pressed |= Time.unscaledTime - previewFrom > 0.35f && UnityEngine.InputSystem.Gamepad.current != null
                   && (UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame || UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame);
        if (pressed || Time.unscaledTime >= previewUntil)
        {
            EndPreview();
        }
    }

    private void EndPreview()
    {
        if (previewUntil < 0f)
        {
            return;
        }

        previewUntil = -1f;
        foreach (GameOption option in HudOptions)
        {
            option.EndPreview();
        }

        GamePause.HudPreview = false;
        if (windowGroup != null)
        {
            windowGroup.alpha = 1f;
            windowGroup.blocksRaycasts = true;
            windowGroup.interactable = true;
        }

        UIRoot.ApplyScaleEverywhere();
    }

    private void ResetFocused()
    {
        if (focused == null || focused.Reset == null)
        {
            return;
        }

        focused.Reset();
        RefreshRows();
        ShowStatus();
        ShowDetails();
    }

    // ----- Tabs -----

    private void ShowTab(int index)
    {
        tab = index;
        UIStyle kit = UIStyle.Current;
        for (int i = 0; i < tabs.Count; i++)
        {
            bool on = i == tab;
            tabs[i].label.color = on ? kit.AccentBright : OptionsWidgets.Body;
            tabs[i].label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            Transform rim = tabs[i].button.transform.Find("Rim");
            if (rim != null)
            {
                rim.GetComponent<Image>().color = HearthKit.Faded(kit.Accent, on ? 0.9f : 0.3f);
            }

            tabs[i].button.image.color = on ? Color.Lerp(Color.white, kit.AccentDeep, 0.35f) * 1.6f : Color.white;
        }

        Clear();
        focused = null;
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

        ShowDetails();
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
            refreshers.Add(OptionsWidgets.Row(content, option, draft, Focus, SetterFor(option)));
        }
    }

    // The quality preset sets its members in the draft; a member changed by hand moves the preset to the one it now matches (Custom),
    // as OptionsSystem does for saved values.
    private Action<float> SetterFor(GameOption option)
    {
        if (option == GameOptions.Preset)
        {
            return value =>
            {
                SetPresetInDraft(Mathf.RoundToInt(value));
                RefreshRows();
            };
        }

        if (IsPresetMember(option))
        {
            return value =>
            {
                draft.Set(option, value);
                draft.Set(GameOptions.Preset, GameOptions.MatchingPreset(draft.Get), first: true);
                RefreshRows();
            };
        }

        return null;
    }

    private void SetPresetInDraft(int preset)
    {
        draft.Set(GameOptions.Preset, preset, first: true);
        if (preset == GameOptions.PresetCustom)
        {
            return;
        }

        for (int i = 0; i < GameOptions.PresetMembers.Count; i++)
        {
            draft.Set(GameOptions.PresetMembers[i], GameOptions.PresetValue(preset, i));
        }
    }

    private static bool IsPresetMember(GameOption option)
    {
        foreach (GameOption member in GameOptions.PresetMembers)
        {
            if (member == option)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshRows()
    {
        foreach (Action refresh in refreshers)
        {
            refresh();
        }

        ShowDetails();
    }

    // A setting kept outside GameOptions (the camera, the window, a volume), edited through the draft by a name.
    private Action Choice(string key, string name, string help, Func<string[]> names, Func<int> saved, Action<int> apply, string defaultName = null)
    {
        var focus = new OptionsFocus
        {
            Name = name,
            Help = help,
            Value = () => NameAt(names(), Mathf.RoundToInt(draft.Get(key, () => saved()))),
            Saved = () => NameAt(names(), saved()),
            Default = defaultName,
            Pending = () => draft.IsChanged(key),
        };
        return OptionsWidgets.ChoiceRow(content, focus, names, () => Mathf.RoundToInt(draft.Get(key, () => saved())),
            index => draft.Set(key, name, () => saved(), value => apply(Mathf.RoundToInt(value)), index), Focus);
    }

    private static string NameAt(string[] names, int index) => names != null && index >= 0 && index < names.Length ? names[index] : "-";

    private void BuildGameplay()
    {
        OptionsWidgets.Section(content, "Camera");
        string[] styles = { CameraStyleRules.Label(CameraStyle.TopDown), CameraStyleRules.Label(CameraStyle.Action) };
        refreshers.Add(Choice("camera.style", "Camera Style", "The style the camera key switches between. Both work everywhere.", () => styles,
            () => CameraPreferences.Chosen == CameraStyle.Action ? 1 : 0, index => CameraPreferences.Chosen = index == 1 ? CameraStyle.Action : CameraStyle.TopDown,
            styles[0]));
        var choices = new string[3];
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i] = CameraStyleRules.Label((CameraStyleOverride)i);
        }

        for (int i = 0; i < CameraStyleRules.ContextCount; i++)
        {
            var context = (CameraContext)i;
            refreshers.Add(Choice("camera.override." + i, context.ToString(), $"Always use one style in this situation: {context}, whatever the camera key says.",
                () => choices, () => (int)CameraPreferences.GetOverride(context), index => CameraPreferences.SetOverride(context, (CameraStyleOverride)index), choices[0]));
        }

        OptionsWidgets.Section(content, "Play");
        Rows(GameOptions.Gameplay);

        // The death rules are the game's, not the machine's: only with a game under way.
        if (Player.Current != null)
        {
            OptionsWidgets.Section(content, "This Game");
            string[] rules = { "Gentle", "Normal", "Harsh" };
            refreshers.Add(Choice("death.rules", "Death Rules",
                "How much a fall costs in this game. Gentle: nothing at home. Normal: your bag in a cairn where you fell, a quarter of a level's " +
                "lessons, meals spent. Harsh: your gear in the cairn too, half a level's lessons, and a raid death loses everything.",
                () => rules, () => (int)GameSession.Progress.DeathRules, index =>
                {
                    var chosen = (DeathRules)Mathf.Clamp(index, 0, rules.Length - 1);
                    if (chosen != GameSession.Progress.DeathRules)
                    {
                        GameSession.Progress.DeathRules = chosen;
                        PlaytestLogger.Note(PlaytestLog.DeathRules, chosen.ToString());
                    }
                }));
        }
    }

    private void BuildGraphics()
    {
        OptionsWidgets.Section(content, "Display");
        refreshers.Add(Choice(DisplayModeKey, "Display Mode", "How the game fills the screen. A new mode asks to be kept after you save it.",
            () => DisplayOptions.ModeNames, () => DisplayOptions.Mode, index => DisplayOptions.Mode = index));
        List<Vector2Int> sizes = DisplayOptions.Resolutions();
        var names = new string[sizes.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = $"{sizes[i].x} x {sizes[i].y}";
        }

        refreshers.Add(Choice(ResolutionKey, "Resolution", "The size of the picture on screen. A new size asks to be kept after you save it.", () => names,
            () => sizes.IndexOf(DisplayOptions.Resolution), index =>
            {
                if (index >= 0 && index < sizes.Count)
                {
                    DisplayOptions.Resolution = sizes[index];
                }
            }));
        Rows(new[] { GameOptions.VSync, GameOptions.FrameLimit });

        OptionsWidgets.Section(content, "Quality");
        Rows(GameOptions.Graphics);
        OptionsWidgets.Section(content, "Look");
        Rows(GameOptions.Look);
    }

    private void BuildAudio()
    {
        OptionsWidgets.Section(content, "Volume");
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        Volume("audio.master", "Master Volume", "Everything together.", () => AudioManager.MasterVolume, value => AudioManager.MasterVolume = value,
            sounds != null ? sounds.MasterVolume : -1f);
        Channel(AudioChannel.Music, "Music", "The score.");
        Channel(AudioChannel.Ambience, "Ambience", "Wind, rain, water and the forest.");
        Channel(AudioChannel.Effects, "Sound Effects", "Fights, footsteps, work and the interface.");
        Channel(AudioChannel.Voice, "Voice", "The storyteller and the people of the Haven.");
        OptionsWidgets.Section(content, "Other");
        Rows(new[] { GameOptions.MuteUnfocused });
    }

    private void Channel(AudioChannel channel, string name, string help)
    {
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        Volume("audio." + channel, name, help, () => AudioManager.GetVolume(channel), value => AudioManager.SetVolume(channel, value),
            sounds != null ? sounds.DefaultVolume(channel) : -1f);
    }

    private void Volume(string key, string name, string help, Func<float> saved, Action<float> apply, float byDefault)
    {
        string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";
        var focus = new OptionsFocus
        {
            Name = name,
            Help = help,
            Value = () => Percent(draft.Get(key, saved)),
            Saved = () => Percent(saved()),
            Default = byDefault >= 0f ? Percent(byDefault) : null,
            Pending = () => draft.IsChanged(key),
        };
        refreshers.Add(OptionsWidgets.SliderRow(content, focus, 0f, 1f, 0.01f, () => draft.Get(key, saved),
            value => draft.Set(key, name, saved, apply, value), Percent, Focus));
    }

    private void BuildControls()
    {
        OptionsWidgets.Section(content, "Camera");
        Rows(GameOptions.Controls);
        OptionsWidgets.Note(content, "Keys: keyboard and mouse on the left, gamepad on the right. Pick one, then press the new key: it is kept at once.");
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
        var focus = new OptionsFocus
        {
            Name = row.Label,
            Help = "Pick a key, then press the new one. If it's in use, the two swap. A new key is kept at once.",
            Value = () => $"{InputRebinds.Describe(row, GameInput.KeyboardMouse)}  /  {InputRebinds.Describe(row, GameInput.Gamepad)}",
        };
        RectTransform control = OptionsWidgets.LabelledRow(content, focus, Focus, out _);
        Action keyboard = KeyButton(control, row, GameInput.KeyboardMouse, new Vector2(0f, 0.5f), new Vector2(0f, 0f), focus);
        Action gamepad = KeyButton(control, row, GameInput.Gamepad, new Vector2(0f, 0.5f), new Vector2(KeyColumn + 12f, 0f), focus);
        refreshers.Add(keyboard);
        refreshers.Add(gamepad);
    }

    private Action KeyButton(RectTransform control, RebindRow row, string scheme, Vector2 anchor, Vector2 position, OptionsFocus focus)
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
                focused = new OptionsFocus { Name = focus.Name, Help = note ?? focus.Help, Value = focus.Value };
                RefreshRows();
            });
        }, out label);
        OptionsWidgets.Place((RectTransform)button.transform, anchor, new Vector2(KeyColumn, 42f), position);
        button.interactable = InputRebinds.HasBinding(row, scheme);
        button.gameObject.AddComponent<OptionsHover>().Entered = () => Focus(focus);
        void Refresh() => label.text = InputRebinds.Describe(row, scheme);
        Refresh();
        return Refresh;
    }

    // Reset puts the tab's defaults in the draft, to be saved like any change. The keys are the exception: they go back at once.
    private void ResetTab()
    {
        switch (tab)
        {
            case 0:
                DraftDefaults(GameOptions.Gameplay);
                draft.Set("camera.style", "Camera Style", () => CameraPreferences.Chosen == CameraStyle.Action ? 1 : 0,
                    value => CameraPreferences.Chosen = value > 0.5f ? CameraStyle.Action : CameraStyle.TopDown, 0f);
                for (int i = 0; i < CameraStyleRules.ContextCount; i++)
                {
                    var context = (CameraContext)i;
                    draft.Set("camera.override." + i, context.ToString(), () => (int)CameraPreferences.GetOverride(context),
                        value => CameraPreferences.SetOverride(context, (CameraStyleOverride)Mathf.RoundToInt(value)), 0f);
                }

                break;
            case 1:
                // Everything the tab shows (the quality settings, the look, the frame rate), then the preset over its members.
                DraftDefaults(GameOptions.Graphics);
                DraftDefaults(GameOptions.Look);
                SetPresetInDraft(GameOptions.PresetUltra);
                DraftDefaults(new[] { GameOptions.VSync, GameOptions.FrameLimit });
                break;
            case 2:
                DraftDefaults(new[] { GameOptions.MuteUnfocused });
                AudioSettingsDefinition sounds = AudioManager.Sounds;
                if (sounds != null)
                {
                    draft.Set("audio.master", "Master Volume", () => AudioManager.MasterVolume, value => AudioManager.MasterVolume = value, sounds.MasterVolume);
                    foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
                    {
                        AudioChannel each = channel;
                        draft.Set("audio." + each, each.ToString(), () => AudioManager.GetVolume(each), value => AudioManager.SetVolume(each, value),
                            sounds.DefaultVolume(each));
                    }
                }

                break;
            case 3:
                DraftDefaults(GameOptions.Controls);
                InputRebinds.ResetAll();
                break;
            default:
                DraftDefaults(GameOptions.Accessibility);
                break;
        }

        RefreshRows();
    }

    private void DraftDefaults(IEnumerable<GameOption> options)
    {
        foreach (GameOption option in options)
        {
            draft.Set(option, option.Default, option == GameOptions.Preset);
        }
    }

    // ----- The window: a saved change has to be kept, or it goes back -----

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

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        closedFrame = -1;
        current = null;
    }
}
