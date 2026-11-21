using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every piece of content a save file can mention, looked up by id: items, quests, residents and jobs (listed here and kept
/// in step with the project by ResourceCatalogPostprocessor), resources (from the resource catalog), recipes and buildings
/// (from the recipe book).
/// Loading a save uses it to turn the ids on disk back into definitions.
/// </summary>
[CreateAssetMenu(fileName = "ContentDatabase", menuName = "Safe Haven/Save/Content Database")]
public sealed class ContentDatabase : ScriptableObject, IContentLookup, IContentTeachers
{
    /// <summary>The name systems load it by, from a Resources folder.</summary>
    public const string ResourceName = "ContentDatabase";

    [Tooltip("Every item in the game. Filled in automatically in the Editor.")]
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();
    [SerializeField] private ResourceCatalog resourceCatalog;
    [SerializeField] private RecipeBook recipeBook;
    [Tooltip("Prefabs that can be spawned while playing and recreated after loading. Filled in automatically in the Editor.")]
    [SerializeField] private List<SaveableEntity> saveablePrefabs = new List<SaveableEntity>();
    [Tooltip("Every quest in the game. Filled in automatically in the Editor.")]
    [SerializeField] private List<QuestDefinition> quests = new List<QuestDefinition>();
    [Tooltip("Every resident in the game. Filled in automatically in the Editor.")]
    [SerializeField] private List<ResidentDefinition> residents = new List<ResidentDefinition>();
    [Tooltip("Every job in the game. Filled in automatically in the Editor.")]
    [SerializeField] private List<JobDefinition> jobs = new List<JobDefinition>();
    [Tooltip("Every gathering node, for nodes that pick one at random (RandomResourceNode).")]
    [SerializeField] private List<GatheringNodeDefinition> gatheringNodes = new List<GatheringNodeDefinition>();
    [Tooltip("Every skill (the skill book: starting ones and bosses' rewards). Filled in automatically in the Editor.")]
    [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();
    [Tooltip("Every building module (BuildingModules). Filled in automatically in the Editor.")]
    [SerializeField] private List<BuildingModuleDefinition> modules = new List<BuildingModuleDefinition>();

    [NonSerialized] private Dictionary<string, ItemDefinition> itemsById;
    [NonSerialized] private Dictionary<string, RecipeDefinition> recipesById;

    public IReadOnlyList<ItemDefinition> Items => items;
    public IReadOnlyList<GatheringNodeDefinition> GatheringNodes => gatheringNodes;
    public IReadOnlyList<QuestDefinition> Quests => quests;
    public IReadOnlyList<BuildingModuleDefinition> Modules => modules;

    public BuildingModuleDefinition FindModule(string id) =>
        string.IsNullOrEmpty(id) ? null : modules.Find(module => module != null && module.Id == id);

    public IReadOnlyList<ResidentDefinition> Residents => residents;

    public BuildingDefinition FindBuilding(string id)
    {
        if (string.IsNullOrEmpty(id) || recipeBook == null)
        {
            return null;
        }

        foreach (BuildingDefinition building in recipeBook.Buildings)
        {
            if (building != null && building.Id == id)
            {
                return building;
            }
        }

        return null;
    }

    public ResidentDefinition FindResident(string id)
    {
        return string.IsNullOrEmpty(id) ? null : residents.Find(resident => resident != null && resident.Id == id);
    }

    public JobDefinition FindJob(string id)
    {
        return string.IsNullOrEmpty(id) ? null : jobs.Find(job => job != null && job.Id == id);
    }

    public QuestDefinition FindQuest(string id)
    {
        return string.IsNullOrEmpty(id) ? null : quests.Find(quest => quest != null && quest.Id == id);
    }

    public SkillDefinition FindSkill(string id)
    {
        return string.IsNullOrEmpty(id) ? null : skills.Find(skill => skill != null && skill.Id == id);
    }

    /// <summary>The prefab a spawned object was made from, by the prefab id it was saved with.</summary>
    public SaveableEntity FindPrefab(string prefabId)
    {
        return string.IsNullOrEmpty(prefabId) ? null : saveablePrefabs.Find(prefab => prefab != null && prefab.PrefabId == prefabId);
    }

    public ItemDefinition FindItem(string id)
    {
        itemsById ??= Index(items, item => item.Id);
        return id != null && itemsById.TryGetValue(id, out ItemDefinition item) ? item : null;
    }

    public ResourceDefinition FindResource(string id)
    {
        return id != null && resourceCatalog != null && resourceCatalog.TryGetResource(id, out ResourceDefinition resource) ? resource : null;
    }

    public RecipeDefinition FindRecipe(string id)
    {
        recipesById ??= Index(recipeBook != null ? recipeBook.Recipes : Array.Empty<RecipeDefinition>(), recipe => recipe.Id);
        return id != null && recipesById.TryGetValue(id, out RecipeDefinition recipe) ? recipe : null;
    }

    private void OnValidate()
    {
        ClearIndex();
    }

    private void ClearIndex()
    {
        itemsById = null;
        recipesById = null;
    }

    private Dictionary<string, T> Index<T>(IReadOnlyList<T> entries, Func<T, string> idOf) where T : UnityEngine.Object
    {
        var index = new Dictionary<string, T>();
        foreach (T entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            string id = idOf(entry);
            if (index.ContainsKey(id))
            {
                Debug.LogWarning($"Two {typeof(T).Name}s share the id '{id}'; saves will always load '{index[id].name}'.", this);
                continue;
            }

            index.Add(id, entry);
        }

        return index;
    }

#if UNITY_EDITOR
    /// <summary>Editor only: lists every item in the project and drops deleted ones. Returns how many were added.</summary>
    public int CollectAll()
    {
        int removed = items.RemoveAll(entry => entry == null);
        int added = 0;
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !items.Contains(item))
            {
                items.Add(item);
                added++;
            }
        }

        if (added > 0 || removed > 0)
        {
            ClearIndex();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        CollectPrefabs();
        CollectQuests();
        CollectAll(ref residents);
        CollectAll(ref jobs);
        CollectAll(ref gatheringNodes);
        CollectAll(ref modules);
        CollectAll(ref skills);
        return added;
    }

    private void CollectAll<T>(ref List<T> into) where T : ScriptableObject
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

        if (found.Count != into.Count || !found.TrueForAll(into.Contains))
        {
            into = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    private void CollectQuests()
    {
        var found = new List<QuestDefinition>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(QuestDefinition)))
        {
            var quest = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (quest != null)
            {
                found.Add(quest);
            }
        }

        if (found.Count != quests.Count || !found.TrueForAll(quests.Contains))
        {
            quests = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    private void CollectPrefabs()
    {
        var found = new List<SaveableEntity>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.TryGetComponent(out SaveableEntity entity))
            {
                found.Add(entity);
            }
        }

        if (found.Count != saveablePrefabs.Count || !found.TrueForAll(saveablePrefabs.Contains))
        {
            saveablePrefabs = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
