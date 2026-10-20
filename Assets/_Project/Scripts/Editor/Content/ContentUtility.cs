using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Small helpers the Content Editor sections share.</summary>
public static class ContentUtility
{
    public const string SearchRoot = "Assets/_Project";

    /// <summary>The object's DisplayName if it has one, otherwise its file name made readable.</summary>
    public static string DisplayName(Object asset)
    {
        if (asset == null)
        {
            return "(missing)";
        }

        PropertyInfo property = asset.GetType().GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
        string value = property != null && property.PropertyType == typeof(string) ? property.GetValue(asset) as string : null;
        return string.IsNullOrWhiteSpace(value) ? ObjectNames.NicifyVariableName(asset.name) : value;
    }

    /// <summary>The object's Id, or null for content that has no id.</summary>
    public static string IdOf(Object asset)
    {
        if (asset == null)
        {
            return null;
        }

        PropertyInfo property = asset.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        return property != null && property.PropertyType == typeof(string) ? property.GetValue(asset) as string ?? "" : null;
    }

    public static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            return;
        }

        Rect pixels = sprite.textureRect;
        Texture2D texture = sprite.texture;
        var uv = new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
        float aspect = pixels.width / Mathf.Max(1f, pixels.height);
        Rect fitted = rect;
        if (aspect > 1f)
        {
            fitted.height = rect.width / aspect;
            fitted.y += (rect.height - fitted.height) * 0.5f;
        }
        else
        {
            fitted.width = rect.height * aspect;
            fitted.x += (rect.width - fitted.width) * 0.5f;
        }

        GUI.DrawTextureWithTexCoords(fitted, texture, uv);
    }

    /// <summary>Scenes, prefabs and data assets that point at the asset, minus those the filter says to ignore.</summary>
    public static List<string> FindReferences(Object asset, Func<string, bool> ignore)
    {
        string target = AssetDatabase.GetAssetPath(asset);
        var users = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject", new[] { SearchRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == target || (ignore != null && ignore(path)))
            {
                continue;
            }

            if (Array.IndexOf(AssetDatabase.GetDependencies(path, false), target) >= 0)
            {
                users.Add(path);
            }
        }

        return users;
    }

    public static void Reveal(Object asset)
    {
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    public static void SetReference(Object owner, string property, Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);
    }

    public static Object GetReference(Object owner, string property)
    {
        SerializedProperty found = new SerializedObject(owner).FindProperty(property);
        return found != null ? found.objectReferenceValue : null;
    }

    public static GUIStyle WrappedMiniLabel => new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

    public static GUIStyle HeaderLabel => new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };

    /// <summary>A titled box that groups part of a details panel.</summary>
    public static void BeginPanel(string title, string description = null)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(description))
        {
            EditorGUILayout.LabelField(description, WrappedMiniLabel);
        }

        EditorGUILayout.Space(2f);
    }

    public static void EndPanel()
    {
        EditorGUILayout.EndVertical();
    }
}

/// <summary>
/// A small popup that asks for a name, used by the "New" buttons beside data fields such as Combat Stats or Attack.
/// </summary>
public sealed class NamePromptPopup : PopupWindowContent
{
    private const string FieldName = "ContentNamePrompt";
    private readonly string title;
    private readonly Action<string> create;
    private string value = "";
    private bool focused;

    public NamePromptPopup(string title, Action<string> create)
    {
        this.title = title;
        this.create = create;
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(300f, 76f);
    }

    public override void OnGUI(Rect rect)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        GUI.SetNextControlName(FieldName);
        value = EditorGUILayout.TextField("Name", value);
        if (!focused)
        {
            EditorGUI.FocusTextInControl(FieldName);
            focused = true;
        }

        bool blank = string.IsNullOrWhiteSpace(value);
        Event current = Event.current;
        bool enter = current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);
        using (new EditorGUI.DisabledScope(blank))
        {
            if (GUILayout.Button("Create") || (enter && !blank))
            {
                create(value.Trim());
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
        }
    }
}
