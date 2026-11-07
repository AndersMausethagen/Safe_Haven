using UnityEngine;

/// <summary>
/// The finishing blow: a light or rushing swing on a staggered creature from behind carries the player to it and
/// strikes it down (MeleeTargeting finds it). Part of PlayerCombat, in its own file.
/// </summary>
public sealed partial class PlayerCombat
{
    private MonsterAI FindFinishable() =>
        weapon == null || weapon.RushAttack == null ? null : MeleeTargeting.FindFinishable(transform.position, Rules);

    private void BeginFinish(MonsterAI target, float hitAt)
    {
        finishing = target;
        finishFrom = transform.position;
        finishEndsAt = Time.time + hitAt + Rules.FinishHoldSeconds;
        // Past each other's bodies for the stab, and not to be knocked out of it before the blade lands.
        finishingCollider = target.GetComponentInChildren<Collider>();
        if (finishingCollider != null && controller != null)
        {
            Physics.IgnoreCollision(controller, finishingCollider, true);
        }

        if (health != null)
        {
            health.AddInvulnerability(this);
        }

        target.HoldForFinish(hitAt + Rules.FinishHoldSeconds + Rules.FinishCloseSeconds, hitAt);
        FinishStarted?.Invoke(target);
    }

    private void UpdateFinish()
    {
        if (finishing == null)
        {
            return;
        }

        if (Time.time >= finishEndsAt)
        {
            EndFinish();
            return;
        }

        CombatTechniqueSettings rules = Rules;
        Vector3 spot = StrikeRules.FinishSpot(finishing.transform.position, finishing.transform.forward, rules.FinishGap);
        float share = Mathf.Clamp01((Time.time - activeStartTime) / rules.FinishCloseSeconds);
        Vector3 wanted = Vector3.Lerp(finishFrom, spot, share * share * (3f - 2f * share));
        Vector3 move = wanted - transform.position;
        move.y = 0f;
        if (controller != null && controller.enabled)
        {
            controller.Move(move);
        }

        movement.SnapFacing(finishing.transform.forward);
    }

    private void EndFinish()
    {
        if (finishingCollider != null && controller != null)
        {
            Physics.IgnoreCollision(controller, finishingCollider, false);
        }

        finishingCollider = null;
        finishing = null;
        if (health != null)
        {
            health.RemoveInvulnerability(this);
        }
    }
}
