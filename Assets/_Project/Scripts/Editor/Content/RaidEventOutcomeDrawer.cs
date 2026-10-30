using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Raid event outcomes are SerializeReference, so a plain list can't say which kind to add. This shows a button with the
/// kind on each line: an empty line offers every RaidEventOutcome class, a filled one can be changed or cleared.
/// </summary>
[CustomPropertyDrawer(typeof(RaidEventOutcome), true)]
public sealed class RaidEventOutcomeDrawer : PropertyDrawer
{
    private static List<Type> kinds;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return property.managedReferenceValue == null
            ? EditorGUIUtility.singleLineHeight
            : EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var button = new Rect(line.x + EditorGUIUtility.labelWidth, line.y, line.width - EditorGUIUtility.labelWidth, line.height);
        object value = property.managedReferenceValue;
        string shown = value is RaidEventOutcome outcome ? outcome.DisplayName : "Choose what happens…";
        if (EditorGUI.DropdownButton(button, new GUIContent(shown), FocusType.Keyboard))
        {
            ShowMenu(property);
        }

        if (value == null)
        {
            EditorGUI.LabelField(line, label);
            return;
        }

        EditorGUI.PropertyField(position, property, GUIContent.none, true);
    }

    private static void ShowMenu(SerializedProperty property)
    {
        kinds ??= FindKinds();
        var menu = new GenericMenu();
        SerializedObject owner = property.serializedObject;
        string path = property.propertyPath;
        foreach (Type kind in kinds)
        {
            var instance = (RaidEventOutcome)Activator.CreateInstance(kind);
            menu.AddItem(new GUIContent(instance.DisplayName), false, () => Set(owner, path, instance));
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Clear"), false, () => Set(owner, path, null));
        menu.ShowAsContext();
    }

    private static void Set(SerializedObject owner, string path, RaidEventOutcome value)
    {
        owner.Update();
        owner.FindProperty(path).managedReferenceValue = value;
        owner.ApplyModifiedProperties();
    }

    private static List<Type> FindKinds()
    {
        var found = new List<Type>();
        foreach (Type type in TypeCache.GetTypesDerivedFrom<RaidEventOutcome>())
        {
            if (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
            {
                found.Add(type);
            }
        }

        found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return found;
    }
}
