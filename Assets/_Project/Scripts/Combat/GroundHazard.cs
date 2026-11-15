using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>What a hazard is, for how fire and frost treat it.</summary>
public enum HazardElement
{
    None,
    /// <summary>Burning: sets webs, gas and dry grass alight; rain and frost put it out.</summary>
    Fire,
    /// <summary>A pool of bile or blight: frost freezes it harmless.</summary>
    Poison,
    /// <summary>Web: fire burns it away in a flash.</summary>
    Web,
    /// <summary>Frost, ice: puts fire out.</summary>
    Frost,
    /// <summary>Poison gas, fog: fire sets it off in a burst of flame.</summary>
    Gas,
}

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

    [Header("Fire and frost (2026-11-14)")]
    [SerializeField] private HazardElement element = HazardElement.None;
    [Tooltip("What it becomes when fire reaches it (a web burning, gas going up). Empty: fire passes it by.")]
    [SerializeField] private GameObject burnsInto;
    [Tooltip("What it becomes when frost reaches it (a frozen pool). Empty: frost passes it by.")]
    [SerializeField] private GameObject freezesInto;
    [Tooltip("Damage done once, to everyone in it, when it goes up in flame (gas).")]
    [SerializeField, Min(0f)] private float ignitionDamage;
    [Tooltip("Fire: it spreads to dry grass round it, a few times over, and burns it short.")]
    [SerializeField] private bool spreadsOnGrass;
    [Tooltip("Played over it, looping, while it lasts (a fire's crackle).")]
    [SerializeField] private SoundDefinition loopSound;

    private Combatant source;
    private Faction faction;
    private float startedAt;
    private float nextTick;
    private Renderer disc;
    private MaterialPropertyBlock block;
    private Color shown;
    private readonly HashSet<IDamageable> touched = new HashSet<IDamageable>();
    private static readonly List<GroundHazard> Active = new List<GroundHazard>();
    private const int MostFires = 16;
    private const int MostSpread = 3;
    private int generation;
    private float nextSpread;
    private bool spent;

    public float Radius => radius;
    public HazardElement Element => element;

    /// <summary>The kind of harm standing in it does (RaidHarms): its status's, its damage's, or its element's.</summary>
    public DamageType Harm =>
        status != null && status.DamageType != DamageType.Physical ? status.DamageType
        : damageType != DamageType.Physical ? damageType
        : element == HazardElement.Fire ? DamageType.Fire
        : element == HazardElement.Frost ? DamageType.Frost
        : element == HazardElement.Poison || element == HazardElement.Gas ? DamageType.Holy
        : DamageType.Physical;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Active.Clear();

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    /// <summary>
    /// Fire or frost arrives at <paramref name="at"/> (a burning charge, a fire blow, a frost stomp), reaching
    /// <paramref name="reach"/> metres: webs and gas there catch fire, pools freeze, fires go out, and fire on dry grass
    /// starts a grass fire.
    /// </summary>
    public static void Meet(HazardElement arriving, Vector3 at, float reach, Combatant by, GroundHazard from = null)
    {
        if (arriving != HazardElement.Fire && arriving != HazardElement.Frost)
        {
            return;
        }

        bool touchedAny = false;
        foreach (GroundHazard hazard in Active.ToArray())
        {
            if (hazard == null || hazard == from || hazard.spent)
            {
                continue;
            }

            Vector3 offset = hazard.transform.position - at;
            offset.y = 0f;
            if (offset.magnitude > reach + hazard.radius)
            {
                continue;
            }

            touchedAny |= arriving == HazardElement.Fire ? hazard.Ignite(by) : hazard.Freeze(by);
        }

        if (arriving == HazardElement.Fire && from == null && !touchedAny && by != null && !Wet() && GroundImpacts.IsGrassy(at))
        {
            StartGrassFire(at, by, 0);
        }
    }

    private static bool Wet()
    {
        WeatherSystem weather = WeatherSystem.Active;
        return weather != null && (weather.Wetness > 0.5f || weather.Snow > 0.3f);
    }

    private static void StartGrassFire(Vector3 at, Combatant by, int generation)
    {
        GameObject prefab = GroundImpactSettings.Current.GrassFire;
        if (prefab == null || CountFires() >= MostFires)
        {
            return;
        }

        GroundHazard fire = Spawn(prefab, at, by);
        if (fire != null)
        {
            fire.generation = generation;
            if (generation == 0)
            {
                AudioManager.Play(GroundImpactSettings.Current.IgniteSound, at);
            }
        }
    }

    private static int CountFires()
    {
        int fires = 0;
        foreach (GroundHazard hazard in Active)
        {
            if (hazard != null && hazard.element == HazardElement.Fire)
            {
                fires++;
            }
        }

        return fires;
    }

    // Fire reaches it. True when it changed.
    private bool Ignite(Combatant by)
    {
        if (burnsInto == null || element == HazardElement.Fire)
        {
            return false;
        }

        spent = true;
        Vector3 at = transform.position;
        GroundImpactSettings settings = GroundImpactSettings.Current;
        AudioManager.Play(element == HazardElement.Gas ? settings.GasIgniteSound : settings.IgniteSound, at);
        Combatant cause = by != null ? by : source;
        if (ignitionDamage > 0f && cause != null)
        {
            Burst(at, cause);
        }

        Spawn(burnsInto, at, cause);
        Destroy(gameObject);
        return true;
    }

    // Frost reaches it. True when it changed.
    private bool Freeze(Combatant by)
    {
        GroundImpactSettings settings = GroundImpactSettings.Current;
        if (element == HazardElement.Fire)
        {
            Douse();
            return true;
        }

        if (freezesInto == null)
        {
            return false;
        }

        spent = true;
        AudioManager.Play(settings.FreezeSound, transform.position);
        Spawn(freezesInto, transform.position, by != null ? by : source);
        Destroy(gameObject);
        return true;
    }

    private void Douse()
    {
        spent = true;
        AudioManager.Play(GroundImpactSettings.Current.DouseSound, transform.position);
        GameObject steam = GroundImpactSettings.Current.Steam;
        if (steam != null)
        {
            PrefabPool.Spawn(steam, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    // Gas going up: everyone in it not on the igniter's side is caught in the flash.
    private void Burst(Vector3 at, Combatant cause)
    {
        int count = Physics.OverlapSphereNonAlloc(at + Vector3.up * 0.5f, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        touched.Clear();
        for (int i = 0; i < count; i++)
        {
            IDamageable victim = Overlaps[i].GetComponentInParent<IDamageable>();
            if (victim == null || !victim.IsAlive || !touched.Add(victim) || victim.Category != TargetCategory.Creature || !cause.Faction.WillAttack(victim.Faction))
            {
                continue;
            }

            Vector3 where = ((Component)victim).transform.position;
            Vector3 outward = where - at;
            victim.ApplyDamage(new DamageInfo(cause, null, ignitionDamage, DamageType.Fire, false, where, outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward));
        }
    }

    // A fire burns what it stands on and reaches what lies near it: webs and gas go up, dry grass round it catches.
    private void Burn()
    {
        if (element != HazardElement.Fire || spent)
        {
            return;
        }

        if (Wet() && Time.time - startedAt > 0.8f)
        {
            Douse();
            return;
        }

        Meet(HazardElement.Fire, transform.position, radius * 0.6f, source, this);
        if (spreadsOnGrass && generation < MostSpread && Time.time >= nextSpread)
        {
            nextSpread = Time.time + Random.Range(1.2f, 2f);
            Vector2 step = Random.insideUnitCircle.normalized * Random.Range(radius * 1.2f, radius * 2f);
            Vector3 next = transform.position + new Vector3(step.x, 0f, step.y);
            bool taken = false;
            foreach (GroundHazard other in Active)
            {
                if (other != null && other.element == HazardElement.Fire && (other.transform.position - next).sqrMagnitude < radius * radius)
                {
                    taken = true;
                    break;
                }
            }

            if (!taken && GroundImpacts.IsGrassy(next))
            {
                StartGrassFire(next, source, generation + 1);
            }
        }
    }

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
        nextSpread = Time.time + Random.Range(0.8f, 1.5f);
        if (element == HazardElement.Fire && GroundImpacts.IsGrassy(transform.position))
        {
            // Burnt short and black where it burns, for the same two minutes as a blow's mark.
            GroundImpacts.Scorch(transform.position, radius, 120f);
        }
        shown = color.a > 0f ? color : status != null ? status.Color : new Color(0.4f, 0.6f, 0.2f, 1f);
        shown.a = 0.45f;
        MakeDisc();
        if (effectPrefab != null)
        {
            Instantiate(effectPrefab, transform.position, Quaternion.identity, transform);
        }

        if (loopSound != null && loopSound.HasClips)
        {
            var voice = gameObject.AddComponent<AudioSource>();
            AudioManager.Configure(voice, loopSound);
            voice.clip = loopSound.PickClip();
            voice.loop = true;
            voice.time = Random.Range(0f, voice.clip.length);
            voice.Play();
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
        Burn();
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
