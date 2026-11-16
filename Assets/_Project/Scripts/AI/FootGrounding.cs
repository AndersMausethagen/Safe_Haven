using UnityEngine;

/// <summary>
/// Keeps a humanoid creature's feet on the ground (2026-11-14: "the enemies float above ground"). Humanoid clips place a
/// body by its foot bones, and on many of these rigs the foot bone sits well above the sole, or (the forest-troll rig: the
/// Small Moss Troll, the Tusse) below it, so the body hung 0.1-0.7 m in the air, by an amount that changed with the clip.
/// After the animation each frame, this finds the lower sole (each foot bone less its height over the sole, measured once
/// in the editor) and eases the body (its Model, where this sits) down or up so that sole meets the ground the creature
/// stands on. It lets go while the creature hops, holds still while it rests or dies, and never moves the body more than Most Shift.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(Animator))]
[OnAFriendsCopy(CopyRule.Same, "puts the copy's feet on its ground")]
public sealed class FootGrounding : MonoBehaviour
{
    [Tooltip("Height of each foot bone over its sole in the model's rest pose, in metres at the body's size (negative: the " +
             "bone is below the sole). Measured by the editor tool; set by hand only to tune.")]
    [SerializeField] private float leftSole;
    [SerializeField] private float rightSole;
    [Tooltip("The most the body is moved from where the prefab puts it, in metres.")]
    [SerializeField, Min(0f)] private float mostShift = 1f;
    [Tooltip("How quickly it settles: higher follows each step more tightly.")]
    [SerializeField, Min(0.1f)] private float settle = 12f;

    private Animator animator;
    private Transform left, right;
    // Each sole as a point fixed to its foot bone, so it turns with the foot (a heel strike, a toe-off, a creature
    // curled up asleep) instead of always being straight below the bone.
    private Vector3 leftSolePoint, rightSolePoint;
    private MonsterAI monster;
    private Transform home;
    private Vector3 rest;
    private float shift;
    // What this wrote last frame, and the body's place before it: when nothing else has moved the body since (no
    // MonsterPose), the correction is taken off again before the feet are read, so it never piles up.
    private Vector3 lastWritten, lastBase;
    private bool wrote;

    public void Configure(float leftSoleHeight, float rightSoleHeight)
    {
        leftSole = leftSoleHeight;
        rightSole = rightSoleHeight;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        monster = GetComponentInParent<MonsterAI>();
        home = transform.parent;
        rest = transform.localPosition;
        if (animator.isHuman)
        {
            left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            // The bones still stand in the rest pose the soles were measured in (nothing has animated them yet).
            if (left != null && right != null)
            {
                leftSolePoint = left.InverseTransformVector(Vector3.down * (leftSole * Scale()));
                rightSolePoint = right.InverseTransformVector(Vector3.down * (rightSole * Scale()));
            }
        }
    }

    // Runs after MonsterAI's LateUpdate, whose body language (MonsterPose) places the body each frame; this adds its
    // correction on top of that, so the pose's recoil, lean and lift (a flyer's hover, a leap) all still show.
    private void LateUpdate()
    {
        if (left == null || right == null || home == null || transform.parent != home)
        {
            return;
        }

        Vector3 placed = wrote && transform.localPosition == lastWritten ? lastBase : transform.localPosition;
        transform.localPosition = placed;

        bool dead = monster != null && monster.CurrentState == MonsterAI.State.Dead;
        bool free = !animator.enabled || monster == null || monster.CurrentState == MonsterAI.State.Hopping;
        float target = 0f;
        if (dead || (monster != null && monster.IsResting))
        {
            // Dead: it falls where it stood, and the death clip or the ragdoll takes it from there. Resting (grazing,
            // asleep): a curled-up body doesn't rest on its soles, and the rest clip is made to the standing pose, so it
            // keeps the standing correction.
            target = shift;
        }
        else if (!free)
        {
            float parentScale = Mathf.Max(1e-4f, home.lossyScale.y);
            float lift = (placed.y - rest.y) * parentScale;
            float sole = Mathf.Min(left.TransformPoint(leftSolePoint).y, right.TransformPoint(rightSolePoint).y);
            float ground = home.position.y + lift;
            target = Mathf.Clamp(ground - sole, -mostShift, mostShift * 0.25f);
        }

        shift = Mathf.Lerp(shift, target, 1f - Mathf.Exp(-settle * Time.deltaTime));
        lastBase = placed;
        lastWritten = placed + Vector3.up * (shift / Mathf.Max(1e-4f, home.lossyScale.y));
        transform.localPosition = lastWritten;
        wrote = true;
    }

    // The soles were measured at the prefab's size; a creature spawned bigger or smaller (size variation) scales them.
    private float Scale() => transform.lossyScale.y / Mathf.Max(1e-4f, measuredScale);

    [SerializeField, HideInInspector] private float measuredScale = 1f;

    /// <summary>Records the scale the soles were measured at (editor tool).</summary>
    public void MeasuredAt(float lossyScaleY) => measuredScale = lossyScaleY;
}
