using UnityEngine;

/// <summary>
/// Whom a monster fights: who it may target (MonsterTargeting, FactionRelations), switching to a nearer one, being
/// pointed at one, and where and how far its target is. Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    // While hunting one target, looks for a better one it can see (MonsterTargeting: the player first while reachable).
    private void ScanForNearerTarget()
    {
        // A wall-breaker at a wall or a building stays at it until someone makes it angry (HomeAssault).
        if (definition.BreaksWalls && !IsProvoked && target is Damageable structure && structure != null
            && structure.Category == TargetCategory.Structure && structure.IsAlive && !structure.IsBroken)
        {
            return;
        }

        if (targeting.TryFindBetter(target, DistanceToTarget(), senses, home, Relentless, out IDamageable better, out Collider betterCollider))
        {
            SetTarget(better, betterCollider);
        }
    }

    private bool IsAvoided(Vector3 position) => targeting.IsAvoided(position);

    private bool CanTarget(IDamageable candidate) => targeting.CanTarget(candidate);

    /// <summary>Starts chasing a target right away, wherever it is. Returns false when it can't target it.</summary>
    public bool Engage(IDamageable newTarget)
    {
        if (definition == null || CurrentState == State.Dead || !CanTarget(newTarget))
        {
            return false;
        }

        SetTarget(newTarget, null);
        return true;
    }

    private void SetTarget(IDamageable newTarget, Collider hitCollider)
    {
        target = newTarget;
        targetComponent = (Component)newTarget;
        targetCollider = hitCollider != null ? hitCollider : targetComponent.GetComponentInChildren<Collider>();
        movement.RepathNow();
        lastSeenTargetAt = Time.time;
        lastKnownTargetPosition = targetComponent.transform.position;
        searchingLostTarget = false;
        if (senses != null)
        {
            bool fresh = !senses.Awareness.IsAlerted;
            senses.Awareness.Alert(lastKnownTargetPosition);
            if (fresh)
            {
                OnSpotted();
            }
        }

        RefreshAmbush();
        if (pack != null)
        {
            pack.Alert(newTarget);
        }
    }

    private void ClearTarget()
    {
        target = null;
        targetComponent = null;
        targetCollider = null;
    }

    private Vector3 TargetPoint()
    {
        if (targetCollider != null && targetCollider.enabled)
        {
            return HitDetector.NearestPoint(targetCollider, transform.position + Vector3.up * CenterHeight);
        }

        return targetComponent.transform.position;
    }

    private float DistanceToTarget()
    {
        return targetComponent != null ? HorizontalDistance(TargetPoint()) : float.MaxValue;
    }

    private float HorizontalDistance(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    /// <summary>Something has hurt it lately (Perception › Provoked Seconds).</summary>
    public bool IsProvoked => Time.time < provokedUntil;
}
