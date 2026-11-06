using UnityEngine;

/// <summary>
/// Footprints for people other than the player (residents, companions): a print every half stride walked, left and
/// right in turn, at the foot bone when the body is humanoid (else beside the middle). They go into the terrain's
/// footprint field only where the player's own prints are kept (TerrainFootprints.StampInWindow), so they cost nothing
/// out of sight. Added in code by the characters that walk (see <see cref="AddTo(GameObject)"/>): people, and wild
/// animals and creatures with their own strides, so their tracks show in snow and can be followed.
/// </summary>
[DisallowMultipleComponent]
public sealed class WalkerFootprints : MonoBehaviour
{
    [Tooltip("Metres walked between two prints (one foot, then the other).")]
    [SerializeField, Min(0.2f)] private float stepMeters = 0.7f;
    [Tooltip("How far each foot is from the middle when there are no foot bones.")]
    [SerializeField, Min(0f)] private float footSpacing = 0.12f;

    private Animator animator;
    private Transform leftFoot;
    private Transform rightFoot;
    private Vector3 previousPosition;
    private float travelled;
    private bool left;

    /// <summary>Adds footprints to a walking character once.</summary>
    public static void AddTo(GameObject walker)
    {
        if (walker != null && !walker.TryGetComponent(out WalkerFootprints _))
        {
            walker.AddComponent<WalkerFootprints>();
        }
    }

    /// <summary>Adds prints to an animal or creature: shorter or longer strides than a person's, feet further apart.</summary>
    public static void AddTo(GameObject walker, float step, float spacing)
    {
        if (walker == null)
        {
            return;
        }

        if (!walker.TryGetComponent(out WalkerFootprints prints))
        {
            prints = walker.AddComponent<WalkerFootprints>();
        }

        prints.stepMeters = Mathf.Max(0.2f, step);
        prints.footSpacing = Mathf.Max(0f, spacing);
    }

    private void OnEnable()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        previousPosition = transform.position;
        travelled = 0f;
    }

    private void Update()
    {
        Vector3 movement = transform.position - previousPosition;
        previousPosition = transform.position;
        movement.y = 0f;
        float distance = movement.magnitude;
        // Warps (a checkpoint, a resident sent home) are not steps.
        if (distance > stepMeters * 3f)
        {
            travelled = 0f;
            return;
        }

        travelled += distance;
        if (travelled < stepMeters)
        {
            return;
        }

        travelled %= stepMeters;
        left = !left;
        Vector3 forward = distance > 0.0001f ? movement / distance : transform.forward;
        Transform foot = left ? leftFoot : rightFoot;
        Vector3 at = foot != null
            ? foot.position
            : transform.position + Vector3.Cross(Vector3.up, forward) * (left ? -footSpacing : footSpacing);
        PlayerTerrainFootprints.TryStamp(at, forward, false);
    }
}
