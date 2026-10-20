using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows the resources of a ResourceInventory as bars, one tab per category, each listed by tier with a tier badge.
/// Resources stay hidden until the player first collects them, so the panel starts clean and grows with exploration
/// (turn off <see cref="hideUndiscovered"/> to show everything). The list scrolls when it is taller than
/// <see cref="maxListHeight"/>, and the panel widens as tabs are added, up to <see cref="maxPanelWidth"/>.
/// Everything shown comes from the definitions in the catalog, so adding a resource or category needs no UI work.
/// The panel follows the catalog: new entries appear by themselves, and <see cref="Refresh"/> forces a rescan.
/// </summary>
public sealed class ResourceInventoryPanel : MonoBehaviour
{
    private const string EmptyMessage = "Nothing gathered here yet.";
    private const string NothingFoundMessage = "Nothing gathered yet. Press E next to a tree, rock or plant to gather it.";

    [Tooltip("Left empty, the first ResourceInventory in the scene is used.")]
    [SerializeField] private ResourceInventory inventory;
    [SerializeField] private ResourceCatalog catalog;

    [Header("Layout")]
    [Tooltip("Parent for the category tabs. Should have a layout group.")]
    [SerializeField] private RectTransform tabBar;
    [SerializeField] private ResourceCategoryTab tabPrefab;
    [Tooltip("Scroll view that holds the list. Its LayoutElement height is set to fit the content up to the maximum.")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("Parent for the headings and bars. Should be the ScrollRect's content and have a layout group.")]
    [SerializeField] private RectTransform content;
    [SerializeField] private ResourceBarView barPrefab;
    [Tooltip("Text used as a template for category headings and messages.")]
    [SerializeField] private TMP_Text headerPrefab;
    [SerializeField, Min(40)] private float maxListHeight = 360f;
    [Tooltip("The panel is never narrower than this.")]
    [SerializeField, Min(100)] private float minPanelWidth = 420f;
    [Tooltip("The panel widens to fit all the tabs up to this width. Beyond it the tab labels shrink instead.")]
    [SerializeField, Min(100)] private float maxPanelWidth = 1000f;
    [SerializeField] private bool showAllTab = true;
    [Tooltip("Hide resources, and categories, until the player has collected them once.")]
    [SerializeField] private bool hideUndiscovered = true;
    [Tooltip("Obsolete: essences are weightless, carry weight is shown by the bag. Hidden when set.")]
    [SerializeField] private TMP_Text carryLabel;

    [Header("Style")]
    [SerializeField, Min(1)] private int categoryFontSize = 20;
    [FormerlySerializedAs("typeFontSize")]
    [SerializeField, Min(1)] private int messageFontSize = 15;
    [FormerlySerializedAs("typeColor")]
    [SerializeField] private Color messageColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color allTabColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color otherTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    // Tab selection keys. A category tab uses the ResourceCategory itself as its key.
    private static readonly object AllKey = new object();
    private static readonly object OtherKey = new object();

    private readonly Dictionary<ResourceDefinition, ResourceBarView> bars = new Dictionary<ResourceDefinition, ResourceBarView>();
    private readonly Dictionary<object, ResourceCategoryTab> tabs = new Dictionary<object, ResourceCategoryTab>();
    private readonly List<object> tabKeys = new List<object>();
    private readonly List<ResourceDefinition> ordered = new List<ResourceDefinition>();
    private readonly List<ResourceCategory> orderedCategories = new List<ResourceCategory>();
    private ResourceStorage storage;
    private object selected = AllKey;
    private bool rebuildPending;

    private void OnEnable()
    {
        inventory = inventory != null ? inventory : Player.Current != null ? Player.Current.Resources : FindAnyObjectByType<ResourceInventory>();
        if (inventory == null || catalog == null || barPrefab == null || headerPrefab == null || tabPrefab == null
            || tabBar == null || scrollRect == null || content == null)
        {
            Debug.LogError("ResourceInventoryPanel is missing a reference (inventory, catalog, tab bar/prefab, scroll view, content, bar/header prefab).", this);
            return;
        }

        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            Debug.LogWarning("ResourceInventoryPanel: the scene has no EventSystem, so the tabs and scrolling can't be used.", this);
        }

        storage = inventory.Storage;
        storage.Changed += OnResourceChanged;
        storage.CapacitiesChanged += RefreshValues;
        storage.Discovered += OnDiscovered;
        catalog.Changed += OnCatalogChanged;
        // Layout components further down the hierarchy aren't enabled yet, so the first build waits for LateUpdate.
        rebuildPending = true;
    }

    private void OnDisable()
    {
        if (storage != null)
        {
            storage.Changed -= OnResourceChanged;
            storage.CapacitiesChanged -= RefreshValues;
            storage.Discovered -= OnDiscovered;
            storage = null;
            catalog.Changed -= OnCatalogChanged;
        }
    }

    private void LateUpdate()
    {
        // Catalog changes can arrive while the editor is validating assets, so the rebuild is done here instead.
        if (rebuildPending)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Looks for resources that were added or removed and updates the panel. In the editor this first rescans the
    /// project for Resource Definitions; in a build it shows whatever is registered in the catalog.
    /// </summary>
    [ContextMenu("Refresh Resources")]
    public void Refresh()
    {
        if (storage == null)
        {
            return;
        }

#if UNITY_EDITOR
        catalog.CollectAllDefinitions();
#endif
        Rebuild();
    }

    /// <summary>Recreates the tabs, headings and bars from the catalog, keeping the selected tab when it still exists.</summary>
    public void Rebuild()
    {
        if (storage == null)
        {
            return;
        }

        rebuildPending = false;
        catalog.GetOrdered(ordered);
        catalog.GetCategoriesOrdered(orderedCategories);
        BuildTabs();
        FitWidth();
        BuildList();
    }

    private void OnCatalogChanged() => rebuildPending = true;

    private void OnDiscovered(ResourceDefinition resource) => rebuildPending = true;

    private bool IsVisible(ResourceDefinition resource)
    {
        return !hideUndiscovered || storage.IsDiscovered(resource);
    }

    private void BuildTabs()
    {
        ClearChildren(tabBar);
        tabs.Clear();
        tabKeys.Clear();

        // A category gets a tab once one of its resources has been found; categories without resources get none.
        foreach (ResourceCategory category in orderedCategories)
        {
            if (ordered.Exists(resource => resource.Category == category && IsVisible(resource)))
            {
                tabKeys.Add(category);
            }
        }

        foreach (ResourceDefinition resource in ordered)
        {
            if (resource.Category == null && IsVisible(resource))
            {
                tabKeys.Add(OtherKey);
                break;
            }
        }

        // A single category makes the "All" tab redundant.
        if (showAllTab && tabKeys.Count != 1)
        {
            tabKeys.Insert(0, AllKey);
        }

        if (!tabKeys.Contains(selected))
        {
            selected = tabKeys.Count > 0 ? tabKeys[0] : AllKey;
        }

        foreach (object key in tabKeys)
        {
            object tabKey = key;
            ResourceCategoryTab tab = Instantiate(tabPrefab, tabBar);
            string label;
            Color accent;
            if (key == AllKey)
            {
                label = "All";
                accent = allTabColor;
            }
            else if (key == OtherKey)
            {
                label = "Other";
                accent = otherTabColor;
            }
            else
            {
                var category = (ResourceCategory)key;
                label = category.DisplayName;
                accent = category.Color;
            }

            tab.name = label;
            tab.Bind(label, accent, () => Select(tabKey));
            tab.SetSelected(tabKey == selected);
            tabs.Add(key, tab);
        }

        tabBar.gameObject.SetActive(tabKeys.Count > 0);
    }

    private void Select(object key)
    {
        if (key == selected)
        {
            return;
        }

        selected = key;
        foreach (KeyValuePair<object, ResourceCategoryTab> entry in tabs)
        {
            entry.Value.SetSelected(entry.Key == selected);
        }

        BuildList();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void BuildList()
    {
        ClearChildren(content);
        bars.Clear();

        bool showCategoryHeaders = selected == AllKey;
        ResourceCategory category = null;
        bool started = false;

        foreach (ResourceDefinition resource in ordered)
        {
            if (!IsShown(resource) || bars.ContainsKey(resource))
            {
                continue;
            }

            if (!started || resource.Category != category)
            {
                category = resource.Category;
                if (showCategoryHeaders && category != null)
                {
                    AddHeader(category.DisplayName, category.Color, categoryFontSize);
                }
            }

            started = true;

            ResourceBarView bar = Instantiate(barPrefab, content);
            bar.name = resource.DisplayName;
            bar.Bind(resource);
            bar.SetValues(storage.GetAmount(resource), storage.GetCapacity(resource));
            bars.Add(resource, bar);
        }

        if (bars.Count == 0)
        {
            bool anythingFound = ordered.Exists(IsVisible);
            AddHeader(anythingFound ? EmptyMessage : NothingFoundMessage, messageColor, messageFontSize);
        }

        FitScrollView();
        RefreshCarry();
    }

    private bool IsShown(ResourceDefinition resource)
    {
        if (!IsVisible(resource))
        {
            return false;
        }

        if (selected == AllKey)
        {
            return true;
        }

        return selected == OtherKey ? resource.Category == null : resource.Category == (ResourceCategory)selected;
    }

    // The panel is as wide as its tabs need, within the min and max width. Tabs ask for the width of their text.
    private void FitWidth()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(tabBar);
        float tabsWidth = LayoutUtility.GetPreferredWidth(tabBar);

        var group = GetComponent<LayoutGroup>();
        float padding = group != null ? group.padding.horizontal : 0f;
        float width = Mathf.Clamp(tabsWidth + padding, minPanelWidth, Mathf.Max(minPanelWidth, maxPanelWidth));
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    // The scroll view grows with its content until maxListHeight, then the content scrolls inside it.
    private void FitScrollView()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float height = Mathf.Min(LayoutUtility.GetPreferredHeight(content), maxListHeight);

        LayoutElement element = scrollRect.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = scrollRect.gameObject.AddComponent<LayoutElement>();
        }

        element.preferredHeight = height;
        element.minHeight = height;
    }

    private void AddHeader(string label, Color color, int fontSize)
    {
        TMP_Text header = Instantiate(headerPrefab, content);
        header.name = label;
        header.text = label;
        header.color = color;
        header.fontSize = fontSize;
    }

    private static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            // Deactivated first so layout ignores the children right away; Destroy only takes effect at end of frame.
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void RefreshValues()
    {
        foreach (KeyValuePair<ResourceDefinition, ResourceBarView> entry in bars)
        {
            entry.Value.SetValues(storage.GetAmount(entry.Key), storage.GetCapacity(entry.Key));
        }

        RefreshCarry();
    }

    private void RefreshCarry()
    {
        if (carryLabel == null || storage == null)
        {
            return;
        }

        carryLabel.gameObject.SetActive(false);
    }

    private void OnResourceChanged(ResourceChange change)
    {
        if (bars.TryGetValue(change.Resource, out ResourceBarView bar))
        {
            bar.SetValues(change.Amount, change.Capacity);
        }

        RefreshCarry();
    }
}
