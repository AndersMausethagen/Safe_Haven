using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Authors scene-owned event listeners without leaving the quest/content designer.</summary>
public static class QuestReactionEditor
{
    public static GameEventListener Create(Object source, ListenFor when)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Open the intended scene outside Play Mode before adding a reaction.");
        var go = new GameObject(source.name + " - " + when + " Reaction");
        Undo.RegisterCreatedObjectUndo(go, "Create quest scene reaction");
        var listener = Undo.AddComponent<GameEventListener>(go);
        var data = new SerializedObject(listener);
        data.FindProperty("listenFor").enumValueIndex = (int)when;
        data.FindProperty(source is QuestDefinition ? "quest" : "gameEvent").objectReferenceValue = source;
        data.ApplyModifiedProperties();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(scene);
        QuestSceneIndex.Invalidate();
        return listener;
    }

    /// <summary>A story in the open scene that starts at this moment of the quest, opened in the Story Graph.</summary>
    public static StoryScript CreateStory(QuestDefinition quest, WhenQuest.Moment moment)
    {
        var trigger = new WhenQuest { quest = quest, when = moment };
        return Finish(StoryGraphWindow.CreateFor($"{quest.DisplayName} - {trigger.Title}", trigger));
    }

    /// <summary>A story in the open scene that starts when the Quest Event is raised, opened in the Story Graph.</summary>
    public static StoryScript CreateStory(GameEventDefinition gameEvent)
    {
        return Finish(StoryGraphWindow.CreateFor($"{gameEvent.DisplayName} - Story", new WhenQuestEvent { questEvent = gameEvent }));
    }

    private static StoryScript Finish(StoryScript story)
    {
        EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        QuestSceneIndex.Invalidate();
        return story;
    }

    public static void Draw(Object source)
    {
        ContentUtility.BeginPanel("Scene Reactions", "A reaction changes something when the quest moves on: music, silence, weather, an object, " +
            "or any parameter. A story is a moment told step by step: lines, choices, the camera, a fight. Both belong to the open " +
            "scene, not the shared quest asset. Save that scene to keep them. " +
            "Parameters uses Unity's standard property/method picker, including float/int/bool/string arguments.");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (source is QuestDefinition)
                {
                    if (GUILayout.Button("+ On Quest Started")) Create(source, ListenFor.QuestStarted);
                    if (GUILayout.Button("+ On Quest Completed")) Create(source, ListenFor.QuestCompleted);
                }
                else if (GUILayout.Button("+ When Event Is Raised")) Create(source, ListenFor.QuestEvent);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (source is QuestDefinition quest)
                {
                    if (GUILayout.Button("+ Story When It Starts"))
                    {
                        CreateStory(quest, WhenQuest.Moment.Starts);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("+ Story When It Completes"))
                    {
                        CreateStory(quest, WhenQuest.Moment.IsCompleted);
                        GUIUtility.ExitGUI();
                    }
                }
                else if (source is GameEventDefinition gameEvent && GUILayout.Button("+ Story When Raised"))
                {
                    CreateStory(gameEvent);
                    GUIUtility.ExitGUI();
                }
            }
        }
        var listeners = Object.FindObjectsByType<GameEventListener>(FindObjectsInactive.Include);
        foreach (var listener in listeners.Where(l => !EditorUtility.IsPersistent(l)))
        {
            var data = new SerializedObject(listener);
            bool matches = source is QuestDefinition
                ? data.FindProperty("quest").objectReferenceValue == source &&
                    data.FindProperty("listenFor").enumValueIndex != (int)ListenFor.QuestEvent
                : data.FindProperty("gameEvent").objectReferenceValue == source &&
                    data.FindProperty("listenFor").enumValueIndex == (int)ListenFor.QuestEvent;
            if (!matches) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(listener.name + " (" + listener.gameObject.scene.name + ")", listener, typeof(GameEventListener), true);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(data.FindProperty("respondIfAlreadyHappened"));
                EditorGUILayout.PropertyField(data.FindProperty("actions"), true);
                EditorGUILayout.PropertyField(data.FindProperty("response"), true);
                EditorGUILayout.PropertyField(data.FindProperty("alreadyHappenedResponse"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    data.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
                    EditorSceneManager.MarkSceneDirty(listener.gameObject.scene);
                }
            }
        }
        ContentUtility.EndPanel();
    }
}
