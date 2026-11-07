using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Progression content (`unity command run_script --file AgentScripts/BuildProgression.cs --entry BuildProgression.Apply`):
/// the Rune Book (read for Insight; in supply caches), the Experience Stone prefab placed off the paths of the prologue
/// and the Misty Woods raid, and PlayerProficiencies on the player prefab. The skills, their perks and LevelingSettings
/// are AgentScripts/BuildProficiencies.cs (they replaced the talents this once made). Safe to run again. Reopens the
/// scene that was open.
/// </summary>
public static class BuildProgression
{
    private const string StonePrefab = "Assets/_Project/Prefabs/Interactables/ExperienceStone.prefab";

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        ItemDefinition book = RuneBook();
        string caches = AddToCaches(book);
        GameObject stone = MakeStone();
        string player = AddPlayerProficiencies();
        AssetDatabase.SaveAssets();

        string prologue = InScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity", () => PlaceStones(stone, new[] { new Vector2(-14f, -57f) }));
        string raid = InScene("Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity", () =>
        {
            Player spawn = Object.FindAnyObjectByType<Player>(FindObjectsInactive.Include);
            Vector3 from = spawn != null ? spawn.transform.position : Vector3.zero;
            var spots = new List<Vector2>();
            for (int i = 0; i < 3; i++)
            {
                float angle = (40f + i * 120f) * Mathf.Deg2Rad;
                spots.Add(new Vector2(from.x + Mathf.Cos(angle) * 48f, from.z + Mathf.Sin(angle) * 48f));
            }

            return PlaceStones(stone, spots);
        });

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        return $"{caches}; {player}; prologue {prologue}; raid {raid}";
    }

    // ----- Rune Book -----

    private static ItemDefinition RuneBook()
    {
        const string path = "Assets/_Project/Data/Items/RuneBook.asset";
        var book = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (book == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Items/ForgeBlueprint.asset", path);
            book = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        }

        var so = new SerializedObject(book);
        so.FindProperty("id").stringValue = "rune_book";
        so.FindProperty("displayName").stringValue = "Rune Book";
        so.FindProperty("description").stringValue = "A rotting book of runes and old tales. Read it (right-click) to learn from it; the pages crumble as you do.";
        so.FindProperty("tier").intValue = 1;
        so.FindProperty("rarity").intValue = (int)ItemRarity.Uncommon;
        so.FindProperty("stackable").boolValue = true;
        so.FindProperty("maximumStack").intValue = 10;
        so.FindProperty("weight").floatValue = 0.3f;
        so.FindProperty("teachesBuildings").ClearArray();
        so.FindProperty("teachesRecipes").ClearArray();
        so.FindProperty("experience").intValue = 120;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(book);
        return book;
    }

    private static string AddToCaches(ItemDefinition book)
    {
        const string path = "Assets/_Project/Prefabs/Raids/SupplyCache.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LootCache source = root.GetComponentInChildren<LootCache>(true);
            var so = new SerializedObject(source);
            SerializedProperty entries = so.FindProperty("drops.entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == book) return "caches already hold rune books";
            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = book;
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = 1;
            entry.FindPropertyRelative("maxQuantity").intValue = 1;
            entry.FindPropertyRelative("probability").floatValue = 0.12f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "rune books in supply caches (12%)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ----- Experience Stone -----

    private static GameObject MakeStone()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/Interactables")) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Interactables");
        var root = new GameObject("Experience Stone");
        try
        {
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.75f, 0f);
            collider.height = 1.5f;
            collider.radius = 0.4f;

            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.name = "Stone";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            model.transform.localRotation = Quaternion.Euler(4f, 18f, -6f);
            model.transform.localScale = new Vector3(0.7f, 1.45f, 0.32f);
            model.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Data/Prologue/Waystone.mat");

            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.1f, 0.3f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.85f, 1f);
            light.intensity = 2.5f;
            light.range = 5f;
            light.shadows = LightShadows.None;
            glow.AddComponent<LightFlicker>();
            glow.AddComponent<VisibilitySource>().Configure(3.5f, 0.5f, false, 0f, false);

            var interactable = root.AddComponent<Interactable>();
            var data = new SerializedObject(interactable);
            data.FindProperty("animationType").intValue = (int)InteractionAnimation.GatherLow;
            data.FindProperty("duration").floatValue = 1.2f;
            data.FindProperty("deactivateOnComplete").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();

            var stone = root.AddComponent<ExperienceStone>();
            var stoneData = new SerializedObject(stone);
            stoneData.FindProperty("experience").intValue = 80;
            SerializedProperty parts = stoneData.FindProperty("litParts");
            parts.arraySize = 1;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = glow;
            stoneData.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, StonePrefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static string PlaceStones(GameObject prefab, IEnumerable<Vector2> spots)
    {
        GameObject old = GameObject.Find("Experience Stones");
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject("Experience Stones").transform;
        var placed = new List<string>();
        foreach (Vector2 spot in spots)
        {
            Terrain terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : Object.FindAnyObjectByType<Terrain>();
            var at = new Vector3(spot.x, 0f, spot.y);
            at.y = terrain != null ? terrain.SampleHeight(at) + terrain.transform.position.y : 0f;
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 6f, NavMesh.AllAreas)) at = hit.position;
            var stone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.gameObject.scene);
            stone.transform.SetParent(group, true);
            stone.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            placed.Add(at.ToString("F0"));
        }

        return string.Join(" ", placed);
    }

    // ----- Player -----

    private static string AddPlayerProficiencies()
    {
        const string path = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<PlayerProficiencies>() != null) return "player has skills";
            root.AddComponent<PlayerProficiencies>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "PlayerProficiencies on the player";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string InScene(string path, System.Func<string> work)
    {
        Scene scene = EditorSceneManager.OpenScene(path);
        string result = work();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return result;
    }
}
