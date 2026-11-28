using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Whatever owns a grid of ItemSlotViews (the bag window, the stash window) and decides what pointer input on a slot
/// means. The slot only reports it.
/// </summary>
public interface IItemSlotHandler
{
    void OnSlotClicked(ItemSlotView slot, PointerEventData eventData);
    void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData);
    void OnSlotDragged(PointerEventData eventData);
    void OnSlotDropped(ItemSlotView target);
    void OnSlotDragEnded();
    void OnSlotHovered(ItemSlotView slot);
    void OnSlotUnhovered(ItemSlotView slot);
}

/// <summary>
/// One cell of a slot grid: the bag, the stash, the skill window and the skill bar all use it. It only shows what it is
/// given (an item stack, or any icon such as a skill's) and reports pointer input (click, drag, drop, hover) to the
/// handler that owns it, which decides what that means.
/// </summary>
public sealed class ItemSlotView : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countLabel;
    [Tooltip("Optional. Shows the item's tier as a Roman numeral.")]
    [SerializeField] private TMP_Text tierLabel;
    [SerializeField] private Color emptyColor = new Color(0.12f, 0.13f, 0.16f, 0.9f);
    [SerializeField] private Color filledColor = new Color(0.18f, 0.19f, 0.23f, 0.95f);
    [SerializeField] private Color hoverColor = new Color(0.28f, 0.3f, 0.36f, 1f);
    [Tooltip("Frame color of a locked slot (no backpack unlocks it yet).")]
    [SerializeField] private Color lockedColor = new Color(0.05f, 0.05f, 0.06f, 0.3f);
    [Tooltip("Frame color of the equipped weapon's slot.")]
    [SerializeField] private Color equippedColor = new Color(0.86f, 0.66f, 0.3f, 1f);
    [Tooltip("How strongly a filled slot's frame takes on its item's rarity color. Common items are never tinted.")]
    [SerializeField, Range(0f, 1f)] private float rarityTint = 0.45f;

    private IItemSlotHandler owner;
    private bool hovered;
    private bool filled;
    private bool locked;
    private Color rarityColor;

    public int Index { get; private set; }

    public void Bind(IItemSlotHandler handler, int index)
    {
        owner = handler;
        Index = index;
        TakeStyle();
    }

    private bool styled;

    // Hearthline 3: the slot's colours from the style's browns (the prefab kept the ones it was made with): empty a darker
    // walnut, filled dark leather, pointed at the worn leather of what is chosen, locked grey (it can't be used).
    private void TakeStyle()
    {
        if (styled || !Application.isPlaying)
        {
            return;
        }

        styled = true;
        UIStyle style = UIStyle.Current;
        emptyColor = Color.Lerp(style.Panel, Color.black, 0.18f);
        filledColor = style.Inset;
        hoverColor = style.Filled;
        lockedColor = style.Disabled;
    }

    /// <param name="isLocked">Shows the slot as locked: it keeps its item but can take no new ones.</param>
    /// <param name="isEquipped">Highlights the slot as the equipped weapon.</param>
    /// <param name="isQuick">Marks the item as one on the quickbar (a small amber dot by its tier).</param>
    public void Show(ItemStack stack, bool isLocked = false, bool isEquipped = false, bool isQuick = false)
    {
        filled = !stack.IsEmpty;
        locked = isLocked;
        rarityColor = isEquipped && filled ? equippedColor
            : filled && stack.Item.Rarity != ItemRarity.Common
            ? Color.Lerp(filledColor, Rarity.ColorOf(stack.Item.Rarity), rarityTint)
            : filledColor;
        Color? worked = isEquipped ? null : GearGlow.SlotTint(stack, UIStyle.Current);
        if (worked.HasValue)
        {
            rarityColor = Color.Lerp(filledColor, worked.Value, rarityTint);
        }

        icon.enabled = filled && stack.Item.Icon != null;
        icon.sprite = filled ? stack.Item.Icon : null;
        icon.color = locked ? new Color(1f, 1f, 1f, 0.45f) : stack.IsBroken ? new Color(1f, 0.55f, 0.5f, 0.7f) : Color.white;
        bool special = filled && (stack.Item.Rarity >= ItemRarity.Uncommon || worked.HasValue);
        Light(special ? (worked ?? Rarity.ColorOf(stack.Item.Rarity)) : (Color?)null,
            filled && (stack.Item.Rarity >= ItemRarity.Epic || stack.Quality == ItemQuality.Masterwork));
        // Gear never stacks, so its corner shows wear instead of a count once it is damaged.
        // A water container shows how much it holds (red when empty).
        countLabel.text = filled && stack.Count > 1 ? stack.Count.ToString()
            : filled && stack.Item.IsLantern ? $"<color={(stack.LightHours < 1f ? UIStyle.Current.DangerHex : UIStyle.Current.GoodHex)}>{Mathf.CeilToInt(stack.LightHours)}h</color>"
            : filled && stack.Item.HoldsWater ? $"<color={(stack.Water < 0.5f ? UIStyle.Current.DangerHex : UIStyle.Current.WaterHex)}>{Mathf.RoundToInt(stack.Water)}</color>"
            : stack.IsDamaged ? $"<color={(stack.IsBroken ? UIStyle.Current.DangerHex : UIStyle.Current.WarningHex)}>{Mathf.RoundToInt(stack.Durability * 100f)}%</color>"
            : worked.HasValue ? $"<color=#{ColorUtility.ToHtmlStringRGB(worked.Value)}>♦</color>"
            : "";
        if (tierLabel != null)
        {
            string rare = filled && GameOptions.RarityLetters.On && stack.Item.Rarity != ItemRarity.Common ? Rarity.NameOf(stack.Item.Rarity).Substring(0, 1) : "";
            tierLabel.text = rare.Length > 0 ? $"{GearGlow.CornerText(stack)} {rare}" : GearGlow.CornerText(stack);
            if (filled && isQuick)
            {
                tierLabel.text = $"<color={UIStyle.Current.AccentHex}>\u2022</color> {tierLabel.text}".TrimEnd();
            }

            tierLabel.color = filled ? Tiers.ColorOf(stack.Item.Tier) : Color.clear;
        }

        HidePlaceholderOverIcon();
        UpdateFrame();
    }

    /// <summary>
    /// Shows something that isn't an item stack, such as a skill: an icon (null shows the slot empty), a frame tint, a
    /// corner label (top-left, where an item's tier goes) and a badge (bottom-right, where its count goes).
    /// </summary>
    public void Show(Sprite content, Color tint, string corner = "", string badge = "", bool isLocked = false)
    {
        filled = content != null;
        locked = isLocked;
        rarityColor = filled ? Color.Lerp(filledColor, tint, rarityTint) : filledColor;
        icon.enabled = filled;
        icon.sprite = content;
        icon.color = locked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        countLabel.text = badge ?? "";
        if (tierLabel != null)
        {
            tierLabel.text = corner ?? "";
            tierLabel.color = new Color(0.92f, 0.87f, 0.74f, 1f);
        }

        HidePlaceholderOverIcon();
        UpdateFrame();
    }

    // A faint word in the middle of an empty slot (what goes there): made on first use, from the count label's font.
    private TMP_Text placeholder;

    /// <summary>A faint word centred in the slot while it is empty, saying what goes there ("Tech", "Power"; the skill bar's kinds,
    /// 2026-11-27). Kept on one line, shrunk to fit. Empty or null hides it; it never shows over an icon.</summary>
    public void SetPlaceholder(string text) => SetPlaceholder(text, null);

    /// <summary>The placeholder word in a colour of its own (the skill bar's slot kinds), faint as ever.</summary>
    public void SetPlaceholder(string text, Color? colour)
    {
        bool show = !string.IsNullOrEmpty(text) && !filled;
        if (placeholder == null)
        {
            if (!show || countLabel == null)
            {
                return;
            }

            var holder = new GameObject("Placeholder", typeof(RectTransform));
            holder.transform.SetParent(transform, false);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            var label = holder.AddComponent<TextMeshProUGUI>();
            label.font = countLabel.font;
            label.fontSharedMaterial = countLabel.fontSharedMaterial;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 6f;
            label.fontSizeMax = Mathf.Max(8f, countLabel.fontSize * 0.75f);
            label.raycastTarget = false;
            placeholder = label;
        }

        placeholder.text = show ? text : "";
        Color muted = colour ?? (UIStyle.Current != null ? UIStyle.Current.Muted : new Color(0.7f, 0.65f, 0.55f));
        muted.a = 0.75f;
        placeholder.color = muted;
        placeholder.enabled = show;
    }

    // Something now fills the slot: its placeholder word goes.
    private void HidePlaceholderOverIcon()
    {
        if (placeholder != null && filled)
        {
            placeholder.enabled = false;
        }
    }

    /// <summary>The icon as a dark shape only: something not yet had (a skill not learned), shown as a goal (UX pass 2026-11-28).</summary>
    public void SetSilhouette(float strength = 0.85f)
    {
        if (filled)
        {
            // Dark enough to read as not had, light enough that its shape reads on the slot's dark ground.
            icon.color = new Color(0.46f, 0.42f, 0.37f, strength);
        }
    }

    /// <summary>Greys the icon (a skill that can't be paid for yet), or shows it as Show left it.</summary>
    public void SetDimmed(bool dimmed)
    {
        if (filled)
        {
            icon.color = dimmed ? new Color(0.55f, 0.55f, 0.55f, 0.6f) : locked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        }
    }

    public void OnPointerClick(PointerEventData eventData) => owner?.OnSlotClicked(this, eventData);

    public void OnBeginDrag(PointerEventData eventData) => owner?.OnSlotDragStarted(this, eventData);

    public void OnDrag(PointerEventData eventData) => owner?.OnSlotDragged(eventData);

    public void OnEndDrag(PointerEventData eventData) => owner?.OnSlotDragEnded();

    public void OnDrop(PointerEventData eventData) => owner?.OnSlotDropped(this);

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        UpdateFrame();
        owner?.OnSlotHovered(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        UpdateFrame();
        owner?.OnSlotUnhovered(this);
    }

    private void OnDisable()
    {
        hovered = false;
        UpdateFrame();
    }

    private Image rarityLight;
    private SlotShimmer shimmer;

    // Hearthline 2: a rare thing glows from behind its icon in its rarity's colour, and the best (Epic and up, a
    // Masterwork) catch a slow shine now and then.
    private void Light(Color? colour, bool shine)
    {
        if (icon == null)
        {
            return;
        }

        if (colour.HasValue && rarityLight == null && UIStyle.Current.Glow != null)
        {
            var rect = (RectTransform)new GameObject("Rarity Light", typeof(RectTransform)).transform;
            rect.SetParent(icon.transform.parent, false);
            rect.SetSiblingIndex(icon.transform.GetSiblingIndex());
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-6f, -6f);
            rect.offsetMax = new Vector2(6f, 6f);
            rarityLight = rect.gameObject.AddComponent<Image>();
            rarityLight.sprite = UIStyle.Current.Glow;
            rarityLight.raycastTarget = false;
        }

        if (rarityLight != null)
        {
            rarityLight.enabled = colour.HasValue;
            if (colour.HasValue)
            {
                Color c = colour.Value;
                c.a = 0.42f;
                rarityLight.color = c;
            }
        }

        if (shine && shimmer == null)
        {
            shimmer = SlotShimmer.Add(icon.rectTransform);
        }

        if (shimmer != null)
        {
            shimmer.enabled = shine;
        }
    }

    private void UpdateFrame()
    {
        if (frame != null)
        {
            frame.color = hovered ? hoverColor : locked && !filled ? lockedColor : filled ? rarityColor : emptyColor;
        }
    }
}
