using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The status effects on a creature or the player (StatusEffectDefinition): poison ticking, chill slowing, roots holding.
/// Added by itself the first time something applies an effect. A new hit of the same effect restarts its time; the
/// slowest effect sets the player's speed. Ticks are marked <see cref="HitTechnique.OverTime"/>, so they show damage
/// numbers but no hit stop, shake, flinch or guard. The slowest effect sets the player's speed here, and a creature's (MonsterAI asks
/// MoveScale); one that Stops Strikes (Frozen) holds a creature's blows while it lasts.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.HostOnly, "a status this player lays is sent to the host's creature, which ticks it; the copy's own don't tick")]
public sealed class StatusEffects : MonoBehaviour, ICopyAware
{
    private const float TickSeconds = 0.5f;

    /// <summary>One effect at work.</summary>
    public sealed class Active
    {
        public StatusEffectDefinition Definition;
        public Combatant Source;
        /// <summary>The source's Damage Bonus as it laid the status (DamageCalculator.BonusScale): what its ticks are multiplied by.</summary>
        public float Scale = 1f;
        public float EndsAt;
        public float NextTick;

        public float SecondsLeft => Mathf.Max(0f, EndsAt - Time.time);
    }

    private readonly List<Active> active = new List<Active>();
    private Damageable target;
    private PlayerMovement movement;
    private MonsterAI creatureMind;

    public IReadOnlyList<Active> Current => active;

    /// <summary>Raised when an effect starts or ends, with the effect.</summary>
    public event Action<StatusEffectDefinition, bool> Changed;

    /// <summary>Puts <paramref name="effect"/> on <paramref name="target"/>. Null when it can't take it.</summary>
    /// <summary>A status was laid on something (co-op sends a friend's to the host's creature: CreaturePuppets).</summary>
    public static event System.Action<Damageable, StatusEffectDefinition, Combatant> Applied;

    /// <summary>A status ended on something (ran out, cured, torn free, burst, or its bearer died): CreatureHost tells friends.</summary>
    public static event System.Action<Damageable, StatusEffectDefinition> EndedOn;

    /// <summary>
    /// On a friend's copy of the host's creature: what the host's game says is at work on it, for its look (the outline, the icon, the
    /// effect). <paramref name="seconds"/> left; 0 ends it. The copy's statuses never tick (CopyRule.HostOnly), so this only shows.
    /// </summary>
    public static void Mirror(Damageable target, StatusEffectDefinition effect, float seconds)
    {
        if (target == null || effect == null)
        {
            return;
        }

        if (seconds <= 0f)
        {
            if (target.TryGetComponent(out StatusEffects shown))
            {
                shown.EndNow(effect);
            }

            return;
        }

        // The host decided it took hold: the copy's own rest (its local copy of the last one ending, heard a moment late) can't refuse it.
        if (target.TryGetComponent(out StatusEffects resting))
        {
            resting.restUntil.Remove(effect);
        }

        StatusEffects effects = Apply(target, effect, null);
        Active entry = effects != null ? effects.Find(effect) : null;
        if (entry != null)
        {
            entry.EndsAt = Time.time + seconds;
        }
    }

    private void EndNow(StatusEffectDefinition effect)
    {
        Active entry = Find(effect);
        if (entry == null)
        {
            return;
        }

        active.Remove(entry);
        Ended(effect);
        UpdateSpeed();
        RaiseEnded(effect);
    }

    // Changed(false) for this bearer and Ended for everyone listening (co-op).
    private void RaiseEnded(StatusEffectDefinition effect)
    {
        Changed?.Invoke(effect, false);
        EndedOn?.Invoke(target != null ? target : GetComponent<Damageable>(), effect);
    }

    /// <summary>A status burst by a blow (Shatter): its bearer, the status and where (StatusBearerLook shows it).</summary>
    public static event System.Action<Damageable, StatusEffectDefinition, Vector3> Consumed;

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

        if (!effects.Add(effect, source))
        {
            return null; // resting after it ended (Rest Seconds): it doesn't take hold, and nothing is sent on
        }

        Applied?.Invoke(target, effect, source);
        return effects;
    }

    /// <summary>
    /// What a blow that got through bursts (Status Effect › Consumed By; a heavy on a frozen creature shatters it): the status ends at once
    /// and its bearer takes Consume Damage, a share of the blow, as its own Damage Type. Asked before the blow's own statuses are laid,
    /// so a blow never bursts what it lays itself.
    /// </summary>
    public static void BeforeHitStatuses(IDamageable victim, in DamageInfo blow, in DamageResult result)
    {
        if (blow.Technique == HitTechnique.None || blow.IsOverTime || result.Damage <= 0f || result.Blocked || result.Parried
            || !(victim is Damageable target) || !target.IsAlive || !target.TryGetComponent(out StatusEffects effects) || effects.isCopy)
        {
            return;
        }

        for (int i = effects.active.Count - 1; i >= 0; i--)
        {
            StatusEffectDefinition definition = effects.active[i].Definition;
            if (definition == null || (definition.ConsumedBy & blow.Technique) == 0 || Time.time >= effects.active[i].EndsAt)
            {
                continue;
            }

            effects.active.RemoveAt(i);
            effects.Ended(definition);
            effects.UpdateSpeed();
            effects.RaiseEnded(definition);
            Consumed?.Invoke(target, definition, blow.HitPoint);
            if (!string.IsNullOrWhiteSpace(definition.ConsumeLine) && blow.Source != null && blow.Source.GetComponent<Player>() != null)
            {
                GameMessages.Post(definition.ConsumeLine, MessageKind.Gain);
            }

            if (definition.ConsumeDamage > 0f && target.IsAlive)
            {
                // A finisher's weight; not a technique that could burst another status in turn.
                var burst = new DamageInfo(blow.Source, null, result.Damage * definition.ConsumeDamage, definition.DamageType, false,
                    blow.HitPoint, blow.Direction, 0f, HitTechnique.None);
                DamageResult burstResult = target.ApplyDamage(burst);
                // The striker's as the blow was (lifesteal, Focus, a kill's Second Wind); no technique, so it bursts nothing in turn.
                if (blow.Source != null)
                {
                    blow.Source.ReportDamageDealt(target, burst, burstResult);
                }
            }
        }
    }

    /// <summary>What a hit by <paramref name="attack"/> leaves behind: only a hit that got through, and by its chance.</summary>
    public static void AfterHit(AttackDefinition attack, IDamageable victim, Combatant source, in DamageResult result)
    {
        if (attack != null && attack.AppliesStatus != null && result.Damage > 0f && !result.Blocked && !result.Parried
            && victim is Damageable target && UnityEngine.Random.value < attack.StatusChance)
        {
            // Already under it: the attack's stronger status on top (Frost Rune on a chilled creature freezes it).
            bool already = attack.OnRepeatStatus != null && target.TryGetComponent(out StatusEffects effects) && effects.Has(attack.AppliesStatus);
            Apply(target, attack.AppliesStatus, source);
            if (already)
            {
                Apply(target, attack.OnRepeatStatus, source);
            }
        }
    }

    public bool Has(StatusEffectDefinition effect) => Find(effect) != null;

    /// <summary>How much of its bearer's damage its blows deal while these last (Status Effect › Damage Dealt; Cowed): 1 when none asks.</summary>
    public float DamageDealtScale
    {
        get
        {
            float scale = 1f;
            foreach (Active entry in active)
            {
                if (entry.Definition != null && Time.time < entry.EndsAt)
                {
                    scale *= entry.Definition.DamageDealt;
                }
            }

            return scale;
        }
    }

    /// <summary>The slowest of what is at work (Status Effect › Move Speed): 1 unhindered, 0 held fast. The player's speed is set from it
    /// here; a creature's movement asks for it (MonsterAI).</summary>
    public float MoveScale
    {
        get
        {
            float scale = 1f;
            foreach (Active entry in active)
            {
                if (entry.Definition != null && Time.time < entry.EndsAt)
                {
                    scale = Mathf.Min(scale, entry.Definition.MoveSpeed);
                }
            }

            return scale;
        }
    }

    /// <summary>How much more its bearer takes while these last (Status Effect › Damage Taken; Sundered): 1 when none asks for more.</summary>
    public float DamageTakenScale
    {
        get
        {
            float scale = 1f;
            foreach (Active entry in active)
            {
                if (entry.Definition != null && Time.time < entry.EndsAt)
                {
                    scale *= entry.Definition.DamageTaken;
                }
            }

            return scale;
        }
    }

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
                    Ended(definition);
                    RaiseEnded(definition);
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

    // On a friend's copy of the host's creature: the host's game ticks the statuses (CopyRule.HostOnly).
    private bool isCopy;

    private void Awake()
    {
        target = GetComponent<Damageable>();
        movement = GetComponent<PlayerMovement>();
        // Laid on a creature already a copy (added with the first status).
        isCopy = TryGetComponent(out MonsterAI creature) && creature.IsRemote;
    }

    public void OnCopyChanged(bool copy) => isCopy = copy;

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

    // False when it can't take hold now (resting after it ended).
    private bool Add(StatusEffectDefinition effect, Combatant source)
    {
        // Resting after it ended (Rest Seconds): the next ring of a volley can't take hold of a player who has just torn free.
        if (restUntil.TryGetValue(effect, out float resting) && !StatusTiming.MayTakeHold(Time.time, resting))
        {
            return false;
        }

        Active entry = Find(effect);
        if (entry != null)
        {
            entry.EndsAt = StatusTiming.EndsAt(effect, true, entry.EndsAt, Time.time);
            entry.Source = source;
            entry.Scale = DamageCalculator.BonusScale(source);
            HoldStrikes(entry.Definition, entry.EndsAt);
            return true;
        }

        active.Add(new Active
        {
            Definition = effect, Source = source, Scale = DamageCalculator.BonusScale(source), EndsAt = StatusTiming.EndsAt(effect, false, 0f, Time.time),
            NextTick = Time.time + TickSeconds,
        });
        UpdateSpeed();
        if (movement != null)
        {
            GameMessages.Post(effect.TakesHoldLine, MessageKind.Warning);
        }

        HoldStrikes(effect, active[active.Count - 1].EndsAt);
        Changed?.Invoke(effect, true);
        return true;
    }

    // A creature held fast (Frozen) breaks off its strike and strikes again only once it is free.
    private void HoldStrikes(StatusEffectDefinition effect, float endsAt)
    {
        if (effect != null && effect.StopsStrikes && !isCopy && TryGetComponent(out MonsterAI creature))
        {
            creature.HoldStrikes(endsAt - Time.time);
        }
    }

    // When each effect that ended may take hold again (Status Effect › Rest Seconds).
    private readonly Dictionary<StatusEffectDefinition, float> restUntil = new Dictionary<StatusEffectDefinition, float>();

    private void Ended(StatusEffectDefinition effect)
    {
        if (effect != null && effect.RestSeconds > 0f)
        {
            restUntil[effect] = StatusTiming.RestUntil(effect, Time.time);
        }
    }

    /// <summary>A roll tears the player free of what a roll ends (Status Effect › Ends On Dodge: Roots). Returns how many it ended.</summary>
    public int EndOnDodge()
    {
        int ended = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            StatusEffectDefinition definition = active[i].Definition;
            if (definition != null && definition.EndsOnDodge)
            {
                active.RemoveAt(i);
                Ended(definition);
                RaiseEnded(definition);
                ended++;
            }
        }

        if (ended > 0)
        {
            UpdateSpeed();
        }

        return ended;
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Active entry = active[i];
            if (target == null || !target.IsAlive || Time.time >= entry.EndsAt)
            {
                active.RemoveAt(i);
                Ended(entry.Definition);
                UpdateSpeed();
                RaiseEnded(entry.Definition);
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
        if (isCopy)
        {
            return;
        }

        Vector3 at = transform.position + Vector3.up;
        var tick = new DamageInfo(entry.Source, null, entry.Definition.DamagePerSecond * TickSeconds * entry.Scale, entry.Definition.DamageType, false, at, Vector3.up, 0f,
            HitTechnique.OverTime);
        target.ApplyDamage(tick);
    }

    private void UpdateSpeed()
    {
        if (movement == null)
        {
            // A creature's speed is its own to set: told to look again (MonsterAI asks MoveScale).
            if (creatureMind != null || TryGetComponent(out creatureMind))
            {
                creatureMind.RefreshSpeed();
            }

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
