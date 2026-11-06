using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Only the game's camera draws to the screen. A camera left in a scene by a tool or a test (a bare "Main Camera" at the
/// scene's root, say) renders after the game's own and covers it: the game then shows the ground from wherever that camera
/// stands. The game's camera calls <see cref="SwitchOff"/> as it starts; cameras drawing into a texture (portraits,
/// previews) and URP overlay cameras are left alone.
/// </summary>
public static class StrayCameras
{
    /// <summary>Switches off every other enabled camera drawing to <paramref name="keep"/>'s display; returns how many.</summary>
    public static int SwitchOff(Camera keep)
    {
        if (keep == null)
        {
            return 0;
        }

        int off = 0;
        foreach (Camera camera in Camera.allCameras)
        {
            if (camera == keep || camera.targetTexture != null || camera.targetDisplay != keep.targetDisplay)
            {
                continue;
            }

            if (camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Overlay)
            {
                continue;
            }

            camera.enabled = false;
            off++;
            Debug.LogWarning($"Switched off the camera '{PathOf(camera.transform)}' in {camera.gameObject.scene.name}: it drew over the game's camera. " +
                             "Delete it from the scene.", camera);
        }

        return off;
    }

    private static string PathOf(Transform transform) =>
        transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
}
