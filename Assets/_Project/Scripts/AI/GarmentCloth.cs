using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A garment that hangs and swings (the Lost Ones' skirts, dresses and coats: the user, 2026-11-17, "separate the armor or cloth only,
/// not the limbs. and give it realistic physics"). Unity's Cloth on the garment's own skinned mesh: it follows the body's bones where
/// it is pinned (above the belt) and swings freer the nearer the hem, up to <see cref="maxSwing"/>, and the legs push it aside
/// (capsules on the thighs and shins, made from the body's Humanoid bones, so one setting fits any body). It rests while out of sight
/// or far away. Only the look: blows and the creature's movement don't depend on it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SkinnedMeshRenderer))]
[OnAFriendsCopy(CopyRule.Same, "only the look: each game swings its own copy's garment")]
public sealed class GarmentCloth : MonoBehaviour
{
    [Tooltip("Share of the garment's height, from its hem, where it is held fast: above this it only follows the body (a dress's bodice, " +
             "a coat's shoulders, a skirt's belt).")]
    [SerializeField, Range(0f, 1f)] private float pinnedAbove = 0.7f;
    [Tooltip("Metres the hem may swing from where the body would hold it. Linen swings far, mail little.")]
    [SerializeField, Min(0f)] private float maxSwing = 0.25f;
    [Tooltip("How much it keeps its length (1: not at all stretchy).")]
    [SerializeField, Range(0f, 1f)] private float stiffness = 0.9f;
    [Tooltip("How much it keeps its shape when bent (high for leather and mail).")]
    [SerializeField, Range(0f, 1f)] private float bendStiffness = 0.4f;
    [Tooltip("How quickly a swing dies down (high for heavy mail).")]
    [SerializeField, Range(0f, 1f)] private float damping = 0.2f;
    [Tooltip("How much of the body's own movement the garment feels: lower trails less behind a lunge.")]
    [SerializeField, Range(0f, 1f)] private float followsMovement = 0.4f;
    [Tooltip("Leg capsules' radius at a 1.8 m body, in metres (thigh; the shin is three quarters of it).")]
    [SerializeField, Min(0.01f)] private float thighRadius = 0.1f;
    [Tooltip("Beyond this many metres from the camera the cloth rests (it holds its last shape).")]
    [SerializeField, Min(5f)] private float restBeyond = 45f;

    private static readonly (HumanBodyBones from, HumanBodyBones to, float share)[] Legs =
    {
        (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 1f),
        (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 1f),
        (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.75f),
        (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.75f),
    };

    private Cloth cloth;
    private SkinnedMeshRenderer skin;
    private float nextLook;

    private void Start()
    {
        skin = GetComponent<SkinnedMeshRenderer>();
        cloth = GetComponent<Cloth>();
        if (cloth == null)
        {
            cloth = gameObject.AddComponent<Cloth>();
        }

        cloth.stretchingStiffness = stiffness;
        cloth.bendingStiffness = bendStiffness;
        cloth.damping = damping;
        cloth.useGravity = true;
        cloth.worldVelocityScale = followsMovement;
        cloth.worldAccelerationScale = Mathf.Min(1f, followsMovement + 0.3f);
        cloth.friction = 0.5f;
        cloth.collisionMassScale = 0.1f;
        Pin();
        HoldOffLegs();
    }

    // The hem free, the top held: by each vertex's height within the garment as it stands.
    private void Pin()
    {
        Vector3[] vertices = cloth.vertices;
        if (vertices.Length == 0)
        {
            return;
        }

        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        var heights = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            heights[i] = transform.TransformPoint(vertices[i]).y;
            low = Mathf.Min(low, heights[i]);
            high = Mathf.Max(high, heights[i]);
        }

        float pin = low + (high - low) * pinnedAbove;
        var coefficients = new ClothSkinningCoefficient[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            float free = pin > low ? Mathf.Clamp01((pin - heights[i]) / (pin - low)) : 0f;
            coefficients[i].maxDistance = maxSwing * free * free;
            coefficients[i].collisionSphereDistance = 0f;
        }

        cloth.coefficients = coefficients;
    }

    // Capsules along the thighs and shins, which the cloth can't pass through.
    private void HoldOffLegs()
    {
        Animator animator = GetComponentInParent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        float scale = hips != null && head != null ? Mathf.Clamp(Vector3.Distance(hips.position, head.position) / 0.75f, 0.5f, 2f) : 1f;
        var capsules = new List<CapsuleCollider>();
        foreach ((HumanBodyBones from, HumanBodyBones to, float share) in Legs)
        {
            Transform bone = animator.GetBoneTransform(from), end = animator.GetBoneTransform(to);
            if (bone == null || end == null)
            {
                continue;
            }

            var holder = new GameObject("Cloth Leg");
            holder.transform.SetParent(bone, false);
            Vector3 along = bone.InverseTransformPoint(end.position);
            var capsule = holder.AddComponent<CapsuleCollider>();
            // Only the cloth's: a trigger takes no part in blows, footing or the world's physics.
            capsule.isTrigger = true;
            float radius = thighRadius * share * scale / Mathf.Max(0.001f, bone.lossyScale.x);
            capsule.radius = radius;
            capsule.center = along * 0.5f;
            capsule.direction = Axis(along);
            capsule.height = along.magnitude + radius * 2f;
            capsules.Add(capsule);
        }

        cloth.capsuleColliders = capsules.ToArray();
    }

    private static int Axis(Vector3 along)
    {
        Vector3 a = new Vector3(Mathf.Abs(along.x), Mathf.Abs(along.y), Mathf.Abs(along.z));
        return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
    }

    // At rest out of sight and far off: the cost is one creature's, and only while it is seen.
    private void Update()
    {
        if (cloth == null || Time.time < nextLook)
        {
            return;
        }

        nextLook = Time.time + 0.5f;
        Camera view = Camera.main;
        bool near = view == null || (view.transform.position - transform.position).sqrMagnitude < restBeyond * restBeyond;
        bool wanted = near && skin.isVisible;
        if (cloth.enabled != wanted)
        {
            cloth.enabled = wanted;
        }
    }
}
