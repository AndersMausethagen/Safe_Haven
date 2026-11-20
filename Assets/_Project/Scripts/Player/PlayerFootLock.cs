using UnityEngine;

/// <summary>
/// Keeps a planted foot where it was put down (the user, 2026-11-20: "start with 1,2,3", the second being feet that don't skate). The
/// clips were made with their own root motion; played in place, through a swing, a turn on the spot or a stop, a foot the clip means to
/// stand on still slides over the ground. While the body stands (barely moving, on the ground, not dodging, swimming, riding or seated),
/// each foot that is down and still in the clip (not stepping: Step Speed) is held at the spot and turn it touched down with, the leg
/// bent to reach it (ArmSolver, as the two-handed grip bends the arm); it lets go, over a tenth of a second, as soon as the clip lifts
/// that foot, steps it or slides it away (Release Distance), the body turns too far past it, or the body moves off, and at once when the
/// body leaves its feet. Feet only: the pose above the knees is the clip's. Measured on the live animator (A/B probe, 2026-11-20):
/// a foot that is down skated 54% less over swings, chains, guard exits, turns and stops of three weapons, turns on the spot 85-90%.
/// </summary>
[DisallowMultipleComponent]
// After the animator, the swing's hop and the lantern's hips; before the hands (TwoHandGrip) and anything that reads the feet.
[DefaultExecutionOrder(90)]
public sealed class PlayerFootLock : MonoBehaviour
{
    [Tooltip("A foot counts as down while it is no more than this above the lowest it has been lately (where it stands), in metres.")]
    [SerializeField, Min(0.005f)] private float downHeight = 0.05f;
    [Tooltip("A foot rising faster than this, in metres a second, is being lifted: it lets go.")]
    [SerializeField, Min(0.05f)] private float liftSpeed = 0.35f;
    [Tooltip("A foot the clip moves faster than this over the ground, in metres a second, is stepping (a swing's quick low step): it isn't " +
             "held, so it goes with the clip. Slower is the foot skating where the clip means it to stand.")]
    [SerializeField, Min(0.1f)] private float stepSpeed = 1f;
    [Tooltip("The clip stepping a held foot this far away from where it is held, in metres, is a step: it lets go and goes down again there.")]
    [SerializeField, Min(0.02f)] private float releaseDistance = 0.22f;
    [Tooltip("The body turning a held foot this many degrees past the way it touched down: it lets go.")]
    [SerializeField, Range(5f, 90f)] private float releaseTurn = 40f;
    [Tooltip("The body moving faster than this, in metres a second (walking off, a lunge, a hop), lets both feet go.")]
    [SerializeField, Min(0.05f)] private float standingSpeed = 0.45f;
    [Tooltip("How fast a foot takes hold and lets go, in full weights a second (10: a tenth of a second).")]
    [SerializeField, Min(1f)] private float fadeRate = 10f;

    private sealed class Leg
    {
        public Transform Upper, Lower, Foot;
        public float UpperLength, LowerLength;
        // The lowest this foot has been lately, over the body's feet: where it stands. Starts at the rest pose's, follows the foot
        // down at once and drifts back up slowly, so a crouched stance, a slope or a rig whose rest pose isn't standing all settle.
        public float Floor;
        public bool Held;
        public float Weight;
        public Vector3 Spot;
        public Quaternion Turn;
        public float HeldYaw;
        public float LastHeight;
        public Vector3 LastAnimated;
        public bool Seen;
        // Let go while still down: not taken hold of again until it lifts or is wholly free, else it was grabbed back at nine tenths of
        // the old spot and dragged behind the clip with the leg at full stretch (review 2026-11-20).
        public bool WaitsToLift;
    }

    // Metres a second a foot's floor drifts back up when the foot stays above it.
    private const float FloorDrift = 0.05f;

    private PlayerMovement movement;
    private PlayerDodge dodge;
    private PlayerSwimming swimming;
    private PlayerRiding riding;
    private Damageable health;
    private Leg left, right;
    private Vector3 lastPosition;
    private float bodySpeed;
    private bool started;

    /// <summary>How much each foot is held now (0 free, 1 held), for tests and probes.</summary>
    public float LeftWeight => left != null ? left.Weight : 0f;
    public float RightWeight => right != null ? right.Weight : 0f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        dodge = GetComponent<PlayerDodge>();
        swimming = GetComponent<PlayerSwimming>();
        riding = GetComponent<PlayerRiding>();
        health = GetComponent<Damageable>();
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        left = LegOf(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        right = LegOf(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        if (left == null || right == null)
        {
            enabled = false;
        }
    }

    // The bones still stand in the rest pose (nothing has animated them yet): each foot's height over the body's feet as it stands.
    private Leg LegOf(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot)
    {
        var leg = new Leg { Upper = animator.GetBoneTransform(upper), Lower = animator.GetBoneTransform(lower), Foot = animator.GetBoneTransform(foot) };
        if (leg.Upper == null || leg.Lower == null || leg.Foot == null)
        {
            return null;
        }

        leg.UpperLength = Vector3.Distance(leg.Upper.position, leg.Lower.position);
        leg.LowerLength = Vector3.Distance(leg.Lower.position, leg.Foot.position);
        leg.Floor = leg.Foot.position.y - transform.position.y;
        return leg;
    }

    private void OnDisable()
    {
        started = false;
        if (left != null)
        {
            left.Held = right.Held = left.Seen = right.Seen = left.WaitsToLift = right.WaitsToLift = false;
            left.Weight = right.Weight = 0f;
        }
    }

    // On the ground and on its own feet: off them (a dodge, a fall, a seat, a saddle, the water, death) the feet go at once.
    private bool OnItsFeet =>
        (movement == null || (movement.IsGrounded && !movement.IsSeated && !movement.IsFloating))
        && (dodge == null || !dodge.IsDodging) && (swimming == null || !swimming.IsSwimming) && (riding == null || !riding.IsRiding)
        && (health == null || health.IsAlive);

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        Vector3 moved = transform.position - lastPosition;
        moved.y = 0f;
        lastPosition = transform.position;
        // Smoothed over a few frames: one long frame mustn't let go of both feet.
        bodySpeed = started ? Mathf.Lerp(bodySpeed, moved.magnitude / dt, 1f - Mathf.Exp(-20f * dt)) : 0f;
        started = true;
        bool onItsFeet = OnItsFeet;
        bool standing = onItsFeet && bodySpeed < standingSpeed;
        Hold(left, standing, !onItsFeet, dt);
        Hold(right, standing, !onItsFeet, dt);
    }

    private void Hold(Leg leg, bool standing, bool letGoAtOnce, float dt)
    {
        Vector3 animated = leg.Foot.position;
        Quaternion animatedTurn = leg.Foot.rotation;
        float height = animated.y - transform.position.y;
        float rising = (height - leg.LastHeight) / dt;
        leg.LastHeight = height;
        Vector3 travel = animated - leg.LastAnimated;
        travel.y = 0f;
        bool stepping = leg.Seen && travel.magnitude / dt > stepSpeed;
        leg.LastAnimated = animated;
        leg.Seen = true;
        leg.Floor = Mathf.Min(leg.Floor + FloorDrift * dt, height);
        bool down = height <= leg.Floor + downHeight && rising < liftSpeed;
        float yaw = transform.eulerAngles.y;

        if (leg.Held)
        {
            Vector3 away = animated - leg.Spot;
            away.y = 0f;
            float turned = Mathf.Abs(Mathf.DeltaAngle(yaw, leg.HeldYaw));
            if (!standing || !down || stepping || away.magnitude > releaseDistance || turned > releaseTurn)
            {
                leg.Held = false;
                leg.WaitsToLift = down;
                // Far off (a body moved a long way at once): reaching back for the old spot would stretch the leg after it.
                if (away.magnitude > releaseDistance * 3f)
                {
                    leg.Weight = 0f;
                }
            }
        }
        else if (standing && down && !stepping && !leg.WaitsToLift)
        {
            // Touched down: held where it shows now, mid-fade included, so taking hold again never jumps.
            leg.Spot = Vector3.Lerp(animated, leg.Spot, leg.Weight);
            leg.Turn = Quaternion.Slerp(animatedTurn, leg.Turn, leg.Weight);
            leg.HeldYaw = yaw;
            leg.Held = true;
        }

        if (letGoAtOnce)
        {
            leg.Held = false;
            leg.Weight = 0f;
        }

        leg.Weight = Mathf.MoveTowards(leg.Weight, leg.Held ? 1f : 0f, fadeRate * dt);
        if (leg.WaitsToLift && (!down || leg.Weight <= 0f))
        {
            leg.WaitsToLift = false;
        }

        if (leg.Weight <= 0f)
        {
            return;
        }

        Vector3 target = Vector3.Lerp(animated, leg.Spot, leg.Weight);
        Quaternion footTurn = Quaternion.Slerp(animatedTurn, leg.Turn, leg.Weight);
        // The knee bends the way it does, and forward when the clip has the leg dead straight (no bend to follow).
        Vector3 bend = leg.Lower.position + transform.forward * 0.02f;
        Vector3 knee = ArmSolver.Solve(leg.Upper.position, bend, target, leg.UpperLength, leg.LowerLength, out Vector3 ankle);
        leg.Upper.rotation = Quaternion.FromToRotation(leg.Lower.position - leg.Upper.position, knee - leg.Upper.position) * leg.Upper.rotation;
        leg.Lower.rotation = Quaternion.FromToRotation(leg.Foot.position - leg.Lower.position, ankle - leg.Lower.position) * leg.Lower.rotation;
        leg.Foot.rotation = footTurn;
    }
}
