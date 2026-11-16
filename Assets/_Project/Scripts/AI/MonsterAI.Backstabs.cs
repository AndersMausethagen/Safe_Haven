using UnityEngine;

/// <summary>
/// How a monster answers being backstabbed, and what it has learned of it: a strike, a hop or a parry. Part of the state machine, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    // ----- Backstabs -----

    // It remembers, and so do those who saw; if it lives the blow has knocked it out of its strike, and it will answer.
    private void OnBackstabbed(bool killed)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        backstabs.Learn(Time.time, rules.BackstabForgotAfter);
        foreach (MonsterAI other in active)
        {
            if (other != this && other.combatant.Faction == combatant.Faction
                && (other.transform.position - transform.position).sqrMagnitude <= rules.BackstabSeenWithin * rules.BackstabSeenWithin)
            {
                other.backstabs.Learn(Time.time, rules.BackstabForgotAfter);
            }
        }

        if (killed)
        {
            return;
        }

        if (CurrentState == State.Attacking)
        {
            attacks.Cancel();
            pose?.CancelAttack();
            SetState(State.Chasing);
        }

        answerAt = Time.time + rules.AnswerDelay;
    }

    // A strike at once, with its wind-up as usual, so it can still be read and dodged; attacks turn it as they wind up. Without a strike
    // that reaches, or some of the time anyway, it hops back. False when there is nobody near enough to answer.
    private bool AnswerBackstab()
    {
        if (!CanTarget(target))
        {
            return false;
        }

        Vector3 toTarget = FlatToTarget();
        float distance = DistanceToTarget();
        if (distance > CombatTechniqueSettings.Current.ReadRange)
        {
            return false;
        }

        int action = definition.Attacks.Count > 0 ? attacks.ChooseAnswer(distance) : -1;
        bool canStrike = definition.Attacks.Count > 0 ? action >= 0 : definition.Attack != null && distance <= definition.Attack.Range;
        if (canStrike && UnityEngine.Random.value < CombatTechniqueSettings.Current.AnswerStrikeChance)
        {
            movement.Stop();
            BeginAttack(action, toTarget);
            return true;
        }

        BeginHop(toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward);
        return true;
    }

    // One that remembers watches for a swing from behind while it strikes. Each swing gets one chance to be seen, and what it sees it
    // breaks off for. True when it did. Being staggered ends this, so a parry stays the sure way in.
    private bool SeesBackstabComing()
    {
        float chance = BackstabReadChance;
        if (chance <= 0f || !OpenWhileStriking || global::Player.Current == null
            || !global::Player.Current.TryGetComponent(out PlayerCombat swinger) || !swinger.IsWindingUp
            || Mathf.Approximately(swinger.SwingStartTime, lastSwingWatched))
        {
            return false;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        Vector3 position = swinger.transform.position;
        if (HorizontalDistance(position) > rules.ReadRange || !StrikeRules.IsBehind(transform.position, transform.forward, position, rules.BackstabArc))
        {
            return false;
        }

        lastSwingWatched = swinger.SwingStartTime;
        if (UnityEngine.Random.value >= chance)
        {
            return false;
        }

        attacks.Cancel();
        pose?.CancelAttack();
        SetState(State.Chasing);
        EnsureIndicator()?.ShowSpotted();
        AudioManager.Play(PerceptionSettings.Current.SpottedSound, transform.position);
        if (definition.ParriesBackstabs && UnityEngine.Random.value < rules.ReadParryChance)
        {
            movement.Stop();
            parryUntil = Time.time + rules.ReadParrySeconds;
            return true;
        }

        // Off to one side of the way the blow is coming.
        Vector3 away = transform.position - position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;
        BeginHop((away + Vector3.Cross(Vector3.up, away) * (UnityEngine.Random.value < 0.5f ? -1f : 1f)).normalized);
        return true;
    }

    private void BeginHop(Vector3 direction)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        movement.Stop();
        hopDirection = direction;
        hopEndsAt = Time.time + rules.HopSeconds;
        damageable.AddInvulnerability(this);
        pose?.Hit(direction, 0.6f, Time.time);
        SetState(State.Hopping);
    }

    private void UpdateHop()
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        if (targetComponent != null)
        {
            movement.TurnTowards(FlatToTarget());
        }

        if (!movement.EnsureOnNavMesh() || Time.time >= hopEndsAt
            || movement.Rush(hopDirection, rules.HopDistance / rules.HopSeconds * Time.deltaTime))
        {
            attacks.DelayAtLeast(0.2f);
            // Leaving the hop gives back its invulnerability (SetState).
            SetState(State.Chasing);
        }
    }

    private Vector3 FlatToTarget()
    {
        Vector3 toTarget = TargetPoint() - transform.position;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude > 0.0001f ? toTarget : transform.forward;
    }
}
