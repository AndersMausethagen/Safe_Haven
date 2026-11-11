using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The notice that a tutorial is waiting: a small card at the right edge with the newest one's painting and name and the key to read it (T).
/// Clicking it, or the key, opens TutorialWindow. It stays until every waiting tutorial is read, and steps aside while a window covers the
/// screen. It only reads Tutorials. Built in code for every scene with a player (ExperienceTracker).
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

    private RectTransform card;
    private Outline rim;
    private Image thumb;
    private TMP_Text thumbGlyph;
    private TMP_Text heading;
    private TMP_Text title;
    private TMP_Text hint;
    private int shownCount;

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

    private void Awake()
    {
        Current = this;
    }

    private void OnEnable()
    {
        Tutorials.Changed += Refresh;
    }

    private void OnDisable()
    {
        Tutorials.Changed -= Refresh;
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
        var background = card.gameObject.AddComponent<Image>();
        background.color = style.Panel;
        rim = card.gameObject.AddComponent<Outline>();
        rim.effectColor = style.Accent;
        rim.effectDistance = new Vector2(1.5f, -1.5f);
        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(TutorialWindow.ShowPending);
        button.transition = Selectable.Transition.None;
        card.gameObject.AddComponent<UIHover>();

        float thumbTop = (CardHeight - ThumbHeight) * 0.5f;
        RectTransform frame = At("Thumb Frame", card, 10f, thumbTop, ThumbWidth, ThumbHeight);
        frame.gameObject.AddComponent<Image>().color = style.Inset;
        thumbGlyph = Label("Glyph", frame, 0f, 0f, ThumbWidth, ThumbHeight, 34f, style.Accent, TextAlignmentOptions.Center);
        thumb = At("Thumb", frame, 0f, 0f, ThumbWidth, ThumbHeight).gameObject.AddComponent<Image>();
        thumb.preserveAspect = true;
        thumb.raycastTarget = false;

        float textX = ThumbWidth + 22f;
        float textWidth = CardWidth - textX - 10f;
        heading = Label("Heading", card, textX, 10f, textWidth, 20f, 13f, style.Accent);
        heading.characterSpacing = 4f;
        title = Label("Name", card, textX, 30f, textWidth, 30f, 20f, style.Text);
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

        card.gameObject.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (card == null)
        {
            return;
        }

        // Out of the way while a window is up (the tutorial itself, the bag, a station...), back when it closes.
        bool wanted = Tutorials.Pending.Count > 0 && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen);
        if (wanted != card.gameObject.activeSelf)
        {
            card.gameObject.SetActive(wanted);
            if (wanted)
            {
                UIMotion.Pop(card, 0.8f, 0.3f);
            }
        }

        if (wanted)
        {
            Color accent = UIStyle.Current.Accent;
            rim.effectColor = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.35f, 1f, UIMotion.Breath(0.6f)));
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
        hint.text = GameInput.WithRebinds("Press (T) or click to read");
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
}
