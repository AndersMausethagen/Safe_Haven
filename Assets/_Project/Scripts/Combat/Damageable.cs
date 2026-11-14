using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a GameObject health and lets it be hit: an enemy, a building, a destructible prop. Max health, armor and
/// resistances come from its Combatant's stats, so they change with buffs. The runtime health lives here; the
/// authored definition is never touched.
/// Other systems react through events (damage numbers, health bars, audio, loot) without this component knowing them.
/// </summary>
[RequireComponent(typeof(Combatant))]
public sealed class Damageable : MonoBehaviour, IDamageable, ISaveable, ISharedState
{
    [Serializable]
    private struct SavedHealth
    {
        public float health;
    }

    [SerializeField] private TargetCategory category = TargetCategory.Creature;
    [SerializeField] private bool destroyOnDeath = true;
    [Tooltip("Seconds between dying and being destroyed, to leave time for death effects.")]
    [SerializeField, Min(0f)] private float destroyDelay = 1.5f;
    [Tooltip("Writes every hit to the console, for balancing.")]
    [SerializeField] private bool logHits;

    private Combatant combatant;
    private float currentHealth;
    private float knownMaxHealth;
    private readonly System.Collections.Generic.HashSet<object> invulnerableSources = new System.Collections.Generic.HashSet<object>();

    public bool IsAlive { get; private set; } = true;
    /// <summary>True while anything (a dodge, a cutscene) makes this object ignore hits. See AddInvulnerability.</summary>
    public bool IsInvulnerable => invulnerableSources.Count > 0;
    public Faction Faction => Owner.Faction;
    public TargetCategory Category => category;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => Mathf.Max(1f, Owner.Stats.Get(StatType.MaxHealth));

    // Looked up on demand too, so inactive objects that haven't run Awake (raid strengthening finds those) can be read.
    private Combatant Owner => combatant != null ? combatant : combatant = GetComponent<Combatant>();
    public float HealthFraction => Mathf.Clamp01(currentHealth / MaxHealth);
    /// <summary>True when dying removes the object (enemies, buildings), so a save should remember it as gone.</summary>
    public bool DestroysOnDeath => destroyOnDeath;
    public string SaveKey => "health";

    bool ISharedState.IsShared => true;

    int ISharedState.SharedMark => Mathf.CeilToInt(currentHealth);

    /// <summary>Raised after every hit that lands, with the hit and what it did.</summary>
    public event Action<DamageInfo, DamageResult> Damaged;

    /// <summary>Raised whenever health changes. Arguments: current health, max health.</summary>
    public event Action<float, float> HealthChanged;

    /// <summary>Raised once, when health reaches zero. The hit's source is the killer.</summary>
    public event Action<Damageable, DamageInfo> Died;

    /// <summary>Raised when anything dies, after its own Died event. For game-wide reactions such as death sounds.</summary>
    public static event Action<Damageable, DamageInfo> AnyDied;

    /// <summary>Raised after any hit lands on anything, after its own Damaged event. For game-wide feedback (CombatFeedback).</summary>
    public static event Action<Damageable, DamageInfo, DamageResult> AnyDamaged;

    /// <summary>Raised when a hit is ignored because this object is invulnerable, such as during a dodge.</summary>
    public event Action<DamageInfo> Evaded;

    /// <summary>Raised when a guard (<see cref="IHitGuard"/>) takes a hit: blocked, parried or broken. Blocked and broken
    /// hits are then applied as well (Damaged follows); parried ones are not.</summary>
    public event Action<DamageInfo, GuardOutcome> Guarded;

    /// <summary>Raised when any object's guard takes a hit, after its own Guarded event (CombatFeedback, the attacker's stagger).</summary>
    public static event Action<Damageable, DamageInfo, GuardOutcome> AnyGuarded;

    /// <summary>Multiplies the damage this object takes, after its defenses (a staggered monster takes more). 1 = normal.</summary>
    public float DamageTakenMultiplier { get; set; } = 1f;

    /// <summary>A hit can't take its health below this, so it can't die: a building breaks and waits for repairs, but is never lost.</summary>
    public float HealthFloor
    {
        get
        {
            float floor = baseFloor;
            foreach (float held in heldFloors.Values)
            {
                floor = Mathf.Max(floor, held);
            }

            return floor;
        }
        set => baseFloor = value;
    }

    private float baseFloor;
    private readonly Dictionary<object, float> heldFloors = new Dictionary<object, float>();

    /// <summary>
    /// Holds health at or above <paramref name="floor"/> for <paramref name="source"/> until released, beside
    /// <see cref="HealthFloor"/>'s own value: the highest floor counts, so one holder (co-op's downed rule) never undoes
    /// another's (a Deathless relic).
    /// </summary>
    public void HoldFloor(object source, float floor) => heldFloors[source] = floor;

    public void ReleaseFloor(object source) => heldFloors.Remove(source);

    /// <summary>True when it has been worn down to its floor.</summary>
    public bool IsBroken => HealthFloor > 0f && currentHealth <= HealthFloor;

    private IHitGuard[] guards;

    /// <summary>
    /// Makes the object ignore hits until the same source removes it. Several sources can overlap (a dodge during a
    /// cutscene); the object is vulnerable again only when all of them are gone.
    /// </summary>
    public void AddInvulnerability(object source)
    {
        if (source != null)
        {
            invulnerableSources.Add(source);
        }
    }

    public void RemoveInvulnerability(object source)
    {
        if (source != null)
        {
            invulnerableSources.Remove(source);
        }
    }

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        guards = GetComponents<IHitGuard>();
        currentHealth = MaxHealth;
        knownMaxHealth = currentHealth;
    }

    private void OnEnable()
    {
        combatant.Stats.Changed += OnStatChanged;
        OnStatChanged(StatType.MaxHealth);
    }

    private void OnDisable()
    {
        combatant.Stats.Changed -= OnStatChanged;
    }

    public DamageResult ApplyDamage(in DamageInfo hit) => Apply(hit, true, false);

    /// <summary>
    /// Co-op, in the host's game: a friend's blow, which their game has already put to this creature's guard as they saw it
    /// (its shield, where it faced there). Asking the guard again here would take its share off twice.
    /// </summary>
    public DamageResult ApplyJudged(in DamageInfo hit) => Apply(hit, false, false);

    // askGuards: off for a blow another game has judged. exact: the amount is taken from health as it stands, past
    // invulnerability and defenses (Finish), so a death decided elsewhere shows what was left and not a number made up to be sure.
    private DamageResult Apply(in DamageInfo hit, bool askGuards, bool exact)
    {
        if (!IsAlive)
        {
            return DamageResult.None;
        }

        if (IsInvulnerable && !exact)
        {
            // A poison tick during a dodge is no perfect dodge.
            if (!hit.IsOverTime)
            {
                Evaded?.Invoke(hit);
            }

            return DamageResult.None;
        }

        DamageInfo damage = hit;
        GuardOutcome guard = GuardOutcome.None;
        if (guards != null && askGuards && !exact)
        {
            foreach (IHitGuard candidate in guards)
            {
                guard = candidate.Guard(damage, out float letThrough);
                if (guard == GuardOutcome.None)
                {
                    continue;
                }

                RaiseGuarded(damage, guard);
                if (guard == GuardOutcome.Parried)
                {
                    return new DamageResult(damage.Amount, 0f, 0f, false, false, damage.Type, guard);
                }

                damage = damage.WithAmount(damage.Amount * letThrough);
                break;
            }
        }

        float final = exact ? damage.Amount : DamageCalculator.Mitigate(damage.Amount, damage.Type, damage.Strike, Owner.Stats, Owner.Rules) * DamageTakenMultiplier;
        float healthLost = Mathf.Min(final, Mathf.Max(0f, currentHealth - HealthFloor));
        currentHealth -= healthLost;

        bool killed = currentHealth <= 0.0001f;
        if (killed)
        {
            currentHealth = 0f;
            IsAlive = false;
        }

        var result = new DamageResult(damage.Amount, final, healthLost, killed, damage.IsCritical, damage.Type, guard);

        if (logHits)
        {
            string attacker = damage.Source != null ? damage.Source.name : "environment";
            Debug.Log($"{attacker} hit {name} for {final:F1} {damage.Type} ({damage.Amount:F1} before defenses){(damage.IsCritical ? " CRIT" : "")}. Health {currentHealth:F0}/{MaxHealth:F0}{(killed ? " - killed" : "")}.", this);
        }

        // A faulty listener must not cost the death below: the end of a raid and the loot hang on Died.
        try
        {
            HealthChanged?.Invoke(currentHealth, MaxHealth);
            Damaged?.Invoke(damage, result);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        try
        {
            AnyDamaged?.Invoke(this, damage, result);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (killed)
        {
            RaiseDied(damage);
            try
            {
                AnyDied?.Invoke(this, damage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelay);
            }
        }

        return result;
    }

    // Each listener runs in isolation, so one failing can't stop the others hearing of the death.
    private void RaiseDied(in DamageInfo damage)
    {
        Action<Damageable, DamageInfo> handlers = Died;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<Damageable, DamageInfo>)handler)(this, damage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void RaiseGuarded(in DamageInfo damage, GuardOutcome outcome)
    {
        Guarded?.Invoke(damage, outcome);
        try
        {
            AnyGuarded?.Invoke(this, damage, outcome);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    /// <summary>Restores health up to the maximum. Returns how much was actually restored.</summary>
    public float Heal(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return 0f;
        }

        float healed = Mathf.Min(amount, MaxHealth - currentHealth);
        if (healed > 0f)
        {
            currentHealth += healed;
            HealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        return healed;
    }

    /// <summary>Co-op: shows the health a friend's game reports, as a share of the maximum (never below <see cref="HealthFloor"/>).</summary>
    public void ShowShare(float share)
    {
        if (!IsAlive)
        {
            return;
        }

        float shown = Mathf.Max(Mathf.Max(HealthFloor, 1f), MaxHealth * Mathf.Clamp01(share));
        if (Mathf.Abs(shown - currentHealth) < 0.01f)
        {
            return;
        }

        currentHealth = shown;
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    /// <summary>Co-op: dies now, as the host's game says this creature did, by <paramref name="killer"/>'s blow (or none).</summary>
    public void Finish(Combatant killer)
    {
        if (!IsAlive)
        {
            return;
        }

        HealthFloor = 0f;
        Apply(new DamageInfo(killer, null, currentHealth, DamageType.Physical, false, transform.position, transform.forward, 0f, HitTechnique.None), false, true);
    }

    /// <summary>
    /// Brings a dead object back with a share of its health, such as the player waking at the portal. Only for objects
    /// that aren't destroyed on death.
    /// </summary>
    /// <summary>Sets a living thing's health to a share of its maximum, without a blow (a tamed horse keeps the wild one's hurts).</summary>
    public void SetHealthFraction(float healthFraction)
    {
        if (!IsAlive)
        {
            return;
        }

        currentHealth = Mathf.Max(1f, MaxHealth * Mathf.Clamp01(healthFraction));
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void Revive(float healthFraction = 1f)
    {
        if (IsAlive)
        {
            return;
        }

        IsAlive = true;
        currentHealth = Mathf.Max(1f, MaxHealth * Mathf.Clamp01(healthFraction));
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SavedHealth { health = currentHealth });
    }

    // Only damage is restored; an object that was dead and removed is handled by its SaveableEntity.
    public void RestoreState(string state)
    {
        if (!IsAlive)
        {
            return;
        }

        float saved = JsonUtility.FromJson<SavedHealth>(state).health;
        currentHealth = Mathf.Clamp(saved, 1f, MaxHealth);
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    // Buffs and gear change max health while playing. Health keeps its share of the maximum, so putting on armor adds
    // health and taking it off removes the same share, and swapping gear back and forth can't heal.
    private void OnStatChanged(StatType stat)
    {
        if (stat != StatType.MaxHealth)
        {
            return;
        }

        float max = MaxHealth;
        if (IsAlive && knownMaxHealth > 0f && !Mathf.Approximately(max, knownMaxHealth))
        {
            currentHealth = Mathf.Clamp(currentHealth / knownMaxHealth * max, 1f, max);
            HealthChanged?.Invoke(currentHealth, max);
        }

        knownMaxHealth = max;
    }
}
