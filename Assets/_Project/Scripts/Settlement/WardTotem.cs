using UnityEngine;

/// <summary>
/// A Runic Ward Totem (GDD §6.8): during a ward breach, attackers near it are slowed by the old cold (its Status Effect) and worn down
/// a little. It guards one approach, so where it stands matters, and it does nothing outside a breach. It can't win a defense alone:
/// the damage is small, and attackers break it like any building.
/// <para>
/// Where it belongs: on the Ward Totem building prefab.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WardTotem : MonoBehaviour
{
    [SerializeField, Min(1f)] private float radius = 7f;

    /// <summary>How far it guards (drawn on the build ghost's readout).</summary>
    public float Radius => radius;
    [SerializeField, Min(0.2f)] private float pulseSeconds = 2f;
    [SerializeField, Min(0f)] private float damagePerPulse = 4f;
    [Tooltip("Put on every attacker in reach at each pulse.")]
    [SerializeField] private StatusEffectDefinition effect;

    private Combatant owner;
    private Construction construction;
    private Damageable health;
    private float nextPulse;

    private void Awake()
    {
        owner = GetComponent<Combatant>();
        construction = GetComponent<Construction>();
        health = GetComponent<Damageable>();
    }

    private void Update()
    {
        WardBreachDirector breach = WardBreachDirector.Current;
        if (Time.time < nextPulse || breach == null || !breach.IsActive || (construction != null && !construction.IsStanding) || (health != null && health.IsBroken))
        {
            return;
        }

        nextPulse = Time.time + pulseSeconds;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster.CurrentState == MonsterAI.State.Dead || (monster.transform.position - transform.position).sqrMagnitude > radius * radius
                || !monster.TryGetComponent(out Damageable victim) || !victim.Faction.WillAttack(Faction.Player))
            {
                continue;
            }

            if (effect != null)
            {
                StatusEffects.Apply(victim, effect, owner);
            }

            Vector3 at = monster.transform.position + Vector3.up;
            victim.ApplyDamage(new DamageInfo(owner, null, damagePerPulse, DamageType.Frost, false, at, Vector3.up, damagePerPulse, HitTechnique.None));
        }
    }
}
