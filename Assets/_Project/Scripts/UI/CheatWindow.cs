using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The cheat menu (F1), for playtesting: tabs for the player, items, raids, creatures and the world, over the calls in Cheats. It
/// pauses the game while open, so a raid's clock stands still while something is looked up. Built in code the first time it is
/// asked for in a scene, and gone with the scene. It only draws and sends requests; what is on lives in Cheats.
/// </summary>
public sealed class CheatWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 780f;
    private const float Margin = 36f;
    private const float TabHeight = 56f;
    private const float PageTop = 72f;
    private const float PageHeight = Height - PageTop - 92f;
    private const float ButtonGap = 10f;
    private const float RowStep = 56f;
    private const float ReadSeconds = 0.5f;
    private const float ScreenMargin = 24f;
    private const float RaidMinutes = 300f;
    private const float EndClockSeconds = 10f;
    private const float SpawnDistance = 7f;

    private static readonly string[] TabNames = { "Player", "Items", "Raid", "Spawn", "World" };
    private static readonly int[] Amounts = { 1, 10, 50, 99 };
    private static readonly int[] Counts = { 1, 3, 5 };
    private static readonly (string Name, float Hour)[] Hours = { ("Dawn", 6f), ("Noon", 12f), ("Dusk", 19.2f), ("Night", 23f) };
    private static int lastTab;
    private static int amount = 10;
    private static int spawnCount = 1;
    private static bool toStash;
    // What the next raid started here is like: a mood by name (null: rolled), a shape (null: rolled), a size (null: the mood's own).
    private static string raidMood;
    private static RaidLayoutShape? raidShape;
    private static bool? raidLarge;
    private static readonly string[] ShapeNames = { "Hub", "Ring", "Spine", "Thicket" };

    private sealed class Flip
    {
        public UIRole Role;
        public TMP_Text Label;
        public string Name;
        public bool ShowsState;
        public Func<bool> IsOn;
    }

    private sealed class ItemRow
    {
        public GameObject Root;
        public string Name;
        public int Group;
    }

    private readonly List<GameObject> pages = new List<GameObject>();
    private readonly List<(TMP_Text Label, Image Underline)> tabs = new List<(TMP_Text, Image)>();
    private readonly List<Flip> flips = new List<Flip>();
    private readonly List<ItemRow> itemRows = new List<ItemRow>();
    private readonly List<TMP_Text> groupLabels = new List<TMP_Text>();
    private UIWindow window;
    private RectTransform panel;
    private TMP_Text status;
    private TMP_Text raidState;
    private TMP_InputField search;
    private RectTransform itemContent;
    private int itemGroup = -1;
    private float nextRead;

    public static CheatWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        amount = 10;
        lastTab = default;
        raidLarge = null;
        raidMood = null;
        raidShape = null;
        spawnCount = 1;
        toStash = false;
    }

    /// <summary>Opens the menu, or closes it. Does nothing where there is no player, or while the Escape menu has the game paused.</summary>
    public static void Toggle()
    {
        if (Current != null && Current.window.IsOpen)
        {
            Current.window.Close();
            return;
        }

        if (Player.Current == null || UIStateManager.Current == null || GamePause.IsPaused)
        {
            return;
        }

        (Current != null ? Current : Create()).window.Open();
    }

    private static CheatWindow Create()
    {
        var root = new GameObject("Cheat Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<CheatWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        GamePause.Release(this);
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (window.IsOpen && Time.unscaledTime >= nextRead)
        {
            Read();
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (open)
        {
            GamePause.Request(this);
            Show(lastTab);
        }
        else
        {
            GamePause.Release(this);
        }
    }

    private void Show(int tab)
    {
        lastTab = tab;
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].SetActive(i == tab);
            tabs[i].Label.color = i == tab ? UIStyle.Current.Accent : UIStyle.Current.Text;
            tabs[i].Underline.enabled = i == tab;
        }

        Read();
    }

    private void Read()
    {
        nextRead = Time.unscaledTime + ReadSeconds;
        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        foreach (Flip flip in flips)
        {
            bool on = flip.IsOn();
            flip.Label.text = flip.ShowsState ? $"{flip.Name}:  {(on ? "On" : "Off")}" : flip.Name;
            flip.Role.Set(on ? UIRole.Kind.PrimaryButton : UIRole.Kind.Button);
            flip.Label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }

        RaidDirector director = RaidDirector.Current;
        raidState.text = director != null && director.IsRunning
            ? $"In {director.Session.Definition.DisplayName}{RunKind(director.Session)}: {RaidStats.FormatTime(director.Session.TimeRemaining)} left."
            : "No raid is running.";
    }

    private void Say(string words)
    {
        status.text = words;
        Read();
    }

    private void Close() => window.Close();

    // ----- Building it -----

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        for (int i = 0; i < TabNames.Length; i++)
        {
            int tab = i;
            Button button = AddTab(TabNames[i], panel, Margin + i * 178f, 8f, 170f, 44f, style, out TMP_Text label, out Image underline);
            label.text = TabNames[i];
            button.onClick.AddListener(() => Show(tab));
            tabs.Add((label, underline));
            pages.Add(At(TabNames[i] + " Page", panel, 0f, PageTop, Width, PageHeight).gameObject);
        }

        Hairline(panel, Margin, TabHeight, Width - Margin * 2f, 1f, style);
        BuildPlayer(pages[0].transform, style);
        BuildItems(pages[1].transform, style);
        BuildRaid(pages[2].transform, style);
        BuildSpawn(pages[3].transform, style);
        BuildWorld(pages[4].transform, style);

        Hairline(panel, Margin, Height - 80f, Width - Margin * 2f, 1f, style);
        status = Label("Status", panel, Margin, Height - 66f, Width - Margin * 2f - 190f, 46f, 19f, style.Text);
        status.text = "F1 closes this. The game is paused while it is open.";
        AddButton("Close", panel, Width - Margin - 170f, Height - 66f, 170f, UIRole.Kind.Button, Close, out _).text = "Close";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Cheats, content.gameObject);
        window.OpenChanged += OnOpenChanged;
    }

    private static TMP_Text Title(string text, Transform page, float y, UIStyle style) => Heading(text, page, Margin, y, Width - Margin * 2f, style);

    private void Action(Transform page, string text, float x, float y, float width, Func<string> run, bool closes = false)
    {
        AddButton(text, page, x, y, width, UIRole.Kind.Button, () =>
        {
            Say(run());
            if (closes)
            {
                Close();
            }
        }, out _).text = text;
    }

    private void Switch(Transform page, string name, float x, float y, float width, Func<bool> isOn, Action<bool> set, bool showsState = true)
    {
        TMP_Text label = AddButton(name, page, x, y, width, UIRole.Kind.Button, () =>
        {
            set(!isOn());
            if (showsState)
            {
                status.text = $"{name}: {(isOn() ? "on" : "off")}.";
            }

            Read();
        }, out Button button);
        flips.Add(new Flip { Role = button.GetComponent<UIRole>(), Label = label, Name = name, ShowsState = showsState, IsOn = isOn });
    }

    private void BuildPlayer(Transform page, UIStyle style)
    {
        const float wide = 544f;
        float right = Margin + wide + ButtonGap * 2f;
        Title("Switches", page, 8f, style);
        float y = 40f;
        Switch(page, "Invincible", Margin, y, wide, () => Cheats.Invincible, Cheats.SetInvincible);
        Switch(page, "One-hit kills", right, y, wide, () => Cheats.OneHitKills, Cheats.SetOneHitKills);
        y += RowStep;
        Switch(page, "Endless stamina", Margin, y, wide, () => Cheats.EndlessStamina, Cheats.SetEndlessStamina);
        Switch(page, "No hunger or thirst", right, y, wide, () => Cheats.NoNeeds, Cheats.SetNoNeeds);
        y += RowStep;
        Switch(page, "Move fast", Margin, y, wide, () => Cheats.Fast, Cheats.SetFast);
        y += RowStep;
        Switch(page, "Free building and crafting", Margin, y, wide, () => Cheats.FreeCosts, Cheats.SetFreeCosts);
        Switch(page, "No building or tier requirements", right, y, wide, () => Cheats.NoRequirements, Cheats.SetNoRequirements);
        y += RowStep + 8f;

        Title("Now", page, y, style);
        y += 32f;
        Action(page, "Heal and fill everything", Margin, y, wide, Cheats.Heal);
        Action(page, "Learn every recipe and building", right, y, wide, Cheats.UnlockEverything);
        y += RowStep;
        Action(page, "500 Insight", Margin, y, wide, () => Cheats.GrantExperience(500));
        Action(page, "50 of every essence", right, y, wide, () => Cheats.GiveEssences(50));
        y += RowStep;
        Action(page, "A wild horse nearby", Margin, y, wide, Cheats.WildHorse);
        Action(page, "Lasso, barley and a saddle", right, y, wide, Cheats.HorseKit);
        y += RowStep;
        Action(page, "The bog's toxic horse nearby", Margin, y, wide, Cheats.ToxicHorse);
        Action(page, "Your horses: full bond", right, y, wide, Cheats.BondHorses);
        y += RowStep + 8f;

        Title("99 of every material, to the stash", page, y, style);
        y += 32f;
        float tierWidth = (Width - Margin * 2f - ButtonGap * 4f) / Tiers.Max;
        for (int tier = 1; tier <= Tiers.Max; tier++)
        {
            int chosen = tier;
            Action(page, $"Tier {Tiers.ToRoman(tier)}", Margin + (tier - 1) * (tierWidth + ButtonGap), y, tierWidth, () => Cheats.GiveMaterials(chosen, 99));
        }
    }

    private void BuildItems(Transform page, UIStyle style)
    {
        RectTransform field = At("Search", page, Margin, 4f, 330f, 46f);
        field.gameObject.AddComponent<Image>();
        field.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        RectTransform area = Stretch("Text Area", field);
        area.offsetMin = new Vector2(14f, 4f);
        area.offsetMax = new Vector2(-14f, -4f);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = Label("Text", area, 0f, 0f, 300f, 38f, 20f, style.Text);
        ((RectTransform)text.transform).anchorMin = Vector2.zero;
        ((RectTransform)text.transform).anchorMax = Vector2.one;
        ((RectTransform)text.transform).offsetMin = ((RectTransform)text.transform).offsetMax = Vector2.zero;
        TMP_Text hint = Label("Hint", area, 0f, 0f, 300f, 38f, 20f, style.Muted);
        hint.text = "Search...";
        search = field.gameObject.AddComponent<TMP_InputField>();
        search.textViewport = area;
        search.textComponent = text;
        search.placeholder = hint;
        search.lineType = TMP_InputField.LineType.SingleLine;
        search.onValueChanged.AddListener(_ => LayOutItems());

        float x = Margin + 344f;
        foreach (int value in Amounts)
        {
            int chosen = value;
            Switch(page, $"{value}", x, 4f, 86f, () => amount == chosen, _ => amount = chosen, showsState: false);
            x += 92f;
        }

        Switch(page, "To the stash", x + 8f, 4f, 240f, () => toStash, on => toStash = on);

        string[] groups = { "All", "Materials", "Supplies", "Gear", "Tools", "Valuables", "Keepsakes", "Essences" };
        const float groupWidth = 130f;
        for (int i = 0; i < groups.Length; i++)
        {
            int group = i - 1;
            Button button = AddTab(groups[i], page, Margin + i * (groupWidth + 8f), 56f, groupWidth, 36f, style, out TMP_Text label, out Image underline);
            label.text = groups[i];
            label.fontSize = 17f;
            underline.enabled = false;
            groupLabels.Add(label);
            button.onClick.AddListener(() =>
            {
                itemGroup = group;
                LayOutItems();
            });
        }

        RectTransform list = At("List", page, Margin, 100f, Width - Margin * 2f, PageHeight - 106f);
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        list.gameObject.AddComponent<RectMask2D>();
        list.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        itemContent = At("Rows", list, 0f, 0f, Width - Margin * 2f, 0f);
        scroll.content = itemContent;
        scroll.viewport = list;

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var items = new List<ItemDefinition>(content.Items);
        items.RemoveAll(item => item == null);
        items.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
        foreach (ItemDefinition item in items)
        {
            ItemDefinition chosen = item;
            AddItemRow(item.DisplayName, item.Icon, GroupOf(item.Category), item.Tier > 0 ? Tiers.ToRoman(item.Tier) : "", () => Say(Cheats.Give(chosen, amount, toStash)), style);
        }

        foreach (ResourceDefinition resource in CheatSettings.Current.Essences)
        {
            if (resource != null)
            {
                ResourceDefinition chosen = resource;
                AddItemRow(resource.DisplayName, resource.Icon, 6, resource.Tier > 0 ? Tiers.ToRoman(resource.Tier) : "", () => Say(Cheats.Give(chosen, amount, toStash)), style);
            }
        }

        LayOutItems();
    }

    private static int GroupOf(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Material:
                return 0;
            case ItemCategory.Consumable:
                return 1;
            case ItemCategory.Equipment:
                return 2;
            case ItemCategory.Tool:
                return 3;
            case ItemCategory.Valuable:
                return 4;
            default:
                return 5;
        }
    }

    private void AddItemRow(string name, Sprite icon, int group, string tier, UnityEngine.Events.UnityAction give, UIStyle style)
    {
        RectTransform rect = At(name, itemContent, 0f, 0f, ItemWidth, ItemHeight);
        var frame = rect.gameObject.AddComponent<Image>();
        rect.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        rect.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.onClick.AddListener(give);
        if (icon != null)
        {
            var image = At("Icon", rect, 6f, 6f, 36f, 36f).gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        Label("Name", rect, 50f, 0f, ItemWidth - 96f, ItemHeight, 17f, style.Text).text = name;
        Label("Tier", rect, ItemWidth - 44f, 0f, 36f, ItemHeight, 16f, style.Muted, TextAlignmentOptions.Right).text = tier;
        itemRows.Add(new ItemRow { Root = rect.gameObject, Name = name, Group = group });
    }

    private const float ItemWidth = 364f;
    private const float ItemHeight = 48f;
    private const int ItemColumns = 3;

    private void LayOutItems()
    {
        for (int i = 0; i < groupLabels.Count; i++)
        {
            groupLabels[i].color = i - 1 == itemGroup ? UIStyle.Current.Accent : UIStyle.Current.Text;
        }

        string wanted = search != null ? search.text.Trim() : "";
        int shown = 0;
        foreach (ItemRow row in itemRows)
        {
            bool show = (itemGroup < 0 || row.Group == itemGroup)
                && (wanted.Length == 0 || row.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0);
            row.Root.SetActive(show);
            if (show)
            {
                ((RectTransform)row.Root.transform).anchoredPosition = new Vector2(shown % ItemColumns * (ItemWidth + 8f), -(shown / ItemColumns) * (ItemHeight + 6f));
                shown++;
            }
        }

        itemContent.sizeDelta = new Vector2(itemContent.sizeDelta.x, Mathf.CeilToInt(shown / (float)ItemColumns) * (ItemHeight + 6f));
    }

    private void BuildRaid(Transform page, UIStyle style)
    {
        const float wide = 544f;
        float right = Margin + wide + ButtonGap * 2f;
        raidState = Label("State", page, Margin, 8f, Width - Margin * 2f, 30f, 21f, style.Text);
        Title("Start", page, 48f, style);
        float y = 80f;

        // The kind of run: every mood the raids' layout rules know, by name, and the shapes and sizes.
        var moods = new List<string> { null };
        foreach (RaidDefinition raid in CheatSettings.Current.Raids)
        {
            if (raid == null || raid.LayoutRules == null)
            {
                continue;
            }

            foreach (RaidMapMood mood in raid.LayoutRules.Moods)
            {
                if (mood != null && !moods.Contains(mood.name))
                {
                    moods.Add(mood.name);
                }
            }
        }

        if (moods.Count > 1)
        {
            const int perRow = 7;
            int across = Mathf.Min(perRow, moods.Count);
            float width = (Width - Margin * 2f - ButtonGap * (across - 1)) / across;
            for (int i = 0; i < moods.Count; i++)
            {
                string name = moods[i];
                Switch(page, name ?? "Any type", Margin + i % perRow * (width + ButtonGap), y + i / perRow * RowStep, width, () => raidMood == name, _ => raidMood = name, showsState: false);
            }

            y += Mathf.CeilToInt(moods.Count / (float)perRow) * RowStep;
            const int choices = 8;
            float narrow = (Width - Margin * 2f - ButtonGap * (choices - 1)) / choices;
            Switch(page, "Any shape", Margin, y, narrow, () => raidShape == null, _ => raidShape = null, showsState: false);
            for (int i = 0; i < ShapeNames.Length; i++)
            {
                var shape = (RaidLayoutShape)i;
                Switch(page, ShapeNames[i], Margin + (i + 1) * (narrow + ButtonGap), y, narrow, () => raidShape == shape, _ => raidShape = shape, showsState: false);
            }

            Switch(page, "Type's size", Margin + 5 * (narrow + ButtonGap), y, narrow, () => raidLarge == null, _ => raidLarge = null, showsState: false);
            Switch(page, "Normal", Margin + 6 * (narrow + ButtonGap), y, narrow, () => raidLarge == false, _ => raidLarge = false, showsState: false);
            Switch(page, "Large", Margin + 7 * (narrow + ButtonGap), y, narrow, () => raidLarge == true, _ => raidLarge = true, showsState: false);
            y += RowStep;
        }

        int column = 0;
        foreach (RaidDefinition raid in CheatSettings.Current.Raids)
        {
            // Those without a scene are planned, not yet there to go to.
            if (raid == null || string.IsNullOrEmpty(raid.SceneName))
            {
                continue;
            }

            RaidDefinition chosen = raid;
            AddButton(raid.name, page, column == 0 ? Margin : right, y, wide, UIRole.Kind.Button, () => StartRaid(chosen), out _).text = $"Start {raid.DisplayName}";
            column = 1 - column;
            y += column == 0 ? RowStep : 0f;
        }

        y += column == 1 ? RowStep : 0f;
        y += 8f;
        Title("In a raid", page, y, style);
        y += 32f;
        Action(page, "Extract now", Margin, y, wide, Cheats.ExtractFromRaid, closes: true);
        Action(page, "Fail the raid", right, y, wide, Cheats.FailRaid, closes: true);
        y += RowStep;
        Action(page, "5 more minutes", Margin, y, wide, () => Cheats.AddRaidTime(RaidMinutes));
        Action(page, "10 seconds left", right, y, wide, () => Cheats.EndRaidClock(EndClockSeconds), closes: true);
        y += RowStep;
        Action(page, "Reveal the map", Margin, y, wide, Cheats.RevealMap);
        Action(page, "Go to the nearest exit", right, y, wide, GoToExit, closes: true);
    }

    // The mood, shape, size and seed of the run under way, for a report of what was tested.
    private static string RunKind(RaidSession session)
    {
        RaidLayoutRules rules = session.Definition.LayoutRules;
        if (rules == null)
        {
            return "";
        }

        (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(rules, session.Seed);
        return $" ({mood.name}, {shape.shape}, {(session.IsLarge ? "large" : "normal")}, seed {session.Seed})";
    }

    private void StartRaid(RaidDefinition raid)
    {
        // Closed first: a raid loads a new scene, and this window goes with the old one.
        window.Close();
        if (!Cheats.StartRaid(raid, out string result, raidMood, raidShape, raidLarge))
        {
            GameMessages.Post(result, MessageKind.Warning);
        }
    }

    private static string GoToExit()
    {
        ExtractionPoint nearest = null;
        Vector3 from = Player.Current != null ? Player.Current.transform.position : Vector3.zero;
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (nearest == null || (exit.transform.position - from).sqrMagnitude < (nearest.transform.position - from).sqrMagnitude)
            {
                nearest = exit;
            }
        }

        return nearest != null ? Cheats.GoTo(nearest.transform.position, nearest.DisplayName) : "There is no exit here.";
    }

    private void BuildSpawn(Transform page, UIStyle style)
    {
        float x = Margin;
        foreach (int value in Counts)
        {
            int chosen = value;
            Switch(page, $"{value} at a time", x, 4f, 180f, () => spawnCount == chosen, _ => spawnCount = chosen, showsState: false);
            x += 188f;
        }

        Action(page, "Kill everything nearby", Width - Margin - 300f, 4f, 300f, Cheats.KillNearby);
        Title("They appear in front of you when you close this", page, 62f, style);
        const int columns = 3;
        float width = (Width - Margin * 2f - ButtonGap * (columns - 1)) / columns;
        int index = 0;
        foreach (MonsterDefinition creature in CheatSettings.Current.Creatures)
        {
            if (creature == null || creature.Prefab == null)
            {
                continue;
            }

            MonsterDefinition chosen = creature;
            Action(page, creature.DisplayName, Margin + index % columns * (width + ButtonGap), 94f + index / columns * RowStep, width, () =>
            {
                int made = Cheats.Spawn(chosen.Prefab, spawnCount, SpawnDistance).Count;
                return made > 0 ? $"{made} {chosen.DisplayName}." : "There is no player here.";
            });
            index++;
        }
    }

    private void BuildWorld(Transform page, UIStyle style)
    {
        const float wide = 544f;
        float right = Margin + wide + ButtonGap * 2f;
        Title("Time of day", page, 8f, style);
        float width = (Width - Margin * 2f - ButtonGap * (Hours.Length - 1)) / Hours.Length;
        for (int i = 0; i < Hours.Length; i++)
        {
            float hour = Hours[i].Hour;
            Action(page, Hours[i].Name, Margin + i * (width + ButtonGap), 40f, width, () => Cheats.SetHour(hour));
        }

        Title("Weather", page, 100f, style);
        IReadOnlyList<WeatherDefinition> weathers = CheatSettings.Current.Weathers;
        const int columns = 4;
        width = (Width - Margin * 2f - ButtonGap * (columns - 1)) / columns;
        for (int i = 0; i < weathers.Count; i++)
        {
            WeatherDefinition weather = weathers[i];
            if (weather != null)
            {
                Action(page, weather.name, Margin + i % columns * (width + ButtonGap), 132f + i / columns * RowStep, width, () => Cheats.SetWeather(weather));
            }
        }

        float y = 132f + (Mathf.CeilToInt(weathers.Count / (float)columns)) * RowStep + 8f;
        Title("Safe Haven and the story", page, y, style);
        y += 32f;
        Action(page, "Finish all construction", Margin, y, wide, Cheats.FinishBuildings);
        Action(page, "Finish all crafting", right, y, wide, Cheats.FinishCrafts);
        y += RowStep;
        Action(page, "Bring everyone home", Margin, y, wide, BringEveryoneHome);
        Action(page, "Start a ward breach", right, y, wide, Cheats.BeginBreach, closes: true);
        y += RowStep;
        Action(page, "Complete the quests under way", Margin, y, wide, Cheats.CompleteActiveQuests);
        Action(page, "Corruption creeps in", right, y, wide, Cheats.BeginCorruption, closes: true);
    }

    private static string BringEveryoneHome()
    {
        int home = 0;
        foreach (ResidentDefinition person in HavenSettings.Current.Expedition)
        {
            if (person != null && !GameSession.Here.HasResident(person))
            {
                Cheats.BringHome(person);
                home++;
            }
        }

        return home > 0 ? $"{home} came home." : "Everyone is already home.";
    }
}
