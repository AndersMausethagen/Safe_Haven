using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Lists a prefab's hierarchy with components (`unity command run_script --file AgentScripts/ProbePrefab.cs --entry ProbePrefab.Tree --args '["path"]'`).</summary>
public static class ProbePrefab
{
    public static string Tree(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return string.Join(" | ", root.GetComponentsInChildren<Transform>(true).Select(t =>
            $"{t.name}@{t.localPosition:F2}x{t.localScale.x:F2}[{string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name))}]"));
    }
}
