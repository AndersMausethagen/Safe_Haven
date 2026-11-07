using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes Quest Targets and marks scene objects with them: a new target from a name, a Quest Zone or Inspect Point for a
/// target at the Scene view, or the selected object made into a target (its trigger, or a Talkable's Quest Target). Used
/// by the objective drawer's New button and the quest checklist's fixes, so a designer never has to find the right field.
/// </summary>
public static class QuestTargetTools
{
    public const string Folder = "Assets/_Project/Data/Quests/Targets";

    /// <summary>Asks for a name and saves a new Quest Target in the targets folder. Null when cancelled.</summary>
    public static QuestTargetDefinition CreateWithDialog(string suggested = "New Place")
    {
        PrefabBuildUtility.EnsureFolder(Folder);
        string path = EditorUtility.SaveFilePanelInProject("New Quest Target", suggested, "asset",
            "Name the place or thing, as quests will call it (\"Old Tower\", \"Lake Stone\").", Folder);
        return string.IsNullOrEmpty(path) ? null : Create(path);
    }

    /// <summary>Saves a new Quest Target at <paramref name="path"/>, named after the file.</summary>
    public static QuestTargetDefinition Create(string path)
    {
        var target = ScriptableObject.CreateInstance<QuestTargetDefinition>();
        string name = Path.GetFileNameWithoutExtension(path);
        target.Configure(PrefabBuildUtility.MakeId(name), name);
        AssetDatabase.CreateAsset(target, AssetDatabase.GenerateUniqueAssetPath(path));
        AssetDatabase.SaveAssets();
        QuestSceneIndex.Invalidate();
        return target;
    }

    /// <summary>Every object in the open scenes that is this person, place or thing for quests.</summary>
    public static List<GameObject> MarkedInOpenScenes(Object identity)
    {
        var found = new List<GameObject>();
        if (identity == null)
        {
            return found;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<GameEventTrigger>(true).Where(t => t.Target == identity).Select(t => t.gameObject));
                found.AddRange(root.GetComponentsInChildren<Talkable>(true).Where(t => t.QuestIdentity == identity).Select(t => t.gameObject));
                found.AddRange(root.GetComponentsInChildren<HomePortal>(true).Where(p => p.QuestTarget == identity).Select(p => p.gameObject));
            }
        }

        return found.Distinct().ToList();
    }

    /// <summary>A Quest Zone for the place at the Scene view (the player walking in counts as Reach Place).</summary>
    public static GameObject CreateZone(QuestTargetDefinition place)
    {
        GameObject zone = QuestObjectPresets.CreateZone();
        Mark(zone.GetComponent<GameEventTrigger>(), place);
        zone.name = $"Quest Zone - {place.DisplayName}";
        return zone;
    }

    /// <summary>An Inspect Point for the thing at the Scene view (using it counts as Use Object).</summary>
    public static GameObject CreateInspectPoint(QuestTargetDefinition thing)
    {
        GameObject point = QuestObjectPresets.CreateInspectPoint();
        Mark(point.GetComponent<GameEventTrigger>(), thing);
        point.name = $"Inspect Point - {thing.DisplayName}";
        return point;
    }

    /// <summary>
    /// Marks the selected object as <paramref name="identity"/>: its Quest Event Trigger's Target (reaching or using it),
    /// or its Talkable's Quest Target (talking to it). Adds a Quest Event Trigger set to Player Interacts when it has
    /// neither. Returns what was changed, for the designer.
    /// </summary>
    public static string MarkSelected(QuestTargetDefinition identity, bool talk)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || EditorUtility.IsPersistent(selected))
        {
            return "Select the object in a scene first.";
        }

        if (talk && selected.TryGetComponent(out Talkable talkable))
        {
            SetField(talkable, "questTarget", identity);
            return $"{selected.name} now counts as {identity.DisplayName} when talked to.";
        }

        if (!selected.TryGetComponent(out GameEventTrigger trigger))
        {
            trigger = Undo.AddComponent<GameEventTrigger>(selected);
            SetEnum(trigger, "when", (int)TriggerWhen.PlayerInteracts);
        }

        Mark(trigger, identity);
        return $"{selected.name} now counts as {identity.DisplayName} ({(trigger.DeedKind == GameEventKind.Reached ? "reaching it" : "using it")}).";
    }

    private static void Mark(GameEventTrigger trigger, QuestTargetDefinition identity)
    {
        if (trigger != null)
        {
            SetField(trigger, "target", identity);
        }
    }

    private static void SetField(Object component, string field, Object value)
    {
        var data = new SerializedObject(component);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedProperties();
        MarkDirty(component);
    }

    private static void SetEnum(Object component, string field, int value)
    {
        var data = new SerializedObject(component);
        data.FindProperty(field).intValue = value;
        data.ApplyModifiedProperties();
    }

    private static void MarkDirty(Object component)
    {
        if (component is Component c && c.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }

        QuestSceneIndex.Invalidate();
    }
}
