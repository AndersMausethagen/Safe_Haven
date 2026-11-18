using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The recipe browser made quicker to use (Hearthline 3, the user's bag and crafting picture), the same in the bag and at
/// every station: a search box over the list (by name, as it is typed), favourites marked with a star on their row and
/// kept at the top of the list (kept between games, PlayerPrefs: a convenience of this player's, not of the save), and an
/// amount picker by the make button (1 up to as many as can be made; the button says how many). Built once, in code.
/// </summary>
public sealed partial class RecipeBrowser
{
    // Not a const: CoopWireTests reads every "SafeHaven." constant as a co-op message name, and this is a PlayerPrefs key.
    private static readonly string FavouritesKey = "SafeHaven.FavouriteRecipes";

    private static HashSet<string> favourites;

    private KitSearch search;
    private string searchText = "";
    private int amount = 1;
    private RecipeDefinition amountFor;
    private TMP_Text amountLabel;
    private Button fewer;
    private Button more;
    private TMP_Text makeLabel;
    private string makeWord;
    private bool kitBuilt;

    private static HashSet<string> Favourites
    {
        get
        {
            if (favourites == null)
            {
                favourites = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in PlayerPrefs.GetString(FavouritesKey, "").Split('|'))
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        favourites.Add(id);
                    }
                }
            }

            return favourites;
        }
    }

    public static bool IsFavourite(RecipeDefinition recipe) => recipe != null && Favourites.Contains(recipe.Id);

    private void ToggleFavourite(RecipeDefinition recipe)
    {
        if (recipe == null)
        {
            return;
        }

        if (!Favourites.Remove(recipe.Id))
        {
            Favourites.Add(recipe.Id);
        }

        PlayerPrefs.SetString(FavouritesKey, string.Join("|", Favourites));
        Refresh();
    }

    // What the list shows: those whose name has the search's words, the favourites first, each kept in its own order.
    private List<RecipeDefinition> Arrange(List<RecipeDefinition> open)
    {
        var arranged = new List<RecipeDefinition>(open.Count);
        string wanted = searchText.Trim();
        foreach (bool favourite in new[] { true, false })
        {
            foreach (RecipeDefinition recipe in open)
            {
                if (IsFavourite(recipe) == favourite && (wanted.Length == 0 || recipe.DisplayName.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    arranged.Add(recipe);
                }
            }
        }

        return arranged;
    }

    private void BuildKit()
    {
        if (kitBuilt || list == null)
        {
            return;
        }

        kitBuilt = true;
        // The search box at the top of the list's scroll area, the list moved down under it.
        ScrollRect scroll = list.GetComponentInParent<ScrollRect>(true);
        if (scroll != null && scroll.viewport != null)
        {
            const float band = 42f;
            RectTransform viewport = scroll.viewport;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, viewport.offsetMax.y - band);
            var scrollRect = (RectTransform)scroll.transform;
            RectTransform box = CodeUI.Box("Search", scrollRect, new Vector2(0f, 1f), new Vector2(0f, 34f), Vector2.zero);
            box.anchorMax = new Vector2(1f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.offsetMin = new Vector2(2f, -36f);
            box.offsetMax = new Vector2(-12f, -2f);
            search = new KitSearch(box, "Search recipes...");
            search.Changed += text =>
            {
                searchText = text ?? "";
                Refresh();
            };
        }

        // The amount picker just above the make buttons: - n +.
        if (makeButton != null)
        {
            makeLabel = makeButton.GetComponentInChildren<TMP_Text>(true);
            makeWord = makeLabel != null ? makeLabel.text : "Make";
            var buttonRect = (RectTransform)makeButton.transform;
            var corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            float scale = buttonRect.lossyScale.x > 0f ? buttonRect.lossyScale.x : 1f;
            RectTransform row = CodeUI.Box("Amount", buttonRect.parent, new Vector2(0.5f, 0.5f), new Vector2(150f, 34f), Vector2.zero);
            row.position = (corners[1] + corners[2]) * 0.5f + new Vector3(0f, 26f, 0f) * scale;
            fewer = Step(row, "-", 0f, -1);
            more = Step(row, "+", 116f, 1);
            amountLabel = CodeUI.Label("Count", row, 36f, 0f, 78f, 34f, 20f, UIStyle.Current.AccentBright, TextAlignmentOptions.Center);
            amountLabel.fontStyle = FontStyles.Bold;
        }
    }

    private Button Step(RectTransform row, string sign, float x, int by)
    {
        RectTransform rect = CodeUI.At(sign == "-" ? "Fewer" : "More", row, x, 0f, 34f, 34f);
        UIStyle style = UIStyle.Current;
        Image fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.8f);
        fill.color = style.Control;
        HearthKit.Rim(rect, 0.5f, 1.8f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        HearthKit.Tint(button);
        UIHighlight.On(rect.gameObject);
        button.onClick.AddListener(() =>
        {
            amount += by;
            RefreshKit();
        });
        CodeUI.Label("Sign", rect, 0f, -1f, 34f, 34f, 22f, style.Text, TextAlignmentOptions.Center).text = sign;
        return button;
    }

    // The amount for the recipe shown: back to one for another recipe, never more than can be made.
    private void RefreshKit()
    {
        if (amountLabel == null)
        {
            return;
        }

        if (selected != amountFor)
        {
            amountFor = selected;
            amount = 1;
        }

        int most = selected != null && maker != null && !elsewhere.Contains(selected) ? Mathf.Max(1, maker.HowMany(selected)) : 1;
        amount = Mathf.Clamp(amount, 1, most);
        amountLabel.text = amount.ToString();
        fewer.interactable = amount > 1;
        more.interactable = amount < most;
        amountLabel.transform.parent.gameObject.SetActive(selected != null && !elsewhere.Contains(selected));
        if (makeLabel != null)
        {
            makeLabel.text = amount > 1 ? $"{makeWord} ×{amount}" : makeWord;
        }
    }

    private int AmountToMake => Mathf.Max(1, amount);

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        favourites = null;
    }
}
