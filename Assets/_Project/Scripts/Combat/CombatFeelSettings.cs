using UnityEngine;

/// <summary>
/// How combat feels, in one asset designers can tune without code: hit stop, camera shake, hit flash, the squash punch
/// and knockback on targets, attack telegraphs and the dodge trail. Lives at Resources/CombatFeel and is read by
/// CombatFeedback, HitReaction, AttackTelegraph and PlayerDodge. Set a value to 0 to turn that effect off.
///
/// Where it belongs: Combat. Dodge distance, timing and cooldown are movement, so they live in PlayerMovementSettings.
/// </summary>
[CreateAssetMenu(fileName = "CombatFeel", menuName = "Safe Haven/Combat/Combat Feel")]
public sealed class CombatFeelSettings : ScriptableObject
{
    public const string ResourceName = "CombatFeel";

    [Header("Hit stop (seconds the game nearly freezes)")]
    [Tooltip("When the player's hit lands.")]
    [SerializeField, Min(0f)] private float hitStopOnHit = 0.04f;
    [Tooltip("When the player's hit is a critical hit.")]
    [SerializeField, Min(0f)] private float hitStopOnCrit = 0.08f;
    [Tooltip("When the player's hit kills.")]
    [SerializeField, Min(0f)] private float hitStopOnKill = 0.12f;
    [Tooltip("When the player is hurt.")]
    [SerializeField, Min(0f)] private float hitStopWhenHurt = 0.06f;
    [Tooltip("Game speed during a hit stop. 0 freezes completely.")]
    [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;

    [Header("Camera shake (0-1 strength)")]
    [SerializeField, Range(0f, 1f)] private float shakeOnHit = 0.15f;
    [SerializeField, Range(0f, 1f)] private float shakeOnCrit = 0.3f;
    [SerializeField, Range(0f, 1f)] private float shakeOnKill = 0.45f;
    [SerializeField, Range(0f, 1f)] private float shakeWhenHurt = 0.55f;
    [Tooltip("How far the camera moves at full strength, in meters.")]
    [SerializeField, Min(0f)] private float shakeMaxOffset = 0.35f;
    [Tooltip("How far the camera rolls at full strength, in degrees.")]
    [SerializeField, Min(0f)] private float shakeMaxRoll = 2.5f;
    [Tooltip("How fast the shake wobbles.")]
    [SerializeField, Min(0.1f)] private float shakeFrequency = 22f;
    [Tooltip("Strength lost per second, so shakes die out.")]
    [SerializeField, Min(0.1f)] private float shakeRecovery = 1.8f;

    [Header("Hit flash")]
    [Tooltip("Color a target flashes when hit. Values above 1 make it glow.")]
    [SerializeField, ColorUsage(false, true)] private Color flashColor = new Color(2.2f, 2.2f, 2.2f);
    [SerializeField, Min(0f)] private float flashSeconds = 0.08f;

    [Header("Hit punch (the target squashes and springs back)")]
    [Tooltip("How much the target squashes. 0.1 is 10%.")]
    [SerializeField, Range(0f, 0.5f)] private float punchAmount = 0.12f;
    [SerializeField, Min(0f)] private float punchSeconds = 0.16f;

    [Header("Knockback (monsters on the NavMesh)")]
    [Tooltip("Meters a monster is pushed by a normal hit.")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.35f;
    [Tooltip("Meters a monster is pushed by a critical or killing hit.")]
    [SerializeField, Min(0f)] private float knockbackDistanceCrit = 0.8f;
    [SerializeField, Min(0.01f)] private float knockbackSeconds = 0.12f;

    [Header("Attack telegraphs")]
    [Tooltip("Transparent unlit material for the ground markers. Its color is replaced by the colors below.")]
    [SerializeField] private Material telegraphMaterial;
    [Tooltip("The full area the attack will hit.")]
    [SerializeField] private Color telegraphAreaColor = new Color(1f, 0.25f, 0.15f, 0.18f);
    [Tooltip("Grows over the area as the attack winds up; when it fills the area, the hit lands.")]
    [SerializeField] private Color telegraphFillColor = new Color(1f, 0.3f, 0.1f, 0.45f);
    [Tooltip("Seconds the marker lingers, bright, after the hit lands.")]
    [SerializeField, Min(0f)] private float telegraphLinger = 0.1f;

    [Header("Dodge")]
    [Tooltip("Trail left behind while dodging. Uses the telegraph material.")]
    [SerializeField] private Gradient dodgeTrailColor = DefaultTrail();
    [SerializeField, Min(0f)] private float dodgeTrailWidth = 0.6f;
    [Tooltip("Text shown over the player when a dodge avoids a hit. Empty shows nothing.")]
    [SerializeField] private string dodgedText = "Dodged";
    [SerializeField] private Color dodgedTextColor = new Color(0.6f, 0.9f, 1f);

    private static CombatFeelSettings current;

    /// <summary>The settings from Resources/CombatFeel, or built-in defaults when the asset is missing.</summary>
    public static CombatFeelSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<CombatFeelSettings>(ResourceName);
                if (current == null)
                {
                    current = CreateInstance<CombatFeelSettings>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            return current;
        }
    }

    public float HitStopOnHit => hitStopOnHit;
    public float HitStopOnCrit => hitStopOnCrit;
    public float HitStopOnKill => hitStopOnKill;
    public float HitStopWhenHurt => hitStopWhenHurt;
    public float HitStopTimeScale => hitStopTimeScale;
    public float ShakeOnHit => shakeOnHit;
    public float ShakeOnCrit => shakeOnCrit;
    public float ShakeOnKill => shakeOnKill;
    public float ShakeWhenHurt => shakeWhenHurt;
    public float ShakeMaxOffset => shakeMaxOffset;
    public float ShakeMaxRoll => shakeMaxRoll;
    public float ShakeFrequency => shakeFrequency;
    public float ShakeRecovery => shakeRecovery;
    public Color FlashColor => flashColor;
    public float FlashSeconds => flashSeconds;
    public float PunchAmount => punchAmount;
    public float PunchSeconds => punchSeconds;
    public float KnockbackDistance => knockbackDistance;
    public float KnockbackDistanceCrit => knockbackDistanceCrit;
    public float KnockbackSeconds => knockbackSeconds;
    public Material TelegraphMaterial => telegraphMaterial;
    public Color TelegraphAreaColor => telegraphAreaColor;
    public Color TelegraphFillColor => telegraphFillColor;
    public float TelegraphLinger => telegraphLinger;
    public Gradient DodgeTrailColor => dodgeTrailColor;
    public float DodgeTrailWidth => dodgeTrailWidth;
    public string DodgedText => dodgedText;
    public Color DodgedTextColor => dodgedTextColor;

    private static Gradient DefaultTrail()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
