using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A small panel that follows the pointer and describes the item, recipe, resource or skill under it. Recipes show what
/// they make (stats and description) and what they cost.
/// </summary>
public sealed class ItemTooltipView : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text titleLabel;
    [Tooltip("Shows the details with rich text.")]
    [SerializeField] private TMP_Text bodyLabel;
    [Tooltip("Offset from the pointer, in canvas units.")]
    [SerializeField] private Vector2 offset = new Vector2(18f, -18f);

    private RectTransform area;

    /// <summary>Sets up a tooltip made in code. Call it while its object is still inactive (before Awake).</summary>
    public void Configure(RectTransform tipPanel, TMP_Text title, TMP_Text body)
    {
        panel = tipPanel;
        titleLabel = title;
        bodyLabel = body;
    }

    private void Awake()
    {
        area = (RectTransform)panel.parent;
        Hide();
    }

    /// <param name="footer">An extra last line from the screen showing it (the bag's quickbar hint), or null.</param>
    public void Show(ItemStack stack, string footer = null)
    {
        if (stack.IsEmpty)
        {
            Hide();
            return;
        }

        ItemDefinition item = stack.Item;
        var hints = new StringBuilder();
        string text = Describe(item, stack, hints);
        if (!string.IsNullOrEmpty(footer))
        {
            hints.Append(hints.Length > 0 ? "\n" : "").Append(footer);
        }

        Show(stack.DisplayName, NameColour(item.Rarity), text, item.Icon, hints.ToString());
        Header(item.Rarity, stack.Quality);
    }

    /// <summary>Shows a new item or a resource (an ItemDefinition or ResourceDefinition), such as a station's output.</summary>
    public void ShowDefinition(Object definition)
    {
        if (definition is ItemDefinition item)
        {
            Show(item.DisplayName, NameColour(item.Rarity), Describe(item, null), item.Icon);
            Header(item.Rarity, ItemQuality.Plain);
        }
        else if (definition is ResourceDefinition resource)
        {
            Show(resource.DisplayName, new Color(0.91f, 0.77f, 0.42f, 1f), DescribeResource(resource), resource.Icon);
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// Describes what a recipe makes (the item's stats and description, or the resource's) followed by its costs, each
    /// green or red by what the player has, where it is made, how long it takes and why it can't be made now.
    /// </summary>
    /// <param name="crafting">Whose materials to count; null shows the costs without amounts.</param>
    /// <param name="problem">Why it can't be made right now, or null.</param>
    public void ShowRecipe(RecipeDefinition recipe, PlayerCrafting crafting, string problem)
    {
        if (recipe == null || !recipe.HasOutput)
        {
            Hide();
            return;
        }

        string amount = recipe.OutputAmount > 1 ? $"  ×{recipe.OutputAmount}" : "";
        Color color = recipe.Output != null ? NameColour(recipe.Output.Rarity) : new Color(0.91f, 0.77f, 0.42f, 1f);
        Sprite icon = recipe.Output != null ? recipe.Output.Icon : recipe.OutputResource != null ? recipe.OutputResource.Icon : null;
        Show(recipe.DisplayName + amount, color, DescribeRecipe(recipe, crafting, problem), icon);
    }

    /// <summary>What the Show tooltip says about a held stack, for windows that show it in place.</summary>
    public static string DescribeStack(ItemStack stack) => Describe(stack.Item, stack);

    /// <summary>What a recipe makes: the item's stats and description, or the resource's.</summary>
    public static string DescribeOutput(RecipeDefinition recipe)
    {
        if (recipe.Output != null)
        {
            return Describe(recipe.Output, null);
        }

        return recipe.OutputResource != null ? DescribeResource(recipe.OutputResource) : "";
    }

    public static string DescribeRecipe(RecipeDefinition recipe, PlayerCrafting crafting, string problem)
    {
        var builder = new StringBuilder(DescribeOutput(recipe));
        builder.Append("\n\n<b>Costs</b>").Append(DescribeCosts(recipe, crafting));
        string where = recipe.IsHandcrafted ? "By hand" : recipe.Station.DisplayName();
        string time = recipe.IsHandcrafted ? "" : $"  ·  {FormatSeconds(recipe.CraftSeconds)}";
        builder.Append($"\n\n<color={UIStyle.Current.MutedHex}>{where}{time}</color>");
        if (!string.IsNullOrEmpty(problem))
        {
            builder.Append($"\n<color={UIStyle.Current.DangerHex}>{problem}</color>");
        }

        return builder.ToString();
    }

    /// <summary>One line per cost, each starting with a line break: green when the player has enough, red when not.</summary>
    public static string DescribeCosts(RecipeDefinition recipe, PlayerCrafting crafting)
    {
        var builder = new StringBuilder();
        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null)
            {
                AppendCost(builder, cost.Resource.DisplayName, crafting != null ? crafting.Have(cost.Resource) : -1, cost.Amount);
            }
        }

        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null)
            {
                AppendCost(builder, cost.Item.DisplayName, crafting != null ? crafting.Have(cost.Item) : -1, cost.Amount);
            }
        }

        return builder.Length > 0 ? builder.ToString() : $"\n<color={UIStyle.Current.MutedHex}>Nothing</color>";
    }

    /// <summary>"8 s", "1 m 30 s".</summary>
    public static string FormatSeconds(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        return total >= 60 ? $"{total / 60} m{(total % 60 > 0 ? $" {total % 60} s" : "")}" : $"{total} s";
    }

    private static void AppendCost(StringBuilder builder, string name, int have, int need)
    {
        if (have < 0)
        {
            builder.Append($"\n{need} {name}");
            return;
        }

        builder.Append($"\n<color={(have >= need ? UIStyle.Current.GoodHex : UIStyle.Current.DangerHex)}>{need} {name}</color>  <color={UIStyle.Current.MutedHex}>(have {have})</color>");
    }

    private static string DescribeResource(ResourceDefinition resource)
    {
        var builder = new StringBuilder();
        builder.Append(Tiers.RichLabel(resource.Tier)).Append($"  <color={UIStyle.Current.MutedHex}>")
            .Append(resource.Category != null ? resource.Category.DisplayName : "Resource").Append("</color>");
        builder.Append($"\n<color={UIStyle.Current.MutedHex}>A resource: weightless, kept with your essences.</color>");
        if (!string.IsNullOrWhiteSpace(resource.Description))
        {
            builder.Append("\n\n").Append(resource.Description);
        }

        if (!string.IsNullOrWhiteSpace(resource.Uses))
        {
            builder.Append($"\n\n<color={UIStyle.Current.AccentHex}>Used for</color>\n").Append(resource.Uses);
        }

        return builder.ToString();
    }

    /// <summary>Shows any title and rich-text body, for things that aren't items (skills).</summary>
    public void Show(string title, Color titleColor, string body) => Show(title, titleColor, body, null);

    /// <summary>
    /// Shows a title and rich-text body. With an <paramref name="icon"/> the body is an item's or resource's description:
    /// its first line (rarity, tier, kind) goes beside the icon, under the name. <paramref name="hints"/> (how to use it,
    /// which keys) go in small type at the foot, under a rule.
    /// </summary>
    public void Show(string title, Color titleColor, string body, Sprite icon, string hints = null)
    {
        Kit();
        string sub = null;
        if (icon != null && subLabel != null && !string.IsNullOrEmpty(body))
        {
            int line = body.IndexOf('\n');
            sub = line < 0 ? body : body.Substring(0, line);
            body = line < 0 ? "" : body.Substring(line + 1).TrimStart('\n');
        }

        titleLabel.text = title;
        titleLabel.color = titleColor;
        bodyLabel.text = HearthKit.ToneNumbers(body);
        bodyLabel.gameObject.SetActive(!string.IsNullOrEmpty(body));
        if (subLabel != null)
        {
            bool framed = icon != null;
            subLabel.text = sub;
            subLabel.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            iconFrame.gameObject.SetActive(framed);
            iconImage.sprite = icon;
            float indent = framed ? IconFrameSize + 12f : 0f;
            titleLabel.margin = new Vector4(indent, 0f, 0f, 0f);
            subLabel.margin = new Vector4(indent, 0f, 0f, 0f);
            // Name and kind together are at least as tall as the framed icon beside them.
            titleSize.minHeight = framed ? 30f : -1f;
            subSize.minHeight = framed ? IconFrameSize - 26f : -1f;
            hintRule.gameObject.SetActive(!string.IsNullOrEmpty(hints));
            hintLabel.text = hints;
            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(hints));
        }
        else if (!string.IsNullOrEmpty(hints))
        {
            bodyLabel.text += "\n" + hints;
        }

        // Last among its window's children: parts a window adds after it is built (the character sheet's stat cards) would draw over it.
        panel.SetAsLastSibling();
        panel.gameObject.SetActive(true);
        Header(ItemRarity.Common, ItemQuality.Plain);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    // Common things are named in the kit's warm text colour, not a cold white; the rest keep their rarity's colour.
    private static Color NameColour(ItemRarity rarity) => rarity == ItemRarity.Common ? UIStyle.Current.Text : Rarity.ColorOf(rarity);

    private const float IconFrameSize = 52f;
    private bool kitted;
    private TMP_Text subLabel;
    private TMP_Text hintLabel;
    private Image hintRule;
    private RectTransform iconFrame;
    private Image iconImage;
    private LayoutElement titleSize;
    private LayoutElement subSize;

    // Hearthline 3: the name in the title face; an item's icon in a gold-framed slot beside its name, its kind under it and
    // a gold rule below; the hints at the foot; a gold rim round it all, so it stands off the window it is over. Only a
    // tooltip laid out in a column (title and body straight under the panel) gets the parts; any other keeps its look.
    private void Kit()
    {
        if (kitted)
        {
            return;
        }

        kitted = true;
        UIStyle style = UIStyle.Current;
        if (style.TitleFont != null)
        {
            titleLabel.font = style.TitleFont;
        }

        var column = panel.GetComponent<VerticalLayoutGroup>();
        if (column == null || titleLabel.transform.parent != panel || bodyLabel.transform.parent != panel)
        {
            return;
        }

        float bodySize = bodyLabel.fontSize;
        subLabel = Line("Kind", titleLabel.transform.GetSiblingIndex() + 1, bodySize - 1f, style.Muted, FontStyles.Normal);
        Image headerRule = Rule("Header Rule", subLabel.transform.GetSiblingIndex() + 1);
        hintRule = Rule("Hint Rule", bodyLabel.transform.GetSiblingIndex() + 1);
        hintLabel = Line("Hints", hintRule.transform.GetSiblingIndex() + 1, bodySize - 1f, style.Muted, FontStyles.Normal);
        titleSize = Sized(titleLabel.gameObject);
        subSize = Sized(subLabel.gameObject);
        headerRule.color = HearthKit.Faded(style.Accent, 0.5f);

        iconFrame = HearthKit.Card(panel, "Icon Frame", column.padding.left, column.padding.top + 1f, IconFrameSize, IconFrameSize, 0.7f);
        iconFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform inner = CodeUI.Stretch("Icon", iconFrame);
        inner.offsetMin = new Vector2(6f, 6f);
        inner.offsetMax = new Vector2(-6f, -6f);
        iconImage = inner.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconFrame.gameObject.SetActive(false);

        Image rim = HearthKit.Rim(panel, 0.6f, 1.2f);
        rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rim.transform.SetAsLastSibling();
    }

    private TMP_Text Line(string name, int index, float size, Color colour, FontStyles fontStyle)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(panel, false);
        rect.SetSiblingIndex(index);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = bodyLabel.font;
        label.fontSize = size;
        label.color = colour;
        label.fontStyle = fontStyle;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        rect.gameObject.SetActive(false);
        return label;
    }

    private Image Rule(string name, int index)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(panel, false);
        rect.SetSiblingIndex(index);
        var size = rect.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = 2f;
        var line = rect.gameObject.AddComponent<Image>();
        line.color = HearthKit.Faded(UIStyle.Current.Accent, 0.3f);
        line.raycastTarget = false;
        return line;
    }

    private static LayoutElement Sized(GameObject part)
    {
        var size = part.GetComponent<LayoutElement>();
        return size != null ? size : part.AddComponent<LayoutElement>();
    }

    private Image band;
    private Material plainTitle;
    // Per face and rarity: a glow made from one font's material would draw another font's text from the wrong atlas.
    private static readonly System.Collections.Generic.Dictionary<(Material, ItemRarity), Material> Glowing =
        new System.Collections.Generic.Dictionary<(Material, ItemRarity), Material>();

    // Hearthline 2: the top of the tooltip takes on the item's rarity (a solid mix, not a see-through wash), and a rare
    // name glows faintly in its colour.
    private void Header(ItemRarity rarity, ItemQuality quality)
    {
        UIStyle style = UIStyle.Current;
        if (band == null)
        {
            var rect = (RectTransform)new GameObject("Rarity Band", typeof(RectTransform)).transform;
            rect.SetParent(panel, false);
            rect.SetAsFirstSibling();
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            band = rect.gameObject.AddComponent<Image>();
            band.raycastTarget = false;
            plainTitle = titleLabel.fontSharedMaterial;
        }

        bool lit = rarity >= ItemRarity.Uncommon || quality != ItemQuality.Plain;
        band.enabled = lit;
        if (lit)
        {
            Color colour = quality == ItemQuality.Masterwork ? Rarity.ColorOf(ItemRarity.Epic)
                : quality == ItemQuality.Fine && rarity < ItemRarity.Rare ? Rarity.ColorOf(ItemRarity.Rare) : Rarity.ColorOf(rarity);
            band.color = Color.Lerp(style.Panel, colour, 0.2f);
            Canvas.ForceUpdateCanvases();
            float top = panel.rect.yMax;
            // Down to the foot of the name, or of the kind line under it.
            RectTransform last = subLabel != null && subLabel.gameObject.activeSelf ? subLabel.rectTransform : titleLabel.rectTransform;
            float below = last.TransformPoint(new Vector3(0f, last.rect.yMin, 0f)).y;
            float height = Mathf.Max(24f, top - panel.InverseTransformPoint(new Vector3(0f, below, 0f)).y + 8f);
            band.rectTransform.sizeDelta = new Vector2(0f, height);
            band.rectTransform.anchoredPosition = Vector2.zero;
        }

        titleLabel.fontSharedMaterial = rarity >= ItemRarity.Rare || quality == ItemQuality.Masterwork
            ? GlowFor(quality == ItemQuality.Masterwork && rarity < ItemRarity.Epic ? ItemRarity.Epic : rarity) : plainTitle;
    }

    private Material GlowFor(ItemRarity rarity)
    {
        if (plainTitle == null || !plainTitle.HasProperty("_GlowColor"))
        {
            return plainTitle;
        }

        if (!Glowing.TryGetValue((plainTitle, rarity), out Material material) || material == null)
        {
            material = new Material(plainTitle) { name = plainTitle.name + " Glow " + rarity, hideFlags = HideFlags.DontSave };
            material.EnableKeyword("GLOW_ON");
            Color colour = Rarity.ColorOf(rarity);
            colour.a = 0.55f;
            material.SetColor("_GlowColor", colour);
            material.SetFloat("_GlowOuter", 0.35f);
            material.SetFloat("_GlowInner", 0.05f);
            material.SetFloat("_GlowPower", 0.6f);
            Glowing[(plainTitle, rarity)] = material;
        }

        return material;
    }

    public void Hide()
    {
        // A window built in code can call this as its parts are torn down with the scene.
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    public void Follow(Vector2 screenPosition)
    {
        if (!panel.gameObject.activeSelf)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, null, out Vector2 local);
        Vector2 position = local + offset;

        // Keep the panel inside the window.
        Rect bounds = area.rect;
        Vector2 size = panel.rect.size;
        position.x = Mathf.Min(position.x, bounds.xMax - size.x);
        position.y = Mathf.Max(position.y, bounds.yMin + size.y);
        panel.anchoredPosition = position;
    }

    // Against what is worn in the slot it would go to: an arrow up in green, down in red, nothing when it is the same.
    /// <summary>What a stat does, where it isn't plain (Docs/PlayerStatsResearch.md #2): Might as the damage it adds.</summary>
    public static string EffectOf(StatBonus bonus)
    {
        if (bonus.op != StatModifierOp.Flat || (bonus.stat != StatType.Strength && bonus.stat != StatType.PhysicalPower))
        {
            return "";
        }

        CombatRules rules = CombatRules.Default;
        float power = bonus.stat == StatType.Strength ? bonus.value * rules.PowerPerStrength : bonus.value;
        return $" <color={UIStyle.Current.MutedHex}>({(power >= 0f ? "+" : "")}{Mathf.RoundToInt(power * rules.DamagePerPower * 100f)}% damage)</color>";
    }

    // This stat against the same stat on what is worn in the slot: an arrow per stat, beside the gear score's.
    private static string StatCompared(ItemDefinition item, StatBonus bonus)
    {
        PlayerEquipment equipment = Player.Current != null ? Player.Current.GetComponent<PlayerEquipment>() : null;
        if (equipment == null || !GearSlots.TryFindSlot(item, equipment.Gear.Slots, out GearSlot slot))
        {
            return "";
        }

        ItemStack worn = equipment.Gear.Get(slot);
        if (worn.IsEmpty || worn.Item == item)
        {
            return "";
        }

        float theirs = 0f;
        foreach (StatBonus other in worn.Item.StatBonuses)
        {
            if (other.stat == bonus.stat && other.op == bonus.op)
            {
                theirs += other.value;
            }
        }

        float difference = bonus.value - theirs;
        if (Mathf.Abs(difference) < 0.001f)
        {
            return "";
        }

        bool fraction = bonus.op == StatModifierOp.Percent || StatNames.IsFraction(bonus.stat);
        string amount = fraction ? $"{Mathf.RoundToInt(difference * 100f)}%" : $"{difference:0.#}";
        UIStyle style = UIStyle.Current;
        return difference > 0f ? $"  <color={style.GoodHex}>\u25B2 +{amount}</color>" : $"  <color={style.DangerHex}>\u25BC {amount}</color>";
    }

    /// <summary>
    /// A meal's or tonic's family (Docs/PlayerStatsResearch.md #8), so a raid's food is chosen by what it is for: Hearty (health, armour),
    /// Swift (stamina, speed), Fierce (damage, often at a cost), Warding (resistances). Empty when it is none of these.
    /// </summary>
    public static string MealFamily(System.Collections.Generic.IReadOnlyList<StatBonus> bonuses)
    {
        float hearty = 0f, swift = 0f, fierce = 0f, warding = 0f;
        foreach (StatBonus bonus in bonuses)
        {
            switch (bonus.stat)
            {
                case StatType.MaxHealth: case StatType.Armor: case StatType.PhysicalResistance: hearty += 1f; break;
                case StatType.MaxStamina: case StatType.MoveSpeed: swift += 1f; break;
                case StatType.Strength: case StatType.PhysicalPower: case StatType.DamageBonus: case StatType.MeleeDamage: fierce += 1.5f; break;
                case StatType.FireResistance: case StatType.FrostResistance: case StatType.HolyResistance: warding += 1f; break;
            }
        }

        float most = Mathf.Max(Mathf.Max(hearty, swift), Mathf.Max(fierce, warding));
        return most <= 0f ? "" : fierce == most ? "Fierce" : hearty == most ? "Hearty" : warding == most ? "Warding" : "Swift";
    }

    private static string Compared(ItemDefinition item, int score)
    {
        PlayerEquipment equipment = Player.Current != null ? Player.Current.GetComponent<PlayerEquipment>() : null;
        if (equipment == null || !GearSlots.TryFindSlot(item, equipment.Gear.Slots, out GearSlot slot))
        {
            return "";
        }

        ItemStack worn = equipment.Gear.Get(slot);
        if (worn.IsEmpty)
        {
            return $"  <color={UIStyle.Current.GoodHex}>\u25B2 nothing worn</color>";
        }

        int difference = score - GearScore.Of(worn);
        UIStyle style = UIStyle.Current;
        return difference > 0 ? $"  <color={style.GoodHex}>\u25B2 +{difference}</color>"
            : difference < 0 ? $"  <color={style.DangerHex}>\u25BC {difference}</color>"
            : "";
    }

    // Without a stack (a recipe's output) it describes the item as it comes out new, for someone deciding whether to make
    // it: what it is for comes before its numbers, and there is no wear, stack size, price or use hint.
    // With hints, how to use it (which keys, which clicks) goes there instead, for the tooltip's foot.
    private static string Describe(ItemDefinition item, ItemStack? stack, StringBuilder hints = null)
    {
        bool held = stack.HasValue;
        int count = held ? stack.Value.Count : 1;
        var builder = new StringBuilder();
        builder.Append(Rarity.Rich(Rarity.NameOf(item.Rarity), item.Rarity)).Append("  ")
            .Append(Tiers.RichLabel(item.Tier)).Append($"  <color={UIStyle.Current.MutedHex}>").Append(item.Category).Append("</color>");
        string gearSlot = GearSlots.Describe(item);
        if (gearSlot.Length > 0)
        {
            builder.Append($"\n<color={UIStyle.Current.MutedHex}>").Append(gearSlot).Append("</color>");
            if (GearScore.Counts(item))
            {
                int score = stack.HasValue ? GearScore.Of(stack.Value) : GearScore.Of(item);
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>Gear Score {score}</color>").Append(Compared(item, score));
            }
        }

        bool describedFirst = !held && !string.IsNullOrWhiteSpace(item.Description);
        if (describedFirst)
        {
            builder.Append("\n\n").Append(item.Description);
        }

        int numbersStart = builder.Length;
        foreach (StatBonus bonus in item.StatBonuses)
        {
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>").Append(bonus.Describe()).Append(EffectOf(bonus)).Append("</color>").Append(StatCompared(item, bonus));
        }

        if (held)
        {
            AppendEssenceWork(builder, stack.Value);
        }

        // A lantern's condition is its light, shown in hours below, not as durability.
        if (item.HasDurability && !item.IsLantern && held)
        {
            ItemStack worn = stack.Value;
            int percent = Mathf.RoundToInt(worn.Durability * 100f);
            string color = worn.IsBroken ? UIStyle.Current.DangerHex : worn.IsDamaged ? UIStyle.Current.WarningHex : UIStyle.Current.GoodHex;
            builder.Append($"\n<color={color}>Durability {percent}%{(worn.IsBroken ? " – broken, repair it at a Workshop" : "")}</color>");
        }

        if (item.IsLantern)
        {
            float hours = held ? stack.Value.LightHours : item.Lantern.Hours;
            string colour = hours < 1f ? UIStyle.Current.DangerHex : UIStyle.Current.GoodHex;
            string refill = item.Lantern.Refill != null ? item.Lantern.Refill.DisplayName.ToLowerInvariant() : "refill";
            builder.Append($"\n<color={colour}>Light {Mathf.CeilToInt(hours)} of {Mathf.RoundToInt(item.Lantern.Hours)} hours</color>");
            Hint(builder, hints, $"Worn in the Light slot; L lights it. Right-click with a {refill} in the bag to refill it.");
        }

        if (item.HoldsWater)
        {
            float water = held ? stack.Value.Water : item.WaterCapacity;
            string color = water < 0.5f ? UIStyle.Current.DangerHex : UIStyle.Current.WaterHex;
            builder.Append($"\n<color={color}>Water {Mathf.RoundToInt(water)} / {Mathf.RoundToInt(item.WaterCapacity)}{(water < 0.5f ? " – fill it at a stream or a well" : "")}</color>");
        }

        if (item.Weight > 0f)
        {
            builder.Append($"\n<color={UIStyle.Current.MutedHex}>Weight {item.Weight:0.##}{(count > 1 ? $"  ({item.Weight * count:0.#} for the stack)" : "")}</color>");
        }

        if (item.Value > 0 && held)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Value {item.Value}{(count > 1 ? $"  ({item.Value * count} for the stack)" : "")}</color>");
        }

        if (item.IsTool)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>{item.ToolType}: gathers up to {Tiers.RichLabel(item.ToolTier)} resources</color>");
        }

        if (item.IsWeapon)
        {
            // A bow hits as hard as its arrow: its own damage is nought, and "0 damage" read as a useless bow.
            if (item.Weapon.IsBow)
            {
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>Bow: hits with the arrow's damage, {item.Weapon.DrawSeconds:0.#} s to a full draw.</color>");
            }
            else if (item.Weapon.Damage > 0f)
            {
                string kind = item.Weapon.Strike != StrikeKind.None ? ", " + item.Weapon.Strike.Label().ToLowerInvariant() : "";
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>Weapon: {Mathf.RoundToInt(item.Weapon.Damage)} damage{kind}.</color>");
            }
        }

        // What a bow's tooltip points to: the arrow's own damage, and the fire it sets.
        if (item.IsArrow)
        {
            ArrowDefinition arrow = item.Arrow;
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Arrow: {Mathf.RoundToInt(arrow.Damage)} damage at a full draw.</color>");
            if (arrow.Burns)
            {
                builder.Append($"\n<color={UIStyle.Current.WarningHex}>Sets what it hits alight: {arrow.BurnDamagePerSecond:0.#} damage a second for {arrow.BurnSeconds:0.#} s.</color>");
            }
        }

        if (item.IsConsumable)
        {
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>").Append(DescribeRestores(item.HealAmount, item.HungerRestore, item.ThirstRestore))
                .Append(held ? " Right-click to use." : "").Append("</color>");
            if (item.HasBoon)
            {
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>{(item.IsTonic ? "Tonic" : "Meal")}: {DescribeBonuses(item.BoonBonuses)} for {RaidStats.FormatTime(item.BoonSeconds)}.</color>");
                string family = MealFamily(item.BoonBonuses);
                if (family.Length > 0)
                {
                    builder.Append($"  <color={UIStyle.Current.MutedHex}>{family}</color>");
                }
            }

            if (item.Cures.Count > 0)
            {
                var cured = new System.Collections.Generic.List<string>();
                foreach (StatusEffectDefinition effect in item.Cures)
                {
                    cured.Add(effect.DisplayName);
                }

                builder.Append($"\n<color={UIStyle.Current.GoodHex}>Cures {string.Join(", ", cured)}.</color>");
            }
        }

        if (item.IsBlueprint)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Blueprint.{(held ? " Right-click to study it and learn how to make it." : " Study it to learn how to make it.")}</color>");
        }

        if (item.IsBackpack)
        {
            if (item.CarryCapacityBonus > 0)
            {
                builder.Append($"\n<color={UIStyle.Current.GoodHex}>+{item.CarryCapacityBonus} carry weight while worn.</color>");
                if (item.CarryNoise > 0f)
                {
                    builder.Append($"\n<color={UIStyle.Current.WarningHex}>Rattles: every step carries {item.CarryNoise:0.#} m further.</color>");
                }

                if (item.CarrySpeedScale < 1f)
                {
                    builder.Append($"\n<color={UIStyle.Current.WarningHex}>{Mathf.RoundToInt((1f - item.CarrySpeedScale) * 100f)}% slower while worn.</color>");
                }
            }

            if (item.BagSlotBonus > 0)
            {
                builder.Append($"\n<color={UIStyle.Current.GoodHex}>Unlocks {item.BagSlotBonus} extra bag slots while worn.</color>");
            }
        }

        if (item.IsRaidModifier)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Raid modifier: {item.RaidModifier.DescribeEffects()}. Choose it at the raid portal.</color>");
        }

        if (item.IsEquippable && held)
        {
            Hint(builder, hints, "Right-click to equip or unequip (C opens the character screen).");
        }

        if (held && !string.IsNullOrWhiteSpace(item.Description))
        {
            builder.Append("\n\n").Append(item.Description);
        }

        if (held && item.Stackable)
        {
            builder.Append($"\n\n<color={UIStyle.Current.MutedHex}>{count} / {item.MaximumStack} in this stack</color>");
        }

        // A blank line between the words and the numbers that follow them.
        if (describedFirst && builder.Length > numbersStart)
        {
            builder.Insert(numbersStart, "\n");
        }

        return builder.ToString();
    }

    private static void Hint(StringBuilder builder, StringBuilder hints, string line)
    {
        if (hints != null)
        {
            hints.Append(hints.Length > 0 ? "\n" : "").Append(line);
        }
        else
        {
            builder.Append($"\n<color={UIStyle.Current.MutedHex}>").Append(line).Append("</color>");
        }
    }

    private static void AppendEssenceWork(StringBuilder builder, ItemStack stack)
    {
        foreach (StatBonus bonus in GearPower.QualityBonuses(stack.Item, stack.Quality, EssenceSettings.Current))
        {
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>").Append(bonus.Describe()).Append($" ({stack.Quality.ToString().ToLowerInvariant()} make)</color>");
        }

        if (!string.IsNullOrEmpty(stack.Maker))
        {
            builder.Append($"\n<i><color={UIStyle.Current.MutedHex}>Made by {stack.Maker}.</color></i>");
        }

        // A caught fish's length (for show: it feeds the same whatever it measures).
        if (stack.Size > 0f)
        {
            string length = FishSizes.Describe(stack.Size);
            builder.Append($"\n<i><color={UIStyle.Current.MutedHex}>{(stack.Count > 1 ? $"The biggest of these: {length}." : $"{length} long.")}</color></i>");
        }

        foreach (StatBonus bonus in GearPower.EmpowerBonuses(stack.Item, stack.Empower, EssenceSettings.Current))
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>").Append(bonus.Describe()).Append(" (empowered)</color>");
        }

        if (stack.Enchantment == null)
        {
            return;
        }

        EnchantmentDefinition enchantment = stack.Enchantment;
        bool inscription = stack.Item.IsInscription;
        string kind = enchantment.IsRelic ? "" : enchantment.IsGreat ? "  <size=80%>Great enchantment</size>" : enchantment.School == EssenceSchool.Cursed ? "  <size=80%>Cursed</size>" : "";
        builder.Append($"\n\n<color={UIStyle.Current.AccentHex}>{(inscription ? "Teaches: " : "")}{enchantment.DisplayName}</color>{kind}");
        foreach (StatBonus bonus in stack.Enchantment.Bonuses)
        {
            builder.Append(!StatNames.IsPenalty(bonus.stat, bonus.value) ? $"\n<color={UIStyle.Current.GoodHex}>" : $"\n<color={UIStyle.Current.DangerHex}>").Append(bonus.Describe()).Append("</color>");
        }

        foreach (RelicEffect effect in stack.Enchantment.Effects)
        {
            if (effect != null)
            {
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>").Append(effect.Describe()).Append("</color>");
            }
        }

        if (stack.Enchantment.IsRelic)
        {
            builder.Append("\n<alpha=#AA>A relic. Nobody at home could make this.");
        }

        string muted = UIStyle.Current.MutedHex;
        if (inscription)
        {
            // What it fits and needs, and what reading it does: teach, or practice when known (Docs/EnchantingResearch.md #8).
            bool known = GameSession.Progress.Knows(enchantment);
            builder.Append($"\n<color={muted}>Fits {enchantment.DescribeTargets()}." +
                           (enchantment.RequiredRunecraft > 0 ? $" Carving needs Runecraft {enchantment.RequiredRunecraft}." : "") +
                           (known ? " You know it already: reading it is Runecraft practice, or give it to Liv." : " Read it at home to learn it.") + "</color>");
            return;
        }

        // An enchantment counts once, however many pieces carry it: say so when another worn piece already gives this one.
        PlayerEquipment equipment = Player.Current != null ? Player.Current.GetComponent<PlayerEquipment>() : null;
        if (equipment != null && !enchantment.IsRelic)
        {
            ItemContainer worn = equipment.Gear.Slots;
            int self = -1;
            for (int i = 0; i < worn.SlotCount && self < 0; i++)
            {
                ItemStack at = worn.GetSlot(i);
                if (!at.IsEmpty && at.Item == stack.Item && at.Enchantment == enchantment && at.Empower == stack.Empower)
                {
                    self = i;
                }
            }

            ItemStack other = GearPower.CarrierOf(worn, enchantment, self);
            if (!other.IsEmpty && (self < 0 || !GearPower.CountsEnchantment(worn, self)))
            {
                builder.Append($"\n<color={UIStyle.Current.DangerHex}>Counts once: your {other.DisplayName} already gives it.</color>");
            }
        }
    }

    /// <summary>"+3 Strength, +15 Max Stamina".</summary>
    public static string DescribeBonuses(System.Collections.Generic.IReadOnlyList<StatBonus> bonuses)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (StatBonus bonus in bonuses)
        {
            parts.Add(bonus.Describe());
        }

        return string.Join(", ", parts);
    }

    /// <summary>"Restores 30 health, 40 hunger." – the parts that are above zero.</summary>
    public static string DescribeRestores(float health, float hunger, float thirst)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (health > 0f)
        {
            parts.Add($"{Mathf.RoundToInt(health)} health");
        }

        if (hunger > 0f)
        {
            parts.Add($"{Mathf.RoundToInt(hunger)} hunger");
        }

        if (thirst > 0f)
        {
            parts.Add($"{Mathf.RoundToInt(thirst)} thirst");
        }

        return parts.Count > 0 ? "Restores " + string.Join(", ", parts) + "." : "";
    }
}
