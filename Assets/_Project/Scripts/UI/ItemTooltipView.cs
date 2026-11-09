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
        string text = Describe(item, stack);
        Show(stack.DisplayName, Rarity.ColorOf(item.Rarity), string.IsNullOrEmpty(footer) ? text : text + "\n" + footer);
        Header(item.Rarity, stack.Quality);
    }

    /// <summary>Shows a new item or a resource (an ItemDefinition or ResourceDefinition), such as a station's output.</summary>
    public void ShowDefinition(Object definition)
    {
        if (definition is ItemDefinition item)
        {
            Show(item.DisplayName, Rarity.ColorOf(item.Rarity), Describe(item, null));
            Header(item.Rarity, ItemQuality.Plain);
        }
        else if (definition is ResourceDefinition resource)
        {
            Show(resource.DisplayName, new Color(0.91f, 0.77f, 0.42f, 1f), DescribeResource(resource));
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
        Color color = recipe.Output != null ? Rarity.ColorOf(recipe.Output.Rarity) : new Color(0.91f, 0.77f, 0.42f, 1f);
        Show(recipe.DisplayName + amount, color, DescribeRecipe(recipe, crafting, problem));
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

        return builder.ToString();
    }

    /// <summary>Shows any title and rich-text body, for things that aren't items (skills).</summary>
    public void Show(string title, Color titleColor, string body)
    {
        titleLabel.text = title;
        titleLabel.color = titleColor;
        bodyLabel.text = body;
        panel.gameObject.SetActive(true);
        Header(ItemRarity.Common, ItemQuality.Plain);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    private Image band;
    private Material plainTitle;
    private static readonly System.Collections.Generic.Dictionary<ItemRarity, Material> Glowing =
        new System.Collections.Generic.Dictionary<ItemRarity, Material>();

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
            float below = titleLabel.rectTransform.TransformPoint(new Vector3(0f, titleLabel.rectTransform.rect.yMin, 0f)).y;
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

        if (!Glowing.TryGetValue(rarity, out Material material) || material == null)
        {
            material = new Material(plainTitle) { name = plainTitle.name + " Glow " + rarity, hideFlags = HideFlags.DontSave };
            material.EnableKeyword("GLOW_ON");
            Color colour = Rarity.ColorOf(rarity);
            colour.a = 0.55f;
            material.SetColor("_GlowColor", colour);
            material.SetFloat("_GlowOuter", 0.35f);
            material.SetFloat("_GlowInner", 0.05f);
            material.SetFloat("_GlowPower", 0.6f);
            Glowing[rarity] = material;
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
    private static string Describe(ItemDefinition item, ItemStack? stack)
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
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>").Append(bonus.Describe()).Append("</color>");
        }

        if (held)
        {
            AppendEssenceWork(builder, stack.Value);
        }

        if (item.HasDurability && held)
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
            builder.Append($"\nWorn in the Light slot; L lights it. Right-click with a {refill} in the bag to refill it.");
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
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Weapon: {Mathf.RoundToInt(item.Weapon.Damage)} damage.</color>");
        }

        if (item.IsConsumable)
        {
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>").Append(DescribeRestores(item.HealAmount, item.HungerRestore, item.ThirstRestore))
                .Append(held ? " Right-click to use." : "").Append("</color>");
            if (item.HasBoon)
            {
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>{(item.IsTonic ? "Tonic" : "Meal")}: {DescribeBonuses(item.BoonBonuses)} for {RaidStats.FormatTime(item.BoonSeconds)}.</color>");
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
            builder.Append($"\n<color={UIStyle.Current.MutedHex}>Right-click to equip or unequip (C opens the character screen).</color>");
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

        foreach (StatBonus bonus in GearPower.EmpowerBonuses(stack.Item, stack.Empower, EssenceSettings.Current))
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>").Append(bonus.Describe()).Append(" (empowered)</color>");
        }

        if (stack.Enchantment == null)
        {
            return;
        }

        builder.Append($"\n\n<color={UIStyle.Current.AccentHex}>{stack.Enchantment.DisplayName}</color>");
        foreach (StatBonus bonus in stack.Enchantment.Bonuses)
        {
            builder.Append(bonus.value >= 0f ? $"\n<color={UIStyle.Current.GoodHex}>" : $"\n<color={UIStyle.Current.DangerHex}>").Append(bonus.Describe()).Append("</color>");
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
