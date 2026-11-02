using UnityEngine;

/// <summary>
/// The body language that makes a monster's attacks readable from above (AttackPose): coiling through the wind-up,
/// snapping into the hit, swaying while staggered; recoiling from blows it takes (tipping away from the hit, shoved and
/// squashed, then springing back); plus the hover of flying monsters. It moves only the "Model" child.
/// Plain C#, owned by a MonsterAI.
/// </summary>
public sealed class MonsterPose
{
    private readonly Transform visual;
    private readonly Transform home;
    private readonly Vector3 homePosition;
    private readonly Quaternion homeRotation;
    private readonly Vector3 homeScale;

    private float hover;
    // The attack whose pose is playing (it keeps settling after the attack ends) and the stagger's timing.
    private AttackDefinition posedAttack;
    private float posedAttackStart;
    private float staggerStart = -1f;
    private float staggerLength;
    // The last blow taken: the way it travelled (in the monster's own space), how hard (0-1) and when.
    private Vector3 recoilDirection;
    private float recoilStrength;
    private float recoilStart = -1f;

    public MonsterPose(Transform visual)
    {
        this.visual = visual;
        home = visual.parent;
        homePosition = visual.localPosition;
        homeRotation = visual.localRotation;
        homeScale = visual.localScale;
    }

    public void Attack(AttackDefinition attack, float start)
    {
        posedAttack = attack;
        posedAttackStart = start;
    }

    /// <summary>An attack broken off before it struck: no follow-through.</summary>
    public void CancelAttack()
    {
        posedAttack = null;
    }

    public void Stagger(float start, float length)
    {
        staggerStart = start;
        staggerLength = length;
        posedAttack = null;
    }

    /// <summary>A blow travelling along <paramref name="worldDirection"/> lands, <paramref name="strength"/> 0-1 (1 for a
    /// strong one): the body tips and is shoved that way, then springs back.</summary>
    public void Hit(Vector3 worldDirection, float strength, float start)
    {
        Transform space = visual.parent;
        Vector3 local = space != null ? space.InverseTransformDirection(worldDirection) : worldDirection;
        local.y = 0f;
        if (local.sqrMagnitude < 1e-4f)
        {
            return;
        }

        recoilDirection = local.normalized;
        recoilStrength = Mathf.Clamp01(strength);
        recoilStart = start;
    }

    /// <summary>The recoil's size at <paramref name="t"/> (0-1 through it): a snap out, back with a small overshoot.</summary>
    public static float RecoilCurve(float t) => t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t);

    /// <param name="hoverHeight">How high it floats now (0 on the ground: not a flyer, exposed or dead).</param>
    /// <param name="charging">Its attack is a charge, which moves the whole body after the wind-up, so it only coils.</param>
    /// <param name="leap">How high a leaping charge has the body, in metres.</param>
    public void Update(float hoverHeight, bool dead, bool charging, float leap = 0f)
    {
        // The body has gone to physics (DeathRagdoll).
        if (visual == null || visual.parent != home)
        {
            return;
        }

        float targetHover = hoverHeight <= 0f ? 0f : hoverHeight + Mathf.Sin(Time.time * 3f) * 0.08f;
        hover = Mathf.Lerp(hover, targetHover, Time.deltaTime * 10f);

        AttackPose.Pose pose = AttackPose.Pose.Rest;
        if (dead)
        {
            posedAttack = null;
        }
        else if (staggerStart >= 0f && Time.time - staggerStart < staggerLength)
        {
            pose = AttackPose.Stagger(Time.time - staggerStart, staggerLength);
        }
        else if (posedAttack != null)
        {
            float elapsed = Time.time - posedAttackStart;
            if (elapsed > posedAttack.HitDelay + AttackPose.StrikeSeconds + AttackPose.SettleSeconds)
            {
                posedAttack = null;
            }
            else
            {
                pose = charging && elapsed >= posedAttack.HitDelay ? AttackPose.Pose.Rest : AttackPose.Attack(elapsed, posedAttack.HitDelay);
            }
        }

        float recoil = 0f;
        if (recoilStart >= 0f)
        {
            CombatFeelSettings feel = CombatFeelSettings.Current;
            float t = (Time.time - recoilStart) / feel.HitRecoilSeconds;
            recoil = RecoilCurve(t) * recoilStrength;
            if (t >= 1f)
            {
                recoilStart = -1f;
            }
        }

        CombatFeelSettings settings = recoil != 0f ? CombatFeelSettings.Current : null;
        // Tipped about the axis across the blow, so the top goes the way the blow went; squashed as the punch was.
        Quaternion tip = recoil != 0f
            ? Quaternion.AngleAxis(recoil * settings.HitRecoilDegrees, Vector3.Cross(Vector3.up, recoilDirection))
            : Quaternion.identity;
        Vector3 shove = recoil != 0f ? recoilDirection * (recoil * settings.HitRecoilShove) : Vector3.zero;
        float squash = recoil != 0f ? recoil * settings.PunchAmount : 0f;

        float width = 1f / Mathf.Sqrt(Mathf.Max(0.5f, pose.Height));
        visual.localPosition = homePosition + Vector3.up * (hover + leap) + Vector3.forward * pose.Shift + shove;
        visual.localRotation = tip * Quaternion.Euler(-pose.Lean, 0f, pose.Sway) * homeRotation;
        visual.localScale = Vector3.Scale(homeScale, new Vector3(width * (1f + squash), pose.Height * (1f - squash), width * (1f + squash)));
    }
}
