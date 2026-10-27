using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneEventAction))]
public sealed class SceneEventActionDrawer : PropertyDrawer
{
    private static string[] Fields(SerializedProperty property)
    {
        switch ((SceneEventActionKind)property.FindPropertyRelative("kind").enumValueIndex)
        {
            case SceneEventActionKind.PlayMusic: return new[] { "sceneAudio", "music" };
            case SceneEventActionKind.SilenceMusic:
            case SceneEventActionKind.ResumeMusic: return new[] { "sceneAudio" };
            case SceneEventActionKind.ChangeWeather: return new[] { "weatherSystem", "weather", "transitionSeconds" };
            case SceneEventActionKind.SetObjectActive: return new[] { "target", "active" };
            default: return new[] { "parameters" };
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + 4f;
        foreach (string field in Fields(property))
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 4f;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(row, property.FindPropertyRelative("kind"), label);
        row.y += row.height + 4f;
        EditorGUI.indentLevel++;
        foreach (string field in Fields(property))
        {
            var value = property.FindPropertyRelative(field);
            row.height = EditorGUI.GetPropertyHeight(value, true);
            EditorGUI.PropertyField(row, value, true);
            row.y += row.height + 4f;
        }
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
