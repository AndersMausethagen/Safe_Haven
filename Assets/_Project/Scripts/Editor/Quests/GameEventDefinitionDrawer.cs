using UnityEditor;
using UnityEngine;

/// <summary>
/// Every Quest Event field (on quest objectives, Quest Event Triggers and Listeners, and anywhere else) gets a "New"
/// button, so a designer can make the event right where it is needed instead of going to the Content Editor first.
/// </summary>
[CustomPropertyDrawer(typeof(GameEventDefinition))]
public sealed class GameEventDefinitionDrawer : PropertyDrawer
{
    private const float ButtonWidth = 42f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var field = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, EditorGUIUtility.singleLineHeight);
        var button = new Rect(field.xMax + 2f, position.y, ButtonWidth, EditorGUIUtility.singleLineHeight);
        EditorGUI.ObjectField(field, property, typeof(GameEventDefinition), label);
        if (!GUI.Button(button, new GUIContent("New", "Create a new Quest Event and use it here."), EditorStyles.miniButton))
        {
            return;
        }

        SerializedObject owner = property.serializedObject;
        string path = property.propertyPath;
        PopupWindow.Show(button, new NamePromptPopup("New Quest Event", name =>
        {
            ScriptableObject created = new QuestEventSection().CreateNamed(name);
            owner.Update();
            owner.FindProperty(path).objectReferenceValue = created;
            owner.ApplyModifiedProperties();
        }));
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
}
