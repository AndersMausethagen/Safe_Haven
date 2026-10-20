using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The list of every resource definition and category in the game. The UI builds its tabs and bars from it and saves
/// can resolve ids through it. Categories are listed separately so one can show up in the UI before it has any
/// resources. In the Editor, new definitions and categories are added automatically (see
/// ResourceCatalogPostprocessor); scripts that bring them in at runtime call Register / RegisterCategory.
/// Anything watching Changed refreshes itself.
/// </summary>
[CreateAssetMenu(fileName = "ResourceCatalog", menuName = "Safe Haven/Resources/Resource Catalog")]
public sealed class ResourceCatalog : ScriptableObject
{
    private static readonly Comparison<ResourceDefinition> DisplayOrder = CompareForDisplay;

    [SerializeField] private List<ResourceDefinition> resources = new List<ResourceDefinition>();
    [SerializeField] private List<ResourceCategory> categories = new List<ResourceCategory>();

    private Dictionary<string, ResourceDefinition> byId;

    public IReadOnlyList<ResourceDefinition> Resources => resources;
    public IReadOnlyList<ResourceCategory> Categories => categories;

    /// <summary>Raised after resources or categories are added to or removed from the catalog.</summary>
    public event Action Changed;

    /// <summary>Adds a resource if it isn't listed yet. Returns true when it was added.</summary>
    public bool Register(ResourceDefinition resource)
    {
        if (resource == null || resources.Contains(resource))
        {
            return false;
        }

        resources.Add(resource);
        byId = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Adds a category if it isn't listed yet. Returns true when it was added.</summary>
    public bool RegisterCategory(ResourceCategory category)
    {
        if (category == null || categories.Contains(category))
        {
            return false;
        }

        categories.Add(category);
        Changed?.Invoke();
        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor only: finds every ResourceDefinition and ResourceCategory in the project, adds the missing ones and
    /// drops entries whose asset is gone. Returns how many entries were added. Does nothing, and raises nothing,
    /// when the catalog is already up to date.
    /// </summary>
    public int CollectAllDefinitions()
    {
        var foundResources = FindAssetsOfType<ResourceDefinition>();
        var foundCategories = FindAssetsOfType<ResourceCategory>();

        int removed = resources.RemoveAll(entry => entry == null) + categories.RemoveAll(entry => entry == null);
        int added = AddMissing(resources, foundResources) + AddMissing(categories, foundCategories);

        if (added > 0 || removed > 0)
        {
            byId = null;
            UnityEditor.EditorUtility.SetDirty(this);
            Changed?.Invoke();
        }

        return added;
    }

    private static List<T> FindAssetsOfType<T>() where T : ScriptableObject
    {
        var found = new List<T>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                found.Add(asset);
            }
        }

        found.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return found;
    }

    private static int AddMissing<T>(List<T> list, List<T> candidates)
    {
        int added = 0;
        foreach (T candidate in candidates)
        {
            if (!list.Contains(candidate))
            {
                list.Add(candidate);
                added++;
            }
        }

        return added;
    }
#endif

    public bool TryGetResource(string id, out ResourceDefinition resource)
    {
        if (byId == null)
        {
            BuildIndex();
        }

        if (id != null && byId.TryGetValue(id, out resource))
        {
            return true;
        }

        resource = null;
        return false;
    }

    /// <summary>Fills results with every resource ordered by category, tier, resource order, then name.</summary>
    public void GetOrdered(List<ResourceDefinition> results)
    {
        results.Clear();
        foreach (ResourceDefinition resource in resources)
        {
            if (resource != null)
            {
                results.Add(resource);
            }
        }

        results.Sort(DisplayOrder);
    }

    /// <summary>
    /// Fills results with every listed category plus any category a resource uses that isn't listed, ordered by
    /// sort order then name. Categories without resources are included.
    /// </summary>
    public void GetCategoriesOrdered(List<ResourceCategory> results)
    {
        results.Clear();
        foreach (ResourceCategory category in categories)
        {
            if (category != null && !results.Contains(category))
            {
                results.Add(category);
            }
        }

        foreach (ResourceDefinition resource in resources)
        {
            if (resource != null && resource.Category != null && !results.Contains(resource.Category))
            {
                results.Add(resource.Category);
            }
        }

        results.Sort(CompareCategories);
    }

    private void OnValidate()
    {
        byId = null;

        var seen = new HashSet<string>();
        foreach (ResourceDefinition resource in resources)
        {
            if (resource != null && !seen.Add(resource.Id))
            {
                Debug.LogWarning($"ResourceCatalog has more than one resource with the id '{resource.Id}'.", this);
            }
        }

        // Fires when the list is edited in the Inspector or by an editor tool, so a UI watching Changed follows along.
        Changed?.Invoke();
    }

    private void BuildIndex()
    {
        byId = new Dictionary<string, ResourceDefinition>();
        foreach (ResourceDefinition resource in resources)
        {
            if (resource != null)
            {
                byId[resource.Id] = resource;
            }
        }
    }

    /// <summary>Display order: category, then tier, then the resource's sort order, then name.</summary>
    public static int CompareForDisplay(ResourceDefinition a, ResourceDefinition b)
    {
        // Resources without a category sort last.
        int result = CompareCategories(a.Category, b.Category);
        if (result != 0)
        {
            return result;
        }

        result = a.Tier.CompareTo(b.Tier);
        if (result != 0)
        {
            return result;
        }

        result = a.SortOrder.CompareTo(b.SortOrder);
        return result != 0 ? result : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sort order, then name. The final tie-break keeps two same-named categories from interleaving.</summary>
    public static int CompareCategories(ResourceCategory a, ResourceCategory b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a == null || b == null)
        {
            return a == null ? 1 : -1;
        }

        int result = a.SortOrder.CompareTo(b.SortOrder);
        if (result == 0)
        {
            result = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        return result != 0 ? result : a.GetEntityId().CompareTo(b.GetEntityId());
    }
}
