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

    /// <summary>What its hits carry (RaidHarms names its harm for the planner).</summary>
    public StatusEffectDefinition Effect => effect;

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
                    MonsterHowlBuff.For(other.gameObject, null).Refresh(damageBonus, Interval * 1.5f);
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


/// <summary>
/// Armoured (Hades II): the first share of its health is an armour layer, outlined in a ring of its colour, through which nothing
/// flinches, interrupts or staggers it (a parry only makes it recoil). Wearing the layer away breaks it with a crash and a long stagger.
/// It is taken from its health, not added to it, so the hits it takes to kill hold. The answer: commit, then punish the break.
/// </summary>
[Serializable]
public sealed class UnyieldingBehaviour : EliteBehaviour
{
    [Tooltip("Share of its health the armour layer is: it breaks when this much is gone.")]
    [SerializeField, Range(0.05f, 0.9f)] private float layer = 0.3f;
    [Tooltip("Seconds it staggers when the layer breaks.")]
    [SerializeField, Min(0f)] private float breakStagger = 2.2f;
    [SerializeField] private Color ringColour = new Color(1f, 0.82f, 0.2f, 0.55f);

    public override string DisplayName => "Unyielding";

    public override Action Attach(EliteCreature elite)
    {
        MonsterAI ai = elite.Ai;
        ai.IsUnyielding = true;
        GameObject ring = MakeRing(elite.transform, ringColour);
        void Break()
        {
            ai.IsUnyielding = false;
            if (ring != null)
            {
                UnityEngine.Object.Destroy(ring);
            }

            AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.GuardBreak : null, elite.transform.position + Vector3.up);
            GameObject burst = CombatFeelSettings.Current.StaggerEffect;
            if (burst != null)
            {
                PrefabPool.Spawn(burst, elite.transform.position + Vector3.up, Quaternion.identity);
            }

            ai.StaggerAtLeast(breakStagger);
        }

        // By its health share, so a friend's copy (whose health the host's game sets) breaks it too.
        void OnTick(float deltaTime)
        {
            if (ai.IsUnyielding && elite.Health.CurrentHealth > 0f && elite.Health.HealthFraction <= 1f - layer)
            {
                Break();
            }
        }

        elite.Ticked += OnTick;
        return () =>
        {
            elite.Ticked -= OnTick;
            ai.IsUnyielding = false;
            if (ring != null)
            {
                UnityEngine.Object.Destroy(ring);
            }
        };
    }

    // A ring of the layer's colour at its feet, the telegraph's look.
    private static GameObject MakeRing(Transform body, Color colour)
    {
        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null)
        {
            return null;
        }

        var ring = new GameObject("Unyielding Ring");
        ring.transform.SetParent(body, false);
        ring.transform.localPosition = Vector3.up * 0.07f;
        float radius = body.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) ? agent.radius + 0.35f : 0.9f;
        ring.transform.localScale = Vector3.one * radius;
        ring.AddComponent<MeshFilter>().sharedMesh = AttackTelegraph.BandFor(360f, 0.2f);
        var renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", colour);
        block.SetColor("_Color", colour);
        renderer.SetPropertyBlock(block);
        return ring;
    }
}

/// <summary>Watchful: it knows the trick from the start and never forgets: while it strikes, a swing from behind is seen as by one that
/// has taken several backstabs, and it breaks off to step aside or parry (MonsterAI.SeesBackstabComing). Like every elite it is open to a
/// backstab only when staggered. The answer: beat it from the front, or parry it.</summary>
[Serializable]
public sealed class WatchfulBehaviour : EliteBehaviour
{
    [Tooltip("Backstabs it is taken to remember, always.")]
    [SerializeField, Min(0)] private int foreknown = 2;

    public override string DisplayName => "Watchful";

    public override Action Attach(EliteCreature elite)
    {
        MonsterAI ai = elite.Ai;
        ai.BackstabsForeknown = foreknown;
        return () => ai.BackstabsForeknown = 0;
    }
}

/// <summary>Relentless: it always goes on into its follow-ups and holds the wind-ups it can hold, so its strikes come as strings. The
/// answer: don't roll on the first blow; wait out the hold.</summary>
[Serializable]
public sealed class PressingBehaviour : EliteBehaviour
{
    public override string DisplayName => "Relentless";

    public override Action Attach(EliteCreature elite)
    {
        MonsterAI ai = elite.Ai;
        ai.Presses = true;
        return () => ai.Presses = false;
    }
}
