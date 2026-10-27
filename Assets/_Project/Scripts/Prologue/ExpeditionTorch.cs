using UnityEngine;

/// <summary>Keeps the expedition's hand-mounted light upright while the hand follows locomotion and gathering.</summary>
[DisallowMultipleComponent]
public sealed class ExpeditionTorch : MonoBehaviour
{
    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
