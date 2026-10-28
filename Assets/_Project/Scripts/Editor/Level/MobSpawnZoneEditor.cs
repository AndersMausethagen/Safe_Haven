using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a Mob Spawn Zone in the Scene view and lets the designer shape it: drag a corner to move it (it follows the
/// ground), Shift-click near an edge to add a corner there, Ctrl-click a corner to remove it (three stay). The Inspector
/// adds Drop Corners to Ground and, in Play mode, Spawn Now / Despawn for trying it out.
/// </summary>
[CustomEditor(typeof(MobSpawnZone))]
public sealed class MobSpawnZoneEditor : Editor
{
    private static readonly Color Fill = new Color(1f, 0.3f, 0.2f, 0.12f);
    private static readonly Color Edge = new Color(1f, 0.35f, 0.25f, 0.95f);

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Scene view: drag the corners; Shift-click near an edge to add a corner, Ctrl-click a corner to remove it.", MessageType.Info);
        DrawDefaultInspector();
        var zone = (MobSpawnZone)target;
        EditorGUILayout.LabelField("Area", $"{ZonePolygon.Area(zone.WorldCorners):0} m², {zone.Entries.Count} kind(s) of creature");
        foreach (SpawnTable.Entry entry in zone.Entries)
        {
            if (entry != null && entry.prefab != null && entry.prefab.GetComponentInChildren<Damageable>(true) == null)
            {
                EditorGUILayout.HelpBox($"{entry.prefab.name} can't be killed (no Damageable), so the zone is never cleared. " +
                                        "Pick the creature prefab, not its model.", MessageType.Warning);
            }
        }
        if (GUILayout.Button("Drop Corners to Ground"))
        {
            Undo.RecordObject(zone, "Drop Zone Corners");
            var dropped = new List<Vector3>();
            foreach (Vector3 corner in zone.Corners)
            {
                dropped.Add(zone.transform.InverseTransformPoint(Ground(zone.transform.TransformPoint(corner))));
            }

            zone.SetCorners(dropped);
        }

        if (Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn Now"))
                {
                    zone.Spawn();
                }

                if (GUILayout.Button("Despawn"))
                {
                    zone.Despawn();
                }
            }

            EditorGUILayout.LabelField("Alive", zone.AliveCount.ToString());
        }
    }

    private void OnSceneGUI()
    {
        var zone = (MobSpawnZone)target;
        var corners = new List<Vector3>(zone.Corners);
        var world = new List<Vector3>(zone.WorldCorners);
        if (world.Count >= 3)
        {
            Handles.color = Fill;
            Vector3 centre = Vector3.zero;
            foreach (Vector3 corner in world)
            {
                centre += corner;
            }

            centre /= world.Count;
            for (int i = 0, j = world.Count - 1; i < world.Count; j = i++)
            {
                Handles.DrawAAConvexPolygon(centre, world[j], world[i]);
            }
        }

        Handles.color = Edge;
        Handles.DrawAAPolyLine(4f, Closed(world));

        Event current = Event.current;
        for (int i = 0; i < world.Count; i++)
        {
            float size = HandleUtility.GetHandleSize(world[i]) * 0.09f;
            if (current.control && current.type == EventType.MouseDown && current.button == 0 && corners.Count > 3
                && HandleUtility.DistanceToCircle(world[i], size) < 5f)
            {
                Undo.RecordObject(zone, "Remove Zone Corner");
                corners.RemoveAt(i);
                zone.SetCorners(corners);
                current.Use();
                return;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world[i], size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Move Zone Corner");
                corners[i] = zone.transform.InverseTransformPoint(Ground(moved));
                zone.SetCorners(corners);
            }
        }

        if (current.shift && current.type == EventType.MouseDown && current.button == 0 && world.Count >= 2)
        {
            int edge = NearestEdge(world, out float distance);
            if (distance < 12f && TryMouseOnGround(out Vector3 point))
            {
                Undo.RecordObject(zone, "Add Zone Corner");
                corners.Insert(edge + 1, zone.transform.InverseTransformPoint(point));
                zone.SetCorners(corners);
                current.Use();
            }
        }

        if (current.shift || current.control)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private static Vector3[] Closed(List<Vector3> points)
    {
        var closed = new Vector3[points.Count + 1];
        for (int i = 0; i < points.Count; i++)
        {
            closed[i] = points[i] + Vector3.up * 0.05f;
        }

        if (points.Count > 0)
        {
            closed[points.Count] = closed[0];
        }

        return closed;
    }

    // The edge (index of its first corner) nearest the mouse, in screen pixels.
    private static int NearestEdge(List<Vector3> world, out float distance)
    {
        int best = 0;
        distance = float.MaxValue;
        for (int i = 0; i < world.Count; i++)
        {
            float d = HandleUtility.DistanceToLine(world[i], world[(i + 1) % world.Count]);
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }

        return best;
    }

    private static bool TryMouseOnGround(out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        point = default;
        return false;
    }

    private static Vector3 Ground(Vector3 point)
    {
        return Physics.Raycast(point + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point
            : point;
    }
}
