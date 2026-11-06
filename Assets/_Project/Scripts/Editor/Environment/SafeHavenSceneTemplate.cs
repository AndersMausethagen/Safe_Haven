using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEngine.SceneManagement;

/// <summary>
/// The Safe Haven Scene template, pinned first in File › New Scene: a camera and a sun, completed by
/// <see cref="SafeHavenScenePipeline"/> with the weather rig, mist and light shafts. Unity's way to give new scenes a
/// starting point, so scenes made by code (tests, tools) are left alone.
/// </summary>
public static class SafeHavenSceneTemplate
{
    public const string Folder = "Assets/_Project/Scenes/Templates";
    public const string ScenePath = Folder + "/SafeHavenScene.unity";
    public const string TemplatePath = Folder + "/SafeHavenScene.scenetemplate";

    /// <summary>Makes the template and its scene, or completes them. Returns the template.</summary>
    [MenuItem("Safe Haven/Setup/World/Create Scene Template", false, 200)]
    public static SceneTemplateAsset Ensure()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Templates");

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        var template = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>(TemplatePath);
        if (template == null)
            template = SceneTemplateService.CreateTemplateFromScene(sceneAsset, TemplatePath);

        template.templateScene = sceneAsset;
        template.templateName = "Safe Haven Scene";
        template.description = "A game scene with a camera, a sun and the weather rig: day and night, weather, mist and light shafts.";
        template.templatePipeline = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/Editor/Environment/SafeHavenScenePipeline.cs");
        template.addToDefaults = true;
        EditorUtility.SetDirty(template);
        AssetDatabase.SaveAssetIfDirty(template);
        return template;
    }
}
