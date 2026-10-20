using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One resource row: icon, name, a fill bar and "amount/capacity" (GDD §12.1, §18.4: "Grey Stone 43/100"). It only
/// displays what it is given and knows nothing about inventories, so the row's look can be reworked without touching any
/// data code. Only essences are bars; food is an item and is eaten from the bag.
/// </summary>
public sealed class ResourceBarView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Optional. Shows the resource's tier as a Roman numeral in the tier's color.")]
    [SerializeField] private TMP_Text tierLabel;
    [Tooltip("Child of the bar background. Its right edge is anchored to show the fill amount.")]
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text amountLabel;

    /// <summary>Applies the resource's configured name, icon, tier and color.</summary>
    public void Bind(ResourceDefinition shown)
    {
        nameLabel.text = shown.DisplayName;
        fill.color = shown.Color;
        icon.sprite = shown.Icon;
        icon.gameObject.SetActive(shown.Icon != null);
        if (tierLabel != null)
        {
            tierLabel.text = Tiers.ToRoman(shown.Tier);
            tierLabel.color = Tiers.ColorOf(shown.Tier);
        }
    }

    public void SetValues(int amount, int capacity)
    {
        float fraction = capacity > 0 ? Mathf.Clamp01((float)amount / capacity) : 0f;
        // Anchoring the right edge keeps the bar independent of any sprite or layout settings.
        fill.rectTransform.anchorMax = new Vector2(fraction, 1f);
        amountLabel.text = $"{amount}/{capacity}";
    }
}