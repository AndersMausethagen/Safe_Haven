using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lists Safe Haven's root objects with their components and what references a terrain (`--entry ProbeHavenRoots.Report`).</summary>
public static class ProbeHavenRoots
{
    public static string Report()
    {
        string previous = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        var lines = scene.GetRootGameObjects().Select(g => $"{g.name}{g.transform.position:F0} [{string.Join(",", g.GetComponents<Component>().Select(c => c.GetType().Name).Where(n => n != "Transform"))}] children {g.transform.childCount}").ToList();
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(behaviour);
            SerializedProperty it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference && (it.objectReferenceValue is Terrain || it.objectReferenceValue is TerrainData || it.objectReferenceValue is TerrainCollider))
                {
                    lines.Add($"REF {behaviour.GetType().Name} on {behaviour.name}.{it.propertyPath} -> {it.objectReferenceValue.name}");
                }
            }
        }

        if (previous != "Assets/_Project/Scenes/SafeHaven.unity") EditorSceneManager.OpenScene(previous);
        return string.Join("\n", lines);
    }
}
