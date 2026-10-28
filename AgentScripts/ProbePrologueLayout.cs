using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Where the prologue's key points are (`unity command run_script --file AgentScripts/ProbePrologueLayout.cs --entry ProbePrologueLayout.Report`). Opens the prologue, reports, reopens the previous scene.</summary>
public static class ProbePrologueLayout
{
    public static string Report()
    {
        string previous = SceneManager.GetActiveScene().path;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity");
        var director = Object.FindAnyObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        var data = new SerializedObject(director);
        Transform start = (Transform)data.FindProperty("startPoint").objectReferenceValue;
        string near = string.Join(", ", Object.FindObjectsByType<Transform>()
            .Where(t => t.parent == null || t.parent.parent == null)
            .Where(t => Vector3.Distance(t.position, start.position) < 8f)
            .Select(t => $"{t.name}{t.position:F0}"));
        string nav = string.Join(" ", new[] { new Vector3(0, 0, -65), new Vector3(0, 0, -78), new Vector3(-25, 0, -36), new Vector3(-40, 0, -45), new Vector3(30, 0, -65) }
            .Select(p => NavMesh.SamplePosition(p + Vector3.up * 5f, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position.ToString("F0") : $"none@{p:F0}"));
        string roots = string.Join(", ", SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name));
        string companions = string.Join(", ", Object.FindObjectsByType<PrologueCompanion>().Select(c => $"{c.name}{c.transform.position:F0}"));
        EditorSceneManager.OpenScene(previous);
        return $"near start: {near}\nnav: {nav}\ncompanions: {companions}\nroots: {roots}";
    }
}
