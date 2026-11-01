using UnityEngine;

/// <summary>
/// What a place's fireflies are like: their colour and how many are about by day and by night. Green over the Haven, blue
/// in the Misty Woods. Read-only at runtime; <see cref="Fireflies"/> flies them.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Environment/Firefly Profile", fileName = "Fireflies")]
public sealed class FireflyProfile : ScriptableObject
{
    [ColorUsage(false)]
    [SerializeField] private Color colour = new Color(0.55f, 1f, 0.3f);
    [Tooltip("How many fly around the camera at full night.")]
    [SerializeField, Min(0)] private int nightCount = 130;
    [Tooltip("How many stay out in full daylight.")]
    [SerializeField, Min(0)] private int dayCount = 8;
    [Tooltip("Metres around the middle of the view that they appear in.")]
    [SerializeField, Min(1f)] private float radius = 26f;
    [Tooltip("Metres above the ground they fly at, lowest and highest.")]
    [SerializeField] private Vector2 height = new Vector2(0.4f, 2.8f);
    [Tooltip("Size of the glow in metres, smallest and largest.")]
    [SerializeField] private Vector2 size = new Vector2(0.22f, 0.4f);
    [Tooltip("Seconds each one lives, shortest and longest. Different lives keep them from blinking together.")]
    [SerializeField] private Vector2 life = new Vector2(6f, 12f);
    [Tooltip("How far they wander. Higher is more restless.")]
    [SerializeField, Min(0f)] private float wander = 0.7f;
    [Tooltip("Share of them that shelter in a downpour.")]
    [SerializeField, Range(0f, 1f)] private float rainShyness = 0.85f;

    public Color Colour => colour;
    public float Radius => radius;
    public Vector2 Height => height;
    public Vector2 Size => size;
    public Vector2 Life => life;
    public float Wander => wander;
    public int MostAtOnce => Mathf.Max(nightCount, dayCount);

    /// <summary>How many should be flying: more the darker it is, fewer the harder it rains.</summary>
    public int CountFor(float night, float rain)
    {
        float count = Mathf.Lerp(dayCount, nightCount, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(night)));
        return Mathf.RoundToInt(count * (1f - rainShyness * Mathf.Clamp01(rain)));
    }
}
