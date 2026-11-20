using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// The terrains take the Ground rendering layer in the Editor too (GroundDecals does it as scenes load in Play), so ground decals
/// show in the Scene view as they will in the game. Not saved into the scene by itself: nothing is marked dirty.
/// </summary>
[InitializeOnLoad]
public static class GroundDecalsInEditor
{
    static GroundDecalsInEditor()
    {
        EditorSceneManager.sceneOpened += (_, _) => GroundDecals.MarkTerrains();
        EditorApplication.delayCall += GroundDecals.MarkTerrains;
    }
}
