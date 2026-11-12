using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// A tutorial: one page of the journal's Mechanics tab shown large, with its painting, what it is and how it works. Opened by the tutorial
/// key (T), by the notice that tutorials are waiting (TutorialNoticeHud) or from the journal. Opened with tutorials waiting it shows them all,
/// oldest first, and each one shown counts as read. Built in code for every scene with a player (ExperienceTracker); the pages are
/// JournalSettings' and their art comes from LocalSpawn (Tools/LocalSpawn/tutorial_art.json).
/// </summary>
public sealed class TutorialWindow : MonoBehaviour
{
    private const float Width = 1280f;
    private const float Height = 760f;
    private const float Margin = 40f;
    private const float ScreenMargin = 24f;
    private const float ArtWidth = 680f;
    private const float ArtHeight = ArtWidth * 9f / 16f;
    private const float ColumnX = Margin + ArtWidth + 40f;
    private const float ColumnWidth = Width - ColumnX - Margin;
    private const float Top = 108f;
    private const float FooterY = Height - 66f;

    private readonly List<JournalPage> pages = new List<JournalPage>();
    private int index;
    private UIWindow window;
    private RectTransform panel;
    private TMP_Text title;
    private TMP_Text group;
    private Image art;
    private TMP_Text artGlyph;
    private TMP_Text summary;
    private TMP_Text body;
    private TMP_Text counter;
    private Button previous;
    private Button next;
    private TMP_Text nextLabel;

    public static TutorialWindow Current { get; private set; }

    /// <summary>The page shown, while the window is open.</summary>
    public JournalPage Shown => window != null && window.IsOpen && index < pages.Count ? pages[index] : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static TutorialWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Tutorial Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<TutorialWindow>();
        Current = view;
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    /// <summary>The tutorial key: closes the window, or shows what waits, or else the last one that opened.</summary>
    public static void Toggle()
    {
        TutorialWindow view = Current != null ? Current : Create();
        if (view.window.IsOpen)
        {
            view.window.Close();
        }
        else if (Tutorials.Pending.Count > 0)
        {
            view.Open(Tutorials.Pending);
        }
        else if (Tutorials.Latest != null)
        {
            view.Open(new[] { Tutorials.Latest });
        }
        else
        {
            GameMessages.Post(GameInput.WithRebinds("Nothing new to learn just now. Everything you have learned is in the journal ({key:Journal}), under Mechanics."));
        }
    }

    /// <summary>Shows the tutorials waiting, oldest first.</summary>
    public static void ShowPending()
    {
        if (Tutorials.Pending.Count > 0)
        {
            (Current != null ? Current : Create()).Open(Tutorials.Pending);
        }
    }

    /// <summary>Shows one page, as from the journal.</summary>
    public static void Show(JournalPage page)
    {
        if (page != null)
        {
            (Current != null ? Current : Create()).Open(new[] { page });
        }
    }

    private void OnEnable()
    {
        Current = this;
        GameInput.KeysChanged += OnKeysChanged;
    }

    private void OnDisable()
    {
        GameInput.KeysChanged -= OnKeysChanged;
        if (Current == this)
        {
            Current = null;
        }
    }

    // A page names keys: picking up a controller while reading shows its buttons.
    private void OnKeysChanged()
    {
        if (window != null && window.IsOpen)
        {
            Draw();
        }
    }

    private void Open(IReadOnlyList<JournalPage> shown)
    {
        pages.Clear();
        pages.AddRange(shown);
        index = 0;
        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        window.Open();
        Draw();
        if (Application.isPlaying)
        {
            UIMotion.Pop(panel, 0.94f, 0.25f);
        }

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(next.gameObject);
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        group = Label("Group", panel, Margin, 16f, Width - Margin * 2f, 26f, 16f, style.Accent, TextAlignmentOptions.Center);
        group.characterSpacing = 6f;
        title = Label("Title", panel, Margin, 40f, Width - Margin * 2f, 54f, 38f, style.Text, TextAlignmentOptions.Center);
        style.Title(title);
        Hairline(panel, Margin, Top - 12f, Width - Margin * 2f, 1f, style);

        // The painting, in a frame of the inset colour so a page without one still has its place.
        RectTransform frame = At("Art Frame", panel, Margin, Top, ArtWidth, ArtHeight);
        frame.gameObject.AddComponent<Image>().color = style.Inset;
        frame.gameObject.AddComponent<Outline>().effectColor = style.Hairline;
        artGlyph = Label("Glyph", frame, 0f, 0f, ArtWidth, ArtHeight, 120f, style.Faint, TextAlignmentOptions.Center);
        art = At("Art", frame, 0f, 0f, ArtWidth, ArtHeight).gameObject.AddComponent<Image>();
        art.preserveAspect = true;
        art.raycastTarget = false;

        float underArt = Top + ArtHeight + 22f;
        Heading("What it is", panel, Margin, underArt, ArtWidth, style);
        summary = Paragraph("Summary", panel, Margin, underArt + 34f, ArtWidth, FooterY - underArt - 50f, 20f, style.Text);
        summary.richText = true;
        summary.enableAutoSizing = true;
        summary.fontSizeMin = 15f;
        summary.fontSizeMax = 20f;

        Heading("How it works", panel, ColumnX, Top, ColumnWidth, style);
        body = Paragraph("Body", panel, ColumnX, Top + 34f, ColumnWidth, FooterY - Top - 50f, 19f, style.Text);
        body.richText = true;
        body.enableAutoSizing = true;
        body.fontSizeMin = 14f;
        body.fontSizeMax = 19f;

        Hairline(panel, Margin, FooterY - 14f, Width - Margin * 2f, 1f, style);
        counter = Label("Counter", panel, Margin, FooterY, 300f, 46f, 18f, style.Muted);
        AddButton("Journal", panel, Width - Margin - 610f, FooterY, 190f, UIRole.Kind.Button, OpenJournal, out _).text = "In the Journal";
        AddButton("Previous", panel, Width - Margin - 410f, FooterY, 190f, UIRole.Kind.Button, () => Step(-1), out previous).text = "Previous";
        nextLabel = AddButton("Next", panel, Width - Margin - 210f, FooterY, 210f, UIRole.Kind.PrimaryButton, () => Step(1), out next);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Tutorial, content.gameObject);
    }

    private void Step(int by)
    {
        int to = index + by;
        if (to >= pages.Count)
        {
            window.Close();
            return;
        }

        index = Mathf.Clamp(to, 0, pages.Count - 1);
        Draw();
    }

    private void OpenJournal()
    {
        window.Close();
        if (UIStateManager.Current != null && !UIStateManager.Current.IsOpen(UIWindowId.Journal))
        {
            UIStateManager.Current.RequestToggle(UIWindowId.Journal);
        }
    }

    private void Draw()
    {
        if (pages.Count == 0)
        {
            return;
        }

        JournalPage page = pages[index];
        group.text = string.IsNullOrWhiteSpace(page.group) ? "" : page.group.ToUpperInvariant();
        title.text = page.title;
        art.sprite = page.art;
        art.enabled = page.art != null;
        artGlyph.text = page.art == null && !string.IsNullOrEmpty(page.title) ? page.title.Substring(0, 1) : "";
        summary.text = GameInput.WithRebinds(string.IsNullOrWhiteSpace(page.summary) ? page.line : page.summary);
        body.text = GameInput.WithRebinds(page.body);
        counter.text = pages.Count > 1 ? $"{index + 1} of {pages.Count}" : "";
        previous.gameObject.SetActive(pages.Count > 1);
        previous.interactable = index > 0;
        nextLabel.text = index < pages.Count - 1 ? "Next" : "Got It";
        // Seen is read: it no longer waits and the journal stops marking it.
        Tutorials.MarkSeen(page);
    }
}
