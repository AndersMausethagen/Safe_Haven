using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Places the Old Moss Troll and its patrol round in the open Raid_MistyWoods scene, under Map Content so they turn with
/// the map (run with `unity command run_script --file AgentScripts/PlaceOldMossTroll.cs --entry PlaceOldMossTroll.Place`).
/// The round loops around the Hollow Oak through the map's patrol markers. Undoable; re-running replaces them.
/// </summary>
public static class PlaceOldMossTroll
{
    private const string Prefab = "Assets/_Project/Prefabs/Enemies/OldMossTroll.prefab";
    private static readonly Vector2[] Waypoints =
    {
        new Vector2(-22f, 31f),
        new Vector2(0.2f, -4.9f),
        new Vector2(27.9f, 8f),
        new Vector2(58f, 13.9f),
    };

    public static string Place()
    {
        var content = GameObject.Find("Raid Map/Map Content");
        if (content == null)
        {
            return "no Raid Map/Map Content in the open scene";
        }

        Transform old = content.transform.Find("Apex");
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var apex = new GameObject("Apex");
        Undo.RegisterCreatedObjectUndo(apex, "Place Old Moss Troll");
        apex.transform.SetParent(content.transform, false);

        var route = new GameObject("Old Moss Troll Round");
        Undo.RegisterCreatedObjectUndo(route, "Place Old Moss Troll");
        route.transform.SetParent(apex.transform, false);
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < Waypoints.Length; i++)
        {
            Vector3 wanted = new Vector3(Waypoints[i].x, 50f, Waypoints[i].y);
            Vector3 ground = Physics.Raycast(wanted, Vector3.down, out RaycastHit hit, 100f, 1, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(wanted.x, 0f, wanted.z);
            bool onMesh = NavMesh.SamplePosition(ground, out NavMeshHit navHit, 6f, NavMesh.AllAreas);
            var point = new GameObject($"Waypoint {i + 1:00}");
            Undo.RegisterCreatedObjectUndo(point, "Place Old Moss Troll");
            point.transform.SetParent(route.transform, false);
            point.transform.position = onMesh ? navHit.position : ground;
            log.Append($"wp{i + 1}={point.transform.position} navmesh={onMesh}; ");
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var troll = (GameObject)PrefabUtility.InstantiatePrefab(prefab, apex.scene);
        Undo.RegisterCreatedObjectUndo(troll, "Place Old Moss Troll");
        troll.transform.SetParent(apex.transform, true);
        troll.transform.position = route.transform.GetChild(0).position;
        troll.transform.rotation = Quaternion.LookRotation(route.transform.GetChild(1).position - troll.transform.position);
        var patrol = Undo.AddComponent<MonsterPatrol>(troll);
        patrol.SetRoute(route.transform);
        EditorUtility.SetDirty(patrol);
        PrefabUtility.RecordPrefabInstancePropertyModifications(patrol);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(apex.scene);
        return log + $"troll at {troll.transform.position}, route={patrol.Route != null}";
    }
}
