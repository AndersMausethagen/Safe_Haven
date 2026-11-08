using UnityEngine;

/// <summary>
/// What a place's fireflies are like: their colour and how many are about by day and by night, how many gather round
/// lights, and the few that can be caught for a lantern. Green over the Haven, blue in the Misty Woods. Read-only at
/// runtime; <see cref="Fireflies"/> flies them and <see cref="CatchableFireflies"/> the ones that can be caught.
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

    [Header("Drawn to light")]
    [Tooltip("Share of new ones that appear round a fire, a lamp or the player's lantern when one is near.")]
    [SerializeField, Range(0f, 1f)] private float lightShare = 0.2f;
    [Tooltip("How far from a light those gather, metres, nearest and furthest.")]
    [SerializeField] private Vector2 lightDistance = new Vector2(1.2f, 5f);

    [Header("Caught")]
    [Tooltip("What catching one gives (Interact when close). Empty: none can be caught here.")]
    [SerializeField] private ItemDefinition catchable;
    [Tooltip("How many catchable ones are about at full night, and by day.")]
    [SerializeField, Min(0)] private int catchableAtNight = 3;
    [SerializeField, Min(0)] private int catchableByDay;
    [Tooltip("Share of them that come near the player rather than gather at a light.")]
    [SerializeField, Range(0f, 1f)] private float nearPlayerShare = 0.35f;
    [Tooltip("Seconds one stays before it flies off, shortest and longest.")]
    [SerializeField] private Vector2 catchableLife = new Vector2(45f, 90f);
    [Tooltip("Their glow: a small bright speck (HDR unlit material; its colour is set to the profile's).")]
    [SerializeField] private Material catchableGlow;

    public Color Colour => colour;
    public float Radius => radius;
    public Vector2 Height => height;
    public Vector2 Size => size;
    public Vector2 Life => life;
    public float Wander => wander;
    public int MostAtOnce => Mathf.Max(nightCount, dayCount);
    public float LightShare => lightShare;
    public Vector2 LightDistance => lightDistance;
    public ItemDefinition Catchable => catchable;
    public float NearPlayerShare => nearPlayerShare;
    public Vector2 CatchableLife => catchableLife;
    public Material CatchableGlow => catchableGlow;

    /// <summary>How many catchable ones should be about: like the swarm, more the darker it is and fewer in rain.</summary>
    public int CatchableFor(float night, float rain)
    {
        float count = Mathf.Lerp(catchableByDay, catchableAtNight, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(night)));
        return Mathf.RoundToInt(count * (1f - rainShyness * Mathf.Clamp01(rain)));
    }

    /// <summary>How many should be flying: more the darker it is, fewer the harder it rains.</summary>
    public int CountFor(float night, float rain)
    {
        float count = Mathf.Lerp(dayCount, nightCount, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(night)));
        return Mathf.RoundToInt(count * (1f - rainShyness * Mathf.Clamp01(rain)));
    }
}
