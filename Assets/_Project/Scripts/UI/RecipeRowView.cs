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
    private static readonly Color DimIcon = new Color(1f, 1f, 1f, 0.7f);

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

    /// <summary>Raised when its star is clicked: the recipe made a favourite, or no longer one.</summary>
    public event Action<RecipeRowView> Starred;

    private Image star;
    private Outline rim;
    private bool favourite;

    /// <summary>Shows whether it is a favourite (a gold star; a faint one to click otherwise).</summary>
    public void ShowFavourite(bool isFavourite)
    {
        favourite = isFavourite;
        UIStyle style = UIStyle.Current;
        if (star == null && style.Icon("Star") != null)
        {
            // On the name's line, beside the tier: the line under it is the recipe's needs, which run long.
            var rect = CodeUI.Box("Star", transform, new Vector2(1f, 1f), new Vector2(20f, 20f), new Vector2(-40f, -17f));
            star = rect.gameObject.AddComponent<Image>();
            star.sprite = style.Icon("Star");
            star.preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = star;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Starred?.Invoke(this));
            // The name gives way to it, so a long one ends before the star rather than under it.
            RectTransform name = nameLabel.rectTransform;
            name.offsetMax = new Vector2(name.offsetMax.x - 30f, name.offsetMax.y);
        }

        if (star != null)
        {
            star.color = favourite ? style.AccentBright : HearthKit.Faded(style.Inactive, hovered ? 0.8f : 0.3f);
        }
    }

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
    /// <param name="muted">The ordinary state of a list: materials still to gather, or a recipe made at another station. What can be made should stand out, not this.</param>
    /// <param name="waiting">How many are already on their way.</param>
    // What a row says it lacks, in the room it has: "Need 5 Grey Stone, 6 Wood" was cut mid-word and read as the whole cost. It says
    // what is missing, the first of it and how many more (the detail pane lists them all).
    private static string Shortfall(string problem)
    {
        if (string.IsNullOrEmpty(problem))
        {
            return problem;
        }

        // An aside in brackets is the detail pane's ("Needs the Charcoal Clamp fitted here (the Upgrade tab)" ran off the row).
        int aside = problem.IndexOf(" (", System.StringComparison.Ordinal);
        if (aside > 0)
        {
            problem = problem.Substring(0, aside);
        }

        if (!problem.StartsWith("Need ", System.StringComparison.Ordinal))
        {
            return problem;
        }

        string[] parts = problem.Substring(5).TrimEnd('.').Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? problem : parts.Length == 1 ? $"Missing {parts[0]}" : $"Missing {parts[0]} +{parts.Length - 1}";
    }

    public void Show(RecipeDefinition recipe, int count, string problem, bool selected, bool muted, int waiting)
    {
        Recipe = recipe;
        isSelected = selected;
        UIStyle style = UIStyle.Current;
        bool canMake = count > 0;

        icon.sprite = recipe.OutputIcon;
        icon.enabled = icon.sprite != null;
        icon.color = canMake ? Color.white : DimIcon;
        nameLabel.text = recipe.OutputAmount > 1 ? $"{recipe.DisplayName} ×{recipe.OutputAmount}" : recipe.DisplayName;
        nameLabel.color = canMake ? style.Text : style.Inactive;
        // Tier I is every early recipe's: its lone "I" at the row's edge read as a stray bar (design review 2026-11-28).
        tierLabel.text = recipe.Tier > 1 ? Tiers.ToRoman(recipe.Tier) : "";
        tierLabel.color = Tiers.ColorOf(recipe.Tier);
        string status = canMake ? $"Can make {count}" : Shortfall(problem);
        if (!statusLabel.enableAutoSizing)
        {
            // Shrinks a little to fit its line rather than being cut.
            statusLabel.fontSizeMax = statusLabel.fontSize;
            statusLabel.fontSizeMin = Mathf.Max(11f, statusLabel.fontSize - 4f);
            statusLabel.enableAutoSizing = true;
        }

        statusLabel.text = waiting > 0 ? $"{waiting} queued  ·  {status}" : status;
        statusLabel.color = canMake ? style.Good : muted ? style.Muted : style.Warning;
        Paint();
    }

    private void Paint()
    {
        UIStyle style = UIStyle.Current;
        background.color = isSelected ? style.ControlLit : hovered ? style.Control : style.Inset;
        selectedLine.enabled = isSelected;
        // Hearthline 3: the chosen recipe framed in gold.
        if (rim == null)
        {
            rim = background.TryGetComponent(out Outline had) ? had : background.gameObject.AddComponent<Outline>();
            rim.effectDistance = new Vector2(1.2f, -1.2f);
        }

        rim.effectColor = isSelected ? HearthKit.Faded(style.AccentBright, 0.85f) : new Color(0f, 0f, 0f, 0f);
        if (star != null)
        {
            ShowFavourite(favourite);
        }
    }
}
