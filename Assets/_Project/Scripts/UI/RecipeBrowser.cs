using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Choosing and making recipes, the same in the bag (B) and at every station: tabs by what recipes make and a Can Make
/// filter over a list of named rows, and beside it the selected recipe with what it does, each material against what is
/// held, how many the player already owns, and the buttons to make one or as many as possible. A right-click or
/// double-click on a row makes one straight away. A shortage of materials is the everyday state of most of a list, so it
/// stays quiet and what can be made stands out. It reads and makes through an IRecipeMaker; the rules stay in
/// PlayerCrafting and StationCrafting.
/// Built by Safe Haven › Setup › UI › Build Recipe Browsers.
/// </summary>
public sealed class RecipeBrowser : MonoBehaviour
{
    [Header("List")]
    [Tooltip("Parent for the tab buttons. Should have a horizontal layout group.")]
    [SerializeField] private RectTransform tabBar;
    [Tooltip("A hidden button copied for each tab: a label and a line under it (the child called Line) that marks the open tab.")]
    [SerializeField] private Button tabTemplate;
    [Tooltip("Switches the list to what can be made now. Built like a tab.")]
    [SerializeField] private Button canMakeButton;
    [Tooltip("Parent for the rows. Should be a ScrollRect's content with a layout group.")]
    [SerializeField] private RectTransform list;
    [SerializeField] private RecipeRowView rowPrefab;
    [Tooltip("Shown when the list is empty.")]
    [SerializeField] private TMP_Text emptyLabel;

    [Header("Selected recipe")]
    [SerializeField] private GameObject selectedPanel;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Where it is made and how long it takes.")]
    [SerializeField] private TMP_Text infoLabel;
    [Tooltip("Inside a ScrollRect: gear with many bonuses says more than fits.")]
    [SerializeField] private TMP_Text descriptionLabel;
    [Tooltip("Parent for the material rows. Should have a vertical layout group.")]
    [SerializeField] private RectTransform costList;
    [SerializeField] private RecipeCostRow costTemplate;
    [Tooltip("What stands in the way of the recipe, or else how many the player already owns.")]
    [SerializeField] private TMP_Text problemLabel;
    [SerializeField] private Button makeButton;
    [SerializeField] private Button makeAllButton;

    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<RecipeRowView> rows = new List<RecipeRowView>();
    private readonly List<RecipeCostRow> costRows = new List<RecipeCostRow>();
    private readonly List<RecipeDefinition> here = new List<RecipeDefinition>();
    private readonly HashSet<RecipeDefinition> elsewhere = new HashSet<RecipeDefinition>();
    private List<RecipeDefinition> shown = new List<RecipeDefinition>();
    private List<RecipeTabs.Tab> tabs = new List<RecipeTabs.Tab>();
    private IRecipeMaker maker;
    private string activeTab;
    private bool onlyMakeable;
    private RecipeDefinition selected;
    private RecipeDefinition described;
    private string emptyText;
    private bool wired;

    /// <summary>Raised after something was made, so the window can show its effects.</summary>
    public event Action Made;

    /// <summary>
    /// Fills the browser. <paramref name="recipes"/> are made through <paramref name="source"/>; the known recipes made
    /// somewhere else go in <paramref name="madeElsewhere"/> and get a tab of their own, to plan with.
    /// </summary>
    public void Show(IRecipeMaker source, IEnumerable<RecipeDefinition> recipes, IEnumerable<RecipeDefinition> madeElsewhere = null)
    {
        Wire();
        maker = source;
        described = null;
        here.Clear();
        here.AddRange(recipes);
        elsewhere.Clear();
        tabs = RecipeTabs.Build(here);
        if (madeElsewhere != null)
        {
            var away = new RecipeTabs.Tab(RecipeTabs.Stations);
            foreach (RecipeDefinition recipe in madeElsewhere)
            {
                away.Recipes.Add(recipe);
                elsewhere.Add(recipe);
            }

            if (away.Recipes.Count > 0)
            {
                tabs.Add(away);
            }
        }

        if (TabNamed(activeTab) == null)
        {
            activeTab = tabs.Count > 0 ? tabs[0].Label : null;
        }

        BuildTabButtons();
        Refresh();
    }

    /// <summary>Redraws what can be made from what the player holds now.</summary>
    public void Refresh()
    {
        if (maker == null)
        {
            return;
        }

        Wire();
        RefreshFilter();
        RefreshList();
        RefreshSelected();
    }

    // Children of a closed window wake late, after their window has already filled them.
    private void Wire()
    {
        if (wired)
        {
            return;
        }

        wired = true;
        emptyText = emptyLabel.text;
        tabTemplate.gameObject.SetActive(false);
        costTemplate.gameObject.SetActive(false);
        canMakeButton.onClick.AddListener(() =>
        {
            onlyMakeable = !onlyMakeable;
            Refresh();
        });
        makeButton.onClick.AddListener(() => Make(selected, 1));
        makeAllButton.onClick.AddListener(() => Make(selected, maker.HowMany(selected)));
    }

    private void Make(RecipeDefinition recipe, int count)
    {
        if (maker != null && recipe != null && !elsewhere.Contains(recipe))
        {
            maker.Make(recipe, count);
            Made?.Invoke();
            Refresh();
        }
    }

    // ----- Tabs -----

    private RecipeTabs.Tab TabNamed(string label)
    {
        foreach (RecipeTabs.Tab tab in tabs)
        {
            if (tab.Label == label)
            {
                return tab;
            }
        }

        return null;
    }

    private void BuildTabButtons()
    {
        // A single tab says nothing, so the bar only shows with two or more.
        int shownTabs = tabs.Count > 1 ? tabs.Count : 0;
        while (tabButtons.Count < shownTabs)
        {
            Button button = Instantiate(tabTemplate, tabBar);
            int index = tabButtons.Count;
            button.onClick.AddListener(() => SelectTab(index));
            tabButtons.Add(button);
        }

        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool used = i < shownTabs;
            tabButtons[i].gameObject.SetActive(used);
            if (used)
            {
                tabButtons[i].name = "Tab " + tabs[i].Label;
                tabButtons[i].GetComponentInChildren<TMP_Text>().text = tabs[i].Label;
            }
        }

        PaintTabs();
    }

    private void SelectTab(int index)
    {
        if (index >= 0 && index < tabs.Count)
        {
            activeTab = tabs[index].Label;
            PaintTabs();
            Refresh();
        }
    }

    private void PaintTabs()
    {
        for (int i = 0; i < tabButtons.Count && i < tabs.Count; i++)
        {
            Paint(tabButtons[i], tabs[i].Label == activeTab);
        }
    }

    private static void Paint(Button tab, bool on)
    {
        tab.GetComponentInChildren<TMP_Text>().color = on ? UIStyle.Current.Accent : UIStyle.Current.Muted;
        tab.transform.Find("Line").gameObject.SetActive(on);
    }

    private void RefreshFilter()
    {
        canMakeButton.GetComponentInChildren<TMP_Text>().text = $"Can make ({RecipeTabs.OnlyMakeable(here, CanMake).Count})";
        Paint(canMakeButton, onlyMakeable);
    }

    private bool CanMake(RecipeDefinition recipe) => !elsewhere.Contains(recipe) && maker.HowMany(recipe) > 0;

    // ----- List -----

    private void RefreshList()
    {
        RecipeTabs.Tab tab = TabNamed(activeTab);
        List<RecipeDefinition> open = tab != null ? tab.Recipes : new List<RecipeDefinition>();
        shown = onlyMakeable ? RecipeTabs.OnlyMakeable(open, CanMake) : open;
        if (!shown.Contains(selected))
        {
            RecipeDefinition first = shown.Find(CanMake);
            selected = first != null || shown.Count == 0 ? first : shown[0];
        }

        while (rows.Count < shown.Count)
        {
            RecipeRowView row = Instantiate(rowPrefab, list);
            row.Clicked += OnRowClicked;
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < shown.Count;
            rows[i].gameObject.SetActive(used);
            if (used)
            {
                RecipeDefinition recipe = shown[i];
                bool away = elsewhere.Contains(recipe);
                int count = away ? 0 : maker.HowMany(recipe);
                bool materials = false;
                string problem = away ? recipe.Station.DisplayName() : count > 0 ? null : RecipeMaking.Problem(maker, recipe, out materials);
                rows[i].name = recipe.DisplayName;
                rows[i].Show(recipe, count, problem, recipe == selected, away || materials, away ? 0 : maker.Waiting(recipe));
            }
        }

        emptyLabel.gameObject.SetActive(shown.Count == 0);
        emptyLabel.text = tabs.Count == 0 ? emptyText : "Nothing here can be made right now. Clear the Can Make filter to see what is missing.";
    }

    private void OnRowClicked(RecipeRowView row, PointerEventData eventData)
    {
        selected = row.Recipe;
        if (eventData.button == PointerEventData.InputButton.Right || eventData.clickCount >= 2)
        {
            Make(selected, 1);
        }
        else
        {
            Refresh();
        }
    }

    // ----- The selected recipe -----

    private void RefreshSelected()
    {
        selectedPanel.SetActive(selected != null);
        if (selected == null)
        {
            described = null;
            return;
        }

        if (selected != described)
        {
            described = selected;
            icon.sprite = selected.OutputIcon;
            icon.enabled = icon.sprite != null;
            nameLabel.text = selected.OutputAmount > 1 ? $"{selected.DisplayName} ×{selected.OutputAmount}" : selected.DisplayName;
            nameLabel.color = selected.Output != null ? Rarity.ColorOf(selected.Output.Rarity) : UIStyle.Current.Accent;
            float seconds = elsewhere.Contains(selected) ? selected.CraftSeconds : maker.Seconds(selected);
            infoLabel.text = selected.IsHandcrafted ? "By hand" : $"{selected.Station.DisplayName()}  ·  {ItemTooltipView.FormatSeconds(seconds)}";
            descriptionLabel.text = ItemTooltipView.DescribeOutput(selected);
            descriptionLabel.rectTransform.anchoredPosition = Vector2.zero;
        }

        RefreshCosts();
        bool here = !elsewhere.Contains(selected);
        int count = here ? maker.HowMany(selected) : 0;
        // A shortage shows in the Needs list right above, so this line only speaks up for what that can't say.
        bool materials = false;
        string problem = here ? RecipeMaking.Problem(maker, selected, out materials) : null;
        bool blocked = problem != null && !materials;
        problemLabel.text = blocked ? problem : Owned(selected, here);
        problemLabel.color = blocked ? UIStyle.Current.Warning : UIStyle.Current.Muted;
        makeButton.gameObject.SetActive(here);
        makeButton.interactable = count > 0;
        makeButton.GetComponentInChildren<TMP_Text>().text = maker.Verb;
        makeAllButton.gameObject.SetActive(here);
        makeAllButton.interactable = count > 1;
        makeAllButton.GetComponentInChildren<TMP_Text>().text = count > 1 ? $"{maker.Verb} ×{count}" : $"{maker.Verb} all";
    }

    private string Owned(RecipeDefinition recipe, bool here)
    {
        int owned = RecipeMaking.Owned(maker, recipe);
        int waiting = here ? maker.Waiting(recipe) : 0;
        string have = owned > 0 ? $"You have {owned}" : "";
        string queued = waiting > 0 ? $"{waiting} queued" : "";
        return have.Length > 0 && queued.Length > 0 ? $"{have}  ·  {queued}" : have + queued;
    }

    private void RefreshCosts()
    {
        MaterialPool pool = maker.Pool;
        int used = 0;
        foreach (ResourceAmount cost in selected.ResourceCost)
        {
            if (cost.Resource != null)
            {
                CostRow(used++).Show(cost.Resource.Icon, cost.Resource.DisplayName, pool.Have(cost.Resource), cost.Amount);
            }
        }

        foreach (ItemAmount cost in selected.ItemCost)
        {
            if (cost.Item != null)
            {
                CostRow(used++).Show(cost.Item.Icon, cost.Item.DisplayName, pool.Have(cost.Item), cost.Amount);
            }
        }

        for (int i = 0; i < costRows.Count; i++)
        {
            costRows[i].gameObject.SetActive(i < used);
        }
    }

    private RecipeCostRow CostRow(int index)
    {
        while (costRows.Count <= index)
        {
            costRows.Add(Instantiate(costTemplate, costList));
        }

        costRows[index].gameObject.SetActive(true);
        return costRows[index];
    }
}
