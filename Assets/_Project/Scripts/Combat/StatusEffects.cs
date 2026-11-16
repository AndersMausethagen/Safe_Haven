using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The status effects on a creature or the player (StatusEffectDefinition): poison ticking, chill slowing, roots holding.
/// Added by itself the first time something applies an effect. A new hit of the same effect restarts its time; the
/// slowest effect sets the player's speed. Ticks are marked <see cref="HitTechnique.OverTime"/>, so they show damage
/// numbers but no hit stop, shake, flinch or guard. Only the player's speed is changed: monsters keep walking.
/// </summary>
[DisallowMultipleComponent]
public sealed class StatusEffects : MonoBehaviour
{
    private const float TickSeconds = 0.5f;

    /// <summary>One effect at work.</summary>
    public sealed class Active
    {
        public StatusEffectDefinition Definition;
        public Combatant Source;
        public float EndsAt;
        public float NextTick;

        public float SecondsLeft => Mathf.Max(0f, EndsAt - Time.time);
    }

    private readonly List<Active> active = new List<Active>();
    private Damageable target;
    private PlayerMovement movement;

    public IReadOnlyList<Active> Current => active;

    /// <summary>Raised when an effect starts or ends, with the effect.</summary>
    public event Action<StatusEffectDefinition, bool> Changed;

    /// <summary>Puts <paramref name="effect"/> on <paramref name="target"/>. Null when it can't take it.</summary>
    /// <summary>A status was laid on something (co-op sends a friend's to the host's creature: CreaturePuppets).</summary>
    public static event System.Action<Damageable, StatusEffectDefinition, Combatant> Applied;

    public static StatusEffects Apply(Damageable target, StatusEffectDefinition effect, Combatant source)
    {
        if (target == null || effect == null || !target.IsAlive)
        {
            return null;
        }

        // Some can't take it at all (a poison-proof horse).
        if (target.TryGetComponent(out IStatusImmunity immune) && immune.Resists(effect))
        {
            return null;
        }

        if (!target.TryGetComponent(out StatusEffects effects))
        {
            effects = target.gameObject.AddComponent<StatusEffects>();
        }

        effects.Add(effect, source);
        Applied?.Invoke(target, effect, source);
        return effects;
    }

    /// <summary>What a hit by <paramref name="attack"/> leaves behind: only a hit that got through, and by its chance.</summary>
    public static void AfterHit(AttackDefinition attack, IDamageable victim, Combatant source, in DamageResult result)
    {
        if (attack != null && attack.AppliesStatus != null && result.Damage > 0f && !result.Blocked && !result.Parried
            && victim is Damageable target && UnityEngine.Random.value < attack.StatusChance)
        {
            Apply(target, attack.AppliesStatus, source);
        }
    }

    public bool Has(StatusEffectDefinition effect) => Find(effect) != null;

    /// <summary>Takes these effects off. Returns how many were at work.</summary>
    public int Cure(IReadOnlyList<StatusEffectDefinition> effects)
    {
        int cured = 0;
        for (int i = active.Count - 1; i >= 0 && effects != null; i--)
        {
            StatusEffectDefinition definition = active[i].Definition;
            for (int e = 0; e < effects.Count; e++)
            {
                if (effects[e] == definition)
                {
                    active.RemoveAt(i);
                    Changed?.Invoke(definition, false);
                    cured++;
                    break;
                }
            }
        }

        if (cured > 0)
        {
            UpdateSpeed();
        }

        return cured;
    }

    private void Awake()
    {
        target = GetComponent<Damageable>();
        movement = GetComponent<PlayerMovement>();
    }

    private void OnDisable()
    {
        active.Clear();
        UpdateSpeed();
    }

    private Active Find(StatusEffectDefinition effect)
    {
        foreach (Active entry in active)
        {
            if (entry.Definition == effect)
            {
                return entry;
            }
        }

        return null;
    }

    private void Add(StatusEffectDefinition effect, Combatant source)
    {
        Active entry = Find(effect);
        if (entry != null)
        {
            entry.EndsAt = Time.time + effect.Seconds;
            entry.Source = source;
            return;
        }

        active.Add(new Active { Definition = effect, Source = source, EndsAt = Time.time + effect.Seconds, NextTick = Time.time + TickSeconds });
        UpdateSpeed();
        if (movement != null)
        {
            GameMessages.Post($"{effect.DisplayName}!", MessageKind.Warning);
        }

        Changed?.Invoke(effect, true);
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Active entry = active[i];
            if (target == null || !target.IsAlive || Time.time >= entry.EndsAt)
            {
                active.RemoveAt(i);
                UpdateSpeed();
                Changed?.Invoke(entry.Definition, false);
            }
            else if (entry.Definition.DamagePerSecond > 0f && Time.time >= entry.NextTick)
            {
                entry.NextTick += TickSeconds;
                Tick(entry);
            }
        }
    }

    private void Tick(Active entry)
    {
        // A friend's copy of the host's creature (co-op): the host's game was sent the status (CoopCreatures › Status) and ticks it on the
        // creature that counts, whose health comes back here. Ticking here too sent every tick to the host as a blow: poison twice over.
        if (TryGetComponent(out MonsterAI copy) && copy.IsRemote)
        {
            return;
        }

        Vector3 at = transform.position + Vector3.up;
        var tick = new DamageInfo(entry.Source, null, entry.Definition.DamagePerSecond * TickSeconds, entry.Definition.DamageType, false, at, Vector3.up, 0f,
            HitTechnique.OverTime);
        target.ApplyDamage(tick);
    }

    private void UpdateSpeed()
    {
        if (movement == null)
        {
            return;
        }

        float scale = 1f;
        foreach (Active entry in active)
        {
            scale = Mathf.Min(scale, entry.Definition.MoveSpeed);
        }

        movement.SetSpeedScale(this, scale);
    }
}

/// <summary>Something that shrugs off some status effects altogether (a poison-proof horse): StatusEffects.Apply asks it first.</summary>
public interface IStatusImmunity
{
    bool Resists(StatusEffectDefinition effect);
}
