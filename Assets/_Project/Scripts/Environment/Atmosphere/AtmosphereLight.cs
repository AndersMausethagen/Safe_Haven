using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a point light light the volumetric fog as well as the ground: a torch's glow hanging in the mist, a beacon's
/// halo. Reads colour, intensity and range from the Light beside it every frame, so flicker carries over. The fog
/// shades the nearest few (AtmosphereFeature.MaxLights) to the camera. Point lights can also clear the participating
/// fog and terrain mist inside a soft sphere around the flame.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class AtmosphereLight : MonoBehaviour
{
    private static readonly List<AtmosphereLight> active = new List<AtmosphereLight>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [Tooltip("Multiplies how strongly this light glows in the fog.")]
    [SerializeField, Min(0f)] private float fogStrength = 1f;
    [Tooltip("Radius in metres where this point light pushes back fog, terrain mist and shaft particles. Limited to the Light's range. Zero disables clearing.")]
    [SerializeField, Min(0f)] private float clearingRadius = 4f;
    [Tooltip("Fraction of fog removed at the centre. Independent of Fog Strength; follows the Light's on/off state and fades out below intensity 1.")]
    [SerializeField, Range(0f, 1f)] private float clearingStrength = 1f;
    [Tooltip("Fraction of the clearing radius used for its soft outer edge.")]
    [SerializeField, Range(0.05f, 1f)] private float clearingSoftness = 0.6f;

    private Light source;

    public static IReadOnlyList<AtmosphereLight> Active => active;

    /// <summary>Colour × intensity × fog strength, linear; black while the light is off.</summary>
    public Color FogColor => source != null && source.isActiveAndEnabled
        ? source.color.linear * (source.intensity * fogStrength)
        : Color.black;

    public float Range => source != null ? source.range : 0f;
    public float ClearingRadius => Mathf.Min(Mathf.Max(0f, clearingRadius), Mathf.Max(0f, Range));
    public float ClearingStrength => source != null && source.isActiveAndEnabled && source.type == LightType.Point
        && source.color.maxColorComponent > 0f
        ? Mathf.Clamp01(clearingStrength) * Mathf.Clamp01(source.intensity)
        : 0f;
    public float ClearingSoftness => Mathf.Clamp(clearingSoftness, 0.05f, 1f);

    /// <summary>Set up from code (WorldLights gives a fire that has none its glow).</summary>
    public void Configure(float strength, float clearing)
    {
        fogStrength = Mathf.Max(0f, strength);
        clearingRadius = Mathf.Max(0f, clearing);
    }

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
