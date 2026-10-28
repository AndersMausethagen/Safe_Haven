using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Who is speaking in the dialogue box: a name, and their model for the live portrait (or a picture).</summary>
public readonly struct DialogueSpeaker
{
    public readonly string Name;
    /// <summary>The speaker in the world: the portrait looks at their face and the camera frames both.</summary>
    public readonly Transform Model;
    /// <summary>A picture to show instead of the live portrait (a voice in the head, a notice board).</summary>
    public readonly Sprite Portrait;
    /// <summary>How close the camera comes while they talk (PlayerCameraController.BeginDialogue); 0 for the usual.</summary>
    public readonly float CameraDistance;

    public DialogueSpeaker(string name, Transform model = null, Sprite portrait = null, float cameraDistance = 0f)
    {
        Name = name;
        Model = model;
        Portrait = portrait;
        CameraDistance = cameraDistance;
    }

    /// <summary>The player, by the name given (or "You").</summary>
    public static DialogueSpeaker Player(string name = null) =>
        new DialogueSpeaker(string.IsNullOrWhiteSpace(name) ? "You" : name, global::Player.Current != null ? global::Player.Current.transform : null);
}

/// <summary>One answer the player can give in the dialogue box.</summary>
public sealed class DialogueChoice
{
    public readonly string Label;
    /// <summary>Shown smaller after the label, e.g. the quest it takes.</summary>
    public readonly string Detail;
    public readonly bool Enabled;

    public DialogueChoice(string label, string detail = null, bool enabled = true)
    {
        Label = label;
        Detail = detail;
        Enabled = enabled;
    }
}

/// <summary>
/// The conversation box at the bottom of the screen: the speaker's live portrait (DialoguePortrait) and name, their line
/// written out letter by letter, and either "continue" or the player's answers. E, Space, Enter or a click finishes the
/// line, then continues; 1-4 (or a click) picks an answer. While a speaker in the world talks, the camera frames them and
/// the player (PlayerCameraController.BeginDialogue). It is a UI window (UIWindowId.Dialogue), so the player doesn't
/// attack or use things while talking.
/// <para>
/// Callers chain lines: <see cref="Say"/> and <see cref="Ask"/> call back when the player moves on, and the box closes
/// unless the callback says or asks something else. Quest givers, companions and story graphs (StoryScript) all talk
/// through it. Made on first use in each scene; nothing to set up.
/// </para>
/// </summary>
public sealed class DialogueBox : MonoBehaviour
{
    private const float LettersPerSecond = 55f;
    // Ignores the key press that opened the conversation (E is also "use").
    private const float InputDelay = 0.2f;
    private const float WalkAwayDistance = 6f;

    private static readonly Color Panel = new Color(0.075f, 0.062f, 0.05f, 0.95f);
    private static readonly Color Rim = new Color(0.64f, 0.49f, 0.29f, 0.9f);
    private static readonly Color Title = new Color(0.91f, 0.77f, 0.42f, 1f);
    private static readonly Color Body = new Color(0.93f, 0.89f, 0.8f, 1f);
    private static readonly Color ButtonColour = new Color(0.3f, 0.235f, 0.17f, 1f);
    private static readonly Color ButtonOff = new Color(0.18f, 0.15f, 0.12f, 1f);

    private static DialogueBox current;

    private readonly List<Button> buttons = new List<Button>();
    private readonly List<TMP_Text> buttonLabels = new List<TMP_Text>();
    private UIWindow window;
    private GameObject content;
    private RawImage portrait;
    private Image picture;
    private TMP_Text nameLabel;
    private TMP_Text lineLabel;
    private TMP_Text hint;
    private DialoguePortrait portraitCamera;
    private PlayerCameraController framing;
    private Transform framed;

    private Action onContinue;
    private Action<int> onChosen;
    private IReadOnlyList<DialogueChoice> choices;
    private float shownAt;
    private float revealStarted;
    private int letters;
    private bool answered;
    private Transform anchor;

    /// <summary>The dialogue box of the loaded scene, made on first use.</summary>
    public static DialogueBox Current
    {
        get
        {
            if (current == null)
            {
                current = Create();
            }

            return current;
        }
    }

    /// <summary>True while a line is shown.</summary>
    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Raised when the box closes (moved on past the last line, walked away or closed).</summary>
    public event Action Closed;

    /// <summary>Shows a line; <paramref name="done"/> runs when the player moves on. Without a follow-up the box closes.</summary>
    public void Say(DialogueSpeaker speaker, string line, Action done = null)
    {
        Show(speaker, line, null);
        onContinue = done;
    }

    /// <summary>Shows a line with answers; <paramref name="chosen"/> gets the answer's index, or -1 when the player walks
    /// away or the box is closed.</summary>
    public void Ask(DialogueSpeaker speaker, string line, IReadOnlyList<DialogueChoice> answers, Action<int> chosen)
    {
        Show(speaker, line, answers);
        onChosen = chosen;
    }

    /// <summary>Says a menu page as a conversation: its title is the speaker's name, its body the line and its actions
    /// the answers (plus "Goodbye" when it has any). How quest givers and companions talk.</summary>
    public void Talk(MenuPage page, DialogueSpeaker who, Action onEnded = null)
    {
        if (page.Actions.Count == 0)
        {
            Say(who, page.Body, onEnded);
            return;
        }

        var answers = new List<DialogueChoice>();
        foreach (MenuAction action in page.Actions)
        {
            answers.Add(new DialogueChoice(action.Label, action.Detail, action.Enabled));
        }

        answers.Add(new DialogueChoice("Goodbye."));
        Ask(who, page.Body, answers, index =>
        {
            if (index >= 0 && index < page.Actions.Count)
            {
                page.Actions[index].Chosen?.Invoke();
            }

            onEnded?.Invoke();
        });
    }

    /// <summary>Closes the box: whoever waited for an answer gets -1.</summary>
    public void Close()
    {
        Action<int> waiting = onChosen;
        onChosen = null;
        onContinue = null;
        if (window.IsOpen)
        {
            window.Close();
        }

        waiting?.Invoke(-1);
    }

    private void Show(DialogueSpeaker speaker, string line, IReadOnlyList<DialogueChoice> answers)
    {
        onContinue = null;
        onChosen = null;
        choices = answers;
        answered = false;
        nameLabel.text = speaker.Name ?? "";
        lineLabel.text = line ?? "";
        lineLabel.maxVisibleCharacters = 0;
        letters = lineLabel.text.Length;
        revealStarted = Time.unscaledTime;
        if (!window.IsOpen)
        {
            shownAt = Time.unscaledTime;
            window.Open();
        }

        bool live = speaker.Portrait == null && speaker.Model != null;
        portrait.gameObject.SetActive(live);
        picture.gameObject.SetActive(!live && speaker.Portrait != null);
        picture.sprite = speaker.Portrait;
        portraitCamera.Show(live ? speaker.Model : null);
        Frame(speaker.Model, speaker.CameraDistance);
        // Walking away ends a conversation with someone standing beside the player, not a shout from afar.
        bool beside = speaker.Model != null && global::Player.Current != null && speaker.Model != global::Player.Current.transform &&
                      (speaker.Model.position - global::Player.Current.transform.position).sqrMagnitude <= WalkAwayDistance * WalkAwayDistance;
        anchor = beside ? speaker.Model : speaker.Model != null && global::Player.Current != null && speaker.Model == global::Player.Current.transform ? anchor : null;

        for (int i = 0; i < buttons.Count; i++)
        {
            bool shown = answers != null && i < answers.Count;
            buttons[i].gameObject.SetActive(shown);
            if (shown)
            {
                DialogueChoice choice = answers[i];
                buttons[i].interactable = choice.Enabled;
                buttons[i].image.color = choice.Enabled ? ButtonColour : ButtonOff;
                buttonLabels[i].text = $"<color=#C9A86A>{i + 1}.</color> {choice.Label}" +
                                       (string.IsNullOrWhiteSpace(choice.Detail) ? "" : $"  <size=75%><alpha=#99>{choice.Detail}</size>");
            }
        }

        UpdateHint();
    }

    private bool Revealed => lineLabel.maxVisibleCharacters >= letters;

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (!Revealed)
        {
            lineLabel.maxVisibleCharacters = Mathf.Min(letters, Mathf.FloorToInt((Time.unscaledTime - revealStarted) * LettersPerSecond));
            UpdateHint();
        }

        if (anchor != null && global::Player.Current != null &&
            (anchor.position - global::Player.Current.transform.position).sqrMagnitude > WalkAwayDistance * WalkAwayDistance)
        {
            Close();
            return;
        }

        if (Time.unscaledTime - shownAt < InputDelay || answered)
        {
            return;
        }

        Keyboard keys = Keyboard.current;
        bool next = keys != null && (keys.eKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame)
                    || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !OverButton();
        if (next && !Revealed)
        {
            lineLabel.maxVisibleCharacters = letters;
            UpdateHint();
            return;
        }

        if (choices != null && choices.Count > 0)
        {
            Key[] numbers = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
            for (int i = 0; i < Mathf.Min(numbers.Length, choices.Count); i++)
            {
                if (keys != null && keys[numbers[i]].wasPressedThisFrame && choices[i].Enabled)
                {
                    Choose(i);
                    return;
                }
            }

            return;
        }

        if (next)
        {
            Continue();
        }
    }

    private void Continue()
    {
        answered = true;
        Action done = onContinue;
        onContinue = null;
        done?.Invoke();
        CloseIfNothingFollowed();
    }

    private void Choose(int index)
    {
        if (!Revealed)
        {
            lineLabel.maxVisibleCharacters = letters;
        }

        answered = true;
        Action<int> chosen = onChosen;
        onChosen = null;
        chosen?.Invoke(index);
        CloseIfNothingFollowed();
    }

    // A callback that said or asked something else has shown a new line (answered is false again).
    private void CloseIfNothingFollowed()
    {
        if (answered && window.IsOpen)
        {
            window.Close();
        }
    }

    private void UpdateHint()
    {
        bool asking = choices != null && choices.Count > 0;
        hint.text = !Revealed ? "<alpha=#88>E ›" : asking ? "" : "E / Space  ›";
    }

    private void Frame(Transform speaker, float distance = 0f)
    {
        if (speaker == framed)
        {
            return;
        }

        if (framing != null && framed != null)
        {
            framing.EndDialogue(framed);
        }

        framed = speaker != null && (global::Player.Current == null || speaker != global::Player.Current.transform) ? speaker : null;
        framing = framing != null ? framing : FindAnyObjectByType<PlayerCameraController>();
        if (framing != null && framed != null)
        {
            framing.BeginDialogue(framed, distance > 0f ? distance : 0.65f);
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (open)
        {
            return;
        }

        // Closed some other way (Escape closes every window): nobody waits for an answer that won't come.
        Action<int> waiting = onChosen;
        onChosen = null;
        onContinue = null;
        waiting?.Invoke(-1);
        portraitCamera.Show(null);
        Frame(null);
        anchor = null;
        Closed?.Invoke();
    }

    private static bool OverButton()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && EventSystem.current.currentSelectedGameObject != null;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }

        if (portraitCamera != null)
        {
            Destroy(portraitCamera.gameObject);
        }
    }

    // ----- Building the box -----

    private static DialogueBox Create()
    {
        var root = new GameObject("Dialogue Box");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var box = root.AddComponent<DialogueBox>();

        RectTransform panel = Rect("Panel", root.transform, new Vector2(0.5f, 0f), new Vector2(1060f, 250f), new Vector2(0f, 170f));
        box.content = panel.gameObject;
        var background = panel.gameObject.AddComponent<Image>();
        background.color = Panel;
        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = Rim;
        rim.effectDistance = new Vector2(2f, -2f);

        RectTransform frame = Rect("Portrait Frame", panel, new Vector2(0f, 0.5f), new Vector2(206f, 206f), new Vector2(125f, 0f));
        frame.gameObject.AddComponent<Image>().color = Rim;
        RectTransform face = Rect("Portrait", frame, new Vector2(0.5f, 0.5f), new Vector2(198f, 198f), Vector2.zero);
        box.portraitCamera = DialoguePortrait.Create();
        box.portrait = face.gameObject.AddComponent<RawImage>();
        box.portrait.texture = box.portraitCamera.Texture;
        RectTransform still = Rect("Picture", frame, new Vector2(0.5f, 0.5f), new Vector2(198f, 198f), Vector2.zero);
        box.picture = still.gameObject.AddComponent<Image>();
        box.picture.preserveAspect = true;

        box.nameLabel = Text("Name", panel, 28f, Title, new Vector2(0f, 1f), new Vector2(560f, 40f), new Vector2(530f, -36f), TextAlignmentOptions.Left);
        box.nameLabel.fontStyle = FontStyles.Bold;
        box.lineLabel = Text("Line", panel, 24f, Body, new Vector2(0f, 1f), new Vector2(480f, 175f), new Vector2(510f, -140f), TextAlignmentOptions.TopLeft);
        box.hint = Text("Hint", panel, 18f, Title, new Vector2(1f, 0f), new Vector2(200f, 30f), new Vector2(-110f, 22f), TextAlignmentOptions.Right);

        RectTransform answers = Rect("Answers", panel, new Vector2(1f, 0.5f), new Vector2(300f, 220f), new Vector2(-160f, 0f));
        var column = answers.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 6f;
        column.childForceExpandHeight = false;
        column.childControlHeight = true;
        column.childControlWidth = true;
        column.childAlignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var row = new GameObject("Answer " + (i + 1), typeof(RectTransform));
            row.transform.SetParent(answers, false);
            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 46f;
            var image = row.AddComponent<Image>();
            var button = row.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => box.Choose(index));
            TMP_Text label = Text("Label", row.transform, 20f, Body, new Vector2(0.5f, 0.5f), new Vector2(284f, 44f), Vector2.zero, TextAlignmentOptions.Left);
            box.buttons.Add(button);
            box.buttonLabels.Add(label);
        }

        box.window = root.AddComponent<UIWindow>();
        box.window.Configure(UIWindowId.Dialogue, panel.gameObject);
        box.window.OpenChanged += box.OnOpenChanged;
        root.SetActive(true);
        return box;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Text(string name, Transform parent, float size, Color colour, Vector2 anchorAt, Vector2 box, Vector2 position,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(name, parent, anchorAt, box, position);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
