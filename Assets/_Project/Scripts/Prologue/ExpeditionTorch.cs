using UnityEngine;

/// <summary>Keeps the expedition's hand-mounted light upright while the hand follows locomotion and gathering.</summary>
[DisallowMultipleComponent]
public sealed class ExpeditionTorch : MonoBehaviour
{
    private void Awake()
    {
        AddAtmosphereLights();
    }

    /// <summary>Gives each point light an AtmosphereLight, once.</summary>
    internal void AddAtmosphereLights()
    {
        foreach (Light light in GetComponentsInChildren<Light>(true))
        {
            if (light.type == LightType.Point && light.GetComponent<AtmosphereLight>() == null)
                light.gameObject.AddComponent<AtmosphereLight>();
        }
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
