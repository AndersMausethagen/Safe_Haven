using UnityEngine;

/// <summary>
/// Poses the player while swimming, over whatever the animator plays (the standing idle; there are no swimming clips).
/// Standing still they tread water: upright, sculling with the forearms and cycling the legs. Moving they lean forward
/// until they lie in the water with the back at the surface and the head up, and swim breaststroke with a flutter kick,
/// stroking faster the faster they go. Blends in and out with <see cref="PlayerSwimming.IsSwimming"/>.
/// <para>
/// Limbs are aimed rather than rotated by fixed angles, so the pose works on any humanoid rig: each segment is turned so
/// it points along a direction given in the torso's frame (x right, y up the spine, z out of the chest).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSwimming))]
public sealed class PlayerSwimAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("How quickly the swimming pose fades in and out (weight per second).")]
    [SerializeField, Min(0.1f)] private float blendSpeed = 4f;
    [Tooltip("Forward lean while treading water, in degrees.")]
    [SerializeField, Range(0f, 45f)] private float treadLean = 8f;
    [Tooltip("Forward lean while swimming flat out, in degrees (90 lies flat).")]
    [SerializeField, Range(0f, 90f)] private float swimLean = 70f;
    [Tooltip("How far under the surface the hips lie while swimming flat out (metres).")]
    [SerializeField] private float swimHipsDepth = 0.22f;
    [Tooltip("Share of the lean the neck takes back, so the swimmer looks ahead.")]
    [SerializeField, Range(0f, 1f)] private float headUp = 0.6f;
    [Tooltip("Seconds per stroke treading water, and swimming flat out.")]
    [SerializeField, Min(0.2f)] private float treadCycle = 1.6f;
    [SerializeField, Min(0.2f)] private float strokeCycle = 1.15f;
    [Tooltip("Metres the body bobs up and down with each stroke.")]
    [SerializeField, Min(0f)] private float bob = 0.04f;

    // Breaststroke of the right arm through one cycle (the left mirrors it): reach ahead, sweep out, pull in under the
    // chest, then gather the hands and shoot them forward again. Upper arm, then forearm, in the torso's frame.
    private static readonly Vector3[] StrokeUpper =
    {
        new Vector3(0.12f, 1f, 0.15f), new Vector3(0.75f, 0.75f, 0.25f), new Vector3(0.9f, 0.1f, 0.5f), new Vector3(0.35f, 0.3f, 1f),
    };
    private static readonly Vector3[] StrokeLower =
    {
        new Vector3(0.05f, 1f, 0.1f), new Vector3(0.8f, 0.5f, 0.35f), new Vector3(-0.3f, 0.3f, 1f), new Vector3(-0.1f, 0.9f, 0.5f),
    };
    // Where in the cycle each of those poses is reached.
    private static readonly float[] StrokeTimes = { 0f, 0.3f, 0.55f, 0.75f };

    private PlayerSwimming swimming;
    private PlayerMovement movement;
    private Transform hips;
    private Transform neck;
    private readonly Limb leftArm = new Limb();
    private readonly Limb rightArm = new Limb();
    private readonly Limb leftLeg = new Limb();
    private readonly Limb rightLeg = new Limb();
    private float weight;
    private float pace;
    private float phase;

    private sealed class Limb
    {
        public Transform Upper;
        public Transform Lower;
        public Transform End;

        public bool Valid => Upper != null && Lower != null && End != null;
    }

    private void Awake()
    {
        swimming = GetComponent<PlayerSwimming>();
        movement = GetComponent<PlayerMovement>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"{name}: PlayerSwimAnimation needs a humanoid Animator; swimming won't be posed.", this);
            enabled = false;
            return;
        }

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        clips = GetComponent<PlayerAnimationController>();
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        neck = neck != null ? neck : animator.GetBoneTransform(HumanBodyBones.Head);
        Find(leftArm, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        Find(rightArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        Find(leftLeg, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        Find(rightLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
    }

    private void Find(Limb limb, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end)
    {
        limb.Upper = animator.GetBoneTransform(upper);
        limb.Lower = animator.GetBoneTransform(lower);
        limb.End = animator.GetBoneTransform(end);
    }

    // After the animator has posed the body this frame.
    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        weight = Mathf.MoveTowards(weight, swimming.IsSwimming ? 1f : 0f, blendSpeed * deltaTime);
        if (weight <= 0f || hips == null)
        {
            return;
        }

        float wanted = Mathf.Clamp01(movement.HorizontalSpeed / Mathf.Max(0.1f, swimming.SwimSpeed));
        pace = Mathf.MoveTowards(pace, wanted, 2f * deltaTime);
        if (clips != null && clips.HasSwimState)
        {
            PlaceInTheWater();
            return;
        }

        phase = Mathf.Repeat(phase + deltaTime / Mathf.Lerp(treadCycle, strokeCycle, pace), 1f);
        float wave = Mathf.Sin(phase * 2f * Mathf.PI);

        // Lift the body so that lying forward keeps the back at the surface, and bob with the stroke.
        float lift = Mathf.Max(0f, swimming.SurfaceHeight - swimHipsDepth - hips.position.y) * pace + wave * bob;
        hips.position += Vector3.up * (lift * weight);

        Vector3 right = transform.right;
        float lean = Mathf.Lerp(treadLean, swimLean, pace) * weight;
        hips.rotation = Quaternion.AngleAxis(lean, right) * hips.rotation;
        if (neck != null)
        {
            neck.rotation = Quaternion.AngleAxis(-lean * headUp, right) * neck.rotation;
        }

        Quaternion torso = Quaternion.AngleAxis(lean, right) * transform.rotation;
        PoseArm(rightArm, torso, phase, 1f, wave);
        PoseArm(leftArm, torso, phase, -1f, wave);
        PoseLeg(rightLeg, torso, phase, 1f);
        PoseLeg(leftLeg, torso, Mathf.Repeat(phase + 0.5f, 1f), -1f);
    }

    // The swim clips carry the hips about 0.25 m higher than standing (made for a body held up by the water): treading water the
    // hips stay where a standing body's are, as the water holds the player; swimming they come up to Swim Hips Depth under the
    // surface, lying in it. The clips do the strokes.
    private PlayerAnimationController clips;
    private float standingHips = -1f;

    private void PlaceInTheWater()
    {
        float treading = transform.position.y + (standingHips > 0f ? standingHips : 0.95f);
        float target = Mathf.Lerp(treading, swimming.SurfaceHeight - swimHipsDepth, pace);
        hips.position += Vector3.up * ((target - hips.position.y) * weight);
    }

    // The standing hips' height over the feet, learnt while on land, for treading water.
    private void Update()
    {
        if (hips != null && swimming != null && !swimming.IsSwimming && movement != null && movement.IsGrounded && movement.HorizontalSpeed < 0.2f)
        {
            float height = hips.position.y - transform.position.y;
            standingHips = standingHips < 0f ? height : Mathf.Lerp(standingHips, height, 0.05f);
        }
    }

    private void PoseArm(Limb arm, Quaternion torso, float at, float side, float wave)
    {
        if (!arm.Valid)
        {
            return;
        }

        // Treading water: arms out to the sides and a little forward, forearms sculling in and out.
        Vector3 treadUpper = new Vector3(0.75f, -0.45f, 0.45f);
        Vector3 treadLower = new Vector3(0.25f + 0.45f * wave, -0.15f, 1f);
        Stroke(at, out Vector3 strokeUpper, out Vector3 strokeLower);
        Aim(arm.Upper, arm.Lower, torso * Mirror(Vector3.Slerp(treadUpper.normalized, strokeUpper, pace), side));
        Aim(arm.Lower, arm.End, torso * Mirror(Vector3.Slerp(treadLower.normalized, strokeLower, pace), side));
    }

    private void PoseLeg(Limb leg, Quaternion torso, float at, float side)
    {
        if (!leg.Valid)
        {
            return;
        }

        // Treading: slow cycling, as on a bicycle. Swimming: a flutter kick, two to a stroke.
        float cycle = at * 2f * Mathf.PI;
        Vector3 treadUpper = new Vector3(0.12f, -1f, 0.35f + 0.35f * Mathf.Sin(cycle));
        Vector3 treadLower = new Vector3(0.05f, -1f, -0.25f + 0.35f * Mathf.Sin(cycle - Mathf.PI * 0.5f));
        Vector3 kickUpper = new Vector3(0.1f, -1f, 0.18f * Mathf.Sin(cycle * 2f));
        Vector3 kickLower = new Vector3(0.08f, -1f, -0.05f + 0.25f * Mathf.Sin(cycle * 2f - 1f));
        Aim(leg.Upper, leg.Lower, torso * Mirror(Vector3.Slerp(treadUpper.normalized, kickUpper.normalized, pace), side));
        Aim(leg.Lower, leg.End, torso * Mirror(Vector3.Slerp(treadLower.normalized, kickLower.normalized, pace), side));
    }

    // The breaststroke pose at a point of the cycle, eased between its key poses.
    private static void Stroke(float at, out Vector3 upper, out Vector3 lower)
    {
        int last = StrokeTimes.Length - 1;
        int from = last;
        for (int i = 0; i < last; i++)
        {
            if (at < StrokeTimes[i + 1])
            {
                from = i;
                break;
            }
        }

        int to = (from + 1) % StrokeTimes.Length;
        float end = to == 0 ? 1f : StrokeTimes[to];
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StrokeTimes[from], end, at));
        upper = Vector3.Slerp(StrokeUpper[from].normalized, StrokeUpper[to].normalized, t);
        lower = Vector3.Slerp(StrokeLower[from].normalized, StrokeLower[to].normalized, t);
    }

    private static Vector3 Mirror(Vector3 direction, float side) => new Vector3(direction.x * side, direction.y, direction.z);

    // Turns the bone so the segment to its child points along the direction, as far as the blend weight goes.
    private void Aim(Transform bone, Transform child, Vector3 direction)
    {
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 1e-6f)
        {
            return;
        }

        Quaternion turn = Quaternion.FromToRotation(current, direction);
        bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
    }
}
