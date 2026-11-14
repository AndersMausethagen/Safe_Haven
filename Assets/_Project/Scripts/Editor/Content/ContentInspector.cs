using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Draws every setting of a data asset for the Content Editor. Fields that point at other content (combat stats, an
/// attack, a resource type) get a "New" button and can be opened and edited in place, so everything about an object can
/// be changed from one panel. Combat stats are shown as a table with every stat listed.
/// </summary>
public sealed class ContentInspector
{
    private const int MaxDepth = 2;

    private static readonly Dictionary<StatType, string> StatHelp = new Dictionary<StatType, string>
    {
        { StatType.MaxHealth, "Health points." },
        { StatType.PhysicalPower, "Each point adds damage to physical hits (weapons and claws)." },
        { StatType.SpellPower, "Each point adds damage to spell hits (fire, frost, holy)." },
        { StatType.PhysicalCritChance, "Chance from 0 to 1 that a physical hit is critical. 0.1 is 10%." },
        { StatType.PhysicalCritDamage, "Extra damage on a physical critical hit. 0.5 is +50%." },
        { StatType.SpellCritChance, "Chance from 0 to 1 that a spell hit is critical." },
        { StatType.SpellCritDamage, "Extra damage on a spell critical hit. 0.5 is +50%." },
        { StatType.Armor, "Reduces physical damage. More armor helps less and less." },
        { StatType.PhysicalResistance, "Share of physical damage ignored. 0.25 is 25% less; negative is a weakness." },
        { StatType.FireResistance, "Share of fire damage ignored. 0.25 is 25% less; negative is a weakness." },
        { StatType.FrostResistance, "Share of frost damage ignored. 0.25 is 25% less; negative is a weakness." },
        { StatType.HolyResistance, "Share of holy damage ignored. 0.25 is 25% less; negative is a weakness." },
        { StatType.StructureDamage, "Extra damage against buildings. 0.5 is +50%." },
        { StatType.BluntResistance, "Share of crushing blows ignored (hammers, clubs, a troll's fist). Negative is a weakness: -0.5 takes half again as much." },
        { StatType.SlashResistance, "Share of cutting blows ignored (swords, axes, claws). Negative is a weakness." },
        { StatType.StabResistance, "Share of piercing blows ignored (spears, thrusts, arrows, fangs). Negative is a weakness." },
    };

    private readonly Func<string, ContentSection> findSection;
    private readonly Action changed;
    private readonly Dictionary<Object, SerializedObject> cache = new Dictionary<Object, SerializedObject>();
    private readonly HashSet<string> expanded = new HashSet<string>();

    public ContentInspector(Func<string, ContentSection> findSection, Action changed)
    {
        this.findSection = findSection;
        this.changed = changed;
    }

    public void ClearCache()
    {
        cache.Clear();
    }

    public SerializedObject Get(Object target)
    {
        if (!cache.TryGetValue(target, out SerializedObject data) || data.targetObject == null)
        {
            data = new SerializedObject(target);
            cache[target] = data;
        }

        return data;
    }

    /// <summary>Draws the target's fields, skipping the hidden ones. Returns true when something changed.</summary>
    public bool Draw(Object target, ICollection<string> hidden, int depth = 0)
    {
        SerializedObject data = Get(target);
        data.Update();
        EditorGUI.BeginChangeCheck();

        if (target is CombatStatsDefinition)
        {
            DrawStatTable(data);
        }
        else
        {
            SerializedProperty property = data.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || (hidden != null && hidden.Contains(property.name)))
                {
                    continue;
                }

                DrawProperty(data, property, depth);
            }
        }

        bool wasChanged = EditorGUI.EndChangeCheck();
        data.ApplyModifiedProperties();
        return wasChanged;
    }

    private void DrawProperty(SerializedObject owner, SerializedProperty property, int depth)
    {
        if (property.propertyType == SerializedPropertyType.ObjectReference && depth < MaxDepth)
        {
            ContentSection section = findSection(ReferencedTypeName(property));
            if (section != null)
            {
                DrawReference(owner, property, section, depth);
                return;
            }
        }

        EditorGUILayout.PropertyField(property, true);
    }

    private void DrawReference(SerializedObject owner, SerializedProperty property, ContentSection section, int depth)
    {
        // Drawn by hand so the "New" button lines up with the field rather than with a [Header] above it.
        FieldInfo field = owner.targetObject.GetType().GetField(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        HeaderAttribute header = field != null ? field.GetCustomAttribute<HeaderAttribute>() : null;
        if (header != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(property, section.AssetType, new GUIContent(property.displayName, property.tooltip));
            if (section.CanCreate && GUILayout.Button(new GUIContent("New", $"Create a new {section.Noun} and use it here."), EditorStyles.miniButton, GUILayout.Width(42f)))
            {
                string path = property.propertyPath;
                Rect anchor = GUILayoutUtility.GetLastRect();
                PopupWindow.Show(anchor, new NamePromptPopup($"New {section.Noun}", name =>
                {
                    ScriptableObject created = section.CreateNamed(name);
                    owner.Update();
                    owner.FindProperty(path).objectReferenceValue = created;
                    owner.ApplyModifiedProperties();
                    expanded.Add(Key(owner, path));
                    changed?.Invoke();
                }));
            }
        }

        Object value = property.objectReferenceValue;
        if (value == null || value is GameObject)
        {
            return;
        }

        string key = Key(owner, property.propertyPath);
        bool open = expanded.Contains(key);
        EditorGUI.indentLevel++;
        bool now = EditorGUILayout.Foldout(open, $"Edit {section.Noun}: {ContentUtility.DisplayName(value)}", true);
        EditorGUI.indentLevel--;
        if (now != open)
        {
            if (now)
            {
                expanded.Add(key);
            }
            else
            {
                expanded.Remove(key);
            }
        }

        if (!now)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"Shared: changes affect everything that uses '{ContentUtility.DisplayName(value)}'.",
                ContentUtility.WrappedMiniLabel);
            Draw(value, section.HiddenFields, depth + 1);
        }
    }

    private static void DrawStatTable(SerializedObject data)
    {
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("A stat left at 0 has no effect. Hover a stat for what it does.", ContentUtility.WrappedMiniLabel);
        SerializedProperty list = data.FindProperty("stats");

        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            int index = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue == (int)stat)
                {
                    index = i;
                    break;
                }
            }

            float current = index >= 0 ? list.GetArrayElementAtIndex(index).FindPropertyRelative("value").floatValue : 0f;
            StatHelp.TryGetValue(stat, out string help);
            float next = EditorGUILayout.FloatField(new GUIContent(ObjectNames.NicifyVariableName(stat.ToString()), help), current);
            if (Mathf.Approximately(next, current))
            {
                continue;
            }

            if (index < 0)
            {
                list.arraySize++;
                index = list.arraySize - 1;
                list.GetArrayElementAtIndex(index).FindPropertyRelative("stat").intValue = (int)stat;
            }

            list.GetArrayElementAtIndex(index).FindPropertyRelative("value").floatValue = next;
        }
    }

    // "PPtr<$CombatStatsDefinition>" -> "CombatStatsDefinition"
    private static string ReferencedTypeName(SerializedProperty property)
    {
        string type = property.type;
        int start = type.IndexOf('$');
        int end = type.LastIndexOf('>');
        return start >= 0 && end > start ? type.Substring(start + 1, end - start - 1) : type;
    }

    private static string Key(SerializedObject owner, string path)
    {
        return owner.targetObject.GetHashCode() + ":" + path;
    }
}
