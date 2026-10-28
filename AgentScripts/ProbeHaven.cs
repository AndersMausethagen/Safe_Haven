using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>What stands in Safe Haven (`--entry ProbeHaven.Report`): the Heart Tree, buildings, nodes and roots. Opens the scene, reports, reopens the previous one.</summary>
public static class ProbeHaven
{
    public static string Report()
    {
        string previous = SceneManager.GetActiveScene().path;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        HeartTree tree = Object.FindAnyObjectByType<HeartTree>(FindObjectsInactive.Include);
        string buildings = string.Join(", ", Object.FindObjectsByType<BuildingObject>(FindObjectsInactive.Include).Select(b => $"{b.name}{b.transform.position:F0}"));
        string nodes = string.Join(", ", Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include).GroupBy(n => n.Definition != null ? n.Definition.name : "?").Select(g => $"{g.Key}×{g.Count()}"));
        string roots = string.Join(", ", SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name));
        Terrain t = Object.FindAnyObjectByType<Terrain>();
        string text = $"tree {tree.transform.position:F0} radius {tree.WardRadius}\nbuildings {buildings}\nnodes {nodes}\nterrain {t?.transform.position:F0} {t?.terrainData.size:F0}\nroots {roots}";
        if (previous != "Assets/_Project/Scenes/SafeHaven.unity") EditorSceneManager.OpenScene(previous);
        return text;
    }
}
