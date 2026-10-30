using System;
using UnityEngine;

/// <summary>
/// The player's stamina. Attacks, skills and dodges pay their full cost through <see cref="TrySpend"/>; insufficient
/// stamina refuses the action. Stamina regenerates after a short pause since the last spend, and not while dead.
/// <para>
/// Where it belongs: on the PlayerArmature prefab, reached through Player.Current.Stamina. Tuning lives in
/// PlayerStaminaSettings; the live value is runtime-only and is not saved.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class PlayerStamina : MonoBehaviour
{
    [SerializeField] private PlayerStaminaSettings settings;
    private Combatant combatant;
    private float maxEfficiency = 1f;

    private Damageable health;
    private StaminaPool pool;
    private float regenAt;
    private float regenMultiplier = 1f;

    public float Current => pool.Current;
    public float Max => pool.Max;
    public float Fraction => pool.Fraction;
    public bool IsEmpty => pool.IsEmpty;

    public float AttackCost => settings != null ? settings.AttackCost : 0f;
    public float ShieldBashCost => settings != null ? settings.ShieldBashCost : 0f;
    public float DodgeCost => settings != null ? settings.DodgeCost : 0f;
    public float SwimCostPerSecond => settings != null ? settings.SwimCostPerSecond : 0f;

    /// <summary>Raised whenever stamina changes. Arguments: current stamina, max stamina.</summary>
    public event Action<float, float> StaminaChanged;

    /// <summary>Raised when an action was refused because stamina is insufficient.</summary>
    public event Action SpendRefused;

    private void Awake()
    {
        health = GetComponent<Damageable>();
        if (settings == null)
        {
            Debug.LogWarning("PlayerStamina has no settings; actions cost nothing.", this);
        }

        pool = new StaminaPool(settings != null ? settings.MaxStamina : 100f);
        if (TryGetComponent(out combatant))
        {
            combatant.Stats.Changed += OnStatChanged;
        }
    }

    private void OnDestroy()
    {
        if (combatant != null)
        {
            combatant.Stats.Changed -= OnStatChanged;
        }
    }

    // Talents adding stamina points resize the pool at once.
    private void OnStatChanged(StatType stat)
    {
        if (stat == StatType.MaxStamina)
        {
            SetEfficiency(maxEfficiency, regenMultiplier);
        }
    }

    /// <summary>Pays the full action cost. Returns false when stamina is insufficient, and the action must not happen.</summary>
    public bool TrySpend(float cost)
    {
        if (!pool.TrySpend(cost))
        {
            SpendRefused?.Invoke();
            return false;
        }

        if (cost > 0f)
        {
            regenAt = Time.time + (settings != null ? settings.RegenDelay : 0f);
            StaminaChanged?.Invoke(pool.Current, pool.Max);
        }

        return true;
    }

    /// <summary>Spends stamina over time (swimming): takes what there is, never refuses, and holds off regeneration
    /// like any spend.</summary>
    public void Drain(float amount)
    {
        if (pool.Drain(amount))
        {
            regenAt = Time.time + (settings != null ? settings.RegenDelay : 0f);
            StaminaChanged?.Invoke(pool.Current, pool.Max);
        }
    }

    /// <summary>
    /// Scales the stamina pool and its regeneration, for conditions such as hunger (see PlayerNeeds). 1 and 1 is normal.
    /// </summary>
    public void SetEfficiency(float maxMultiplier, float regenerationMultiplier)
    {
        maxEfficiency = maxMultiplier;
        float baseMax = (settings != null ? settings.MaxStamina : 100f) + (combatant != null ? combatant.Stats.Get(StatType.MaxStamina) : 0f);
        pool.SetMax(baseMax * Mathf.Max(0.1f, maxMultiplier));
        regenMultiplier = Mathf.Max(0f, regenerationMultiplier);
        StaminaChanged?.Invoke(pool.Current, pool.Max);
    }

    private void Update()
    {
        if (settings == null || !health.IsAlive || Time.time < regenAt)
        {
            return;
        }

        if (pool.Regenerate(settings.RegenPerSecond * regenMultiplier * Time.deltaTime))
        {
            StaminaChanged?.Invoke(pool.Current, pool.Max);
        }
    }
}
