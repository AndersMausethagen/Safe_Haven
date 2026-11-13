using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The raid planner (GDD §7.5), opened at the repaired portal. The left column reads: this run's hour, map and rumours, the
/// known dangers and the likely finds (icons, with the details on hover). The right column acts: difficulty tier, extra time
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
    private const float Height = 868f;
    private const float Margin = 44f;
    private const float ScreenMargin = 24f;
    // Up a little from the middle: at 1080p it then clears the health bars and the hotbar below it.
    private const float Lift = 24f;
    private const float ColumnWidth = 556f;
    private const float RightColumn = 640f;
    private const float BodyTop = 172f;
    private const float BodyBottom = 702f;
    private const float StatsTop = 710f;
    private const float FooterTop = 794f;
    private const float HeadingHeight = 30f;
    private const float SectionGap = 16f;
    private const float RowHeight = 56f;
    private const float FindSize = 48f;
    private const string EmptyBag = "None in your bag. Make runes at the Construction Table or the Cauldron, or find them in raids.";

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

    /// <summary>A short fact on a dark plate, sized to its words; more on hover.</summary>
    private sealed class Chip
    {
        private const float ChipHeight = 34f;
        private const float Padding = 12f;
        private readonly RectTransform rect;
        private readonly TMP_Text label;

        public GameObject Root => rect.gameObject;

        public Chip(string name, Transform parent, UIStyle style)
        {
            rect = At(name, parent, 0f, 0f, 100f, ChipHeight);
            Root.AddComponent<Image>().color = style.Inset;
            label = Label("Label", rect, Padding, 0f, 76f, ChipHeight, 18f, style.Text);
        }

        /// <summary>Shows it at a place and returns where the next chip can start.</summary>
        public float Show(string text, float x, float y)
        {
            Root.SetActive(true);
            label.text = text;
            float width = Mathf.Ceil(label.GetPreferredValues(text, 1000f, ChipHeight).x) + 2f;
            label.rectTransform.sizeDelta = new Vector2(width, ChipHeight);
            rect.sizeDelta = new Vector2(width + Padding * 2f, ChipHeight);
            rect.anchoredPosition = new Vector2(x, -y);
            return x + rect.sizeDelta.x + 8f;
        }
    }

    private sealed class RumourLine
    {
        public RectTransform Rect;
        public TMP_Text Label;
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
    private readonly List<RumourLine> rumourLines = new List<RumourLine>();
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
    private string hourDetail;
    private string moodDetail;
    private TMP_Text dangersHeading;
    private TMP_Text dangersLabel;
    private TMP_Text findsHeading;
    private GameObject preparation;
    private TMP_Text tierHeading;
    private TMP_Text gearLabel;
    private TMP_Text tierBriefing;
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
        enterLabel.text = closed ? "Locked" : "Enter Raid";

        titleLabel.text = raid != null ? raid.DisplayName : "The portal is dormant";
        ShowPlace(raid != null ? raid.PlaceArtwork : null);
        descriptionLabel.text = raid != null ? raid.Description : "";
        briefing.SetActive(raid != null);
        preparation.SetActive(raid != null && !closed);
        stats.SetActive(raid != null && !closed);
        lockedNote.gameObject.SetActive(closed);
        if (raid == null)
        {
            ShowRisk(problem);
            return;
        }

        RaidTimeOfDay timeOfDay = plan.TimeOfDay;
        DrawBriefing(raid, timeOfDay, closed);
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
            tab.Label.text = raid.IsLocked ? $"{raid.DisplayName} <size=70%>locked</size>" : raid.DisplayName;
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
