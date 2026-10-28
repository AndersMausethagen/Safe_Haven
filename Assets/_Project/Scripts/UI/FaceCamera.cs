using UnityEngine;

/// <summary>
/// Keeps a world-space label (a name over someone's head) turned to the camera, so it reads the right way round
/// whichever way its owner faces.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/UI/Face Camera")]
public sealed class FaceCamera : MonoBehaviour
{
    private Camera view;

    private void LateUpdate()
    {
        view = view != null && view.isActiveAndEnabled ? view : Camera.main;
        if (view != null)
        {
            transform.rotation = view.transform.rotation;
        }
    }
}
