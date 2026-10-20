using System;
using UnityEngine;

/// <summary>
/// Gives a GameObject health and lets it be hit: an enemy, a building, a destructible prop. Max health, armor and
/// resistances come from its Combatant's stats, so they change with buffs. The runtime health lives here; the
/// authored definition is never touched.
/// Other systems react through events (damage numbers, health bars, audio, loot) without this component knowing them.
/// </summary>
[RequireComponent(typeof(Combatant))]
public sealed class Damageable : MonoBehaviour, IDamageable, ISaveable
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

    public DamageResult ApplyDamage(in DamageInfo damage)
    {
        if (!IsAlive)
        {
            return DamageResult.None;
        }

        if (IsInvulnerable)
        {
            Evaded?.Invoke(damage);
            return DamageResult.None;
        }

        float final = DamageCalculator.Mitigate(damage.Amount, damage.Type, combatant.Stats, combatant.Rules);
        float healthLost = Mathf.Min(final, currentHealth);
        currentHealth -= healthLost;

        bool killed = currentHealth <= 0.0001f;
        if (killed)
        {
            currentHealth = 0f;
            IsAlive = false;
        }

        var result = new DamageResult(damage.Amount, final, healthLost, killed, damage.IsCritical, damage.Type);

        if (logHits)
        {
            string attacker = damage.Source != null ? damage.Source.name : "environment";
            Debug.Log($"{attacker} hit {name} for {final:F1} {damage.Type} ({damage.Amount:F1} before defenses){(damage.IsCritical ? " CRIT" : "")}. Health {currentHealth:F0}/{MaxHealth:F0}{(killed ? " - killed" : "")}.", this);
        }

        HealthChanged?.Invoke(currentHealth, MaxHealth);
        Damaged?.Invoke(damage, result);
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
            Died?.Invoke(this, damage);
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

    /// <summary>
    /// Brings a dead object back with a share of its health, such as the player waking at the Heart Tree. Only for objects
    /// that aren't destroyed on death.
    /// </summary>
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
