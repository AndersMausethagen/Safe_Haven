using UnityEngine;

/// <summary>
/// A creature on fire (a fire arrow): it takes Fire damage every half second for a while, glowing as it burns. A new
/// fire on a burning creature restarts the time and keeps the stronger burn. The ticks are marked
/// <see cref="HitTechnique.OverTime"/>, so they show damage numbers but no hit stop, shake or flinch.
/// </summary>
[DisallowMultipleComponent]
public sealed class Burning : MonoBehaviour
{
    private const float TickSeconds = 0.5f;

    private Damageable target;
    private Combatant source;
    private float damagePerSecond;
    private float endsAt;
    private float nextTick;
    private Light glow;

    public bool IsBurning => enabled && Time.time < endsAt;

    /// <summary>Sets <paramref name="target"/> burning for <paramref name="seconds"/>.</summary>
    public static Burning Apply(Damageable target, Combatant source, float damagePerSecond, float seconds)
    {
        if (target == null || !target.IsAlive || damagePerSecond <= 0f || seconds <= 0f)
        {
            return null;
        }

        if (!target.TryGetComponent(out Burning burning))
        {
            burning = target.gameObject.AddComponent<Burning>();
        }

        bool fresh = !burning.IsBurning;
        burning.target = target;
        burning.source = source;
        // As strong as who set it alight hits (their Damage Bonus: a raid's tier, an elite, a boss's phase).
        damagePerSecond *= DamageCalculator.BonusScale(source);
        burning.damagePerSecond = fresh ? damagePerSecond : Mathf.Max(burning.damagePerSecond, damagePerSecond);
        burning.endsAt = Time.time + seconds;
        if (fresh)
        {
            // The first tick comes half a second after catching fire, not all the missed ones at once.
            burning.nextTick = Time.time + TickSeconds;
            burning.enabled = true;
        }

        return burning;
    }

    private void Awake()
    {
        var holder = new GameObject("Burning Glow");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = Vector3.up;
        glow = holder.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.2f);
        glow.range = 4f;
        glow.shadows = LightShadows.None;
    }

    private void OnEnable()
    {
        if (glow != null)
        {
            glow.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (glow != null)
        {
            glow.enabled = false;
        }
    }

    private void Update()
    {
        if (target == null || !target.IsAlive || Time.time >= endsAt)
        {
            enabled = false;
            return;
        }

        glow.intensity = 2f + Mathf.PerlinNoise(Time.time * 6f, 0.3f) * 1.5f;
        if (Time.time < nextTick)
        {
            return;
        }

        nextTick += TickSeconds;
        Vector3 at = target.transform.position + Vector3.up;
        var tick = new DamageInfo(source, null, damagePerSecond * TickSeconds, DamageType.Fire, false, at, Vector3.up, 0f, HitTechnique.OverTime);
        // Not reported as damage dealt: ticks are no blows, so they don't wear the bow or earn Focus. A kill still
        // names the source.
        target.ApplyDamage(tick);
    }
}
