using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tamed horse's window, opened by using it (E): everything about it and everything to do with it, in place of a menu
/// of lines. A header with its live picture (a small camera beside it), its name (the pencil renames it), what it is and
/// five numbers with icons; then tabs: Overview (about it, its numbers, its traits; care and the main orders), Care
/// (feeding, grooming, work), Equipment (saddle, barding, bags and what is in them, cart), Skills (what it is learning,
/// how far along) and History (where it came from, how far it has gone). Its buttons are the horse's own menu
/// (Horse.MenuFor), sorted by MenuAction.Group, so what can be done stays in one place. While it is open the player stands
/// still (PlayerHold) and so does the horse (HorseMotion.SetInspected), until something hurts it, which closes it.
/// <para>Made of the UI kit (HearthKit, KitParts), after the user's picture (2026-11-13). Built in code the first time it
/// is opened.</para>
/// </summary>
public sealed class HorsePanelWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 930f;
    private const float Margin = 34f;
    private const float BodyTop = 384f;
    private const float BodyHeight = Height - BodyTop - Margin;
    private const float RefreshSeconds = 0.5f;
    private static readonly string[] TabNames = { "Overview", "Care", "Equipment", "Skills", "History" };
    private static readonly string[] StatNames = { "Health", "Stamina", "Carry", "Speed", "Bond" };
    private static readonly string[] StatIcons = { "Heart", "Bolt", "Weight", "Horseshoe", "Bond" };

    private enum Tab
    {
        Overview = 0,
        Care = 1,
        Equipment = 2,
        Skills = 3,
        History = 4,
    }

    private static readonly (HorseTrait trait, string name, string learnt, string how)[] Traits =
    {
        (HorseTrait.SureFooted, "Sure-footed", "Rough ground no longer slows it.", "Ride it over rough ground: rock, slopes, the forest floor."),
        (HorseTrait.Swimmer, "Swimmer", "It swims faster and tires less in the water.", "Swim it across rivers and meres."),
        (HorseTrait.Enduring, "Enduring", "Its breath comes back sooner and a gallop costs it less.", "Ride it far, a great many kilometres."),
        (HorseTrait.Brave, "Brave", "It never panics, throws you or shies from monsters.", "Fight from its back: it learns from the blows it takes."),
    };

    private static HorsePanelWindow current;

    private UIWindow window;
    private RectTransform panel;
    private Horse horse;
    private GameObject user;
    private float reach = 3.5f;
    private float nextRefresh;
    private MenuPage page;
    private Tab tab;

    private KitPortrait portrait;

    private TMP_Text nameLabel;
    private RectTransform pencil;
    private TMP_Text subtitle;
    private TMP_Text summary;
    private readonly KitStatRow[] headerRows = new KitStatRow[5];
    private KitTabs tabs;
    private readonly RectTransform[] pages = new RectTransform[TabNames.Length];

    private TMP_Text about;
    private readonly KitStatRow[] statRows = new KitStatRow[5];
    private KitChips chips;
    private TMP_Text careLine;
    private readonly KitStatRow[] careRows = new KitStatRow[3];
    private KitButton feedButton;
    private KitButton groomButton;
    private TMP_Text groomedLine;
    private readonly List<KitButton> mainButtons = new List<KitButton>();
    private TMP_Text tip;

    private TMP_Text careSummary;
    private KitActionList careList;
    private KitSlotCard saddleSlot;
    private KitSlotCard bardingSlot;
    private KitSlotCard bagsSlot;
    private KitActionList gearList;
    private KitSlotGrid bagGrid;
    private TMP_Text bagNote;
    private readonly KitProgressRow[] traitRows = new KitProgressRow[4];
    private TMP_Text natureName;
    private TMP_Text natureText;
    private readonly KitFactRow[] historyRows = new KitFactRow[7];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>True while it is open.</summary>
    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Opens it on this horse, for <paramref name="who"/> (whose bag it feeds from), while they are within
    /// <paramref name="range"/> of it (and a little more: a grazing horse ambles).</summary>
    public static void Show(Horse horse, GameObject who, float range)
    {
        if (horse == null || who == null)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Horse Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<HorsePanelWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        if (current.horse != null && current.horse != horse && current.horse.Motion != null)
        {
            current.horse.Motion.SetInspected(current, false);
        }

        current.horse = horse;
        current.user = who;
        current.reach = range;
        if (current.window.IsOpen && horse.Motion != null)
        {
            horse.Motion.SetInspected(current, true);
        }

        // Open first: its labels are measured as they are filled, which they can't be while hidden.
        current.window.Open();
        HearthKit.FitToScreen(current.panel, (RectTransform)current.transform, Width, Height);
        current.ShowTab(Tab.Overview);
    }

    // ----- Building it -----

    private void Build(Transform root)
    {
        panel = HearthKit.Window(root, Width, Height, () => window.Close(), out RectTransform content);
        BuildHeader();
        tabs = new KitTabs(panel, Margin + 8f, 312f, 178f, 54f, TabNames, i => ShowTab((Tab)i));
        HearthKit.Rule(panel, Margin, 370f, Width - Margin * 2f);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = CodeUI.At(TabNames[i] + " Page", panel, Margin, BodyTop, Width - Margin * 2f, BodyHeight);
        }

        BuildOverview(pages[(int)Tab.Overview]);
        BuildCare(pages[(int)Tab.Care]);
        BuildEquipment(pages[(int)Tab.Equipment]);
        BuildSkills(pages[(int)Tab.Skills]);
        BuildHistory(pages[(int)Tab.History]);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.HorsePanel, content.gameObject);
        window.OpenChanged += open =>
        {
            PlayerHold.Set(this, open);
            if (horse != null && horse.Motion != null)
            {
                horse.Motion.SetInspected(this, open);
            }

            if (!open && portrait != null)
            {
                portrait.Hide();
            }
        };
        PadTabs.Register(this, () => window != null && window.IsOpen, step => ShowTab((Tab)PadTabs.Next((int)tab, TabNames.Length, step)),
            () => tabs.Rects);
    }

    private void BuildHeader()
    {
        UIStyle style = UIStyle.Current;
        // The picture, live, in a gold-rimmed frame: the horse before a painted meadow (PortraitStage).
        portrait = KitPortrait.Make(panel, "Picture", Margin + 8f, Margin + 8f, 330f, 250f, "HorseBackdrop");

        float x = Margin + 372f;
        nameLabel = HearthKit.DisplayName(panel, x, Margin - 6f, 420f);
        pencil = CodeUI.At("Rename", panel, x + 200f, Margin + 22f, 38f, 38f);
        var hit = pencil.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        var rename = pencil.gameObject.AddComponent<Button>();
        rename.targetGraphic = hit;
        rename.onClick.AddListener(() => Choose(Find("Name")));
        HearthKit.Icon(pencil, "Pencil", 6f, 6f, 26f, style.Muted);
        subtitle = CodeUI.Label("Subtitle", panel, x, Margin + 82f, 400f, 30f, 21f, style.Accent);
        CodeUI.Hairline(panel, x, Margin + 120f, 380f, 1f, style);
        summary = CodeUI.Paragraph("Summary", panel, x, Margin + 134f, 384f, 120f, 20f, style.Muted);
        for (int i = 0; i < StatNames.Length; i++)
        {
            headerRows[i] = new KitStatRow(panel, Width - Margin - 360f, Margin + 14f + i * 50f, 352f, StatNames[i], StatIcons[i], i == 0 || i == 1 || i == 4, 21f);
        }
    }

    private void BuildOverview(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        const float leftWidth = 548f;
        RectTransform left = HearthKit.Card(page, "About Card", 0f, 0f, leftWidth, BodyHeight);
        HearthKit.Section(left, "About", "Info", 22f, 18f);
        about = CodeUI.Paragraph("About", left, 24f, 58f, leftWidth - 48f, 78f, 19f, style.Text);
        CodeUI.Hairline(left, 22f, 142f, leftWidth - 44f, 1f, style);
        HearthKit.Section(left, "Stats", "Stats", 22f, 156f);
        string[] names = { "Health", "Stamina", "Carry capacity", "Speed", "Bond" };
        for (int i = 0; i < names.Length; i++)
        {
            statRows[i] = new KitStatRow(left, 24f, 196f + i * 38f, leftWidth - 48f, names[i], StatIcons[i], i == 0 || i == 1 || i == 4, 18f, inline: true);
        }

        CodeUI.Hairline(left, 22f, 392f, leftWidth - 44f, 1f, style);
        HearthKit.Section(left, "Traits", "Star", 22f, 404f);
        chips = new KitChips(left, 24f, 444f, leftWidth - 48f);

        float rx = leftWidth + 16f;
        float rightWidth = Width - Margin * 2f - rx;
        RectTransform right = HearthKit.Card(page, "Care Card", rx, 0f, rightWidth, BodyHeight);
        HearthKit.Section(right, "Care", "Wheat", 22f, 18f);
        careLine = CodeUI.Label("Line", right, 24f, 54f, rightWidth - 48f, 26f, 17f, style.Muted);
        string[] care = { "Health", "Stamina", "Bond" };
        string[] careIcons = { "Heart", "Bolt", "Bond" };
        for (int i = 0; i < care.Length; i++)
        {
            careRows[i] = new KitStatRow(right, 24f, 92f + i * 46f, rightWidth - 190f, care[i], careIcons[i], true, 18f, inline: true, labelWidth: 100f, valueWidth: 50f);
        }

        feedButton = new KitButton(right, rightWidth - 150f, 86f, 126f, 38f, Choose, KitButtonShape.Compact);
        groomButton = new KitButton(right, rightWidth - 150f, 178f, 126f, 38f, Choose, KitButtonShape.Compact);
        groomedLine = CodeUI.Label("Groomed", right, 24f, 226f, rightWidth - 48f, 24f, 17f, style.Muted);
        CodeUI.Hairline(right, 22f, 262f, rightWidth - 44f, 1f, style);
        HearthKit.Section(right, "Actions", "Gear", 22f, 276f);
        const float buttonWidth = 122f;
        for (int i = 0; i < 4; i++)
        {
            mainButtons.Add(new KitButton(right, 24f + i * (buttonWidth + 9f), 316f, buttonWidth, 84f, Choose));
        }

        RectTransform tipBox = HearthKit.Card(right, "Tip", 22f, 418f, rightWidth - 44f, 72f, 0.6f);
        HearthKit.Icon(tipBox, "Info", 16f, 21f, 30f);
        tip = CodeUI.Paragraph("Tip", tipBox, 58f, 10f, rightWidth - 44f - 74f, 56f, 16f, style.Muted);
        tip.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void BuildCare(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "Care Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "Care and work", "Brush", 22f, 18f);
        careSummary = CodeUI.Paragraph("Summary", card, 24f, 56f, width - 48f, 54f, 18f, style.Muted);
        careList = new KitActionList(card, 22f, 118f, width - 44f, Choose);
    }

    private void BuildEquipment(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "Gear Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "Equipment", "Saddle", 22f, 18f);
        float slot = (width - 44f - 24f) / 3f;
        saddleSlot = new KitSlotCard(card, 22f, 58f, slot, "Saddle", "Saddle");
        bardingSlot = new KitSlotCard(card, 22f + slot + 12f, 58f, slot, "Barding", "Barding");
        bagsSlot = new KitSlotCard(card, 22f + (slot + 12f) * 2f, 58f, slot, "Saddle bags", "Bags");
        const float listWidth = 500f;
        gearList = new KitActionList(card, 22f, 166f, listWidth, Choose, columns: 1, rowHeight: 58f);
        // What it carries: its bags' slots as they are; a click opens them.
        float bagsX = 22f + listWidth + 24f;
        HearthKit.Section(card, "In its bags", "Bags", bagsX, 166f);
        bagGrid = new KitSlotGrid(card, bagsX, 206f, width - bagsX - 22f, () => Choose(Find("Gear", "Saddle bags")));
        bagNote = CodeUI.Paragraph("Note", card, bagsX + 2f, 206f, width - bagsX - 22f, 60f, 17f, style.Muted);
    }

    private void BuildSkills(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "Skills Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "What it learns", "Star", 22f, 18f);
        for (int i = 0; i < traitRows.Length; i++)
        {
            traitRows[i] = new KitProgressRow(card, 22f, 58f + i * 84f, width - 44f);
        }

        CodeUI.Hairline(card, 22f, 400f, width - 44f, 1f, style);
        HearthKit.Section(card, "Its nature", "HorseHead", 22f, 412f);
        natureName = CodeUI.Label("Nature", card, 24f, 448f, 300f, 26f, 20f, style.Accent);
        natureText = CodeUI.Paragraph("Nature Text", card, 220f, 448f, width - 260f, 48f, 17f, style.Muted);
    }

    private void BuildHistory(RectTransform page)
    {
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "History Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "Its story", "Hourglass", 22f, 18f);
        for (int i = 0; i < historyRows.Length; i++)
        {
            historyRows[i] = new KitFactRow(card, 22f, 62f + i * 58f, width - 44f);
        }
    }

    // ----- Showing it -----

    private void ShowTab(Tab shown)
    {
        tab = shown;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].gameObject.SetActive(i == (int)shown);
        }

        tabs.Show((int)shown);
        Refresh();
    }

    private void Update()
    {
        if (window == null || !window.IsOpen)
        {
            return;
        }

        if (horse == null || !horse.IsAlive || !horse.IsTamed || user == null
            // Attacked while the player looks it over, it is free to move, and the window gives the player back their hands.
            || horse.Motion != null && !horse.Motion.HeldForInspection
            || (user.transform.position - horse.transform.position).sqrMagnitude > (reach + 8f) * (reach + 8f))
        {
            window.Close();
            return;
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (horse == null || user == null)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + RefreshSeconds;
        page = horse.MenuFor(user);
        UIStyle style = UIStyle.Current;
        HorseDefinition kind = horse.Definition;
        HorseStats stats = horse.Stats;

        nameLabel.text = horse.HorseName;
        nameLabel.ForceMeshUpdate();
        MenuAction naming = Find("Name");
        pencil.gameObject.SetActive(naming != null && naming.Enabled);
        pencil.anchoredPosition = new Vector2(nameLabel.rectTransform.anchoredPosition.x + Mathf.Min(nameLabel.preferredWidth, 400f) + 14f, pencil.anchoredPosition.y);
        subtitle.text = $"{Capital(stats.SexName)}  •  {Capital(Grade(horse))} {kind.DisplayName.ToLowerInvariant()}";
        summary.text = Summary(horse);
        float health = horse.Health.HealthFraction;
        float breath = horse.MaxStamina > 0f ? horse.Stamina / horse.MaxStamina : 0f;
        float bond = horse.CurrentBond() / HorseBondRules.Most;
        SetStats(headerRows, health, breath, bond, style);

        switch (tab)
        {
            case Tab.Overview:
                RefreshOverview(style, kind, stats, health, breath, bond);
                break;
            case Tab.Care:
                careSummary.text = CareSummary(horse);
                careList.Show(Actions("Care"));
                break;
            case Tab.Equipment:
                RefreshEquipment();
                break;
            case Tab.Skills:
                for (int i = 0; i < Traits.Length; i++)
                {
                    bool learnt = stats.Has(Traits[i].trait);
                    traitRows[i].Set(Traits[i].name, learnt ? Traits[i].learnt : Traits[i].how, learnt, horse.TraitProgress(Traits[i].trait));
                }

                natureName.text = Capital(HorseTemperamentRules.NameOf(stats.temperament));
                natureText.text = horse.Temperament.description ?? "";
                break;
            case Tab.History:
                RefreshHistory(stats);
                break;
        }
    }

    private void SetStats(KitStatRow[] rows, float health, float breath, float bond, UIStyle style)
    {
        HorseStats stats = horse.Stats;
        rows[0].Set($"{horse.Health.CurrentHealth:0} / {horse.Health.MaxHealth:0}", health, style.Good, style.Danger);
        rows[1].Set($"{horse.Stamina:0} / {horse.MaxStamina:0}", breath, style.Water, style.Warning);
        rows[2].Set($"{stats.carryWeight:0} kg", 0f, style.Text, style.Accent);
        rows[3].Set($"{stats.gallopSpeed:0.0} m/s", 0f, style.Text, style.Accent);
        rows[4].Set($"{horse.CurrentBond():0} / 100", bond, style.Accent, style.Danger);
    }

    private void RefreshOverview(UIStyle style, HorseDefinition kind, HorseStats stats, float health, float breath, float bond)
    {
        about.text = About(horse);
        SetStats(statRows, health, breath, bond, style);
        float Share(float value, Vector2 range) => Mathf.InverseLerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), value);
        var shown = new List<(string, Color)> { (Capital(HorseTemperamentRules.NameOf(stats.temperament)), style.Good) };
        foreach (var (trait, name, _, _) in Traits)
        {
            if (stats.Has(trait))
            {
                shown.Add((name, style.Info));
            }
        }

        if (Share(stats.gallopSpeed, kind.GallopSpeed) >= 0.75f) shown.Add(("Fast", style.Info));
        if (Share(stats.carryWeight, kind.CarryWeight) >= 0.75f) shown.Add(("Strong", style.Info));
        if (!stats.IsGrown) shown.Add(("Foal", style.Warning));
        chips.Show(shown);

        careLine.text = $"Keep {horse.HorseName} well, and it does more for you.";
        careRows[0].Set($"{horse.Health.CurrentHealth:0}", health, style.Good, style.Danger);
        careRows[1].Set($"{horse.Stamina:0}", breath, style.Water, style.Warning);
        careRows[2].Set($"{horse.CurrentBond():0}", bond, style.Accent, style.Danger);
        feedButton.Bind(Find("Care", "Feed it"), "Feed");
        groomButton.Bind(Find("Care", "Groom it"), "Groom");
        bool groomed = PlantedTrees.Today - horse.LastGroomed < 1f;
        groomedLine.text = groomed ? "Groomed today: it is content." : "Not groomed today: a brush once a day grows its bond.";
        List<MenuAction> main = Actions("Main");
        for (int i = 0; i < mainButtons.Count; i++)
        {
            mainButtons[i].Bind(i < main.Count ? main[i] : null);
        }

        tip.text = Tip(horse, groomed);
    }

    private void RefreshEquipment()
    {
        saddleSlot.Set(horse.Saddle != null ? horse.Saddle.DisplayName : "No saddle", horse.Saddle != null ? $"{horse.Saddle.SaddleSlots} places in its bags" : "Without one it carries nothing", horse.Saddle != null);
        bardingSlot.Set(horse.Armour != null ? horse.Armour.DisplayName : "No barding", horse.Armour != null ? $"Keeps {horse.Armour.HorseArmour * 100f:0}% of a blow off" : "Iron or hide keeps blows off it", horse.Armour != null);
        bagsSlot.Set(horse.Saddle != null ? $"{horse.Weight:0} / {horse.Capacity:0} weight" : "None yet", horse.Saddle != null ? (horse.IsOverfilled ? "Too heavy: it slows" : "More than six tenths slows it") : "They come with a saddle", horse.Saddle != null,
            horse.Saddle != null ? horse.Weight / Mathf.Max(1f, horse.Capacity) : -1f);
        gearList.Show(Actions("Gear"));
        int slots = horse.Saddle != null ? horse.Saddle.SaddleSlots : 0;
        int used = bagGrid.Show(horse.Saddle != null ? horse.Cargo : null, slots);
        bagNote.rectTransform.anchoredPosition = new Vector2(bagNote.rectTransform.anchoredPosition.x, -(slots > 0 ? bagGrid.Bottom(slots) + 6f : 206f));
        bagNote.text = slots == 0 ? "No saddle, no bags: a hide saddle gives it eight places to carry your load."
            : used == 0 ? "Empty. Click to open the bags and load them." : $"{used} of {slots} places used. Click to open the bags.";
    }

    private void RefreshHistory(HorseStats stats)
    {
        string owner = horse.IsMine ? "You" : string.IsNullOrWhiteSpace(horse.OwnerName) ? "A friend" : horse.OwnerName;
        string origin = stats.generation > 0 ? $"Born in your stable, generation {stats.generation}" : "Caught wild and broken to the saddle";
        string age = stats.IsGrown ? "Fully grown" : $"A foal, {stats.growth * 100f:0}% grown";
        string doing = horse.IsStabled ? "In the stable" : horse.Order == Horse.Orders.Follow ? "Following you" : horse.Order == Horse.Orders.Stay ? "Staying where it is"
            : horse.Order == Horse.Orders.Work ? "At work" : horse.Rider != null ? "Ridden" : "Grazing";
        historyRows[0].Set("Where it came from", origin);
        historyRows[1].Set("Age", age);
        historyRows[2].Set("Ridden", horse.RiddenMetres >= 1000f ? $"{horse.RiddenMetres / 1000f:0.0} km" : $"{horse.RiddenMetres:0} m");
        historyRows[3].Set("Its rider", owner);
        historyRows[4].Set("Now", doing);
        historyRows[5].Set("Learned", horse.TraitNames().Length > 0 ? horse.TraitNames() : "Nothing yet");
        historyRows[6].Set("Foal", horse.WithFoalSince >= 0f ? "With foal" : "None on the way");
    }

    private List<MenuAction> Actions(string group)
    {
        var found = new List<MenuAction>();
        if (page != null)
        {
            foreach (MenuAction action in page.Actions)
            {
                if (action.Group == group)
                {
                    found.Add(action);
                }
            }
        }

        return found;
    }

    private MenuAction Find(string group, string label = null)
    {
        if (page == null)
        {
            return null;
        }

        foreach (MenuAction action in page.Actions)
        {
            if (action.Group == group && (label == null || action.Label == label))
            {
                return action;
            }
        }

        return null;
    }

    private void Choose(MenuAction action)
    {
        if (action == null || !action.Enabled)
        {
            return;
        }

        action.Chosen?.Invoke();
        if (action.ClosesMenu)
        {
            window.Close();
        }
        else
        {
            Refresh();
        }
    }

    // ----- Words -----

    private static string Grade(Horse horse)
    {
        float quality = horse.Definition != null ? horse.Stats.Quality(horse.Definition) : 0f;
        return quality >= 0.8f ? "fine" : quality >= 0.5f ? "good" : quality >= 0.25f ? "ordinary" : "poor";
    }

    private static string Summary(Horse horse)
    {
        HorseStats stats = horse.Stats;
        string nature = HorseTemperamentRules.NameOf(stats.temperament);
        string where = horse.IsStabled ? "resting in the stable" : horse.Order == Horse.Orders.Follow ? "following you"
            : horse.Order == Horse.Orders.Stay ? "waiting where you left it" : horse.Order == Horse.Orders.Work ? "hard at work" : "grazing nearby";
        return $"A {nature} {stats.SexName}, {where}." + (horse.Saddle != null ? $" It wears a {horse.Saddle.DisplayName.ToLowerInvariant()}." : " It has no saddle yet.");
    }

    private static string About(Horse horse)
    {
        HorseStats stats = horse.Stats;
        string nature = horse.Temperament.description;
        string bred = stats.generation > 0 ? $" Born in your stable, generation {stats.generation}." : "";
        string foal = horse.WithFoalSince >= 0f ? " She is with foal." : "";
        return (string.IsNullOrWhiteSpace(nature) ? $"{horse.HorseName} is {HorseTemperamentRules.NameOf(stats.temperament)} by nature." : nature) + bred + foal;
    }

    private static string CareSummary(Horse horse)
    {
        bool groomed = PlantedTrees.Today - horse.LastGroomed < 1f;
        return $"Barley, a turnip or bread from your bag mends it; a brush once a day grows its bond{(groomed ? " (done today)" : "")}. " +
               "In the stable it heals, safe from the night; near a station it can work for you.";
    }

    private static string Tip(Horse horse, bool groomed)
    {
        if (horse.Saddle == null)
        {
            return "A hide saddle, made at the Workshop, gives it bags to carry your load.";
        }

        if (!groomed)
        {
            return "Groom it today: a horse that is fond of you has more breath and fears less.";
        }

        if (horse.CurrentBond() < 40f)
        {
            return "Ride it far and it grows fond of you; at a bond of 40 it strikes out at your command.";
        }

        return $"Tap {GameInput.KeyLabel(GameInput.HorseGallop)} to cruise, hold it to gallop; {GameInput.KeyLabel(GameInput.Dodge)} jumps what is ahead.";
    }

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    // ----- The picture: a camera beside it, three-quarters on, looking at its head and shoulder; only while open -----

    private void LateUpdate()
    {
        if (window == null || !window.IsOpen || horse == null)
        {
            return;
        }

        portrait.Show(horse.transform, Shot);
    }

    /// <summary>A horse's portrait: from its right and a little ahead, at the height of its neck.</summary>
    public static (Vector3 eye, Vector3 look) Shot(Transform body)
    {
        Vector3 look = body.position + Vector3.up * 1.45f + body.forward * 0.35f;
        return (look + body.right * 3.4f + body.forward * 2.6f + Vector3.up * 0.25f, look);
    }

    private void OnDestroy()
    {
        PlayerHold.Set(this, false);
        PadTabs.Unregister(this);
    }
}
