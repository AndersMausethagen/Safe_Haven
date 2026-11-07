using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a quest objective with only the fields its type uses (QuestObjective.FieldsOf), each named for what it means in
/// that type ("Who", "Place", "Object"), plus a line showing how it will read in the game, so designers aren't faced with
/// settings that do nothing. A Quest Target field has a New button that makes one on the spot. Used for objectives, a
/// quest's Start When and the stories' deed nodes.
/// </summary>
[CustomPropertyDrawer(typeof(QuestObjective))]
public sealed class QuestObjectiveDrawer : PropertyDrawer
{
    private const float Gap = 2f;

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
            var type = (ObjectiveType)property.FindPropertyRelative("type").enumValueIndex;
            foreach (string field in Fields(property))
            {
                Next(ref row);
                SerializedProperty value = property.FindPropertyRelative(field);
                var named = new GUIContent(LabelOf(type, field) ?? value.displayName, value.tooltip);
                if (field == "target")
                {
                    DrawTarget(row, value, named);
                }
                else
                {
                    EditorGUI.PropertyField(row, value, named);
                }
            }

            Next(ref row);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("description"), new GUIContent("Custom Text", "Replaces the automatic text."));
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    /// <summary>What a field is called in an objective of this type; null keeps its own name.</summary>
    public static string LabelOf(ObjectiveType type, string field)
    {
        switch (field)
        {
            case "person":
                return type == ObjectiveType.TalkTo ? "Who (a person)" : "Who (empty: anyone)";
            case "target":
                return type == ObjectiveType.TalkTo ? "Or What (Quest Target)" : type == ObjectiveType.ReachPlace ? "Place" : "Object";
            case "job":
                return "Job (empty: any)";
            case "breach":
                return "Breach (empty: any)";
            case "rank":
                return "Haven Rank";
            case "bringHome":
                return "Must Come Home";
            default:
                return null;
        }
    }

    // A Quest Target field with a button that makes a new one, named by the designer, and assigns it.
    private static void DrawTarget(Rect row, SerializedProperty value, GUIContent label)
    {
        const float Button = 48f;
        var field = new Rect(row.x, row.y, row.width - Button - 2f, row.height);
        EditorGUI.PropertyField(field, value, label);
        if (GUI.Button(new Rect(field.xMax + 2f, row.y, Button, row.height), new GUIContent("New", "Make a new Quest Target: a place or thing quests can point at.")))
        {
            QuestTargetDefinition made = QuestTargetTools.CreateWithDialog();
            if (made != null)
            {
                value.objectReferenceValue = made;
                value.serializedObject.ApplyModifiedProperties();
            }

            GUIUtility.ExitGUI();
        }
    }

    private static void Next(ref Rect row)
    {
        row.y += EditorGUIUtility.singleLineHeight + Gap;
    }

    private static string[] Fields(SerializedProperty property)
    {
        var type = (ObjectiveType)property.FindPropertyRelative("type").enumValueIndex;
        return QuestObjective.FieldsOf(type);
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
