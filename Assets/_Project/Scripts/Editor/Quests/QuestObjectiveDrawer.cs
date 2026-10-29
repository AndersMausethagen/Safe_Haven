using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a quest objective with only the fields its type uses, plus a line showing how it will read in the game, so
/// designers aren't faced with settings that do nothing.
/// </summary>
[CustomPropertyDrawer(typeof(QuestObjective))]
public sealed class QuestObjectiveDrawer : PropertyDrawer
{
    private const float Gap = 2f;

    private static readonly Dictionary<ObjectiveType, string[]> FieldsByType = new Dictionary<ObjectiveType, string[]>
    {
        { ObjectiveType.CollectItem, new[] { "item", "countAlreadyOwned" } },
        { ObjectiveType.GatherResource, new[] { "resource", "countAlreadyOwned" } },
        { ObjectiveType.KillMonster, new[] { "monster", "faction" } },
        { ObjectiveType.DestroyBuilding, new[] { "building" } },
        { ObjectiveType.CraftItem, new[] { "item", "resource" } },
        { ObjectiveType.CompleteRaids, new[] { "raid", "mustExtract", "withinMinutes" } },
        { ObjectiveType.QuestEvent, new[] { "questEvent", "countAlreadyOwned" } },
        { ObjectiveType.BuildStructure, new[] { "building", "countAlreadyOwned" } },
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight + Gap;
        if (!property.isExpanded)
        {
            return line;
        }

        return line * (4 + Fields(property).Length) + Gap;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float line = EditorGUIUtility.singleLineHeight;
        var row = new Rect(position.x, position.y, position.width, line);

        // The foldout reads like the objective, so a collapsed list is still readable.
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, Describe(property), true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            Next(ref row);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("type"));
            Next(ref row);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("amount"));
            foreach (string field in Fields(property))
            {
                Next(ref row);
                EditorGUI.PropertyField(row, property.FindPropertyRelative(field));
            }

            Next(ref row);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("description"), new GUIContent("Custom Text", "Replaces the automatic text."));
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void Next(ref Rect row)
    {
        row.y += EditorGUIUtility.singleLineHeight + Gap;
    }

    private static string[] Fields(SerializedProperty property)
    {
        var type = (ObjectiveType)property.FindPropertyRelative("type").enumValueIndex;
        return FieldsByType.TryGetValue(type, out string[] fields) ? fields : new string[0];
    }

    private static string Describe(SerializedProperty property)
    {
        try
        {
            if (property.boxedValue is QuestObjective objective)
            {
                string problem = objective.FindProblem();
                return problem != null ? $"{objective.Describe()}   ⚠ {problem}" : objective.Describe();
            }
        }
        catch (System.Exception)
        {
            // Some property states (multi-editing, a missing script) can't be read; the plain label is fine then.
        }

        return property.displayName;
    }
}
