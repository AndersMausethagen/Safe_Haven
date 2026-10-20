using UnityEditor;
using UnityEngine;

/// <summary>
/// The RaidMap inspector: the Map Painter (zones, markers, heights; see RaidMapPainter) and the Bake button that rebuilds the
/// terrain paint, trees, rocks and spawn points from the painted layout.
/// </summary>
[CustomEditor(typeof(RaidMap))]
public sealed class RaidMapEditor : Editor
{
    private string lastReport;
    private RaidMapPainter painter;

    private void OnEnable()
    {
        painter = new RaidMapPainter((RaidMap)target);
    }

    private void OnDisable()
    {
        painter?.Dispose();
        painter = null;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var map = (RaidMap)target;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            painter.DrawInspector();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Bake rebuilds the terrain's texture paint, trees and rocks, the player start, the exits' places, " +
                                "and the points of every spawn group with a Site or a Zone, then rebakes the navmesh. Heights " +
                                "are left alone; hand-painted terrain textures and trees are replaced. Save the scene afterwards.",
                                MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying || map.Definition == null))
        {
            if (GUILayout.Button("Bake Map", GUILayout.Height(28f)))
            {
                lastReport = RaidMapBaker.Bake(map);
                Debug.Log(lastReport, map);
            }
        }

        if (!string.IsNullOrEmpty(lastReport))
        {
            EditorGUILayout.HelpBox(lastReport, MessageType.None);
        }
    }

    private void OnSceneGUI()
    {
        if (!Application.isPlaying) painter?.OnSceneGUI();
    }
}
