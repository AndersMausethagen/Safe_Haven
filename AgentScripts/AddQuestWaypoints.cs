using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts quest waypoints where the quests happen (`unity command run_script --file AgentScripts/AddQuestWaypoints.cs --entry AddQuestWaypoints.Apply`):
/// the prologue's shelter, campfire and waystones; the Haven's portal for "They Are Still Out There"; the waking stone
/// in the story demo. Replaces waypoints it made before. Reopens the scene that was open.
/// </summary>
public static class AddQuestWaypoints
{
    private const string Quests = "Assets/_Project/Data/Prologue/Quests/";

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        string log = "";
        log += InScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity", () =>
        {
            PrologueDirector director = Object.FindAnyObjectByType<PrologueDirector>(FindObjectsInactive.Include);
            var data = new SerializedObject(director);
            var camp = (Transform)data.FindProperty("campPoint").objectReferenceValue;
            var portal = (Transform)data.FindProperty("portalPoint").objectReferenceValue;
            Add(camp, "FindShelter", "Shelter");
            Add(camp, "LightFire", "Campfire");
            Add(portal, "ReachPortal", "Waystones");
            Add(portal, "ActivatePortal", "Waystones");
            return "prologue: shelter, campfire, waystones";
        });
        log += "; " + InScene("Assets/_Project/Scenes/SafeHaven.unity", () =>
        {
            RaidPortal portal = Object.FindAnyObjectByType<RaidPortal>(FindObjectsInactive.Include);
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/TheyAreStillOutThere.asset")
                        ?? AssetDatabase.LoadAssetAtPath<QuestDefinition>(Quests + "TheyAreStillOutThere.asset");
            if (portal == null || quest == null)
            {
                return "haven: no portal or quest";
            }

            Add(portal.transform, quest, "Portal");
            return "haven: portal";
        });
        log += "; " + InScene("Assets/_Project/Scenes/Demo/StoryDemo.unity", () =>
        {
            GameObject stone = GameObject.Find("The Waking Stone");
            Add(stone.transform, AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/StoryDemo/TheWakingStone.asset"), "The stone");
            return "demo: stone";
        });

        if (!string.IsNullOrEmpty(previous))
        {
            EditorSceneManager.OpenScene(previous);
        }

        return log;
    }

    private static string InScene(string path, System.Func<string> work)
    {
        Scene scene = EditorSceneManager.OpenScene(path);
        string result = work();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return result;
    }

    private static void Add(Transform at, string questFile, string label) =>
        Add(at, AssetDatabase.LoadAssetAtPath<QuestDefinition>(Quests + questFile + ".asset"), label);

    private static void Add(Transform at, QuestDefinition quest, string label)
    {
        string name = $"Quest Waypoint - {quest.DisplayName}";
        Transform old = at.Find(name);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        var go = new GameObject(name);
        go.transform.SetParent(at, false);
        go.AddComponent<QuestWaypoint>().Configure(quest, 0, label);
    }
}
