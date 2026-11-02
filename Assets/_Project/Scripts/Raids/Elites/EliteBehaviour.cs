using System;
using UnityEngine;

/// <summary>
/// What an elite affix makes a creature do beyond numbers. Authored on an EliteAffixDefinition, so one object serves every creature and
/// must keep no state: Attach starts it on one elite and returns what ends it, and whatever it remembers lives in that closure. To add a
/// kind: derive from this with a [Serializable] class and a parameterless constructor; the inspector's menu finds it by itself.
/// </summary>
[Serializable]
public abstract class EliteBehaviour
{
    /// <summary>The name in the inspector's menu.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Starts it on an elite. Returns what takes it off again.</summary>
    public abstract Action Attach(EliteCreature elite);
}

/// <summary>Its hits can carry a status effect. The answer: don't get hit, or cure it.</summary>
[Serializable]
public sealed class StatusOnHitBehaviour : EliteBehaviour
{
    [SerializeField] private StatusEffectDefinition effect;
    [SerializeField, Range(0f, 1f)] private float chance = 1f;

    public override string DisplayName => "Status on Hit";

    public override Action Attach(EliteCreature elite)
    {
        void OnDealt(IDamageable target, DamageInfo blow, DamageResult result)
        {
            if (effect != null && result.HealthLost > 0f && target is Damageable victim && UnityEngine.Random.value < chance)
            {
                StatusEffects.Apply(victim, effect, elite.Combatant);
            }
        }

        elite.Combatant.DamageDealt += OnDealt;
        return () => elite.Combatant.DamageDealt -= OnDealt;
    }
}

/// <summary>Grows back while nobody is hitting it. The answer: stay on it, or finish it in one go.</summary>
[Serializable]
public sealed class RegrowthBehaviour : EliteBehaviour
{
    [Tooltip("Share of its health it regains each second.")]
    [SerializeField, Range(0f, 0.2f)] private float perSecond = 0.03f;
    [Tooltip("Seconds after a hit before it starts growing back.")]
    [SerializeField, Min(0f)] private float afterHit = 3.5f;

    public override string DisplayName => "Regrowth";

    public override Action Attach(EliteCreature elite)
    {
        float quietFrom = 0f;
        void OnDamaged(DamageInfo blow, DamageResult result) => quietFrom = Time.time + afterHit;
        void OnTick(float deltaTime)
        {
            Damageable body = elite.Health;
            if (Time.time >= quietFrom && body.CurrentHealth > 0f && body.CurrentHealth < body.MaxHealth)
            {
                body.Heal(body.MaxHealth * perSecond * deltaTime);
            }
        }

        elite.Health.Damaged += OnDamaged;
        elite.Ticked += OnTick;
        return () =>
        {
            elite.Health.Damaged -= OnDamaged;
            elite.Ticked -= OnTick;
        };
    }
}

/// <summary>Bursts when it dies, after a warning on the ground. The answer: kill it, then step out of the circle.</summary>
[Serializable]
public sealed class BurstOnDeathBehaviour : EliteBehaviour
{
    [Tooltip("What the burst is. Its Range is the size of the circle.")]
    [SerializeField] private AttackDefinition attack;
    [SerializeField, Min(0f)] private float damage = 30f;
    [Tooltip("Seconds between its death and the burst.")]
    [SerializeField, Min(0.1f)] private float delay = 1.1f;

    public override string DisplayName => "Burst on Death";

    public override Action Attach(EliteCreature elite)
    {
        void OnDied(Damageable victim, DamageInfo blow)
        {
            if (attack != null)
            {
                EliteBurst.Begin(elite, attack, damage, delay);
            }
        }

        elite.Health.Died += OnDied;
        return () => elite.Health.Died -= OnDied;
    }
}

/// <summary>Those around it hit harder while it lives, and the circle under it shows how far. The answer: kill it first.</summary>
[Serializable]
public sealed class RallyingBehaviour : EliteBehaviour
{
    [SerializeField, Min(0.5f)] private float radius = 9f;
    [SerializeField, Range(0f, 1f)] private float damageBonus = 0.15f;

    private const float Interval = 1f;

    public override string DisplayName => "Rallying Aura";

    public override Action Attach(EliteCreature elite)
    {
        // The project blends in Linear, so a faint light overlay comes out far brighter than its alpha.
        elite.AddRing(radius, 0.04f);
        float next = 0f;
        void OnTick(float deltaTime)
        {
            if (Time.time < next || elite.Health.CurrentHealth <= 0f)
            {
                return;
            }

            next = Time.time + Interval;
            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other != elite.Ai && other.CurrentState != MonsterAI.State.Dead && other.TryGetComponent(out Combatant side)
                    && side.Faction == elite.Combatant.Faction && Vector3.Distance(elite.transform.position, other.transform.position) <= radius)
                {
                    MonsterHowlBuff buff = other.GetComponent<MonsterHowlBuff>();
                    if (buff == null)
                    {
                        buff = other.gameObject.AddComponent<MonsterHowlBuff>();
                    }

                    buff.Refresh(damageBonus, Interval * 1.5f);
                }
            }
        }

        elite.Ticked += OnTick;
        return () => elite.Ticked -= OnTick;
    }
}

/// <summary>Walks wrapped in fog, so it is hard to see until it is close. The answer: fall back to open ground and watch for its ring.</summary>
[Serializable]
public sealed class MistCloakBehaviour : EliteBehaviour
{
    [Tooltip("Half its width, in metres.")]
    [SerializeField, Min(0.5f)] private float radius = 6f;
    [Tooltip("Half its height, in metres.")]
    [SerializeField, Min(0.25f)] private float height = 2.2f;
    [SerializeField, Range(0f, 0.5f)] private float density = 0.14f;

    public override string DisplayName => "Mist Cloak";

    public override Action Attach(EliteCreature elite)
    {
        var cloak = new GameObject("Mist Cloak");
        cloak.transform.SetParent(elite.transform, false);
        cloak.AddComponent<FogArea>().Configure(density, radius, height);
        void OnDied(Damageable victim, DamageInfo blow) => cloak.SetActive(false);
        elite.Health.Died += OnDied;
        return () =>
        {
            elite.Health.Died -= OnDied;
            if (cloak != null)
            {
                UnityEngine.Object.Destroy(cloak);
            }
        };
    }
}
