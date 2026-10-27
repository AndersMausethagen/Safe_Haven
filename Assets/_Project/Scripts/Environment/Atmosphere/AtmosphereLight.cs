using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a point light light the volumetric fog as well as the ground: a torch's glow hanging in the mist, a beacon's
/// halo. Reads colour, intensity and range from the Light beside it every frame, so flicker carries over. The fog
/// shades the nearest few (AtmosphereFeature.MaxLights) to the camera.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class AtmosphereLight : MonoBehaviour
{
    private static readonly List<AtmosphereLight> active = new List<AtmosphereLight>();

    [Tooltip("Multiplies how strongly this light glows in the fog.")]
    [SerializeField, Min(0f)] private float fogStrength = 1f;

    private Light source;

    public static IReadOnlyList<AtmosphereLight> Active => active;

    /// <summary>Colour × intensity × fog strength, linear; black while the light is off.</summary>
    public Color FogColor => source != null && source.isActiveAndEnabled
        ? source.color.linear * (source.intensity * fogStrength)
        : Color.black;

    public float Range => source != null ? source.range : 0f;

    private void OnEnable()
    {
        source = GetComponent<Light>();
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }
}
