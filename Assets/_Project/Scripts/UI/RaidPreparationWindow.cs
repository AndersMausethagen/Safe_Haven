using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The raid planner (GDD §7.5), opened at the repaired portal. The left column shows, as pictures with their names: this run's
/// hour, map and sky (chips), what is out there (a card for each rumour, sighting and ask from home, its painting or picture), the
/// known dangers (the creatures' portraits and the harms they deal) and the likely finds, every word about them on hover. The right column acts: difficulty tier, extra time
/// with Portal Sparks (GDD §7.3, paid once on entering) and raid modifier items from the bag (each kind at most once; one is
/// used up). A strip of figures shows the resulting rules, and the footer states in one line what can be lost (GDD §18.6:
/// before the first raid, not on the death screen), the rules in full on hover. Longer text stays behind hover so the screen
/// can be read at a glance. Built in code when it wakes, inside the prefab's content object, and closes when the player walks
/// away from the portal.
/// <para>
/// The choices and their rules are a <see cref="RaidPlan"/>'s; this window shows the plan and turns clicks into its
/// methods. Its parts: .Briefing (the left column), .Preparation (the right column, the figures and the footer) and
/// .Building (the layout, made in code).
/// </para>
/// </summary>
public sealed partial class RaidPreparationWindow : MonoBehaviour
{
    private const float Width = 1240f;
    // 44 taller (2026-11-21) so two rows of the briefing's event cards fit above the dangers and finds.
    private const float Height = 912f;
    private const float Margin = 44f;
    private const float ScreenMargin = 24f;
    // Up a little from the middle: at 1080p it then clears the health bars and the hotbar below it.
    private const float Lift = 24f;
    private const float ColumnWidth = 556f;
    private const float RightColumn = 640f;
    private const float BodyTop = 172f;
    private const float BodyBottom = 746f;
    private const float StatsTop = 754f;
    private const float FooterTop = 838f;
    private const float HeadingHeight = 30f;
    private const float SectionGap = 16f;
    private const float RowHeight = 56f;
    private const float FindSize = 48f;
    // The briefing's cards: four events across the column, as many rows as the column has room for before they scroll; six
    // creatures in a row.
    private const float CardGap = 10f;
    private const float EventCardWidth = 131f;
    private const float EventPictureHeight = 74f;
    private const float DangerCardWidth = 84f;
    private const float DangerPictureHeight = 66f;
    // How much of the next row shows when there are more cards than rows: a sign there is more to scroll to.
    private const float RowPeek = 28f;
    private const float ChipRowHeight = 44f;
    private const int MaxWantedCards = 3;
    private const string EmptyBag = "Make runes at the Construction Table or the Cauldron, or find them in raids. One of each kind can be used on a raid, and it is used up on entry.";
    private const string NoRunesForBosses = "A boss encounter takes no runes: there is no clock to stretch and no map to read. Come ready: eat, bring tonics and mend your gear.";

    private enum Figure
    {
        Time = 0,
        Extraction = 1,
        Loot = 2,
        EnemyHealth = 3,
        EnemyPower = 4,
        Elites = 5,
    }

    private static readonly string[] FigureNames = { "Time limit", "To extract", "Cache loot", "Enemy health", "Enemy power", "Elites" };
    private static readonly string[] FigureIcons = { "Hourglass", "Lead", "Bags", "Heart", "Sword", "Skull" };

    private sealed class Tab
    {
        public TMP_Text Label;
        public RectTransform Rect;
        public GameObject Mark;
    }

    private sealed class TierButton
    {
        public Button Button;
        public TMP_Text Label;
        public GameObject Mark;
        public RaidDifficulty Tier;
    }

    /// <summary>A short fact on a rounded plate with its sign, sized to its words; more on hover.</summary>
    private sealed class Chip
    {
        private const float ChipHeight = 34f;
        private const float Padding = 12f;
        private const float IconSize = 20f;
        private readonly RectTransform rect;
        private readonly Image icon;
        private readonly TMP_Text label;

        public GameObject Root => rect.gameObject;

        public Chip(string name, Transform parent, UIStyle style)
        {
            rect = At(name, parent, 0f, 0f, 100f, ChipHeight);
            Image plate = HearthKit.Sliced(Root.AddComponent<Image>(), style.Card, 1.6f);
            plate.color = style.Inset;
            HearthKit.Rim(rect, 0.3f, 1.6f);
            UIHighlight.On(Root);
            icon = At("Icon", rect, Padding - 2f, (ChipHeight - IconSize) * 0.5f, IconSize, IconSize).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            label = Label("Label", rect, Padding + IconSize + 4f, 0f, 76f, ChipHeight, 18f, style.Text);
        }

        /// <summary>Shows it with the kit icon <paramref name="sign"/> at a place, and returns where the next chip can start.</summary>
        public float Show(string sign, string text, float x, float y)
        {
            Root.SetActive(true);
            icon.sprite = UIStyle.Current.Icon(sign);
            icon.color = HearthKit.IconColour(sign);
            icon.enabled = icon.sprite != null;
            float lead = icon.enabled ? IconSize + 6f : 0f;
            label.text = text;
            float width = Mathf.Ceil(label.GetPreferredValues(text, 1000f, ChipHeight).x) + 2f;
            label.rectTransform.anchoredPosition = new Vector2(Padding + lead, 0f);
            label.rectTransform.sizeDelta = new Vector2(width, ChipHeight);
            rect.sizeDelta = new Vector2(width + lead + Padding * 2f, ChipHeight);
            rect.anchoredPosition = new Vector2(x, -y);
            return x + rect.sizeDelta.x + 8f;
        }
    }

    private sealed class FindSlot
    {
        public RectTransform Rect;
        public Image Icon;
        public UnityEngine.Object Definition;
    }

    private sealed class ModifierRow
    {
        public GameObject Root;
        public Button Button;
        public Image Icon;
        public GameObject Mark;
        public TMP_Text Name;
        public TMP_Text Effect;
        public TMP_Text State;
        public ItemDefinition Item;
    }

    [SerializeField] private UIWindow window;
    [Tooltip("The object the UIWindow shows and hides. The window is built inside it when the game starts.")]
    [SerializeField] private RectTransform content;
    [Tooltip("The loss rules in one line, stated before every raid (GDD §21.3).")]
    [SerializeField] private string lossSummary = "Fall or run out of time: materials, food and finds are lost. Gear returns damaged.";
    [Tooltip("The loss rules in full, shown on hover over the summary.")]
    [SerializeField, TextArea] private string lossRules = "If you fall or time runs out, you wake at the portal. What you carry is lost: materials, food and anything found in the raid. The tools, weapons and packs you bring return damaged and can be repaired at home. Your home remains safe.";

    private readonly RaidPlan plan = new RaidPlan();
    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<TierButton> tierButtons = new List<TierButton>();
    private readonly List<KitPictureCard> eventCards = new List<KitPictureCard>();
    private readonly List<KitPictureCard> dangerCards = new List<KitPictureCard>();
    private readonly List<FindSlot> findSlots = new List<FindSlot>();
    private readonly List<ModifierRow> modifierRows = new List<ModifierRow>();
    private readonly TMP_Text[] figures = new TMP_Text[FigureNames.Length];
    private ItemInventory inventory;
    private bool dirty;

    private RectTransform panel;
    private ItemTooltipView tooltip;
    private TMP_Text titleLabel;
    private RawImage placeArt;
    private TMP_Text descriptionLabel;
    private GameObject briefing;
    private TMP_Text thisRunHeading;
    private Chip hourChip;
    private Chip moodChip;
    private Chip skyChip;
    private string hourDetail;
    private string moodDetail;
    private string moodTitle;
    private string skyDetail;
    private RectTransform eventView;
    private RectTransform eventContent;
    private TMP_Text dangersHeading;
    private RectTransform dangerView;
    private RectTransform dangerContent;
    private TMP_Text findsHeading;
    private GameObject preparation;
    private TMP_Text tierHeading;
    private TMP_Text gearLabel;
    private TMP_Text tierBriefing;
    private Chip growthChip;
    private string growthDetail;
    private string companionRiskDetail;
    private string noModifiersDetail;
    private TMP_Text sparksHeading;
    private GameObject sparksRow;
    private RectTransform sparksRowRect;
    private TMP_Text sparksValue;
    private TMP_Text sparksAdded;
    private Image sparksIcon;
    private TMP_Text sparksOwned;
    private Button fewerSparks;
    private Button moreSparks;
    private TMP_Text sizeHeading;
    private GameObject sizeRow;
    private RectTransform sizeRowRect;
    private TMP_Text sizeName;
    private TMP_Text sizeDetail;
    private TMP_Text companionHeading;
    private GameObject companionRow;
    private RectTransform companionRowRect;
    private TMP_Text companionName;
    private TMP_Text companionRisk;
    // Who they are as a fighter (Docs/PeopleSystemReview_2026-11-23.md §2.2): one line, all of it on hover.
    private TMP_Text companionSelf;
    private string companionSelfDetail;
    private TMP_Text companionBond;
    private TMP_Text modifiersHeading;
    private TMP_Text modifiersNote;
    private TMP_Text noModifiers;
    private RectTransform modifierView;
    private RectTransform modifierContent;
    private TMP_Text lockedNote;
    private GameObject stats;
    private TMP_Text riskLabel;
    private string riskDetail;
    private Button enterButton;
    private TMP_Text enterLabel;

    private void Awake()
    {
        Build();
        window.OpenChanged += OnOpenChanged;
    }

    private void OnDestroy()
    {
        window.OpenChanged -= OnOpenChanged;
        Watch(null);
    }

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (plan.Portal == null || plan.Player == null || !plan.Portal.IsInReach(plan.Player.transform.position))
        {
            Close();
            return;
        }

        if (dirty)
        {
            Rebuild();
        }

        tooltip.Follow(GameInput.PointerPosition);
    }

    public void Open(RaidPortal source, GameObject user)
    {
        plan.Begin(source, user);
        Watch(user != null ? user.GetComponentInParent<ItemInventory>() : null);
        // The text is measured to lay the columns out, and text can only be measured once its objects are awake.
        window.Open();
        Rebuild();
    }

    public void Close()
    {
        window.Close();
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            Watch(null);
            plan.End();
            tooltip.Hide();
        }
    }

    // The bag can change while the window is open (crafting a modifier, say), so the list follows it.
    private void Watch(ItemInventory bag)
    {
        if (inventory != null)
        {
            inventory.Container.SlotChanged -= OnBagChanged;
        }

        inventory = bag;
        if (inventory != null)
        {
            inventory.Container.SlotChanged += OnBagChanged;
        }
    }

    private void OnBagChanged(int slot)
    {
        dirty = true;
    }

    private void Rebuild()
    {
        dirty = false;
        var counts = new Dictionary<ItemDefinition, int>();
        var order = new List<ItemDefinition>();
        ItemContainer bag = inventory != null ? inventory.Container : null;
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (stack.IsEmpty || !stack.Item.IsRaidModifier)
            {
                continue;
            }

            if (!counts.ContainsKey(stack.Item))
            {
                order.Add(stack.Item);
                counts[stack.Item] = 0;
            }

            counts[stack.Item] += stack.Count;
        }

        plan.ForgetMissing(counts.Keys);
        while (modifierRows.Count < order.Count)
        {
            modifierRows.Add(AddModifierRow(modifierRows.Count));
        }

        for (int i = 0; i < modifierRows.Count; i++)
        {
            bool used = i < order.Count;
            modifierRows[i].Root.SetActive(used);
            if (used)
            {
                BindModifier(modifierRows[i], order[i], counts[order[i]]);
            }
        }

        modifierContent.sizeDelta = new Vector2(ColumnWidth, order.Count * RowHeight);
        Refresh();
    }

    private void Toggle(ItemDefinition item)
    {
        plan.Toggle(item);
        foreach (ModifierRow row in modifierRows)
        {
            ShowPicked(row);
        }

        Refresh();
    }

    // A click changes the plan, and the window shows the plan again.
    private void Change(System.Action change)
    {
        change();
        Refresh();
    }

    private void Refresh()
    {
        Rect screen = content.rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin - Lift * 2f) / Height);

        RaidDefinition raid = plan.Destination;
        DrawTabs();
        // The cheat menu can open a locked raid, and then it plans like any other.
        bool closed = plan.IsClosed;
        plan.Settle();
        string problem = plan.Problem();
        enterButton.interactable = problem == null;
        enterLabel.text = closed ? "Locked" : raid != null && raid.IsEncounter ? "Into the Mist" : "Enter Raid";

        titleLabel.text = raid != null ? raid.DisplayName : "The portal is dormant";
        ShowPlace(raid != null ? raid.PlaceArtwork : null);
        descriptionLabel.text = raid != null ? raid.Description : "";
        bool kitShown = leftPage == LeftPage.Kit;
        leftTabs.Show((int)leftPage);
        briefing.SetActive(raid != null && !kitShown);
        kitPage.SetActive(kitShown);
        if (kitShown)
        {
            DrawKit();
        }

        preparation.SetActive(raid != null && !closed);
        stats.SetActive(raid != null && !closed);
        lockedNote.gameObject.SetActive(closed);
        if (raid == null)
        {
            ShowRisk(problem);
            return;
        }

        RaidTimeOfDay timeOfDay = plan.TimeOfDay;
        if (!kitShown)
        {
            DrawBriefing(raid, timeOfDay, closed);
        }
        if (closed)
        {
            lockedNote.text = raid.LockedText;
            riskLabel.text = "";
            riskDetail = null;
            return;
        }

        DrawPreparation(raid, plan.SparksUsable);
        DrawFigures(raid, timeOfDay);
        ShowRisk(problem);
    }

    private void DrawTabs()
    {
        int count = plan.Destinations.Count;
        while (tabs.Count < count)
        {
            tabs.Add(AddTab(tabs.Count));
        }

        UIStyle style = UIStyle.Current;
        const float width = 168f;
        for (int i = 0; i < tabs.Count; i++)
        {
            Tab tab = tabs[i];
            bool used = count > 1 && i < count;
            tab.Rect.gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }

            RaidDefinition raid = plan.Destinations[i];
            bool shown = i == plan.DestinationIndex;
            tab.Rect.anchoredPosition = new Vector2(Width - Margin - (count - i) * width, tab.Rect.anchoredPosition.y);
            tab.Label.text = raid.IsLocked ? $"{raid.DisplayName} <size=70%>locked</size>"
                : raid.IsEncounter && raid.Encounter.IsBeatenIn(GameSession.Here) ? $"{raid.DisplayName} <size=70%>beaten</size>"
                : raid.DisplayName;
            tab.Label.color = shown ? style.AccentBright : raid.IsLocked ? style.Inactive : style.Text;
            tab.Label.fontStyle = shown ? FontStyles.Bold : FontStyles.Normal;
            tab.Mark.SetActive(shown);
        }
    }

    private void Enter()
    {
        if (plan.Portal == null)
        {
            return;
        }

        if (plan.TryLaunch(out string problem))
        {
            Close();
        }
        else
        {
            GameMessages.Post(problem, MessageKind.Warning);
            Refresh();
        }
    }

    private static float Measure(TMP_Text label, float width) => Mathf.Ceil(label.GetPreferredValues(label.text, width, 0f).y);

    // Moves a part to a height in its column, and returns the height below it.
    private static float Stack(Component part, float y, float height, float gap = 0f)
    {
        var rect = (RectTransform)part.transform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        return y + height + gap;
    }
}
