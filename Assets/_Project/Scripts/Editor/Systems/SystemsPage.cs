using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// One page of the Systems Editor: a game system's rules (survival, carrying, raids, the portal...) shown as the
/// settings that tune it plus the numbers they lead to ("a full belly lasts 40 minutes at home"), and tables for
/// balancing many assets side by side.
/// <para>
/// Extending, to add a page: subclass SystemsPage (Title, Group, Help, Draw) and add it to SystemsEditorWindow.CreatePages.
/// Use the helpers here (SettingsPanel, Table, Derived) so every page looks and behaves the same.
/// </para>
/// </summary>
public abstract class SystemsPage
{
    public abstract string Title { get; }
    /// <summary>Heading in the sidebar: Player, World or Settings.</summary>
    public abstract string Group { get; }
    /// <summary>What the page balances, in a sentence or two, shown at the top.</summary>
    public abstract string Help { get; }

    public abstract void Draw();

    // ---------- Loading ----------

    /// <summary>A game-wide settings asset in Assets/_Project/Resources, or null with a button to create it.</summary>
    protected static T Settings<T>(string resourceName) where T : ScriptableObject
    {
        string path = $"Assets/_Project/Resources/{resourceName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            EditorGUILayout.HelpBox($"There is no {resourceName} asset yet, so the game uses built-in defaults.", MessageType.Info);
            if (GUILayout.Button($"Create {resourceName}"))
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
        }

        return asset;
    }

    public static List<T> LoadAll<T>() where T : Object
    {
        var assets = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        assets.Sort((a, b) => string.Compare(ContentUtility.DisplayName(a), ContentUtility.DisplayName(b), StringComparison.OrdinalIgnoreCase));
        return assets;
    }

    /// <summary>The player prefab, whose components hold the player's own tuning (carry budget, bag size).</summary>
    protected static GameObject PlayerPrefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");

    // ---------- Drawing ----------

    /// <summary>
    /// Draws an asset's (or a component's) fields in a panel. With property names, only those are drawn, in that order;
    /// without, every visible field is. Changes are saved with undo.
    /// </summary>
    protected static void SettingsPanel(Object target, string title, string description = null, params string[] properties)
    {
        if (target == null)
        {
            return;
        }

        ContentUtility.BeginPanel(title, description);
        var data = new SerializedObject(target);
        data.Update();
        if (properties == null || properties.Length == 0)
        {
            SerializedProperty iterator = data.GetIterator();
            bool enter = true;
            while (iterator.NextVisible(enter))
            {
                enter = false;
                if (iterator.name != "m_Script")
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
        }
        else
        {
            foreach (string name in properties)
            {
                SerializedProperty property = data.FindProperty(name);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, true);
                }
                else
                {
                    EditorGUILayout.LabelField(name, "(missing field)");
                }
            }
        }

        if (data.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(target);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select asset", EditorStyles.miniButton, GUILayout.Width(90f)))
            {
                ContentUtility.Reveal(target);
            }
        }

        ContentUtility.EndPanel();
    }

    /// <summary>A derived number: "Hunger lasts at home  40:00".</summary>
    protected static void Derived(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(260f));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        }
    }

    protected static void BeginDerived(string title = "What these numbers mean")
    {
        ContentUtility.BeginPanel(title, null);
    }

    protected static void EndDerived() => ContentUtility.EndPanel();

    /// <summary>A column of a balance table: a serialized field, its header and width.</summary>
    protected readonly struct Column
    {
        public readonly string Property;
        public readonly string Header;
        public readonly float Width;

        public Column(string property, string header, float width = 70f)
        {
            Property = property;
            Header = header;
            Width = width;
        }
    }

    /// <summary>
    /// A balance table: one row per asset with its name and the given fields editable in place, and an optional extra
    /// column of derived text. Clicking the name selects the asset.
    /// </summary>
    protected static void Table<T>(string title, string description, IList<T> assets, Column[] columns, Func<T, string> extra = null,
        string extraHeader = null) where T : Object
    {
        ContentUtility.BeginPanel(title, description);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Name", EditorStyles.miniBoldLabel, GUILayout.Width(170f));
            foreach (Column column in columns)
            {
                EditorGUILayout.LabelField(column.Header, EditorStyles.miniBoldLabel, GUILayout.Width(column.Width));
            }

            if (extra != null)
            {
                EditorGUILayout.LabelField(extraHeader ?? "", EditorStyles.miniBoldLabel);
            }
        }

        if (assets.Count == 0)
        {
            EditorGUILayout.LabelField("Nothing to show.", EditorStyles.miniLabel);
        }

        foreach (T asset in assets)
        {
            if (asset == null)
            {
                continue;
            }

            var data = new SerializedObject(asset);
            data.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ContentUtility.DisplayName(asset), EditorStyles.label, GUILayout.Width(170f)))
                {
                    ContentUtility.Reveal(asset);
                }

                foreach (Column column in columns)
                {
                    SerializedProperty property = data.FindProperty(column.Property);
                    if (property != null)
                    {
                        Cell(property, column.Width);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("–", GUILayout.Width(column.Width));
                    }
                }

                if (extra != null)
                {
                    EditorGUILayout.LabelField(extra(asset), ContentUtility.WrappedMiniLabel);
                }
            }

            if (data.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(asset);
            }
        }

        ContentUtility.EndPanel();
    }

    /// <summary>Minutes and seconds, "40:00", or "never" for infinity.</summary>
    protected static string Minutes(float minutes)
    {
        return float.IsInfinity(minutes) ? "never" : RaidStats.FormatTime(minutes * 60f);
    }

    // A table cell: just the value, without the field's headers and spacing. Balance numbers in tables are never negative.
    private static void Cell(SerializedProperty property, float width)
    {
        GUILayoutOption size = GUILayout.Width(width);
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(property.floatValue, size));
                break;
            case SerializedPropertyType.Integer:
                property.intValue = Mathf.Max(0, EditorGUILayout.IntField(property.intValue, size));
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = EditorGUILayout.Toggle(property.boolValue, size);
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = EditorGUILayout.Popup(property.enumValueIndex, property.enumDisplayNames, size);
                break;
            default:
                EditorGUILayout.PropertyField(property, GUIContent.none, size);
                break;
        }
    }
}
