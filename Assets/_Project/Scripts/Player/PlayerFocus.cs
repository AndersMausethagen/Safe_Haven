using System;
using UnityEngine;

/// <summary>
/// Focus: what fighting earns and skills spend. Landing hits builds it (finishers and heavy swings more, counters
/// more still), a parry or a perfect dodge builds a lot, blocking a little, and blows taken build it by the share of health they
/// take; every gain is worth more the more creatures are after the player (the user, 2026-11-25: on the defensive and
/// surrounded, Focus had all but stopped coming). It is kept in circles (the user, 2026-11-24): when one
/// fills it lights above the skill bar and stays lit, never draining, until an ability, a spell or a weapon art spends it; up to
/// three are kept. Only the circle still filling holds for a while after the last gain and then drains away. The player is made
/// anew with each scene, so the circles start empty on entering or leaving a raid. Rules are in
/// <see cref="CombatTechniqueSettings"/> (Focus).
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public sealed class PlayerFocus : MonoBehaviour
{
    private PlayerCombat combat;
    private PlayerGuard guard;
    private Combatant combatant;
    private Damageable health;
    private float lastGain = float.NegativeInfinity;

    /// <summary>All the Focus held: the full circles' and the one filling.</summary>
    public float Current { get; private set; }
    public float Max => Rules.FocusMax;
    public float PerCircle => Rules.FocusPerCircle;
    public int MaxCircles => Rules.FocusCircles;

    /// <summary>Full circles, ready to spend.</summary>
    public int Circles => CirclesIn(Current, PerCircle, MaxCircles);

    /// <summary>How full the next circle is, 0-1: 1 once every circle is full.</summary>
    public float Filling => FillingOf(Current, PerCircle, MaxCircles);

    public bool HasCircle => Circles > 0;

    /// <summary>Raised when Focus changes. Arguments: current, max.</summary>
    public event Action<float, float> FocusChanged;

    /// <summary>Raised when something asks for more Focus than there is.</summary>
    public event Action SpendRefused;

    /// <summary>Raised with the Focus fighting earned (hits, parries, dodges, blocks, blows taken; after the crowd's share): not
    /// refunds or gifts. The playtest log counts it.</summary>
    public event Action<float> Earned;

    private CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        guard = GetComponent<PlayerGuard>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
    }

    // What fighting pays, blow by blow as each lands on a foe (FightBlows): a swing per foe it struck, up to Focus Targets Counted, and
    // an arrow as a swing does (a perfect release as a finisher: an archer has skills to pay for too). Counted from the targets a swing
    // found, a crate, a fence or a creature's parry paid as a creature did (review 2026-11-27).
    private float countedSwing = float.NaN;
    private int countedBlows;

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        if (!FightBlows.Counts(combatant, target, damage, result))
        {
            return;
        }

        WeaponDefinition weapon = combat.Weapon;
        if (weapon != null && weapon.IsBow && damage.Attack == weapon.ShotAttack)
        {
            Earn(Rules.FocusPerHit * (result.IsCritical ? 2f : 1f));
            return;
        }

        float worth = ForBlow(damage.Technique, Rules);
        if (worth <= 0f)
        {
            return;
        }

        // A swing's blows land over its sweep: counted against the swing they belong to.
        if (combat.SwingStartTime != countedSwing)
        {
            countedSwing = combat.SwingStartTime;
            countedBlows = 0;
        }

        if (countedBlows < Rules.FocusTargetsCounted)
        {
            countedBlows++;
            Earn(worth);
        }
    }

    // A blow that got through earns by the share of health it took. Not one the guard took, not burning or poison ticking, not the
    // player's own doing (a fall, their own fire): only being struck by something.
    private void OnHurt(DamageInfo damage, DamageResult result)
    {
        if (result.Damage <= 0f || result.Blocked || result.Parried || damage.IsOverTime || damage.Source == null || damage.Source == combatant)
        {
            return;
        }

        // Health lost, not the blow's size: one a floor held off (downed in co-op, a relic's last stand, sparring) cost nothing.
        // A skill's buff can make blows taken pay more (Bear-Hide).
        float scale = TryGetComponent(out PlayerSkills skills) ? skills.FocusFromBlowsScale : 1f;
        Earn(ForHurt(result.HealthLost, health.MaxHealth, Rules) * scale);
    }

    private void OnEnable()
    {
        combat.PerfectDodge += OnPerfectDodge;
        if (combatant != null)
        {
            combatant.DamageDealt += OnDamageDealt;
        }

        if (guard != null)
        {
            guard.Parried += OnParried;
            guard.Blocked += OnBlocked;
        }

        if (health != null)
        {
            health.Damaged += OnHurt;
        }
    }

    private void OnDisable()
    {
        combat.PerfectDodge -= OnPerfectDodge;
        if (combatant != null)
        {
            combatant.DamageDealt -= OnDamageDealt;
        }

        if (guard != null)
        {
            guard.Parried -= OnParried;
            guard.Blocked -= OnBlocked;
        }

        if (health != null)
        {
            health.Damaged -= OnHurt;
        }
    }

    // Only the circle still filling drains; full circles are kept.
    private void Update()
    {
        float kept = Circles * PerCircle;
        if (Current > kept && Time.time - lastGain > Rules.FocusHoldSeconds)
        {
            Set(Mathf.Max(kept, Current - Rules.FocusDrainPerSecond * Time.deltaTime));
        }
    }

    /// <summary>Full circles in that much Focus.</summary>
    public static int CirclesIn(float focus, float perCircle, int maxCircles) =>
        perCircle > 0f ? Mathf.Clamp(Mathf.FloorToInt((focus + 0.001f) / perCircle), 0, maxCircles) : 0;

    /// <summary>How full the circle after the full ones is, 0-1; 1 when all are full.</summary>
    public static float FillingOf(float focus, float perCircle, int maxCircles)
    {
        int full = CirclesIn(focus, perCircle, maxCircles);
        return full >= maxCircles ? 1f : perCircle > 0f ? Mathf.Clamp01((focus - full * perCircle) / perCircle) : 0f;
    }

    /// <summary>Focus a swing earns: per target hit (up to Focus Targets Counted), more for finishers, heavy swings and counters.</summary>
    public static float ForSwing(SwingInfo swing, int hits, CombatTechniqueSettings rules)
    {
        if (hits <= 0 || swing.Kind == SwingKind.ShieldBash || swing.Kind == SwingKind.Art)
        {
            return 0f;
        }

        bool heavy = swing.Kind == SwingKind.Heavy || swing.Kind == SwingKind.GuardCounter;
        return Worth(swing.IsFinisher, heavy, swing.IsCounter, rules) * Mathf.Min(hits, rules.FocusTargetsCounted);
    }

    /// <summary>Focus one landed blow of a swing earns, by its technique: none for a shield bash, an art, an ability or a tick; else as
    /// <see cref="ForSwing"/> counts one target.</summary>
    public static float ForBlow(HitTechnique technique, CombatTechniqueSettings rules)
    {
        const HitTechnique earnsNothing = HitTechnique.ShieldBash | HitTechnique.WeaponArt | HitTechnique.Ability | HitTechnique.OverTime;
        if ((technique & HitTechnique.Melee) == 0 || (technique & earnsNothing) != 0)
        {
            return 0f;
        }

        bool heavy = (technique & (HitTechnique.Heavy | HitTechnique.GuardCounter)) != 0;
        return Worth((technique & HitTechnique.Finisher) != 0, heavy, (technique & HitTechnique.Counter) != 0, rules);
    }

    private static float Worth(bool finisher, bool heavy, bool counter, CombatTechniqueSettings rules)
    {
        float gain = rules.FocusPerHit * (finisher ? 2f : heavy ? 1.5f : 1f);
        return counter ? gain * 1.5f : gain;
    }

    /// <summary>Focus a blow that took <paramref name="damage"/> of <paramref name="maxHealth"/> earns: Focus When Hurt for a whole bar.</summary>
    public static float ForHurt(float damage, float maxHealth, CombatTechniqueSettings rules) =>
        maxHealth > 0f && damage > 0f ? rules.FocusWhenHurt * Mathf.Clamp01(damage / maxHealth) : 0f;

    /// <summary>What a crowd of <paramref name="crowd"/> creatures after the player makes each Focus gain worth: 1 for one or none,
    /// Focus Per Extra Foe more for each beyond, at most Focus Crowd Most.</summary>
    public static float CrowdScale(int crowd, CombatTechniqueSettings rules) =>
        Mathf.Clamp(1f + rules.FocusPerExtraFoe * (crowd - 1), 1f, Mathf.Max(1f, rules.FocusCrowdMost));

    /// <summary>The creatures after this player within Focus Crowd Radius now (as the Hurt rows' crowd counts them).</summary>
    public int Crowd => health != null ? MonsterAI.CountAt(health, transform.position, Rules.FocusCrowdRadius) : 0;

    /// <summary>Focus fighting earned (a blow, a parry, a staff's cast): worth more in a crowd, and counted by the log. Refunds and
    /// gifts go through <see cref="Gain"/> as they are.</summary>
    public void Earn(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float earned = amount * CrowdScale(Crowd, Rules);
        Gain(earned);
        Earned?.Invoke(earned);
    }

    private void OnParried(DamageInfo damage) => Earn(Rules.FocusOnParry);

    private void OnPerfectDodge(DamageInfo damage) => Earn(Rules.FocusOnPerfectDodge);

    private void OnBlocked(DamageInfo damage) => Earn(Rules.FocusOnBlock);

    public void Gain(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        lastGain = Time.time;
        Set(Current + amount);
    }

    /// <summary>
    /// Spends one full circle, keeping what the next had filled; with none full, spends nothing and raises <see cref="SpendRefused"/>.
    /// Returns the Focus spent (0 when refused).
    /// </summary>
    public float TrySpendCircle() => TrySpendCircles(1);

    /// <summary>Spends that many full circles at once, or none (raising <see cref="SpendRefused"/>). Returns the Focus spent.</summary>
    public float TrySpendCircles(int count)
    {
        if (count <= 0)
        {
            return 0f;
        }

        if (Circles < count)
        {
            SpendRefused?.Invoke();
            return 0f;
        }

        float spent = PerCircle * count;
        Set(Current - spent);
        return spent;
    }

    private void Set(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, Max);
        if (Mathf.Approximately(clamped, Current))
        {
            return;
        }

        Current = clamped;
        FocusChanged?.Invoke(Current, Max);
    }
}
