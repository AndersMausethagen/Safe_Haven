#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time migration to Talkable (`unity command run_script --file AgentScripts/MigrateTalkables.cs --entry
/// MigrateTalkables.Apply`): everyone who talks (expedition members, companions, residents) gets the one shared
/// conversation component, with the speaker's name, the prologue's camera distance and their TalkedTo event; a quest giver
/// is moved above the camp chatter so its offer speaks first. Safe to run again. Reopens the scene that was open.
/// </summary>
public static class MigrateTalkables
{
    private const string EventFolder = "Assets/_Project/Data/Prologue/Events/";

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        var report = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !Talks(asset)) continue;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = Migrate(contents, null);
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    report.Add($"{Path.GetFileNameWithoutExtension(path)} ({changed})");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        string[] owners = { Guid<PrologueCompanion>(), Guid<ResidentNpc>(), Guid<Companion>() };
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string text = File.ReadAllText(path);
            bool mentions = false;
            foreach (string owner in owners) mentions |= text.Contains(owner);
            if (!mentions) continue;

            Scene scene = EditorSceneManager.OpenScene(path);
            PrologueDirector director = Object.FindAnyObjectByType<PrologueDirector>(FindObjectsInactive.Include);
            float? distance = director != null && director.Definition != null ? director.Definition.DialogueCameraDistance : (float?)null;
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) changed += Migrate(root, distance);
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add($"{scene.name} ({changed})");
            }
        }

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        return string.Join(", ", report);
    }

    private static string Guid<T>() where T : MonoBehaviour
    {
        string[] found = AssetDatabase.FindAssets($"{typeof(T).Name} t:MonoScript");
        foreach (string guid in found)
            if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == typeof(T).Name) return guid;
        return "-";
    }

    private static bool Talks(GameObject root) =>
        root.GetComponentInChildren<PrologueCompanion>(true) != null || root.GetComponentInChildren<ResidentNpc>(true) != null ||
        root.GetComponentInChildren<Companion>(true) != null;

    private static int Migrate(GameObject root, float? cameraDistance)
    {
        int changed = 0;
        var talkers = new HashSet<GameObject>();
        foreach (PrologueCompanion member in root.GetComponentsInChildren<PrologueCompanion>(true)) talkers.Add(member.gameObject);
        foreach (ResidentNpc resident in root.GetComponentsInChildren<ResidentNpc>(true)) talkers.Add(resident.gameObject);
        foreach (Companion companion in root.GetComponentsInChildren<Companion>(true)) talkers.Add(companion.gameObject);

        foreach (GameObject talker in talkers)
        {
            bool touched = false;
            if (!talker.TryGetComponent(out Talkable talkable))
            {
                talkable = talker.AddComponent<Talkable>();
                touched = true;
            }

            var data = new SerializedObject(talkable);
            PrologueCompanion member = talker.GetComponent<PrologueCompanion>();
            if (member != null && member.Resident != null)
            {
                touched |= Set(data.FindProperty("speakerName"), member.Resident.DisplayName);
                var talked = AssetDatabase.LoadAssetAtPath<GameEventDefinition>($"{EventFolder}TalkedTo{member.Resident.DisplayName}.asset");
                if (talked != null && data.FindProperty("talkedEvent").objectReferenceValue != talked)
                {
                    data.FindProperty("talkedEvent").objectReferenceValue = talked;
                    touched = true;
                }
            }

            if (cameraDistance.HasValue && !Mathf.Approximately(data.FindProperty("cameraDistance").floatValue, cameraDistance.Value))
            {
                data.FindProperty("cameraDistance").floatValue = cameraDistance.Value;
                touched = true;
            }

            data.ApplyModifiedPropertiesWithoutUndo();

            // The quest offer speaks before the camp chatter.
            QuestGiver giver = talker.GetComponent<QuestGiver>();
            if (giver != null && member != null)
            {
                while (IndexOf(talker, giver) > IndexOf(talker, member) && ComponentUtility.MoveComponentUp(giver)) touched = true;
            }

            if (touched) changed++;
        }

        return changed;
    }

    private static bool Set(SerializedProperty property, string value)
    {
        if (property.stringValue == value) return false;
        property.stringValue = value;
        return true;
    }

    private static int IndexOf(GameObject owner, Component component) => System.Array.IndexOf(owner.GetComponents<Component>(), component);
}
