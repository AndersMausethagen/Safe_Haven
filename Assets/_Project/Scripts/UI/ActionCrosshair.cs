using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dot in the middle of the screen that aims while the action camera is active. Hidden in the top-down style,
/// where the mouse cursor aims, and while a window or the pause menu has the cursor.
/// </summary>
public sealed class ActionCrosshair : MonoBehaviour
{
    [SerializeField] private Graphic graphic;

    private void LateUpdate()
    {
        if (graphic == null)
        {
            return;
        }

        UIStateManager ui = UIStateManager.Current;
        bool show = CameraPointer.UsesScreenCenter && !GamePause.HidesHud && (ui == null || !ui.IsAnyWindowOpen);
        if (graphic.enabled != show)
        {
            graphic.enabled = show;
        }
    }
}
