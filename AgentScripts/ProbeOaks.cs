using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Scale and collider of each generated oak in the open scene (`unity command run_script --file AgentScripts/ProbeOaks.cs --entry ProbeOaks.Report`).</summary>
public static class ProbeOaks
{
    public static string Report() => string.Join("; ", Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
        .Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("rich_"))
        .Select(t => $"{System.IO.Path.GetFileName(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))} s={t.lossyScale.y:F2} col={t.GetComponentInChildren<Collider>() != null}"));
}
