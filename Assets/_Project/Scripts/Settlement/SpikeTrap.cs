using UnityEngine;

/// <summary>
/// A pit of sharpened stakes (GDD §14.2 Watch Post / Trap): an enemy creature that walks onto it takes a hit, and the trap
/// needs a moment before it can bite again. It only hurts enemies, so the player and their people walk over it safely.
/// Where it belongs: on a building prefab with a trigger collider and no NavMeshObstacle, so creatures do walk onto it.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeTrap : MonoBehaviour
{
    private const float CheckSeconds = 0.2f;

    [SerializeField, Min(0f)] private float damage = 30f;
    [SerializeField, Min(0.1f)] private float radius = 1.3f;
    [Tooltip("Seconds before it can bite again.")]
    [SerializeField, Min(0.1f)] private float rearmSeconds = 4f;

    private Combatant owner;
    private Construction construction;
    private float nextCheck;

    private void Awake()
    {
        owner = GetComponent<Combatant>();
        construction = GetComponent<Construction>();
    }

    private void Update()
    {
        if (Time.time < nextCheck || (construction != null && construction.IsBuilding))
        {
            return;
        }

        nextCheck = Time.time + CheckSeconds;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster.CurrentState == MonsterAI.State.Dead || (monster.transform.position - transform.position).sqrMagnitude > radius * radius
                || !monster.TryGetComponent(out Damageable victim) || !victim.Faction.WillAttack(Faction.Player))
            {
                continue;
            }

            nextCheck = Time.time + rearmSeconds;
            victim.ApplyDamage(new DamageInfo(owner, null, damage, DamageType.Physical, false, monster.transform.position + Vector3.up, Vector3.up, damage, HitTechnique.None));
            return;
        }
    }
}
