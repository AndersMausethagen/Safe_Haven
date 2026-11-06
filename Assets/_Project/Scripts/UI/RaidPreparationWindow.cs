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
/// </summary>
public sealed class RaidPreparationWindow : MonoBehaviour
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

    private readonly HashSet<ItemDefinition> picked = new HashSet<ItemDefinition>();
    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<TierButton> tierButtons = new List<TierButton>();
    private readonly List<RumourLine> rumourLines = new List<RumourLine>();
    private readonly List<FindSlot> findSlots = new List<FindSlot>();
    private readonly List<ModifierRow> modifierRows = new List<ModifierRow>();
    private readonly TMP_Text[] figures = new TMP_Text[FigureNames.Length];
    private RaidPortal portal;
    private GameObject player;
    private ItemInventory inventory;
    private bool dirty;
    private int destinationIndex;
    private int sparks;
    private int pendingSeed;
    private RaidDifficulty difficulty;

    private RectTransform panel;
    private ItemTooltipView tooltip;
    private TMP_Text titleLabel;
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
    private bool large;
    private TMP_Text companionHeading;
    private GameObject companionRow;
    private RectTransform companionRowRect;
    private TMP_Text companionName;
    private TMP_Text companionRisk;
    private TMP_Text companionBond;
    private readonly List<ResidentDefinition> companions = new List<ResidentDefinition>();
    private ResidentDefinition companion;
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

        if (portal == null || player == null || !portal.IsInReach(player.transform.position))
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
        portal = source;
        player = user;
        picked.Clear();
        destinationIndex = 0;
        difficulty = null;
        sparks = 0;
        companion = null;
        large = false;
        // Rolled now, so the briefing shows the hour (and layout) the raid will really have.
        pendingSeed = RaidSession.NewSeed();
        large = LargeByDefault(Destination);
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
            portal = null;
            player = null;
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

        picked.RemoveWhere(item => !counts.ContainsKey(item));
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
        if (!picked.Remove(item))
        {
            picked.Add(item);
        }

        foreach (ModifierRow row in modifierRows)
        {
            ShowPicked(row);
        }

        Refresh();
    }

    private RaidDefinition Destination
    {
        get
        {
            if (portal == null)
            {
                return null;
            }

            IReadOnlyList<RaidDefinition> destinations = portal.Destinations;
            return destinations.Count > 0 ? destinations[Mathf.Clamp(destinationIndex, 0, destinations.Count - 1)] : null;
        }
    }

    private void PickDestination(int index)
    {
        destinationIndex = index;
        difficulty = null;
        large = LargeByDefault(Destination);
        Refresh();
    }

    // A run whose mood is made for a large map (the ravine country) is offered large first; the player can still pick Normal.
    private bool LargeByDefault(RaidDefinition raid)
    {
        if (raid == null || !raid.CanBeLarge || raid.LayoutRules == null)
        {
            return false;
        }

        (RaidShapeRule _, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, pendingSeed);
        return mood != null && mood.largeByDefault;
    }

    private void PickTier(RaidDifficulty tier)
    {
        difficulty = tier;
        Refresh();
    }

    private void ToggleSize()
    {
        large = !large;
        Refresh();
    }

    private float DrawSize(RaidDefinition raid, float y)
    {
        bool can = raid.CanBeLarge;
        sizeHeading.gameObject.SetActive(can);
        sizeRow.SetActive(can);
        if (!can)
        {
            large = false;
            return y;
        }

        y = Stack(sizeHeading, y, HeadingHeight);
        sizeName.text = large ? "Large" : "Normal";
        RaidSizeOption option = raid.LargeRaid;
        float area = option.widthScale * option.widthScale;
        sizeDetail.text = large ? $"{area:0} times the ground and what is on it, {RaidStats.FormatTime(option.extraSeconds)} more time" : "The usual forest";
        return Stack(sizeRowRect, y, 46f, SectionGap);
    }

    private void StepCompanion(int step)
    {
        // Going alone is one of the places in the list.
        int at = companions.IndexOf(companion);
        int count = companions.Count + 1;
        at = (at + 1 + step + count) % count;
        companion = at == 0 ? null : companions[at - 1];
        Refresh();
    }

    private float DrawCompanion(float y)
    {
        companions.Clear();
        foreach (ResidentRecord record in GameSession.Settlement.Residents)
        {
            if (RaidLauncher.CanGoAlong(record.Resident))
            {
                companions.Add(record.Resident);
            }
        }

        bool any = companions.Count > 0;
        companionHeading.gameObject.SetActive(any);
        companionRow.SetActive(any);
        companionRisk.gameObject.SetActive(any);
        if (!any)
        {
            companion = null;
            return y;
        }

        if (!companions.Contains(companion))
        {
            companion = null;
        }

        y = Stack(companionHeading, y, HeadingHeight);
        y = Stack(companionRowRect, y, 46f);
        companionName.text = companion != null ? companion.DisplayName : "Going alone";
        companionBond.text = "";
        if (companion != null)
        {
            int raids = GameSession.Settlement.Find(companion)?.RaidsAlong ?? 0;
            string rank = CompanionBond.RankFor(raids, SettlementSettings.Current.BondRanks)?.name;
            companionBond.text = raids == 0 ? "No raids together yet" : $"{(string.IsNullOrEmpty(rank) ? "" : rank + ", ")}{raids} raid{(raids == 1 ? "" : "s")} together";
        }

        companionRisk.text = companion == null ? "One person can go with you."
            : $"Help them up within {companion.RaidCompanion.BleedOutSeconds:0} seconds if they fall, or the forest has {companion.DisplayName}, and they will forget you.";
        return Stack(companionRisk, y, 26f, SectionGap);
    }

    private void StepSparks(int step)
    {
        sparks = Mathf.Clamp(sparks + step, 0, SettlementSettings.Current.MaxSparksPerRaid);
        Refresh();
    }

    private void Refresh()
    {
        Rect screen = content.rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin - Lift * 2f) / Height);

        RaidDefinition raid = Destination;
        string problem = RaidLauncher.CheckLaunch(raid);
        DrawTabs();
        // The cheat menu can open a locked raid, and then it plans like any other.
        bool closed = raid != null && raid.IsLocked && !Cheats.NoRequirements;
        if (raid != null && difficulty == null)
        {
            // The highest tier the player's gear is ready for; refused tiers stay on offer, and say why.
            difficulty = raid.DifficultyFor(GearScore.Of(player));
        }

        bool sparksUsable = raid != null && !closed && SparksUsable;
        if (!sparksUsable)
        {
            sparks = 0;
        }

        problem ??= RaidLauncher.CheckWard(difficulty);
        problem ??= RaidLauncher.CheckGear(difficulty, player);
        problem ??= RaidLauncher.CheckSparks(player, sparks);
        enterButton.interactable = problem == null;
        enterLabel.text = closed ? "Locked" : "Enter Raid";

        titleLabel.text = raid != null ? raid.DisplayName : "The portal is dormant";
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

        RaidTimeOfDay timeOfDay = RaidTimeOfDay.For(raid, pendingSeed);
        DrawBriefing(raid, timeOfDay, closed);
        if (closed)
        {
            lockedNote.text = raid.LockedText;
            riskLabel.text = "";
            riskDetail = null;
            return;
        }

        DrawPreparation(raid, sparksUsable);
        DrawFigures(raid, timeOfDay);
        ShowRisk(problem);
    }

    private void DrawTabs()
    {
        int count = portal != null ? portal.Destinations.Count : 0;
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

            RaidDefinition raid = portal.Destinations[i];
            bool shown = i == Mathf.Clamp(destinationIndex, 0, count - 1);
            tab.Rect.anchoredPosition = new Vector2(Width - Margin - (count - i) * width, tab.Rect.anchoredPosition.y);
            tab.Label.text = raid.IsLocked ? $"{raid.DisplayName} <size=70%>locked</size>" : raid.DisplayName;
            tab.Label.color = shown ? style.Text : style.Muted;
            tab.Mark.SetActive(shown);
        }
    }

    // The left column. Each part sits under the last, so a run with one rumour and a tier with few monsters leave no gaps.
    // A place that can't be reached has no run to tell of, only what is known of it.
    private void DrawBriefing(RaidDefinition raid, RaidTimeOfDay timeOfDay, bool closed)
    {
        UIStyle style = UIStyle.Current;
        float y = BodyTop;
        float x = Margin;
        y = Stack(thisRunHeading, y, HeadingHeight);
        float chips = y;
        bool anyChip = false;
        hourChip.Root.SetActive(false);
        moodChip.Root.SetActive(false);
        if (timeOfDay != null && !closed)
        {
            int hours = Mathf.FloorToInt(timeOfDay.hour) % 24;
            int minutes = Mathf.FloorToInt((timeOfDay.hour - Mathf.Floor(timeOfDay.hour)) * 60f);
            string reward = timeOfDay.lootMultiplier > 1.001f ? $"  <color={UIStyle.Hex(style.Good)}>+{timeOfDay.lootMultiplier - 1f:P0} loot</color>" : "";
            x = hourChip.Show($"{timeOfDay.DisplayName} {hours:00}:{minutes:00}{reward}", x, chips);
            hourDetail = timeOfDay.briefing;
            anyChip = true;
        }

        if (raid.LayoutRules != null && !closed)
        {
            (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, pendingSeed);
            moodChip.Show(mood.name, x, chips);
            moodDetail = $"{mood.briefing} {shape.briefing}";
            anyChip = true;
        }

        y = anyChip ? chips + 44f : chips;

        List<RaidEventDefinition> foretold = closed ? new List<RaidEventDefinition>() : RaidForecast.For(raid, pendingSeed, HavenReadiness.ForetoldEvents);
        List<RaidRumourDefinition> rumours = closed ? new List<RaidRumourDefinition>() : RaidRumours.For(raid, pendingSeed, GameSession.Settlement, player != null ? player.GetComponent<PlayerCrafting>() : null, large ? raid.LargeRaid.widthScale : 1f);
        while (rumourLines.Count < rumours.Count + foretold.Count)
        {
            rumourLines.Add(AddRumourLine());
        }

        for (int i = 0; i < rumourLines.Count; i++)
        {
            RumourLine line = rumourLines[i];
            line.Rect.gameObject.SetActive(i < rumours.Count + foretold.Count);
            if (i < rumours.Count + foretold.Count)
            {
                line.Label.text = i < rumours.Count ? rumours[i].BriefingFor(GameSession.Settlement) : $"Seen from the Watch Post: {foretold[i - rumours.Count].DisplayName}.";
                float height = Measure(line.Label, ColumnWidth - 16f);
                line.Label.rectTransform.sizeDelta = new Vector2(ColumnWidth - 16f, height);
                y = Stack(line.Rect, y, height, 6f);
            }
        }

        thisRunHeading.gameObject.SetActive(anyChip || rumours.Count > 0);
        y = (anyChip || rumours.Count > 0) ? y + SectionGap - 6f : BodyTop;

        var enemies = new List<string>();
        foreach (MonsterDefinition monster in raid.KnownEnemies)
        {
            if (monster != null && (difficulty == null || raid.CanMeet(monster, difficulty.tier)))
            {
                enemies.Add(monster.DisplayName);
            }
        }

        dangersHeading.gameObject.SetActive(enemies.Count > 0);
        dangersLabel.gameObject.SetActive(enemies.Count > 0);
        if (enemies.Count > 0)
        {
            y = Stack(dangersHeading, y, HeadingHeight);
            dangersLabel.text = string.Join("  ·  ", enemies);
            y = Stack(dangersLabel, y, Measure(dangersLabel, ColumnWidth), SectionGap);
        }

        DrawFinds(raid, y);
    }

    private void DrawFinds(RaidDefinition raid, float y)
    {
        var finds = new List<(Sprite icon, UnityEngine.Object definition)>();
        foreach (ItemDefinition item in raid.LikelyItems)
        {
            if (item != null && (difficulty == null || difficulty.Allows(item)))
            {
                finds.Add((item.Icon, item));
            }
        }

        foreach (ResourceDefinition resource in raid.LikelyResources)
        {
            if (resource != null)
            {
                finds.Add((resource.Icon, resource));
            }
        }

        while (findSlots.Count < finds.Count)
        {
            findSlots.Add(AddFindSlot());
        }

        findsHeading.gameObject.SetActive(finds.Count > 0);
        if (finds.Count > 0)
        {
            y = Stack(findsHeading, y, HeadingHeight);
        }

        const float step = FindSize + 6f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth + 6f) / step));
        for (int i = 0; i < findSlots.Count; i++)
        {
            FindSlot slot = findSlots[i];
            slot.Rect.gameObject.SetActive(i < finds.Count);
            if (i < finds.Count)
            {
                slot.Definition = finds[i].definition;
                slot.Icon.sprite = finds[i].icon;
                slot.Icon.enabled = finds[i].icon != null;
                slot.Rect.anchoredPosition = new Vector2(Margin + i % perRow * step, -(y + i / perRow * step));
            }
        }
    }

    // The right column, stacked the same way: what can't be chosen here takes no room.
    private void DrawPreparation(RaidDefinition raid, bool sparksUsable)
    {
        float y = BodyTop;
        bool tiers = raid.Difficulties.Count > 0;
        tierHeading.gameObject.SetActive(tiers);
        gearLabel.gameObject.SetActive(tiers);
        tierBriefing.gameObject.SetActive(tiers);
        while (tierButtons.Count < raid.Difficulties.Count)
        {
            tierButtons.Add(AddTierButton(tierButtons.Count));
        }

        for (int i = 0; i < tierButtons.Count; i++)
        {
            tierButtons[i].Button.gameObject.SetActive(i < raid.Difficulties.Count);
        }

        if (tiers)
        {
            y = DrawTiers(raid, y);
        }

        sparksHeading.gameObject.SetActive(sparksUsable);
        sparksRow.SetActive(sparksUsable);
        if (sparksUsable)
        {
            y = Stack(sparksHeading, y, HeadingHeight);
            y = Stack(sparksRowRect, y, 46f, SectionGap);
            DrawSparks();
        }

        y = DrawSize(raid, y);
        y = DrawCompanion(y);
        bool any = modifierContent.sizeDelta.y > 0f;
        Stack(modifiersHeading, y, HeadingHeight);
        Stack(modifiersNote, y + 2f, 26f);
        y += HeadingHeight;
        modifiersNote.gameObject.SetActive(any);
        noModifiers.gameObject.SetActive(!any);
        modifierView.gameObject.SetActive(any);
        if (any)
        {
            Stack(modifierView, y, BodyBottom - y);
        }
        else
        {
            noModifiers.text = EmptyBag;
            Stack(noModifiers, y, Measure(noModifiers, ColumnWidth));
        }
    }

    private float DrawTiers(RaidDefinition raid, float y)
    {
        UIStyle style = UIStyle.Current;
        int score = GearScore.Of(player);
        y = Stack(tierHeading, y, HeadingHeight);
        for (int i = 0; i < raid.Difficulties.Count; i++)
        {
            TierButton button = tierButtons[i];
            RaidDifficulty tier = raid.Difficulties[i];
            button.Tier = tier;
            bool shown = tier == difficulty;
            Color dim = style.Muted;
            dim.a = 0.5f;
            button.Label.text = Tiers.ToRoman(tier.tier);
            button.Label.color = shown ? Tiers.ColorOf(tier.tier) : (score >= tier.minimumGearScore && RaidLauncher.CheckWard(tier) == null) || Cheats.NoRequirements ? style.Text : dim;
            button.Mark.SetActive(shown);
            var rect = (RectTransform)button.Button.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        }

        if (difficulty != null)
        {
            Color colour = score >= difficulty.recommendedGearScore ? style.Good : score >= difficulty.minimumGearScore ? style.Accent : style.Warning;
            gearLabel.text = $"Your gear <color={UIStyle.Hex(colour)}>{score}</color>  ·  recommended {difficulty.recommendedGearScore}";
            Stack(gearLabel, y, 46f);
        }

        y += 54f;
        tierBriefing.text = difficulty != null ? difficulty.briefing : "";
        return Stack(tierBriefing, y, Measure(tierBriefing, ColumnWidth), SectionGap);
    }

    private bool SparksUsable => SettlementSettings.Current.PortalSpark != null && SettlementSettings.Current.MaxSparksPerRaid > 0;

    private void DrawSparks()
    {
        UIStyle style = UIStyle.Current;
        SettlementSettings settings = SettlementSettings.Current;
        int owned = MaterialPool.For(player).Have(settings.PortalSpark);
        sparksValue.text = $"{sparks} / {settings.MaxSparksPerRaid}";
        sparksAdded.text = sparks > 0 ? $"+{RaidStats.FormatTime(sparks * settings.SecondsPerSpark)}" : "";
        sparksAdded.color = style.Good;
        sparksIcon.sprite = settings.PortalSpark.Icon;
        sparksIcon.enabled = sparksIcon.sprite != null;
        sparksOwned.text = owned > 0 ? $"You have {owned}" : "You have none";
        sparksOwned.color = owned > 0 ? style.Text : style.Muted;
        fewerSparks.interactable = sparks > 0;
        moreSparks.interactable = sparks < settings.MaxSparksPerRaid && sparks < owned;
    }

    private void DrawFigures(RaidDefinition raid, RaidTimeOfDay timeOfDay)
    {
        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in picked)
        {
            modifiers.Add(item.RaidModifier);
        }

        RaidParameters rules = RaidParameters.Calculate(raid, modifiers, RaidLauncher.ExtraSeconds(sparks) + (large && raid.CanBeLarge ? raid.LargeRaid.extraSeconds : 0f), timeOfDay, difficulty, RaidLauncher.ExtractionCut);
        ShowFigure(Figure.Time, RaidStats.FormatTime(rules.Duration), raid, rules, RaidStat.Duration, true);
        ShowFigure(Figure.Extraction, $"{rules.ExtractionTime:0.#}s", raid, rules, RaidStat.ExtractionTime, false);
        ShowFigure(Figure.Loot, $"×{rules.LootMultiplier:0.##}", raid, rules, RaidStat.LootQuantity, true);
        ShowFigure(Figure.EnemyHealth, $"×{rules.EnemyHealthMultiplier:0.##}", raid, rules, RaidStat.EnemyHealth, false);
        ShowFigure(Figure.EnemyPower, $"×{rules.EnemyPowerMultiplier:0.##}", raid, rules, RaidStat.EnemyPower, false);
        figures[(int)Figure.Elites].text = DescribeElites(difficulty);
        figures[(int)Figure.Elites].color = UIStyle.Current.Text;
    }

    // Against the raid's base rules: green where the choices made here help, amber where they make it harder.
    private void ShowFigure(Figure figure, string value, RaidDefinition raid, RaidParameters rules, RaidStat stat, bool moreIsBetter)
    {
        UIStyle style = UIStyle.Current;
        float was = raid.GetBase(stat);
        float now = rules.Get(stat);
        TMP_Text label = figures[(int)figure];
        label.text = value;
        label.color = Mathf.Approximately(was, now) ? style.Text : now > was == moreIsBetter ? style.Good : style.Warning;
    }

    private static string DescribeElites(RaidDifficulty tier)
    {
        if (tier == null || tier.eliteCount.y <= 0)
        {
            return "None";
        }

        return tier.eliteCount.x == tier.eliteCount.y ? tier.eliteCount.x.ToString() : $"{tier.eliteCount.x}–{tier.eliteCount.y}";
    }

    private void ShowRisk(string problem)
    {
        UIStyle style = UIStyle.Current;
        string warning = UIStyle.Hex(style.Warning);
        string carrying = DescribeCarried();
        riskDetail = $"{lossRules}\n\n{carrying}";
        if (problem != null)
        {
            riskLabel.text = $"<color={warning}>{problem}</color>";
            return;
        }

        string cart = PlayerTransport.Current != null ? PlayerTransport.Current.RaidWarning() : "";
        riskLabel.text = cart.Length > 0
            ? $"<color={warning}>{cart}</color>\n{lossSummary}"
            : $"<color={warning}>{lossSummary}</color>\n{carrying}";
    }

    private string DescribeCarried()
    {
        int items = 0;
        int resources = 0;
        if (player != null)
        {
            PlayerSnapshot carried = PlayerSnapshot.Capture(player);
            foreach (KeyValuePair<ItemDefinition, int> entry in carried.CountItems())
            {
                items += entry.Value;
            }

            foreach (KeyValuePair<ResourceDefinition, int> entry in carried.CountResources())
            {
                resources += entry.Value;
            }
        }

        return items == 0 && resources == 0
            ? "You carry nothing that can be lost."
            : $"You carry {items} item{(items == 1 ? "" : "s")} and {resources} resource{(resources == 1 ? "" : "s")}. Leave what you can't risk in storage.";
    }

    private void Enter()
    {
        if (portal == null)
        {
            return;
        }

        if (RaidLauncher.TryLaunch(Destination, player, picked, out string problem, sparks, pendingSeed, difficulty, companion, large))
        {
            Close();
        }
        else
        {
            GameMessages.Post(problem, MessageKind.Warning);
            Refresh();
        }
    }

    // ----- Building -----

    private void Build()
    {
        UIStyle style = UIStyle.Current;
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), new Vector2(0f, Lift));
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text kicker = Label("Kicker", panel, Margin, 30f, 500f, 24f, 17f, style.Accent);
        kicker.text = "THE PORTAL";
        kicker.characterSpacing = 12f;
        titleLabel = Label("Title", panel, Margin, 52f, 760f, 56f, 42f, style.Text);
        style.Title(titleLabel);
        descriptionLabel = Paragraph("Description", panel, Margin, 108f, Width - Margin * 2f, 50f, 18f, style.Muted);
        Hairline(panel, Margin, 160f, Width - Margin * 2f, 1f, style);

        BuildBriefing(style);
        Hairline(panel, RightColumn - 20f, BodyTop, 1f, BodyBottom - BodyTop, style);
        BuildPreparation(style);
        lockedNote = Paragraph("Locked Note", panel, RightColumn, BodyTop + 4f, ColumnWidth, 200f, 22f, style.Muted);
        style.Title(lockedNote);
        lockedNote.color = style.Muted;
        BuildStats(style);
        BuildFooter(style);
        BuildTooltip(style);
    }

    private void BuildBriefing(UIStyle style)
    {
        RectTransform part = At("Briefing", panel, 0f, 0f, Width, Height);
        briefing = part.gameObject;
        thisRunHeading = Heading("This run", part, Margin, 0f, ColumnWidth, style);
        hourChip = new Chip("Hour", part, style);
        moodChip = new Chip("Forest", part, style);
        Hover(hourChip.Root, () => tooltip.Show("The hour", style.Accent, hourDetail));
        Hover(moodChip.Root, () => tooltip.Show("The forest", style.Accent, moodDetail));
        dangersHeading = Heading("Known dangers", part, Margin, 0f, ColumnWidth, style);
        dangersLabel = Paragraph("Dangers", part, Margin, 0f, ColumnWidth, 30f, 18f, style.Text);
        findsHeading = Heading("Likely finds", part, Margin, 0f, ColumnWidth, style);
    }

    private void BuildPreparation(UIStyle style)
    {
        RectTransform part = At("Preparation", panel, 0f, 0f, Width, Height);
        preparation = part.gameObject;
        tierHeading = Heading("Difficulty", part, RightColumn, 0f, ColumnWidth, style);
        gearLabel = Label("Gear", part, RightColumn, 0f, ColumnWidth, 46f, 18f, style.Muted, TextAlignmentOptions.MidlineRight);
        tierBriefing = Paragraph("Tier Briefing", part, RightColumn, 0f, ColumnWidth, 30f, 17f, style.Muted);

        sparksHeading = Heading("Extra time", part, RightColumn, 0f, ColumnWidth, style);
        sparksRowRect = At("Sparks", part, RightColumn, 0f, ColumnWidth, 46f);
        sparksRow = sparksRowRect.gameObject;
        AddButton("Fewer", sparksRowRect, 0f, 0f, 46f, UIRole.Kind.Button, () => StepSparks(-1), out fewerSparks).text = "−";
        sparksValue = Label("Value", sparksRowRect, 50f, 0f, 76f, 46f, 20f, style.Text, TextAlignmentOptions.Center);
        AddButton("More", sparksRowRect, 130f, 0f, 46f, UIRole.Kind.Button, () => StepSparks(1), out moreSparks).text = "+";
        sparksAdded = Label("Added", sparksRowRect, 190f, 0f, 100f, 46f, 20f, style.Good);
        RectTransform owned = At("Owned", sparksRowRect, ColumnWidth - 210f, 0f, 210f, 46f);
        owned.gameObject.AddComponent<Image>().color = Color.clear;
        sparksIcon = At("Icon", owned, 0f, 8f, 30f, 30f).gameObject.AddComponent<Image>();
        sparksIcon.raycastTarget = false;
        sparksOwned = Label("Label", owned, 38f, 0f, 172f, 46f, 18f, style.Text);
        Hover(owned.gameObject, () => tooltip.ShowDefinition(SettlementSettings.Current.PortalSpark));

        sizeHeading = Heading("Size", part, RightColumn, 0f, ColumnWidth, style);
        sizeRowRect = At("Size", part, RightColumn, 0f, ColumnWidth, 46f);
        sizeRow = sizeRowRect.gameObject;
        AddButton("Change", sizeRowRect, 0f, 0f, 150f, UIRole.Kind.Button, ToggleSize, out Button sizeButton);
        sizeName = sizeButton.GetComponentInChildren<TMP_Text>();
        sizeDetail = Label("Detail", sizeRowRect, 164f, 0f, ColumnWidth - 164f, 46f, 16f, style.Muted);

        companionHeading = Heading("Companion", part, RightColumn, 0f, ColumnWidth, style);
        companionRowRect = At("Companion", part, RightColumn, 0f, ColumnWidth, 46f);
        companionRow = companionRowRect.gameObject;
        AddButton("Previous", companionRowRect, 0f, 0f, 46f, UIRole.Kind.Button, () => StepCompanion(-1), out Button previous).text = "<";
        companionName = Label("Name", companionRowRect, 50f, 0f, 230f, 46f, 20f, style.Text, TextAlignmentOptions.Center);
        AddButton("Next", companionRowRect, 284f, 0f, 46f, UIRole.Kind.Button, () => StepCompanion(1), out Button next).text = ">";
        companionBond = Label("Bond", companionRowRect, 340f, 0f, 216f, 46f, 17f, style.Muted, TextAlignmentOptions.MidlineRight);
        companionRisk = Paragraph("Risk", part, RightColumn, 0f, ColumnWidth, 26f, 15f, style.Muted);

        modifiersHeading = Heading("Raid modifiers", part, RightColumn, 0f, ColumnWidth, style);
        modifiersNote = Label("Note", part, RightColumn, 0f, ColumnWidth, 26f, 16f, style.Muted, TextAlignmentOptions.MidlineRight);
        modifiersNote.text = "One of each is used up on entry";
        noModifiers = Paragraph("None", part, RightColumn, 0f, ColumnWidth, 30f, 17f, style.Muted);
        modifierView = At("Modifier List", part, RightColumn, 0f, ColumnWidth, 150f);
        modifierView.gameObject.AddComponent<Image>().color = Color.clear;
        modifierView.gameObject.AddComponent<RectMask2D>();
        modifierContent = At("Content", modifierView, 0f, 0f, ColumnWidth, 0f);
        var scroll = modifierView.gameObject.AddComponent<ScrollRect>();
        scroll.content = modifierContent;
        scroll.viewport = modifierView;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private void BuildStats(UIStyle style)
    {
        RectTransform part = At("Rules", panel, 0f, 0f, Width, Height);
        stats = part.gameObject;
        Hairline(part, Margin, StatsTop, Width - Margin * 2f, 1f, style);
        float width = (Width - Margin * 2f) / figures.Length;
        for (int i = 0; i < figures.Length; i++)
        {
            float x = Margin + i * width;
            figures[i] = Label(FigureNames[i] + " Value", part, x, StatsTop + 6f, width, 40f, 28f, style.Text);
            Label(FigureNames[i] + " Label", part, x, StatsTop + 44f, width, 24f, 16f, style.Muted).text = FigureNames[i];
        }

        Hairline(part, Margin, FooterTop - 8f, Width - Margin * 2f, 1f, style);
    }

    private void BuildFooter(UIStyle style)
    {
        const float enter = 190f;
        const float close = 130f;
        const float gap = 12f;
        float buttons = FooterTop + 14f;
        float closeAt = Width - Margin - enter - gap - close;
        riskLabel = Paragraph("Risk", panel, Margin, FooterTop, closeAt - Margin - 24f, 72f, 17f, style.Muted);
        riskLabel.raycastTarget = true;
        Hover(riskLabel.gameObject, () =>
        {
            if (riskDetail != null)
            {
                tooltip.Show("What can be lost", style.Warning, riskDetail);
            }
        });
        AddButton("Close Button", panel, closeAt, buttons, close, UIRole.Kind.Button, Close, out _).text = "Close";
        enterLabel = AddButton("Enter Button", panel, Width - Margin - enter, buttons, enter, UIRole.Kind.PrimaryButton, Enter, out enterButton);
    }

    // Laid over the content, not the panel, so it stays at full size when a small screen shrinks the panel.
    private void BuildTooltip(UIStyle style)
    {
        var holder = new GameObject("Tooltip Holder", typeof(RectTransform));
        holder.SetActive(false);
        holder.transform.SetParent(content, false);
        RectTransform tip = Box("Tooltip", content, new Vector2(0.5f, 0.5f), new Vector2(380f, 100f), Vector2.zero);
        tip.pivot = new Vector2(0f, 1f);
        tip.gameObject.AddComponent<Image>().raycastTarget = false;
        tip.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        tip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        var layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 12);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = Paragraph("Title", tip, 0f, 0f, 352f, 28f, 19f, style.Text);
        style.Title(title);
        TMP_Text body = Paragraph("Body", tip, 0f, 0f, 352f, 28f, 16f, style.Text);
        tooltip = holder.AddComponent<ItemTooltipView>();
        tooltip.Configure(tip, title, body);
        holder.SetActive(true);
    }

    private void Hover(GameObject target, Action entered)
    {
        var hover = target.AddComponent<UIHover>();
        hover.Entered = entered;
        hover.Exited = () =>
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        };
    }

    private Tab AddTab(int index)
    {
        UIStyle style = UIStyle.Current;
        const float width = 168f;
        RectTransform rect = At($"Destination {index}", panel, 0f, 44f, width, 46f);
        rect.gameObject.AddComponent<Image>().color = Color.clear;
        rect.gameObject.AddComponent<Button>().onClick.AddListener(() => PickDestination(index));
        TMP_Text label = Label("Label", rect, 0f, 0f, width, 44f, 20f, style.Muted, TextAlignmentOptions.Center);
        RectTransform mark = At("Mark", rect, 0f, 44f, width, 2f);
        mark.gameObject.AddComponent<Image>().color = style.Accent;
        return new Tab { Rect = rect, Label = label, Mark = mark.gameObject };
    }

    private TierButton AddTierButton(int index)
    {
        UIStyle style = UIStyle.Current;
        const float width = 64f;
        var entry = new TierButton();
        entry.Label = AddButton($"Tier {index}", preparation.transform, RightColumn + index * (width + 6f), 0f, width, UIRole.Kind.Button, () => PickTier(entry.Tier), out entry.Button);
        RectTransform mark = At("Mark", entry.Button.transform, 0f, 44f, width, 2f);
        var line = mark.gameObject.AddComponent<Image>();
        line.color = style.Accent;
        line.raycastTarget = false;
        entry.Mark = mark.gameObject;
        Hover(entry.Button.gameObject, () => tooltip.Show($"Tier {Tiers.ToRoman(entry.Tier.tier)}", Tiers.ColorOf(entry.Tier.tier), DescribeTierNeeds(entry.Tier)));
        return entry;
    }

    private static string DescribeTierNeeds(RaidDifficulty tier) =>
        (RaidLauncher.CheckWard(tier) is string ward ? ward + " " : "") + (tier.minimumGearScore > 0
            ? $"Needs a gear score of {tier.minimumGearScore}. {tier.recommendedGearScore} is recommended."
            : $"Open to anyone. A gear score of {tier.recommendedGearScore} is recommended.");

    private RumourLine AddRumourLine()
    {
        UIStyle style = UIStyle.Current;
        RectTransform rect = At("Rumour", briefing.transform, Margin, 0f, ColumnWidth, 30f);
        var mark = At("Mark", rect, 1f, 9f, 7f, 7f).gameObject.AddComponent<Image>();
        mark.color = style.Accent;
        mark.raycastTarget = false;
        mark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        TMP_Text label = Paragraph("Label", rect, 16f, 0f, ColumnWidth - 16f, 30f, 18f, style.Text);
        return new RumourLine { Rect = rect, Label = label };
    }

    private FindSlot AddFindSlot()
    {
        RectTransform rect = At("Find", briefing.transform, Margin, 0f, FindSize, FindSize);
        rect.gameObject.AddComponent<Image>();
        rect.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        rect.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        var icon = At("Icon", rect, 4f, 4f, FindSize - 8f, FindSize - 8f).gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        var slot = new FindSlot { Rect = rect, Icon = icon };
        Hover(rect.gameObject, () => tooltip.ShowDefinition(slot.Definition));
        return slot;
    }

    private ModifierRow AddModifierRow(int index)
    {
        UIStyle style = UIStyle.Current;
        RectTransform rect = At($"Modifier {index}", modifierContent, 0f, index * RowHeight, ColumnWidth, RowHeight - 4f);
        var row = new ModifierRow { Root = rect.gameObject };
        var image = row.Root.AddComponent<Image>();
        row.Button = row.Root.AddComponent<Button>();
        row.Button.targetGraphic = image;
        row.Button.onClick.AddListener(() => Toggle(row.Item));
        var mark = At("Mark", rect, 0f, 0f, 3f, RowHeight - 4f).gameObject.AddComponent<Image>();
        mark.color = style.Accent;
        mark.raycastTarget = false;
        row.Mark = mark.gameObject;
        row.Icon = At("Icon", rect, 12f, 6f, 40f, 40f).gameObject.AddComponent<Image>();
        row.Icon.raycastTarget = false;
        row.Name = Label("Name", rect, 64f, 3f, ColumnWidth - 150f, 26f, 18f, style.Text);
        row.Effect = Label("Effect", rect, 64f, 27f, ColumnWidth - 150f, 22f, 15f, style.Muted);
        row.State = Label("State", rect, ColumnWidth - 86f, 0f, 74f, RowHeight - 4f, 17f, style.Text, TextAlignmentOptions.MidlineRight);
        return row;
    }

    private void BindModifier(ModifierRow row, ItemDefinition item, int count)
    {
        row.Item = item;
        row.Icon.sprite = item.Icon;
        row.Icon.enabled = item.Icon != null;
        row.Name.text = count > 1 ? $"{item.DisplayName}  <color={UIStyle.Hex(UIStyle.Current.Muted)}>×{count}</color>" : item.DisplayName;
        row.Effect.text = item.RaidModifier.DescribeEffects();
        ShowPicked(row);
    }

    private void ShowPicked(ModifierRow row)
    {
        UIStyle style = UIStyle.Current;
        bool on = row.Item != null && picked.Contains(row.Item);
        row.Mark.SetActive(on);
        row.State.text = on ? "Using" : "Use";
        row.State.color = on ? style.Good : style.Text;
        ColorBlock colors = row.Button.colors;
        colors.normalColor = colors.selectedColor = on ? style.Filled : style.Inset;
        colors.highlightedColor = style.Filled;
        colors.pressedColor = style.ControlLit;
        colors.colorMultiplier = 1f;
        row.Button.colors = colors;
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
