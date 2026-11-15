using UnityEngine;

/// <summary>
/// The struck body shudders for the moment a blow holds (Sakurai's hit stop: the victim shakes side to side while the moment stops,
/// shrinking as it ends), and where time can't stop for everyone (hosting co-op: GamePause.TimeIsShared) its animator holds still
/// instead, the attacker's slows, so a blow still lands with weight. Visual only: the shake moves the hips bone after the animator has
/// posed it, so nothing builds up from frame to frame, and the body's collider never moves. Added to a body the first time it is struck.
/// </summary>
public sealed class HitShake : MonoBehaviour
{
    // Side-to-side shakes per second of real time.
    private const float Frequency = 55f;

    private Animator animator;
    private Transform bone;
    private float startedAt;
    private float until = -1f;
    private float strength;
    private float heldUntil = -1f;
    private float speedBefore = 1f;
    // What this wrote: only that is put back, so a speed something else set meanwhile (a creature's held wind-up ending) stands.
    private float heldSpeed = -1f;

    /// <summary>Shakes <paramref name="victim"/>'s body for <paramref name="seconds"/> of real time by up to <paramref name="metres"/>;
    /// with <paramref name="holdAnimator"/>, its animator stands still as long.</summary>
    public static void Play(Component victim, float seconds, float metres, bool holdAnimator)
    {
        if (victim == null || seconds <= 0f)
        {
            return;
        }

        if (!victim.TryGetComponent(out HitShake shake))
        {
            shake = victim.gameObject.AddComponent<HitShake>();
        }

        shake.Begin(seconds, metres, holdAnimator);
    }

    /// <summary>Slows <paramref name="animator"/> to <paramref name="speed"/> for <paramref name="seconds"/> of real time (the attacker's
    /// side of a co-op hit stop).</summary>
    public static void Slow(Animator animator, float seconds, float speed)
    {
        if (animator == null || seconds <= 0f)
        {
            return;
        }

        if (!animator.TryGetComponent(out HitShake shake))
        {
            shake = animator.gameObject.AddComponent<HitShake>();
        }

        shake.Hold(seconds, speed);
    }

    private void Begin(float seconds, float metres, bool holdAnimator)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            bone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        }

        bool shaking = Time.unscaledTime < until;
        startedAt = Time.unscaledTime;
        until = Mathf.Max(shaking ? until : -1f, startedAt + seconds);
        strength = shaking ? Mathf.Max(metres, strength) : metres;
        if (holdAnimator)
        {
            Hold(seconds, 0f);
        }
    }

    private void Hold(float seconds, float speed)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            return;
        }

        if (heldUntil < 0f)
        {
            speedBefore = animator.speed;
        }

        heldUntil = Mathf.Max(heldUntil, Time.unscaledTime + seconds);
        animator.speed = speed;
        heldSpeed = speed;
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (heldUntil >= 0f && now >= heldUntil)
        {
            heldUntil = -1f;
            if (animator != null && Mathf.Approximately(animator.speed, heldSpeed))
            {
                animator.speed = speedBefore;
            }
        }

        if (bone == null || now >= until)
        {
            return;
        }

        float left = Mathf.Clamp01((until - now) / Mathf.Max(0.01f, until - startedAt));
        bone.position += transform.right * (Mathf.Sin((now - startedAt) * Frequency * 2f * Mathf.PI) * strength * left);
    }

    private void OnDisable()
    {
        if (heldUntil >= 0f && animator != null && Mathf.Approximately(animator.speed, heldSpeed))
        {
            animator.speed = speedBefore;
        }

        heldUntil = -1f;
        until = -1f;
    }
}
