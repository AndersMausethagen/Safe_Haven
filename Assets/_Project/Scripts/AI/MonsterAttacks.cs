using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// How a monster carries out a strike once it has chosen to: aimed when it starts (so a target can still dodge), landing
/// through AttackResolver at the attack's Hit Delay, then recovering. The definition's actions (MonsterAttack) add
/// projectile bursts, charges that leave it exposed, howls that rally its side and summons that call in others, each with
/// its own cooldown. Plain C#, owned by a MonsterAI, which decides when to strike and what to do after.
/// </summary>
public sealed class MonsterAttacks
{
    private readonly Transform self;
    private readonly MonsterDefinition definition;
    private readonly Combatant combatant;
    private readonly MonsterMovement movement;
    private readonly LayerMask targetLayers;
    private readonly float[] readyAt;
    private readonly List<GameObject> summoned = new List<GameObject>();

    private float nextAttackTime;
    private float startTime;
    private Vector3 direction;
    private bool hitPending;
    private MonsterAttack action;
    private int actionIndex = -1;
    private int shotsFired;
    private float chargeTravelled;
    private float chargeLength;
    private float exposedAt = -1f;
    private bool chargeHit;

    public MonsterAttacks(Transform self, MonsterDefinition definition, Combatant combatant, MonsterMovement movement, LayerMask targetLayers)
    {
        this.self = self;
        this.definition = definition;
        this.combatant = combatant;
        this.movement = movement;
        this.targetLayers = targetLayers;
        readyAt = new float[definition.Attacks.Count];
    }

    /// <summary>The action of the current strike, or null for the definition's plain attack.</summary>
    public MonsterAttack Action => action;

    /// <summary>The attack of the current strike. Its Hit Delay is the wind-up before the hit.</summary>
    public AttackDefinition Attack => action != null ? action.attack : definition.Attack;

    /// <summary>Where the current strike was aimed when it started; it doesn't follow the target.</summary>
    public Vector3 Direction => direction;

    /// <summary>True after a charge, while it stands open to a counter.</summary>
    public bool IsExposed => exposedAt >= 0f;

    /// <summary>True once the last strike's cooldown is over.</summary>
    public bool IsReady => Time.time >= nextAttackTime;

    /// <summary>The highest-priority action usable at <paramref name="distance"/> now, or -1.</summary>
    public int ChooseAction(float distance, bool isLeader) => SelectAttack(definition.Attacks, readyAt, distance, Time.time, isLeader);

    public static int SelectAttack(IReadOnlyList<MonsterAttack> attacks, float[] cooldowns, float distance, float now, bool isLeader)
    {
        int chosen = -1;
        for (int i = 0; i < attacks.Count; i++)
        {
            MonsterAttack candidate = attacks[i];
            if (candidate != null && candidate.IsAvailable(distance, now, cooldowns[i], isLeader)
                && (chosen < 0 || candidate.priority > attacks[chosen].priority))
            {
                chosen = i;
            }
        }
        return chosen;
    }

    /// <summary>True when one of its actions that strikes (not a howl) reaches <paramref name="distance"/>.</summary>
    public bool AnyStrikeReaches(float distance)
    {
        foreach (MonsterAttack candidate in definition.Attacks)
        {
            if (candidate != null && candidate.attack != null && candidate.kind != MonsterAttackKind.Howl && candidate.kind != MonsterAttackKind.Summon
                && distance >= candidate.minimumRange && distance <= candidate.maximumRange)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Starts a strike toward <paramref name="toTarget"/>: action <paramref name="index"/>, or -1 for the plain
    /// attack.</summary>
    public void Begin(int index, Vector3 toTarget)
    {
        actionIndex = index;
        action = index >= 0 ? definition.Attacks[index] : null;
        direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : self.forward;
        startTime = Time.time;
        hitPending = true;
        shotsFired = 0;
        chargeTravelled = 0f;
        chargeLength = action != null ? Mathf.Min(action.chargeDistance, toTarget.magnitude + 0.7f) : 0f;
        exposedAt = -1f;
        chargeHit = false;
    }

    /// <summary>Carries the strike on. False once it is over, so the owner goes back to the chase.</summary>
    /// <param name="aimPoint">Where projectiles fly (the target), or null to shoot along the strike's direction.</param>
    public bool Update(Vector3? aimPoint)
    {
        AttackDefinition attack = Attack;
        if (attack == null)
        {
            return false;
        }

        movement.TurnTowards(direction);
        float elapsed = Time.time - startTime;
        if (action != null)
        {
            return UpdateAction(attack, elapsed, aimPoint);
        }

        if (hitPending && elapsed >= attack.HitDelay)
        {
            hitPending = false;
            AttackResolver.Execute(combatant, attack, definition.AttackDamage, self.position, direction, targetLayers);
        }

        if (elapsed >= attack.RecoveryTime)
        {
            nextAttackTime = Time.time + definition.AttackCooldown;
            return false;
        }

        return true;
    }

    /// <summary>Breaks off the strike (a stagger): nothing lands, and the action's cooldown still applies.</summary>
    public void Cancel()
    {
        if (action != null && actionIndex >= 0)
        {
            readyAt[actionIndex] = Time.time + action.cooldown;
        }

        action = null;
        actionIndex = -1;
        hitPending = false;
        exposedAt = -1f;
    }

    /// <summary>Ends an interruptible action hit during its wind-up. True when that happened.</summary>
    public bool TryInterrupt(float healthLost)
    {
        if (action == null || !action.interruptible || healthLost <= 0f || Time.time - startTime >= action.attack.HitDelay)
        {
            return false;
        }

        Finish();
        return true;
    }

    /// <summary>No new strike for <paramref name="seconds"/> (catching its breath after a stagger).</summary>
    public void Delay(float seconds)
    {
        nextAttackTime = Time.time + seconds;
    }

    private bool UpdateAction(AttackDefinition attack, float elapsed, Vector3? aimPoint)
    {
        if (elapsed < attack.HitDelay)
        {
            return true;
        }

        switch (action.kind)
        {
            case MonsterAttackKind.Projectile:
                if (shotsFired < action.burstCount && elapsed >= attack.HitDelay + shotsFired * action.burstInterval)
                {
                    FireProjectile(aimPoint);
                    shotsFired++;
                }
                if (shotsFired >= action.burstCount
                    && elapsed >= Mathf.Max(attack.RecoveryTime, attack.HitDelay + (action.burstCount - 1) * action.burstInterval + 0.5f))
                {
                    return Finish();
                }
                return true;
            case MonsterAttackKind.Charge:
                return UpdateCharge(attack);
            case MonsterAttackKind.Howl:
                if (hitPending)
                {
                    hitPending = false;
                    Howl();
                }
                return elapsed < attack.RecoveryTime || Finish();
            case MonsterAttackKind.Summon:
                if (hitPending)
                {
                    hitPending = false;
                    Summon();
                }
                return elapsed < attack.RecoveryTime || Finish();
            default:
                if (hitPending)
                {
                    hitPending = false;
                    AttackResolver.Execute(combatant, attack, action.damage, self.position, direction, targetLayers);
                }
                return elapsed < attack.RecoveryTime || Finish();
        }
    }

    private bool UpdateCharge(AttackDefinition attack)
    {
        if (exposedAt >= 0f)
        {
            return Time.time - exposedAt < action.exposedSeconds || Finish();
        }

        if (!movement.EnsureOnNavMesh())
        {
            exposedAt = Time.time;
            return true;
        }

        float step = Mathf.Min(action.chargeSpeed * Time.deltaTime, chargeLength - chargeTravelled);
        bool blocked = movement.Rush(direction, step);
        chargeTravelled += step;
        if (!chargeHit)
        {
            chargeHit = AttackResolver.Execute(combatant, attack, action.damage, self.position, direction, targetLayers) > 0;
        }

        if (blocked || chargeTravelled >= chargeLength || chargeHit)
        {
            exposedAt = Time.time;
            movement.Stop();
        }

        return true;
    }

    // Monsters of its side nearby hit harder for a while.
    private void Howl()
    {
        foreach (MonsterAI other in MonsterAI.Active)
        {
            if (other.CurrentState == MonsterAI.State.Dead || !other.TryGetComponent(out Combatant side) || side.Faction != combatant.Faction
                || Vector3.Distance(self.position, other.transform.position) > action.buffRadius)
            {
                continue;
            }

            MonsterHowlBuff buff = other.GetComponent<MonsterHowlBuff>();
            if (buff == null)
            {
                buff = other.gameObject.AddComponent<MonsterHowlBuff>();
            }
            buff.Refresh(action.damageBonus, action.buffSeconds);
        }

        GameMessages.Post($"{definition.DisplayName} howls! Nearby monsters gain +{action.damageBonus:P0} damage.", MessageKind.Warning);
    }

    // Others of its kind rise around it and go for the player. A raid gives them its own strength.
    private void Summon()
    {
        if (action.summonPrefab == null)
        {
            Debug.LogError($"{definition.DisplayName}: summon action has no prefab.", self);
            return;
        }

        summoned.RemoveAll(IsGone);
        int called = 0;
        Player player = Player.Current;
        for (int i = 0; i < action.summonCount && summoned.Count < action.summonLimit; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * action.summonRadius;
            if (!NavMesh.SamplePosition(self.position + new Vector3(offset.x, 0f, offset.y), out NavMeshHit hit, action.summonRadius, NavMesh.AllAreas))
            {
                continue;
            }

            GameObject root = Object.Instantiate(action.summonPrefab, hit.position, self.rotation, self.parent);
            summoned.Add(root);
            called++;
            if (RaidDirector.Current != null)
            {
                RaidDirector.Current.PrepareSpawned(root, true);
            }
            else if (player != null)
            {
                foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>())
                {
                    ai.Engage(player.Health);
                }
            }
        }

        if (called > 0)
        {
            GameMessages.Post($"{definition.DisplayName} calls up its kin!", MessageKind.Warning);
        }
    }

    private static bool IsGone(GameObject root)
    {
        if (root == null)
        {
            return true;
        }

        foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>())
        {
            if (ai.CurrentState != MonsterAI.State.Dead)
            {
                return false;
            }
        }

        return true;
    }

    private void FireProjectile(Vector3? aimPoint)
    {
        if (action.projectilePrefab == null)
        {
            Debug.LogError($"{definition.DisplayName}: projectile action has no projectile prefab.", self);
            return;
        }

        Vector3 origin = self.position + Vector3.up;
        Vector3 aim = aimPoint.HasValue ? aimPoint.Value + Vector3.up - origin : direction;
        GameObject projectile = PrefabPool.Spawn(action.projectilePrefab, origin + aim.normalized * 0.7f, Quaternion.LookRotation(aim));
        MonsterProjectile flight = projectile.GetComponent<MonsterProjectile>();
        if (flight == null)
        {
            Debug.LogError($"{projectile.name} has no MonsterProjectile component.", projectile);
            PrefabPool.Release(projectile);
            return;
        }

        flight.Initialize(combatant, action, aim);
    }

    // The action is over: its cooldown starts and the next strike may follow shortly. Always false (the strike ended).
    private bool Finish()
    {
        readyAt[actionIndex] = Time.time + action.cooldown;
        nextAttackTime = Time.time + 0.15f;
        action = null;
        actionIndex = -1;
        exposedAt = -1f;
        return false;
    }
}
