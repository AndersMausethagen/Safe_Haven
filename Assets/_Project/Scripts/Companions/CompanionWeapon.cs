using System;
using UnityEngine;

/// <summary>
/// How a companion fights the enemy it is engaged with: within reach it turns to face it and starts an attack every
/// Attack Interval (the animation), which lands at the Strike Delay as a swing through AttackResolver or a pooled arrow
/// like the player's. Plain C#, owned by a Companion.
/// </summary>
public sealed class CompanionWeapon
{
    private const float TurnSpeed = 540f;
    private const float ShotHeight = 1.4f;
    private static readonly RaycastHit[] ShotHits = new RaycastHit[16];

    private readonly Transform self;
    private readonly Combatant combatant;
    private readonly CompanionDefinition definition;
    private readonly CompanionBody body;
    private readonly Predicate<MonsterAI> isFoe;

    private float nextAttack;
    private float strikeAt = -1f;
    private MonsterAI striking;

    /// <summary>What the weapon in their hand makes of their kind's damage: the player's gift against the kind's own (CompanionKit). 1 with none.</summary>
    public float DamageScale { get; set; } = 1f;

    /// <summary>A share more (or less) against one foe: their traits and marks (Troll-wary, Afraid of Widows: ResidentTraits.DamageAgainst).</summary>
    public Func<MonsterAI, float> Against { get; set; }

    private float DamageFor(MonsterAI target, float damage) =>
        damage * DamageScale * Mathf.Max(0.1f, 1f + (Against != null && target != null ? Against(target) : 0f));

    public CompanionWeapon(Transform self, Combatant combatant, CompanionDefinition definition, CompanionBody body, Predicate<MonsterAI> isFoe)
    {
        this.self = self;
        this.combatant = combatant;
        this.definition = definition;
        this.body = body;
        this.isFoe = isFoe;
    }

    /// <summary>Lands an attack whose strike moment has come (even while standing still for a story moment).</summary>
    public void LandDue()
    {
        if (strikeAt >= 0f && Time.time >= strikeAt)
        {
            Strike();
        }
    }

    /// <summary>True when nothing but creatures stands between it and <paramref name="target"/>: arrows pass its friends, not the trees.</summary>
    public bool HasClearShot(MonsterAI target)
    {
        Vector3 origin = self.position + Vector3.up * ShotHeight;
        Vector3 aim = target.TryGetComponent(out Collider targetBody) ? targetBody.bounds.center : target.transform.position + Vector3.up * 0.5f;
        Vector3 line = aim - origin;
        float length = line.magnitude;
        if (length < 0.01f)
        {
            return true;
        }

        int count = Physics.RaycastNonAlloc(origin, line / length, ShotHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = ShotHits[i].collider.transform;
            if (hit.IsChildOf(self) || hit.IsChildOf(target.transform))
            {
                continue;
            }

            Damageable body = ShotHits[i].collider.GetComponentInParent<Damageable>();
            if (body == null || body.Category != TargetCategory.Creature)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Faces <paramref name="engaged"/> when within reach and starts the next attack when it is ready, unless it is
    /// <paramref name="busy"/> (getting out of the way, or looking for a line).</summary>
    public void Fight(MonsterAI engaged, bool busy = false)
    {
        if (engaged == null || !isFoe(engaged))
        {
            return;
        }

        Vector3 toTarget = engaged.transform.position - self.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > definition.ReachFor(0.5f))
        {
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            self.rotation = Quaternion.RotateTowards(self.rotation, Quaternion.LookRotation(toTarget), TurnSpeed * Time.deltaTime);
        }

        if (!busy && Time.time >= nextAttack && strikeAt < 0f)
        {
            nextAttack = Time.time + definition.AttackInterval;
            strikeAt = Time.time + definition.StrikeDelay;
            striking = engaged;
            body.PlayAttack();
        }
    }

    /// <summary>Drops an attack in progress (knocked down).</summary>
    public void Cancel()
    {
        strikeAt = -1f;
        striking = null;
    }

    private void Strike()
    {
        MonsterAI target = striking;
        Cancel();
        if (target == null || !isFoe(target))
        {
            return;
        }

        if (definition.IsRanged)
        {
            Shoot(target);
        }
        else
        {
            Vector3 direction = target.transform.position - self.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : self.forward;
            AttackResolver.Execute(combatant, definition.Attack, DamageFor(target, definition.WeaponDamage), self.position, direction, ~0);
        }

        AudioManager.Play(definition.AttackSound, self.position);
    }

    private void Shoot(MonsterAI target)
    {
        ItemDefinition arrowItem = definition.Arrow;
        ArrowDefinition arrow = arrowItem.Arrow;
        if (arrow.Projectile == null)
        {
            return;
        }

        Vector3 origin = self.position + Vector3.up * ShotHeight + self.forward * 0.4f;
        Vector3 aim = target.TryGetComponent(out Collider targetBody) ? targetBody.bounds.center : target.transform.position + Vector3.up * 0.5f;
        float speed = definition.ArrowSpeed * arrow.SpeedMultiplier;
        Vector3 direction = ArcheryRules.LaunchDirection(origin, aim, speed, Physics.gravity.magnitude * arrow.GravityScale);
        ArrowProjectile projectile = PrefabPool.Spawn(arrow.Projectile, origin, Quaternion.LookRotation(direction));
        projectile.Launch(combatant, definition.Attack, arrowItem, DamageFor(target, arrow.Damage * definition.ArrowDamageShare), direction * speed);
    }
}
