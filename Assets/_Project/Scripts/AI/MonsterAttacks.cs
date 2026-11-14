using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// How a monster carries out a strike once it has chosen to: aimed when it starts (so a target can still dodge), landing
/// through AttackResolver at the attack's Hit Delay, then recovering. The definition's actions (MonsterAttack) add
/// projectile bursts, charges that leave it exposed, howls that rally its side and summons that call in others, each with
/// its own cooldown; a melee action can go on into more blows, or hold its wind-up. Plain C#, owned by a MonsterAI, which decides when to strike and what to do after.
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
    private int blowsLeft;
    private float holdFrom;
    private float holdLeft;
    private float hazardTravelled;
    private float trailTravelled;
    private readonly Damageable health;
    // A held wind-up stops here in its fill, before the end, so the blow is plainly still to come.
    private const float HoldAtShare = 0.6f;

    /// <summary>The stage of a boss's fight: what it knows so far.</summary>
    public int Phase { get; set; }

    public MonsterAttacks(Transform self, MonsterDefinition definition, Combatant combatant, MonsterMovement movement, LayerMask targetLayers)
    {
        this.self = self;
        this.definition = definition;
        this.combatant = combatant;
        this.movement = movement;
        this.targetLayers = targetLayers;
        readyAt = new float[definition.Attacks.Count];
        health = self.GetComponent<Damageable>();
    }

    /// <summary>Whom the current strike is at (set as it begins): a call reaches them, a screech tells the others.</summary>
    public IDamageable Target { get; set; }

    // Its own health as a share, for actions it only uses when hurt (Below Health).
    private float HealthShare => health != null && health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;

    /// <summary>How its target is moving, flat, for actions that aim ahead of it (Lead Target).</summary>
    public Vector3 TargetVelocity { get; set; }

    /// <summary>Multiplies how long it waits between strikes (a frenzied elite). Wind-ups stay as they are, so they can still be read.</summary>
    public float CooldownScale { get; set; } = 1f;

    /// <summary>The action of the current strike, or null for the definition's plain attack.</summary>
    public MonsterAttack Action => action;

    /// <summary>The attack of the current strike. Its Hit Delay is the wind-up before the hit.</summary>
    public AttackDefinition Attack => action == null ? definition.Attack : Blow > 0 ? action.followUps[Blow - 1] : action.attack;

    /// <summary>Which blow of a combo it is on: 0 for the first.</summary>
    public int Blow { get; private set; }

    /// <summary>True while it holds its wind-up: the strike's clock has stopped.</summary>
    public bool IsHolding { get; private set; }

    /// <summary>True while it is still coming at its target with a close strike, rather than spending a far one or recovering.</summary>
    public bool IsClosingIn => action == null || action.kind == MonsterAttackKind.Melee ? hitPending || blowsLeft > 0
        : action.kind == MonsterAttackKind.Charge && exposedAt < 0f;

    /// <summary>Where the current strike was aimed when it started; it doesn't follow the target.</summary>
    public Vector3 Direction => direction;

    /// <summary>True after a charge, while it stands open to a counter.</summary>
    public bool IsExposed => exposedAt >= 0f;

    /// <summary>True once the last strike's cooldown is over.</summary>
    public bool IsReady => Time.time >= nextAttackTime;

    /// <summary>The highest-priority action usable at <paramref name="distance"/> now, or -1.</summary>
    public int ChooseAction(float distance, bool isLeader) => SelectAttack(definition.Attacks, readyAt, distance, Time.time, isLeader, RaidTier, Phase, HealthShare);

    /// <summary>The strike that reaches <paramref name="distance"/>, spent or not, or -1: what it wheels round into when it answers a
    /// backstab. The blow it was knocked out of has just gone on cooldown, and that mustn't leave it with nothing to answer with.</summary>
    public int ChooseAnswer(float distance)
    {
        IReadOnlyList<MonsterAttack> actions = definition.Attacks;
        int chosen = -1;
        for (int i = 0; i < actions.Count; i++)
        {
            MonsterAttack candidate = actions[i];
            if (candidate != null && candidate.Strikes && candidate.fromPhase <= Phase && candidate.KnownAt(RaidTier) && distance >= candidate.minimumRange && distance <= candidate.maximumRange
                && (chosen < 0 || candidate.priority > actions[chosen].priority))
            {
                chosen = i;
            }
        }

        return chosen;
    }

    // Read each time, as a creature can outlive a raid's start but never changes raids.
    private static int RaidTier => GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Tier : 0;

    public static int SelectAttack(IReadOnlyList<MonsterAttack> attacks, float[] cooldowns, float distance, float now, bool isLeader, int tier = 0, int phase = 0, float health = 1f)
    {
        int chosen = -1;
        for (int i = 0; i < attacks.Count; i++)
        {
            MonsterAttack candidate = attacks[i];
            if (candidate != null && candidate.IsAvailable(distance, now, cooldowns[i], isLeader, tier, phase, health)
                && (chosen < 0 || candidate.priority > attacks[chosen].priority))
            {
                chosen = i;
            }
        }
        return chosen;
    }

    /// <summary>True when one of its actions that strikes (not a howl) reaches <paramref name="distance"/>, so it can hold its ground.</summary>
    public bool AnyStrikeReaches(float distance)
    {
        return HoldsGround(definition.Attacks, readyAt, distance, Time.time, RaidTier, Phase);
    }

    /// <summary>
    /// Whether a creature stops here rather than closing in: a strike is ready to use from here, or it is already within reach of its
    /// everyday strike, so it doesn't crowd its target between bites. What it can't use yet is no reason to wait: a wolf whose pounce is spent
    /// runs in to bite, and a Guardian whose wide slam is spent walks up to use its own. A creature whose only strike is on a long
    /// cooldown, such as a Mistwing's dive, has nothing to walk up with, so it keeps its distance until that is ready.
    /// </summary>
    public static bool HoldsGround(IReadOnlyList<MonsterAttack> attacks, float[] cooldowns, float distance, float now, int tier, int phase = 0)
    {
        float everyday = 0f;
        bool any = false, charges = false;
        for (int i = 0; i < attacks.Count; i++)
        {
            MonsterAttack candidate = attacks[i];
            if (candidate == null || !candidate.Strikes || candidate.fromPhase > phase || !candidate.KnownAt(tier))
            {
                continue;
            }

            bool inRange = distance >= candidate.minimumRange && distance <= candidate.maximumRange;
            if (inRange && now >= cooldowns[i])
            {
                return true;
            }

            if (candidate.kind == MonsterAttackKind.Charge)
            {
                charges |= inRange;
            }
            else if (!any || candidate.maximumRange < everyday)
            {
                any = true;
                everyday = candidate.maximumRange;
            }
        }

        return any ? distance <= everyday : charges;
    }

    /// <summary>How high a leaping charge has the body right now, in metres: it rises and falls over the length of the charge.</summary>
    public float LeapHeight
    {
        get
        {
            if (action == null || action.kind != MonsterAttackKind.Charge || action.leapHeight <= 0f || exposedAt >= 0f || chargeLength <= 0f)
            {
                return 0f;
            }

            float through = Mathf.Clamp01(chargeTravelled / chargeLength);
            return action.leapHeight * 4f * through * (1f - through);
        }
    }

    /// <summary>Starts a strike toward <paramref name="toTarget"/>: action <paramref name="index"/>, or -1 for the plain
    /// attack.</summary>
    public void Begin(int index, Vector3 toTarget)
    {
        actionIndex = index;
        action = index >= 0 ? definition.Attacks[index] : null;
        if (action != null && action.kind == MonsterAttackKind.Charge && action.leadTarget > 0f)
        {
            toTarget += TargetVelocity * (toTarget.magnitude / Mathf.Max(action.chargeSpeed, 0.1f) * action.leadTarget);
        }

        direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : self.forward;
        startTime = Time.time;
        hitPending = true;
        shotsFired = 0;
        chargeTravelled = 0f;
        hazardTravelled = 0f;
        trailTravelled = 0f;
        chargeLength = action != null ? Mathf.Min(action.chargeDistance, toTarget.magnitude + 0.7f) : 0f;
        exposedAt = -1f;
        chargeHit = false;
        Blow = 0;
        bool melee = action != null && action.kind == MonsterAttackKind.Melee;
        blowsLeft = melee && action.followUps != null && RaidTier >= action.comboFromTier && Random.value < action.comboChance ? action.followUps.Length : 0;
        holdLeft = melee && Random.value < action.holdChance ? action.holdSeconds : 0f;
        holdFrom = action != null && action.attack != null ? action.attack.HitDelay * HoldAtShare : 0f;
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

        // Set here and not worked out from the clock, which this moves back: whoever else reads it this frame must see the same answer.
        IsHolding = holdLeft > 0f && Time.time - startTime >= holdFrom;
        if (IsHolding)
        {
            holdLeft -= Time.deltaTime;
            startTime += Time.deltaTime;
            Aim(aimPoint);
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
            Shake(attack);
        }

        if (elapsed >= attack.RecoveryTime)
        {
            nextAttackTime = Time.time + definition.AttackCooldown * CooldownScale;
            return false;
        }

        return true;
    }

    /// <summary>Breaks off the strike (a stagger): nothing lands, and the action's cooldown still applies.</summary>
    public void Cancel()
    {
        if (action != null && actionIndex >= 0)
        {
            readyAt[actionIndex] = Time.time + action.cooldown * CooldownScale;
        }

        action = null;
        actionIndex = -1;
        hitPending = false;
        exposedAt = -1f;
        EndBlows();
    }

    private void EndBlows()
    {
        Blow = 0;
        blowsLeft = 0;
        holdLeft = 0f;
        IsHolding = false;
    }

    private void Aim(Vector3? aimPoint)
    {
        if (aimPoint.HasValue)
        {
            Vector3 toAim = aimPoint.Value - self.position;
            toAim.y = 0f;
            direction = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : direction;
        }
    }

    /// <summary>Ends an interruptible action hit during its wind-up. True when that happened.</summary>
    public bool TryInterrupt(float healthLost)
    {
        if (action == null || !action.interruptible || healthLost <= 0f || Time.time - startTime >= Attack.HitDelay)
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
            case MonsterAttackKind.Lob:
                if (shotsFired < action.burstCount && elapsed >= attack.HitDelay + shotsFired * action.burstInterval)
                {
                    ThrowLob(aimPoint);
                    shotsFired++;
                }
                if (shotsFired >= action.burstCount
                    && elapsed >= Mathf.Max(attack.RecoveryTime, attack.HitDelay + (action.burstCount - 1) * action.burstInterval + 0.3f))
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
                    Shake(attack);
                }

                if (blowsLeft > 0 && elapsed >= attack.HitDelay + action.comboGap)
                {
                    blowsLeft--;
                    Blow++;
                    Aim(aimPoint);
                    startTime = Time.time;
                    hitPending = true;
                    return true;
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
        hazardTravelled += step;
        trailTravelled += step;
        GroundImpactDefinition plough = attack.GroundImpact;
        if (plough != null && plough.Mark == GroundMarkShape.Furrow && trailTravelled >= plough.FurrowLength)
        {
            trailTravelled = 0f;
            GroundImpacts.Strike(attack, combatant, self.position, direction, true);
        }
        if (action.hazardPrefab != null && action.hazardEvery > 0f && hazardTravelled >= action.hazardEvery)
        {
            hazardTravelled = 0f;
            GroundHazard.Spawn(action.hazardPrefab, self.position, combatant);
        }
        if (!chargeHit)
        {
            chargeHit = AttackResolver.Execute(combatant, attack, action.damage, self.position, direction, targetLayers) > 0;
        }

        if (blocked || chargeTravelled >= chargeLength || chargeHit)
        {
            exposedAt = Time.time;
            movement.Stop();
            GroundImpacts.Strike(attack, combatant, self.position + direction * 0.8f, direction);
            if (action.hazardPrefab != null && action.hazardEvery <= 0f)
            {
                GroundHazard.Spawn(action.hazardPrefab, self.position, combatant);
            }
        }

        return true;
    }

    // A call: its side nearby (or only itself) hits harder, mends, hurries or hardens for a while; the others of its side
    // learn where its foe is; its foe, near and in sight, is laid under a spell and drawn in.
    private void Howl()
    {
        bool rallies = action.damageBonus > 0f || action.healShare > 0f || action.speedBonus > 0f || action.hasteBonus > 0f || action.toughness > 0f;
        if (rallies)
        {
            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other.CurrentState == MonsterAI.State.Dead || !other.TryGetComponent(out Combatant side) || side.Faction != combatant.Faction
                    || (action.selfOnly ? other.transform != self : Vector3.Distance(self.position, other.transform.position) > action.buffRadius))
                {
                    continue;
                }

                MonsterHowlBuff.For(other.gameObject, action).Refresh(action);
            }
        }

        Component foe = Target as Component;
        if (action.alertsOthers && foe != null)
        {
            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other.transform != self && other.CurrentState != MonsterAI.State.Dead && other.TryGetComponent(out Combatant side)
                    && side.Faction == combatant.Faction && Vector3.Distance(self.position, other.transform.position) <= action.buffRadius
                    && !(other.Target is Component busy && busy != null))
                {
                    other.Engage(Target);
                }
            }
        }

        if ((action.foeStatus != null || Mathf.Abs(action.foePull) > 0.01f) && foe != null && Target.IsAlive
            && Vector3.Distance(self.position, foe.transform.position) <= action.buffRadius && InSight(foe.transform))
        {
            if (action.foeStatus != null && foe is Damageable body)
            {
                StatusEffects.Apply(body, action.foeStatus, combatant);
            }

            ForcedMove.Drag(foe.transform, self.position, action.foePull, Mathf.Min(1.5f, action.buffSeconds));
        }

        if (!string.IsNullOrWhiteSpace(action.announce))
        {
            GameMessages.Post($"{definition.DisplayName} {action.announce}", MessageKind.Warning);
        }
        else if (action.damageBonus > 0f)
        {
            GameMessages.Post($"{definition.DisplayName} howls! Nearby monsters gain +{action.damageBonus:P0} damage.", MessageKind.Warning);
        }
    }

    // Nothing solid between its eyes and the foe's chest.
    private bool InSight(Transform foe)
    {
        Vector3 eye = self.position + Vector3.up * 1.5f;
        Vector3 chest = foe.position + Vector3.up * 1.2f;
        return !Physics.Linecast(eye, chest, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
            || hit.transform.IsChildOf(foe) || hit.transform.IsChildOf(self);
    }

    // The blow strikes the ground where it lands (its Ground Impact): a stomp at its feet, a sweep ahead of it.
    private void Shake(AttackDefinition attack)
    {
        if (attack.GroundImpact == null)
        {
            return;
        }

        Vector3 at = attack.Arc >= 300f ? self.position : self.position + direction * Mathf.Min(attack.Range * 0.6f, 3f);
        GroundImpacts.Strike(attack, combatant, at, direction);
    }

    // One lob of a burst: where the foe stands (ahead of them by Lead Target), the later ones scattered about them.
    private void ThrowLob(Vector3? aimPoint)
    {
        Vector3 point = aimPoint ?? self.position + direction * Mathf.Min(action.maximumRange, 8f);
        point += TargetVelocity * (action.lobSeconds * action.leadTarget);
        if (shotsFired > 0 && action.lobScatter > 0f)
        {
            Vector2 scatter = Random.insideUnitCircle * action.lobScatter;
            point += new Vector3(scatter.x, 0f, scatter.y);
        }

        LobStrike.Throw(combatant, action, self.position + Vector3.up * 2.2f + self.forward * 0.6f, point, targetLayers);
    }

    // Others of its kind rise around it and go for the player. A raid gives them its own strength.
    private void Summon()
    {
        // Co-op, in a friend's game: the host's creature calls its kin in the host's game, and they come here as every creature
        // does. Made here they were removed at once (this game makes none), and readying them threw.
        if (CreatureSource.Remote != null)
        {
            return;
        }

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
                RaidDirector.Current.Creatures.Prepare(root, true, false);
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
            GameMessages.Post(string.IsNullOrWhiteSpace(action.announce) ? $"{definition.DisplayName} calls up its kin!" : $"{definition.DisplayName} {action.announce}", MessageKind.Warning);
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
        if (aimPoint.HasValue && action.leadTarget > 0f)
        {
            aim += TargetVelocity * (aim.magnitude / Mathf.Max(action.projectileSpeed, 0.1f) * action.leadTarget);
        }

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
        readyAt[actionIndex] = Time.time + action.cooldown * CooldownScale;
        nextAttackTime = Time.time + 0.15f;
        action = null;
        actionIndex = -1;
        exposedAt = -1f;
        EndBlows();
        return false;
    }
}
