using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A patch of ground that hurts whoever stands in it for a while: a bog spitter's bile, a widow's web, the fire a burning
/// knight leaves in its wake. It lays its status on every foe of whoever made it standing inside, again each tick, and
/// can burn on top. Drawn as a soft disc in its colour, with an effect of its own if it has one. Left by a monster's shot,
/// lob or charge (Monster Attack › Hazard Prefab); a prefab of its own per kind of patch.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Collider[] Overlaps = new Collider[32];

    [SerializeField, Min(0.3f)] private float radius = 2f;
    [SerializeField, Min(0.5f)] private float seconds = 5f;
    [Tooltip("Laid on whoever stands in it, again each tick (poison, roots, chill).")]
    [SerializeField] private StatusEffectDefinition status;
    [Tooltip("Damage each second to whoever stands in it, on top of the status.")]
    [SerializeField, Min(0f)] private float damagePerSecond;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("Seconds between its touches.")]
    [SerializeField, Min(0.1f)] private float tick = 0.5f;
    [Tooltip("The disc's colour. Clear: the status's own colour.")]
    [SerializeField] private Color color = Color.clear;
    [Tooltip("An effect that plays over it while it lasts (mist, embers, strands).")]
    [SerializeField] private GameObject effectPrefab;

    private Combatant source;
    private Faction faction;
    private float startedAt;
    private float nextTick;
    private Renderer disc;
    private MaterialPropertyBlock block;
    private Color shown;
    private readonly HashSet<IDamageable> touched = new HashSet<IDamageable>();

    public float Radius => radius;

    /// <summary>Lays the patch on the ground at <paramref name="at"/> (the NavMesh beneath it), made by <paramref name="by"/>.</summary>
    public static GroundHazard Spawn(GameObject prefab, Vector3 at, Combatant by)
    {
        if (prefab == null || by == null || !prefab.TryGetComponent(out GroundHazard _))
        {
            return null;
        }

        if (NavMesh.SamplePosition(at, out NavMeshHit ground, 3f, NavMesh.AllAreas))
        {
            at = ground.position;
        }

        GameObject made = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        GroundHazard hazard = made.GetComponent<GroundHazard>();
        hazard.source = by;
        hazard.faction = by.Faction;
        return hazard;
    }

    private void Start()
    {
        startedAt = Time.time;
        nextTick = Time.time + 0.2f;
        shown = color.a > 0f ? color : status != null ? status.Color : new Color(0.4f, 0.6f, 0.2f, 1f);
        shown.a = 0.45f;
        MakeDisc();
        if (effectPrefab != null)
        {
            Instantiate(effectPrefab, transform.position, Quaternion.identity, transform);
        }

        Destroy(gameObject, seconds);
    }

    private void MakeDisc()
    {
        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null)
        {
            return;
        }

        var part = new GameObject("Disc", typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(transform, false);
        part.transform.localPosition = Vector3.up * 0.05f;
        part.transform.localScale = new Vector3(radius, 1f, radius);
        part.GetComponent<MeshFilter>().sharedMesh = AttackTelegraph.FanFor(360f);
        disc = part.GetComponent<MeshRenderer>();
        disc.sharedMaterial = material;
        disc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        disc.receiveShadows = false;
        block = new MaterialPropertyBlock();
        Paint(0f);
    }

    private void Paint(float age)
    {
        if (disc == null)
        {
            return;
        }

        // Spreads in over a moment, holds, and fades over its last second.
        float grow = Mathf.Clamp01(age / 0.25f);
        float fade = Mathf.Clamp01((seconds - age) / 1f);
        Color now = shown;
        now.a *= grow * fade;
        disc.transform.localScale = new Vector3(radius * Mathf.Lerp(0.5f, 1f, grow), 1f, radius * Mathf.Lerp(0.5f, 1f, grow));
        block.SetColor(BaseColorId, now);
        block.SetColor(ColorId, now);
        disc.SetPropertyBlock(block);
    }

    private void Update()
    {
        Paint(Time.time - startedAt);
        if (Time.time < nextTick)
        {
            return;
        }

        nextTick = Time.time + tick;
        Touch();
    }

    private void Touch()
    {
        touched.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.5f, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            IDamageable victim = Overlaps[i].GetComponentInParent<IDamageable>();
            if (victim == null || !victim.IsAlive || !touched.Add(victim) || victim.Category != TargetCategory.Creature
                || !faction.WillAttack(victim.Faction))
            {
                continue;
            }

            // Only what stands in it, not what reaches over it from above.
            Vector3 at = ((Component)victim).transform.position;
            Vector3 flat = at - transform.position;
            flat.y = 0f;
            if (flat.magnitude > radius || Mathf.Abs(at.y - transform.position.y) > 2f)
            {
                continue;
            }

            if (status != null && victim is Damageable body)
            {
                StatusEffects.Apply(body, status, source);
            }

            if (damagePerSecond > 0f)
            {
                var hurt = new DamageInfo(source, null, damagePerSecond * tick, damageType, false, at, flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward,
                    0f, HitTechnique.OverTime);
                victim.ApplyDamage(hurt);
            }
        }
    }
}
