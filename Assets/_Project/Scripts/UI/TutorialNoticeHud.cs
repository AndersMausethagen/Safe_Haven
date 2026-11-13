using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The notice that a tutorial is waiting. When one opens, a card at the right edge fades in quickly with the newest one's painting and
/// name and the key to read it (T); left unread it fades slowly away after a few seconds (not while the pointer is on it), leaving only a
/// small line at the edge that says how many wait ("2 unread tutorials · T"), which stays until they are read. Clicking either, or the
/// key, opens TutorialWindow. Both step aside while a window covers the screen. It only reads Tutorials. Built in code for every scene with
/// a player (ExperienceTracker).
/// </summary>
public sealed class TutorialNoticeHud : MonoBehaviour
{
    private const float CardWidth = 340f;
    private const float CardHeight = 92f;
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
    private CanvasGroup cardFade;
    private RectTransform line;
    private CanvasGroup lineFade;
    private TMP_Text lineText;
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
        card = Box("Card", root, new Vector2(1f, 0.5f), new Vector2(CardWidth, CardHeight), new Vector2(-EdgeGap - CardWidth * 0.5f, RaisedBy));
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
        Image lineBackground = HearthKit.Sliced(line.gameObject.AddComponent<Image>(), style.Card, 2f);
        lineBackground.color = HearthKit.Faded(style.Panel, 0.88f);
        HearthKit.Rim(line, 0.45f, 2f);
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

        // Out of the way while a window is up (the tutorial itself, the bag, a station...), back when it closes. The card only for a
        // while after a tutorial opens (longer while the pointer rests on it); then the small line.
        bool waiting = Tutorials.Pending.Count > 0 && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen);
        if (card.gameObject.activeSelf && Hovered(card))
        {
            shownUntil = Mathf.Max(shownUntil, Time.unscaledTime + 1f);
        }

        bool wanted = waiting && Time.unscaledTime < shownUntil;
        float dt = Time.unscaledDeltaTime;
        cardFade.alpha = Mathf.MoveTowards(cardFade.alpha, wanted ? 1f : 0f, dt / (wanted ? FadeIn : FadeOut));
        bool cardUp = cardFade.alpha > 0.001f;
        if (cardUp != card.gameObject.activeSelf)
        {
            card.gameObject.SetActive(cardUp);
        }

        bool lineWanted = waiting && !wanted && cardFade.alpha < 0.3f;
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

    private void Refresh()
    {
        if (card == null)
        {
            return;
        }

        int count = Tutorials.Pending.Count;
        if (count == 0)
        {
            shownCount = 0;
            return;
        }

        JournalPage newest = Tutorials.Pending[count - 1];
        heading.text = count > 1 ? $"{count} NEW TUTORIALS" : "NEW TUTORIAL";
        title.text = newest.title;
        thumb.sprite = newest.art;
        thumb.enabled = newest.art != null;
        thumbGlyph.text = newest.art == null && !string.IsNullOrEmpty(newest.title) ? newest.title.Substring(0, 1) : "";
        hint.text = GameInput.WithRebinds("Press ({key:Tutorial}) or click to read");
        lineText.text = HearthKit.InlineIcon("Book") + "  " + GameInput.WithRebinds(count > 1 ? $"{count} unread tutorials  ·  ({{key:Tutorial}})" : "1 unread tutorial  ·  ({key:Tutorial})");
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
