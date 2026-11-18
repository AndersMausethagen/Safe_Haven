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
        finishUnderWay = true;
        finishFrom = transform.position;
        finishEndsAt = Time.time + hitAt + Rules.FinishHoldSeconds;
        // Past each other's bodies for the stab, and not to be knocked out of it before the blade lands.
        finishingCollider = target.GetComponentInChildren<Collider>();
        if (finishingCollider != null && controller != null)
        {
            BodyPassThrough.Ignore(controller, finishingCollider);
        }

        if (health != null)
        {
            health.AddInvulnerability(FinishKey);
        }

        target.HoldForFinish(hitAt + Rules.FinishHoldSeconds + Rules.FinishCloseSeconds, hitAt);
        FinishStarted?.Invoke(target);
    }

    private void UpdateFinish()
    {
        if (!finishUnderWay)
        {
            return;
        }

        // Done, or the creature is gone mid-finish (destroyed: a friend's copy taken away, a thief fled): it ends either way, or the
        // invulnerability it gave would stay for good.
        if (finishing == null || Time.time >= finishEndsAt)
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

    // Its own key: the heavy-hit and knockdown safety use the combat's, and ending a swing must not strip those.
    private static readonly LeaseKey FinishKey = new LeaseKey("a finishing blow");

    // True from a finishing blow's start until EndFinish, even if the creature is destroyed meanwhile (then "finishing" reads null).
    private bool finishUnderWay;

    private void EndFinish()
    {
        if (finishingCollider != null && controller != null)
        {
            BodyPassThrough.Restore(controller, finishingCollider);
        }

        // Not "finishing != null": a creature destroyed mid-finish reads as null, and its invulnerability stayed.
        bool wasFinishing = finishUnderWay;
        finishUnderWay = false;
        finishingCollider = null;
        finishing = null;
        if (wasFinishing && health != null)
        {
            health.RemoveInvulnerability(FinishKey);
        }
    }
}
