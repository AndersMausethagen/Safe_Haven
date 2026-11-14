#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Makes the open scene's DayNightCycle saved: a SaveableEntity on its object that keeps only component state
/// (`unity command run_script --file AgentScripts/SaveTheClock.cs --entry SaveTheClock.Apply`).</summary>
public static class SaveTheClock
{
    public static string Apply()
    {
        DayNightCycle cycle = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        if (cycle == null)
        {
            return "no DayNightCycle in " + EditorSceneManager.GetActiveScene().name;
        }

        if (!cycle.TryGetComponent(out SaveableEntity entity))
        {
            entity = Undo.AddComponent<SaveableEntity>(cycle.gameObject);
        }

        var data = new SerializedObject(entity);
        data.FindProperty("saveTransform").boolValue = false;
        data.FindProperty("saveActiveState").boolValue = false;
        data.FindProperty("rememberDestruction").boolValue = false;
        data.FindProperty("saveComponents").boolValue = true;
        data.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);
        EditorSceneManager.SaveScene(cycle.gameObject.scene);
        return $"{cycle.name} in {cycle.gameObject.scene.name}: saveable id '{data.FindProperty("id").stringValue}'";
    }
}
