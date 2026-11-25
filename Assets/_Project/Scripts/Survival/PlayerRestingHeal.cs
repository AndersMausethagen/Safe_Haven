using UnityEngine;

/// <summary>
/// Health regained out of combat (StatType.HealthRegen): every <see cref="IntervalSeconds"/> the player is out of a fight
/// (GameStateManager.InCombat) and hurt, that share of their most health comes back. Stewed Berries gives 1% (the user, 2026-11-25:
/// "Restores 1% of max hp every 5 seconds while out of combat"). A fight starting puts the count back to the start.
/// </summary>
[RequireComponent(typeof(Combatant), typeof(Damageable))]
public sealed class PlayerRestingHeal : MonoBehaviour
{
    /// <summary>Seconds between one return of health and the next (StatType.HealthRegen is per this many seconds).</summary>
    public const float IntervalSeconds = 5f;

    private Combatant combatant;
    private Damageable health;
    private float next;

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
    }

    private void Update()
    {
        float share = combatant.Stats != null ? combatant.Stats.Get(StatType.HealthRegen) : 0f;
        if (share <= 0f || !health.IsAlive || GameStateManager.InCombat || health.HealthFraction >= 1f)
        {
            next = Time.time + IntervalSeconds;
            return;
        }

        if (Time.time < next)
        {
            return;
        }

        next = Time.time + IntervalSeconds;
        health.Heal(health.MaxHealth * share);
    }
}
