using UnityEngine;

/// <summary>
/// Tells the camera when the player aims a bow (holding guard with a bow, or drawing it), so the view narrows a little
/// and, top-down, leans further toward the cursor (PlayerCameraController.SetAiming). Finds the scene's camera rig
/// itself, so the player prefab needs no reference to it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerArchery))]
public sealed class ArcheryCameraAim : MonoBehaviour
{
    private PlayerArchery archery;
    private PlayerCameraController view;
    private bool sent;

    private void Awake() => archery = GetComponent<PlayerArchery>();

    private void OnDisable() => Send(false);

    private void LateUpdate() => Send(archery.IsAiming || archery.IsDrawing);

    private void Send(bool aiming)
    {
        if (view == null)
        {
            if (!aiming)
            {
                return;
            }

            view = FindAnyObjectByType<PlayerCameraController>();
            if (view == null)
            {
                return;
            }
        }

        if (aiming != sent)
        {
            sent = aiming;
            view.SetAiming(aiming);
        }
    }
}
