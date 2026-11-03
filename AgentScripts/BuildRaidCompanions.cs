using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Who can go along on a raid, and the body they go in. The Raid Companion prefab is taken from an expedition member in the prologue
/// scene, without their model or story, so it fights and is talked to just as they are. Also the cairn and what those at home say.
/// Run: unity command script run -- --file AgentScripts/BuildRaidCompanions.cs --entry BuildRaidCompanions.Run
/// </summary>
public static class BuildRaidCompanions
{
    private const string Prefab = "Assets/_Project/Prefabs/Characters/People/RaidCompanion.prefab";
    private const string Cairn = "Assets/_Project/Prefabs/Characters/People/FallenCairn.prefab";

    public static string Run()
    {
        string scene = EditorSceneManager.GetActiveScene().path;
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new System.Exception("The open scene has changes; save or revert it first.");
        }

        MakePrefab();
        MakeCairn();
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        // Read back from the assets: what SaveAsPrefabAsset handed over doesn't outlive the scene it was made in.
        var companion = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<Companion>();
        var cairn = AssetDatabase.LoadAssetAtPath<GameObject>(Cairn).GetComponent<FallenCairn>();

        var fighter = AssetDatabase.LoadAssetAtPath<CompanionDefinition>("Assets/_Project/Data/Companions/Fighter.asset");
        var archer = AssetDatabase.LoadAssetAtPath<CompanionDefinition>("Assets/_Project/Data/Companions/Archer.asset");
        int people = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ResidentDefinition"))
        {
            var person = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            // Runa's the archer of the expedition, and Asa was a hunter before.
            Edit(person, so => so.FindProperty("raidCompanion").objectReferenceValue = person.name == "Runa" || person.name == "Asa" ? archer : fighter);
            people++;
        }

        // The Warden holds the line and the Hunter shoots over it; the rest shelter.
        Edit(AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Warden.asset"), so => so.FindProperty("defendsAs").objectReferenceValue = fighter);
        Edit(AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Hunter.asset"), so => so.FindProperty("defendsAs").objectReferenceValue = archer);

        Edit(SettlementSettings.Current, so =>
        {
            so.FindProperty("raidCompanionPrefab").objectReferenceValue = companion;
            so.FindProperty("fallenCairn").objectReferenceValue = cairn;
            so.FindProperty("griefRaids").intValue = 2;
            string[] lines =
            {
                "I keep looking for {0} at the fire.",
                "You came back. {0} didn't. I know how it goes out there. I still don't like it.",
                "We put up stones for {0}. It isn't much.",
                "Nobody here says it, but we all count who comes back through that portal. {0}... well.",
            };
            SerializedProperty grief = so.FindProperty("griefLines");
            grief.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                grief.GetArrayElementAtIndex(i).stringValue = lines[i];
            }
        });

        AssetDatabase.SaveAssets();
        return $"{people} people can go along; prefab has {companion.GetComponentsInChildren<Animator>(true).Length} animators (0 wanted)";
    }

    private static Companion MakePrefab()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity", OpenSceneMode.Single);
        Companion source = null;
        foreach (Companion one in Object.FindObjectsByType<Companion>(FindObjectsInactive.Include))
        {
            if (one.name == "Liv - Expedition")
            {
                source = one;
            }
        }

        if (source == null)
        {
            throw new System.Exception("No Liv - Expedition to take the body from");
        }

        // A copy that never touches the scene file: the scene is reopened without saving right after.
        GameObject copy = Object.Instantiate(source.gameObject);
        copy.name = "RaidCompanion";
        // Liv waits switched off in the prologue until she is rescued; the body mustn't.
        copy.SetActive(true);
        Object.DestroyImmediate(copy.GetComponent<PrologueCompanion>());
        for (int i = copy.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);
        }

        var so = new SerializedObject(copy.GetComponent<Companion>());
        so.FindProperty("definition").objectReferenceValue = null;
        so.FindProperty("resident").objectReferenceValue = null;
        so.FindProperty("startingOrder").intValue = (int)CompanionOrder.Follow;
        so.ApplyModifiedPropertiesWithoutUndo();
        var talk = new SerializedObject(copy.GetComponent<Talkable>());
        talk.FindProperty("speakerName").stringValue = "";
        talk.ApplyModifiedPropertiesWithoutUndo();
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, Prefab);
        Object.DestroyImmediate(copy);
        return saved.GetComponent<Companion>();
    }

    // Stones from the ground's own rock, stacked by hand, narrowing to the top.
    private static FallenCairn MakeCairn()
    {
        var node = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/GreyStoneNode.prefab");
        MeshFilter filter = node.GetComponentInChildren<MeshFilter>();
        Renderer look = node.GetComponentInChildren<Renderer>();
        var root = new GameObject("FallenCairn");
        (Vector3 at, float size, float yaw)[] stones =
        {
            (new Vector3(0f, 0.12f, 0f), 0.62f, 0f), (new Vector3(0.28f, 0.1f, 0.14f), 0.36f, 80f), (new Vector3(-0.26f, 0.1f, -0.16f), 0.4f, 160f),
            (new Vector3(0.02f, 0.44f, 0.02f), 0.42f, 210f), (new Vector3(-0.02f, 0.72f, 0f), 0.28f, 40f), (new Vector3(0.01f, 0.92f, 0.01f), 0.17f, 300f),
        };
        Bounds bounds = filter.sharedMesh.bounds;
        float unit = 1f / Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        foreach ((Vector3 at, float size, float yaw) in stones)
        {
            var stone = new GameObject("Stone");
            stone.transform.SetParent(root.transform, false);
            stone.transform.localPosition = at;
            stone.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            stone.transform.localScale = Vector3.one * (size * unit);
            stone.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            stone.AddComponent<MeshRenderer>().sharedMaterials = look.sharedMaterials;
        }

        Interactable interactable = root.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.None, 0f, 2.2f, false);
        var use = new SerializedObject(interactable);
        use.FindProperty("kind").intValue = (int)InteractionKind.Use;
        use.ApplyModifiedPropertiesWithoutUndo();
        FallenCairn cairn = root.AddComponent<FallenCairn>();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Cairn);
        Object.DestroyImmediate(root);
        return saved.GetComponent<FallenCairn>();
    }

    private static void Edit(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
