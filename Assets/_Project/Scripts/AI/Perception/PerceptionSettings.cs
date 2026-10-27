using UnityEngine;

/// <summary>
/// Game-wide rules for how monsters notice things and how the player stays unnoticed (Resources/PerceptionSettings,
/// Systems Editor › World › Senses & Stealth). A monster's own eyes and ears (sight range and cone, close sense,
/// hearing) are on its MonsterDefinition; these are the shared timings, the player's visibility and the noise every
/// action makes.
/// </summary>
[CreateAssetMenu(fileName = "PerceptionSettings", menuName = "Safe Haven/Settings/Perception Settings")]
public sealed class PerceptionSettings : GameSettings<PerceptionSettings>
{
    [Header("Noticing")]
    [Tooltip("Awareness (0–1) at which a monster becomes suspicious: it shows '?' and goes to look. At 1 it has spotted you.")]
    [SerializeField, Range(0.05f, 0.95f)] private float suspiciousAt = 0.35f;
    [Tooltip("Seconds of full view it takes to spot a fully visible target right in front of it, just outside close range.")]
    [SerializeField, Min(0.05f)] private float nearNoticeSeconds = 0.6f;
    [Tooltip("Seconds of full view it takes to spot a fully visible target at the edge of its sight range.")]
    [SerializeField, Min(0.05f)] private float farNoticeSeconds = 3f;
    [Tooltip("Seconds it takes to notice something inside its close sense range, from any direction and through cover.")]
    [SerializeField, Min(0.05f)] private float closeNoticeSeconds = 0.35f;
    [Tooltip("Awareness lost per second while it sees nothing (until it is calm again).")]
    [SerializeField, Min(0f)] private float decayPerSecond = 0.15f;
    [Tooltip("Sight range is multiplied by the square root of the target's visibility, kept between these bounds: a sneaking target can come closer, a fighting one is seen from further away.")]
    [SerializeField, Range(0.1f, 1f)] private float minRangeScale = 0.45f;
    [SerializeField, Range(1f, 2f)] private float maxRangeScale = 1.3f;
    [Tooltip("Height of a monster's eyes and of the point on the target it looks at, for line of sight.")]
    [SerializeField, Min(0f)] private float eyeHeight = 1.4f;
    [SerializeField, Min(0f)] private float targetHeight = 1.1f;
    [Tooltip("A second point this far above the target point (the head) is also tried, so a bump in the ground doesn't hide a standing target.")]
    [SerializeField, Min(0f)] private float headAboveTarget = 0.6f;
    [Tooltip("What blocks sight: terrain, trees, rocks and buildings. Characters never block it.")]
    [SerializeField] private LayerMask sightBlockers = 1;

    [Header("Fog and Dark")]
    [Tooltip("Volumetric fog density (Atmospheric Fog, weather included) counted as dense fog.")]
    [SerializeField, Min(0.001f)] private float denseFog = 0.06f;
    [Tooltip("Share of their sight range monsters keep in dense fog; thinner fog costs proportionally less.")]
    [SerializeField, Range(0.2f, 1f)] private float sightInDenseFog = 0.55f;
    [Tooltip("Share of their sight range monsters keep at night (they still hear as well).")]
    [SerializeField, Range(0.2f, 1f)] private float sightAtNight = 0.75f;

    [Header("Hunting and searching")]
    [Tooltip("Seconds a hunting monster keeps after a target it can no longer see before it goes to where it last saw it.")]
    [SerializeField, Min(0f)] private float loseTrackSeconds = 3f;
    [Tooltip("Seconds it looks around at the spot it went to check.")]
    [SerializeField, Min(0f)] private float searchSeconds = 6f;
    [Tooltip("Seconds it may walk toward a spot before it gives up and searches where it is.")]
    [SerializeField, Min(1f)] private float maxInvestigateSeconds = 15f;
    [Tooltip("Movement speed while going to check something, as a share of its normal speed.")]
    [SerializeField, Range(0.2f, 1f)] private float investigateSpeedScale = 0.55f;
    [Tooltip("Degrees per second it turns while looking around.")]
    [SerializeField, Min(0f)] private float lookAroundSpeed = 70f;
    [Tooltip("Awareness right after losing track of a target: suspicious, and quick to spot it again.")]
    [SerializeField, Range(0f, 1f)] private float lostTrackAwareness = 0.8f;
    [Tooltip("Seconds between idle monsters glancing in a new direction (a random pick in this range).")]
    [SerializeField] private Vector2 idleGlanceSeconds = new Vector2(4f, 9f);
    [Tooltip("How far, in degrees, an idle monster glances away from the way it first faced.")]
    [SerializeField, Range(0f, 180f)] private float idleGlanceAngle = 75f;
    [Tooltip("A monster that spots a target calls out: other idle monsters within this radius come to look. 0 = no call.")]
    [SerializeField, Min(0f)] private float spottedCallRadius = 10f;

    [Header("Hearing")]
    [Tooltip("Awareness a sound gives at its very edge (it reaches the suspicious level, so they come to look)…")]
    [SerializeField, Range(0f, 1f)] private float heardAwarenessFar = 0.35f;
    [Tooltip("…and right at its source. Below 1, so a sound alone never spots you; they have to see you.")]
    [SerializeField, Range(0f, 0.99f)] private float heardAwarenessNear = 0.9f;

    [Header("Ambush")]
    [Tooltip("Damage an unaware monster takes (a hit from the shadows). 1 = no bonus.")]
    [SerializeField, Min(1f)] private float ambushDamageTaken = 1.5f;

    [Header("Player visibility (1 = plainly visible)")]
    [SerializeField, Min(0f)] private float movingVisibility = 1f;
    [SerializeField, Min(0f)] private float stillVisibility = 0.75f;
    [SerializeField, Min(0f)] private float sneakingVisibility = 0.45f;
    [Tooltip("While fighting (hitting or being hit) and for Fighting Seconds after.")]
    [SerializeField, Min(0f)] private float fightingVisibility = 1.4f;
    [SerializeField, Min(0f)] private float fightingSeconds = 3f;
    [Tooltip("Movement speed while sneaking (hold Left Ctrl / left bumper), as a share of normal speed.")]
    [SerializeField, Range(0.1f, 1f)] private float sneakSpeedScale = 0.5f;

    [Header("Player noise (radius in metres; monsters inside hear it)")]
    [SerializeField, Min(0f)] private float footstepNoise = 5f;
    [SerializeField, Min(0f)] private float sneakingFootstepNoise = 1.2f;
    [Tooltip("Seconds between footstep noises while moving.")]
    [SerializeField, Min(0.1f)] private float footstepInterval = 0.45f;
    [SerializeField, Min(0f)] private float dodgeNoise = 7f;
    [Tooltip("Every hit the player lands or takes.")]
    [SerializeField, Min(0f)] private float combatNoise = 13f;
    [Tooltip("Seconds between combat noises, so a long fight doesn't flood the forest with sounds.")]
    [SerializeField, Min(0f)] private float combatNoiseInterval = 0.6f;

    [Header("Presentation")]
    [Tooltip("Shows '?' over suspicious and searching monsters and '!' when one spots its target.")]
    [SerializeField] private bool showIndicators = true;
    [SerializeField, Min(0f)] private float indicatorHeight = 2.4f;
    [SerializeField, Min(0.5f)] private float indicatorFontSize = 6f;
    [SerializeField] private Color suspiciousColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private Color searchingColor = new Color(0.8f, 0.8f, 0.75f);
    [SerializeField] private Color spottedColor = new Color(1f, 0.25f, 0.18f);
    [Tooltip("Seconds the '!' stays up after a monster spots its target.")]
    [SerializeField, Min(0f)] private float spottedShowSeconds = 1.4f;
    [Tooltip("Plays when a monster spots the player. Empty: none.")]
    [SerializeField] private SoundDefinition spottedSound;
    [Tooltip("Plays when a monster becomes suspicious of a noise or a glimpse. Empty: none.")]
    [SerializeField] private SoundDefinition suspiciousSound;

    public float SuspiciousAt => suspiciousAt;
    public float NearNoticeSeconds => nearNoticeSeconds;
    public float FarNoticeSeconds => Mathf.Max(nearNoticeSeconds, farNoticeSeconds);
    public float CloseNoticeSeconds => closeNoticeSeconds;
    public float DecayPerSecond => decayPerSecond;
    public float MinRangeScale => minRangeScale;
    public float MaxRangeScale => maxRangeScale;
    public float EyeHeight => eyeHeight;
    public float TargetHeight => targetHeight;
    public float HeadAboveTarget => headAboveTarget;
    public LayerMask SightBlockers => sightBlockers;
    public float DenseFog => denseFog;
    public float SightInDenseFog => sightInDenseFog;
    public float SightAtNight => sightAtNight;

    public float LoseTrackSeconds => loseTrackSeconds;
    public float SearchSeconds => searchSeconds;
    public float MaxInvestigateSeconds => maxInvestigateSeconds;
    public float InvestigateSpeedScale => investigateSpeedScale;
    public float LookAroundSpeed => lookAroundSpeed;
    public float LostTrackAwareness => Mathf.Max(lostTrackAwareness, suspiciousAt);
    public Vector2 IdleGlanceSeconds => idleGlanceSeconds;
    public float IdleGlanceAngle => idleGlanceAngle;
    public float SpottedCallRadius => spottedCallRadius;

    public float HeardAwarenessFar => heardAwarenessFar;
    public float HeardAwarenessNear => Mathf.Max(heardAwarenessFar, heardAwarenessNear);

    public float AmbushDamageTaken => ambushDamageTaken;

    public float MovingVisibility => movingVisibility;
    public float StillVisibility => stillVisibility;
    public float SneakingVisibility => sneakingVisibility;
    public float FightingVisibility => fightingVisibility;
    public float FightingSeconds => fightingSeconds;
    public float SneakSpeedScale => sneakSpeedScale;

    public float FootstepNoise => footstepNoise;
    public float SneakingFootstepNoise => sneakingFootstepNoise;
    public float FootstepInterval => footstepInterval;
    public float DodgeNoise => dodgeNoise;
    public float CombatNoise => combatNoise;
    public float CombatNoiseInterval => combatNoiseInterval;

    public bool ShowIndicators => showIndicators;
    public float IndicatorHeight => indicatorHeight;
    public float IndicatorFontSize => indicatorFontSize;
    public Color SuspiciousColor => suspiciousColor;
    public Color SearchingColor => searchingColor;
    public Color SpottedColor => spottedColor;
    public float SpottedShowSeconds => spottedShowSeconds;
    public SoundDefinition SpottedSound => spottedSound;
    public SoundDefinition SuspiciousSound => suspiciousSound;
}
