using UnityEngine;

/// <summary>
/// Someone up on the Watch Post with a bow (GDD §6.8): from Tier II, during a ward breach, it shoots the nearest attacker in reach.
/// Each tier hits harder, and a Warden working here shoots faster. It only acts in a breach, so the game hunted by day at home stays
/// the player's. Player combat stays the main thing: a shot is about what one blow of early gear does.
/// <para>
/// Where it belongs: on the Watch Post building prefab, next to its Construction.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WatchPostArcher : MonoBehaviour
{
    private const float ShotShowsSeconds = 0.12f;

    [SerializeField, Min(1f)] private float reach = 18f;

    /// <summary>How far it shoots (drawn on the build ghost's readout).</summary>
    public float Reach => reach;
    [Tooltip("Damage per shot for each tier above the first.")]
    [SerializeField, Min(0f)] private float damagePerTier = 8f;
    [SerializeField, Min(0.2f)] private float secondsBetweenShots = 3f;
    [Tooltip("Share off the time between shots while a resident's job works here.")]
    [SerializeField, Range(0f, 0.9f)] private float wardenSpeedUp = 0.35f;

    private Combatant owner;
    private Construction construction;
    private BuildingObject building;
    private Damageable health;
    private float nextShot;

    private void Awake()
    {
        owner = GetComponent<Combatant>();
        construction = GetComponent<Construction>();
        building = GetComponent<BuildingObject>();
        health = GetComponent<Damageable>();
    }

    private bool IsManned => building != null && GameSession.Here.Residents is var residents && HasWorkerHere(residents);

    private bool HasWorkerHere(System.Collections.Generic.IReadOnlyList<ResidentRecord> residents)
    {
        foreach (ResidentRecord resident in residents)
        {
            if (resident.Job != null && resident.Job.Workplace == building.Definition)
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        WardBreachDirector breach = WardBreachDirector.Current;
        // Its archer comes with the second level.
        if (Time.time < nextShot || breach == null || !breach.IsActive || construction == null || construction.Level < 2
            || !Construction.DefenceWorks(construction, health))
        {
            return;
        }

        MonsterAI target = Nearest();
        if (target == null || !target.TryGetComponent(out Damageable victim))
        {
            nextShot = Time.time + 0.4f;
            return;
        }

        nextShot = Time.time + secondsBetweenShots * (IsManned ? 1f - wardenSpeedUp : 1f);
        // Brukam's Horn and the like (fightShare): the post's shots hit harder.
        float damage = damagePerTier * (construction.Level - 1) * (1f + construction.Sum(bonus => bonus.fightShare));
        Vector3 hit = target.transform.position + Vector3.up;
        victim.ApplyDamage(new DamageInfo(owner, null, damage, DamageType.Physical, false, hit, hit - transform.position, damage, HitTechnique.None));
        ShowShot(transform.position + Vector3.up * 4f, hit);
    }

    private MonsterAI Nearest()
    {
        MonsterAI best = null;
        float bestDistance = reach * reach;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster.CurrentState == MonsterAI.State.Dead || !monster.TryGetComponent(out Damageable victim) || !victim.Faction.WillAttack(Faction.Player))
            {
                continue;
            }

            float distance = (monster.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monster;
            }
        }

        return best;
    }

    private static void ShowShot(Vector3 from, Vector3 to)
    {
        var shot = new GameObject("Arrow Flight").AddComponent<LineRenderer>();
        shot.useWorldSpace = true;
        shot.positionCount = 2;
        shot.SetPosition(0, from);
        shot.SetPosition(1, to);
        shot.startWidth = 0.06f;
        shot.endWidth = 0.02f;
        shot.sharedMaterial = ConstructionSettings.Current.ScaffoldMaterial;
        Destroy(shot.gameObject, ShotShowsSeconds);
    }
}
