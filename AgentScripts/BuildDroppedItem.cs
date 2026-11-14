#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>
/// The object a dropped stack becomes, its fallback sack and Resources/DroppedItemSettings. Run with
/// unity command run_script --file AgentScripts/BuildDroppedItem.cs --entry BuildDroppedItem.All
/// Makes objects in the open scene while it works: check the scene is clean after.
/// </summary>
public static class BuildDroppedItem
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Items/Dropped Item.prefab";
    private const string SettingsPath = "Assets/_Project/Resources/DroppedItemSettings.asset";
    private const string Tints = "Assets/_Project/Materials/Haven/";

    public static string All()
    {
        var cloth = AssetDatabase.LoadAssetAtPath<Material>(Tints + "Cargo Sack.mat");
        var cord = AssetDatabase.LoadAssetAtPath<Material>(Tints + "Cart Plank Dark.mat");
        var root = new GameObject("Dropped Item");
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        Transform sack = new GameObject("Sack").transform;
        sack.SetParent(visual, false);
        Piece(sack, PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0f), new Vector3(0.42f, 0.3f, 0.36f), cloth);
        Piece(sack, PrimitiveType.Sphere, new Vector3(0f, 0.31f, 0f), new Vector3(0.13f, 0.14f, 0.13f), cloth);
        Piece(sack, PrimitiveType.Cylinder, new Vector3(0f, 0.285f, 0f), new Vector3(0.115f, 0.012f, 0.115f), cord);

        var interactable = root.AddComponent<Interactable>();
        var so = new SerializedObject(interactable);
        so.FindProperty("kind").intValue = (int)InteractionKind.Use;
        so.FindProperty("deactivateOnComplete").boolValue = false;
        so.FindProperty("range").floatValue = 1.9f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var dropped = new SerializedObject(root.AddComponent<DroppedItem>());
        dropped.FindProperty("visual").objectReferenceValue = visual;
        dropped.FindProperty("sack").objectReferenceValue = sack.gameObject;
        dropped.ApplyModifiedPropertiesWithoutUndo();

        var saveable = new SerializedObject(root.AddComponent<SaveableEntity>());
        saveable.FindProperty("saveActiveState").boolValue = false;
        saveable.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        var entity = prefab.GetComponent<SaveableEntity>();
        entity.RefreshPrefabId();
        PrefabUtility.SavePrefabAsset(prefab);

        var settings = AssetDatabase.LoadAssetAtPath<DroppedItemSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<DroppedItemSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var wiring = new SerializedObject(settings);
        wiring.FindProperty("prefab").objectReferenceValue = entity;
        wiring.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);

        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        AssetDatabase.SaveAssets();
        return $"prefab id '{entity.PrefabId}', cord {cord.name}";
    }

    private static void Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
    }
}
