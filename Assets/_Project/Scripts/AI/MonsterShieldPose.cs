using UnityEngine;

/// <summary>
/// Holds a monster's shield up in front of it while it is up (MonsterAI.ShieldIsUp), the left arm bent to bring it there and the shield's
/// face turned to the front, after the animation has posed the body. It lets the arm go, with a short fade, for what doesn't hold it there:
/// a strike, a stagger, a hop. The clips were made with both arms free, so this is what says the thing is guarding.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI))]
// After the animator's own update, as with the player's TwoHandGrip.
[DefaultExecutionOrder(100)]
public sealed class MonsterShieldPose : MonoBehaviour
{
    private const float FadePerSecond = 9f;

    [SerializeField] private Transform shield;
    [Tooltip("Where the left hand is held, from the shoulder: metres forward, to its left and up.")]
    [SerializeField] private Vector3 handFromShoulder = new Vector3(0.36f, -0.02f, -0.12f);

    private MonsterAI ai;
    private Transform upperArm;
    private Transform lowerArm;
    private Transform hand;
    private float upperLength;
    private float lowerLength;
    private Vector3 localFace = Vector3.forward;
    private float weight;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        Animator animator = GetComponentInChildren<Animator>();
        if (shield == null || animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (upperArm == null || lowerArm == null || hand == null)
        {
            enabled = false;
            return;
        }

        upperLength = Vector3.Distance(upperArm.position, lowerArm.position);
        lowerLength = Vector3.Distance(lowerArm.position, hand.position);
        localFace = FaceOf(shield);
    }

    /// <summary>The shield splinters (two guard-broken staggers): it falls away in pieces and the arm is let go for good.</summary>
    public void Splinter(Vector3 blowDirection)
    {
        if (shield != null && shield.gameObject.activeInHierarchy)
        {
            ArmourDebris.BreakOff(shield, blowDirection, splinters: true);
        }

        enabled = false;
    }

    // The thinnest way through the shield's mesh is the way its face looks.
    private static Vector3 FaceOf(Transform model)
    {
        MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return Vector3.forward;
        }

        Vector3 size = filter.sharedMesh.bounds.size;
        Vector3 axis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
        return model.InverseTransformDirection(filter.transform.TransformDirection(axis)).normalized;
    }

    private void LateUpdate()
    {
        weight = Mathf.MoveTowards(weight, ai.ShieldIsUp ? 1f : 0f, FadePerSecond * Time.deltaTime);
        if (weight <= 0f)
        {
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 wanted = upperArm.position + forward * handFromShoulder.x - transform.right * handFromShoulder.z + Vector3.up * handFromShoulder.y;
        Vector3 target = Vector3.Lerp(hand.position, wanted, weight);
        Quaternion handRotation = hand.rotation;
        Vector3 elbow = ArmSolver.Solve(upperArm.position, lowerArm.position, target, upperLength, lowerLength, out Vector3 wrist);
        upperArm.rotation = Quaternion.FromToRotation(lowerArm.position - upperArm.position, elbow - upperArm.position) * upperArm.rotation;
        lowerArm.rotation = Quaternion.FromToRotation(hand.position - lowerArm.position, wrist - lowerArm.position) * lowerArm.rotation;
        hand.rotation = handRotation;

        // Whichever way its face already looks more nearly forward, so the shield doesn't flip over.
        Vector3 face = shield.TransformDirection(localFace);
        Vector3 toward = Vector3.Dot(face, forward) >= 0f ? forward : -forward;
        Quaternion turned = Quaternion.FromToRotation(face, toward) * shield.rotation;
        shield.rotation = Quaternion.Slerp(shield.rotation, turned, weight);
    }
}
