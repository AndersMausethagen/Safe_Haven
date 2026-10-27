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

    public static void Draw(Object source)
    {
        ContentUtility.BeginPanel("Scene Reactions", "Add music, silence, weather, object activation or parameter actions here. " +
            "Reactions belong to the currently open scene, not the shared quest asset. Save that scene to keep them. " +
            "Parameters uses Unity's standard property/method picker, including float/int/bool/string arguments.");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (source is QuestDefinition)
            {
                if (GUILayout.Button("+ On Quest Started")) Create(source, ListenFor.QuestStarted);
                if (GUILayout.Button("+ On Quest Completed")) Create(source, ListenFor.QuestCompleted);
            }
            else if (GUILayout.Button("+ When Event Is Raised")) Create(source, ListenFor.QuestEvent);
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
