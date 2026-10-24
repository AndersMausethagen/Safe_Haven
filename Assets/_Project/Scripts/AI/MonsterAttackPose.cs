using UnityEngine;

/// <summary>
/// How a monster's body moves around an attack, so the attack reads even without animations of its own: during the
/// wind-up it coils (leans back, draws back and crouches, slowly at first and faster near the end), at the hit it snaps
/// forward and stretches, then settles. While staggered it sways and sags. MonsterAI applies it to the monster's
/// Model; the numbers are deliberately broad, since the camera sees monsters from far above. Plain functions, tested.
/// </summary>
public static class AttackPose
{
    public const float StrikeSeconds = 0.12f;
    public const float SettleSeconds = 0.3f;

    public readonly struct Pose
    {
        /// <summary>Degrees the body pitches back (positive) or forward (negative).</summary>
        public readonly float Lean;
        /// <summary>Metres along the attack direction: negative draws back, positive lunges.</summary>
        public readonly float Shift;
        /// <summary>Height scale: below 1 crouches, above 1 stretches (width compensates).</summary>
        public readonly float Height;
        /// <summary>Degrees of side-to-side sway (staggered).</summary>
        public readonly float Sway;

        public Pose(float lean, float shift, float height, float sway)
        {
            Lean = lean;
            Shift = shift;
            Height = height;
            Sway = sway;
        }

        public static Pose Rest => new Pose(0f, 0f, 1f, 0f);
    }

    /// <summary>The pose <paramref name="elapsed"/> seconds into an attack whose hit lands at <paramref name="hitDelay"/>.</summary>
    public static Pose Attack(float elapsed, float hitDelay, float strength = 1f)
    {
        if (hitDelay <= 0f || elapsed < 0f)
        {
            return Pose.Rest;
        }

        if (elapsed < hitDelay)
        {
            float coil = elapsed / hitDelay;
            coil *= coil; // slow to start, committed at the end
            return new Pose(16f * coil * strength, -0.2f * coil * strength, 1f - 0.1f * coil * strength, 0f);
        }

        float after = elapsed - hitDelay;
        if (after < StrikeSeconds)
        {
            float snap = Mathf.Sin(after / StrikeSeconds * Mathf.PI * 0.5f);
            return new Pose(Mathf.Lerp(16f, -12f, snap) * strength, Mathf.Lerp(-0.2f, 0.3f, snap) * strength,
                Mathf.Lerp(0.9f, 1.06f, snap), 0f);
        }

        float settle = Mathf.Clamp01((after - StrikeSeconds) / SettleSeconds);
        float ease = 1f - (1f - settle) * (1f - settle);
        return new Pose(Mathf.Lerp(-12f, 0f, ease) * strength, Mathf.Lerp(0.3f, 0f, ease) * strength, Mathf.Lerp(1.06f, 1f, ease), 0f);
    }

    /// <summary>The pose <paramref name="elapsed"/> seconds into a stagger lasting <paramref name="seconds"/>.</summary>
    public static Pose Stagger(float elapsed, float seconds)
    {
        if (seconds <= 0f || elapsed < 0f || elapsed >= seconds)
        {
            return Pose.Rest;
        }

        float left = 1f - elapsed / seconds;
        float knock = Mathf.Clamp01(elapsed / 0.08f); // thrown back at once, recovering over the stagger
        return new Pose(14f * knock * left, -0.25f * knock * left, 1f - 0.08f * left, Mathf.Sin(elapsed * 13f) * 9f * left);
    }
}
