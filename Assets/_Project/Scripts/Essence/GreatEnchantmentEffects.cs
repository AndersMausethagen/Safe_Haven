using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Great enchantments (Docs/EnchantingResearch.md #7): verbs, not numbers. Each hooks something the player already does (a perfect
// dodge, a counter, a backstab, a parry, a kill, a heavy roll, a weapon art, a cast) and changes what it does. The rules they keep:
// never attack speed (clips keep their cadence), the blow order stays (plain, mid-strike backstab, counter, staggered backstab), and
// whatever number they add is small and counted by the raid tiers' balance (GearBalance). Like every RelicEffect they keep no state on
// the asset: Attach starts one on a wearer and its closure holds what it remembers. An enchantment counts once (PlayerRelics), so two
// pieces never double one. Effects on creatures act on this game's creature; a friend's are sent to the host (CoopCreatures › Status).

/// <summary>After a perfect dodge, the next swing costs no stamina: Frenzied.</summary>
[Serializable]
public sealed class FreeSwingAfterPerfectDodgeEffect : RelicEffect
{
    [Tooltip("Seconds the free swing waits after the perfect dodge.")]
    [SerializeField, Min(0.5f)] private float within = 3f;

    public override string DisplayName => "Free Swing After a Perfect Dodge";
    public override string Describe() => $"After a perfect dodge, your next swing within {within:0} s costs no stamina.";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerCombat combat = wearer.Combat;
        if (combat == null || wearer.Stamina == null)
        {
            return () => { };
        }

        float until = -1f;
        void OnPerfect(DamageInfo dodged) => until = Time.time + within;

        void OnSwing(SwingInfo swing)
        {
            if (Time.time > until || swing.Attack == null)
            {
                return;
            }

            until = -1f;
            // What the swing really took (an attack's Stamina Cost of -1 means the default, which came back as nothing).
            wearer.Stamina.Restore(combat.LastSwingStaminaPaid);
        }

        combat.PerfectDodge += OnPerfect;
        combat.SwingStarted += OnSwing;
        return () =>
        {
            combat.PerfectDodge -= OnPerfect;
            combat.SwingStarted -= OnSwing;
        };
    }
}

/// <summary>
/// Blows of a kind lay a status on what they strike: counters and backstabs Chill (Frostbound), charged blows Root (Rootbinder), a
/// perfect release pins (Pinning). The status's own rules decide what it does to the creature.
/// </summary>
[Serializable]
public sealed class StatusOnTechniqueEffect : RelicEffect
{
    [SerializeField] private StatusEffectDefinition status;
    [Tooltip("Blows with any of these count.")]
    [SerializeField] private HitTechnique techniques = HitTechnique.Counter | HitTechnique.Backstab;
    [Tooltip("Only critical blows count (a bow's perfect release is a sure critical).")]
    [SerializeField] private bool criticalOnly;
    [Tooltip("The words for the blows, in the tooltip: \"counters and backstabs\".")]
    [SerializeField] private string blows = "Counters and backstabs";

    public override string DisplayName => "Status on a Kind of Blow";
    public override string Describe() => status == null ? "" : $"{blows} leave {status.DisplayName} on what they strike.";

    public override Action Attach(PlayerRelics wearer)
    {
        void OnDamaged(Damageable victim, DamageInfo hit, DamageResult result)
        {
            if (status == null || hit.Source != wearer.Combatant || victim == wearer.Health || result.Damage <= 0f
                || (hit.Technique & techniques) == 0 || (criticalOnly && !hit.IsCritical))
            {
                return;
            }

            StatusEffects.Apply(victim, status, wearer.Combatant);
        }

        Damageable.AnyDamaged += OnDamaged;
        return () => Damageable.AnyDamaged -= OnDamaged;
    }
}

/// <summary>Regain wins back more of what a blow took: Regrowing.</summary>
[Serializable]
public sealed class RegainBoostEffect : RelicEffect
{
    [SerializeField, Range(1f, 3f)] private float scale = 1.75f;

    public override string DisplayName => "Stronger Regain";
    public override string Describe() => $"Striking back wins {Percent(scale - 1f)} more of a hit back (Regain).";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerRegain regain = wearer.Regain;
        if (regain == null)
        {
            return () => { };
        }

        regain.WonScale *= scale;
        return () => regain.WonScale /= scale;
    }
}

/// <summary>The first blow of a fight (after a calm) can't make the wearer flinch: Unyielding.</summary>
[Serializable]
public sealed class FirstBlowUnshakenEffect : RelicEffect
{
    [Tooltip("Seconds unhurt before the ward is ready again.")]
    [SerializeField, Min(1f)] private float calm = 8f;

    public override string DisplayName => "The First Blow Doesn't Shake You";
    public override string Describe() => $"The first heavy blow of each fight can't make you flinch (ready again after {calm:0} s unhurt).";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerCombat combat = wearer.Combat;
        if (combat == null)
        {
            return () => { };
        }

        float hurtAt = -calm;
        void OnHit(DamageInfo hit, DamageResult result) => hurtAt = Time.time;
        wearer.Health.Damaged += OnHit;
        Action stop = wearer.Every(0.5f, () =>
        {
            if (Time.time - hurtAt >= calm)
            {
                combat.FlinchWarded = true;
            }
        });
        combat.FlinchWarded = true;
        return () =>
        {
            wearer.Health.Damaged -= OnHit;
            stop();
            combat.FlinchWarded = false;
        };
    }
}

/// <summary>A perfect dodge leaves mist: creatures lose sight of the wearer for a moment, so an ambush is possible again: Mistcloaked.</summary>
[Serializable]
public sealed class MistAfterPerfectDodgeEffect : RelicEffect
{
    [SerializeField, Min(0.2f)] private float seconds = 1.5f;

    public override string DisplayName => "Mist After a Perfect Dodge";
    public override string Describe() => $"A perfect dodge leaves mist: for {seconds:0.#} s creatures can hardly see you.";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerCombat combat = wearer.Combat;
        PlayerStealth stealth = wearer.Stealth;
        if (combat == null || stealth == null)
        {
            return () => { };
        }

        void OnPerfect(DamageInfo dodged) => stealth.Veil(seconds);
        combat.PerfectDodge += OnPerfect;
        return () => combat.PerfectDodge -= OnPerfect;
    }
}

/// <summary>Each blow in a string on one creature wears its poise more than the last: Relentless.</summary>
[Serializable]
public sealed class PoiseStringEffect : RelicEffect
{
    [Tooltip("Extra share of a blow's poise for each blow before it in the string.")]
    [SerializeField, Range(0f, 1f)] private float perBlow = 0.2f;
    [Tooltip("The string breaks after this many seconds without a blow on it.")]
    [SerializeField, Min(0.3f)] private float gap = 1.6f;
    [SerializeField, Min(1)] private int most = 5;

    public override string DisplayName => "Relentless String";
    public override string Describe() => $"Each blow in a row on one creature wears its poise {Percent(perBlow)} more than the last.";

    public override Action Attach(PlayerRelics wearer)
    {
        MonsterAI target = null;
        float lastAt = -10f;
        int streak = 0;

        void OnDamaged(Damageable victim, DamageInfo hit, DamageResult result)
        {
            if (hit.Source != wearer.Combatant || hit.Poise <= 0f || (hit.Technique & (HitTechnique.Melee | HitTechnique.WeaponArt)) == 0
                || !victim.TryGetComponent(out MonsterAI monster))
            {
                return;
            }

            streak = monster == target && Time.time - lastAt <= gap ? Mathf.Min(streak + 1, most) : 0;
            target = monster;
            lastAt = Time.time;
            if (streak > 0)
            {
                monster.TakePoise(hit.Poise * perBlow * streak);
            }
        }

        Damageable.AnyDamaged += OnDamaged;
        return () => Damageable.AnyDamaged -= OnDamaged;
    }
}

/// <summary>What the wearer kills bursts a moment later, hurting what stands near it: Vengeful.</summary>
[Serializable]
public sealed class DeathBurstEffect : RelicEffect
{
    [SerializeField, Min(0f)] private float delay = 1f;
    [Tooltip("Share of the dead creature's health dealt to each creature near it.")]
    [SerializeField, Range(0f, 1f)] private float share = 0.25f;
    [SerializeField, Min(0.5f)] private float radius = 3.5f;
    [SerializeField] private GameObject burstEffect;
    [SerializeField] private SoundDefinition burstSound;

    // A burst's own kills don't burst again: one blow must not clear a camp.
    private static bool bursting;

    public override string DisplayName => "Burst on Kill";
    public override string Describe() => $"What you kill bursts after {delay:0.#} s: {Percent(share)} of its health to everything within {radius:0.#} m.";

    public override Action Attach(PlayerRelics wearer)
    {
        void OnDied(Damageable victim, DamageInfo blow)
        {
            if (bursting || blow.Source != wearer.Combatant || victim == wearer.Health || victim.TryGetComponent(out Combatant dead) && dead.Faction.IsPlayerSide())
            {
                return;
            }

            wearer.StartCoroutine(Burst(wearer.Combatant, victim.transform.position + Vector3.up, victim.MaxHealth * share));
        }

        Damageable.AnyDied += OnDied;
        return () => Damageable.AnyDied -= OnDied;
    }

    private IEnumerator Burst(Combatant by, Vector3 at, float damage)
    {
        yield return new WaitForSeconds(delay);
        if (by == null)
        {
            yield break;
        }

        if (burstEffect != null)
        {
            UnityEngine.Object.Destroy(UnityEngine.Object.Instantiate(burstEffect, at, Quaternion.identity), 4f);
        }

        AudioManager.Play(burstSound, at);
        bursting = true;
        try
        {
            foreach (Damageable near in Near.Enemies(at, radius, by))
            {
                Vector3 away = near.transform.position - at;
                near.ApplyDamage(new DamageInfo(by, null, damage, DamageType.Physical, false, near.transform.position + Vector3.up, away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward));
            }
        }
        finally
        {
            bursting = false;
        }
    }
}

/// <summary>Companions near the wearer hit harder: Rallying. A support enchantment: nothing for the wearer alone.</summary>
[Serializable]
public sealed class RallyingEffect : RelicEffect
{
    [SerializeField, Range(0f, 0.5f)] private float damage = 0.1f;
    [SerializeField, Min(1f)] private float radius = 9f;

    public override string DisplayName => "Rallying";
    public override string Describe() => $"Companions within {radius:0} m hit {Percent(damage)} harder. Nothing for you alone.";

    public override Action Attach(PlayerRelics wearer)
    {
        var source = new object();
        var rallied = new HashSet<Combatant>();
        Action stop = wearer.Every(1f, () =>
        {
            foreach (Combatant was in rallied)
            {
                if (was != null)
                {
                    was.Stats.RemoveModifiersFrom(source);
                }
            }

            rallied.Clear();
            foreach (Companion companion in Companion.Active)
            {
                if (companion != null && companion.TryGetComponent(out Combatant fighter)
                    && (companion.transform.position - wearer.transform.position).sqrMagnitude <= radius * radius)
                {
                    fighter.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, damage, source));
                    rallied.Add(fighter);
                }
            }
        });
        return () =>
        {
            stop();
            foreach (Combatant was in rallied)
            {
                if (was != null)
                {
                    was.Stats.RemoveModifiersFrom(source);
                }
            }
        };
    }
}

/// <summary>A kill with a weapon art gives Focus back: Heartwood. The arts come from the weapon's kind and its art rune; this feeds them.</summary>
[Serializable]
public sealed class FocusOnArtKillEffect : RelicEffect
{
    [SerializeField, Min(0f)] private float focus = 20f;

    public override string DisplayName => "Focus From Art Kills";
    public override string Describe() => $"A kill with a weapon art gives back {focus:0} Focus.";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerFocus playerFocus = wearer.Focus;
        if (playerFocus == null)
        {
            return () => { };
        }

        void OnDied(Damageable victim, DamageInfo blow)
        {
            if (blow.Source == wearer.Combatant && victim != wearer.Health && (blow.Technique & HitTechnique.WeaponArt) != 0)
            {
                playerFocus.Gain(focus);
            }
        }

        Damageable.AnyDied += OnDied;
        return () => Damageable.AnyDied -= OnDied;
    }
}

/// <summary>Brought low, the wearer's skin turns to stone for a while, once in a while: Stoneblood.</summary>
[Serializable]
public sealed class StoneSkinWhenLowEffect : RelicEffect
{
    [SerializeField, Range(0.05f, 0.9f)] private float below = 0.3f;
    [SerializeField, Min(0f)] private float armor = 40f;
    [SerializeField, Min(0.5f)] private float seconds = 6f;
    [SerializeField, Min(1f)] private float cooldown = 60f;

    public override string DisplayName => "Stone Skin When Low";
    public override string Describe() => $"Below {Percent(below)} health your skin turns to stone: +{armor:0} armour for {seconds:0} s (once a minute).";

    public override Action Attach(PlayerRelics wearer)
    {
        var source = new object();
        StatSheet stats = wearer.Combatant.Stats;
        float readyAt = 0f;
        float endsAt = -1f;

        void OnHit(DamageInfo hit, DamageResult result)
        {
            Damageable health = wearer.Health;
            if (Time.time < readyAt || !health.IsAlive || health.CurrentHealth > health.MaxHealth * below)
            {
                return;
            }

            readyAt = Time.time + cooldown;
            endsAt = Time.time + seconds;
            stats.AddModifier(new StatModifier(StatType.Armor, StatModifierOp.Flat, armor, source));
            GameMessages.Post("Your skin hardens to stone.", MessageKind.Info);
        }

        Action stop = wearer.Every(0.25f, () =>
        {
            if (endsAt > 0f && Time.time >= endsAt)
            {
                endsAt = -1f;
                stats.RemoveModifiersFrom(source);
            }
        });
        wearer.Health.Damaged += OnHit;
        return () =>
        {
            wearer.Health.Damaged -= OnHit;
            stop();
            stats.RemoveModifiersFrom(source);
        };
    }
}

/// <summary>A parried blow is turned back on the one who struck it: Thornguard.</summary>
[Serializable]
public sealed class ParryThornsEffect : RelicEffect
{
    [Tooltip("Share of the parried blow dealt back.")]
    [SerializeField, Range(0f, 2f)] private float share = 0.5f;

    public override string DisplayName => "Parried Blows Strike Back";
    public override string Describe() => $"A parried blow strikes back: {Percent(share)} of it to the one who struck.";

    public override Action Attach(PlayerRelics wearer)
    {
        void OnGuarded(DamageInfo hit, GuardOutcome outcome)
        {
            if (outcome != GuardOutcome.Parried || hit.Source == null || hit.Amount <= 0f || !hit.Source.TryGetComponent(out Damageable striker) || !striker.IsAlive)
            {
                return;
            }

            Vector3 back = striker.transform.position - wearer.transform.position;
            striker.ApplyDamage(new DamageInfo(wearer.Combatant, null, hit.Amount * share, DamageType.Physical, false,
                striker.transform.position + Vector3.up, back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.forward));
        }

        wearer.Health.Guarded += OnGuarded;
        return () => wearer.Health.Guarded -= OnGuarded;
    }
}

/// <summary>A staff bolt leaps on to a second creature: Stormcall.</summary>
[Serializable]
public sealed class ChainBoltEffect : RelicEffect
{
    [SerializeField, Range(0f, 1f)] private float share = 0.5f;
    [SerializeField, Min(1f)] private float radius = 6f;

    // The leap's own hit doesn't leap again.
    private static bool leaping;

    public override string DisplayName => "Leaping Bolts";
    public override string Describe() => $"A staff bolt leaps to the nearest other creature within {radius:0} m for {Percent(share)} of its damage.";

    public override Action Attach(PlayerRelics wearer)
    {
        void OnDamaged(Damageable victim, DamageInfo hit, DamageResult result)
        {
            if (leaping || hit.Source != wearer.Combatant || (hit.Technique & HitTechnique.Spell) == 0 || result.Damage <= 0f)
            {
                return;
            }

            Damageable next = null;
            float best = float.MaxValue;
            foreach (Damageable near in Near.Enemies(victim.transform.position, radius, wearer.Combatant))
            {
                float distance = (near.transform.position - victim.transform.position).sqrMagnitude;
                if (near != victim && distance < best)
                {
                    best = distance;
                    next = near;
                }
            }

            if (next == null)
            {
                return;
            }

            leaping = true;
            try
            {
                Vector3 way = next.transform.position - victim.transform.position;
                next.ApplyDamage(new DamageInfo(wearer.Combatant, hit.Attack, result.Damage * share, hit.Type, false, next.transform.position + Vector3.up,
                    way.sqrMagnitude > 1e-4f ? way.normalized : Vector3.forward));
            }
            finally
            {
                leaping = false;
            }
        }

        Damageable.AnyDamaged += OnDamaged;
        return () => Damageable.AnyDamaged -= OnDamaged;
    }
}

/// <summary>A heavy roll knocks small creatures off their feet: Avalanche. Only a heavy load rolls heavily, so it is the heavy kit's.</summary>
[Serializable]
public sealed class HeavyRollQuakeEffect : RelicEffect
{
    [SerializeField, Min(0.5f)] private float radius = 3f;
    [SerializeField, Min(0.1f)] private float stagger = 1.2f;

    public override string DisplayName => "Heavy Roll Quake";
    public override string Describe() => $"A heavy roll staggers creatures within {radius:0.#} m (not the great ones). Only at a heavy load.";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerDodge dodge = wearer.Dodge;
        if (dodge == null)
        {
            return () => { };
        }

        void OnDodge(DodgeStyle style, DodgeShape shape)
        {
            if (style != DodgeStyle.HeavyRoll)
            {
                return;
            }

            foreach (Damageable near in Near.Enemies(wearer.transform.position, radius, wearer.Combatant))
            {
                if (near.TryGetComponent(out MonsterAI monster) && near.GetComponent<BossEncounter>() == null)
                {
                    monster.StaggerAtLeast(stagger);
                }
            }
        }

        dodge.DodgeStarted += OnDodge;
        return () => dodge.DodgeStarted -= OnDodge;
    }
}

/// <summary>Focus spent is paid half in health instead: Blood-Price, a Cursed bargain. Arts and skills come twice as often, at a cost in blood.</summary>
[Serializable]
public sealed class BloodPriceEffect : RelicEffect
{
    [Tooltip("Share of each Focus spend given back.")]
    [SerializeField, Range(0f, 1f)] private float refund = 0.5f;
    [Tooltip("Health paid for each point of Focus given back, as a share of max health.")]
    [SerializeField, Range(0f, 0.05f)] private float healthPerFocus = 0.004f;

    public override string DisplayName => "Blood-Price";
    public override string Describe() => $"Half of the Focus you spend comes back, paid in health ({Mathf.Round(healthPerFocus * 1000f) / 10f}% of your health a point).";

    public override Action Attach(PlayerRelics wearer)
    {
        PlayerFocus focus = wearer.Focus;
        if (focus == null)
        {
            return () => { };
        }

        float last = focus.Current;
        bool paying = false;

        void OnChanged(float current, float max)
        {
            float spent = last - current;
            last = current;
            if (paying || spent <= 0.5f || !wearer.Health.IsAlive)
            {
                return;
            }

            float back = spent * refund;
            float price = wearer.Health.MaxHealth * healthPerFocus * back;
            if (wearer.Health.CurrentHealth <= price + 1f)
            {
                return;
            }

            paying = true;
            try
            {
                wearer.Health.ApplyDamage(new DamageInfo(null, null, price, DamageType.Physical, false, wearer.transform.position + Vector3.up, Vector3.down)
                    .Scaled(1f, 0f, HitTechnique.OverTime, false));
                focus.Gain(back);
                last = focus.Current;
            }
            finally
            {
                paying = false;
            }
        }

        focus.FocusChanged += OnChanged;
        return () => focus.FocusChanged -= OnChanged;
    }
}

/// <summary>What is near a point and against the one asking: creatures to burst on, leap to, knock down.</summary>
public static class Near
{
    private static readonly Collider[] Overlaps = new Collider[48];
    private static readonly List<Damageable> Found = new List<Damageable>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Found.Clear();

    /// <summary>Living Damageables within <paramref name="radius"/> of <paramref name="at"/> that aren't on <paramref name="by"/>'s side.</summary>
    public static List<Damageable> Enemies(Vector3 at, float radius, Combatant by)
    {
        Found.Clear();
        int count = Physics.OverlapSphereNonAlloc(at, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Damageable target = Overlaps[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive || Found.Contains(target) || (by != null && target.gameObject == by.gameObject))
            {
                continue;
            }

            if (target.TryGetComponent(out Combatant side) && by != null && side.Faction.IsPlayerSide() == by.Faction.IsPlayerSide())
            {
                continue;
            }

            Found.Add(target);
        }

        return new List<Damageable>(Found);
    }
}
