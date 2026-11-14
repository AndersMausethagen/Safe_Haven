using UnityEngine;

/// <summary>
/// A false light (the Marsh Light's lure): looks like the real one and drifts about where it was called up, but it has no
/// bolt and nothing in it. Any blow, arrow or spell bursts it. It fades by itself after a while. Called up by the Marsh
/// Light's Summon action; the real one is the one that shoots.
/// </summary>
[DisallowMultipleComponent]
public sealed class DecoyLight : MonoBehaviour, IDamageable
{
    [SerializeField] private Faction faction = Faction.Enemy;
    [Tooltip("Seconds before it fades on its own.")]
    [SerializeField, Min(1f)] private float lasts = 14f;
    [Tooltip("Metres it wanders from where it was called up.")]
    [SerializeField, Min(0f)] private float wander = 3.5f;
    [Tooltip("Played where it bursts.")]
    [SerializeField] private GameObject burstEffect;

    private Vector3 home;
    private float seed;
    private float bornAt;
    private bool alive = true;

    public bool IsAlive => alive;
    public Faction Faction => faction;
    public TargetCategory Category => TargetCategory.Creature;

    private void Start()
    {
        home = transform.position;
        seed = Random.Range(0f, 100f);
        bornAt = Time.time;
    }

    private void Update()
    {
        float t = Time.time + seed;
        Vector3 drift = new Vector3(Mathf.Sin(t * 0.7f), 0f, Mathf.Cos(t * 0.53f)) * wander;
        drift.y = Mathf.Sin(t * 1.9f) * 0.25f;
        transform.position = Vector3.Lerp(transform.position, home + drift, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
        if (Time.time - bornAt >= lasts)
        {
            Burst();
        }
    }

    public DamageResult ApplyDamage(in DamageInfo damage)
    {
        if (!alive || damage.IsOverTime)
        {
            return DamageResult.None;
        }

        Burst();
        return new DamageResult(damage.Amount, 0f, 0f, true, false, damage.Type, GuardOutcome.None);
    }

    private void Burst()
    {
        if (!alive)
        {
            return;
        }

        alive = false;
        if (burstEffect != null)
        {
            PrefabPool.Spawn(burstEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
