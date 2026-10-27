using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lists the root objects of scenes and their notable components, opening them additively and closing them
/// again without saving (`unity command run_script --file AgentScripts/ProbeSceneRoots.cs --entry ProbeSceneRoots.List`).</summary>
public static class ProbeSceneRoots
{
    public static string List()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string path in new[] { "Assets/_Project/Scenes/SafeHaven.unity", "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity" })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            sb.Append("== " + path + "\n");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                string components = string.Join(",", root.GetComponents<Component>().Where(c => c != null && !(c is Transform))
                                                          .Select(c => c.GetType().Name).Take(8));
                string children = string.Join(",", root.transform.Cast<Transform>().Take(10).Select(t => t.name));
                sb.Append($"  {root.name} [{components}] children={root.transform.childCount}: {children}\n");
            }

            EditorSceneManager.CloseScene(scene, true);
        }

        sb.Append("build scenes: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : "(off)"))));
        return sb.ToString();
    }
}
