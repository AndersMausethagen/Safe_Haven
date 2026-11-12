using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// The fixes from the quest audit of 2026-11-11 (QuestAuditTests finds these kinds of fault)
/// (`unity command script run --file AgentScripts/FixQuestAudit.cs --entry FixQuestAudit.Apply`). Safe to run again.
/// <list type="bullet">
/// <item>Strength for the Road waited for the Haven's Drinking Spot, removed in 37c31c49: it now counts drinking at any water
/// (SurvivalSettings › Drank At Water, raised by every WaterSource).</item>
/// <item>The Road North counted any talk with the portal from before it began (Count Already Owned), so it finished the moment
/// it started and the portal's account of the Heartroot was never heard: it now waits for that conversation's end.</item>
/// <item>Words that no longer fit: "six of your people" (four are still out then), "the light that called Ingrid home" (she
/// may not be home yet), and what the Kitchen and the Watch Post need standing first.</item>
/// <item>Co-op: the Haven-bound Quest Events are Home Only (QuestObjective.CountsInAFriendsHaven).</item>
/// </list>
/// The quests' first builders (BuildHomeStory, BuildFirstAct) carry the same words now.
/// </summary>
public static class FixQuestAudit
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";

    public static string Apply()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "the open scene " + SceneManager.GetSceneAt(i).name + " has unsaved changes";
        }

        var log = new List<string>();

        // ----- Drinking at any water -----
        GameEventDefinition drank = Event("DrankAtWater", "survival.drank_at_water", "Drank at open water", "Drink, or fill a waterskin, at the river or a lake", false);
        Edit(Load<SurvivalSettings>("Assets/_Project/Resources/SurvivalSettings.asset"), so => so.FindProperty("drankAtWater").objectReferenceValue = drank);
        Edit(Q("FoodAndWater"), so =>
        {
            SerializedProperty step = Objective(so, "Drink or fill a waterskin", "Drink, or fill a waterskin");
            step.FindPropertyRelative("type").intValue = (int)ObjectiveType.QuestEvent;
            step.FindPropertyRelative("questEvent").objectReferenceValue = drank;
            step.FindPropertyRelative("target").objectReferenceValue = null;
            step.FindPropertyRelative("description").stringValue = "Drink, or fill a waterskin, at the river or a lake";
            step.FindPropertyRelative("amount").intValue = 1;
            step.FindPropertyRelative("countAlreadyOwned").boolValue = false;
        });
        log.Add("Strength for the Road: any water");

        // ----- The Road North waits for its conversation -----
        GameEventDefinition told = Event("RoadNorthHeard", "home.road_north_heard", "The portal told of the Heartroot", "Speak to the portal", true);
        Edit(Q("TheRoadNorth"), so =>
        {
            SerializedProperty step = so.FindProperty("objectives").GetArrayElementAtIndex(0);
            step.FindPropertyRelative("type").intValue = (int)ObjectiveType.QuestEvent;
            step.FindPropertyRelative("questEvent").objectReferenceValue = told;
            step.FindPropertyRelative("target").objectReferenceValue = null;
            step.FindPropertyRelative("person").objectReferenceValue = null;
            step.FindPropertyRelative("description").stringValue = "Speak to the portal";
            step.FindPropertyRelative("countAlreadyOwned").boolValue = false;
        });

        // ----- Words -----
        Replace(Q("ALightTheyCanSee"), "description", "and six of your people are still out there", "and the rest of your people are still out there");
        Replace(Q("TheWeakeningWard"), "description", "The light that called Ingrid home was seen by other things too.", BuildHomeStory_WeakeningWardOpening);
        Edit(Q("TheCook"), so => Objective(so, "Build a Kitchen", null).FindPropertyRelative("description").stringValue = "Build a Kitchen (build menu: {key:Build}): it wants Bjarne's Smithy standing");
        Edit(Q("TheWarden"), so => Objective(so, "Build a Watch Post", null).FindPropertyRelative("description").stringValue = "Build a Watch Post (build menu: {key:Build}): it wants the Woodcutter's Lodge standing");

        // ----- Co-op: what belongs to a Haven -----
        foreach (string file in new[] { "LandStoneLit", "LandTaskDone", "LandRuneOffered", "LandForestGathers", "LandPushAnswered", "LandSecure", "PortalRepaired", "PortalBloomed" })
        {
            var gameEvent = Load<GameEventDefinition>(Quests + "Events/" + file + ".asset");
            if (gameEvent != null) Edit(gameEvent, so => so.FindProperty("homeOnly").boolValue = true);
            else log.Add("no event " + file);
        }

        AssetDatabase.SaveAssets();
        // Loaded again from disk before a scene keeps it (a reference made in the same run can save empty: BuildFirstAct).
        told = Load<GameEventDefinition>(Quests + "Events/RoadNorthHeard.asset");
        log.Add(RoadNorthStory(told));
        return string.Join("; ", log);
    }

    // The CountAsDone (Talk To the portal) at the end of the Road North becomes the Story Event the quest now waits for. Patched
    // in place: the story keeps its save id and its trigger's id, so a saved game remembers whether it has played.
    private static string RoadNorthStory(GameEventDefinition told)
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        StoryScript story = Object.FindObjectsByType<StoryScript>(FindObjectsInactive.Include).FirstOrDefault(s => s.name == "Story - The Road North");
        if (story == null) return "no Road North story in SafeHaven";
        if (story.Nodes.OfType<RaiseQuestEventNode>().Any(n => n.questEvent == told)) return "the Road North story already raises " + told.name;

        CountAsDoneNode done = story.Nodes.OfType<CountAsDoneNode>().FirstOrDefault();
        if (done == null) return "the Road North story has no Count As Done";
        List<StoryLink> into = story.Links.Where(l => l.to == done.id).ToList();
        var raise = new RaiseQuestEventNode { questEvent = told, position = done.position };
        story.AddNode(raise);
        foreach (StoryLink link in into)
        {
            story.Connect(story.Find(link.from), link.output, raise);
        }

        story.RemoveNode(done);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string problems = string.Join(", ", story.FindProblems());
        return $"Road North story: raises {told.name} ({into.Count} link(s) in){(problems.Length > 0 ? "; problems: " + problems : "")}, dirty={scene.isDirty}";
    }

    public const string BuildHomeStory_WeakeningWardOpening = "Ingrid wasn't the only one watching your fire.";

    // ----- Helpers -----

    // The objective whose description starts with the text (or its fallback).
    private static SerializedProperty Objective(SerializedObject so, string startsWith, string orStartsWith)
    {
        SerializedProperty objectives = so.FindProperty("objectives");
        for (int i = 0; i < objectives.arraySize; i++)
        {
            string text = objectives.GetArrayElementAtIndex(i).FindPropertyRelative("description").stringValue;
            if (text.StartsWith(startsWith, StringComparison.Ordinal) || orStartsWith != null && text.StartsWith(orStartsWith, StringComparison.Ordinal))
            {
                return objectives.GetArrayElementAtIndex(i);
            }
        }

        throw new InvalidOperationException($"{so.targetObject.name} has no objective \"{startsWith}\"");
    }

    private static void Replace(Object asset, string field, string from, string to)
    {
        Edit(asset, so =>
        {
            SerializedProperty text = so.FindProperty(field);
            if (!text.stringValue.Contains(to)) text.stringValue = text.stringValue.Replace(from, to);
        });
    }

    private static QuestDefinition Q(string file) => Load<QuestDefinition>(Quests + file + ".asset") ?? throw new InvalidOperationException("Missing quest " + file);

    private static GameEventDefinition Event(string file, string id, string name, string objective, bool homeOnly)
    {
        string path = Quests + "Events/" + file + ".asset";
        var gameEvent = Load<GameEventDefinition>(path);
        if (gameEvent == null)
        {
            gameEvent = ScriptableObject.CreateInstance<GameEventDefinition>();
            AssetDatabase.CreateAsset(gameEvent, path);
        }

        Edit(gameEvent, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("objectiveText").stringValue = objective;
            so.FindProperty("homeOnly").boolValue = homeOnly;
        });
        return gameEvent;
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
