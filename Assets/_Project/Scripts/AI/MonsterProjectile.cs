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

    public void Initialize(Combatant source, MonsterAttack action, Vector3 aim, CastVolley cast = null, bool visual = false)
    {
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
    private void Splash(Vector3 at)
    {
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
            if ((owner != null && collider.transform.IsChildOf(owner.transform)) || collider.transform.IsChildOf(transform))
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
            if (!visualOnly && victim != null && victim.IsAlive && owner.Faction.WillAttack(victim.Faction))
            {
                DamageInfo info = volley != null ? CastBlow(victim, hit)
                    : DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, Random.value);
                info = info.WithAmount(info.Amount * CombatFeelSettings.HighGroundFor(owner.transform.position, victim is Component struck ? struck.transform.position : hit.point));
                DamageResult result = victim.ApplyDamage(info);
                owner.ReportDamageDealt(victim, info, result);
                StatusEffects.AfterHit(attack, victim, owner, result);
                ForcedMove.AfterHit(attack, victim, owner, result);
            }
            Splash(victim is Component splashed && splashed != null ? splashed.transform.position : hit.point);
            PrefabPool.Release(gameObject);
            return;
        }
        transform.position += direction * step;
        remaining -= step;
        if (remaining <= 0f)
        {
            Splash(transform.position);
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
        bool sure = PlayerProficiencies.CaughtUnaware(victim, from);
        DamageInfo info = DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, sure ? DamageCalculator.SureCrit : Random.value);
        info = info.Scaled(1f, volley.Poise, volley.Technique, sure && attack.CanCrit);
        float again = volley.Struck.Add(victim) ? 1f : volley.RepeatShare;
        float spell = Mathf.Max(0f, 1f + owner.Stats.Get(StatType.SpellDamage));
        return info.WithAmount(info.Amount * volley.Multiplier * again * spell * PlayerProficiencies.AmbushScale(owner.Stats, victim, from));
    }
}

/// <summary>One cast of a player's staff, shared by its bolts: the swing's damage and poise multipliers, the technique its hits carry, and
/// whom its bolts have struck (Docs/RangedStealthResearch.md §3.8).</summary>
public sealed class CastVolley
{
    public float Multiplier = 1f;
    public float Poise = 1f;
    public HitTechnique Technique = HitTechnique.Spell;
    public float RepeatShare = 0.5f;
    public readonly System.Collections.Generic.HashSet<IDamageable> Struck = new System.Collections.Generic.HashSet<IDamageable>();
}
