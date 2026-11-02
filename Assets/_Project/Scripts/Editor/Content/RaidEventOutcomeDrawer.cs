using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A SerializeReference list can't say which kind to add. This shows a button with the kind on each line: an empty line
/// offers every class of the base type, a filled one can be changed or cleared.
/// </summary>
public abstract class KindMenuDrawer<T> : PropertyDrawer where T : class
{
    private static List<Type> kinds;

    protected abstract string NameOf(T value);
    protected abstract string EmptyText { get; }

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
        string shown = value is T kind ? NameOf(kind) : EmptyText;
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

    private void ShowMenu(SerializedProperty property)
    {
        kinds ??= FindKinds();
        var menu = new GenericMenu();
        SerializedObject owner = property.serializedObject;
        string path = property.propertyPath;
        foreach (Type kind in kinds)
        {
            var instance = (T)Activator.CreateInstance(kind);
            menu.AddItem(new GUIContent(NameOf(instance)), false, () => Set(owner, path, instance));
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Clear"), false, () => Set(owner, path, null));
        menu.ShowAsContext();
    }

    private static void Set(SerializedObject owner, string path, T value)
    {
        owner.Update();
        owner.FindProperty(path).managedReferenceValue = value;
        owner.ApplyModifiedProperties();
    }

    private static List<Type> FindKinds()
    {
        var found = new List<Type>();
        foreach (Type type in TypeCache.GetTypesDerivedFrom<T>())
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

/// <summary>The kind menu on a raid event's outcomes.</summary>
[CustomPropertyDrawer(typeof(RaidEventOutcome), true)]
public sealed class RaidEventOutcomeDrawer : KindMenuDrawer<RaidEventOutcome>
{
    protected override string NameOf(RaidEventOutcome value) => value.DisplayName;
    protected override string EmptyText => "Choose what happens…";
}

/// <summary>The kind menu on an elite affix's behaviours.</summary>
[CustomPropertyDrawer(typeof(EliteBehaviour), true)]
public sealed class EliteBehaviourDrawer : KindMenuDrawer<EliteBehaviour>
{
    protected override string NameOf(EliteBehaviour value) => value.DisplayName;
    protected override string EmptyText => "Choose what it does…";
}

/// <summary>The kind menu on a relic's effects.</summary>
[CustomPropertyDrawer(typeof(RelicEffect), true)]
public sealed class RelicEffectDrawer : KindMenuDrawer<RelicEffect>
{
    protected override string NameOf(RelicEffect value) => value.DisplayName;
    protected override string EmptyText => "Choose what it does…";
}
