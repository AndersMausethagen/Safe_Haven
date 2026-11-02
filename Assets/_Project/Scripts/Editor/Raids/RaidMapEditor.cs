using UnityEditor;
using UnityEngine;

/// <summary>
/// The RaidMap inspector. Says first which raid plays this map and whether its layout is generated, as that decides what
/// matters here: a generated raid is tuned in the raid editor's World tab and only reads the definition's look, while a
/// painted one uses the Map Painter (zones, markers, heights; see RaidMapPainter) and the Bake button that rebuilds the terrain
/// paint, trees, rocks and spawn points.
/// </summary>
[CustomEditor(typeof(RaidMap))]
public sealed class RaidMapEditor : Editor
{
    private const string ReferencesKey = "SafeHaven.RaidMap.References";
    private const string PainterKey = "SafeHaven.RaidMap.Painter";

    private string lastReport;
    private RaidMapPainter painter;
    private RaidDefinition raid;

    private void OnEnable()
    {
        var map = (RaidMap)target;
        painter = new RaidMapPainter(map);
        raid = RaidSection.RaidPlaying(map.gameObject.scene.name);
    }

    private void OnDisable()
    {
        painter?.Dispose();
        painter = null;
    }

    public override void OnInspectorGUI()
    {
        var map = (RaidMap)target;
        bool generated = raid != null && raid.LayoutRules != null;
        DrawStatus(generated);

        if (Foldout(ReferencesKey, "Scene references and turning", false))
        {
            DrawDefaultInspector();
        }

        if (Foldout(PainterKey, generated ? "Painted layout (not used while the raid generates its map)" : "Painted layout", !generated))
        {
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
    }

    private void DrawStatus(bool generated)
    {
        string message = raid == null
            ? "No raid plays this scene yet. Point a raid at it on the Overview tab of Content Editor › Raids."
            : generated
                ? $"{raid.DisplayName} generates a new map from this one every run. Materials, trees and plants come from the map's definition and the layout from its rules: tune them all in the raid editor's World tab."
                : $"{raid.DisplayName} plays this painted map, turned 0–3 quarter turns. Paint and bake it below.";
        EditorGUILayout.HelpBox(message, MessageType.Info);
        if (raid != null && GUILayout.Button("Open in the Raid Editor", GUILayout.Height(24f)))
        {
            RaidSection.ShowWorld(raid);
        }

        EditorGUILayout.Space(2f);
    }

    private static bool Foldout(string key, string label, bool byDefault)
    {
        bool open = EditorPrefs.GetBool(key, byDefault);
        bool nowOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, label);
        EditorGUILayout.EndFoldoutHeaderGroup();
        if (nowOpen != open)
        {
            EditorPrefs.SetBool(key, nowOpen);
        }

        return nowOpen;
    }

    private void OnSceneGUI()
    {
        if (!Application.isPlaying) painter?.OnSceneGUI();
    }
}

/// <summary>A rules asset can be edited as it stands, but the raid editor shows its maps beside it, so the inspector offers the way there.</summary>
[CustomEditor(typeof(RaidLayoutRules))]
public sealed class RaidLayoutRulesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RaidDefinition raid = RaidSection.RaidUsing((RaidLayoutRules)target);
        if (raid != null && GUILayout.Button($"Edit in the Raid Editor ({raid.DisplayName})", GUILayout.Height(24f)))
        {
            RaidSection.ShowWorld(raid);
        }

        DrawDefaultInspector();
    }
}

/// <summary>The same for a map's definition, with zones and markers left to the Map Painter.</summary>
[CustomEditor(typeof(RaidMapDefinition))]
public sealed class RaidMapDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RaidDefinition raid = RaidSection.RaidOfMap((RaidMapDefinition)target);
        if (raid != null && GUILayout.Button($"Edit in the Raid Editor ({raid.DisplayName})", GUILayout.Height(24f)))
        {
            RaidSection.ShowWorld(raid);
        }

        DrawDefaultInspector();
    }
}
