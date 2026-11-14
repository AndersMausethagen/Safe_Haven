#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Small trees and saplings (`unity command script run --file AgentScripts/BuildSaplings.cs --entry BuildSaplings.Apply`,
/// with SafeHaven open and saved):
/// - Young Tree: the node a small tree's stand-in is (by hand, 2 to 3 Sticks and a Cedar Sapling, never grows back), and
///   TerrainSmallTreeProxy (the tree proxy without its felling); TreeChoppingSettings › Small Trees uses them below 6 m.
/// - Cedar Sapling: planted at home (right-click), it grows into one of the Haven's cedars in 3 in-game days. Felling a
///   tree also gives one (the Tree node's yields).
/// - PlantedTrees on the Haven's terrain, beside its TerrainTreeMemory.
/// Re-runnable.
/// </summary>
public static class BuildSaplings
{
    private const string TreeNode = "Assets/_Project/Data/Gathering/Tree.asset";
    private const string SticksNode = "Assets/_Project/Data/Gathering/Sticks.asset";
    private const string YoungTreeNode = "Assets/_Project/Data/Gathering/YoungTree.asset";
    private const string TreeProxy = "Assets/_Project/Prefabs/Systems/TerrainTreeProxy.prefab";
    private const string SmallProxy = "Assets/_Project/Prefabs/Systems/TerrainSmallTreeProxy.prefab";
    private const string SaplingItem = "Assets/_Project/Data/Items/Materials/CedarSapling.asset";
    private const string SaplingData = "Assets/_Project/Data/Items/Saplings/Cedar Sapling.asset";
    private const string SticksItem = "Assets/_Project/Data/Items/Materials/Sticks.asset";
    private const string Leaves = "Assets/_Project/Prefabs/Effects/Gathering/LeafFall.prefab";
    private const string IconFolder = "Assets/_Project/Art/Generated/Saplings";
    private static readonly string[] Grown = { "Cedar A", "Cedar B", "Cedar C", "Cedar D", "Cedar Slender" };

    public static string Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven" || scene.isDirty)
        {
            return "open SafeHaven, saved, first";
        }

        Terrain home = null;
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain.GetComponent<TerrainTreeMemory>() != null)
            {
                home = terrain;
            }
        }

        if (home == null)
        {
            return "no Haven terrain with TerrainTreeMemory";
        }

        var report = new List<string>();
        var prefabs = new List<GameObject>();
        GameObject saplingModel = null;
        foreach (TreePrototype prototype in home.terrainData.treePrototypes)
        {
            if (prototype.prefab == null) continue;
            if (System.Array.IndexOf(Grown, prototype.prefab.name) >= 0) prefabs.Add(prototype.prefab);
            if (prototype.prefab.name == "Cedar Sapling") saplingModel = prototype.prefab;
        }

        ItemDefinition sapling = Sapling(prefabs, saplingModel);
        report.Add($"Cedar Sapling grows into {prefabs.Count} cedars");

        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>(SticksItem);
        GatheringNodeDefinition young = YoungTree(sticks, sapling);
        GameObject proxy = SmallTreeProxy(young);
        report.Add("Young Tree node and small tree proxy");

        AddYield(AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(TreeNode), sapling, 1, 1);
        report.Add("felling a tree gives a Cedar Sapling");

        var settings = new SerializedObject(TreeChoppingSettings.Current);
        settings.FindProperty("smallTreeHeight").floatValue = 6f;
        settings.FindProperty("smallTreeProxy").objectReferenceValue = proxy;
        settings.FindProperty("smallTreeBreak").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Leaves);
        settings.ApplyModifiedPropertiesWithoutUndo();

        if (home.GetComponent<PlantedTrees>() == null)
        {
            Light sun = RenderSettings.sun;
            Undo.AddComponent<PlantedTrees>(home.gameObject);
            // Saving here once dropped the scene's sun reference: it is put back if so.
            if (RenderSettings.sun == null && sun != null) RenderSettings.sun = sun;
            EditorSceneManager.SaveScene(scene);
            report.Add("PlantedTrees on " + home.name);
        }

        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static ItemDefinition Sapling(List<GameObject> grows, GameObject model)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SaplingData));
        System.IO.Directory.CreateDirectory(IconFolder);
        var data = AssetDatabase.LoadAssetAtPath<SaplingDefinition>(SaplingData);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<SaplingDefinition>();
            AssetDatabase.CreateAsset(data, SaplingData);
        }

        var so = new SerializedObject(data);
        SerializedProperty list = so.FindProperty("growsInto");
        list.arraySize = grows.Count;
        for (int i = 0; i < grows.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = grows[i];
        so.FindProperty("growDays").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(SaplingItem);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, SaplingItem);
        }

        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("id").stringValue = "cedar_sapling";
        itemSo.FindProperty("displayName").stringValue = "Cedar Sapling";
        itemSo.FindProperty("description").stringValue = "A young cedar, roots and a fist of earth. Plant it at home (right-click) " +
            "and in a few days there is a tree where there wasn't one.";
        itemSo.FindProperty("category").intValue = (int)ItemCategory.Material;
        itemSo.FindProperty("tier").intValue = 1;
        itemSo.FindProperty("value").intValue = 3;
        itemSo.FindProperty("stackable").boolValue = true;
        itemSo.FindProperty("maximumStack").intValue = 20;
        itemSo.FindProperty("weight").floatValue = 0.3f;
        itemSo.FindProperty("sapling").objectReferenceValue = data;
        if (itemSo.FindProperty("icon").objectReferenceValue == null && model != null)
        {
            itemSo.FindProperty("icon").objectReferenceValue = IconRenderer.RenderToSprite(model, $"{IconFolder}/Cedar Sapling Icon.png");
        }

        itemSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static GatheringNodeDefinition YoungTree(ItemDefinition sticks, ItemDefinition sapling)
    {
        var young = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(YoungTreeNode);
        if (young == null)
        {
            AssetDatabase.CopyAsset(SticksNode, YoungTreeNode);
            young = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(YoungTreeNode);
        }

        var so = new SerializedObject(young);
        so.FindProperty("id").stringValue = "young_tree";
        so.FindProperty("displayName").stringValue = "Young Tree";
        so.FindProperty("gatherTool").intValue = (int)ToolType.None;
        so.FindProperty("gatherAnimation").intValue = (int)InteractionAnimation.GatherHigh;
        so.FindProperty("gatherSeconds").floatValue = 1.5f;
        so.FindProperty("depleteOnGather").boolValue = true;
        so.FindProperty("respawnSeconds").floatValue = 0f;
        // Its world object is the small tree stand-in (TreeChoppingSettings › Small Tree Proxy), not the copied Sticks node.
        so.FindProperty("worldPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Systems/TerrainSmallTreeProxy.prefab");
        so.FindProperty("worldModelPrefab").objectReferenceValue = null;
        SerializedProperty entries = so.FindProperty("yields.entries");
        entries.arraySize = 2;
        SetEntry(entries.GetArrayElementAtIndex(0), sticks, 2, 3);
        SetEntry(entries.GetArrayElementAtIndex(1), sapling, 1, 1);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(young);
        return young;
    }

    private static void SetEntry(SerializedProperty entry, ItemDefinition item, int least, int most)
    {
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("resource").objectReferenceValue = null;
        entry.FindPropertyRelative("minQuantity").intValue = least;
        entry.FindPropertyRelative("maxQuantity").intValue = most;
        entry.FindPropertyRelative("probability").floatValue = 1f;
    }

    private static void AddYield(GatheringNodeDefinition node, ItemDefinition item, int least, int most)
    {
        var so = new SerializedObject(node);
        SerializedProperty entries = so.FindProperty("yields.entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item)
            {
                return;
            }
        }

        entries.arraySize++;
        SetEntry(entries.GetArrayElementAtIndex(entries.arraySize - 1), item, least, most);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);
    }

    // The felling stand-in without its felling: a node gathered by hand.
    private static GameObject SmallTreeProxy(GatheringNodeDefinition young)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SmallProxy) == null)
        {
            AssetDatabase.CopyAsset(TreeProxy, SmallProxy);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SmallProxy);
        try
        {
            root.name = "TerrainSmallTreeProxy";
            ChoppableTree chop = root.GetComponent<ChoppableTree>();
            if (chop != null)
            {
                Object.DestroyImmediate(chop);
            }

            var node = new SerializedObject(root.GetComponent<ResourceNode>());
            node.FindProperty("node").objectReferenceValue = young;
            node.ApplyModifiedPropertiesWithoutUndo();
            // What breaking it gives, handed over when the work is done (the felling proxy pays out through its timber instead).
            var source = new SerializedObject(root.GetComponent<ResourceSource>());
            source.FindProperty("drops").FindPropertyRelative("entries").arraySize = 0;
            SerializedProperty yields = new SerializedObject(young).FindProperty("yields").FindPropertyRelative("entries");
            SerializedProperty drops = source.FindProperty("drops").FindPropertyRelative("entries");
            for (int i = 0; i < yields.arraySize; i++)
            {
                drops.InsertArrayElementAtIndex(i);
                SerializedProperty from = yields.GetArrayElementAtIndex(i);
                SerializedProperty to = drops.GetArrayElementAtIndex(i);
                to.FindPropertyRelative("item").objectReferenceValue = from.FindPropertyRelative("item").objectReferenceValue;
                to.FindPropertyRelative("resource").objectReferenceValue = from.FindPropertyRelative("resource").objectReferenceValue;
                to.FindPropertyRelative("minQuantity").intValue = from.FindPropertyRelative("minQuantity").intValue;
                to.FindPropertyRelative("maxQuantity").intValue = from.FindPropertyRelative("maxQuantity").intValue;
                to.FindPropertyRelative("probability").floatValue = from.FindPropertyRelative("probability").floatValue;
            }

            source.FindProperty("grantOnInteractionComplete").boolValue = true;
            source.ApplyModifiedPropertiesWithoutUndo();
            InteractionVerb verb = root.GetComponent<InteractionVerb>() ?? root.AddComponent<InteractionVerb>();
            var verbSo = new SerializedObject(verb);
            verbSo.FindProperty("verb").stringValue = "Break down";
            verbSo.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, SmallProxy);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
