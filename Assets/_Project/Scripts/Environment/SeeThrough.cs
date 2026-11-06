using UnityEngine;

/// <summary>
/// See-through occluders, in the way of Gord: trees, roofs and rocks that stand between the camera and the player
/// dissolve in a soft, dithered circle around the player. The shaders do the cutting (Shaders/SeeThrough/SeeThrough.hlsl
/// in Tree Wind, Layered Lit and the See-Through copies of URP Lit and Unlit); this component, on the player, tells them
/// where the player is every frame. Shadows stay whole, and nothing behind the player is touched. Materials that should
/// dissolve use one of those shaders (Safe Haven › Setup › Rendering › Make Materials See-Through).
/// </summary>
[DisallowMultipleComponent]
public sealed class SeeThrough : MonoBehaviour
{
    private static readonly int TargetId = Shader.PropertyToID("_SeeThroughTarget");
    private static readonly int ParamsId = Shader.PropertyToID("_SeeThroughParams");
    private static readonly int CanopyId = Shader.PropertyToID("_SeeThroughCanopy");

    [Tooltip("Height above the player's feet that is kept in view (about the chest).")]
    [SerializeField, Min(0f)] private float targetHeight = 1.2f;
    [Tooltip("Radius of the see-through circle at the player, in metres.")]
    [SerializeField, Min(0.1f)] private float radius = 3.5f;
    [Tooltip("Radius near the camera. Small keeps the circle the same size on screen however close the occluder is.")]
    [SerializeField, Min(0f)] private float radiusAtCamera = 1.2f;
    [Tooltip("Share of the radius over which the edge dissolves in a stipple.")]
    [SerializeField, Range(0.05f, 1f)] private float softness = 0.55f;
    [Tooltip("Nothing closer to the player than this (along the view) is cut, so the player and what they hold stay solid.")]
    [SerializeField, Min(0f)] private float margin = 1.6f;
    [Header("Canopy")]
    [Tooltip("Leaves above the crown start whose line of sight lands within this many metres of the player stipple away, so a " +
             "dense wood never roofs over the fight (0 = off). Bark, undergrowth and shadows stay.")]
    [SerializeField, Min(0f)] private float canopyRadius = 14f;
    [Tooltip("Height above the player's feet where crowns begin: lower leaves (undergrowth, saplings) stay.")]
    [SerializeField, Min(0f)] private float crownStart = 2.5f;
    [Tooltip("Metres over which the canopy's edge stipples back in.")]
    [SerializeField, Min(0.1f)] private float canopySoftness = 5f;
    [Tooltip("Seconds to fade the effect in or out when it is switched (Enabled).")]
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;

    private float strength;

    /// <summary>Switch the effect off for a while (a cutscene, the build camera); it fades.</summary>
    public bool Active { get; set; } = true;

    private void OnDisable()
    {
        strength = 0f;
        Shader.SetGlobalVector(TargetId, Vector4.zero);
    }

    private void LateUpdate()
    {
        strength = Mathf.MoveTowards(strength, Active ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
        Vector3 target = transform.position + Vector3.up * targetHeight;
        Shader.SetGlobalVector(TargetId, new Vector4(target.x, target.y, target.z, strength));
        Shader.SetGlobalVector(ParamsId, new Vector4(radius, softness, margin, radiusAtCamera));
        Shader.SetGlobalVector(CanopyId, new Vector4(canopyRadius, crownStart, canopySoftness, transform.position.y));
    }
}
