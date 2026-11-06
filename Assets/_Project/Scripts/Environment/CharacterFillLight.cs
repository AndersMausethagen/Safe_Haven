using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A soft light that lights characters only (the player, creatures), from the camera's side. In the Misty Woods the dark
/// litter and the dark figures on it were one value from the top-down camera, so a fight read as shapes in the murk; this
/// lifts the figures a little without touching the ground, the trees or the mood (URP rendering layers: Characters marks
/// a renderer into the layer, the light lights that layer alone). On the player; it follows them.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterFillLight : MonoBehaviour
{
    /// <summary>The rendering layer the fill light reaches (Light Layer 7: unused by anything else).</summary>
    public const uint CharacterLayer = 1u << 7;

    [Tooltip("Where the light hangs from the player: up and towards the camera, so figures are lit on the side seen.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -3f);
    [SerializeField, Min(0f)] private float intensity = 0.9f;
    [SerializeField, Min(1f)] private float range = 16f;
    [SerializeField] private Color colour = new Color(1f, 0.95f, 0.88f);

    private Light fill;

    /// <summary>Puts every renderer of a character into the fill light's layer (keeping its others).</summary>
    public static void Mark(GameObject character)
    {
        foreach (Renderer part in character.GetComponentsInChildren<Renderer>(true))
        {
            part.renderingLayerMask |= CharacterLayer;
        }
    }

    private void Awake()
    {
        Mark(gameObject);
        var holder = new GameObject("Character Fill Light");
        holder.transform.SetParent(transform, false);
        fill = holder.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = colour;
        fill.intensity = intensity;
        fill.range = range;
        fill.shadows = LightShadows.None;
        fill.renderingLayerMask = (int)CharacterLayer;
        var data = holder.AddComponent<UniversalAdditionalLightData>();
        data.renderingLayers = CharacterLayer;
    }

    private void LateUpdate()
    {
        // World-space offset: the camera looks north, so the light keeps to the camera's side however the player turns.
        fill.transform.position = transform.position + offset;
    }
}
