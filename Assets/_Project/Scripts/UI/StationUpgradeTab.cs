using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A station's Upgrade tab (StationWindow). The user asked on 2026-11-21: "Add an Upgrade tab to the buildings ... upgrade to the next tier
/// or add modular improvements". Then: "bring it up to the same standard as ... crafting tab and horse UI ... proper art work. Not just
/// boxes of text and plain buttons".
/// <list type="bullet">
/// <item>On the left, the next tier as a card: its numeral and name, what it brings (each line with its picture: what it makes, what it
/// opens, the modules it teaches, a new slot, its bonuses), what it still needs, each material held against its cost, and the button.</item>
/// <item>On the right, the building's module slots as cards (fitted with a × to take it off, placed beside it, free, or the tier that
/// opens it), and every module known that fits, each with its painting, what it does, its cost and a Fit button.</item>
/// </list>
/// Built in code from the UI kit (HearthKit, KitButton); the rules are Construction's and BuildingModules'.
/// </summary>
public sealed class StationUpgradeTab
{
    private const float Gap = 16f;
    private const float LeftWidth = 440f;
    private const float Pad = 22f;
    private const float SlotHeight = 132f;
    private const float RowHeight = 100f;
    private const int MostBrings = 5;
    private const float BringsTop = 188f;
    private const float BringsStep = 34f;
    private const int MostChips = 8;

    private readonly RectTransform page;
    private readonly Action changed;
    private Vector2 builtFor;

    // The next tier.
    private RectTransform left;
    private TMP_Text leftHeading;
    private TMP_Text numeral;
    private TMP_Text tierTitle;
    private TMP_Text tierLine;
    private TMP_Text flavour;
    private readonly List<(GameObject root, Image icon, TMP_Text text)> brings = new List<(GameObject, Image, TMP_Text)>();
    private GameObject costBox;
    private TMP_Text costHeading;
    private int bringsFit;
    private readonly List<(GameObject root, Image icon, TMP_Text text)> chips = new List<(GameObject, Image, TMP_Text)>();
    private RectTransform progressFill;
    private Image progressImage;
    private GameObject progressRoot;
    private KitButton upgradeButton;

    // The modules.
    private RectTransform right;
    private TMP_Text slotCount;
    private readonly List<SlotCard> slots = new List<SlotCard>();
    private GameObject fitsHeading;
    private RectTransform rowsArea;
    private RectTransform rows;
    private readonly List<ModuleRow> moduleRows = new List<ModuleRow>();
    private TMP_Text emptyNote;
    private float rightWidth;

    private BuildingObject building;
    private GameObject user;

    public StationUpgradeTab(RectTransform page, Action changed)
    {
        this.page = page;
        this.changed = changed;
    }

    /// <summary>Draws the tab for <paramref name="shown"/> as it stands now (built again when the page's size changed).</summary>
    public void Show(BuildingObject shown, GameObject viewer)
    {
        building = shown;
        user = viewer;
        Vector2 size = page.sizeDelta;
        if (left == null || size != builtFor)
        {
            Build(size);
        }

        if (building == null || user == null || !building.TryGetComponent(out Construction construction))
        {
            return;
        }

        bool mayChange = !user.TryGetComponent(out BuildPlacer builder) || builder.AllowsChanges;
        ShowTier(construction, mayChange);
        ShowModules(construction, mayChange);
    }

    // ----- Building it -----

    private sealed class SlotCard
    {
        public GameObject Root;
        public Image Face;
        public Image Rim;
        public Image Picture;
        public Image Mark;
        public TMP_Text Name;
        public TMP_Text State;
        public Button Remove;
        public BuildingModuleDefinition Module;
    }

    private sealed class ModuleRow
    {
        public GameObject Root;
        public Image Picture;
        public TMP_Text Name;
        public TMP_Text Effect;
        public TMP_Text Note;
        public readonly List<(GameObject root, Image icon, TMP_Text text)> Chips = new List<(GameObject, Image, TMP_Text)>();
        public KitButton Fit;
    }

    private void Build(Vector2 size)
    {
        foreach (Transform child in page)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        brings.Clear();
        chips.Clear();
        slots.Clear();
        moduleRows.Clear();
        builtFor = size;
        UIStyle style = UIStyle.Current;
        float height = size.y;

        // The next tier.
        left = HearthKit.Card(page, "Next Tier Card", 0f, 0f, LeftWidth, height);
        leftHeading = HearthKit.Section(left, "Next tier", "Hammer", Pad, 16f);
        RectTransform medal = HearthKit.Card(left, "Tier Medal", Pad, 60f, 96f, 96f, 0.95f, Color.Lerp(style.Inset, style.AccentDeep, 0.45f), 2.2f);
        numeral = CodeUI.Label("Numeral", medal, 0f, 0f, 96f, 96f, 46f, style.AccentBright, TextAlignmentOptions.Center);
        style.Display(numeral);
        numeral.overflowMode = TextOverflowModes.Overflow;
        float textX = Pad + 96f + 18f;
        tierTitle = CodeUI.Label("Tier Name", left, textX, 66f, LeftWidth - textX - Pad, 38f, 26f, style.Text);
        style.Title(tierTitle);
        tierTitle.enableAutoSizing = true;
        tierTitle.fontSizeMin = 18f;
        tierTitle.fontSizeMax = 26f;
        tierLine = CodeUI.Label("Tier Line", left, textX, 104f, LeftWidth - textX - Pad, 24f, 16f, style.Accent);
        flavour = CodeUI.Paragraph("Flavour", left, textX, 128f, LeftWidth - textX - Pad, 40f, 14f, style.Muted);
        flavour.fontStyle = FontStyles.Italic;
        flavour.enableAutoSizing = true;
        flavour.fontSizeMin = 11f;
        flavour.fontSizeMax = 14f;
        HearthKit.Rule(left, Pad, 176f, LeftWidth - Pad * 2f, 0.25f);
        float costTop = height - 74f - 112f;
        bringsFit = Mathf.Clamp(Mathf.FloorToInt((costTop - 6f - BringsTop) / BringsStep), 1, MostBrings);
        for (int i = 0; i < MostBrings; i++)
        {
            RectTransform row = CodeUI.At("Brings " + i, left, Pad, BringsTop + i * BringsStep, LeftWidth - Pad * 2f, 32f);
            Image icon = PictureOf(row, 0f, 1f, 30f);
            TMP_Text text = CodeUI.Label("Text", row, 42f, 0f, LeftWidth - Pad * 2f - 42f, 32f, 17f, style.Text);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 13f;
            text.fontSizeMax = 17f;
            brings.Add((row.gameObject, icon, text));
        }

        RectTransform costRect = CodeUI.At("Cost Heading", left, 0f, costTop, LeftWidth, 34f);
        costBox = costRect.gameObject;
        costHeading = HearthKit.Section(costRect, "Cost", "Coin", Pad, 0f);
        for (int i = 0; i < MostChips; i++)
        {
            chips.Add(Chip(left, Pad + (i % 4) * 100f, costTop + 38f + (i / 4) * 40f, 94f, 34f, 26f, 16f));
        }

        progressRoot = CodeUI.At("Progress", left, Pad, costTop + 40f, LeftWidth - Pad * 2f, 14f).gameObject;
        progressFill = KitBar.Make(progressRoot.transform, 0f, 0f, LeftWidth - Pad * 2f, 12f, out progressImage);
        upgradeButton = new KitButton(left, Pad, height - 72f, LeftWidth - Pad * 2f, 54f, Choose, KitButtonShape.Compact);

        // The modules.
        float rx = LeftWidth + Gap;
        rightWidth = size.x - rx;
        right = HearthKit.Card(page, "Modules Card", rx, 0f, rightWidth, height);
        HearthKit.Section(right, "Modules", "Gear", Pad, 16f);
        slotCount = CodeUI.Label("Slots", right, rightWidth - Pad - 260f, 16f, 260f, 30f, 18f, style.Muted, TextAlignmentOptions.Right);
        float slotWidth = (rightWidth - Pad * 2f - 4f * 10f) / 5f;
        for (int i = 0; i < BuildingModules.MostSlots; i++)
        {
            slots.Add(Slot(right, Pad + i * (slotWidth + 10f), 58f, slotWidth));
        }

        float listTop = 58f + SlotHeight + 20f;
        RectTransform fitsRect = CodeUI.At("Fits Heading", right, 0f, listTop, rightWidth, 34f);
        fitsHeading = fitsRect.gameObject;
        HearthKit.Section(fitsRect, "Fits here", "Star", Pad, 0f);
        rowsArea = CodeUI.At("Fits", right, Pad, listTop + 40f, rightWidth - Pad * 2f, height - listTop - 40f - 14f);
        var scroll = rowsArea.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        rowsArea.gameObject.AddComponent<RectMask2D>();
        rowsArea.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        rows = CodeUI.At("Rows", rowsArea, 0f, 0f, rightWidth - Pad * 2f, 0f);
        scroll.content = rows;
        scroll.viewport = rowsArea;
        emptyNote = CodeUI.Paragraph("Empty", rowsArea, 44f, 6f, rightWidth - Pad * 2f - 44f, 90f, 17f, style.Muted);
        HearthKit.Icon(emptyNote.transform, "Info", -44f, 0f, 30f);
    }

    private static Image PictureOf(Transform parent, float x, float y, float size)
    {
        Image image = CodeUI.At("Picture", parent, x, y, size, size).gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // A material against its cost: its picture and "held/needed", green when there is enough, red when not.
    private static (GameObject, Image, TMP_Text) Chip(Transform parent, float x, float y, float width, float height, float iconSize, float textSize)
    {
        UIStyle style = UIStyle.Current;
        RectTransform chip = HearthKit.Card(parent, "Cost Chip", x, y, width, height, 0.25f, style.Inset, 1.6f);
        Image icon = PictureOf(chip, 6f, (height - iconSize) * 0.5f, iconSize);
        TMP_Text text = CodeUI.Label("Amount", chip, iconSize + 10f, 0f, width - iconSize - 14f, height, textSize, style.Text);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = true;
        return (chip.gameObject, icon, text);
    }

    private SlotCard Slot(Transform parent, float x, float y, float width)
    {
        UIStyle style = UIStyle.Current;
        var card = new SlotCard();
        RectTransform rect = CodeUI.At("Slot", parent, x, y, width, SlotHeight);
        card.Root = rect.gameObject;
        card.Face = HearthKit.Sliced(card.Root.AddComponent<Image>(), style.Card, 1.3f);
        card.Face.raycastTarget = false;
        card.Rim = HearthKit.Rim(rect, 0.4f, 1.3f);
        card.Picture = PictureOf(rect, (width - 56f) * 0.5f, 16f, 56f);
        card.Mark = HearthKit.Icon(rect, "Lock", (width - 34f) * 0.5f, 26f, 34f);
        card.Name = CodeUI.Label("Name", rect, 6f, 78f, width - 12f, 26f, 15f, style.Text, TextAlignmentOptions.Center);
        card.Name.enableAutoSizing = true;
        card.Name.fontSizeMin = 11f;
        card.Name.fontSizeMax = 15f;
        card.State = CodeUI.Label("State", rect, 6f, 102f, width - 12f, 22f, 13f, style.Muted, TextAlignmentOptions.Center);
        card.Remove = HearthKit.CloseButton(rect, width - 28f, 3f, () => RemoveFrom(card));
        card.Remove.transform.localScale = Vector3.one * 0.54f;
        return card;
    }

    private ModuleRow Row()
    {
        UIStyle style = UIStyle.Current;
        float width = rightWidth - Pad * 2f;
        var row = new ModuleRow();
        RectTransform rect = HearthKit.Card(rows, "Module", 0f, moduleRows.Count * (RowHeight + 8f), width, RowHeight, 0.3f);
        row.Root = rect.gameObject;
        RectTransform frame = HearthKit.Card(rect, "Picture Frame", 12f, 12f, 76f, 76f, 0.55f, style.Panel, 1.6f);
        row.Picture = PictureOf(frame, 6f, 6f, 64f);
        float textX = 104f;
        float textWidth = width - textX - 160f;
        row.Name = CodeUI.Label("Name", rect, textX, 8f, textWidth, 30f, 20f, style.Text);
        style.Title(row.Name);
        row.Effect = CodeUI.Paragraph("Effect", rect, textX, 36f, textWidth, 34f, 14f, style.Muted);
        row.Effect.enableAutoSizing = true;
        row.Effect.fontSizeMin = 11f;
        row.Effect.fontSizeMax = 14f;
        row.Note = CodeUI.Label("Note", rect, textX, 70f, textWidth, 24f, 14f, style.Danger);
        for (int i = 0; i < 4; i++)
        {
            row.Chips.Add(Chip(rect, textX + i * 70f, 70f, 66f, 24f, 20f, 13f));
        }

        row.Fit = new KitButton(rect, width - 146f, (RowHeight - 46f) * 0.5f, 132f, 46f, Choose, KitButtonShape.Compact);
        moduleRows.Add(row);
        return row;
    }

    // ----- The next tier -----

    private void ShowTier(Construction construction, bool mayChange)
    {
        UIStyle style = UIStyle.Current;
        BuildingDefinition definition = building.Definition;
        PlayerCrafting crafting = user.GetComponent<PlayerCrafting>();
        MaterialPool pool = MaterialPool.For(user);
        int level = Mathf.Max(1, construction.Level);
        BuildingLevel next = definition.UpgradeTo(level + 1);
        bool raising = construction.IsBuilding && construction.Level >= 1;
        int shownTier = raising ? construction.Target : next != null ? level + 1 : level;
        BuildingLevel shown = definition.UpgradeTo(shownTier);

        leftHeading.text = raising ? "Being raised" : next != null ? "Next tier" : "Its tier";
        numeral.text = Tiers.ToRoman(shownTier);
        tierTitle.text = shown != null ? $"{shown.title} {definition.DisplayName}" : construction.Title;
        tierLine.text = raising ? $"Tier {Tiers.ToRoman(level)} to {Tiers.ToRoman(shownTier)}: hammer at its scaffold"
            : next != null ? $"Tier {Tiers.ToRoman(level)} of {Tiers.ToRoman(definition.MaxLevel)} now"
            : $"Tier {Tiers.ToRoman(level)} of {Tiers.ToRoman(definition.MaxLevel)}: as high as it goes";
        flavour.text = shown != null && !string.IsNullOrWhiteSpace(shown.description) ? shown.description
            : next == null ? BuildingModules.TakesModules(definition) ? "From here it grows through its modules." : "Nothing higher to build." : "";

        // What it brings, each with its picture.
        var lines = new List<(Sprite picture, string icon, string text)>();
        if (shown != null && (next != null || raising))
        {
            List<RecipeDefinition> makes = crafting != null && definition.Station != CraftingStationType.None
                ? crafting.Recipes.Where(r => r != null && r.Station == definition.Station && r.RequiredStationTier == shownTier && crafting.IsKnown(r)).ToList()
                : new List<RecipeDefinition>();
            if (makes.Count > 0)
            {
                string names = string.Join(", ", makes.Take(2).Select(r => r.DisplayName));
                lines.Add((makes[0].Output != null ? makes[0].Output.Icon : makes[0].OutputResource != null ? makes[0].OutputResource.Icon : null, "Hammer",
                    $"Makes {names}{(makes.Count > 2 ? $" and {makes.Count - 2} more" : "")}"));
            }

            List<string> opens = crafting != null ? BuildingUnlocks.OpenedBy(definition, shownTier, crafting.Buildings) : new List<string>();
            if (opens.Count > 0)
            {
                lines.Add((null, "House", $"Opens {string.Join(", ", opens)}"));
            }

            foreach (BuildingModuleDefinition module in shown.unlocksModules.Where(m => m != null))
            {
                lines.Add((module.Icon, "Gear", $"Teaches the {module.DisplayName}"));
            }

            if (BuildingModules.TakesModules(definition) && shownTier <= BuildingModules.MostSlots && shownTier > StationExtensions.Slots(building))
            {
                lines.Add((null, "Gear", "One more module slot"));
            }

            string bonuses = HavenBuildings.BonusesOf(shown);
            if (bonuses.Length > 0)
            {
                string change = Changes(shown);
                lines.Add((null, "Stats", char.ToUpperInvariant(bonuses[0]) + bonuses.Substring(1) + (change.Length > 0 ? "\n" + change : "")));
            }
        }

        string missing = next != null && !raising ? BuildingUnlocks.Missing(next) : "";
        if (!string.IsNullOrEmpty(missing))
        {
            // Kept in view: what blocks the upgrade goes before what would not fit.
            lines.Insert(Mathf.Min(lines.Count, bringsFit - 1), (null, "Lock", missing));
        }

        for (int i = 0; i < brings.Count; i++)
        {
            bool used = i < lines.Count && i < bringsFit;
            brings[i].root.SetActive(used);
            if (!used)
            {
                continue;
            }

            Sprite picture = lines[i].picture != null ? lines[i].picture : style.Icon(lines[i].icon);
            brings[i].icon.sprite = picture;
            brings[i].icon.enabled = picture != null;
            bool blocked = lines[i].icon == "Lock";
            brings[i].icon.color = lines[i].picture != null ? Color.white : blocked ? style.Danger : HearthKit.IconColour(lines[i].icon);
            brings[i].text.text = blocked ? lines[i].text : HearthKit.ToneNumbers(lines[i].text);
            brings[i].text.color = blocked ? style.Danger : style.Text;
        }

        // Each material against what it costs, or the scaffold's progress.
        bool costShown = next != null && !raising;
        costBox.SetActive(costShown || raising);
        costHeading.text = raising ? "Raised so far" : "Cost";
        int chip = 0;
        if (costShown)
        {
            foreach (ItemAmount cost in next.cost.Where(c => c.Item != null))
            {
                SetChip(chips, chip++, cost.Item.Icon, pool.Have(cost.Item), cost.Amount);
            }

            foreach (ResourceAmount cost in next.essence.Where(c => c.Resource != null))
            {
                SetChip(chips, chip++, cost.Resource.Icon, pool.Have(cost.Resource), cost.Amount);
            }
        }

        for (int i = chip; i < chips.Count; i++)
        {
            chips[i].root.SetActive(false);
        }

        progressRoot.SetActive(raising);
        if (raising)
        {
            float share = construction.WorkNeeded > 0f ? Mathf.Clamp01(construction.Work / construction.WorkNeeded) : 0f;
            KitBar.Set(progressFill, progressImage, share, style.Good);
        }

        if (costShown)
        {
            bool affordable = pool.CanAfford(next.essence, next.cost, out string lacking);
            bool open = BuildingUnlocks.IsOpen(next);
            string why = !mayChange ? HavenVisit.Refusal : construction.IsFitting ? $"Finish fitting the {construction.Fitting.DisplayName} first"
                : !open ? missing : !affordable ? lacking : "";
            upgradeButton.Bind(new MenuAction($"Upgrade to Tier {Tiers.ToRoman(level + 1)}", why, mayChange && !construction.IsFitting && open && affordable,
                () => construction.TryOrderUpgrade(user)) { Icon = "Hammer" });
        }
        else
        {
            upgradeButton.Bind(null);
        }
    }

    private static void SetChip(List<(GameObject root, Image icon, TMP_Text text)> pool, int index, Sprite picture, int have, int need)
    {
        if (index >= pool.Count)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        pool[index].root.SetActive(true);
        pool[index].icon.sprite = picture;
        pool[index].icon.enabled = picture != null;
        string colour = UIStyle.Hex(have >= need ? style.Good : style.Danger);
        pool[index].text.text = $"<color={colour}>{Mathf.Min(have, 9999)}</color><color={UIStyle.Hex(style.Muted)}>/</color>{need}";
    }

    // ----- The modules -----

    private void ShowModules(Construction construction, bool mayChange)
    {
        UIStyle style = UIStyle.Current;
        BuildingDefinition definition = building.Definition;
        bool takes = BuildingModules.TakesModules(definition);
        right.gameObject.SetActive(true);
        int open = StationExtensions.Slots(building);
        IReadOnlyList<BuildingModuleDefinition> fitted = construction.Fitted;
        IReadOnlyList<BuildingObject> placed = StationExtensions.Of(building);
        int used = fitted.Count + placed.Count + (construction.IsFitting ? 1 : 0);
        slotCount.text = takes ? HearthKit.ToneNumbers($"{used} of {open} slot{(open == 1 ? "" : "s")} used") : "";
        int cards = takes ? Mathf.Clamp(Mathf.Max(open, definition.MaxLevel), 1, BuildingModules.MostSlots) : 0;
        for (int i = 0; i < slots.Count; i++)
        {
            SlotCard card = slots[i];
            card.Root.SetActive(i < cards);
            if (i >= cards)
            {
                continue;
            }

            card.Module = null;
            card.Remove.gameObject.SetActive(false);
            if (i < fitted.Count)
            {
                BuildingModuleDefinition module = fitted[i];
                card.Module = module;
                ShowSlot(card, module.Icon != null ? module.Icon : style.Icon("Gear"), null, module.DisplayName, "Fitted", true);
                card.Remove.gameObject.SetActive(mayChange && !HavenVisit.IsVisiting);
            }
            else if (i == fitted.Count && construction.IsFitting && i < open)
            {
                BuildingModuleDefinition module = construction.Fitting;
                ShowSlot(card, module.Icon != null ? module.Icon : style.Icon("Gear"), null, module.DisplayName,
                    GameInput.WithRebinds($"Hold {{key:Hammer}}: {Mathf.RoundToInt(construction.Share * 100f)}%"), false);
                card.Rim.color = HearthKit.Faded(style.Accent, 0.6f);
            }
            else if (i < used)
            {
                BuildingObject extension = placed[i - fitted.Count - (construction.IsFitting ? 1 : 0)];
                ShowSlot(card, extension.Definition != null ? extension.Definition.Icon : null, "House", extension.Definition != null ? extension.Definition.DisplayName : "Extension",
                    "Placed beside it", true);
            }
            else if (i < open)
            {
                ShowSlot(card, null, "Spark", "Free slot", "Fit one below", false);
            }
            else
            {
                ShowSlot(card, null, "Lock", $"Tier {Tiers.ToRoman(i + 1)}", "opens this slot", false);
                card.Face.color = style.Disabled;
                card.Rim.color = HearthKit.Faded(style.Inactive, 0.3f);
                card.Name.color = style.Inactive;
            }
        }

        fitsHeading.SetActive(takes);
        rowsArea.gameObject.SetActive(true);

        // What fits: what can be fitted now first, then by the tier it needs.
        List<BuildingModuleDefinition> known = takes
            ? BuildingModules.All.Where(m => m != null && m.Fits(definition) && !fitted.Contains(m) && m != construction.Fitting && BuildingModules.IsKnown(m))
                .OrderBy(m => BuildingModules.WhyNot(building, m) == null ? 0 : 1).ThenBy(m => m.Tier).ThenBy(m => m.DisplayName).ToList()
            : new List<BuildingModuleDefinition>();
        MaterialPool pool = MaterialPool.For(user);
        while (moduleRows.Count < known.Count)
        {
            Row();
        }

        for (int i = 0; i < moduleRows.Count; i++)
        {
            ModuleRow row = moduleRows[i];
            row.Root.SetActive(i < known.Count);
            if (i < known.Count)
            {
                ShowRow(row, known[i], pool, mayChange);
            }
        }

        rows.sizeDelta = new Vector2(rows.sizeDelta.x, known.Count * (RowHeight + 8f));
        emptyNote.gameObject.SetActive(known.Count == 0);
        emptyNote.text = !takes ? $"The {definition.DisplayName} takes no modules."
            : $"No modules known for the {definition.DisplayName} yet. Quests and higher tiers teach them, and each is announced when it is learned.";
    }

    private static void ShowSlot(SlotCard card, Sprite picture, string mark, string name, string state, bool filled)
    {
        UIStyle style = UIStyle.Current;
        card.Picture.sprite = picture;
        card.Picture.enabled = picture != null;
        card.Mark.sprite = picture == null ? style.Icon(mark) : null;
        card.Mark.enabled = picture == null && card.Mark.sprite != null;
        card.Mark.color = HearthKit.Faded(HearthKit.IconColour(mark), filled ? 1f : 0.6f);
        card.Face.color = filled ? Color.Lerp(style.Inset, style.AccentDeep, 0.25f) : style.Inset;
        card.Rim.color = HearthKit.Faded(style.Accent, filled ? 0.8f : 0.3f);
        card.Name.text = name;
        card.Name.color = filled ? style.Text : style.Muted;
        card.State.text = state;
        card.State.color = filled ? style.Accent : style.Muted;
    }

    private void ShowRow(ModuleRow row, BuildingModuleDefinition module, MaterialPool pool, bool mayChange)
    {
        UIStyle style = UIStyle.Current;
        row.Picture.sprite = module.Icon != null ? module.Icon : style.Icon("Gear");
        row.Picture.color = module.Icon != null ? Color.white : style.Accent;
        row.Name.text = module.DisplayName;
        string change = Changes(module.Bonus);
        row.Effect.text = HearthKit.ToneNumbers(module.Effect()) + (change.Length > 0 ? $"\n<color={UIStyle.Hex(style.Accent)}>{change}</color>" : "");
        string why = BuildingModules.WhyNot(building, module);
        bool spare = GameSession.Home.SpareCount(module.Id) > 0;
        bool affordable = spare || pool.CanAfford(module.Essence, module.Cost, out _);
        row.Note.gameObject.SetActive(why != null || spare);
        row.Note.text = GameInput.WithRebinds(why ?? "A spare waits in store: fitting it is free");
        row.Note.color = why != null ? style.Danger : style.Good;
        int chip = 0;
        if (why == null && !spare)
        {
            foreach (ItemAmount cost in module.Cost.Where(c => c.Item != null))
            {
                SetChip(row.Chips, chip++, cost.Item.Icon, pool.Have(cost.Item), cost.Amount);
            }
        }

        for (int i = chip; i < row.Chips.Count; i++)
        {
            row.Chips[i].root.SetActive(false);
        }

        BuildingObject at = building;
        GameObject by = user;
        row.Fit.Bind(new MenuAction(spare ? "Fit spare" : "Fit", "", why == null && affordable && mayChange, () => BuildingModules.Fit(at, module, by)) { Icon = "Gear" });
    }

    // ----- Before and after -----

    // What a bonus would change here, as numbers before and after ("Output slots 1 → 2 · Room 10 → 15"): players judge an upgrade by
    // its change (the systems review, 2026-11-21). At most three, the ones the station shows.
    private string Changes(BuildingBonus bonus)
    {
        if (bonus == null || building == null || !building.TryGetComponent(out Construction construction))
        {
            return "";
        }

        var parts = new List<string>();
        void Add(string name, float now, float more, string format = "0", float most = float.MaxValue)
        {
            float after = Mathf.Min(now + more, most);
            if (more > 0f && after > now + 0.001f && parts.Count < 3)
            {
                parts.Add($"{name} {now.ToString(format)} → {after.ToString(format)}");
            }
        }

        if (building.TryGetComponent(out CraftingStation station) && station.Queue != null)
        {
            StationQueue queue = station.Queue;
            Add("Output slots", queue.OutputSlots, bonus.extraOutputSlots);
            Add("Room", queue.Capacity, bonus.extraOutput);
            Add("Queue", queue.MaxQueued, bonus.extraQueue);
            Add("Luck", queue.ExtraChance * 100f, bonus.extraChance * 100f, "0'%'", BuildingModules.MostLuck * 100f);
            Add("Fine work", queue.FineChance * 100f, bonus.fineChance * 100f, "0'%'", BuildingModules.MostLuck * 100f);
            Add("Speed", (1f + construction.Sum(b => b.craftSpeed)) * 100f, bonus.craftSpeed * 100f, "0'%'");
        }

        if (bonus.healthShare > 0f && building.TryGetComponent(out Damageable health))
        {
            float share = construction.Sum(b => b.healthShare);
            float plain = health.MaxHealth / (1f + share);
            Add("Health", health.MaxHealth, plain * bonus.healthShare);
        }

        Add("Comfort", construction.Sum(b => b.comfort), bonus.comfort);
        Add("Beds", building.Definition.Beds + construction.Sum(b => b.beds), bonus.beds);
        Add("Light", construction.Sum(b => b.extraLight), bonus.extraLight, "0' m'");
        Add("Foretold", HavenReadiness.ForetoldEvents, bonus.foretoldEvents);
        return string.Join("  ·  ", parts);
    }

    // ----- Choosing -----

    private void Choose(MenuAction action)
    {
        if (action == null || !action.Enabled)
        {
            return;
        }

        action.Chosen?.Invoke();
        changed?.Invoke();
    }

    private void RemoveFrom(SlotCard card)
    {
        if (card.Module != null && building != null && BuildingModules.Remove(building, card.Module))
        {
            changed?.Invoke();
        }
    }
}
