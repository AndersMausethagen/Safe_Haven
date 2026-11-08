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
    }

    /// <param name="isLocked">Shows the slot as locked: it keeps its item but can take no new ones.</param>
    /// <param name="isEquipped">Highlights the slot as the equipped weapon.</param>
    public void Show(ItemStack stack, bool isLocked = false, bool isEquipped = false)
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
            tierLabel.color = filled ? Tiers.ColorOf(stack.Item.Tier) : Color.clear;
        }

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

        UpdateFrame();
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
