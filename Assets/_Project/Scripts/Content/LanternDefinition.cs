using UnityEngine;

/// <summary>
/// A light worn on the belt (the Light slot of the character screen), so both hands stay free: how it shines, how long
/// it lasts and what refills it. An item with one is a lantern (ItemDefinition.Lantern); each lantern keeps how much
/// light it has left as its condition (ItemStack.LightHours). PlayerLantern hangs it at the hip and lights it (L).
/// Read-only at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Items/Lantern", fileName = "Lantern")]
public sealed class LanternDefinition : ScriptableObject
{
    [Tooltip("In-game hours of light when full. It burns only while lit.")]
    [SerializeField, Min(0.1f)] private float hours = 24f;
    [Tooltip("What refills it, one at a time from the bag (right-click the lantern): it is full again.")]
    [SerializeField] private ItemDefinition refill;

    [Header("Light")]
    [ColorUsage(false)]
    [SerializeField] private Color colour = new Color(0.62f, 1f, 0.55f);
    [SerializeField, Min(0.5f)] private float range = 7f;
    [SerializeField, Min(0f)] private float intensity = 1.4f;
    [Tooltip("How much the light breathes and flickers, as a share of its intensity (fireflies inside, not a flame).")]
    [SerializeField, Range(0f, 0.9f)] private float flicker = 0.35f;
    [Tooltip("How fast it breathes, in pulses a second.")]
    [SerializeField, Min(0.05f)] private float pulseSpeed = 1.1f;
    [Tooltip("How bright the glass glows while lit (emission, HDR).")]
    [SerializeField, Min(0f)] private float glow = 3f;

    [Header("On the belt")]
    [Tooltip("The lantern's model: a child named Glow is its lit glass, children named Mote are the fireflies inside, " +
             "a child Light is its light.")]
    [SerializeField] private GameObject model;
    [Tooltip("Where it hangs, from the hips, in the body's own directions (x right, y up, z forward), metres.")]
    [SerializeField] private Vector3 beltOffset = new Vector3(-0.21f, -0.06f, 0.04f);

    public float Hours => hours;
    public ItemDefinition Refill => refill;
    public Color Colour => colour;
    public float Range => range;
    public float Intensity => intensity;
    public float Flicker => flicker;
    public float PulseSpeed => pulseSpeed;
    public float Glow => glow;
    public GameObject Model => model;
    public Vector3 BeltOffset => beltOffset;

    /// <summary>The light's strength now, as a share of full: breathing slowly, with the odd bright blink of one firefly.</summary>
    public float Pulse(float time, float seed)
    {
        float breathe = Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f + seed) * 0.5f + 0.5f;
        float drift = Mathf.PerlinNoise(time * 0.7f + seed, seed * 3.1f);
        float blink = Mathf.Pow(Mathf.PerlinNoise(time * 2.3f, seed + 7.7f), 6f) * 2.5f;
        return Mathf.Clamp(1f - flicker + flicker * (0.55f * breathe + 0.45f * drift) + flicker * blink, 0f, 1.6f);
    }
}
