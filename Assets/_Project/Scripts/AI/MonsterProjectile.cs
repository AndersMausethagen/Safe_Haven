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
    private readonly RaycastHit[] hits = new RaycastHit[32];

    public void Initialize(Combatant source, MonsterAttack action, Vector3 aim)
    {
        owner = source;
        attack = action.attack;
        damage = action.damage;
        direction = aim.normalized;
        speed = action.projectileSpeed;
        radius = action.projectileRadius;
        remaining = action.maximumRange + 3f;
    }

    private void Update()
    {
        if (owner == null || attack == null)
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
            if (collider.transform.IsChildOf(owner.transform) || collider.transform.IsChildOf(transform))
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
            if (victim != null && victim.IsAlive && owner.Faction.WillAttack(victim.Faction))
            {
                DamageInfo info = DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, Random.value);
                DamageResult result = victim.ApplyDamage(info);
                owner.ReportDamageDealt(victim, info, result);
                StatusEffects.AfterHit(attack, victim, owner, result);
            }
            PrefabPool.Release(gameObject);
            return;
        }
        transform.position += direction * step;
        remaining -= step;
        if (remaining <= 0f)
        {
            PrefabPool.Release(gameObject);
        }
    }
}
