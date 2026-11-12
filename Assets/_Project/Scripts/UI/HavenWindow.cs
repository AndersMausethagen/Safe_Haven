using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// Where Safe Haven is run from (H, or asking the portal). The Overview tab has its rank and the climb ahead as a ladder of
/// stops, the numbers at a glance, then what to do next, what the renown is made of and who of the expedition is home. Stores,
/// Buildings and People are HavenListPages: everything in camp, every building and every person, each with what can be done about
/// it from here, including going straight there. Built in code by HavenProgress, so it exists at home only; everything is read
/// again every second while it is open.
/// </summary>
public sealed class HavenWindow : MonoBehaviour
{
    private const float Width = 1240f;
    private const float TabBarHeight = 56f;
    private const float TabWidth = 176f;
    private const float Height = 776f + TabBarHeight;
    private const float Margin = 44f;
    private const float ColumnsTop = 314f;
    private const float ColumnsBottom = 680f;
    private const float PageHeight = ColumnsBottom + 10f;
    private const float StepHeight = 64f;
    private const float PartHeight = 34f;
    private const float PersonHeight = 46f;
    private const int MostSteps = 5;
    private const int MostParts = 8;
    private const int MostPeople = 7;
    private const float ReadSeconds = 1f;
    private const float RiseSeconds = 0.16f;
    private const float RiseUnits = 12f;
    private const float ClimbSeconds = 0.7f;
    private const float ScreenMargin = 24f;

    private enum Tab
    {
        Overview,
        Stores,
        Buildings,
        People,
        Lands,
        Raids,
    }

    private static readonly Tab[] Tabs = { Tab.Overview, Tab.Stores, Tab.Buildings, Tab.People, Tab.Lands, Tab.Raids };
    private static Tab lastTab = Tab.Overview;

    private sealed class TabView
    {
        public Tab Tab;
        public TMP_Text Label;
        public Image Underline;
    }

    private sealed class StepRow
    {
        public GameObject Root;
        public GameObject KeyCap;
        public GameObject Bullet;
        public TMP_Text Key;
        public TMP_Text Title;
        public TMP_Text Detail;
        public TMP_Text Renown;
    }

    private sealed class PartRow
    {
        public GameObject Root;
        public TMP_Text Label;
        public TMP_Text Renown;
    }

    private sealed class PersonRow
    {
        public GameObject Root;
        public Image Mark;
        public TMP_Text Name;
        public TMP_Text State;
    }

    private sealed class Stop
    {
        public RectTransform Mark;
        public Image Fill;
        public TMP_Text Name;
        public TMP_Text Renown;
    }

    private UIWindow window;
    private RectTransform panel;
    private RectTransform overviewPage;
    private RawImage rankArt;
    private HavenListPage storesPage;
    private HavenListPage buildingsPage;
    private HavenListPage peoplePage;
    private HavenListPage raidsPage;
    private HavenListPage landsPage;
    private readonly List<TabView> tabViews = new List<TabView>();
    private Tab shown = Tab.Overview;
    private TMP_Text rankName;
    private TMP_Text rankLine;
    private TMP_Text renownValue;
    private TMP_Text wardValue;
    private TMP_Text clearedValue;
    private TMP_Text buildingsValue;
    private RectTransform climbFill;
    private TMP_Text nextLine;
    private TMP_Text noSteps;
    private TMP_Text noParts;
    private TMP_Text partsNote;
    private TMP_Text peopleHome;
    private TMP_Text buildLabel;
    private readonly List<Stop> stops = new List<Stop>();
    private readonly List<StepRow> steps = new List<StepRow>();
    private readonly List<PartRow> parts = new List<PartRow>();
    private readonly List<PersonRow> people = new List<PersonRow>();
    private float openedAt;
    private float nextRead;
    private float climb;

    public static HavenWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static HavenWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Haven Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<HavenWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    public void Open() => window.Open();

    private void OnEnable()
    {
        Current = this;
        PadTabs.Register(this, () => window != null && window.IsOpen, StepTab, () => tabViews.ConvertAll(view => view.Label.rectTransform));
    }

    // LB and RB on a controller (PadTabs).
    private void StepTab(int direction)
    {
        int at = tabViews.FindIndex(view => view.Tab == shown);
        if (tabViews.Count > 0)
        {
            Show(tabViews[PadTabs.Next(Mathf.Max(0, at), tabViews.Count, direction)].Tab);
        }
    }

    private void OnDisable()
    {
        PadTabs.Unregister(this);
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        float age = Time.unscaledTime - openedAt;
        float shown = Mathf.Clamp01(age / RiseSeconds);
        panel.anchoredPosition = new Vector2(0f, (1f - shown) * (1f - shown) * -RiseUnits);
        float climbed = Mathf.Clamp01(age / ClimbSeconds);
        climbFill.anchorMax = new Vector2(climb * (1f - (1f - climbed) * (1f - climbed)), 1f);
        if (Time.unscaledTime >= nextRead)
        {
            Read();
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            return;
        }

        // It takes the screen's centre, and the portal's menu is how the player often gets here.
        if (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen)
        {
            ActionMenuWindow.Current.Close();
        }

        openedAt = Time.unscaledTime;
        climbFill.anchorMax = new Vector2(0f, 1f);
        Show(lastTab);
    }

    private void Show(Tab tab)
    {
        shown = tab;
        lastTab = tab;
        overviewPage.gameObject.SetActive(tab == Tab.Overview);
        storesPage.SetVisible(tab == Tab.Stores);
        buildingsPage.SetVisible(tab == Tab.Buildings);
        peoplePage.SetVisible(tab == Tab.People);
        raidsPage.SetVisible(tab == Tab.Raids);
        landsPage.SetVisible(tab == Tab.Lands);
        landsPage.ScrollToTop();
        storesPage.ScrollToTop();
        buildingsPage.ScrollToTop();
        peoplePage.ScrollToTop();
        Read();
    }

    private void RunAction(HavenAction action)
    {
        if (action.ClosesWindow)
        {
            window.Close();
        }

        action.Run();
        if (window.IsOpen)
        {
            Read();
        }
    }

    // At the largest UI and Text Size setting the screen is 720 units tall, less than the window.
    private void FitTheScreen()
    {
        Rect screen = ((RectTransform)transform).rect;
        float fit = Mathf.Min((screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        panel.localScale = Vector3.one * Mathf.Min(1f, fit);
    }

    private void Read()
    {
        nextRead = Time.unscaledTime + ReadSeconds;
        FitTheScreen();
        GameObject user = Player.Current != null ? Player.Current.gameObject : null;
        HavenOverview overview = HavenOverview.Read(user);
        climb = overview.Climb;
        if (shown == Tab.Overview)
        {
            DrawStanding(overview);
            DrawSteps(overview);
            DrawParts(overview);
            DrawPeople(overview);
        }

        // Every list is read, not only the one shown: the tabs say which of them wants the player.
        List<HavenShelf> stores = HavenStores.Read();
        List<HavenShelf> buildings = HavenBuildings.Read(user != null ? user.GetComponent<PlayerCrafting>() : null, user != null ? MaterialPool.For(user) : null,
            Resources.Load<ContentDatabase>(ContentDatabase.ResourceName), user != null ? user.GetComponent<BuildPlacer>() : null);
        List<HavenShelf> residents = HavenPeople.Read();
        List<HavenShelf> lands = HavenLands.Read();
        DrawTabs(buildings, residents, lands);
        switch (shown)
        {
            case Tab.Stores:
                storesPage.Draw(stores, StoresSummary(stores));
                break;
            case Tab.Buildings:
                buildingsPage.Draw(buildings, BuildingsSummary(buildings));
                break;
            case Tab.People:
                peoplePage.Draw(residents, PeopleSummary(residents));
                break;
            case Tab.Raids:
                raidsPage.Draw(HavenReadiness.Read(), "What the base does for you out there.");
                break;
            case Tab.Lands:
                landsPage.Draw(lands, HavenLands.Summary());
                break;
        }

        buildLabel.text = $"Build  ({GameInput.KeyLabel(GameInput.Build)})";
    }

    private void DrawTabs(List<HavenShelf> buildings, List<HavenShelf> residents, List<HavenShelf> lands)
    {
        UIStyle style = UIStyle.Current;
        foreach (TabView view in tabViews)
        {
            bool on = view.Tab == shown;
            int wanted = view.Tab == Tab.Buildings ? WantingAttention(buildings) : view.Tab == Tab.People ? WantingAttention(residents)
                : view.Tab == Tab.Lands ? WantingAttention(lands) : 0;
            view.Label.text = wanted > 0 ? $"{view.Tab}  <color={UIStyle.Hex(style.Warning)}>{wanted}</color>" : view.Tab.ToString();
            view.Label.color = on ? style.Accent : style.Text;
            view.Underline.enabled = on;
        }
    }

    private static int WantingAttention(List<HavenShelf> shelves)
    {
        int wanted = 0;
        foreach (HavenShelf shelf in shelves)
        {
            foreach (HavenEntry entry in shelf.Entries)
            {
                if (entry.State == HavenEntryState.Attention)
                {
                    wanted++;
                }
            }
        }

        return wanted;
    }

    private static int EntryCount(List<HavenShelf> shelves)
    {
        int count = 0;
        foreach (HavenShelf shelf in shelves)
        {
            count += shelf.Entries.Count;
        }

        return count;
    }

    private static int ShelfCount(List<HavenShelf> shelves, string title)
    {
        HavenShelf shelf = shelves.Find(one => one.Title == title);
        return shelf != null ? shelf.Entries.Count : 0;
    }

    private static string StoresSummary(List<HavenShelf> stores)
    {
        if (StashChest.SlotsStanding() < 0)
        {
            return "No chest stands yet: without one the stash can't be opened.";
        }

        ItemContainer stash = GameSession.Stash.Items;
        int used = 0;
        for (int i = 0; i < stash.UnlockedSlots; i++)
        {
            if (!stash.GetSlot(i).IsEmpty)
            {
                used++;
            }
        }

        return $"{EntryCount(stores)} kinds in camp. Stash {used} of {stash.UnlockedSlots} slots. What you carry is in your bag.";
    }

    private static string BuildingsSummary(List<HavenShelf> buildings) =>
        $"{ShelfCount(buildings, HavenBuildings.StandingShelf)} standing, {ShelfCount(buildings, HavenBuildings.ReadyShelf)} ready to build, {ShelfCount(buildings, HavenBuildings.LockedShelf)} to unlock.";

    private static string PeopleSummary(List<HavenShelf> residents) =>
        $"{ShelfCount(residents, HavenPeople.HomeShelf)} home, {ShelfCount(residents, HavenPeople.AwayShelf)} still out there.";

    private void DrawStanding(HavenOverview overview)
    {
        UIStyle style = UIStyle.Current;
        IReadOnlyList<HavenSettings.Rank> ranks = HavenSettings.Current.Ranks;
        bool last = overview.Rank >= ranks.Count - 1;
        wardValue.text = $"{overview.WardRadius:0}<size=60%> m";
        clearedValue.text = $"{overview.ClearedShare * 100f:0}<size=60%>%";
        buildingsValue.text = overview.Buildings.ToString();
        if (ranks.Count == 0)
        {
            return;
        }

        HavenSettings.Rank now = ranks[overview.Rank];
        rankName.text = now.name;
        ShowRankArt(now.picture);
        rankLine.text = now.line;
        renownValue.text = last ? overview.Renown.ToString() : $"{overview.Renown}<size=60%> / {ranks[overview.Rank + 1].renown}";
        nextLine.text = last ? "Safe Haven has reached its highest rank." : NextRank(now, ranks[overview.Rank + 1], overview.Renown);
        for (int i = 0; i < stops.Count; i++)
        {
            Stop stop = stops[i];
            bool reached = i <= overview.Rank;
            bool here = i == overview.Rank;
            float size = here ? 22f : 14f;
            stop.Mark.sizeDelta = new Vector2(size, size);
            stop.Fill.color = reached ? style.Accent : style.Inset;
            stop.Name.color = here ? style.Accent : reached ? style.Text : style.Muted;
        }
    }

    private static string NextRank(HavenSettings.Rank now, HavenSettings.Rank next, int renown)
    {
        string ward = next.wardBonus > now.wardBonus ? $" The ward reaches {next.wardBonus - now.wardBonus:0} m further." : "";
        return $"{Mathf.Max(0, next.renown - renown)} more renown to {next.name}.{ward}";
    }

    private void DrawSteps(HavenOverview overview)
    {
        noSteps.gameObject.SetActive(overview.Steps.Count == 0);
        for (int i = 0; i < steps.Count; i++)
        {
            StepRow row = steps[i];
            bool used = i < overview.Steps.Count;
            row.Root.SetActive(used);
            if (!used)
            {
                continue;
            }

            HavenOverview.Step step = overview.Steps[i];
            bool hasKey = !string.IsNullOrEmpty(step.Key);
            row.KeyCap.SetActive(hasKey);
            row.Bullet.SetActive(!hasKey);
            row.Key.text = step.Key;
            row.Title.text = step.Title;
            row.Detail.text = step.Detail;
            row.Renown.text = step.Renown > 0 ? $"+{step.Renown}" : "";
        }
    }

    private void DrawParts(HavenOverview overview)
    {
        noParts.gameObject.SetActive(overview.Parts.Count == 0);
        for (int i = 0; i < parts.Count; i++)
        {
            PartRow row = parts[i];
            bool used = i < overview.Parts.Count;
            row.Root.SetActive(used);
            if (!used)
            {
                continue;
            }

            // The last row sums up what doesn't fit, so the column always adds up to the total.
            int hidden = overview.Parts.Count - parts.Count;
            bool rest = i == parts.Count - 1 && hidden > 0;
            int restRenown = 0;
            for (int j = i; rest && j < overview.Parts.Count; j++)
            {
                restRenown += overview.Parts[j].Renown;
            }

            row.Label.text = rest ? $"And {hidden + 1} more" : overview.Parts[i].Label;
            row.Renown.text = $"+{(rest ? restRenown : overview.Parts[i].Renown)}";
        }

        partsNote.text = $"A second of a kind counts {HavenSettings.Current.RepeatShare * 100f:0}%: variety is worth more.";
    }

    private void DrawPeople(HavenOverview overview)
    {
        UIStyle style = UIStyle.Current;
        peopleHome.text = $"{overview.PeopleHome} of {overview.Expedition.Count} home";
        for (int i = 0; i < people.Count; i++)
        {
            PersonRow row = people[i];
            bool used = i < overview.Expedition.Count;
            row.Root.SetActive(used);
            if (!used)
            {
                continue;
            }

            HavenOverview.Person person = overview.Expedition[i];
            row.Mark.color = person.Home ? style.Good : style.Inset;
            row.Name.text = person.Name;
            row.Name.color = person.Home ? style.Text : style.Muted;
            row.State.text = !person.Home ? "Still out there" : string.IsNullOrEmpty(person.Job) ? "Home" : person.Job;
            row.State.color = person.Home ? style.Good : style.Muted;
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

        BuildTabs(style);
        RectTransform body = At("Body", panel, 0f, TabBarHeight, Width, Height - TabBarHeight);
        overviewPage = At("Overview", body, 0f, 0f, Width, PageHeight);
        BuildHeader(style);
        BuildLadder(style);
        Hairline(overviewPage, Margin, 296f, Width - Margin * 2f, 1f, style);
        BuildSteps(style);
        Hairline(overviewPage, 624f, ColumnsTop, 1f, ColumnsBottom - ColumnsTop, style);
        BuildParts(style);
        Hairline(overviewPage, 954f, ColumnsTop, 1f, ColumnsBottom - ColumnsTop, style);
        BuildPeople(style);
        storesPage = new HavenListPage(body, "Stores", Width, PageHeight, 2, "Nothing is stored in camp yet. Build a chest and bring something home.", RunAction);
        buildingsPage = new HavenListPage(body, "Buildings", Width, PageHeight, 2, "Nothing to show yet.", RunAction);
        peoplePage = new HavenListPage(body, "People", Width, PageHeight, 1, "Nobody has come home yet.", RunAction, detailIconSize: 150f);
        raidsPage = new HavenListPage(body, "Raids", Width, PageHeight, 1, "", RunAction);
        landsPage = new HavenListPage(body, "Lands", Width, PageHeight, 1, "No regions are known yet.", RunAction);
        BuildFooter(body, style);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Haven, content.gameObject);
        window.OpenChanged += OnOpenChanged;
    }

    private void BuildTabs(UIStyle style)
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            Tab tab = Tabs[i];
            Button button = AddTab(tab.ToString(), panel, Margin + i * (TabWidth + 8f), 8f, TabWidth, 44f, style, out TMP_Text label, out Image underline);
            button.onClick.AddListener(() => Show(tab));
            tabViews.Add(new TabView { Tab = tab, Label = label, Underline = underline });
        }

        Hairline(panel, Margin, TabBarHeight, Width - Margin * 2f, 1f, style);
    }

    private const float HeaderArtHeight = 168f;

    private void BuildHeader(UIStyle style)
    {
        // The camp as it stands at this rank, behind the header: solid under the name, darkened under the figures.
        rankArt = At("Rank Art", overviewPage, 0f, 0f, Width, HeaderArtHeight).gameObject.AddComponent<RawImage>();
        rankArt.raycastTarget = false;
        rankArt.enabled = false;
        var fade = At("Rank Art Fade", overviewPage, 0f, 0f, Width, HeaderArtHeight).gameObject.AddComponent<RawImage>();
        fade.texture = HeaderFade();
        fade.color = style.Panel;
        fade.raycastTarget = false;

        TMP_Text kicker = Label("Kicker", overviewPage, Margin, 34f, 500f, 24f, 17f, style.Accent);
        kicker.text = "SAFE HAVEN";
        kicker.characterSpacing = 12f;
        rankName = Label("Rank", overviewPage, Margin, 58f, 620f, 62f, 46f, style.Text);
        style.Title(rankName);
        rankLine = Label("Line", overviewPage, Margin, 122f, 620f, 32f, 21f, style.Muted);
        style.Title(rankLine);
        rankLine.color = style.Muted;

        renownValue = Figure("Renown", 664f, 148f, style.Accent, style);
        wardValue = Figure("Ward", 812f, 124f, style.Text, style);
        clearedValue = Figure("Cleared", 936f, 124f, style.Text, style);
        buildingsValue = Figure("Buildings", 1060f, 136f, style.Text, style);
    }

    // Alpha only, tinted the panel's colour: solid at the left under the rank's name, never clearer than half on the right
    // (the figures stand on it), solid again along the bottom.
    private static Texture2D HeaderFade()
    {
        const int width = 128;
        const int height = 32;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Haven Header Fade" };
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.InverseLerp(0.62f, 0.3f, x / (width - 1f));
                float down = Mathf.InverseLerp(0.35f, 0f, y / (height - 1f));
                float alpha = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.Max(across, down)), 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    // The middle band of the painting that fits the header's shape.
    private void ShowRankArt(Sprite art)
    {
        rankArt.enabled = art != null;
        if (art == null)
        {
            return;
        }

        Rect rect = art.textureRect;
        Texture texture = art.texture;
        float band = Mathf.Min(1f, HeaderArtHeight / Width * rect.width / rect.height);
        rankArt.texture = texture;
        rankArt.uvRect = new Rect(rect.x / texture.width, (rect.y + rect.height * (0.5f - band * 0.5f)) / texture.height,
            rect.width / texture.width, rect.height * band / texture.height);
    }

    private TMP_Text Figure(string label, float x, float width, Color colour, UIStyle style)
    {
        Hairline(overviewPage, x - 20f, 50f, 1f, 76f, style);
        TMP_Text value = Label(label + " Value", overviewPage, x, 44f, width - 20f, 50f, 36f, colour);
        Label(label + " Label", overviewPage, x, 96f, width - 20f, 24f, 17f, style.Muted).text = label;
        return value;
    }

    private void BuildLadder(UIStyle style)
    {
        const float top = 180f;
        const float inset = 92f;
        float left = Margin + inset;
        float width = Width - (Margin + inset) * 2f;
        RectTransform track = At("Climb", overviewPage, left, top - 1f, width, 3f);
        track.gameObject.AddComponent<Image>().color = style.ControlLit;
        climbFill = Box("Fill", track, Vector2.zero, Vector2.zero, Vector2.zero);
        climbFill.anchorMax = new Vector2(0f, 1f);
        climbFill.offsetMin = climbFill.offsetMax = Vector2.zero;
        climbFill.gameObject.AddComponent<Image>().color = style.Accent;

        IReadOnlyList<HavenSettings.Rank> ranks = HavenSettings.Current.Ranks;
        for (int i = 0; i < ranks.Count; i++)
        {
            float x = left + (ranks.Count > 1 ? width * i / (ranks.Count - 1) : 0f);
            var stop = new Stop { Mark = Box(ranks[i].name, overviewPage, new Vector2(0f, 1f), Vector2.one * 14f, new Vector2(x, -top)) };
            stop.Mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            stop.Mark.gameObject.AddComponent<Image>().color = style.ControlLit;
            RectTransform fill = Stretch("Fill", stop.Mark);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            stop.Fill = fill.gameObject.AddComponent<Image>();
            stop.Name = Label("Name", overviewPage, x - inset, top + 18f, inset * 2f, 28f, 19f, style.Muted, TextAlignmentOptions.Center);
            stop.Name.text = ranks[i].name;
            stop.Renown = Label("Renown", overviewPage, x - inset, top + 44f, inset * 2f, 24f, 17f, style.Muted, TextAlignmentOptions.Center);
            stop.Renown.text = ranks[i].renown.ToString();
            stops.Add(stop);
        }

        nextLine = Label("Next", overviewPage, Margin, 252f, Width - Margin * 2f, 30f, 20f, style.Text, TextAlignmentOptions.Center);
    }

    private void BuildSteps(UIStyle style)
    {
        const float x = Margin;
        const float width = 560f;
        Heading("What next", overviewPage, x, ColumnsTop, width, style);
        noSteps = Label("Nothing", overviewPage, x, ColumnsTop + 40f, width, 30f, 20f, style.Muted);
        noSteps.text = "Nothing pressing. Safe Haven can breathe.";
        for (int i = 0; i < MostSteps; i++)
        {
            var row = new StepRow { Root = At($"Step {i}", overviewPage, x, ColumnsTop + 36f + i * StepHeight, width, StepHeight).gameObject };
            Transform parent = row.Root.transform;
            RectTransform cap = At("Key", parent, 0f, 10f, 40f, 40f);
            cap.gameObject.AddComponent<Image>();
            cap.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            cap.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
            row.KeyCap = cap.gameObject;
            row.Key = Label("Label", cap, 0f, 0f, 40f, 40f, 18f, style.Text, TextAlignmentOptions.Center);
            row.Bullet = Box("Bullet", parent, new Vector2(0f, 1f), Vector2.one * 10f, new Vector2(20f, -30f)).gameObject;
            row.Bullet.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            row.Bullet.AddComponent<Image>().color = style.Accent;
            row.Title = Label("Title", parent, 56f, 4f, 440f, 30f, 21f, style.Text);
            row.Detail = Label("Detail", parent, 56f, 32f, 440f, 26f, 17f, style.Muted);
            row.Renown = Label("Renown", parent, 500f, 8f, 56f, 40f, 24f, style.Accent, TextAlignmentOptions.Right);
            steps.Add(row);
        }
    }

    private void BuildParts(UIStyle style)
    {
        const float x = 644f;
        const float width = 290f;
        Heading("Renown", overviewPage, x, ColumnsTop, width, style);
        noParts = Label("Nothing", overviewPage, x, ColumnsTop + 40f, width, 60f, 18f, style.Muted);
        noParts.textWrappingMode = TextWrappingModes.Normal;
        noParts.text = "Nothing yet. Every building and every cleared stump counts.";
        for (int i = 0; i < MostParts; i++)
        {
            var row = new PartRow { Root = At($"Part {i}", overviewPage, x, ColumnsTop + 36f + i * PartHeight, width, PartHeight).gameObject };
            row.Label = Label("Label", row.Root.transform, 0f, 0f, width - 50f, PartHeight, 19f, style.Text);
            row.Renown = Label("Renown", row.Root.transform, width - 50f, 0f, 50f, PartHeight, 20f, style.Accent, TextAlignmentOptions.Right);
            parts.Add(row);
        }

        partsNote = Label("Note", overviewPage, x, ColumnsBottom - 44f, width, 44f, 16f, style.Muted, TextAlignmentOptions.BottomLeft);
        partsNote.textWrappingMode = TextWrappingModes.Normal;
    }

    private void BuildPeople(UIStyle style)
    {
        const float x = 974f;
        const float width = 222f;
        Heading("Expedition", overviewPage, x, ColumnsTop, width, style);
        peopleHome = Label("Home", overviewPage, x, ColumnsTop, width, 26f, 17f, style.Muted, TextAlignmentOptions.Right);
        for (int i = 0; i < MostPeople; i++)
        {
            var row = new PersonRow { Root = At($"Person {i}", overviewPage, x, ColumnsTop + 36f + i * PersonHeight, width, PersonHeight).gameObject };
            RectTransform mark = Box("Mark", row.Root.transform, new Vector2(0f, 1f), Vector2.one * 11f, new Vector2(7f, -PersonHeight * 0.5f));
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            mark.gameObject.AddComponent<Image>().color = style.ControlLit;
            RectTransform fill = Stretch("Fill", mark);
            fill.offsetMin = new Vector2(1.5f, 1.5f);
            fill.offsetMax = new Vector2(-1.5f, -1.5f);
            row.Mark = fill.gameObject.AddComponent<Image>();
            row.Name = Label("Name", row.Root.transform, 24f, 0f, 90f, PersonHeight, 20f, style.Text);
            row.State = Label("State", row.Root.transform, 96f, 0f, width - 96f, PersonHeight, 17f, style.Muted, TextAlignmentOptions.Right);
            people.Add(row);
        }
    }

    private void BuildFooter(RectTransform body, UIStyle style)
    {
        Hairline(body, Margin, ColumnsBottom + 10f, Width - Margin * 2f, 1f, style);
        const float y = 708f;
        buildLabel = AddButton("Build", body, Width - Margin - 366f, y, 186f, UIRole.Kind.PrimaryButton, StartBuilding, out _);
        AddButton("Close", body, Width - Margin - 170f, y, 170f, UIRole.Kind.Button, () => window.Close(), out _).text = "Close";
    }

    // Most of what the window suggests starts in the build menu.
    private void StartBuilding()
    {
        window.Close();
        if (Player.Current != null && Player.Current.TryGetComponent(out BuildPlacer builder))
        {
            builder.ToggleMenu();
        }
    }
}
