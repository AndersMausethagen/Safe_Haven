using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// A section whose content has a generated world prefab (and optionally an icon): resources, monsters, buildings and
/// items. It adds the World Prefab panel (create, update from data, swap the 3D model, place in the scene, edit the
/// prefab's components), the Icon panel (generate a sprite from the model) and the prefab choices on the New form.
/// </summary>
public abstract class PrefabContentSection : ContentSection
{
    private PrefabTools tools;
    private GameObject createModel;
    private bool createPrefab = true;
    private GameObject panelModel;
    private Object panelModelOwner;
    private Object paintOwner;
    private IconShape paintShape;
    private Color paintColor = new Color(0.62f, 0.62f, 0.66f);

    /// <summary>Name of the definition field that holds the generated prefab.</summary>
    protected abstract string PrefabField { get; }
    protected abstract PrefabBuildUtility.Placeholder Placeholder { get; }
    /// <summary>What the prefab is, in a sentence, for the panel's description.</summary>
    protected abstract string PrefabDescription { get; }
    /// <summary>Throws ArgumentException when the model can't be used.</summary>
    protected abstract void ValidateModel(GameObject model);
    protected abstract GameObject BuildPrefab(Object asset, GameObject model);
    protected abstract void UpdatePrefab(GameObject prefab, Object asset);
    protected abstract List<string> PrefabProblems(GameObject prefab, Object asset);

    /// <summary>Name of the definition's icon field, or null when it has none.</summary>
    protected virtual string IconField => null;
    protected virtual string IconFolder => null;

    /// <summary>The model to suggest for a new prefab.</summary>
    protected virtual GameObject DefaultModel(Object asset) => null;

    /// <summary>Called when a model is used for the prefab, so the definition can remember it.</summary>
    protected virtual void OnModelUsed(Object asset, GameObject model)
    {
    }

    protected override string[] LinkedPrefabFields => new[] { PrefabField };

    public override ICollection<string> HiddenFields
    {
        get
        {
            var hidden = new List<string> { PrefabField };
            if (IconField != null)
            {
                hidden.Add(IconField);
            }

            return hidden;
        }
    }

    private PrefabTools Tools => tools ??= new PrefabTools(() => Host?.Repaint());

    public override GameObject LinkedPrefab(Object asset)
    {
        return ContentUtility.GetReference(asset, PrefabField) as GameObject;
    }

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        GameObject prefab = LinkedPrefab(asset);
        if (prefab != null)
        {
            foreach (string problem in PrefabProblems(prefab, asset))
            {
                problems.Add("Prefab: " + problem + ". Press Update From Data.");
            }
        }

        return problems;
    }

    public override void DrawExtras(Object asset)
    {
        if (IconField != null)
        {
            DrawIconPanel(asset, PlaceholderIconPainter.Guess(Label(asset), Title, ToolType.None, false, false));
        }

        DrawPrefabPanel(asset);
    }

    public override void DrawCreateOptions()
    {
        ContentUtility.BeginPanel("World Prefab", PrefabDescription);
        createPrefab = EditorGUILayout.ToggleLeft("Also create the prefab", createPrefab);
        using (new EditorGUI.DisabledScope(!createPrefab))
        {
            createModel = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("3D Model", "Drag a model or prefab from the Project window. Leave empty for a placeholder shape you can swap later."),
                createModel, typeof(GameObject), false);
        }

        if (createPrefab && createModel == null)
        {
            EditorGUILayout.LabelField("No model: a placeholder shape is used until you pick one.", ContentUtility.WrappedMiniLabel);
        }

        ContentUtility.EndPanel();
    }

    public override string ValidateCreate(ScriptableObject draft, string name)
    {
        return createPrefab ? ModelError(createModel) : null;
    }

    protected override void AfterCreate(ScriptableObject asset, bool withOptions)
    {
        if (withOptions && createPrefab)
        {
            GameObject model = createModel != null ? createModel : DefaultModel(asset);
            createModel = null;
            CreateAndLink(asset, model);
        }
    }

    public override void Dispose()
    {
        tools?.Dispose();
    }

    protected void DrawIconPanel(Object asset, IconShape suggestedShape)
    {
        ContentUtility.BeginPanel("Icon", "The picture shown in menus and inventories.");
        SerializedObject data = Host.Inspector.Get(asset);
        data.Update();
        SerializedProperty icon = data.FindProperty(IconField);

        using (new EditorGUILayout.HorizontalScope())
        {
            Rect box = GUILayoutUtility.GetRect(72f, 72f, GUILayout.Width(72f), GUILayout.Height(72f));
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.25f));
            ContentUtility.DrawSprite(new Rect(box.x + 4f, box.y + 4f, 64f, 64f), icon.objectReferenceValue as Sprite);

            using (new EditorGUILayout.VerticalScope())
            {
                icon.objectReferenceValue = EditorGUILayout.ObjectField(
                    new GUIContent("Icon", "The picture shown in menus and inventories."), icon.objectReferenceValue, typeof(Sprite), false);
                DrawGenerateIconButton(asset, icon);
                DrawPaintIconControls(asset, icon, null, suggestedShape);
            }
        }

        data.ApplyModifiedProperties();
        ContentUtility.EndPanel();
    }

    /// <summary>
    /// Paints a simple placeholder icon (a log, an ingot, a pickaxe...) and assigns it, for content without art yet.
    /// With no fixed color, a color picker is shown.
    /// </summary>
    protected void DrawPaintIconControls(Object asset, SerializedProperty icon, Color? fixedColor, IconShape suggestedShape)
    {
        if (paintOwner != asset)
        {
            paintOwner = asset;
            paintShape = suggestedShape;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            paintShape = (IconShape)EditorGUILayout.EnumPopup(paintShape, GUILayout.Width(90f));
            if (!fixedColor.HasValue)
            {
                paintColor = EditorGUILayout.ColorField(GUIContent.none, paintColor, false, false, false, GUILayout.Width(44f));
            }

            if (GUILayout.Button(new GUIContent("Paint Placeholder Icon", "Draws a simple icon of the chosen shape in this color and uses it.")))
            {
                Color color = fixedColor ?? paintColor;
                Sprite sprite = PlaceholderIconPainter.Paint(paintShape, color, Color.Lerp(color, Color.white, 0.4f),
                    $"{IconFolder}/{PrefabBuildUtility.MakeFileName(Label(asset))}.png");
                icon.objectReferenceValue = sprite;
                icon.serializedObject.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Host.RefreshList();
                GUIUtility.ExitGUI();
            }
        }
    }

    /// <summary>A button that photographs the prefab (or model) and assigns the result as the icon.</summary>
    protected void DrawGenerateIconButton(Object asset, SerializedProperty icon)
    {
        GameObject source = LinkedPrefab(asset);
        if (source == null)
        {
            source = DefaultModel(asset);
        }

        using (new EditorGUI.DisabledScope(source == null || IconFolder == null))
        {
            if (GUILayout.Button(new GUIContent("Generate Icon From 3D Model", "Takes a picture of the model and saves it as a new sprite.")))
            {
                try
                {
                    Sprite sprite = IconRenderer.RenderToSprite(source, $"{IconFolder}/{PrefabBuildUtility.MakeFileName(Label(asset))}.png");
                    icon.objectReferenceValue = sprite;
                    icon.serializedObject.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Host.RefreshList();
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                {
                    EditorUtility.DisplayDialog("Icon not created", exception.Message, "OK");
                }

                GUIUtility.ExitGUI();
            }
        }

        if (source == null)
        {
            EditorGUILayout.LabelField("Create the prefab or pick a 3D model first to generate an icon.", ContentUtility.WrappedMiniLabel);
        }
    }

    protected void DrawPrefabPanel(Object asset)
    {
        GameObject prefab = LinkedPrefab(asset);
        ContentUtility.BeginPanel("World Prefab", PrefabDescription);

        if (panelModelOwner != asset)
        {
            panelModelOwner = asset;
            panelModel = prefab == null ? DefaultModel(asset) : null;
        }

        if (prefab == null)
        {
            EditorGUILayout.LabelField("No prefab yet.", EditorStyles.miniBoldLabel);
            panelModel = ModelField(panelModel);
            string error = ModelError(panelModel);
            if (error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Create Prefab", GUILayout.Height(26f)))
                {
                    Host.Inspector.Get(asset).ApplyModifiedProperties();
                    CreateAndLink(asset, panelModel);
                    Host.RefreshList();
                    GUIUtility.ExitGUI();
                }
            }

            ContentUtility.EndPanel();
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            Tools.DrawPreview(prefab, 110f);
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(prefab.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(prefab), ContentUtility.WrappedMiniLabel);
                List<string> problems = PrefabProblems(prefab, asset);
                if (problems.Count > 0)
                {
                    EditorGUILayout.HelpBox("Out of date:\n• " + string.Join("\n• ", problems), MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("✓ Matches the settings above.", EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Update From Data", "Adds any missing parts and applies the settings above to the prefab.")))
                    {
                        Host.Inspector.Get(asset).ApplyModifiedProperties();
                        Run("Prefab not updated", () => UpdatePrefab(prefab, asset));
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Open Prefab"))
                    {
                        AssetDatabase.OpenAsset(prefab);
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Place in Scene", "Adds a copy where the Scene view is looking.")))
                    {
                        PrefabTools.PlaceInScene(prefab);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Show in Project"))
                    {
                        ContentUtility.Reveal(prefab);
                    }
                }
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Change the 3D model", EditorStyles.miniBoldLabel);
        if (!PrefabBuildUtility.HasModelChild(prefab))
        {
            EditorGUILayout.LabelField(
                "This prefab was made by an older tool and has no separate model. Delete it and create a new one to swap models here.",
                ContentUtility.WrappedMiniLabel);
        }
        else
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                panelModel = ModelField(panelModel);
                string error = panelModel == null ? "" : ModelError(panelModel);
                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button(new GUIContent("Replace Model",
                        "Swaps the model and refits the collider and health bar. Gameplay settings are kept."), GUILayout.Width(110f)))
                    {
                        GameObject model = panelModel;
                        Run("Model not replaced", () =>
                        {
                            PrefabBuildUtility.ReplaceModel(prefab, model, Placeholder);
                            OnModelUsed(asset, model);
                        });
                        panelModel = null;
                        GUIUtility.ExitGUI();
                    }
                }

                if (!string.IsNullOrEmpty(error))
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
        }

        EditorGUILayout.Space(4f);
        Tools.DrawComponents(prefab,
            "Fine-tune the prefab's parts directly. Changes are saved into the prefab. Update From Data re-applies the settings above, which wins where both set the same thing.");
        ContentUtility.EndPanel();
    }

    private void CreateAndLink(Object asset, GameObject model)
    {
        Run("Prefab not created", () =>
        {
            OnModelUsed(asset, model);
            GameObject prefab = BuildPrefab(asset, model);
            ContentUtility.SetReference(asset, PrefabField, prefab);
            AssetDatabase.SaveAssets();
        });
    }

    private string ModelError(GameObject model)
    {
        try
        {
            ValidateModel(model);
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    private static GameObject ModelField(GameObject model)
    {
        return (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("3D Model", "Drag a model or prefab from the Project window. Leave empty for a placeholder shape."),
            model, typeof(GameObject), false);
    }

    private void Run(string failureTitle, Action action)
    {
        try
        {
            action();
            AssetDatabase.SaveAssets();
        }
        catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
        {
            EditorUtility.DisplayDialog(failureTitle, exception.Message, "OK");
        }

        Host?.RefreshList();
    }
}
