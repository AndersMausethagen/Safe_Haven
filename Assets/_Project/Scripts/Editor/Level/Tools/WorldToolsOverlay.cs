using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>The "Safe Haven World" panel in the Scene view (shown by default; hide or dock it from the Scene view's
/// overlay menu, the ⋮ or the ` key): the World Tools, Stick to Ground, dropping the selection on the ground, sculpting,
/// building the world and checking the scene, without leaving the Scene view.</summary>
[Overlay(typeof(SceneView), Id, "Safe Haven World", true)]
public sealed class WorldToolsOverlay : Overlay
{
    private const string Id = "safe-haven-world-tools";

    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement { style = { width = 220f } };
        root.Add(new IMGUIContainer(OnGUI));
        return root;
    }

    private static void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Place…", "Pick a prefab and click in the Scene view to place it on the ground")))
            {
                WorldToolsWindow.Open(WorldToolsWindow.Tab.Place);
            }

            if (GUILayout.Button(new GUIContent("Tools…", "All the world tools: build (rivers, paths, cliffs, forests, biomes), ground, water, plants, sky, scene")))
            {
                WorldToolsWindow.Open(WorldToolsWindow.Tab.Build);
            }
        }

        if (PrefabPlacer.Active != null && GUILayout.Button($"Stop placing {PrefabPlacer.Active.name}"))
        {
            PrefabPlacer.Stop();
        }

        PlacementSettings.StickToGround = EditorGUILayout.ToggleLeft(
            new GUIContent("Stick to ground", "Dragged-in, duplicated and moved objects settle on the ground"), PlacementSettings.StickToGround);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Drop", "Set the selected objects on the ground (End)")))
            {
                EditorApplication.ExecuteMenuItem("Safe Haven/World/Place/Drop Selection to Ground _END");
            }

            if (GUILayout.Button(new GUIContent("Drop + Align", "...tilted with the slope (Shift+End)")))
            {
                EditorApplication.ExecuteMenuItem("Safe Haven/World/Place/Drop and Align to Slope #_END");
            }

            if (GUILayout.Button(new GUIContent("Physics", "Let them fall and settle naturally, then remove the physics (Ctrl+End; Ctrl+Shift+End keeps them upright). A selected folder drops everything in it")))
            {
                EditorApplication.ExecuteMenuItem("Safe Haven/World/Place/Drop with Physics %_END");
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Biome Brush", "Paint a biome's ground, plants and props at once (meadow, forest floor, riverbank)")))
            {
                BiomeBrush.Open();
            }

            if (GUILayout.Button(new GUIContent("Sculpt", "Select the terrain in view: its Inspector has the sculpt and paint brushes")))
            {
                WorldToolsWindow.SelectTerrain();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Build World", "Rebuild the rivers, paths, cliffs and forests that changed")))
            {
                WorldBuilder.BuildChanged(false);
            }

            if (GUILayout.Button(new GUIContent("Check Scene", "Make it playable as a home or a raid, and see what is missing")))
            {
                WorldToolsWindow.Open(WorldToolsWindow.Tab.Scene);
            }
        }
    }
}
