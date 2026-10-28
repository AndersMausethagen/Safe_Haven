using UnityEditor;
using UnityEngine;

/// <summary>
/// Placeable level helpers (`unity command run_script --file AgentScripts/BuildLevelSpawners.cs --entry BuildLevelSpawners.All`):
/// the Mob Spawn Zone and Random Resource Node prefabs (in the Place catalog's Raid and Resources groups), and three
/// stone looks for the Grey Stone node. Re-running updates in place.
/// </summary>
public static class BuildLevelSpawners
{
    private const string ZonePath = "Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab";
    private const string NodePath = "Assets/_Project/Prefabs/Resources/Random Resource Node.prefab";

    public static string All()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ZonePath) == null)
        {
            var zone = new GameObject("Mob Spawn Zone");
            zone.AddComponent<MobSpawnZone>();
            PrefabUtility.SaveAsPrefabAsset(zone, ZonePath);
            Object.DestroyImmediate(zone);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(NodePath) == null)
        {
            var node = new GameObject("Random Resource Node");
            node.AddComponent<RandomResourceNode>();
            PrefabUtility.SaveAsPrefabAsset(node, NodePath);
            Object.DestroyImmediate(node);
        }

        var stone = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/GreyStone.asset");
        var data = new SerializedObject(stone);
        SerializedProperty variants = data.FindProperty("modelVariants");
        variants.ClearArray();
        foreach (string look in new[] { "Field Stone", "Grey Boulder", "Lichen Boulder" })
        {
            variants.InsertArrayElementAtIndex(variants.arraySize);
            variants.GetArrayElementAtIndex(variants.arraySize - 1).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Environment/World/{look}.prefab");
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stone);
        AssetDatabase.SaveAssets();
        return $"zone: {PrefabCatalog.CategoryOf(ZonePath, "Mob Spawn Zone")}, node: {PrefabCatalog.CategoryOf(NodePath, "Random Resource Node")}, " +
               $"grey stone looks: {stone.ModelVariants.Count}";
    }
}
