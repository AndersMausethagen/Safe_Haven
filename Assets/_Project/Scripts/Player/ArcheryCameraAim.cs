using UnityEngine;

/// <summary>
/// Tells the camera about the bow (PlayerCameraController): with one in hand the framing holds still, so drawing doesn't
/// zoom or swing the view under the aim, and while aiming or drawing the right button doesn't turn it. It reaches the scene's
/// camera rig through PlayerCameraController.Current, so the player prefab needs no reference to it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerArchery))]
public sealed class ArcheryCameraAim : MonoBehaviour
{
    private PlayerArchery archery;
    private PlayerCameraController view;
    private bool sentAiming;
    private bool sentBow;

    private void Awake() => archery = GetComponent<PlayerArchery>();

    private void OnDisable() => Send(false, false);

    private void LateUpdate() => Send(archery.IsAiming || archery.IsDrawing, archery.Bow != null);

    private void Send(bool aiming, bool bow)
    {
        PlayerCameraController current = PlayerCameraController.Current;
        if (current != view)
        {
            // A new scene's camera has heard nothing yet.
            view = current;
            sentAiming = false;
            sentBow = false;
        }

        if (view == null)
        {
            return;
        }

        if (aiming != sentAiming)
        {
            sentAiming = aiming;
            view.SetAiming(aiming);
        }

        if (bow != sentBow)
        {
            sentBow = bow;
            view.SetBowInHand(bow);
        }
    }
}
