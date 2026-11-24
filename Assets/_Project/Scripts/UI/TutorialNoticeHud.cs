using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The notice that a tutorial is waiting. When one opens, a card at the right edge fades in quickly with the newest one's painting,
/// name, what it is in a line, and the key to read it all (T). A page with steps teaches on the card while the game goes on
/// (Docs/QualityLevers_2026-11-24.md §1: a new tutorial cost a trip into a window, 56 s of the first 13 at home): "TRY IT" and its steps,
/// each with its key, ticked off in order as that key is pressed (TutorialLesson); all done, "LEARNED" and the page counts as read. The
/// card stays while a lesson is under way (up to LessonSeconds), else fades slowly after a few seconds (not while the pointer is on it),
/// leaving a small line at the edge that says how many wait ("2 unread tutorials · T"). Clicking either, or the key, opens
/// TutorialWindow. Both step aside while a window covers the screen. Built in code for every scene with a player (ExperienceTracker).
/// </summary>
public sealed class TutorialNoticeHud : MonoBehaviour
{
    private const float CardWidth = 380f;
    // The card's head (painting, name, hint); what it is and the steps go under it.
    private const float CardHeight = 92f;
    private const float SummaryGap = 6f;
    private const float StepHeight = 26f;
    private const int MostSteps = 4;
    // A lesson keeps the card up this long at most; done, "LEARNED" shows this long.
    private const float LessonSeconds = 45f;
    private const float LearnedSeconds = 2.5f;
    private const string StepIndent = "<space=1.5em>";
    private const float ThumbWidth = 128f;
    private const float ThumbHeight = ThumbWidth * 9f / 16f;
    // Up from the middle of the right edge, clear of the quest tracker above and the minimap below.
    private const float EdgeGap = 24f;
    private const float RaisedBy = 40f;
    // The card: in quickly, held a while, out slowly; the small line after it.
    private const float FadeIn = 0.15f;
    private const float HoldSeconds = 8f;
    private const float FadeOut = 2.5f;
    private const float LineWidth = 250f;
    private const float LineHeight = 30f;

    private RectTransform card;
    private Image rim;
    private Image thumb;
    private TMP_Text thumbGlyph;
    private TMP_Text heading;
    private TMP_Text title;
    private TMP_Text hint;
    private int shownCount;
    private TMP_Text summary;
    private readonly TMP_Text[] stepRows = new TMP_Text[MostSteps];
    private TutorialLesson lesson;
    private float lessonUntil;
    private float learnedUntil = -1f;
    // The frame the lesson began: the press that opened its page (the first swing at a tree) doesn't also do its first step.
    private int lessonFrame;
    private CanvasGroup cardFade;
    private RectTransform line;
    private CanvasGroup lineFade;
    private TMP_Text lineText;
    private Image lineBackground;
    private GameObject lineRim;
    private Material lineMaterial;
    private HudRules.Density styledFor = (HudRules.Density)(-1);
    private int styledCount = -1;

    // The line once the card has gone: a framed box with a sentence on a Full HUD; in a Contextual one the book, the count and the
    // key, unframed (it stayed mid-screen at the right edge as a box all game: the HUD review of 2026-11-18). Clickable either way:
    // the background is only made clear.
    private void StyleLine(int count)
    {
        styledFor = HudRules.Current;
        styledCount = count;
        bool framed = HudRules.ShowsEverything;
        lineBackground.color = framed ? HearthKit.Faded(UIStyle.Current.Panel, 0.88f) : Color.clear;
        lineRim.SetActive(framed);
        // Unframed it lies on the ground like the rest of the frameless HUD, so it takes that soft shadow: without it "4 (T)" was
        // lost on the Haven's sunlit grass.
        if (lineMaterial == null)
        {
            lineMaterial = lineText.fontSharedMaterial;
        }
        if (framed)
        {
            lineText.fontSharedMaterial = lineMaterial;
        }
        else
        {
            UIStyle.Current.OnWorld(lineText);
        }
        lineText.text = HearthKit.InlineIcon("Book") + "  " + GameInput.WithRebinds(!framed ? $"{count}  ({{key:Tutorial}})"
            : count > 1 ? $"{count} unread tutorials  ·  ({{key:Tutorial}})" : "1 unread tutorial  ·  ({key:Tutorial})");
    }
    private float shownUntil;

    public static TutorialNoticeHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the notice (hidden until something waits) for this scene, unless it has one.</summary>
    public static TutorialNoticeHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Tutorial Notice");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<TutorialNoticeHud>();
        Current = view;
        view.Build(root.transform);
        return view;
    }

    /// <summary>Whether the card is up, for tests and probes.</summary>
    public bool Showing => card != null && card.gameObject.activeSelf;
    /// <summary>Whether the small "unread" line is up instead.</summary>
    public bool ShowingLine => line != null && line.gameObject.activeSelf;

    /// <summary>The page being learned on the card, with its steps done so far; null when the card has none.</summary>
    public TutorialLesson Lesson => lesson;

    /// <summary>The key named <paramref name="token"/> was pressed (Update, from the input; tests call it): the lesson's step now is done
    /// when it is that key's, and the page is learned when it was the last.</summary>
    public void Pressed(string token)
    {
        if (lesson == null || !lesson.Press(token))
        {
            return;
        }

        if (Application.isPlaying)
        {
            UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.QuestStarted : null);
        }

        if (!lesson.Finished)
        {
            Draw(lesson.Page, Tutorials.Pending.Count);
            return;
        }

        // Learned: the card says so a moment, then the next waiting page (if any) takes it. Read, it no longer waits.
        learnedUntil = Time.unscaledTime + LearnedSeconds;
        shownUntil = Mathf.Max(shownUntil, learnedUntil);
        Tutorials.MarkSeen(lesson.Page);
        Draw(lesson.Page, Tutorials.Pending.Count);
        if (Application.isPlaying && card.gameObject.activeSelf)
        {
            UIMotion.Punch(card);
        }
    }

    private void Awake()
    {
        Current = this;
    }

    private void OnEnable()
    {
        Tutorials.Changed += Refresh;
        GameInput.KeysChanged += Refresh;
    }

    private void OnDisable()
    {
        Tutorials.Changed -= Refresh;
        GameInput.KeysChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        // Hung from its top, so what it is and the steps grow it downwards.
        card = Box("Card", root, new Vector2(1f, 0.5f), new Vector2(CardWidth, CardHeight), new Vector2(-EdgeGap - CardWidth * 0.5f, RaisedBy + CardHeight * 0.5f));
        card.pivot = new Vector2(0.5f, 1f);
        // Hearthline 3: a rounded card in the kit's look, its gold rim breathing while it waits.
        Image background = HearthKit.Sliced(card.gameObject.AddComponent<Image>(), style.Card, 1.2f);
        background.color = style.Panel;
        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(TutorialWindow.ShowPending);
        button.transition = Selectable.Transition.None;
        card.gameObject.AddComponent<UIHover>();

        float thumbTop = (CardHeight - ThumbHeight) * 0.5f;
        RectTransform frame = HearthKit.Card(card, "Thumb Frame", 10f, thumbTop, ThumbWidth, ThumbHeight, 0f, style.Inset, 2f);
        thumbGlyph = Label("Glyph", frame, 0f, 0f, ThumbWidth, ThumbHeight, 34f, style.Accent, TextAlignmentOptions.Center);
        thumb = At("Thumb", frame, 3f, 3f, ThumbWidth - 6f, ThumbHeight - 6f).gameObject.AddComponent<Image>();
        thumb.preserveAspect = true;
        thumb.raycastTarget = false;
        HearthKit.Rim(frame, 0.7f, 2f).transform.SetAsLastSibling();

        float textX = ThumbWidth + 22f;
        float textWidth = CardWidth - textX - 10f;
        heading = Label("Heading", card, textX, 10f, textWidth, 20f, 13f, style.Accent);
        heading.characterSpacing = 4f;
        title = Label("Name", card, textX, 30f, textWidth, 30f, 20f, style.Text);
        style.Title(title);
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 14f;
        title.fontSizeMax = 20f;
        hint = Label("Hint", card, textX, 60f, textWidth, 22f, 15f, style.Muted);
        hint.richText = true;
        summary = Paragraph("What It Is", card, 12f, CardHeight + SummaryGap, CardWidth - 24f, 60f, 15f, style.Text);
        summary.richText = true;
        for (int i = 0; i < MostSteps; i++)
        {
            stepRows[i] = Label("Step " + (i + 1), card, 14f, CardHeight, CardWidth - 28f, StepHeight, 16f, style.Text);
            stepRows[i].richText = true;
            stepRows[i].gameObject.SetActive(false);
        }

        foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>())
        {
            text.raycastTarget = false;
        }

        rim = HearthKit.Rim(card, 1f, 1.2f);
        cardFade = card.gameObject.AddComponent<CanvasGroup>();
        cardFade.alpha = 0f;
        card.gameObject.SetActive(false);

        // The small line left at the edge once the card has faded: how many wait, and the key.
        line = Box("Unread", root, new Vector2(1f, 0.5f), new Vector2(LineWidth, LineHeight), new Vector2(-EdgeGap - LineWidth * 0.5f, RaisedBy - CardHeight * 0.5f + LineHeight * 0.5f));
        lineBackground = HearthKit.Sliced(line.gameObject.AddComponent<Image>(), style.Card, 2f);
        lineBackground.color = HearthKit.Faded(style.Panel, 0.88f);
        lineRim = HearthKit.Rim(line, 0.45f, 2f).gameObject;
        var lineButton = line.gameObject.AddComponent<Button>();
        lineButton.targetGraphic = lineBackground;
        lineButton.onClick.AddListener(TutorialWindow.ShowPending);
        lineButton.transition = Selectable.Transition.None;
        line.gameObject.AddComponent<UIHover>();
        lineText = Label("Text", line, 12f, 0f, LineWidth - 24f, LineHeight, 15f, style.Accent, TextAlignmentOptions.MidlineRight);
        lineText.raycastTarget = false;
        lineFade = line.gameObject.AddComponent<CanvasGroup>();
        lineFade.alpha = 0f;
        line.gameObject.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (card == null)
        {
            return;
        }

        if (styledFor != HudRules.Current && styledCount > 0)
        {
            StyleLine(styledCount);
        }

        // Out of the way while a window is up (the tutorial itself, the bag, a station...), back when it closes. The card only for a
        // while after a tutorial opens (longer while the pointer rests on it); then the small line.
        // The step's key, pressed in play (a window's own keys don't count: the gameplay keys are off under it).
        string token = lesson != null ? lesson.CurrentToken : null;
        if (token != null && Time.frameCount > lessonFrame)
        {
            UnityEngine.InputSystem.InputAction action = GameInput.ActionForToken(token);
            if (action != null && action.enabled && action.WasPressedThisFrame())
            {
                Pressed(token);
            }
        }

        // "LEARNED" has had its moment: the next waiting page, if any.
        if (learnedUntil > 0f && Time.unscaledTime >= learnedUntil)
        {
            learnedUntil = -1f;
            lesson = null;
            shownCount = 0;
            Refresh();
        }

        bool noWindow = UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen;
        bool pending = Tutorials.Pending.Count > 0 && noWindow;
        bool waiting = (Tutorials.Pending.Count > 0 || learnedUntil > 0f) && noWindow;
        bool teaching = lesson != null && !lesson.Finished && Time.unscaledTime < lessonUntil;
        if (card.gameObject.activeSelf && Hovered(card))
        {
            shownUntil = Mathf.Max(shownUntil, Time.unscaledTime + 1f);
        }

        bool wanted = waiting && (Time.unscaledTime < shownUntil || teaching);
        float dt = Time.unscaledDeltaTime;
        cardFade.alpha = Mathf.MoveTowards(cardFade.alpha, wanted ? 1f : 0f, dt / (wanted ? FadeIn : FadeOut));
        bool cardUp = cardFade.alpha > 0.001f;
        if (cardUp != card.gameObject.activeSelf)
        {
            card.gameObject.SetActive(cardUp);
        }

        bool lineWanted = pending && !wanted && cardFade.alpha < 0.3f;
        lineFade.alpha = Mathf.MoveTowards(lineFade.alpha, lineWanted ? 1f : 0f, dt / 0.4f);
        bool lineUp = lineFade.alpha > 0.001f;
        if (lineUp != line.gameObject.activeSelf)
        {
            line.gameObject.SetActive(lineUp);
        }

        if (cardUp)
        {
            rim.color = HearthKit.Faded(UIStyle.Current.Accent, Mathf.Lerp(0.35f, 1f, UIMotion.Breath(0.6f)));
        }
    }

    /// <summary>Shows what waits now (heard from Tutorials.Changed; an edit-mode test, where OnEnable doesn't run, calls it).</summary>
    public void Refresh()
    {
        if (card == null)
        {
            return;
        }

        int count = Tutorials.Pending.Count;
        // "LEARNED" holds the card until its moment is over (Update refreshes then).
        if (learnedUntil > 0f)
        {
            if (count > 0)
            {
                StyleLine(count);
            }

            if (lesson != null)
            {
                Draw(lesson.Page, count);
            }

            return;
        }

        if (count == 0)
        {
            shownCount = 0;
            lesson = null;
            return;
        }

        // The lesson: the newest waiting page with steps; one read in the window meanwhile is no longer taught.
        if (lesson != null && !Contains(Tutorials.Pending, lesson.Page))
        {
            lesson = null;
        }

        for (int i = count - 1; i >= 0 && lesson == null; i--)
        {
            if (TutorialLesson.HasSteps(Tutorials.Pending[i]))
            {
                lesson = new TutorialLesson(Tutorials.Pending[i]);
                lessonUntil = Time.unscaledTime + LessonSeconds;
                lessonFrame = Time.frameCount;
            }
        }

        JournalPage newest = lesson != null ? lesson.Page : Tutorials.Pending[count - 1];
        Draw(newest, count);
        StyleLine(count);
        // A new one brings the card back, quickly, for a while.
        if (count > shownCount)
        {
            shownUntil = Time.unscaledTime + HoldSeconds;
        }

        // Motion and sound are the game's: none in the Editor's tests.
        if (count > shownCount && card.gameObject.activeSelf && Application.isPlaying)
        {
            UIMotion.Punch(card);
        }

        if (count > shownCount && Application.isPlaying)
        {
            UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.QuestStarted : null);
        }

        shownCount = count;
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyList<JournalPage> pages, JournalPage page)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == page)
            {
                return true;
            }
        }

        return false;
    }

    // The card for <page>: its head, what it is, and its steps when it is the lesson.
    private void Draw(JournalPage page, int count)
    {
        bool teaching = lesson != null && lesson.Page == page;
        heading.text = teaching ? (lesson.Finished ? "LEARNED" : "TRY IT") : count > 1 ? $"{count} NEW TUTORIALS" : "NEW TUTORIAL";
        title.text = page.title;
        thumb.sprite = page.art;
        thumb.enabled = page.art != null;
        thumbGlyph.text = page.art == null && !string.IsNullOrEmpty(page.title) ? page.title.Substring(0, 1) : "";
        hint.text = GameInput.WithRebinds(teaching && lesson.Finished ? "In the journal ({key:Journal})" : "({key:Tutorial}) or click to read more");

        summary.text = GameInput.WithRebinds(string.IsNullOrWhiteSpace(page.summary) ? page.line : page.summary) ?? "";
        float summaryHeight = summary.text.Length == 0 ? 0f : Mathf.Ceil(summary.GetPreferredValues(summary.text, CardWidth - 24f, 0f).y);
        RectTransform summaryRect = summary.rectTransform;
        summaryRect.sizeDelta = new Vector2(summaryRect.sizeDelta.x, summaryHeight);

        UIStyle style = UIStyle.Current;
        float y = CardHeight + (summaryHeight > 0f ? SummaryGap + summaryHeight + 6f : 0f);
        int shown = teaching ? Mathf.Min(MostSteps, lesson.Count) : 0;
        for (int i = 0; i < MostSteps; i++)
        {
            TMP_Text row = stepRows[i];
            row.gameObject.SetActive(i < shown);
            if (i >= shown)
            {
                continue;
            }

            string step = GameInput.WithRebinds(page.steps[i]);
            if (i < lesson.Done)
            {
                row.text = HearthKit.InlineIcon("Star") + "  <s>" + step + "</s>";
                row.color = style.Muted;
            }
            else
            {
                // Indented as far as a done row's star, so the keys line up.
                row.text = StepIndent + (i == lesson.Done ? "<b>" + step + "</b>" : step);
                row.color = i == lesson.Done ? style.Text : style.Faint;
            }

            row.rectTransform.anchoredPosition = new Vector2(14f, -y);
            y += StepHeight;
        }

        card.sizeDelta = new Vector2(CardWidth, y + 10f);
    }

    private static bool Hovered(RectTransform rect)
    {
        if (GameInput.Point == null)
        {
            return false;
        }

        Vector2 pointer = GameInput.Point.ReadValue<Vector2>();
        return RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, null);
    }
}
