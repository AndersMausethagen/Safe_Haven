using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The journal (J, or from the Escape menu): the backstory (with the cinematics reached, to watch again), the quests in more depth than the tracker has room for, the creatures met, how things work, the
/// raids behind you and the Lost who hunt you (once one is met: until then its tab isn't there), one tab each. It reads and shows; JournalContent decides what there is to say. Built in code for every scene with a player
/// (ExperienceTracker).
/// </summary>
public sealed class JournalWindow : MonoBehaviour
{
    private enum Tab
    {
        Backstory,
        Quests,
        Creatures,
        Mechanics,
        Raids,
        Lost,
        Modules,
    }

    private const float Width = 1400f;
    private const float Height = 840f;
    private const float TabBarHeight = 56f;
    // Seven tabs and Close on one bar (Modules joined them, 2026-11-21).
    private const float TabWidth = 150f;
    private const float Margin = 44f;
    private const float ScreenMargin = 24f;
    // Long reading wants a wide column; a creature has a picture to show.
    private const float ReadingWidth = 800f;
    private const float PortraitSize = 190f;

    private static readonly Tab[] Tabs = { Tab.Backstory, Tab.Quests, Tab.Creatures, Tab.Mechanics, Tab.Modules, Tab.Raids, Tab.Lost };

    private sealed class TabView
    {
        public Tab Tab;
        public Button Button;
        public TMP_Text Label;
        public Image Underline;
        public HavenListPage Page;
    }

    private readonly List<TabView> tabViews = new List<TabView>();
    private UIWindow window;
    private RectTransform panel;
    private Tab shown = Tab.Backstory;

    public static JournalWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static JournalWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Journal Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<JournalWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    private void OnEnable()
    {
        Current = this;
        GameInput.KeysChanged += OnKeysChanged;
        PadTabs.Register(this, () => window != null && window.IsOpen, StepTab,
            () => tabViews.FindAll(view => IsOffered(view.Tab)).ConvertAll(view => (RectTransform)view.Button.transform));
    }

    // LB and RB on a controller (PadTabs), over the tabs there are.
    private void StepTab(int direction)
    {
        List<TabView> offered = tabViews.FindAll(view => IsOffered(view.Tab));
        int at = offered.FindIndex(view => view.Tab == shown);
        if (offered.Count > 0)
        {
            Show(offered[PadTabs.Next(Mathf.Max(0, at), offered.Count, direction)].Tab);
        }
    }

    // The Lost's tab waits for the first of them met, so it gives nothing away before.
    private static bool IsOffered(Tab tab) => tab != Tab.Lost || GameSession.Progress.LostOnes.AnyMet;

    private static string TabName(Tab tab) => tab == Tab.Lost ? "The Lost" : tab.ToString();

    private void OnDisable()
    {
        GameInput.KeysChanged -= OnKeysChanged;
        PadTabs.Unregister(this);
        if (Current == this)
        {
            Current = null;
        }
    }

    // Pages name keys: picking up a controller while reading shows its buttons.
    private void OnKeysChanged()
    {
        if (window != null && window.IsOpen)
        {
            Show(shown);
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

        RectTransform body = At("Body", panel, 0f, TabBarHeight, Width, Height - TabBarHeight);
        float height = Height - TabBarHeight;
        for (int i = 0; i < Tabs.Length; i++)
        {
            Tab tab = Tabs[i];
            Button button = AddTab(TabName(tab), panel, Margin + i * (TabWidth + 8f), 8f, TabWidth, 44f, style, out TMP_Text label, out Image underline);
            button.onClick.AddListener(() => Show(tab));
            // Creatures have a portrait; the backstory and mechanics pages a painting, wide, with the page's name beside it.
            bool painted = tab == Tab.Backstory || tab == Tab.Mechanics;
            var page = new HavenListPage(body, tab.ToString(), Width, height, 1, EmptyText(tab), Run, ReadingWidth,
                tab == Tab.Creatures || tab == Tab.Modules || painted ? PortraitSize : 84f, painted ? 16f / 9f : 1f);
            page.Chosen += OnChosen;
            tabViews.Add(new TabView { Tab = tab, Button = button, Label = label, Underline = underline, Page = page });
        }

        AddButton("Close", panel, Width - Margin - 160f, 6f, 160f, UIRole.Kind.Button, () => window.Close(), out _).text = "Close";
        Hairline(panel, Margin, TabBarHeight, Width - Margin * 2f, 1f, style);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Journal, content.gameObject);
        window.OpenChanged += OnOpenChanged;
    }

    private static string EmptyText(Tab tab)
    {
        switch (tab)
        {
            case Tab.Quests:
                return "No quests yet. Someone will want something soon enough.";
            case Tab.Raids:
                return "You haven't been through the portal yet. Every raid you go on is written down here.";
            case Tab.Lost:
                return "None of the Lost has found you yet.";
            default:
                return "Nothing written yet.";
        }
    }

    private void Run(HavenAction action)
    {
        action.Run?.Invoke();
        if (action.ClosesWindow)
        {
            window.Close();
        }
        else
        {
            Show(shown);
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            return;
        }

        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        Show(shown);
        // Something for the stick to start from: a mouse needs no such thing, and a highlighted tab would only confuse it.
        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(tabViews.Find(view => view.Tab == shown).Button.gameObject);
        }
    }

    private void Show(Tab tab)
    {
        shown = IsOffered(tab) ? tab : Tab.Backstory;
        tab = shown;
        UIStyle style = UIStyle.Current;
        foreach (TabView view in tabViews)
        {
            view.Button.gameObject.SetActive(IsOffered(view.Tab));
            bool on = view.Tab == tab;
            // Framed tabs (CodeUI.AddTab): the one shown in bright gold, bold, in its gold rim.
            view.Label.color = on ? style.AccentBright : style.Text;
            view.Label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            view.Underline.enabled = on;
            view.Page.SetVisible(on);
        }

        Draw(tabViews.Find(view => view.Tab == tab));
        DrawTabLabels();
    }

    private void Draw(TabView view)
    {
        view.Page.Draw(Shelves(view.Tab, out string summary), summary);
    }

    private static List<HavenShelf> Shelves(Tab tab, out string summary)
    {
        JournalSettings settings = JournalSettings.Current;
        QuestLog log = GameSession.Quests;
        CharacterProgress progress = GameSession.Progress;
        switch (tab)
        {
            case Tab.Quests:
                return JournalContent.Quests(log, progress, QuestManager.Current != null ? QuestManager.Current.Quests : null, Pin, out summary);
            case Tab.Creatures:
                return JournalContent.Creatures(settings, progress, out summary);
            case Tab.Mechanics:
                return JournalContent.Mechanics(settings, log, progress, out summary);
            case Tab.Raids:
                return JournalContent.Raids(progress, out summary);
            case Tab.Lost:
                return LostOneJournal.Shelves(LostOneSettings.Current, progress, out summary);
            case Tab.Modules:
                return ModuleJournal.Shelves(out summary);
            default:
                return JournalContent.Backstory(settings, log, progress, out summary, Watch, WatchBlocked());
        }
    }

    // The journal closes first (the action does), then the cinematic plays over the game, which it holds still.
    private static void Watch(CinematicDefinition cinematic) => CinematicScreen.Play(cinematic);

    // A cinematic pauses this game, which in co-op stops nobody else.
    private static string WatchBlocked() =>
        CoopSession.Current != null && CoopSession.Current.IsActive ? "not in co-op" : null;

    private static void Pin(QuestDefinition quest)
    {
        GameSession.Progress.Journal.PinnedQuest = quest != null ? quest.Id : null;
        QuestManager.NotifyChanged();
    }

    private void OnChosen(HavenEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ReadKey) || GameSession.Progress.Journal.HasRead(entry.ReadKey))
        {
            return;
        }

        GameSession.Progress.Journal.MarkRead(entry.ReadKey);
        entry.Badge = "";
        if (entry.State == HavenEntryState.Ready)
        {
            entry.State = HavenEntryState.Good;
        }

        DrawTabLabels();
    }

    // A count beside a tab with anything still to read.
    private void DrawTabLabels()
    {
        string accent = UIStyle.Hex(UIStyle.Current.Accent);
        foreach (TabView view in tabViews)
        {
            int unread = 0;
            foreach (HavenShelf shelf in Shelves(view.Tab, out _))
            {
                foreach (HavenEntry entry in shelf.Entries)
                {
                    if (entry.Badge == JournalContent.UnreadMark)
                    {
                        unread++;
                    }
                }
            }

            view.Label.text = unread > 0 ? $"{TabName(view.Tab)}  <color={accent}>{unread}</color>" : TabName(view.Tab);
        }
    }
}
