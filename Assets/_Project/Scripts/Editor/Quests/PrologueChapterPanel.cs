using UnityEditor;
using UnityEngine;

/// <summary>
/// A quest's place in a story sequence (PrologueDefinition), on its Content Editor page: what the scene does while it runs,
/// where a checkpoint goes back to and what the companions do. So the whole of a chapter is edited in one place, and a new
/// quest joins the sequence with one button.
/// </summary>
public static class PrologueChapterPanel
{
    public static void Draw(QuestDefinition quest)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PrologueDefinition"))
        {
            var definition = AssetDatabase.LoadAssetAtPath<PrologueDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && DrawFor(definition, quest))
            {
                return;
            }
        }
    }

    private static bool DrawFor(PrologueDefinition definition, QuestDefinition quest)
    {
        var data = new SerializedObject(definition);
        SerializedProperty chapters = data.FindProperty("chapters");
        int index = -1;
        bool sameChain = false;
        for (int i = 0; i < chapters.arraySize; i++)
        {
            var other = chapters.GetArrayElementAtIndex(i).FindPropertyRelative("quest").objectReferenceValue as QuestDefinition;
            if (other == quest) index = i;
            sameChain |= other != null && !string.IsNullOrWhiteSpace(quest.Chain) && other.Chain == quest.Chain;
        }

        if (index < 0)
        {
            if (!sameChain) return false;
            ContentUtility.BeginPanel("Story Chapter", $"This quest is in the chain of {definition.name}, but not in its sequence: it runs beside it. Side quests stay out; a step of the main story goes in.");
            if (GUILayout.Button($"+  Add As The Last Chapter Of {definition.name}"))
            {
                int added = chapters.arraySize++;
                chapters.GetArrayElementAtIndex(added).FindPropertyRelative("quest").objectReferenceValue = quest;
                chapters.GetArrayElementAtIndex(added).FindPropertyRelative("behaviour").intValue = (int)PrologueStep.Story;
                data.ApplyModifiedProperties();
            }

            ContentUtility.EndPanel();
            return true;
        }

        ContentUtility.BeginPanel($"Story Chapter {index + 1} of {chapters.arraySize}", $"Its place in {definition.name}. Behaviour Story has no mechanic of its own: story graphs and scene reactions make its moments, and you say below where it is and what the companions do. The order of the chapters doesn't change the prerequisites: keep them in step in the Quest Map.");
        data.Update();
        SerializedProperty chapter = chapters.GetArrayElementAtIndex(index);
        SerializedProperty behaviour = chapter.FindPropertyRelative("behaviour");
        EditorGUILayout.PropertyField(behaviour);
        EditorGUILayout.PropertyField(chapter.FindPropertyRelative("arrivalText"));
        if ((PrologueStep)behaviour.intValue == PrologueStep.Story)
        {
            EditorGUILayout.PropertyField(chapter.FindPropertyRelative("site"));
            EditorGUILayout.PropertyField(chapter.FindPropertyRelative("companions"));
            EditorGUILayout.PropertyField(chapter.FindPropertyRelative("timeToTalk"));
        }
        else
        {
            EditorGUILayout.PropertyField(chapter.FindPropertyRelative("duration"));
            EditorGUILayout.PropertyField(chapter.FindPropertyRelative("initialEnemies"));
            EditorGUILayout.PropertyField(chapter.FindPropertyRelative("enemies"), true);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("Earlier")) chapters.MoveArrayElement(index, index - 1);
            }

            using (new EditorGUI.DisabledScope(index == chapters.arraySize - 1))
            {
                if (GUILayout.Button("Later")) chapters.MoveArrayElement(index, index + 1);
            }

            if (GUILayout.Button("Take Out Of The Sequence")) chapters.DeleteArrayElementAtIndex(index);
        }

        data.ApplyModifiedProperties();
        string problem = definition.FindProblem();
        if (problem != null)
        {
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        ContentUtility.EndPanel();
        return true;
    }
}
