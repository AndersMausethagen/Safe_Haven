using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A practice duel with a companion (Eirik's lesson at the morning camp). For its length the companion's own mind is set
/// aside and this fights the player with the companion's body and attack: closes in, winds up slower than in a real fight
/// (a readable tell to parry or dodge), strikes through AttackResolver for half its weapon's damage, and keeps its distance
/// between blows. It counts as an enemy only while it lasts, so the player's blows land on it. Neither can die: both are
/// held at a health floor (Yield At), and whoever reaches it first yields. Then both are healed and the companion is
/// itself again. Started by the story graph's Spar node.
/// </summary>
public sealed class CompanionSpar : MonoBehaviour
{
    public enum Outcome { None, PlayerWon, CompanionWon, Stopped }

    // Slower than in battle, so the tell can be read; half the weapon's damage; a step back between blows.
    private const float ExtraWindUp = 0.35f;
    private const float DamageShare = 0.5f;
    private const float IntervalScale = 1.6f;
    private const float KeepAway = 1.4f;
    private const float TurnSpeed = 540f;
    private const float FloorMargin = 0.5f;

    private Companion companion;
    private CompanionDefinition definition;
    private CompanionBody body;
    private NavMeshAgent agent;
    private Combatant combatant;
    private Damageable self;
    private Damageable foe;
    private Faction faction;
    private bool wasSheathed;
    private float stoppingDistance;
    private float strikeAt = -1f;
    private float nextAttack;
    private bool restored;

    public Outcome Result { get; private set; }

    public bool IsOver => Result != Outcome.None;

    /// <summary>Starts a duel between <paramref name="who"/> and <paramref name="player"/>; whoever is worn down to
    /// <paramref name="yieldAt"/> of their health first yields. Null when the companion can't fight.</summary>
    public static CompanionSpar Begin(Companion who, Damageable player, float yieldAt)
    {
        if (who == null || player == null || who.Definition == null || who.Definition.Attack == null || who.Body == null ||
            !who.TryGetComponent(out Damageable health) || !who.TryGetComponent(out Combatant fighter))
        {
            return null;
        }

        var spar = who.gameObject.AddComponent<CompanionSpar>();
        spar.companion = who;
        spar.definition = who.Definition;
        spar.body = who.Body;
        spar.agent = who.GetComponent<NavMeshAgent>();
        spar.combatant = fighter;
        spar.self = health;
        spar.foe = player;
        spar.faction = fighter.Faction;
        spar.wasSheathed = who.Body.IsSheathed;
        if (spar.agent != null)
        {
            // Its usual stopping distance kept it just out of reach.
            spar.stoppingDistance = spar.agent.stoppingDistance;
            spar.agent.stoppingDistance = 0.1f;
        }
        float share = Mathf.Clamp(yieldAt, 0.05f, 0.9f);
        health.HoldFloor(spar, health.MaxHealth * share);
        player.HoldFloor(spar, player.MaxHealth * share);
        who.enabled = false;
        fighter.SetFaction(Faction.Enemy);
        who.Body.SetSheathed(false);
        spar.nextAttack = Time.time + 1f;
        return spar;
    }

    /// <summary>Ends the duel at once (the story moved on): nobody wins.</summary>
    public void Stop() => End(Outcome.Stopped);

    private void Update()
    {
        if (IsOver)
        {
            return;
        }

        if (foe == null || !foe.IsAlive || self == null || !self.IsAlive)
        {
            End(Outcome.Stopped);
            return;
        }

        if (self.CurrentHealth <= self.HealthFloor + FloorMargin)
        {
            End(Outcome.PlayerWon);
            return;
        }

        if (foe.CurrentHealth <= foe.HealthFloor + FloorMargin)
        {
            End(Outcome.CompanionWon);
            return;
        }

        Vector3 toFoe = foe.transform.position - transform.position;
        toFoe.y = 0f;
        float distance = toFoe.magnitude;
        Vector3 facing = distance > 0.01f ? toFoe / distance : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), TurnSpeed * Time.deltaTime);

        if (strikeAt >= 0f)
        {
            Halt();
            if (Time.time >= strikeAt)
            {
                Strike(facing);
            }

            return;
        }

        float reach = definition.ReachFor(0.4f);
        if (distance > reach + 0.15f)
        {
            Approach(foe.transform.position - facing * (reach * 0.7f));
        }
        else if (distance < KeepAway * 0.6f)
        {
            Approach(transform.position - facing * KeepAway);
        }
        else
        {
            Halt();
            if (Time.time >= nextAttack)
            {
                nextAttack = Time.time + definition.AttackInterval * IntervalScale;
                strikeAt = Time.time + definition.StrikeDelay + ExtraWindUp;
                body.PlayAttack();
            }
        }
    }

    private void Strike(Vector3 facing)
    {
        strikeAt = -1f;
        AttackResolver.Execute(combatant, definition.Attack, definition.WeaponDamage * DamageShare, transform.position, facing, ~0);
        AudioManager.Play(definition.AttackSound, transform.position);
    }

    private void Approach(Vector3 to)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(to);
            body.SetSpeed(agent.velocity.magnitude);
        }
    }

    private void Halt()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        body.SetSpeed(0f);
    }

    private void End(Outcome outcome)
    {
        if (IsOver)
        {
            return;
        }

        Result = outcome;
        Restore();
        if (outcome != Outcome.Stopped)
        {
            // A lesson, not a fight: both walk away whole.
            self.Heal(self.MaxHealth);
            foe.Heal(foe.MaxHealth);
        }

        Destroy(this);
    }

    private void Restore()
    {
        if (restored)
        {
            return;
        }

        restored = true;
        strikeAt = -1f;
        if (self != null)
        {
            self.ReleaseFloor(this);
        }

        if (foe != null)
        {
            foe.ReleaseFloor(this);
        }

        if (combatant != null)
        {
            combatant.SetFaction(faction);
        }

        if (body != null)
        {
            body.SetSpeed(0f);
            body.SetSheathed(wasSheathed);
        }

        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
        }

        if (companion != null)
        {
            companion.enabled = true;
        }
    }

    // Unloaded or removed mid-duel: the companion must not stay an enemy.
    private void OnDestroy()
    {
        if (Result == Outcome.None)
        {
            Result = Outcome.Stopped;
        }

        Restore();
    }
}
