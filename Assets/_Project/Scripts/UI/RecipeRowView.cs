using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One recipe in the crafting list: what it makes, what it costs (each cost green when the player has enough, red when
/// not), why it can't be crafted yet, and a Craft button. Reports when the pointer is over it, so its owner can show the
/// recipe's tooltip.
/// </summary>
public sealed class RecipeRowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly string Enough = "#8BE08B";
    private static readonly string Missing = "#FF7B6B";

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Shows the costs with rich text.")]
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button craftButton;

    private RecipeDefinition recipe;
    private Action<RecipeDefinition> onCraft;

    /// <summary>Raised with true when the pointer enters the row and false when it leaves.</summary>
    public event Action<RecipeRowView, bool> HoverChanged;

    public RecipeDefinition Recipe => recipe;

    private void Awake()
    {
        craftButton.onClick.AddListener(() => onCraft?.Invoke(recipe));
    }

    public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(this, true);

    public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(this, false);

    public void Bind(RecipeDefinition shown, Action<RecipeDefinition> craft)
    {
        recipe = shown;
        onCraft = craft;
        icon.sprite = shown.OutputIcon;
        icon.enabled = icon.sprite != null;
        string amount = shown.OutputAmount > 1 ? $" ×{shown.OutputAmount}" : "";
        nameLabel.text = $"{shown.DisplayName}{amount}  <size=13>{Tiers.RichLabel(shown.Tier)}</size>";
    }

    /// <summary>Shows how much of each cost the player has, and whether the recipe can be crafted.</summary>
    public void Refresh(PlayerCrafting crafting)
    {
        var builder = new StringBuilder();
        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null)
            {
                AppendCost(builder, cost.Resource.DisplayName, crafting.Have(cost.Resource), cost.Amount);
            }
        }

        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null)
            {
                AppendCost(builder, cost.Item.DisplayName, crafting.Have(cost.Item), cost.Amount);
            }
        }

        costLabel.text = builder.ToString();

        PlayerCrafting.CraftCheck check = crafting.Check(recipe);
        string where = recipe.Station == CraftingStationType.None ? "By hand" : recipe.Station.DisplayName();
        statusLabel.text = check.CanCraft ? where : $"{where}  ·  <color={Missing}>{check.Problem}</color>";
        craftButton.interactable = check.CanCraft;
    }

    private static void AppendCost(StringBuilder builder, string name, int have, int need)
    {
        if (builder.Length > 0)
        {
            builder.Append("   ");
        }

        builder.Append($"<color={(have >= need ? Enough : Missing)}>{need} {name}</color> <size=12>({have})</size>");
    }
}
