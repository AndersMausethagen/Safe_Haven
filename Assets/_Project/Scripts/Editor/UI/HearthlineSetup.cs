using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts the UI in the Hearthline style (UIStyle, GDD §18.7): the fonts and their shadowed versions, the roles on every
/// window, button and slot in the UI prefabs, and the frameless HUD. Safe to run again: it sets positions and roles outright.
/// Safe Haven › UI › Apply Hearthline.
/// </summary>
public static class HearthlineSetup
{
    private const string FontFolder = "Assets/_Project/UI/Fonts/";
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
    private const string StylePath = "Assets/_Project/Resources/UIStyle.asset";
    private const string OldFontName = "LiberationSans SDF";
    private const string OnTop = "Always On Top";

    public static readonly string[] GameScenes =
    {
        "Assets/_Project/Scenes/MainMenu.unity",
        "Assets/_Project/Scenes/RaidResults.unity",
        "Assets/_Project/Scenes/Prologue_MistyWoods.unity",
        "Assets/_Project/Scenes/SafeHaven.unity",
    };

    [MenuItem("Safe Haven/UI/Apply Hearthline")]
    public static void ApplyAll()
    {
        SetUpType();
        int changed = ApplyPrefabs();
        foreach (string scene in GameScenes)
        {
            ApplyScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Hearthline: {changed} prefabs restyled.");
    }

    // ----- Type -----

    public static void SetUpType()
    {
        TMP_FontAsset body = Font("FiraSans-Regular SDF");
        TMP_FontAsset bold = Font("FiraSans-SemiBold SDF");
        TMP_FontAsset title = Font("Gelasio SDF");
        TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/" + OldFontName);

        foreach (TMP_FontAsset font in new[] { body, bold, title })
        {
            var data = new SerializedObject(font);
            data.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            data.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            // Arrows and the odd symbol aren't in these fonts.
            if (fallback != null && !font.fallbackFontAssetTable.Contains(fallback))
            {
                font.fallbackFontAssetTable.Add(fallback);
            }

            EditorUtility.SetDirty(font);
        }

        var weights = new SerializedObject(body);
        weights.FindProperty("m_FontWeightTable").GetArrayElementAtIndex(7).FindPropertyRelative("regularTypeface").objectReferenceValue = bold;
        weights.ApplyModifiedPropertiesWithoutUndo();

        var settings = new SerializedObject(TMP_Settings.instance);
        settings.FindProperty("m_defaultFontAsset").objectReferenceValue = body;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);

        var style = AssetDatabase.LoadAssetAtPath<UIStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<UIStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }

        var values = new SerializedObject(style);
        values.FindProperty("bodyFont").objectReferenceValue = body;
        values.FindProperty("titleFont").objectReferenceValue = title;
        values.FindProperty("bodyOnWorld").objectReferenceValue = ShadowOf(body);
        values.FindProperty("titleOnWorld").objectReferenceValue = ShadowOf(title);
        values.ApplyModifiedPropertiesWithoutUndo();

        var markers = AssetDatabase.LoadAssetAtPath<QuestMarkerSettings>("Assets/_Project/Resources/QuestMarkerSettings.asset");
        if (markers != null)
        {
            var data = new SerializedObject(markers);
            data.FindProperty("font").objectReferenceValue = title;
            data.FindProperty("fontMaterial").objectReferenceValue = OnTopOf(title);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset Font(string name)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + name + ".asset");
        if (font == null)
        {
            throw new System.InvalidOperationException($"{name} is missing from {FontFolder}.");
        }

        return font;
    }

    private static Material ShadowOf(TMP_FontAsset font)
    {
        Material material = Variant(font, "Shadow", font.material.shader);
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.95f));
        material.SetFloat("_UnderlayOffsetX", 0.15f);
        material.SetFloat("_UnderlayOffsetY", -0.4f);
        material.SetFloat("_UnderlayDilate", 0.75f);
        material.SetFloat("_UnderlaySoftness", 0.7f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // World markers draw over everything: the old font's material did this, and a material only suits its own font's atlas.
    private static Material OnTopOf(TMP_FontAsset font)
    {
        var old = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/UI/Materials/{OldFontName} {OnTop}.mat");
        Material material = Variant(font, OnTop, old != null ? old.shader : font.material.shader);
        if (old != null && old.HasProperty("unity_GUIZTestMode"))
        {
            material.SetFloat("unity_GUIZTestMode", old.GetFloat("unity_GUIZTestMode"));
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Variant(TMP_FontAsset font, string suffix, Shader shader)
    {
        string path = $"{FontFolder}{font.name} {suffix}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.CopyPropertiesFromMaterial(font.material);
        return material;
    }

    // ----- Prefabs -----

    public static int ApplyPrefabs()
    {
        int changed = 0;
        WorldPrompt();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Restyle(root);
                switch (root.name)
                {
                    case "PlayerVitalsHUD": Vitals(root); break;
                    case "SkillHUD": SkillBar(root); break;
                    case "QuestTracker": QuestTracker(root); break;
                    case "RaidHUD": RaidHud(root); break;
                    case "UIRoot": UiRoot(root); break;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }

    // The player carries the interaction prompt, and overrides its text with a material that draws over trees and roofs.
    private static void WorldPrompt()
    {
        const string path = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Named outright: TextMesh Pro drops a material that doesn't suit its font as soon as the prefab loads.
            foreach (WorldInteractionIndicator prompt in root.GetComponentsInChildren<WorldInteractionIndicator>(true))
            {
                foreach (TMP_Text text in prompt.GetComponentsInChildren<TMP_Text>(true))
                {
                    // No bold here: it would come from the SemiBold atlas, which has no such material, and sink behind the panel.
                    TMP_FontAsset font = text.name == "TargetName" ? UIStyle.Current.TitleFont : UIStyle.Current.BodyFont;
                    text.font = font;
                    text.fontSharedMaterial = OnTopOf(font);
                    text.fontStyle = FontStyles.Normal;
                    GiveItsLineRoom(text);
                    EditorUtility.SetDirty(text);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A line that doesn't fit its box is dropped whole by Truncate and Ellipsis, not clipped, so the label comes up empty.
    private static void GiveItsLineRoom(TMP_Text text)
    {
        RectTransform rect = text.rectTransform;
        if (text.overflowMode != TextOverflowModes.Overflow && rect.anchorMin.y == rect.anchorMax.y)
        {
            float line = text.fontSize * text.font.faceInfo.lineHeight / text.font.faceInfo.pointSize;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(rect.sizeDelta.y, Mathf.Ceil(line) + 4f));
        }
    }

    /// <summary>Fonts, roles and text colours for one piece of UI, found by what each part is.</summary>
    public static void Restyle(GameObject root)
    {
        UIStyle style = UIStyle.Current;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Refont(text, style);
        }

        foreach (ItemSlotView slot in root.GetComponentsInChildren<ItemSlotView>(true))
        {
            var data = new SerializedObject(slot);
            data.FindProperty("emptyColor").colorValue = style.Inset;
            data.FindProperty("filledColor").colorValue = style.Filled;
            data.FindProperty("hoverColor").colorValue = style.ControlLit;
            data.FindProperty("lockedColor").colorValue = style.Panel;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (slot.TryGetComponent(out Image frame))
            {
                Thin(frame, style.Hairline);
                Role(frame, UIRole.Kind.Slot);
            }
        }

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.targetGraphic is Image image)
            {
                Role(image, IsGreen(image.color) || HasRole(image, UIRole.Kind.PrimaryButton) ? UIRole.Kind.PrimaryButton : UIRole.Kind.Button);
            }
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Button>() != null || image.GetComponent<UIRole>() != null)
            {
                continue;
            }

            if (image.GetComponent<Outline>() != null || IsWindow(image))
            {
                Thin(image, style.Rim);
                Role(image, UIRole.Kind.Window);
            }
            else if (Near(image.color, new Color(0f, 0f, 0f, 0.25f)) || Near(image.color, new Color32(0x21, 0x24, 0x2B, 0xF2)))
            {
                Role(image, UIRole.Kind.Inset);
            }
            else if (Near(image.color, new Color32(0x1F, 0x21, 0x29, 0xE6)) || Near(image.color, new Color32(0x14, 0x17, 0x1C, 0xE6)))
            {
                Thin(image, style.Hairline);
                Role(image, UIRole.Kind.Slot);
            }
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            GiveItsLineRoom(text);
            if (text.GetComponentInParent<Button>(true) != null)
            {
                continue;
            }

            if (text.name == "Title" && text.fontSize >= 22f)
            {
                style.Title(text);
            }
            else if (text.name.EndsWith("Title") || text.name == "Title")
            {
                text.color = style.Accent;
            }
            else if (IsWhiteish(text.color))
            {
                text.color = style.Text;
            }
        }
    }

    private static void Refont(TMP_Text text, UIStyle style)
    {
        Material material = text.fontSharedMaterial;
        bool staleMaterial = material != null && IsOldFont(material.name);
        if (!IsOldFont(text.font != null ? text.font.name : "") && !staleMaterial)
        {
            return;
        }

        bool onTop = material != null && material.name.Contains(OnTop);
        bool shadow = material != null && material.name.Contains("Shadow");
        TMP_FontAsset font = text.font != null && text.font == style.TitleFont ? style.TitleFont : style.BodyFont;
        text.font = font;
        text.fontSharedMaterial = onTop ? OnTopOf(font) : shadow ? ShadowOf(font) : font.material;
    }

    // A prefab or scene can name a font, or override only its material, so both are looked at.
    private static bool IsOldFont(string name) => name.Length == 0 || name.StartsWith(OldFontName) || name.StartsWith("FolktaleDisplay") || name.StartsWith("AlegreyaSans");

    private static bool IsWindow(Image image)
    {
        if (image.name == "Panel" || image.name == "Tooltip" || image.name == "Slate")
        {
            return true;
        }

        return image.name.EndsWith(" Window") && image.GetComponentsInChildren<TMP_Text>(true).Length > 0;
    }

    private static bool HasRole(Image image, UIRole.Kind kind) => image.TryGetComponent(out UIRole role) && role.Role == kind;

    private static void Role(Image image, UIRole.Kind kind)
    {
        UIRole role = image.GetComponent<UIRole>();
        if (role == null)
        {
            role = image.gameObject.AddComponent<UIRole>();
        }

        role.Set(kind);
    }

    private static void Thin(Image image, Color color)
    {
        Outline line = image.GetComponent<Outline>();
        if (line == null)
        {
            line = image.gameObject.AddComponent<Outline>();
        }

        line.effectColor = color;
        line.effectDistance = new Vector2(1f, -1f);
    }

    private static bool Near(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f && Mathf.Abs(a.a - b.a) < 0.05f;
    }

    private static bool IsGreen(Color color) => color.g > color.r + 0.1f && color.g > color.b + 0.05f;

    private static bool IsWhiteish(Color color) => color.r > 0.85f && color.g > 0.82f && color.b > 0.72f;

    // ----- The frameless HUD -----

    private static void Vitals(GameObject root)
    {
        UIStyle style = UIStyle.Current;
        Transform panel = root.transform.Find("Vitals Panel");
        ((RectTransform)panel).sizeDelta = new Vector2(350f, 134f);
        foreach (string frame in new[] { "Rim", "Backing", "Inner Rule", "Inner Backing", "Crest", "Crest Inset", "Foot", "Health Rune", "Stamina Rune" })
        {
            Hide(panel, frame);
        }

        Bar(panel, "Health Bar", 0f, -22f, 350f, 5f, style.Danger);
        Bar(panel, "Stamina Bar", 0f, -60f, 350f, 5f, style.Good);
        Bar(panel, "Focus Bar", 0f, -69f, 350f, 2f, style.Accent);
        Bar(panel, "Hunger Bar", 0f, -112f, 165f, 3f, new Color32(0xC9, 0x8A, 0x3B, 0xFF));
        Bar(panel, "Thirst Bar", 185f, -112f, 165f, 3f, new Color32(0x6F, 0xA3, 0xC7, 0xFF));
        Label(panel, "Health Label", 0f, 0f, 200f);
        Value(panel, "Health Value", 200f, 0f, 150f);
        Label(panel, "Stamina Label", 0f, -38f, 200f);
        Value(panel, "Stamina Value", 200f, -38f, 150f);
        Label(panel, "Hunger Label", 0f, -92f, 165f);
        Label(panel, "Thirst Label", 185f, -92f, 165f);
        Hide(panel, "Focus Label");
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            style.OnWorld(text);
        }

        // The stamina frame stays: it is unseen at rest, but flashes when an action is refused and carries the dodge's glow.
        var frameImage = panel.Find("Stamina Bar/Border").GetComponent<Image>();
        frameImage.gameObject.SetActive(true);
        frameImage.color = Color.clear;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = false;
        group.interactable = false;
        var view = new SerializedObject(root.GetComponent<PlayerVitalsView>());
        view.FindProperty("group").objectReferenceValue = group;
        view.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Bar(Transform panel, string name, float x, float y, float width, float height, Color fill)
    {
        var bar = (RectTransform)panel.Find(name);
        Place(bar, x, y, width, height);
        Hide(bar, "Border");
        Hide(bar, "Cap Left");
        Hide(bar, "Cap Right");
        var well = (RectTransform)bar.Find("Well");
        well.offsetMin = well.offsetMax = Vector2.zero;
        well.GetComponent<Image>().color = UIStyle.Current.Track;
        Shadow shadow = well.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = well.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -1f);
        foreach (string notch in new[] { "Notch 1", "Notch 2", "Notch 3" })
        {
            Hide(well, notch);
        }

        Transform fillPart = well.Find("Fill");
        fillPart.GetComponent<Image>().color = fill;
        Hide(fillPart, "Sheen");
        Hide(fillPart, "Shade");
    }

    private static void Label(Transform panel, string name, float x, float y, float width)
    {
        var text = panel.Find(name).GetComponent<TMP_Text>();
        Place((RectTransform)text.transform, x, y, width, 20f);
        text.fontSize = 15f;
        text.characterSpacing = 8f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = UIStyle.Current.Text;
    }

    private static void Value(Transform panel, string name, float x, float y, float width)
    {
        var text = panel.Find(name).GetComponent<TMP_Text>();
        Place((RectTransform)text.transform, x, y, width, 20f);
        text.fontSize = 15f;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.color = UIStyle.Current.Text;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Hide(Transform parent, string name)
    {
        Transform part = parent.Find(name);
        if (part != null)
        {
            part.gameObject.SetActive(false);
        }
    }

    private static void SkillBar(GameObject root)
    {
        var bar = (RectTransform)root.transform.Find("Skill Bar");
        bar.sizeDelta = new Vector2(362f, 66f);
        bar.anchoredPosition = new Vector2(0f, 34f);
        foreach (string frame in new[] { "Rim", "Backing", "Inner Rule", "Inner Backing", "Crest", "Crest Inset" })
        {
            Hide(bar, frame);
        }

        UIStyle style = UIStyle.Current;
        for (int i = 0; i < 5; i++)
        {
            var slot = (RectTransform)bar.Find($"Skill Slot {i + 1}");
            slot.anchoredPosition = new Vector2(1f + i * 74f, 0f);
            slot.sizeDelta = new Vector2(64f, 64f);
            ((RectTransform)slot.Find("Icon")).sizeDelta = new Vector2(-4f, -4f);
            ((RectTransform)slot.Find("Cooldown")).sizeDelta = new Vector2(-4f, -4f);
            foreach (TMP_Text text in slot.GetComponentsInChildren<TMP_Text>(true))
            {
                style.OnWorld(text);
            }
        }
    }

    private static void QuestTracker(GameObject root)
    {
        Transform panel = root.transform.Find("Panel");
        Object.DestroyImmediate(panel.GetComponent<UIRole>());
        Object.DestroyImmediate(panel.GetComponent<Outline>());
        panel.GetComponent<Image>().color = Color.clear;
        var rect = (RectTransform)panel;
        rect.anchoredPosition = new Vector2(-34f, -30f);
        rect.sizeDelta = new Vector2(420f, rect.sizeDelta.y);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperRight;
        Hide(panel, "Header");
        var body = panel.Find("Body").GetComponent<TMP_Text>();
        UIStyle.Current.OnWorld(body);
        body.alignment = TextAlignmentOptions.TopRight;
        body.fontSize = 18f;
        body.color = UIStyle.Current.Text;
    }

    private static void RaidHud(GameObject root)
    {
        UIStyle style = UIStyle.Current;
        foreach (string name in new[] { "Timer Panel", "Exits Panel" })
        {
            Transform panel = root.transform.Find(name);
            Object.DestroyImmediate(panel.GetComponent<UIRole>());
            Object.DestroyImmediate(panel.GetComponent<Outline>());
            panel.GetComponent<Image>().color = Color.clear;
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                style.OnWorld(text, text.name == "Timer");
            }
        }

        foreach (string list in new[] { "Exits Panel/Exits", "At Risk", "Companion", "Timer Panel/Extraction" })
        {
            root.transform.Find(list).GetComponent<TMP_Text>().fontSize = 17f;
        }

        Transform banner = root.transform.Find("Banner");
        var bannerImage = banner.GetComponent<Image>();
        Thin(bannerImage, style.Rim);
        Role(bannerImage, UIRole.Kind.Window);
        style.Title(banner.Find("Title").GetComponent<TMP_Text>());
    }

    // The camera buttons of an old Escape menu, left on it with nothing behind them: they stood in the middle of the screen.
    private static void UiRoot(GameObject root)
    {
        Transform menu = root.transform.Find("Screen Canvas/PauseMenu");
        foreach (Transform child in menu.Cast<Transform>().Where(c => c.name.StartsWith("Camera ")).ToArray())
        {
            Object.DestroyImmediate(child.gameObject, true);
        }
    }

    // ----- Scenes -----

    /// <summary>The UI a scene holds itself: fonts, and the clock when it has one. Saves and closes it, leaving the open scene as it was.</summary>
    public static void ApplyScene(string path)
    {
        var active = EditorSceneManager.GetActiveScene();
        bool wasOpen = active.path == path;
        var scene = wasOpen ? active : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        UIStyle style = UIStyle.Current;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(text))
                {
                    Refont(text, style);
                    EditorUtility.SetDirty(text);
                }
                else
                {
                    FollowPrefab(text);
                }
            }

            foreach (EnvironmentHudView clock in root.GetComponentsInChildren<EnvironmentHudView>(true))
            {
                Clock(clock, style);
            }

            // Gelasio's lines stand a little further apart than Georgia's, which set the title rule beneath it.
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Title" && text.font == style.TitleFont && text.fontSize >= 100f)
                {
                    text.lineSpacing = -34f;
                    EditorUtility.SetDirty(text);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!wasOpen)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // A scene's override of a prefab's text can hold the old font's material, and it wins: the prefab says how text looks.
    private static void FollowPrefab(TMP_Text text)
    {
        var data = new SerializedObject(text);
        foreach (string field in new[] { "m_sharedMaterial", "m_fontAsset" })
        {
            SerializedProperty property = data.FindProperty(field);
            if (property.prefabOverride)
            {
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
            }
        }
    }

    private static void Clock(EnvironmentHudView clock, UIStyle style)
    {
        if (clock.TryGetComponent(out Image background))
        {
            background.color = Color.clear;
            EditorUtility.SetDirty(background);
        }

        foreach (TMP_Text text in clock.GetComponentsInChildren<TMP_Text>(true))
        {
            style.OnWorld(text);
            EditorUtility.SetDirty(text);
        }
    }
}
