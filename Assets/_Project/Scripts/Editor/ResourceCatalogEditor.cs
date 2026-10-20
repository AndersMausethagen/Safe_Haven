using UnityEditor;
using UnityEngine;

/// <summary>Adds a button to the catalog that gathers every ResourceDefinition in the project into it.</summary>
[CustomEditor(typeof(ResourceCatalog))]
public sealed class ResourceCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Collect All Definitions"))
        {
            Undo.RecordObject(target, "Collect Resource Definitions");
            int added = ((ResourceCatalog)target).CollectAllDefinitions();
            Debug.Log(added == 0 ? "Resource catalog is already up to date." : $"Added {added} entr{(added == 1 ? "y" : "ies")} to the catalog.", target);
        }

        EditorGUILayout.HelpBox(
            "New Resource Definitions and Categories are added automatically. This button forces a scan, and also removes entries whose asset was deleted.",
            MessageType.Info);
    }
}
