using UnityEngine;

/// <summary>
/// Loot left on the body (the user, 2026-11-24: "killing enemies is gonna change so we have to go to the killed monster and loot it. a
/// window with the loot shows. The monster will glow faintly and have some glittering coming off the body"). A creature's kill no longer
/// goes straight into the bag: what it carried waits on its body (LootableRemains), which glows faintly and glitters until it is looted
/// in the loot window (LootWindow). Gathering, breakables and caches still hand things over at once. Resources/BodyLootSettings,
/// Systems Editor › World › Loot on Bodies.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Body Loot Settings", fileName = "BodyLootSettings")]
public sealed class BodyLootSettings : GameSettings<BodyLootSettings>
{
    [Tooltip("On: a creature's loot waits on its body. Off: straight into the killer's bag, as before 2026-11-24.")]
    [SerializeField] private bool lootFromBodies = true;
    [Tooltip("How long a body keeps its loot, in seconds. Then it fades with what it held; a looted body sinks as any corpse.")]
    [SerializeField, Min(10f)] private float lootableSeconds = 600f;
    [Tooltip("How close the player must be to loot it, in metres, measured to its loot point.")]
    [SerializeField, Min(0.5f)] private float reach = 2.2f;
    [Tooltip("Walking this far from it closes the loot window.")]
    [SerializeField, Min(1f)] private float windowReach = 4f;
    [Tooltip("The faint outline round a body with loot (the hover outline's shader, in this colour and strength).")]
    [SerializeField] private Color glow = new Color(1f, 0.84f, 0.45f, 0.85f);
    [Tooltip("The outline's width in screen pixels.")]
    [SerializeField, Min(0.5f)] private float glowPixels = 2f;
    [Tooltip("The glow breathes between this share of its colour's strength and the whole of it, over Glow Breath Seconds.")]
    [SerializeField, Range(0f, 1f)] private float glowLeast = 0.35f;
    [SerializeField, Min(0.2f)] private float glowBreathSeconds = 2.4f;
    [Tooltip("The glitter rising off it: a particle material (additive, soft).")]
    [SerializeField] private Material sparkleMaterial;
    [SerializeField] private Color sparkleColour = new Color(1f, 0.88f, 0.55f, 0.9f);
    [Tooltip("Glints a second from a body, at most: a big body glitters more, up to this.")]
    [SerializeField, Min(0f)] private float sparklesPerSecond = 9f;
    [SerializeField] private Vector2 sparkleSize = new Vector2(0.09f, 0.18f);
    [Tooltip("How long a glint lives, and how fast it drifts up, in m/s.")]
    [SerializeField, Min(0.1f)] private float sparkleSeconds = 1.3f;
    [SerializeField, Min(0f)] private float sparkleRise = 0.35f;

    public bool LootFromBodies => lootFromBodies;
    public float LootableSeconds => lootableSeconds;
    public float Reach => reach;
    public float WindowReach => windowReach;
    public Color Glow => glow;
    public float GlowPixels => glowPixels;

    /// <summary>The glow's colour at <paramref name="time"/>: breathing between Glow Least and the whole of it.</summary>
    public Color GlowAt(float time)
    {
        float breath = 0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2f / glowBreathSeconds);
        Color now = glow;
        now.a = glow.a * Mathf.Lerp(glowLeast, 1f, breath);
        return now;
    }
    public Material SparkleMaterial => sparkleMaterial;
    public Color SparkleColour => sparkleColour;
    public float SparklesPerSecond => sparklesPerSecond;
    public Vector2 SparkleSize => sparkleSize;
    public float SparkleSeconds => sparkleSeconds;
    public float SparkleRise => sparkleRise;
}
