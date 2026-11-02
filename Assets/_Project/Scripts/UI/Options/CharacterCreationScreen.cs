using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Making the character a new game starts with: man or woman, a name (or one from the dice), and their skin, face, hair,
/// brows, beard, eyes and clothes, on a live preview that can be turned by dragging. It only edits a CharacterAppearance and
/// hands it back; starting the game is the caller's (the main menu). Built in code when first opened, from the same parts
/// as the Options and Saves screens.
/// </summary>
public sealed class CharacterCreationScreen : MonoBehaviour
{
    private const int PreviewWidth = 560;
    private const int PreviewHeight = 780;

    private static CharacterCreationScreen current;
    private static int closedFrame = -1;

    private readonly List<Action> refreshers = new List<Action>();
    private GameObject window;
    private RawImage preview;
    private TMP_InputField nameField;
    private TMP_Text helpLine;
    private CharacterPreviewStage stage;
    private CharacterAppearance look;
    private Action<CharacterAppearance> confirmed;
    private Action cancelled;

    /// <summary>True while the screen is up, or was until this frame: the same Escape press must not reach the menu under it.</summary>
    public static bool HasEscape => (current != null && current.window.activeSelf) || closedFrame == Time.frameCount;

    private static CharacterCatalog Catalog => CharacterCatalog.Current;
    private CharacterBodyDefinition Body => Catalog.Body(look.gender);

    /// <summary>Shows the screen with the catalog's starting look.</summary>
    /// <param name="onConfirmed">Gets the finished look (with its name) when the player begins.</param>
    /// <param name="onCancelled">Runs when the player backs out.</param>
    public static void Open(Action<CharacterAppearance> onConfirmed, Action onCancelled = null)
    {
        if (current == null)
        {
            Create();
        }

        current.Begin(onConfirmed, onCancelled);
    }

    private static void Create()
    {
        var root = new GameObject("Character Creation Screen");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var screen = root.AddComponent<CharacterCreationScreen>();
        screen.Build(root.transform);
        root.SetActive(true);
        current = screen;
    }

    private void Begin(Action<CharacterAppearance> onConfirmed, Action onCancelled)
    {
        confirmed = onConfirmed;
        cancelled = onCancelled;
        look = Catalog.StartingLook;
        nameField.SetTextWithoutNotify(look.name);
        stage = CharacterPreviewStage.Create(PreviewWidth, PreviewHeight);
        preview.texture = stage.Texture;
        window.SetActive(true);
        ShowHelp("Drag the picture to turn them round.");
        Changed();
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
        EndStage();
        if (current == this)
        {
            current = null;
        }
    }

    private void OnBackPressed(InputAction.CallbackContext context)
    {
        // Escape while typing only leaves the name field.
        if (window.activeSelf && !nameField.isFocused)
        {
            Close(cancelled);
        }
    }

    private void Close(Action then)
    {
        window.SetActive(false);
        closedFrame = Time.frameCount;
        EndStage();
        confirmed = null;
        cancelled = null;
        then?.Invoke();
    }

    private void EndStage()
    {
        if (stage != null)
        {
            Destroy(stage.gameObject);
            stage = null;
        }

        if (preview != null)
        {
            preview.texture = null;
        }
    }

    private void Confirm()
    {
        look.name = CharacterAppearance.CleanName(nameField.text);
        CharacterAppearance chosen = look.Clone();
        Action<CharacterAppearance> then = confirmed;
        Close(null);
        then?.Invoke(chosen);
    }

    // ----- Building -----

    private void Build(Transform root)
    {
        // The rows show the look as they are made.
        look = Catalog.StartingLook;
        RectTransform dim = OptionsWidgets.Rect("Dim", root);
        OptionsWidgets.Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        window = dim.gameObject;

        RectTransform panel = OptionsWidgets.Place(OptionsWidgets.Rect("Panel", dim), new Vector2(0.5f, 0.5f), new Vector2(1400f, 940f), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = OptionsWidgets.Panel;
        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = OptionsWidgets.Rim;
        rim.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = OptionsWidgets.Text("Title", panel, "Who Are You?", 38f, OptionsWidgets.Title);
        OptionsWidgets.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(700f, 56f), new Vector2(36f, -22f));
        title.fontStyle = FontStyles.Bold;

        RectTransform frame = OptionsWidgets.Place(OptionsWidgets.Rect("Preview", panel), new Vector2(1f, 1f), new Vector2(PreviewWidth, PreviewHeight), new Vector2(-36f, -36f));
        preview = frame.gameObject.AddComponent<RawImage>();
        preview.color = Color.white;
        frame.gameObject.AddComponent<PreviewDrag>().Dragged = degrees =>
        {
            if (stage != null)
            {
                stage.Turn(degrees);
            }
        };

        RectTransform list = OptionsWidgets.Rect("Choices", panel);
        OptionsWidgets.Stretch(list, new Vector2(36f, 150f), new Vector2(-(PreviewWidth + 72f), -92f));
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        BuildNameRow(list);
        Choice(list, "Body", "A man or a woman.", () => new[] { "Man", "Woman" }, () => (int)look.gender, i => look.gender = (CharacterGender)i);
        Choice(list, "Skin", "Skin tone, of the face and body alike.", () => Labels(Body.Skins), () => look.skin, i => look.skin = i);
        Choice(list, "Hair", "Hair style.", () => Labels(Body.HairStyles), () => look.hair, i => look.hair = i);
        Choice(list, "Hair Colour", "Colours the hair, brows and beard alike.", () => Body.HairColours.ToArray(), () => look.hairColour, i => look.hairColour = i);
        Choice(list, "Brows", "Brow shape.", () => Labels(Body.Brows), () => look.brows, i => look.brows = i);
        Choice(list, "Beard", "Only men grow one.", () => Body.Beards.Count > 0 ? Labels(Body.Beards) : new[] { "None" }, () => look.beard, i => look.beard = i);
        Choice(list, "Eyes", "Eye colour.", () => Labels(Body.Eyes), () => look.eyes, i => look.eyes = i);
        Choice(list, "Clothes", "What is worn until armour is found.", () => PlainClothesLabels, () => look.clothes, i => look.clothes = i);

        helpLine = OptionsWidgets.Text("Help", panel, "", 21f, OptionsWidgets.Muted);
        OptionsWidgets.Place((RectTransform)helpLine.transform, new Vector2(0f, 0f), new Vector2(740f, 48f), new Vector2(36f, 84f));

        RectTransform bottom = OptionsWidgets.Place(OptionsWidgets.Rect("Footer", panel), new Vector2(0f, 0f), new Vector2(1328f, 50f), new Vector2(36f, 20f));
        Button back = OptionsWidgets.Button("Back", bottom, "Back", 22f, () => Close(cancelled), out _);
        OptionsWidgets.Place((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(220f, 50f), Vector2.zero);
        Button random = OptionsWidgets.Button("Randomise", bottom, "Randomise", 22f, Randomise, out _);
        OptionsWidgets.Place((RectTransform)random.transform, new Vector2(0f, 0.5f), new Vector2(220f, 50f), new Vector2(240f, 0f));
        Button begin = OptionsWidgets.Button("Begin", bottom, "Begin the Journey", 22f, Confirm, out _);
        OptionsWidgets.Place((RectTransform)begin.transform, new Vector2(1f, 0.5f), new Vector2(300f, 50f), Vector2.zero);

        window.SetActive(false);
    }

    private void BuildNameRow(Transform list)
    {
        RectTransform control = OptionsWidgets.LabelledRow(list, "Name", "What people will call you. Leave it empty to be the Wanderer.", ShowHelp);
        RectTransform field = OptionsWidgets.Rect("Name", control);
        OptionsWidgets.Stretch(field, Vector2.zero, new Vector2(-64f, 0f));
        var background = field.gameObject.AddComponent<Image>();
        background.color = OptionsWidgets.Track;
        RectTransform area = OptionsWidgets.Rect("Text Area", field);
        OptionsWidgets.Stretch(area, new Vector2(14f, 4f), new Vector2(-14f, -4f));
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = OptionsWidgets.Text("Text", area, "", 24f, OptionsWidgets.Body);
        OptionsWidgets.Stretch((RectTransform)text.transform);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_Text placeholder = OptionsWidgets.Text("Placeholder", area, CharacterAppearance.DefaultName, 24f, OptionsWidgets.Muted);
        OptionsWidgets.Stretch((RectTransform)placeholder.transform);
        placeholder.fontStyle = FontStyles.Italic;
        nameField = field.gameObject.AddComponent<TMP_InputField>();
        nameField.targetGraphic = background;
        nameField.textViewport = area;
        nameField.textComponent = text;
        nameField.placeholder = placeholder;
        nameField.characterLimit = CharacterAppearance.MaximumNameLength;
        nameField.lineType = TMP_InputField.LineType.SingleLine;
        Button dice = OptionsWidgets.Button("Random Name", control, "?", 26f, RandomName, out _);
        OptionsWidgets.Place((RectTransform)dice.transform, new Vector2(1f, 0.5f), new Vector2(52f, 42f), Vector2.zero);
    }

    private void Choice(Transform list, string label, string help, Func<string[]> names, Func<int> get, Action<int> set)
    {
        refreshers.Add(OptionsWidgets.ChoiceRow(list, label, help, names, get, i =>
        {
            set(i);
            Changed();
        }, ShowHelp));
    }

    private static string[] Labels(IReadOnlyList<NamedMaterial> options) => options.Select(o => o.label).ToArray();

    private string[] PlainClothesLabels => Catalog.PlainClothes.Count > 0 ? Catalog.PlainClothes.Select(c => c.label).ToArray() : new[] { "None" };

    private static string[] Labels(IReadOnlyList<ColouredPart> options) => options.Select(o => o.label).ToArray();

    private void ShowHelp(string line) => helpLine.text = line;

    // ----- Changing the look -----

    // Keeps every choice within the current body's options (a woman has other hair styles and no beard), then redraws.
    private void Changed()
    {
        CharacterBodyDefinition body = Body;
        look.skin = Fit(look.skin, body.Skins.Count);
        look.hair = Fit(look.hair, body.HairStyles.Count);
        look.hairColour = Fit(look.hairColour, body.HairColours.Count);
        look.brows = Fit(look.brows, body.Brows.Count);
        look.beard = Fit(look.beard, body.Beards.Count);
        look.eyes = Fit(look.eyes, body.Eyes.Count);
        look.underclothes = Fit(look.underclothes, body.Underclothes.Count);
        look.clothes = Fit(look.clothes, Catalog.PlainClothes.Count);
        if (stage != null)
        {
            stage.Show(look, Catalog.PlainClothing(look.clothes), Catalog.PlainShoes);
        }

        foreach (Action refresh in refreshers)
        {
            refresh();
        }
    }

    private static int Fit(int index, int count) => count <= 0 ? 0 : Mathf.Clamp(index, 0, count - 1);

    private void Randomise()
    {
        look.gender = UnityEngine.Random.value < 0.5f ? CharacterGender.Male : CharacterGender.Female;
        CharacterBodyDefinition body = Body;
        look.skin = UnityEngine.Random.Range(0, Mathf.Max(1, body.Skins.Count));
        look.hair = UnityEngine.Random.Range(0, Mathf.Max(1, body.HairStyles.Count));
        look.hairColour = UnityEngine.Random.Range(0, Mathf.Max(1, body.HairColours.Count));
        look.brows = UnityEngine.Random.Range(0, Mathf.Max(1, body.Brows.Count));
        look.beard = UnityEngine.Random.Range(0, Mathf.Max(1, body.Beards.Count));
        look.eyes = UnityEngine.Random.Range(0, Mathf.Max(1, body.Eyes.Count));
        look.underclothes = UnityEngine.Random.Range(0, Mathf.Max(1, body.Underclothes.Count));
        look.clothes = UnityEngine.Random.Range(0, Mathf.Max(1, Catalog.PlainClothes.Count));
        RandomName();
        Changed();
    }

    private void RandomName()
    {
        IReadOnlyList<string> names = Catalog.Names(look.gender);
        if (names.Count > 0)
        {
            nameField.text = names[UnityEngine.Random.Range(0, names.Count)];
        }
    }

    /// <summary>Turns the preview as the pointer drags across it.</summary>
    private sealed class PreviewDrag : MonoBehaviour, IDragHandler
    {
        public Action<float> Dragged;

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(-eventData.delta.x * 0.4f);
    }
}
