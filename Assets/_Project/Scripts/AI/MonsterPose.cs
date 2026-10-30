using UnityEngine;

/// <summary>
/// The body language that makes a monster's attacks readable from above (AttackPose): coiling through the wind-up,
/// snapping into the hit, swaying while staggered; plus the hover of flying monsters. It moves only the "Model" child.
/// Plain C#, owned by a MonsterAI.
/// </summary>
public sealed class MonsterPose
{
    private readonly Transform visual;
    private readonly Vector3 homePosition;
    private readonly Quaternion homeRotation;
    private readonly Vector3 homeScale;

    private float hover;
    // The attack whose pose is playing (it keeps settling after the attack ends) and the stagger's timing.
    private AttackDefinition posedAttack;
    private float posedAttackStart;
    private float staggerStart = -1f;
    private float staggerLength;

    public MonsterPose(Transform visual)
    {
        this.visual = visual;
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

    /// <param name="hoverHeight">How high it floats now (0 on the ground: not a flyer, exposed or dead).</param>
    /// <param name="charging">Its attack is a charge, which moves the whole body after the wind-up, so it only coils.</param>
    public void Update(float hoverHeight, bool dead, bool charging)
    {
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

        float width = 1f / Mathf.Sqrt(Mathf.Max(0.5f, pose.Height));
        visual.localPosition = homePosition + Vector3.up * hover + Vector3.forward * pose.Shift;
        visual.localRotation = Quaternion.Euler(-pose.Lean, 0f, pose.Sway) * homeRotation;
        visual.localScale = Vector3.Scale(homeScale, new Vector3(width, pose.Height, width));
    }
}
