using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One recipe in a RecipeBrowser's list: what it makes, its tier, and in a word or two whether it can be made and why
/// not. Reports clicks; the browser decides what they do.
/// </summary>
public sealed class RecipeRowView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color DimIcon = new Color(1f, 1f, 1f, 0.45f);

    [SerializeField] private Image background;
    [Tooltip("The thin line that marks the selected row.")]
    [SerializeField] private Image selectedLine;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text tierLabel;

    private bool isSelected;
    private bool hovered;

    /// <summary>Raised when the row is clicked.</summary>
    public event Action<RecipeRowView, PointerEventData> Clicked;

    public RecipeDefinition Recipe { get; private set; }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this, eventData);

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Paint();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Paint();
    }

    /// <summary>Shows the recipe. A count above zero says how many can be made; otherwise the problem does.</summary>
    /// <param name="muted">The problem isn't something to fix here, such as a recipe made at another station.</param>
    public void Show(RecipeDefinition recipe, int count, string problem, bool selected, bool muted)
    {
        Recipe = recipe;
        isSelected = selected;
        UIStyle style = UIStyle.Current;
        bool canMake = count > 0;

        icon.sprite = recipe.OutputIcon;
        icon.enabled = icon.sprite != null;
        icon.color = canMake ? Color.white : DimIcon;
        nameLabel.text = recipe.OutputAmount > 1 ? $"{recipe.DisplayName} ×{recipe.OutputAmount}" : recipe.DisplayName;
        nameLabel.color = canMake ? style.Text : style.Muted;
        tierLabel.text = Tiers.ToRoman(recipe.Tier);
        tierLabel.color = Tiers.ColorOf(recipe.Tier);
        statusLabel.text = canMake ? $"Can make {count}" : problem;
        statusLabel.color = canMake ? style.Good : muted ? style.Muted : style.Danger;
        Paint();
    }

    private void Paint()
    {
        UIStyle style = UIStyle.Current;
        background.color = isSelected ? style.ControlLit : hovered ? style.Control : style.Inset;
        selectedLine.enabled = isSelected;
    }
}
