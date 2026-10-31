using System;
using UnityEngine;

/// <summary>
/// What a relic does beyond numbers: it changes how the wearer plays while it is worn (GDD §13.6). Authored on a relic's
/// EnchantmentDefinition, so one object serves every wearer and must keep no state: Attach starts it on a wearer and returns
/// what ends it, and whatever it remembers lives in that closure. To add a kind: derive from this with a [Serializable]
/// class and a parameterless constructor; the inspector's menu finds it by itself.
/// </summary>
[Serializable]
public abstract class RelicEffect
{
    /// <summary>The name in the inspector's menu.</summary>
    public abstract string DisplayName { get; }

    /// <summary>What it does, in a line for the tooltip.</summary>
    public abstract string Describe();

    /// <summary>Starts it on a wearer. Returns what takes it off again.</summary>
    public abstract Action Attach(PlayerRelics wearer);

    protected static string Percent(float share) => Mathf.RoundToInt(share * 100f) + "%";
}

/// <summary>Every kill gives back health and stamina, so fights feed the next fight.</summary>
[Serializable]
public sealed class FedByKillsEffect : RelicEffect
{
    [SerializeField, Range(0f, 1f)] private float health = 0.06f;
    [SerializeField, Range(0f, 1f)] private float stamina = 0.2f;

    public override string DisplayName => "Fed by Kills";
    public override string Describe() => $"Every kill gives back {Percent(health)} health and {Percent(stamina)} stamina.";

    public override Action Attach(PlayerRelics wearer)
    {
        void OnDied(Damageable victim, DamageInfo blow)
        {
            if (blow.Source == null || blow.Source != wearer.Combatant || victim == wearer.Health)
            {
                return;
            }

            wearer.Health.Heal(wearer.Health.MaxHealth * health);
            wearer.Stamina.Restore(wearer.Stamina.Max * stamina);
        }

        Damageable.AnyDied += OnDied;
        return () => Damageable.AnyDied -= OnDied;
    }
}

/// <summary>Everything the wearer does carries less far, so a raid can be walked past instead of fought through.</summary>
[Serializable]
public sealed class HushedEffect : RelicEffect
{
    [Tooltip("What share of a noise's reach is left.")]
    [SerializeField, Range(0.1f, 1f)] private float reach = 0.5f;

    public override string DisplayName => "Hushed";
    public override string Describe() => $"Every sound you make carries only {Percent(reach)} as far.";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerNoise noise = wearer.Noise;
        if (noise == null)
        {
            return () => { };
        }

        noise.Reach *= reach;
        return () => noise.Reach /= reach;
    }
}

/// <summary>Once each outing, a blow that would kill leaves the wearer standing.</summary>
[Serializable]
public sealed class DeathlessEffect : RelicEffect
{
    [Tooltip("Share of health the wearer gets back.")]
    [SerializeField, Range(0.05f, 1f)] private float rises = 0.3f;

    public override string DisplayName => "Deathless";
    public override string Describe() => $"Once each raid, a killing blow leaves you standing with {Percent(rises)} health.";

    public override Action Attach(PlayerRelics wearer)
    {
        if (wearer.IsSpent(this))
        {
            return () => { };
        }

        Damageable health = wearer.Health;
        const float Floor = 1f;
        health.HealthFloor = Floor;

        void Stop()
        {
            health.HealthFloor = 0f;
            health.Damaged -= OnHit;
        }

        void OnHit(DamageInfo hit, DamageResult result)
        {
            if (health.CurrentHealth > Floor)
            {
                return;
            }

            Stop();
            wearer.Spend(this);
            health.Heal(health.MaxHealth * rises);
            GameMessages.Post("Death looks you over, and leaves you for later.", MessageKind.Warning);
        }

        health.Damaged += OnHit;
        return Stop;
    }
}

/// <summary>Strong in the dark and weak by day, so the hour a raid rolls decides how it is played.</summary>
[Serializable]
public sealed class MoonboundEffect : RelicEffect
{
    [Tooltip("Extra damage at the dark of night.")]
    [SerializeField, Range(0f, 1f)] private float darkDamage = 0.3f;
    [Tooltip("Extra speed at the dark of night.")]
    [SerializeField, Range(0f, 0.5f)] private float darkSpeed = 0.1f;
    [Tooltip("Damage lost in full daylight.")]
    [SerializeField, Range(0f, 1f)] private float dayDamage = 0.15f;

    public override string DisplayName => "Moonbound";
    public override string Describe() => $"In the dark, {Percent(darkDamage)} more damage and {Percent(darkSpeed)} more speed. By day, {Percent(dayDamage)} less damage.";

    public override Action Attach(PlayerRelics wearer)
    {
        var source = new object();
        StatSheet stats = wearer.Combatant.Stats;
        Action stop = wearer.Every(1f, () =>
        {
            float dark = NightSettings.Darkness;
            stats.RemoveModifiersFrom(source);
            stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, Mathf.Lerp(-dayDamage, darkDamage, dark), source));
            stats.AddModifier(new StatModifier(StatType.MoveSpeed, StatModifierOp.Flat, darkSpeed * dark, source));
        });

        return () =>
        {
            stop();
            stats.RemoveModifiersFrom(source);
        };
    }
}

/// <summary>A dodge is followed by a burst of speed, so running away is a real plan.</summary>
[Serializable]
public sealed class AfterDodgeSpeedEffect : RelicEffect
{
    [SerializeField, Range(0f, 1f)] private float speed = 0.4f;
    [SerializeField, Min(0.1f)] private float seconds = 2f;

    public override string DisplayName => "Speed After a Dodge";
    public override string Describe() => $"After a dodge, {Percent(speed)} more speed for {seconds:0.#} seconds.";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerDodge dodge = wearer.Dodge;
        if (dodge == null)
        {
            return () => { };
        }

        var source = new object();
        StatSheet stats = wearer.Combatant.Stats;
        float until = 0f;

        void OnDodged(Vector3 direction)
        {
            until = Time.time + seconds;
            stats.RemoveModifiersFrom(source);
            stats.AddModifier(new StatModifier(StatType.MoveSpeed, StatModifierOp.Flat, speed, source));
        }

        Action stop = wearer.Every(0.1f, () =>
        {
            if (until > 0f && Time.time >= until)
            {
                until = 0f;
                stats.RemoveModifiersFrom(source);
            }
        });
        dodge.Dodged += OnDodged;
        return () =>
        {
            dodge.Dodged -= OnDodged;
            stop();
            stats.RemoveModifiersFrom(source);
        };
    }
}

/// <summary>The fog of war lifts far around the wearer, so a new map gives up its secrets sooner.</summary>
[Serializable]
public sealed class FarSeeingEffect : RelicEffect
{
    [SerializeField, Min(1f)] private float radius = 32f;

    public override string DisplayName => "Far-Seeing";
    public override string Describe() => $"The map fills in {radius:0} metres around you.";

    public override Action Attach(PlayerRelics wearer)
    {
        return wearer.Every(0.5f, () =>
        {
            if (ExplorationMap.Current != null)
            {
                ExplorationMap.Current.RevealAround(wearer.transform.position, radius);
            }
        });
    }
}

/// <summary>Wounds close by themselves once the fighting stops, so a raid can go on longer without supplies.</summary>
[Serializable]
public sealed class MendingEffect : RelicEffect
{
    [Tooltip("Share of health back each second.")]
    [SerializeField, Range(0f, 0.2f)] private float perSecond = 0.025f;
    [Tooltip("Seconds without being hurt before it starts.")]
    [SerializeField, Min(0f)] private float calm = 8f;

    public override string DisplayName => "Mending";
    public override string Describe() => $"After {calm:0} seconds unhurt, wounds close by {Mathf.Round(perSecond * 1000f) / 10f}% each second.";

    public override Action Attach(PlayerRelics wearer)
    {
        Damageable health = wearer.Health;
        float hurtAt = -calm;

        void OnHit(DamageInfo hit, DamageResult result) => hurtAt = Time.time;

        health.Damaged += OnHit;
        Action stop = wearer.Every(1f, () =>
        {
            if (health.IsAlive && Time.time - hurtAt >= calm && health.CurrentHealth < health.MaxHealth)
            {
                health.Heal(health.MaxHealth * perSecond);
            }
        });

        return () =>
        {
            health.Damaged -= OnHit;
            stop();
        };
    }
}

/// <summary>Hits can set a status on what they strike, such as fire.</summary>
[Serializable]
public sealed class StatusOnHitEffect : RelicEffect
{
    [SerializeField] private StatusEffectDefinition status;
    [SerializeField, Range(0f, 1f)] private float chance = 0.3f;

    public override string DisplayName => "Status on Hit";
    public override string Describe() => status == null ? "" : $"{Percent(chance)} of your hits leave {status.DisplayName} behind.";

    public override Action Attach(PlayerRelics wearer)
    {
        void OnDamaged(Damageable victim, DamageInfo hit, DamageResult result)
        {
            if (status != null && hit.Source != null && hit.Source == wearer.Combatant && victim != wearer.Health && UnityEngine.Random.value < chance)
            {
                StatusEffects.Apply(victim, status, wearer.Combatant);
            }
        }

        Damageable.AnyDamaged += OnDamaged;
        return () => Damageable.AnyDamaged -= OnDamaged;
    }
}
