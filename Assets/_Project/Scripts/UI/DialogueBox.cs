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
    /// <summary>What is heard as each of their lines starts. Optional: most people are only read.</summary>
    public readonly SoundDefinition Voice;
    /// <summary>Where the voice comes from, when that isn't the model: the portal has no face to frame, but it has a place.</summary>
    public readonly Transform VoiceFrom;

    public DialogueSpeaker(string name, Transform model = null, Sprite portrait = null, float cameraDistance = 0f, SoundDefinition voice = null, Transform voiceFrom = null)
    {
        Name = name;
        Model = model;
        Portrait = portrait;
        CameraDistance = cameraDistance;
        Voice = voice;
        VoiceFrom = voiceFrom;
    }

    /// <summary>The player, by the name given, else the name they chose for their character (or "You").</summary>
    public static DialogueSpeaker Player(string name = null) =>
        new DialogueSpeaker(!string.IsNullOrWhiteSpace(name) ? name : !string.IsNullOrWhiteSpace(GameSession.Appearance.name) ? GameSession.Appearance.DisplayName : "You",
            global::Player.Current != null ? global::Player.Current.transform : null);
}

/// <summary>One answer the player can give in the dialogue box.</summary>
public sealed class DialogueChoice
{
    public readonly string Label;
    /// <summary>Shown smaller after the label, e.g. the quest it takes.</summary>
    public readonly string Detail;
    public readonly bool Enabled;
    /// <summary>The quest this answer is about, in the colour of its rank.</summary>
    public readonly QuestDefinition Quest;
    public readonly QuestAnswer Does;

    public DialogueChoice(string label, string detail = null, bool enabled = true, QuestDefinition quest = null, QuestAnswer does = QuestAnswer.None)
    {
        Label = label;
        Detail = detail;
        Enabled = enabled;
        Quest = quest;
        Does = quest != null ? does : QuestAnswer.None;
    }

    /// <summary>What the answer's tag says: the quest it starts or hands in, in its rank's colour. Null for an ordinary answer.</summary>
    public string QuestTag()
    {
        if (Does == QuestAnswer.None)
        {
            return null;
        }

        string text = Does == QuestAnswer.Starts ? $"! Starts {QuestMarkerSettings.RankName(Quest.Rank).ToLowerInvariant()}: {Quest.DisplayName}"
            : Does == QuestAnswer.HandsIn ? $"? Hands in: {Quest.DisplayName}"
            : $"! {QuestMarkerSettings.RankName(Quest.Rank)}: {Quest.DisplayName}";
        return $"<color={QuestMarkerSettings.Current.RankHex(Quest.Rank)}>{text}</color>";
    }
}

/// <summary>What choosing an answer does to its quest.</summary>
public enum QuestAnswer
{
    None,
    /// <summary>Choosing it takes the quest on.</summary>
    Starts,
    /// <summary>Choosing it hands the quest in.</summary>
    HandsIn,
    /// <summary>Choosing it asks about a quest on offer, which can still be turned down.</summary>
    AsksAbout,
}

/// <summary>
/// The conversation box at the bottom of the screen: the speaker's live portrait (DialoguePortrait) and name, their line
/// written out letter by letter, and either "continue" or the player's answers. E, Space, Enter or a click finishes the
/// line, then continues; 1-4 (or a click) picks an answer. While a speaker in the world talks, the camera frames them and
/// the player (PlayerCameraController.BeginDialogue). It is a UI window (UIWindowId.Dialogue), so the player doesn't
/// attack or use things while talking.
/// <para>
/// In a fight (a monster hunting the player or a companion, <see cref="MonsterAI.AnyHuntingPlayerSide"/>) a line without
/// answers is a subtitle instead: one strip above the HUD, shown for its reading time, that leaves the controls, the
/// cursor and the camera to the player; a fight that starts during such a line turns it into one. Lines with answers
/// always wait for the player.
/// </para>
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
    private const float PanelWidth = 1060f;
    // The portrait's share of the box, which a voice with no face speaks into.
    private const float FaceWidth = 226f;
    private const float NameX = 530f;
    private const float LineX = 510f;
    private const float LineWidth = 480f;
    private const float PanelHeight = 250f;
    private const float BubbleAnswersWidth = 380f;
    private const float AnswerRowHeight = 46f;
    private const float AnswerRowStep = 52f;
    // A subtitle stays for its reading time: a second plus letters at this pace, within these bounds.
    private const float SubtitleLettersPerSecond = 14f;
    private const float SubtitleMinSeconds = 4f;
    private const float SubtitleMaxSeconds = 12f;
    private const float FightCheckInterval = 0.25f;

    private static Color Panel => UIStyle.Current.Panel;
    private static Color Rim => UIStyle.Current.Rim;
    private static Color Title => UIStyle.Current.Accent;
    private static Color Body => UIStyle.Current.Text;
    private static Color ButtonColour => UIStyle.Current.ControlLit;
    private static Color ButtonOff => UIStyle.Current.Control;

    private static DialogueBox current;

    private readonly List<Button> buttons = new List<Button>();
    private readonly List<TMP_Text> buttonLabels = new List<TMP_Text>();
    private UIWindow window;
    private GameObject content;
    private RectTransform answerColumn;
    private GameObject portraitFrame;
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
    private GameObject subtitleStrip;
    private TMP_Text subtitle;
    private Action subtitleDone;
    private float subtitleEnds = -1f;
    private float nextFightCheck;
    private float subtitleTimeMultiplier = 1f;
    // True while the line shown is being heard from its recording (VoicePlayer).
    private bool voicing;
    private bool demoting;
    // Speech bubbles (2026-11-09): a line from someone in the world is shown in a bubble over them (SpeechBubble); the
    // box then only holds the answers, if any. A voice with nobody to point at keeps the box.
    private SpeechBubble bubble;
    private TMP_Text activeLine;
    private TMP_Text activeHint;
    private Transform shownOver;
    private Image panelBackground;
    private Outline panelRim;

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

    /// <summary>True while anyone is talking: a line in the box or a subtitle.</summary>
    public static bool IsSpeaking => IsOpen || (current != null && current.IsSubtitling);

    /// <summary>Whether this character is speaking in the conversation window, rather than a combat subtitle.</summary>
    public static bool IsTalkingTo(Transform speaker) => speaker != null && IsOpen && current.framed == speaker;

    private static int clickFrame = -1;

    /// <summary>True while a click belongs to the conversation: a line is shown, or the click that closed it this frame.
    /// The player's attack ignores such clicks, so reading on never draws the bow.</summary>
    public static bool ClaimsClick => IsOpen || clickFrame == Time.frameCount;

    /// <summary>Raised when the box closes (moved on past the last line, walked away or closed).</summary>
    public event Action Closed;

    /// <summary>Raised with the speaker and the words when a line is said that has no recording: how the editor
    /// learns of lines that still want a voice.</summary>
    public static event Action<string, string> Unvoiced;

    /// <summary>True while the line shown is being heard from its recording.</summary>
    public bool IsVoicing => voicing && VoicePlayer.IsSpeaking;

    /// <summary>Shows a line; <paramref name="done"/> runs when the player moves on. Without a follow-up the box closes.</summary>
    public void Say(DialogueSpeaker speaker, string line, Action done = null, float subtitleTimeMultiplier = 1f)
    {
        line = GameInput.WithRebinds(line);
        this.subtitleTimeMultiplier = Mathf.Clamp(subtitleTimeMultiplier, 1f, 2f);
        if (!IsOpen && MonsterAI.AnyHuntingPlayerSide())
        {
            Speak(speaker, line);
            Subtitle(speaker.Name, line, done, Over(speaker));
            return;
        }

        Show(speaker, line, null);
        onContinue = done;
    }

    /// <summary>A line spoken aside, on the road or by the fire: a subtitle whatever is going on, leaving the controls and the
    /// camera to the player. <paramref name="done"/> runs when it has been read.</summary>
    public void Aside(DialogueSpeaker speaker, string line, Action done = null)
    {
        subtitleTimeMultiplier = 1f;
        Speak(speaker, line);
        Subtitle(speaker.Name, line, done, Over(speaker));
    }

    /// <summary>True while a subtitle is shown.</summary>
    public bool IsSubtitling => subtitleEnds >= 0f;

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
            answers.Add(new DialogueChoice(action.Label, action.Detail, action.Enabled, action.Quest, action.Does));
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
        if (IsSubtitling)
        {
            subtitleDone = null;
            EndSubtitle();
        }

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
        // The box takes over from a subtitle; whoever waited for it hears Closed when the box closes.
        subtitleDone = null;
        subtitleEnds = -1f;
        subtitleStrip.SetActive(false);
        onContinue = null;
        onChosen = null;
        choices = answers;
        answered = false;
        nameLabel.text = speaker.Name ?? "";
        lineLabel.text = line ?? "";
        shownOver = Over(speaker);
        bool bubbled = shownOver != null;
        if (bubbled)
        {
            bubble.Show(speaker.Name, line, shownOver);
            activeLine = bubble.Line;
            activeHint = bubble.Hint;
        }
        else
        {
            bubble.Hide();
            activeLine = lineLabel;
            activeHint = hint;
        }

        activeLine.maxVisibleCharacters = 0;
        letters = activeLine.text.Length;
        revealStarted = Time.unscaledTime;
        if (!window.IsOpen)
        {
            shownAt = Time.unscaledTime;
            window.Open();
        }

        // A resident speaks with their painted portrait; anyone else without a picture of their own is filmed live.
        Sprite still = speaker.Portrait != null ? speaker.Portrait : PaintedPortraitOf(speaker.Model);
        bool live = still == null && speaker.Model != null && !bubbled;
        portrait.gameObject.SetActive(live);
        picture.gameObject.SetActive(!live && !bubbled && still != null);
        picture.sprite = still;
        portraitCamera.Show(live ? speaker.Model : null);
        LayOutFor(live || (!bubbled && still != null));
        Frame(speaker.Model, speaker.CameraDistance);
        if (!string.IsNullOrWhiteSpace(line))
        {
            CharacterGestures.Speak(speaker.Model);
        }

        Speak(speaker, line);
        // Walking away ends a conversation with someone standing beside the player, not a shout from afar.
        bool beside = speaker.Model != null && global::Player.Current != null && speaker.Model != global::Player.Current.transform &&
                      (speaker.Model.position - global::Player.Current.transform.position).sqrMagnitude <= WalkAwayDistance * WalkAwayDistance;
        anchor = beside ? speaker.Model : speaker.Model != null && global::Player.Current != null && speaker.Model == global::Player.Current.transform ? anchor : null;

        int count = answers != null ? answers.Count : 0;
        while (buttons.Count < count)
        {
            AddAnswerRow();
        }

        // Up to four answers fit the box; more make it taller, growing upwards. With a bubble the box is only the answers:
        // a narrow column (nothing at all when there are none; the bubble has the hint).
        float height = bubbled ? count * AnswerRowStep + 30f : Mathf.Max(PanelHeight, count * AnswerRowStep + 30f);
        ((RectTransform)content.transform).sizeDelta = new Vector2(bubbled ? BubbleAnswersWidth : PanelWidth, height);
        answerColumn.sizeDelta = new Vector2(answerColumn.sizeDelta.x, height - 30f);
        answerColumn.anchoredPosition = new Vector2(bubbled ? -BubbleAnswersWidth / 2f : -160f, 0f);
        panelBackground.enabled = !bubbled || count > 0;
        panelRim.enabled = panelBackground.enabled;
        portraitFrame.SetActive(!bubbled && portraitFrame.activeSelf);
        nameLabel.gameObject.SetActive(!bubbled);
        lineLabel.gameObject.SetActive(!bubbled);
        hint.gameObject.SetActive(!bubbled);
        for (int i = 0; i < buttons.Count; i++)
        {
            bool shown = i < count;
            buttons[i].gameObject.SetActive(shown);
            if (shown)
            {
                DialogueChoice choice = answers[i];
                buttons[i].interactable = choice.Enabled;
                buttons[i].image.color = choice.Enabled ? ButtonColour : ButtonOff;
                string tag = choice.QuestTag();
                buttonLabels[i].text = $"<color={UIStyle.Current.AccentHex}>{i + 1}.</color> {choice.Label}" +
                                       (tag != null ? $"\n<size=75%>{tag}</size>" :
                                       string.IsNullOrWhiteSpace(choice.Detail) ? "" : $"  <size=75%><alpha=#99>{choice.Detail}</size>");
            }
        }

        UpdateHint();
    }

    // The line's recording when the voice bank has one (found by speaker and words), which also ends the line before
    // it. Otherwise the speaker's own sound, as before recordings: most people are then only read.
    private void Speak(DialogueSpeaker speaker, string line)
    {
        voicing = false;
        if (string.IsNullOrWhiteSpace(line))
        {
            VoicePlayer.Stop();
            return;
        }

        if (VoicePlayer.Say(speaker.Name, line))
        {
            voicing = true;
            return;
        }

        Unvoiced?.Invoke(speaker.Name, line);
        if (speaker.Voice == null)
        {
            return;
        }

        Transform from = speaker.VoiceFrom != null ? speaker.VoiceFrom : speaker.Model;
        AudioManager.Play(speaker.Voice, from != null ? from.position : AudioManager.EarPosition);
    }

    // The portal and a voice through its stones have no face to show, so their words take its place.
    // The resident a speaker is (their ResidentNpc, PrologueCompanion or Companion), if they have a painted portrait.
    private static Sprite PaintedPortraitOf(Transform model)
    {
        IQuestPerson person = model != null ? model.GetComponentInParent<IQuestPerson>() : null;
        return person != null && person.Resident != null ? person.Resident.Portrait : null;
    }

    private void LayOutFor(bool hasFace)
    {
        float freed = hasFace ? 0f : FaceWidth;
        portraitFrame.SetActive(hasFace);
        nameLabel.rectTransform.anchoredPosition = new Vector2(NameX - freed, nameLabel.rectTransform.anchoredPosition.y);
        lineLabel.rectTransform.sizeDelta = new Vector2(LineWidth + freed, lineLabel.rectTransform.sizeDelta.y);
        lineLabel.rectTransform.anchoredPosition = new Vector2(LineX - freed / 2f, lineLabel.rectTransform.anchoredPosition.y);
    }

    private bool Revealed => activeLine.maxVisibleCharacters >= letters;

    // Where a bubble goes: over the speaker, else where their voice comes from (the portal); null keeps the box.
    private static Transform Over(DialogueSpeaker speaker) => speaker.Model != null ? speaker.Model : speaker.VoiceFrom;

    private void Update()
    {
        if (IsSubtitling && Time.time >= subtitleEnds)
        {
            FinishSubtitle();
        }

        if (IsVoicing)
        {
            // Nobody is left reading the line (walked away, moved on, closed): it stops with its words.
            if (!window.IsOpen && !IsSubtitling)
            {
                VoicePlayer.Stop();
                voicing = false;
            }
            else
            {
                VoicePlayer.FollowVolume();
            }
        }

        if (!window.IsOpen)
        {
            return;
        }

        if (GameInput.Click.WasPressedThisFrame())
        {
            clickFrame = Time.frameCount;
        }

        bool asking = choices != null && choices.Count > 0;
        if (!asking && !answered && Time.unscaledTime >= nextFightCheck)
        {
            nextFightCheck = Time.unscaledTime + FightCheckInterval;
            if (MonsterAI.AnyHuntingPlayerSide())
            {
                ToSubtitle();
                return;
            }
        }

        if (!Revealed)
        {
            float shown = (Time.unscaledTime - revealStarted) * GameOptions.DialogueLettersPerSecond(LettersPerSecond);
            activeLine.maxVisibleCharacters = float.IsInfinity(shown) || shown >= letters ? letters : Mathf.FloorToInt(shown);
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

        // UI › Advance (E, Space, Enter), or a click that isn't on one of the answer buttons.
        bool next = GameInput.Advance.WasPressedThisFrame() || GameInput.Click.WasPressedThisFrame() && !OverButton();
        if (next && !Revealed)
        {
            activeLine.maxVisibleCharacters = letters;
            UpdateHint();
            return;
        }

        if (choices != null && choices.Count > 0)
        {
            // UI › Answer1-9: the number keys pick an answer.
            for (int i = 0; i < Mathf.Min(GameInput.AnswerCount, choices.Count); i++)
            {
                if (GameInput.Answer(i).WasPressedThisFrame() && choices[i].Enabled)
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
            activeLine.maxVisibleCharacters = letters;
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
        activeHint.text = !Revealed ? "<alpha=#88>E ›" : asking ? "" : "E / Space  ›";
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
        framing = framing != null ? framing : PlayerCameraController.Current;
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

        if (!IsSubtitling)
        {
            bubble.Hide();
        }

        if (demoting)
        {
            portraitCamera.Show(null);
            Frame(null);
            anchor = null;
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

    // ----- Subtitles -----

    private void Subtitle(string speakerName, string line, Action done, Transform over = null)
    {
        subtitleDone = done;
        if (over != null)
        {
            // Over whoever says it, read in its own time like a subtitle, nothing to press.
            subtitleStrip.SetActive(false);
            bubble.Show(speakerName, line, over);
            bubble.Line.maxVisibleCharacters = int.MaxValue;
        }
        else
        {
            bubble.Hide();
            subtitle.text = string.IsNullOrWhiteSpace(speakerName) ? line : $"<color={UIStyle.Current.AccentHex}>{speakerName}:</color> {line}";
            subtitleStrip.SetActive(true);
        }

        float seconds = (1f + (line ?? "").Length / SubtitleLettersPerSecond) * GameOptions.SubtitleHold;
        float reading = Mathf.Clamp(seconds, SubtitleMinSeconds, SubtitleMaxSeconds) * subtitleTimeMultiplier;
        // A line that is heard stays until it has been said, however short its reading time.
        subtitleEnds = Time.time + (IsVoicing ? Mathf.Max(reading, VoicePlayer.SecondsLeft + VoiceBank.Current.SubtitleTail) : reading);
    }

    // The line on screen when the fight started goes on as a subtitle, with whatever was to follow it.
    private void ToSubtitle()
    {
        Action done = onContinue;
        onContinue = null;
        string speakerName = nameLabel.text;
        string line = lineLabel.text;
        Transform over = shownOver;
        demoting = true;
        window.Close();
        demoting = false;
        Subtitle(speakerName, line, done, over);
    }

    private void FinishSubtitle()
    {
        Action done = subtitleDone;
        subtitleDone = null;
        subtitleEnds = -1f;
        done?.Invoke();
        if (!IsSubtitling && !window.IsOpen)
        {
            EndSubtitle();
        }
    }

    private void EndSubtitle()
    {
        subtitleEnds = -1f;
        subtitleStrip.SetActive(false);
        if (!window.IsOpen)
        {
            bubble.Hide();
            Closed?.Invoke();
        }
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
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var box = root.AddComponent<DialogueBox>();

        RectTransform panel = Rect("Panel", root.transform, new Vector2(0.5f, 0f), new Vector2(PanelWidth, PanelHeight), new Vector2(0f, 45f));
        panel.pivot = new Vector2(0.5f, 0f);
        box.content = panel.gameObject;
        var background = panel.gameObject.AddComponent<Image>();
        background.color = Panel;
        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = Rim;
        rim.effectDistance = new Vector2(2f, -2f);
        box.panelBackground = background;
        box.panelRim = rim;

        RectTransform frame = Rect("Portrait Frame", panel, new Vector2(0f, 0.5f), new Vector2(206f, 206f), new Vector2(125f, 0f));
        frame.gameObject.AddComponent<Image>().color = Rim;
        box.portraitFrame = frame.gameObject;
        RectTransform face = Rect("Portrait", frame, new Vector2(0.5f, 0.5f), new Vector2(198f, 198f), Vector2.zero);
        box.portraitCamera = DialoguePortrait.Create();
        box.portrait = face.gameObject.AddComponent<RawImage>();
        box.portrait.texture = box.portraitCamera.Texture;
        RectTransform still = Rect("Picture", frame, new Vector2(0.5f, 0.5f), new Vector2(198f, 198f), Vector2.zero);
        box.picture = still.gameObject.AddComponent<Image>();
        box.picture.preserveAspect = true;

        box.nameLabel = Text("Name", panel, 28f, Title, new Vector2(0f, 1f), new Vector2(560f, 40f), new Vector2(NameX, -36f), TextAlignmentOptions.Left);
        box.nameLabel.fontStyle = FontStyles.Bold;
        box.lineLabel = Text("Line", panel, 24f, Body, new Vector2(0f, 1f), new Vector2(LineWidth, 175f), new Vector2(LineX, -140f), TextAlignmentOptions.TopLeft);
        // Long lines (a leader's briefing) shrink to fit rather than run out of the box.
        box.lineLabel.enableAutoSizing = true;
        box.lineLabel.fontSizeMin = 15f;
        box.lineLabel.fontSizeMax = 24f;
        box.hint = Text("Hint", panel, 18f, Title, new Vector2(1f, 0f), new Vector2(200f, 30f), new Vector2(-110f, 22f), TextAlignmentOptions.Right);

        box.answerColumn = Rect("Answers", panel, new Vector2(1f, 0.5f), new Vector2(300f, PanelHeight - 30f), new Vector2(-160f, 0f));
        var column = box.answerColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = AnswerRowStep - AnswerRowHeight;
        column.childForceExpandHeight = false;
        column.childControlHeight = true;
        column.childControlWidth = true;
        column.childAlignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < 4; i++)
        {
            box.AddAnswerRow();
        }

        // Above the HUD, and never in the way of a click: the fight goes on beneath it.
        RectTransform strip = Rect("Subtitle", root.transform, new Vector2(0.5f, 0f), new Vector2(PanelWidth, 64f), new Vector2(0f, 330f));
        Image stripBackground = strip.gameObject.AddComponent<Image>();
        stripBackground.color = new Color(Panel.r, Panel.g, Panel.b, 0.78f);
        stripBackground.raycastTarget = false;
        box.subtitle = Text("Line", strip, 24f, Body, new Vector2(0.5f, 0.5f), new Vector2(PanelWidth - 40f, 56f), Vector2.zero, TextAlignmentOptions.Center);
        box.subtitle.enableAutoSizing = true;
        box.subtitle.fontSizeMin = 16f;
        box.subtitle.fontSizeMax = 24f;
        box.subtitleStrip = strip.gameObject;
        strip.gameObject.SetActive(false);
        box.bubble = SpeechBubble.Create(root.transform);
        box.activeLine = box.lineLabel;
        box.activeHint = box.hint;

        box.window = root.AddComponent<UIWindow>();
        box.window.Configure(UIWindowId.Dialogue, panel.gameObject);
        box.window.OpenChanged += box.OnOpenChanged;
        root.SetActive(true);
        return box;
    }

    private void AddAnswerRow()
    {
        int index = buttons.Count;
        var row = new GameObject("Answer " + (index + 1), typeof(RectTransform));
        row.transform.SetParent(answerColumn, false);
        var element = row.AddComponent<LayoutElement>();
        element.preferredHeight = AnswerRowHeight;
        var image = row.AddComponent<Image>();
        var button = row.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => Choose(index));
        TMP_Text label = Text("Label", row.transform, 20f, Body, new Vector2(0.5f, 0.5f), new Vector2(284f, 44f), Vector2.zero, TextAlignmentOptions.Left);
        row.SetActive(false);
        buttons.Add(button);
        buttonLabels.Add(label);
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
