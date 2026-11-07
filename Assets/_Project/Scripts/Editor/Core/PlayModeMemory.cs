using System;
using UnityEditor;

/// <summary>
/// Gives back the memory a Play session used once it ends. The project enters Play Mode without a domain or scene
/// reload (fast iteration), so whatever a session loaded (raid scenes, generated maps, textures, pooled effects) stays
/// in the Editor until something unloads it: without this the Editor grew by about 2 GB per Play.
/// </summary>
[InitializeOnLoad]
public static class PlayModeMemory
{
    static PlayModeMemory()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        // Wait a frame so the play scenes' objects are gone, then drop the assets nothing uses any more.
        EditorApplication.delayCall += () =>
        {
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            GC.Collect();
        };
    }
}
