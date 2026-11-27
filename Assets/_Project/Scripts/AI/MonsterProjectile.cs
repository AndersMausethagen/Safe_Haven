using UnityEngine;

/// <summary>A straight, swept projectile. Solid scenery and allies stop it; it never homes. Fired copies come from and
/// return to <see cref="PrefabPool"/>, so Initialize resets everything a flight uses.</summary>
public sealed class MonsterProjectile : MonoBehaviour
{
    private Combatant owner;
    private AttackDefinition attack;
    private float damage;
    private Vector3 direction;
    private float speed;
    private float radius;
    private float remaining;
    private GameObject hazard;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    [Tooltip("Played where it strikes or comes down (a pooled effect with PooledEffect; empty plays none).")]
    [SerializeField] private GameObject impactEffect;
    // Its flying look's particles (a bolt from the VFX pack), restarted each flight: a pooled copy came back with last flight's trail.
    private ParticleSystem[] systems;

    public void Initialize(Combatant source, MonsterAttack action, Vector3 aim, CastVolley cast = null, bool visual = false)
    {
        systems ??= GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem system in systems)
        {
            system.Clear(true);
            system.Play(true);
        }

        visualOnly = visual;
        volley = cast;
        owner = source;
        attack = action.attack;
        damage = action.damage;
        direction = aim.normalized;
        speed = action.projectileSpeed;
        radius = action.projectileRadius;
        remaining = action.maximumRange + 3f;
        hazard = action.hazardPrefab;
    }

    // Where it comes down, it leaves its hazard (a pool of bile), on the ground beneath.
    // struck: it met something (else it ran out of reach in the air, where no impact shows). A friend's bolt drawn here (co-op) shows
    // none: it has no owner to pass by, so it stops on bodies the real one flies past (review 2026-11-27).
    // at: the ground it leaves its hazard on (under a creature struck); contact: where it struck, for its impact (on the chest, not the feet).
    private void Splash(Vector3 at, Vector3 contact, bool struck)
    {
        if (impactEffect != null && struck && !visualOnly)
        {
            Vector3 back = -direction;
            PrefabPool.Spawn(impactEffect, contact, back.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(back) : Quaternion.identity);
        }

        if (hazard != null && owner != null)
        {
            GroundHazard.Spawn(hazard, at, owner);
        }
    }

    private void Update()
    {
        if ((owner == null && !visualOnly) || attack == null)
        {
            PrefabPool.Release(gameObject);
            return;
        }

        float step = Mathf.Min(remaining, speed * Time.deltaTime);
        int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, hits, step, ~0, QueryTriggerInteraction.Ignore);
        // Overflow must not allow a projectile to pass through an unexamined obstacle.
        RaycastHit[] contacts = hits;
        if (count == hits.Length)
        {
            contacts = Physics.SphereCastAll(transform.position, radius, direction, step, ~0, QueryTriggerInteraction.Ignore);
            count = contacts.Length;
        }
        int nearest = -1;
        for (int i = 0; i < count; i++)
        {
            Collider collider = contacts[i].collider;
            // A friend's bolt has no owner to pass by: what it starts inside (the friend's own body) it doesn't stop on.
            if (visualOnly && contacts[i].distance <= 0f)
            {
                continue;
            }

            // Started inside something (a tree beside the caster): the cast reports it at the world's origin; it flies on out of it,
            // as an arrow does (review 2026-11-27: its impact played at 0,0,0).
            if (contacts[i].distance <= 0f && contacts[i].point == Vector3.zero)
            {
                continue;
            }

            if ((owner != null && collider.transform.IsChildOf(owner.transform)) || collider.transform.IsChildOf(transform))
            {
                continue;
            }

            // A bolt goes past its own side, as arrows do: a player's past a companion, a tamed horse or a friend (one stopped on a
            // companion's back was a cast wasted for nothing), and a creature's past its kin (the Heartroot's Thornwave died on the
            // Rootlings it had just called, 2026-11-17).
            if (owner != null && collider.GetComponentInParent<IDamageable>() is IDamageable body && body.IsAlive
                && body.Category == TargetCategory.Creature && owner.Faction.AreAllies(body.Faction))
            {
                continue;
            }
            if (nearest < 0 || contacts[i].distance < contacts[nearest].distance)
            {
                nearest = i;
            }
        }
        if (nearest >= 0)
        {
            RaycastHit hit = contacts[nearest];
            IDamageable victim = hit.collider.GetComponentInParent<IDamageable>();
            // What the side's blades can strike, its bolts can (a deer, a crate): only its own side is spared.
            if (!visualOnly && victim != null && victim.IsAlive && !owner.Faction.AreAllies(victim.Faction))
            {
                DamageInfo info = volley != null ? CastBlow(victim, hit)
                    : DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, Random.value);
                info = info.WithAmount(info.Amount * CombatFeelSettings.HighGroundFor(owner.transform.position, victim is Component struck ? struck.transform.position : hit.point));
                DamageResult result = victim.ApplyDamage(info);
                AttackResolver.Landed(owner, attack, victim, info, result);
                WebLasso.AfterHit(attack, victim, owner, result);
            }
            Splash(victim is Component splashed && splashed != null ? splashed.transform.position : hit.point, hit.point, true);
            PrefabPool.Release(gameObject);
            return;
        }
        transform.position += direction * step;
        remaining -= step;
        if (remaining <= 0f)
        {
            Splash(transform.position, transform.position, false);
            PrefabPool.Release(gameObject);
        }
    }

    private CastVolley volley;
    // Co-op: a friend's bolt drawn here as it flies; it strikes nothing (their game judges the cast).
    private bool visualOnly;

    // A player's cast: what a swing adds (its multiplier, a sneak attack's sure critical, Ambush Damage, the Spell Damage of Runecraft),
    // as a spell (it trains Runecraft); a body struck again by the same cast takes Repeat Share, so a point-blank fan doesn't triple.
    private DamageInfo CastBlow(IDamageable victim, RaycastHit hit)
    {
        Vector3 from = hit.point - direction * 2f;
        bool sure = PlayerBlows.FromTheShadows(owner, victim, from, volley.Critical, out float ambush);
        DamageInfo info = DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, sure ? DamageCalculator.SureCrit : Random.value);
        info = info.Scaled(1f, volley.Poise, volley.Technique, sure && attack.CanCrit);
        float again = volley.Struck.Add(victim) ? 1f : volley.RepeatShare;
        float spell = Mathf.Max(0f, 1f + owner.Stats.Get(StatType.SpellDamage));
        return info.WithAmount(info.Amount * volley.Multiplier * again * spell * ambush);
    }
}

/// <summary>One cast of a player's staff, shared by its bolts: the swing's damage and poise multipliers, the technique its hits carry, and
/// whom its bolts have struck (Docs/RangedStealthResearch.md §3.8).</summary>
public sealed class CastVolley
{
    public float Multiplier = 1f;
    public float Poise = 1f;
    public HitTechnique Technique = HitTechnique.Spell;
    /// <summary>A sure critical where the attack can crit: the swing's (a counter, an always-critical attack).</summary>
    public bool Critical;
    public float RepeatShare = 0.5f;
    public readonly System.Collections.Generic.HashSet<IDamageable> Struck = new System.Collections.Generic.HashSet<IDamageable>();
}
