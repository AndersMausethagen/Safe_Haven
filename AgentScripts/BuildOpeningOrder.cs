using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// The order of the first days at home (`unity command script run --file AgentScripts/BuildOpeningOrder.cs --entry BuildOpeningOrder.Apply`),
/// run after BuildHomeStory and BuildFoundation. Playtest 2026-11-08: "They Are Still Out There" said to reactivate the portal but not
/// how, while quests for new buildings piled up. Now the opening teaches before it builds: tools, a workshop, a chest (what stays
/// safe at home), a weapon, food, then the Life Orbs and the portal, and the first raid. The Woodcutter's Lodge and the walls come
/// after it, with what the raid brought home. Nobody arrives with a Haven rank any more: everyone comes home from a raid.
/// Replaced on 2026-11-10 by BuildQuestCategories (Docs/QuestRedesign.md): the door opens after the fire and the stones, and
/// these lessons are Haven quests beside the story. Don't run this one again: Gifts of the Clearing no longer exists.
/// </summary>
public static class BuildOpeningOrder
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";

    public static string Apply()
    {
        foreach (Scene open in OpenScenes())
        {
            if (open.isDirty) return "the open scene " + open.name + " has unsaved changes";
        }

        QuestDefinition safe = SafePlace();
        QuestDefinition weapon = Made("FirstWeapon", "firstweapon", "Something to Fight With",
            "A hatchet fells trees; it won't keep a wolf off you. Make a Knotted Club at the workshop, from oak and nettle fibre: it stuns what it hits. Wear it in your weapon slot (C) before you go near the portal.",
            safe,
            (QuestObjective.Create(ObjectiveType.CraftItem, 1, Item("KnottedClub")), "Make a Knotted Club at the Workshop"));

        After("FoodAndWater", weapon);
        DropCraft("FoodAndWater", "KnottedClub");
        // The Woodcutter's Lodge, the walls and A Growing Home follow Tor's rescue now: BuildRescueOrder, run after this.
        string portal = ThePortalQuest();
        string ranks = NoSettlers();

        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        string home = Home();
        return $"{safe.DisplayName}: {string.Join("; ", safe.FindProblems())} | {weapon.DisplayName}: {string.Join("; ", weapon.FindProblems())} | {portal} | {ranks} | {home}";
    }

    /// <summary>Only A Safe Place: build the chest, then put something in it, so the stash is used once and not only built.</summary>
    public static string SafePlaceOnly()
    {
        QuestDefinition safe = SafePlace();
        AssetDatabase.SaveAssets();
        return $"{safe.DisplayName}: {safe.Objectives.Count} objectives, {string.Join("; ", safe.FindProblems())}";
    }

    // ----- Quests -----

    private static QuestDefinition SafePlace() => Made("SafeKeeping", "safekeeping", "A Safe Place",
        "What you carry through the portal is lost if you fall out there. What you leave in a chest at home stays safe. Build a Small Chest beside the workshop (build menu: G), and before every raid leave in it whatever you can't afford to lose.",
        Quest("FirstCamp"),
        (QuestObjective.Create(ObjectiveType.BuildStructure, 1, Building("SmallChest")), "Build a Small Chest (build menu: G)"),
        (QuestObjective.Create(ObjectiveType.StoreItem, 1), "Put something you can't afford to lose in the chest"));

    // A First Days quest, made once and kept up to date, its objectives in order. Copied from FirstCamp, so it is set up like its neighbours.
    private static QuestDefinition Made(string file, string id, string title, string description, QuestDefinition after,
        params (QuestObjective objective, string text)[] steps)
    {
        string path = Quests + file + ".asset";
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (quest == null)
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(Quest("FirstCamp")), path)) throw new InvalidOperationException("Could not make " + path);
            quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        }

        var so = new SerializedObject(quest);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = title;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("journalNotes").stringValue = "";
        so.FindProperty("chain").stringValue = "The First Days";
        so.FindProperty("start").intValue = (int)QuestStart.Automatically;
        so.FindProperty("startEvent").objectReferenceValue = null;
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = after;
        SerializedProperty objectives = so.FindProperty("objectives");
        objectives.arraySize = steps.Length;
        for (int i = 0; i < steps.Length; i++)
        {
            SerializedProperty step = objectives.GetArrayElementAtIndex(i);
            step.boxedValue = steps[i].objective;
            step.FindPropertyRelative("description").stringValue = steps[i].text;
            step.FindPropertyRelative("countAlreadyOwned").boolValue = true;
        }

        so.FindProperty("objectivesInOrder").boolValue = steps.Length > 1;
        so.FindProperty("rewardItems").arraySize = 0;
        so.FindProperty("rewardResources").arraySize = 0;
        so.FindProperty("rewardRecipes").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
        return quest;
    }

    private static void After(string file, QuestDefinition before) => Edit(Quest(file), so =>
    {
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = before;
    });

    private static void Text(string file, string description) => Edit(Quest(file), so => so.FindProperty("description").stringValue = description);

    // The weapon has a quest of its own now, so the meal quest no longer asks for it.
    private static void DropCraft(string file, string item) => Edit(Quest(file), so =>
    {
        SerializedProperty objectives = so.FindProperty("objectives");
        for (int i = objectives.arraySize - 1; i >= 0; i--)
        {
            Object made = objectives.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue;
            if (made != null && made.name == item) objectives.DeleteArrayElementAtIndex(i);
        }
    });

    // The Lost Expedition's quest at home says how the portal wakes, and that the First Days lead there.
    private static string ThePortalQuest()
    {
        QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Prologue/Quests/TheyAreStillOutThere.asset");
        Edit(quest, so =>
        {
            so.FindProperty("description").stringValue = "You heard Sigrid through the dormant stones, so someone out there is alive. The portal wakes again with three Life Orbs, held by the rootbound stones around the clearing. Before you can free them and go through you need tools, a workshop, a chest for what you can't afford to lose and a weapon: the quests of The First Days lead there one step at a time.";
            so.FindProperty("objectivesInOrder").boolValue = true;
            SerializedProperty objectives = so.FindProperty("objectives");
            for (int i = 0; i < objectives.arraySize; i++)
            {
                SerializedProperty objective = objectives.GetArrayElementAtIndex(i);
                if (objective.FindPropertyRelative("type").intValue == (int)ObjectiveType.QuestEvent)
                    objective.FindPropertyRelative("description").stringValue = "Wake the portal with three Life Orbs (follow The First Days)";
            }
        });
        return quest.DisplayName + ": " + string.Join("; ", quest.FindProblems());
    }

    private static string NoSettlers()
    {
        int cleared = 0;
        Edit(HavenSettings.Current, so =>
        {
            SerializedProperty ranks = so.FindProperty("ranks");
            for (int i = 0; i < ranks.arraySize; i++)
            {
                SerializedProperty settler = ranks.GetArrayElementAtIndex(i).FindPropertyRelative("settler");
                if (settler.objectReferenceValue != null) cleared++;
                settler.objectReferenceValue = null;
            }
        });
        return cleared + " rank settlers removed";
    }

    // ----- Home: what the portal says -----

    private static string Home()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        HomePortal tree = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        RaidPortal portal = Object.FindAnyObjectByType<RaidPortal>(FindObjectsInactive.Include);
        if (tree == null || portal == null) return "no portal in " + HomeScene;

        // A later whisper wins when two quests are active, so each goes where its quest sits in the story.
        Whisper(tree, "SafeKeeping", "FirstCamp", "<i>What you carry through me, the woods can take from you. What you leave here, I keep. Build a chest beside your workshop (G), and put in it what you cannot bear to lose.</i>");
        Whisper(tree, "FirstWeapon", "SafeKeeping", "<i>A hatchet fells trees. It will not keep the wolves off you. Make something to fight with at your workshop, and carry it.</i>");
        Whisper(tree, "WoodcuttersLodge", "ThroughThePortal", "<i>Your carpenter is home. Let him make something of the woods: a lodge by the trees, a saw that turns logs into planks. The walls and the shed start with a plank.</i>");
        Whisper(tree, "RaiseTheWalls", "WoodcuttersLodge", "<i>My ward keeps out the worst of the forest, not all of it. Put a wall between your fire and the trees.</i>");

        var portalData = new SerializedObject(portal);
        portalData.FindProperty("ruinText").stringValue = "The stones that dropped you here, cracked and cold. A faint hum lingers in them, and under it a voice you know. Three Life Orbs would wake them: the rootbound stones around the clearing still hold some.";
        portalData.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "portal whispers set, dirty=" + scene.isDirty;
    }

    // Puts a quest's whisper just after its predecessor's, or updates it where it is.
    private static void Whisper(HomePortal tree, string quest, string after, string text)
    {
        var so = new SerializedObject(tree);
        SerializedProperty whispers = so.FindProperty("questWhispers");
        QuestDefinition mine = Quest(quest), before = Quest(after);
        int at = -1, previous = -1;
        for (int i = 0; i < whispers.arraySize; i++)
        {
            Object owner = whispers.GetArrayElementAtIndex(i).FindPropertyRelative("quest").objectReferenceValue;
            if (owner == mine) at = i;
            if (owner == before) previous = i;
        }

        if (at < 0)
        {
            at = previous >= 0 ? previous + 1 : whispers.arraySize;
            whispers.InsertArrayElementAtIndex(Mathf.Min(at, whispers.arraySize));
            at = Mathf.Min(at, whispers.arraySize - 1);
        }

        SerializedProperty whisper = whispers.GetArrayElementAtIndex(at);
        whisper.FindPropertyRelative("quest").objectReferenceValue = mine;
        whisper.FindPropertyRelative("text").stringValue = text;
        whisper.FindPropertyRelative("moment").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ----- Helpers -----

    private static IEnumerable<Scene> OpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) yield return SceneManager.GetSceneAt(i);
    }

    private static QuestDefinition Quest(string file) => Load<QuestDefinition>(Quests + file + ".asset");

    private static ItemDefinition Item(string name) => Find<ItemDefinition>(name);

    private static BuildingDefinition Building(string name) => Find<BuildingDefinition>(name);

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing " + path);
        return asset;
    }

    private static T Find<T>(string name) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == name) return asset;
        }

        throw new InvalidOperationException($"No {typeof(T).Name} named {name}");
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
