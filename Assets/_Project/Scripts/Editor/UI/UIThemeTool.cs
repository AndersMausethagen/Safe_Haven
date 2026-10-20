using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the UITheme to every UI prefab in Assets/_Project/Prefabs/UI (menu: Safe Haven > UI > Apply UI Theme). It
/// recolors by role, so it is safe to run again after changing the theme:
/// <list type="bullet">
/// <item>Objects named "Panel" (window panels) get the panel color and a gold rim.</item>
/// <item>Buttons whose color is a neutral grey get the button color; strongly colored buttons keep theirs.</item>
/// <item>Text named "Title" gets the title color; pure white text becomes parchment.</item>
/// </list>
/// HUD pieces with their own art (the vitals frame, slots) are left alone because they don't match these roles.
/// </summary>
public static class UIThemeTool
{
    private const string ThemePath = "Assets/_Project/UI/UITheme.asset";
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI";

    [MenuItem("Safe Haven/UI/Apply UI Theme")]
    public static void ApplyFromMenu()
    {
        int changed = Apply(LoadOrCreateTheme());
        EditorUtility.DisplayDialog("UI Theme", $"Recolored {changed} UI elements in {PrefabFolder}.", "OK");
    }

    [MenuItem("Safe Haven/UI/Select UI Theme")]
    public static void SelectTheme()
    {
        Selection.activeObject = LoadOrCreateTheme();
    }

    public static UITheme LoadOrCreateTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
        }

        return theme;
    }

    /// <summary>Recolors every UI prefab. Returns how many elements changed.</summary>
    public static int Apply(UITheme theme)
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int before = changed;
            try
            {
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "Panel" && image.GetComponent<Button>() == null)
                    {
                        changed += Set(image, theme.panel);
                        Outline rim = image.GetComponent<Outline>();
                        if (rim == null)
                        {
                            rim = image.gameObject.AddComponent<Outline>();
                        }

                        rim.effectColor = theme.panelRim;
                        rim.effectDistance = new Vector2(2f, -2f);
                    }
                }

                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.targetGraphic is Image image && IsNeutral(image.color))
                    {
                        changed += Set(image, theme.button);
                        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                        {
                            label.color = theme.buttonText;
                        }
                    }
                }

                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name == "Title")
                    {
                        if (text.color != theme.title)
                        {
                            text.color = theme.title;
                            changed++;
                        }
                    }
                    else if (text.color == Color.white)
                    {
                        text.color = theme.body;
                        changed++;
                    }
                }

                if (changed > before)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }

    private static int Set(Image image, Color color)
    {
        if (image.color == color)
        {
            return 0;
        }

        image.color = color;
        return 1;
    }

    // Grey-ish colors (all channels close together) are default buttons; saturated ones were chosen on purpose.
    private static bool IsNeutral(Color color)
    {
        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return max - min < 0.12f;
    }
}
