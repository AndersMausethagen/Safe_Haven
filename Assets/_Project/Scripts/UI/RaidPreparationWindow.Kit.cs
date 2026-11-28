using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The planner's second page in the left column, "Your kit" (the user, 2026-11-28: "there is little chance for a player to set up
/// what they bring on a raid: which weapons, gear, skills"): what goes through the portal, as pictures, each changed in place. A
/// click on a slot opens a picker over the page (Hunt: Showdown's and Darktide's loadout: click the slot, pick from a list, compare
/// on hover) with every piece that fits it from the bag and, at home, the stash; a skill slot offers the known skills of its kind,
/// a consumable slot the usable things in the bag. The gear score, the bag's fill and "Pack as last time" sit at the foot. Nothing
/// here has rules of its own: gear goes on through PlayerEquipment, skills through PlayerSkills, as in the C and K windows.
/// </summary>
public sealed partial class RaidPreparationWindow
{
    private enum LeftPage
    {
        Place = 0,
        Kit = 1,
    }

    private static readonly GearSlot[] HandSlots = { GearSlot.MainHand, GearSlot.OffHand, GearSlot.Light, GearSlot.AxeTool, GearSlot.PickaxeTool, GearSlot.SickleTool };
    private static readonly GearSlot[] WornSlots = { GearSlot.Head, GearSlot.Neck, GearSlot.Chest, GearSlot.Hands, GearSlot.Waist, GearSlot.Feet, GearSlot.Finger1, GearSlot.Backpack };
    private const float PageSwitchHeight = 36f;
    private const float PickerCard = 76f;

    private LeftPage leftPage = LeftPage.Place;
    private KitTabs leftTabs;
    private GameObject kitPage;
    private readonly List<KitPictureCard> handCards = new List<KitPictureCard>();
    private readonly List<KitPictureCard> wornCards = new List<KitPictureCard>();
    private readonly List<KitPictureCard> barCards = new List<KitPictureCard>();
    private TMP_Text kitGear;
    private TMP_Text kitBag;

    // The picker: one slot's choices over the page.
    private GameObject picker;
    private TMP_Text pickerTitle;
    private RectTransform pickerContent;
    private readonly List<KitPictureCard> pickerCards = new List<KitPictureCard>();
    private readonly List<System.Action> pickerChoices = new List<System.Action>();
    private readonly List<System.Action> pickerTips = new List<System.Action>();
    private System.Action reopenPicker;

    // The pages' tabs sit at the column's right on its first heading's line, taking no height from the briefing (whose last row ran
    // into the figures below when they had a line of their own).
    private float LeftTop => BodyTop;

    private void BuildKit(UIStyle style)
    {
        const float tabWidth = 120f;
        leftTabs = new KitTabs(panel, Margin + ColumnWidth - tabWidth * 2f - 8f, BodyTop - 4f, tabWidth, PageSwitchHeight - 6f, new[] { "The place", "Your kit" }, ShowLeft, 8f);
        RectTransform page = At("Kit", panel, Margin, LeftTop, ColumnWidth, BodyBottom - LeftTop);
        kitPage = page.gameObject;
        float y = 0f;
        Heading("Weapons and tools", page, 0f, y, ColumnWidth, style);
        y += HeadingHeight + 4f;
        y = Row(page, handCards, HandSlots.Length, 84f, 62f, 2, y, index => GearCard(HandSlots[index]));
        Heading("Worn", page, 0f, y + 6f, ColumnWidth, style);
        y += HeadingHeight + 10f;
        y = Row(page, wornCards, WornSlots.Length, 64f, 56f, 1, y, index => GearCard(WornSlots[index]));
        Heading("Skill bar", page, 0f, y + 6f, ColumnWidth, style);
        y += HeadingHeight + 10f;
        y = Row(page, barCards, SkillLoadout.SlotCount, 56f, 56f, 0, y, BarCard);
        kitGear = Label("Gear", page, 0f, y + 8f, ColumnWidth, 26f, 17f, style.Text);
        kitBag = Label("Bag", page, 0f, y + 36f, ColumnWidth - 220f, 26f, 16f, style.Muted);
        AddButton("Pack", page, ColumnWidth - 210f, y + 30f, 210f, UIRole.Kind.Button, PackAgain, out _).text = "Pack as last time";
        BuildPicker(page, style);
        kitPage.SetActive(false);
    }

    // A row of cards spread over the column; returns the height below it.
    private float Row(Transform parent, List<KitPictureCard> cards, int count, float width, float pictureHeight, int nameLines, float y, System.Func<int, System.Action> clicked)
    {
        float step = count > 1 ? (ColumnWidth - width) / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var card = new KitPictureCard(parent, "Slot " + i, width, pictureHeight, nameLines, 13f, () => KitTip(cards, index), HideTip);
            card.Place(i * step, y);
            Clickable(card, () => clicked(index)?.Invoke());
            cards.Add(card);
        }

        return y + (cards.Count > 0 ? cards[0].Height : 0f) + 8f;
    }

    private static void Clickable(KitPictureCard card, System.Action chosen)
    {
        var button = card.Root.gameObject.AddComponent<Button>();
        button.targetGraphic = card.Root.GetComponent<Image>();
        button.onClick.AddListener(() => chosen());
    }

    private void ShowLeft(int shown)
    {
        leftPage = (LeftPage)shown;
        ClosePicker();
        Refresh();
    }

    private void BuildPicker(Transform page, UIStyle style)
    {
        // Under the pages' tabs, so they still turn the page with the picker open.
        RectTransform rect = HearthKit.Card(page, "Picker", -8f, PageSwitchHeight - 8f, ColumnWidth + 16f, BodyBottom - LeftTop - PageSwitchHeight + 8f, 0.95f, style.Panel, 1.4f);
        picker = rect.gameObject;
        pickerTitle = Label("Title", rect, 18f, 12f, ColumnWidth - 80f, 30f, 20f, style.AccentBright);
        style.Title(pickerTitle);
        AddButton("Back", rect, ColumnWidth - 92f, 10f, 96f, UIRole.Kind.Button, ClosePicker, out _).text = "Back";
        RectTransform view = At("View", rect, 14f, 54f, ColumnWidth - 12f, BodyBottom - LeftTop - PageSwitchHeight - 64f);
        view.gameObject.AddComponent<Image>().color = Color.clear;
        view.gameObject.AddComponent<RectMask2D>();
        pickerContent = At("Content", view, 0f, 0f, ColumnWidth - 12f, 10f);
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = pickerContent;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        picker.SetActive(false);
    }

    private void ClosePicker()
    {
        reopenPicker = null;
        if (picker != null)
        {
            picker.SetActive(false);
        }

        HideTip();
    }

    // ----- Drawing -----

    private void DrawKit()
    {
        UIStyle style = UIStyle.Current;
        GameObject player = plan.Player;
        PlayerEquipment equipment = player != null ? player.GetComponent<PlayerEquipment>() : null;
        PlayerSkills skills = player != null ? player.GetComponent<PlayerSkills>() : null;
        for (int i = 0; i < HandSlots.Length; i++)
        {
            ShowGear(handCards[i], equipment, HandSlots[i]);
        }

        for (int i = 0; i < WornSlots.Length; i++)
        {
            ShowGear(wornCards[i], equipment, WornSlots[i]);
            // A worn card is too narrow for a piece's name: it says its place, the piece is on hover.
            wornCards[i].SetName(GearSlots.Label(WornSlots[i]));
        }

        for (int i = 0; i < barCards.Count; i++)
        {
            KitPictureCard card = barCards[i];
            SkillDefinition skill = skills != null ? skills.Loadout.Get(i) : null;
            ItemDefinition item = skills != null ? skills.Loadout.GetItem(i) : null;
            Sprite picture = skill != null ? skill.Icon : item != null ? item.Icon : null;
            card.ShowPicture(picture, i < SkillLoadout.SkillSlotCount ? "Spark" : "Pot");
            card.SetLook(picture == null, skill != null ? SkillKinds.Colour(skill.Kind) : (Color?)null);
            int held = item != null && inventory != null ? inventory.CountCarried(item) : 0;
            card.SetBadge(item != null ? held.ToString() : picture == null ? SkillLoadout.SlotBadge(i) : (i + 1).ToString());
        }

        int score = player != null ? GearScore.Of(player) : 0;
        RaidDifficulty tier = plan.Difficulty;
        Color colour = tier == null ? style.Text : score >= tier.recommendedGearScore ? style.Good : score >= tier.minimumGearScore ? style.Accent : style.Warning;
        kitGear.text = tier != null
            ? $"Gear score <color={UIStyle.Hex(colour)}><b>{score}</b></color>  ·  tier {Tiers.ToRoman(tier.tier)} recommends {tier.recommendedGearScore}"
            : $"Gear score <b>{score}</b>";
        ItemContainer bag = inventory != null ? inventory.Container : null;
        kitBag.text = bag != null
            ? $"Bag {bag.CountItems()} of {bag.SlotCount}" + (bag.HasWeightLimit ? $"  ·  {bag.CurrentWeight:0} / {bag.WeightCapacity:0} weight" : "")
            : "";
        reopenPicker?.Invoke();
    }

    private static void ShowGear(KitPictureCard card, PlayerEquipment equipment, GearSlot slot)
    {
        UIStyle style = UIStyle.Current;
        ItemStack worn = equipment != null ? equipment.Gear.Get(slot) : ItemStack.Empty;
        card.ShowPicture(worn.IsEmpty ? null : worn.Item.Icon, IconOf(slot));
        card.SetName(worn.IsEmpty ? GearSlots.Label(slot) : worn.Item.DisplayName);
        card.SetLook(worn.IsEmpty, worn.IsEmpty ? (Color?)null : worn.Item.Rarity > ItemRarity.Common ? Rarity.ColorOf(worn.Item.Rarity) : (Color?)null);
        card.SetBadge(worn.IsEmpty ? "" : worn.Item.HasDurability && worn.Durability < 0.995f ? $"{Mathf.RoundToInt(worn.Durability * 100f)}%" : "");
    }

    private static string IconOf(GearSlot slot) => slot switch
    {
        GearSlot.MainHand => "Sword",
        GearSlot.OffHand => "Barding",
        GearSlot.Light => "Lantern",
        GearSlot.AxeTool => "Axe",
        GearSlot.PickaxeTool => "Pickaxe",
        GearSlot.SickleTool => "Sickle",
        GearSlot.Backpack => "Bags",
        _ => "Barding",
    };

    private void KitTip(List<KitPictureCard> cards, int index)
    {
        GameObject player = plan.Player;
        if (player == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        if (cards == barCards)
        {
            PlayerSkills skills = player.GetComponent<PlayerSkills>();
            SkillDefinition skill = skills != null ? skills.Loadout.Get(index) : null;
            ItemDefinition item = skills != null ? skills.Loadout.GetItem(index) : null;
            if (skill != null)
            {
                tooltip.Show(skill.DisplayName, SkillKinds.Colour(skill.Kind), $"{skill.Description}\n\n<size=85%>Slot {index + 1}: {SkillLoadout.SlotName(index)}. Click to change.</size>", skill.Icon);
            }
            else if (item != null)
            {
                tooltip.ShowDefinition(item);
            }
            else
            {
                tooltip.Show($"Slot {index + 1}", style.Muted, $"Empty. Takes a {SkillLoadout.SlotName(index).ToLowerInvariant()}. Click to choose one.");
            }

            return;
        }

        GearSlot slot = cards == handCards ? HandSlots[index] : WornSlots[index];
        ItemStack worn = player.TryGetComponent(out PlayerEquipment equipment) ? equipment.Gear.Get(slot) : ItemStack.Empty;
        if (worn.IsEmpty)
        {
            tooltip.Show(GearSlots.Label(slot), style.Muted, "Nothing here. Click to choose from your bag" + (Place.StashUsable ? " and stash." : "."));
        }
        else
        {
            tooltip.Show(worn, "Click to change.");
        }
    }

    // ----- Choosing -----

    private System.Action GearCard(GearSlot slot) => () => OpenGearPicker(slot);

    private System.Action BarCard(int index) => () => OpenBarPicker(index);

    private void OpenGearPicker(GearSlot slot)
    {
        GameObject player = plan.Player;
        if (player == null || !player.TryGetComponent(out PlayerEquipment equipment))
        {
            return;
        }

        reopenPicker = () => FillGearPicker(slot, equipment);
        picker.SetActive(true);
        FillGearPicker(slot, equipment);
    }

    private void FillGearPicker(GearSlot slot, PlayerEquipment equipment)
    {
        UIStyle style = UIStyle.Current;
        pickerTitle.text = GearSlots.Label(slot);
        ItemStack worn = equipment.Gear.Get(slot);
        int wornScore = worn.IsEmpty ? 0 : GearScore.Of(worn);
        BeginPicker();
        if (!worn.IsEmpty)
        {
            pickerEmptyingFirst = true;
            AddChoice(null, "Take off", "Return", style.Muted, "", () => TakeOff(equipment, slot),
                () => tooltip.Show("Take off", style.Muted, $"{worn.DisplayName} goes back to your bag{(Place.StashUsable ? ", or the stash when the bag is full" : "")}."));
        }

        AddFitting(inventory != null ? inventory.Container : null, "Bag");
        if (Place.StashUsable && GameSession.Stash != null)
        {
            AddFitting(GameSession.Stash.Items, "Stash");
        }

        EndPicker($"Nothing else in your bag{(Place.StashUsable ? " or stash" : "")} fits here.");

        void AddFitting(ItemContainer from, string where)
        {
            for (int i = 0; from != null && i < from.SlotCount; i++)
            {
                ItemStack stack = from.GetSlot(i);
                // A stash slot in the block of a chest that is gone is out of reach (review 2026-11-28: it could be equipped from the
                // portal, past the chest rule).
                if (stack.IsEmpty || stack.IsBroken || !GearSlots.Accepts(slot, stack.Item) || !from.IsUnlocked(i))
                {
                    continue;
                }

                int index = i;
                ItemStack listed = stack;
                int delta = GearScore.Of(stack) - wornScore;
                string badge = delta > 0 ? $"+{delta}" : delta < 0 ? delta.ToString() : "=";
                Color rim = stack.Item.Rarity > ItemRarity.Common ? Rarity.ColorOf(stack.Item.Rarity) : HearthKit.Faded(style.Accent, 0.5f);
                AddChoice(stack.Item.Icon, stack.DisplayName, null, rim, badge,
                    () => PutOn(equipment, from, index, listed, slot),
                    () => tooltip.Show(stack, $"From your {where.ToLowerInvariant()}. {(delta > 0 ? $"Gear score +{delta}." : delta < 0 ? $"Gear score {delta}." : "Same gear score.")}"),
                    delta > 0 ? style.Good : delta < 0 ? style.Warning : style.Muted);
            }
        }
    }

    // Puts on the piece listed, if it is still where it was listed (the stash can change under an open picker: a co-op snapshot,
    // workers taking from it), making room in the hands as the bag's own equip does: a two-hander puts the off-hand away first,
    // and an off-hand piece puts a worn two-hander away.
    private void PutOn(PlayerEquipment equipment, ItemContainer from, int index, ItemStack listed, GearSlot slot)
    {
        if (from == null || !from.GetSlot(index).SameKind(listed))
        {
            GameMessages.Post($"{listed.DisplayName} has moved. Choose again.", MessageKind.Warning);
            return;
        }

        ItemDefinition item = listed.Item;
        if (slot == GearSlot.MainHand && item.IsWeapon && item.Weapon.RequiresBothHands && !equipment.OffHand.IsEmpty && !TakeOff(equipment, GearSlot.OffHand))
        {
            return;
        }

        if (slot == GearSlot.OffHand && !equipment.MainHand.IsEmpty && equipment.MainHand.Item.IsWeapon && equipment.MainHand.Item.Weapon.RequiresBothHands
            && !TakeOff(equipment, GearSlot.MainHand))
        {
            return;
        }

        equipment.EquipTo(from, index, slot);
    }

    // To the bag, or at home to the stash when the bag has no room for it.
    private bool TakeOff(PlayerEquipment equipment, GearSlot slot)
    {
        ItemStack worn = equipment.Gear.Get(slot);
        ItemContainer bag = inventory != null ? inventory.Container : null;
        bool bagTakesIt = bag != null && bag.FirstEmptySlotFor(worn.Item) >= 0 && bag.FitsByWeight(worn.Item) >= worn.Count;
        if (!bagTakesIt && Place.StashUsable && GameSession.Stash != null)
        {
            ItemContainer stash = GameSession.Stash.Items;
            for (int i = 0; i < stash.SlotCount; i++)
            {
                // As GearSet.Unequip moves it to the bag, into a free reachable stash slot instead.
                if (stash.IsUnlocked(i) && stash.GetSlot(i).IsEmpty && equipment.Gear.Slots.MoveTo((int)slot, stash, i))
                {
                    GameMessages.Post($"{worn.DisplayName} went to the stash: your bag is full.");
                    return true;
                }
            }
        }

        return equipment.Unequip(slot);
    }

    private void OpenBarPicker(int index)
    {
        GameObject player = plan.Player;
        if (player == null || !player.TryGetComponent(out PlayerSkills skills))
        {
            return;
        }

        reopenPicker = () => FillBarPicker(index, skills);
        picker.SetActive(true);
        FillBarPicker(index, skills);
    }

    private void FillBarPicker(int index, PlayerSkills skills)
    {
        UIStyle style = UIStyle.Current;
        pickerTitle.text = $"Slot {index + 1}: {SkillLoadout.SlotName(index)}";
        BeginPicker();
        if (!skills.Loadout.IsEmpty(index))
        {
            pickerEmptyingFirst = true;
            AddChoice(null, "Clear", "Return", style.Muted, "", () => skills.Loadout.Clear(index), () => tooltip.Show("Clear", style.Muted, "Empties the slot."));
        }

        if (index < SkillLoadout.SkillSlotCount)
        {
            foreach (SkillDefinition skill in skills.KnownSkills)
            {
                if (skill == null || !SkillLoadout.Fits(index, skill))
                {
                    continue;
                }

                SkillDefinition chosen = skill;
                bool here = skills.Loadout.Get(index) == skill;
                AddChoice(skill.Icon, skill.DisplayName, null, SkillKinds.Colour(skill.Kind), here ? "On" : "",
                    () => skills.Equip(index, chosen),
                    () => tooltip.Show(chosen.DisplayName, SkillKinds.Colour(chosen.Kind), chosen.Description, chosen.Icon));
            }

            EndPicker($"No known {SkillLoadout.SlotName(index).ToLowerInvariant()} yet. Learn more at the Skills window (K).");
            return;
        }

        // Consumables: one of each kind in the bag.
        var seen = new HashSet<ItemDefinition>();
        ItemContainer bag = inventory != null ? inventory.Container : null;
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            // Things to use up only (draughts, food, bandages), as the slot says: IsUsable also takes gear, blueprints and mounts.
            if (stack.IsEmpty || !QuickItems.Accepts(stack.Item) || !stack.Item.IsConsumable || stack.Item.IsEquippable || !seen.Add(stack.Item))
            {
                continue;
            }

            ItemDefinition item = stack.Item;
            AddChoice(item.Icon, item.DisplayName, null, HearthKit.Faded(style.Accent, 0.5f), inventory.CountCarried(item).ToString(),
                () => skills.Loadout.EquipItem(index, item), () => tooltip.ShowDefinition(item));
        }

        EndPicker("Nothing in your bag to use from the bar: draughts, food and bandages go here." + (Place.StashUsable ? " Pack as last time takes them from the stash." : ""));
    }

    private int pickerShown;
    // Whether the first choice only empties the slot ("Take off", "Clear").
    private bool pickerEmptyingFirst;

    private void BeginPicker()
    {
        pickerShown = 0;
        pickerEmptyingFirst = false;
        pickerChoices.Clear();
        pickerTips.Clear();
        HideTip();
    }

    private void AddChoice(Sprite picture, string name, string icon, Color rim, string badge, System.Action choose, System.Action tip, Color? badgeColour = null)
    {
        int at = pickerShown++;
        if (at >= pickerCards.Count)
        {
            var card = new KitPictureCard(pickerContent, "Choice " + at, PickerCard, PickerCard - 10f, 2, 13f, () => pickerTips[at]?.Invoke(), HideTip);
            Clickable(card, () => Choose(at));
            pickerCards.Add(card);
        }

        pickerChoices.Add(choose);
        pickerTips.Add(tip);
        KitPictureCard shown = pickerCards[at];
        shown.SetActive(true);
        int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth - 12f + 8f) / (PickerCard + 8f)));
        float step = (ColumnWidth - 12f - PickerCard) / Mathf.Max(1, perRow - 1);
        shown.Place(at % perRow * step, at / perRow * (shown.Height + 10f));
        shown.ShowPicture(picture, icon ?? "Barding");
        shown.SetName(name);
        shown.SetLook(false, rim);
        shown.SetBadge(badge);
    }

    private void EndPicker(string nothing)
    {
        for (int i = pickerShown; i < pickerCards.Count; i++)
        {
            pickerCards[i].SetActive(false);
        }

        int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth - 12f + 8f) / (PickerCard + 8f)));
        float rowHeight = pickerCards.Count > 0 ? pickerCards[0].Height + 10f : PickerCard;
        pickerContent.sizeDelta = new Vector2(pickerContent.sizeDelta.x, Mathf.CeilToInt(pickerShown / (float)perRow) * rowHeight);
        // "Take off" or "Clear" alone is no choice to make: the note says why there is nothing else.
        bool onlyEmptying = pickerShown == 1 && pickerEmptyingFirst;
        if ((pickerShown == 0 || onlyEmptying) && nothing != null)
        {
            pickerTitle.text += $"\n<size=70%><color={UIStyle.Current.MutedHex}>{nothing}</color></size>";
        }
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= pickerChoices.Count)
        {
            return;
        }

        pickerChoices[index]?.Invoke();
        // Back to the page: the change is seen on its slot, and the gear score moves with it.
        ClosePicker();
        UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.Click : null);
        Refresh();
    }

    private void PackAgain()
    {
        PackForRaid.Pack(plan.Player);
        Refresh();
    }
}
