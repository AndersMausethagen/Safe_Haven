using UnityEngine;

/// <summary>
/// What a creature shows of itself in a fight, so it can be read (Docs/EnemyCombatResearch.md, phases A and B): reeling when its poise
/// is worn low, waiting its turn (a ready stance), the opening after its blow, a strike broken off, drawing its weapon when it spots
/// someone, wavering when one of its side is killed brutally, and what its foe is visibly doing, which it can choose a strike against.
/// Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    // How long one sign of waiting its turn lasts: Engage marks it every frame it waits.
    private const float WaitingMarkSeconds = 0.3f;
    // A guard counts as raised against it once it has been up this long.
    private const float GuardReadSeconds = 0.8f;
    // A foe moving away from it faster than this (m/s) is backing off.
    private const float BackingOffSpeed = 0.8f;
    // A foe further round than this from where it faces is at its flank.
    private const float FlankDegrees = 100f;
    // A brutal kill (a finishing blow, a staggered backstab) makes ordinary creatures this near waver for this long.
    private const float WaverRadius = 8f;
    private const float WaverSeconds = 0.6f;
    // Summons run this long when the one who called them falls.
    private const float SummonRoutSeconds = 4f;

    private float waitingUntil = float.NegativeInfinity;
    private float drawingUntil = float.NegativeInfinity;
    private bool remoteWaiting;
    private ReelingTell reelingTell;

    /// <summary>Raised when a blow or a parry breaks off its strike before it lands (an interrupt, a recoil): the body shows it.</summary>
    public event System.Action Interrupted;

    /// <summary>Raised when it wavers (one of its side killed brutally nearby): the body flinches back.</summary>
    public event System.Action Wavered;

    /// <summary>Raised when it stops to draw its weapon on spotting someone (Monster Definition › Spotted Pause).</summary>
    public event System.Action DrawStarted;

    /// <summary>Its poise is worn low (under Combat Technique Settings › Reeling Share): a stagger is near. A look only.</summary>
    public bool IsReeling => poise != null && CurrentState != State.Staggered && CurrentState != State.Dead
        && poise.Fraction < CombatTechniqueSettings.Current.ReelingShare;

    /// <summary>True while it waits its turn at its target: circling, standing ready, or holding its aim or its distance.</summary>
    public bool IsWaitingTurn => CurrentState == State.Chasing && (IsRemote ? remoteWaiting : Time.time < waitingUntil);

    /// <summary>True while it draws its weapon on spotting someone (Monster Definition › Spotted Pause).</summary>
    public bool IsDrawing => Time.time < drawingUntil;

    /// <summary>The opening after its blow: true while its strike has spent itself and it can't act yet, with the seconds left and in all.</summary>
    public bool TryGetOpening(out float left, out float length)
    {
        left = length = 0f;
        return CurrentState == State.Attacking && attacks != null && attacks.TryGetOpening(out left, out length);
    }

    private void MarkWaiting() => waitingUntil = Time.time + WaitingMarkSeconds;

    private void RaiseInterrupted() => Interrupted?.Invoke();

    // Spotted while it stood unaware (idle or looking round): it stops a beat to ready its weapon, a tell that it has seen the player
    // and a moment for the first blow.
    private void BeginDrawIfUnaware()
    {
        if (IsRemote || definition == null || definition.SpottedPause <= 0f
            || (CurrentState != State.Idle && CurrentState != State.Investigating))
        {
            return;
        }

        drawingUntil = Time.time + definition.SpottedPause;
        attacks?.DelayAtLeast(definition.SpottedPause);
        DrawStarted?.Invoke();
    }

    // Drawing its weapon: it stands and turns to its foe until done. True while it does.
    private bool HoldsWhileDrawing(Vector3 toTarget)
    {
        if (!IsDrawing)
        {
            return false;
        }

        SetState(State.Chasing);
        movement.Stop();
        movement.TurnTowards(toTarget);
        return true;
    }

    /// <summary>What its target is visibly doing right now, for choosing a strike against it (Monster Attack › When Foe).</summary>
    private FoeDoing ReadFoe(Vector3 toTarget)
    {
        if (targetComponent == null)
        {
            return FoeDoing.None;
        }

        FoeDoing doing = FoeDoing.None;
        Transform foe = targetComponent.transform;
        Vector3 away = toTarget;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f)
        {
            away.Normalize();
            if (Vector3.Dot(attacks.TargetVelocity, away) > BackingOffSpeed)
            {
                doing |= FoeDoing.BackingOff;
            }

            if (Vector3.Angle(transform.forward, away) > FlankDegrees)
            {
                doing |= FoeDoing.Behind;
            }
        }

        if (foe.TryGetComponent(out PlayerGuard guard) && guard.IsGuarding && guard.GuardedFor >= GuardReadSeconds
            && Vector3.Angle(foe.forward, transform.position - foe.position) < 60f)
        {
            doing |= FoeDoing.Guarding;
        }

        if (foe.TryGetComponent(out PlayerArchery archery) && (archery.IsDrawing || archery.IsAiming))
        {
            doing |= FoeDoing.Shooting;
        }

        return doing;
    }

    /// <summary>
    /// Shaken by a brutal kill nearby: it falters a moment before it strikes again, flinching back from where it happened. A strike
    /// already under way goes on. Great beasts, elites, bosses and those sent on a mission don't falter.
    /// </summary>
    public void Waver(Vector3 from, float seconds)
    {
        if (definition == null || IsRemote || CurrentState == State.Dead || IsHeavyweight || Relentless || seconds <= 0f)
        {
            return;
        }

        attacks.DelayAtLeast(seconds);
        Vector3 back = transform.position - from;
        back.y = 0f;
        pose?.Hit(back.sqrMagnitude > 0.0001f ? back.normalized : -transform.forward, 0.5f, Time.time);
        Wavered?.Invoke();
    }

    // It fell: what it called up runs a while, and a brutal end shakes those of its side who saw it.
    private void ShakeTheOthers(in DamageInfo killingBlow, bool wasStaggered)
    {
        if (IsRemote)
        {
            return;
        }

        attacks?.RoutSummoned(SummonRoutSeconds);
        bool brutal = (killingBlow.Technique & HitTechnique.Finisher) != 0
            || ((killingBlow.Technique & HitTechnique.Backstab) != 0 && wasStaggered);
        if (!brutal || !TryGetComponent(out Combatant mine))
        {
            return;
        }

        foreach (MonsterAI other in active)
        {
            if (other != this && other.CurrentState != State.Dead && other.TryGetComponent(out Combatant side) && side.Faction == mine.Faction
                && (other.transform.position - transform.position).sqrMagnitude <= WaverRadius * WaverRadius)
            {
                other.Waver(transform.position, WaverSeconds);
            }
        }
    }

    // Body language after movement: the sway and embers of worn-down poise.
    private void UpdateTells()
    {
        if (reelingTell == null)
        {
            return;
        }

        UpdateHurt();
        reelingTell.Update(IsReeling, Time.deltaTime);
        if (pose != null)
        {
            pose.Reeling = reelingTell.Amount;
        }
    }
}
