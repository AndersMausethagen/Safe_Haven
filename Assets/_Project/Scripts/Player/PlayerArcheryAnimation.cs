using UnityEngine;

/// <summary>
/// Plays the bow on the animator's "Bow" layer (upper body; built by Safe Haven › Characters › Set Up Combat
/// Animations): the layer fades in while a bow is in hand, BowDrawing holds the draw (Load, then Hold), BowShot plays the
/// release. Load and release are sped up or slowed so they last as long as the bow's draw and reload take.
/// <para>
/// Aiming: the archer clips stand sideways and shoot wherever their spine points, which on our walking hips is off to
/// one side. After the animator runs, the spine is turned so the line from the drawing hand to the bow hand points at
/// <see cref="PlayerArchery.AimPoint"/> (up or down too, within limits), blended in with the draw.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10)] // before HeldBowView strings the bow on the corrected hands
[RequireComponent(typeof(PlayerArchery))]
public sealed class PlayerArcheryAnimation : MonoBehaviour
{
    public const string LayerName = "Bow";
    private static readonly int DrawingHash = Animator.StringToHash("BowDrawing");
    private static readonly int ShotHash = Animator.StringToHash("BowShot");
    private static readonly int DrawSpeedHash = Animator.StringToHash("DrawSpeed");
    private static readonly int ReloadSpeedHash = Animator.StringToHash("ReloadSpeed");

    [SerializeField] private Animator animator;
    [Tooltip("Length of the Load clip in seconds (set by the animation setup).")]
    [SerializeField, Min(0.05f)] private float loadClipSeconds = 0.83f;
    [Tooltip("Length of the Release clip in seconds (set by the animation setup).")]
    [SerializeField, Min(0.05f)] private float releaseClipSeconds = 0.8f;
    [Tooltip("How quickly the bow pose fades in and out (weight per second).")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 6f;
    [Tooltip("Most the spine turns sideways to aim, in degrees.")]
    [SerializeField, Range(0f, 120f)] private float maxAimYaw = 90f;
    [Tooltip("Most the spine leans up or down to aim, in degrees.")]
    [SerializeField, Range(0f, 60f)] private float maxAimPitch = 35f;
    [Tooltip("How quickly aiming blends in with the draw and out after the shot (weight per second).")]
    [SerializeField, Min(0.1f)] private float aimBlendSpeed = 5f;

    private PlayerArchery archery;
    private int layer = -1;
    private float weight;
    private float aimWeight;
    private Transform spine;
    private Transform bowHand;
    private Transform drawHand;

    private void Awake()
    {
        archery = GetComponent<PlayerArchery>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        layer = animator != null ? animator.GetLayerIndex(LayerName) : -1;
        if (animator != null && animator.isHuman)
        {
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            bowHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            drawHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (layer < 0)
        {
            Debug.LogWarning($"{name}: the animator has no \"{LayerName}\" layer; run Safe Haven > Characters > Set Up Combat Animations.", this);
        }
    }

    private void OnEnable()
    {
        archery.DrawStarted += OnDrawStarted;
        archery.DrawCancelled += OnDrawCancelled;
        archery.Shot += OnShot;
    }

    private void OnDisable()
    {
        archery.DrawStarted -= OnDrawStarted;
        archery.DrawCancelled -= OnDrawCancelled;
        archery.Shot -= OnShot;
    }

    private void Update()
    {
        if (layer < 0)
        {
            return;
        }

        weight = Mathf.MoveTowards(weight, archery.Bow != null ? 1f : 0f, fadeSpeed * Time.deltaTime);
        animator.SetLayerWeight(layer, weight);
    }

    private void LateUpdate()
    {
        bool aiming = archery.IsDrawing || archery.State == PlayerArchery.Phase.Reloading;
        aimWeight = Mathf.MoveTowards(aimWeight, aiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
        if (aimWeight <= 0f || spine == null || bowHand == null || drawHand == null)
        {
            return;
        }

        Vector3 line = bowHand.position - drawHand.position;
        Vector3 wanted = archery.AimPoint - bowHand.position;
        if (line.sqrMagnitude < 0.0001f || wanted.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Turn first, around the vertical, then lean, around the shoulders' axis; each within its limit.
        float yaw = Mathf.Clamp(Vector3.SignedAngle(Flat(line), Flat(wanted), Vector3.up), -maxAimYaw, maxAimYaw);
        Quaternion turn = Quaternion.AngleAxis(yaw * aimWeight, Vector3.up);
        line = turn * line;
        Vector3 across = Vector3.Cross(Vector3.up, line);
        float pitch = across.sqrMagnitude > 0.0001f
            ? Mathf.Clamp(Vector3.SignedAngle(line, Vector3.ProjectOnPlane(wanted, across), across), -maxAimPitch, maxAimPitch)
            : 0f;
        spine.rotation = Quaternion.AngleAxis(pitch * aimWeight, across.normalized) * turn * spine.rotation;
    }

    private static Vector3 Flat(Vector3 direction) => new Vector3(direction.x, 0f, direction.z);

    private void OnDrawStarted(float drawSeconds)
    {
        if (layer < 0)
        {
            return;
        }

        animator.ResetTrigger(ShotHash);
        animator.SetFloat(DrawSpeedHash, Mathf.Clamp(loadClipSeconds / drawSeconds, 0.25f, 4f));
        animator.SetBool(DrawingHash, true);
    }

    private void OnDrawCancelled()
    {
        if (layer >= 0)
        {
            animator.SetBool(DrawingHash, false);
        }
    }

    private void OnShot(ArrowShot shot)
    {
        if (layer < 0)
        {
            return;
        }

        animator.SetFloat(ReloadSpeedHash, Mathf.Clamp(releaseClipSeconds / Mathf.Max(0.2f, shot.ReloadSeconds), 0.5f, 4f));
        animator.SetTrigger(ShotHash);
        animator.SetBool(DrawingHash, false);
    }
}
