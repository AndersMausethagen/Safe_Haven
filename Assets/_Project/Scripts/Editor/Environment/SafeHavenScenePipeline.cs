using UnityEditor.SceneTemplate;
using UnityEngine.SceneManagement;

/// <summary>
/// What the Safe Haven Scene template (Scenes/Templates) does to each scene made from it: adds the weather rig, mist and
/// light shafts (<see cref="SceneEnvironmentDefaults"/>). Built and kept up to date by <see cref="SafeHavenSceneTemplate"/>.
/// </summary>
public sealed class SafeHavenScenePipeline : SceneTemplatePipelineAdapter
{
    public override void AfterTemplateInstantiation(SceneTemplateAsset sceneTemplateAsset, Scene scene, bool isAdditive, string sceneName)
    {
        SceneEnvironmentDefaults.AddTo(scene);
    }
}
