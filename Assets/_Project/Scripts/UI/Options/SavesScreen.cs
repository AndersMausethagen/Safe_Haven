using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The saved games, opened from the main menu and the Escape menu: every save with its place, level, time played and date,
/// to load, rename or delete, and while playing to overwrite with the game in hand or to save as a new one. Anything that
/// loses progress asks first. It holds no save rules: everything goes through SaveGameService. Built in code when first
/// opened in a scene, from the same parts as the Options screen.
/// </summary>
public sealed class SavesScreen : MonoBehaviour
{
    private const float RowHeight = 96f;
    private const float ActionWidth = 124f;

    private static SavesScreen current;
    private static int closedFrame = -1;

    private GameObject window;
    private RectTransform content;
    private TMP_Text statusLine;
    private TMP_Text emptyLine;
    private GameObject footer;
    private Button saveAsNew;
    private GameObject askBar;
    private TMP_Text askText;
    private TMP_Text askYesLabel;
    private GameObject nameBar;
    private TMP_InputField nameField;
    private Action askYes;
    private string renaming;
    private IContentLookup lookup;
    private string fallbackScene;
    private Action closed;
    private Action loadingStarted;

    /// <summary>True while the screen is up, or was until this frame: the same Escape press must not close the menu under it.</summary>
    public static bool HasEscape => (current != null && current.window.activeSelf) || closedFrame == Time.frameCount;

    /// <summary>Shows the saves.</summary>
    /// <param name="onClosed">Runs when the screen goes away with nothing loaded.</param>
    /// <param name="onLoading">Runs when a save starts loading.</param>
    public static void Open(IContentLookup content, string fallbackScene, Action onClosed = null, Action onLoading = null)
    {
        if (current == null)
        {
            Create();
        }

        current.lookup = content;
        current.fallbackScene = fallbackScene;
        current.closed = onClosed;
        current.loadingStarted = onLoading;
        current.window.SetActive(true);
        current.ShowStatus("");
        current.ShowFooter();
        current.Refresh();
    }

    private static void Create()
    {
        var root = new GameObject("Saves Screen");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var screen = root.AddComponent<SavesScreen>();
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
        EndTyping();
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private int lastBackFrame = -1;

    // Back steps out of a question or a rename first, and only then out of the screen.
    private void OnBackPressed(InputAction.CallbackContext context)
    {
        // Escape is both Pause and Cancel: one press came twice, stepping out of the question and then out of the screen.
        if (!window.activeSelf || Time.frameCount == lastBackFrame)
        {
            return;
        }

        lastBackFrame = Time.frameCount;

        if (footer.activeSelf)
        {
            Close();
        }
        else
        {
            ShowFooter();
        }
    }

    private void Close()
    {
        window.SetActive(false);
        closedFrame = Time.frameCount;
        Action then = closed;
        closed = null;
        then?.Invoke();
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

        TMP_Text title = OptionsWidgets.Text("Title", panel, "Saved Games", 38f, OptionsWidgets.Title);
        OptionsWidgets.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(600f, 56f), new Vector2(36f, -22f));
        title.fontStyle = FontStyles.Bold;

        RectTransform view = OptionsWidgets.Rect("View", panel);
        OptionsWidgets.Stretch(view, new Vector2(36f, 138f), new Vector2(-36f, -92f));
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        view.gameObject.AddComponent<RectMask2D>();
        content = OptionsWidgets.Rect("Content", view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = new Vector2(-18f, 0f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        emptyLine = OptionsWidgets.Text("Empty", view, "No saved games yet.", 24f, OptionsWidgets.Muted, TextAlignmentOptions.Center);
        OptionsWidgets.Stretch((RectTransform)emptyLine.transform);

        statusLine = OptionsWidgets.Text("Status", panel, "", 21f, OptionsWidgets.Muted);
        OptionsWidgets.Place((RectTransform)statusLine.transform, new Vector2(0f, 0f), new Vector2(1208f, 48f), new Vector2(36f, 84f));

        // Three ways to fill the bottom edge: the usual buttons, a question, or a name being typed.
        RectTransform bottom = OptionsWidgets.Place(OptionsWidgets.Rect("Footer", panel), new Vector2(0f, 0f), new Vector2(1208f, 50f), new Vector2(36f, 20f));
        footer = bottom.gameObject;
        saveAsNew = OptionsWidgets.Button("Save as New", bottom, "Save as New", 22f, SaveAsNew, out _);
        OptionsWidgets.Place((RectTransform)saveAsNew.transform, new Vector2(0f, 0.5f), new Vector2(240f, 50f), Vector2.zero);
        Button back = OptionsWidgets.Button("Back", bottom, "Back", 22f, Close, out _);
        OptionsWidgets.Place((RectTransform)back.transform, new Vector2(1f, 0.5f), new Vector2(240f, 50f), Vector2.zero);

        RectTransform ask = OptionsWidgets.Place(OptionsWidgets.Rect("Ask", panel), new Vector2(0f, 0f), new Vector2(1208f, 50f), new Vector2(36f, 20f));
        askBar = ask.gameObject;
        askText = OptionsWidgets.Text("Question", ask, "", 22f, OptionsWidgets.Title);
        OptionsWidgets.Stretch((RectTransform)askText.transform, Vector2.zero, new Vector2(-420f, 0f));
        Button yes = OptionsWidgets.Button("Yes", ask, "", 22f, Answer, out askYesLabel);
        OptionsWidgets.Place((RectTransform)yes.transform, new Vector2(1f, 0.5f), new Vector2(190f, 50f), new Vector2(-210f, 0f));
        Button no = OptionsWidgets.Button("No", ask, "Cancel", 22f, ShowFooter, out _);
        OptionsWidgets.Place((RectTransform)no.transform, new Vector2(1f, 0.5f), new Vector2(190f, 50f), Vector2.zero);

        BuildNameBar(panel);
        ShowFooter();
        window.SetActive(false);
    }

    private void BuildNameBar(RectTransform panel)
    {
        RectTransform bar = OptionsWidgets.Place(OptionsWidgets.Rect("Rename", panel), new Vector2(0f, 0f), new Vector2(1208f, 50f), new Vector2(36f, 20f));
        nameBar = bar.gameObject;
        RectTransform field = OptionsWidgets.Rect("Name", bar);
        OptionsWidgets.Stretch(field, Vector2.zero, new Vector2(-420f, 0f));
        var background = field.gameObject.AddComponent<Image>();
        background.color = OptionsWidgets.Track;
        RectTransform area = OptionsWidgets.Rect("Text Area", field);
        OptionsWidgets.Stretch(area, new Vector2(14f, 4f), new Vector2(-14f, -4f));
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = OptionsWidgets.Text("Text", area, "", 24f, OptionsWidgets.Body);
        OptionsWidgets.Stretch((RectTransform)text.transform);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        nameField = field.gameObject.AddComponent<TMP_InputField>();
        nameField.targetGraphic = background;
        nameField.textViewport = area;
        nameField.textComponent = text;
        nameField.characterLimit = 32;
        nameField.lineType = TMP_InputField.LineType.SingleLine;
        nameField.onSubmit.AddListener(_ => FinishRename());
        Button ok = OptionsWidgets.Button("OK", bar, "Rename", 22f, FinishRename, out _);
        OptionsWidgets.Place((RectTransform)ok.transform, new Vector2(1f, 0.5f), new Vector2(190f, 50f), new Vector2(-210f, 0f));
        Button no = OptionsWidgets.Button("Cancel", bar, "Cancel", 22f, ShowFooter, out _);
        OptionsWidgets.Place((RectTransform)no.transform, new Vector2(1f, 0.5f), new Vector2(190f, 50f), Vector2.zero);
    }

    // ----- The list -----

    private static bool InGame => Player.Current != null;

    private void Refresh()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        List<SaveSlotInfo> saves = SaveGameService.ListSaves();
        emptyLine.gameObject.SetActive(saves.Count == 0);
        foreach (SaveSlotInfo save in saves)
        {
            AddRow(save);
        }

        string problem = InGame ? SaveGameService.CheckPlayerSave() : null;
        saveAsNew.gameObject.SetActive(InGame);
        saveAsNew.interactable = problem == null;
        if (problem != null)
        {
            ShowStatus(problem);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void AddRow(SaveSlotInfo save)
    {
        RectTransform row = OptionsWidgets.NewRow(content, save.Slot, RowHeight);
        row.gameObject.AddComponent<Image>().color = OptionsWidgets.Track;
        bool playing = InGame && save.Slot == SaveGameService.CurrentSlot;
        int actions = InGame ? 4 : 3;
        float textRight = 16f + actions * (ActionWidth + 8f);
        TMP_Text name = OptionsWidgets.Text("Name", row, playing ? $"{save.Name}  <size=70%><color={UIStyle.Current.AccentHex}>playing now</color></size>" : save.Name, 26f, OptionsWidgets.Body);
        OptionsWidgets.Stretch((RectTransform)name.transform, new Vector2(18f, 44f), new Vector2(-textRight, -8f));
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text details = OptionsWidgets.Text("Details", row, save.Details, 19f, save.IsReadable ? OptionsWidgets.Muted : new Color(1f, 0.48f, 0.42f), TextAlignmentOptions.TopLeft);
        OptionsWidgets.Stretch((RectTransform)details.transform, new Vector2(18f, 10f), new Vector2(-textRight, -54f));
        details.textWrappingMode = TextWrappingModes.NoWrap;
        details.overflowMode = TextOverflowModes.Ellipsis;

        float x = -14f;
        Action<string, Action, bool> add = (label, clicked, enabled) =>
        {
            Button button = OptionsWidgets.Button(label, row, label, 20f, clicked, out _);
            OptionsWidgets.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(ActionWidth, 56f), new Vector2(x, 0f));
            button.interactable = enabled;
            x -= ActionWidth + 8f;
        };

        add("Delete", () => AskDelete(save), true);
        add("Rename", () => StartRename(save), save.IsReadable);
        if (InGame)
        {
            add("Overwrite", () => AskOverwrite(save), !playing && SaveGameService.CheckPlayerSave() == null);
        }

        add("Load", () => AskLoad(save), save.IsReadable && SaveGameService.CheckLoad() == null);
    }

    // ----- Actions -----

    private void SaveAsNew()
    {
        if (SaveGameService.TrySaveAsNew(out string problem))
        {
            ShowStatus($"<color={UIStyle.Current.GoodHex}>Saved as {SaveGameService.CurrentName}.</color>");
            Refresh();
        }
        else
        {
            ShowStatus($"<color={UIStyle.Current.DangerHex}>{problem}</color>");
        }
    }

    private void AskLoad(SaveSlotInfo save)
    {
        if (!InGame)
        {
            Load(save);
            return;
        }

        Ask($"Load {save.Name}? Unsaved progress is lost.", "Load", () => Load(save));
    }

    private void Load(SaveSlotInfo save)
    {
        if (!SaveGameService.TryLoad(save.Slot, lookup, fallbackScene, out string problem))
        {
            ShowFooter();
            ShowStatus($"<color={UIStyle.Current.DangerHex}>{problem}</color>");
            return;
        }

        // The menu under this keeps waiting for the scene to change.
        window.SetActive(false);
        closedFrame = Time.frameCount;
        closed = null;
        Action then = loadingStarted;
        loadingStarted = null;
        then?.Invoke();
    }

    private void AskOverwrite(SaveSlotInfo save)
    {
        Ask($"Replace {save.Name} with your game? It can't be undone.", "Overwrite", () =>
        {
            bool saved = SaveGameService.TryOverwrite(save.Slot, out string problem);
            ShowFooter();
            ShowStatus(saved ? $"<color={UIStyle.Current.GoodHex}>Saved over {save.Name}.</color>" : $"<color={UIStyle.Current.DangerHex}>{problem}</color>");
            Refresh();
        });
    }

    private void AskDelete(SaveSlotInfo save)
    {
        Ask($"Delete {save.Name}? It can't be undone.", "Delete", () =>
        {
            bool deleted = SaveGameService.TryDelete(save.Slot, out string problem);
            ShowFooter();
            ShowStatus(deleted ? $"Deleted {save.Name}." : $"<color={UIStyle.Current.DangerHex}>{problem}</color>");
            Refresh();
        });
    }

    private void StartRename(SaveSlotInfo save)
    {
        renaming = save.Slot;
        ShowOnly(nameBar);
        ShowStatus("Type a new name.");
        GameInput.SuspendGameplay(this);
        nameField.text = save.Name;
        nameField.ActivateInputField();
        nameField.Select();
    }

    private void FinishRename()
    {
        if (renaming == null)
        {
            return;
        }

        if (SaveGameService.TryRename(renaming, nameField.text, out string problem))
        {
            ShowFooter();
            ShowStatus("");
            Refresh();
        }
        else
        {
            ShowStatus($"<color={UIStyle.Current.DangerHex}>{problem}</color>");
        }
    }

    private void Ask(string question, string yesLabel, Action yes)
    {
        askText.text = question;
        askYesLabel.text = yesLabel;
        askYes = yes;
        ShowOnly(askBar);
    }

    private void Answer()
    {
        Action yes = askYes;
        askYes = null;
        yes?.Invoke();
    }

    private void ShowFooter()
    {
        EndTyping();
        ShowOnly(footer);
    }

    private void ShowOnly(GameObject bar)
    {
        footer.SetActive(bar == footer);
        askBar.SetActive(bar == askBar);
        nameBar.SetActive(bar == nameBar);
    }

    private void EndTyping()
    {
        if (renaming != null)
        {
            renaming = null;
            GameInput.ResumeGameplay(this);
        }
    }

    private void ShowStatus(string line)
    {
        statusLine.text = line;
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        closedFrame = -1;
        current = null;
    }
}
