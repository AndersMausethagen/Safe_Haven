#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every tree can be felled (`unity command run_script --file AgentScripts/BuildChoppableTrees.cs --entry BuildChoppableTrees.All`):
/// renames Oak Wood to Wood, creates the TreeChoppingSettings and the shared "Tree" gathering node, and gives every tree
/// prefab of the Place catalog (and the generated vegetation trees) an Interactable, ResourceSource, ResourceNode and
/// ChoppableTree. Prefabs that already are gathering nodes are left alone; base prefabs go before their variants.
/// Re-running updates in place.
/// </summary>
public static class BuildChoppableTrees
{
    private const string Materials = "Assets/_Project/Data/Items/Materials/";
    private const string TreeNode = "Assets/_Project/Data/Gathering/Tree.asset";

    public static string All()
    {
        var log = new List<string>();
        ItemDefinition wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Materials + "OakWood.asset");
        Set(wood, so =>
        {
            so.FindProperty("displayName").stringValue = "Wood";
            so.FindProperty("description").stringValue = "Logs from any tree. Felled with an axe; a better axe and a bigger tree give more, and sometimes better wood.";
        });
        Set(AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/OakWood.asset"),
            so => so.FindProperty("displayName").stringValue = "Wood");
        Set(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StoneHatchet.asset"),
            so => so.FindProperty("description").stringValue = "A sharp stone lashed to a stick. Fells any tree; a better axe brings more logs and better wood.");

        var settings = AssetDatabase.LoadAssetAtPath<TreeChoppingSettings>("Assets/_Project/Resources/TreeChoppingSettings.asset");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TreeChoppingSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/_Project/Resources/TreeChoppingSettings.asset");
        }

        Set(settings, so =>
        {
            SerializedProperty woods = so.FindProperty("woodByTier");
            woods.ClearArray();
            foreach (string name in new[] { "OakWood", "Hardwood", "Elderwood" })
            {
                woods.InsertArrayElementAtIndex(woods.arraySize);
                woods.GetArrayElementAtIndex(woods.arraySize - 1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Materials + name + ".asset");
            }
        });

        GatheringNodeDefinition node = Node(wood);
        GameObject proxy = Proxy(node, wood);
        Set(settings, so => so.FindProperty("terrainTreeProxy").objectReferenceValue = proxy);
        log.Add($"wood renamed; settings, Tree node and terrain tree proxy ready");

        PrefabCatalog.Refresh();
        List<string> paths = PrefabCatalog.All.Where(e => e.Category == "Trees").Select(e => e.Path).ToList();
        paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Models/Generated/Vegetation" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<MeshFilter>(true).Any(f => f.name == "Trunk")));
        // Bases before variants, so a variant inherits the parts instead of getting them twice.
        paths = paths.Distinct()
            .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant ? 1 : 0)
            .ToList();

        foreach (string path in paths)
        {
            log.Add(MakeChoppable(path, node, wood));
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static GatheringNodeDefinition Node(ItemDefinition wood)
    {
        var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(TreeNode);
        if (node == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Gathering/OakWood.asset", TreeNode);
            node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(TreeNode);
            Set(node, so =>
            {
                so.FindProperty("id").stringValue = "tree";
                so.FindProperty("displayName").stringValue = "Tree";
                so.FindProperty("swings").intValue = 4;
                so.FindProperty("respawnSeconds").floatValue = 900f;
                so.FindProperty("gatherRange").floatValue = 1.8f;
                so.FindProperty("worldModelPrefab").objectReferenceValue = null;
                so.FindProperty("worldPrefab").objectReferenceValue = null;
                SetDrops(so.FindProperty("yields").FindPropertyRelative("entries"), wood);
            });
        }

        return node;
    }

    // The invisible stand-in for painted terrain trees: a trunk capsule (TerrainTreeFelling sizes it) and the Tree node.
    private static GameObject Proxy(GatheringNodeDefinition node, ItemDefinition wood)
    {
        const string path = "Assets/_Project/Prefabs/Systems/TerrainTreeProxy.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject("TerrainTreeProxy");
        root.AddComponent<CapsuleCollider>();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        MakeChoppable(path, node, wood);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? saved;
    }

    private static string MakeChoppable(string path, GatheringNodeDefinition node, ItemDefinition wood)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<ResourceNode>() != null)
            {
                return $"{path}: already a gathering node";
            }

            bool solid = root.GetComponentInChildren<Collider>(true) != null;
            if (!root.TryGetComponent(out Interactable interactable))
            {
                interactable = root.AddComponent<Interactable>();
            }
            Set(interactable, so =>
            {
                so.FindProperty("animationType").intValue = (int)node.GatherAnimation;
                so.FindProperty("duration").floatValue = node.GatherSeconds;
                so.FindProperty("range").floatValue = node.GatherRange;
                so.FindProperty("deactivateOnComplete").boolValue = false;
            });
            var source = root.AddComponent<ResourceSource>();
            Set(source, so =>
            {
                SetDrops(so.FindProperty("drops").FindPropertyRelative("entries"), wood);
                so.FindProperty("grantOnInteractionComplete").boolValue = false;
            });
            var resource = root.AddComponent<ResourceNode>();
            Set(resource, so => so.FindProperty("node").objectReferenceValue = node);
            root.AddComponent<ChoppableTree>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"{path}: choppable{(solid ? "" : " (no collider: reach is measured from its foot)")}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // One sure log of wood: the node's main yield, for icons and the minigame's bonus log. The felled tree's real logs
    // come from ChoppableTree.
    private static void SetDrops(SerializedProperty entries, ItemDefinition wood)
    {
        entries.ClearArray();
        entries.InsertArrayElementAtIndex(0);
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("item").objectReferenceValue = wood;
        entry.FindPropertyRelative("resource").objectReferenceValue = null;
        entry.FindPropertyRelative("minQuantity").intValue = 1;
        entry.FindPropertyRelative("maxQuantity").intValue = 1;
        entry.FindPropertyRelative("probability").floatValue = 1f;
    }

    private static void Set(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
