using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainMist))]
public sealed class TerrainMistEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Independent ground clouds. Weather Only Atmosphere on Weather (default) uses only Weather Amount; Base and Golden Hour amounts are ignored. Disable it for authored constant/dawn mist. Preview in Game view.", MessageType.Info);
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Wave Amplitude, Length and Speed create calm rolling mist. Lowland Boost gathers it in hollows; Water Boost gathers it above water surfaces. Rebuild the ground/water map after terrain or water edits. Fog remains stationary and independent.", MessageType.Info);

        FogGround ground = FogGround.Current;
        if (ground == null || ground.Texture == null)
        {
            EditorGUILayout.HelpBox("Mist needs Fog Ground. Use the setup menu below to add the ground sampler and renderer feature.", MessageType.Warning);
        }

        if (GUILayout.Button("Ensure Mist Rendering Setup"))
        {
            Selection.activeGameObject = ((TerrainMist)target).gameObject;
            EditorApplication.ExecuteMenuItem("Safe Haven/World/Sky and Weather/Fog/Add Drifting Mist to Selected Terrain");
        }

        using (new EditorGUI.DisabledScope(ground == null))
        {
            if (GUILayout.Button("Rebake Mist Ground and Water Map"))
            {
                ground.Bake();
            }
        }

        if (TerrainMist.Active.Count > AtmosphereFeature.MaxMists)
        {
            EditorGUILayout.HelpBox("Only the four nearest mist terrain tiles are rendered at once.", MessageType.Warning);
        }
    }
}
