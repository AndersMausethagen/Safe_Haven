using System;
using UnityEngine;

/// <summary>
/// The player's hunger and thirst (GDD §11). Both drain all the time, faster during raids, and are restored by eating and
/// drinking (consumable items, edible resources, a water source). Running out never kills: hunger at zero lowers maximum
/// stamina and slows regeneration, thirst at zero slows regeneration in combat (SurvivalSettings has the numbers).
/// <para>
/// Where it belongs: on the PlayerArmature prefab, reached through Player.Current.Needs. The values travel between scenes
/// and into saves through PlayerSnapshot; the HUD (PlayerVitalsView) listens to Changed.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerNeeds : MonoBehaviour
{
    private SurvivalSettings settings;
    private PlayerStamina stamina;
    private Damageable health;
    private NeedMeter hunger;
    private NeedMeter thirst;
    private bool warnedHungry;
    private bool warnedThirsty;
    private float appliedMax = 1f;
    private float appliedRegen = 1f;

    // Created on first use: PlayerPersistence restores saved values before this component's Awake runs.
    public NeedMeter Hunger
    {
        get
        {
            EnsureMeters();
            return hunger;
        }
    }

    public NeedMeter Thirst
    {
        get
        {
            EnsureMeters();
            return thirst;
        }
    }

    /// <summary>Raised whenever a meter changes: every frame while draining, and on eating or drinking.</summary>
    public event Action Changed;

    private void Awake()
    {
        EnsureMeters();
        stamina = GetComponent<PlayerStamina>();
        health = GetComponent<Damageable>();
    }

    private void EnsureMeters()
    {
        if (hunger != null)
        {
            return;
        }

        settings = SurvivalSettings.Current;
        hunger = new NeedMeter(settings.MaxHunger, settings.StartingHunger);
        thirst = new NeedMeter(settings.MaxThirst, settings.StartingThirst);
    }

    /// <summary>Restores both meters to saved or carried values. The stamina effects follow on the next frame.</summary>
    public void Restore(float hungerValue, float thirstValue)
    {
        Hunger.Set(hungerValue);
        Thirst.Set(thirstValue);
        warnedHungry = Hunger.Fraction <= settings.LowWarning;
        warnedThirsty = Thirst.Fraction <= settings.LowWarning;
        Changed?.Invoke();
    }

    private void Update()
    {
        if (health != null && !health.IsAlive)
        {
            return;
        }

        float multiplier = RaidDirector.Current != null ? settings.RaidMultiplier : 1f;
        float minutes = Time.deltaTime / 60f;
        bool changed = Hunger.Drain(settings.HungerPerMinute * multiplier * minutes);
        changed |= Thirst.Drain(settings.ThirstPerMinute * multiplier * minutes);
        if (changed)
        {
            Warn();
            Changed?.Invoke();
        }

        ApplyEffects();
    }

    /// <summary>Whether eating or drinking this much would do anything: it feeds a hunger or quenches a thirst that isn't full.</summary>
    public bool WouldHelp(float hungerAmount, float thirstAmount) => (hungerAmount > 0f && !Hunger.IsFull) || (thirstAmount > 0f && !Thirst.IsFull);

    /// <summary>
    /// Eats or drinks something that restores the given amounts. Refuses (returns false, with a message) when neither meter
    /// would rise, so food isn't wasted.
    /// </summary>
    public bool TryConsume(string name, float hungerAmount, float thirstAmount)
    {
        if (!WouldHelp(hungerAmount, thirstAmount))
        {
            GameMessages.Post(hungerAmount > 0f && thirstAmount > 0f ? "You're not hungry or thirsty." : hungerAmount > 0f ? "You're not hungry." : "You're not thirsty.",
                MessageKind.Warning);
            return false;
        }

        float ate = Hunger.Restore(hungerAmount);
        float drank = Thirst.Restore(thirstAmount);
        warnedHungry &= Hunger.Fraction <= settings.LowWarning;
        warnedThirsty &= Thirst.Fraction <= settings.LowWarning;
        GameMessages.Post($"{name}: " + ItemTooltipView.DescribeRestores(0f, ate, drank).Replace("Restores ", "+").TrimEnd('.'), MessageKind.Gain);
        ApplyEffects();
        Changed?.Invoke();
        return true;
    }

    private void Warn()
    {
        if (!warnedHungry && Hunger.Fraction <= settings.LowWarning)
        {
            warnedHungry = true;
            GameMessages.Post("You're getting hungry. Eat something soon.", MessageKind.Warning);
        }

        if (!warnedThirsty && Thirst.Fraction <= settings.LowWarning)
        {
            warnedThirsty = true;
            GameMessages.Post("You're getting thirsty. Drink something soon.", MessageKind.Warning);
        }
    }

    // Recomputed every frame because the thirst penalty depends on being in combat; the stamina is only told on change.
    private void ApplyEffects()
    {
        if (stamina == null)
        {
            return;
        }

        float max = Hunger.IsEmpty ? settings.StarvingMaxStamina : 1f;
        float regen = Hunger.IsEmpty ? settings.StarvingRegeneration : 1f;
        if (Thirst.IsEmpty)
        {
            regen *= settings.ParchedRegeneration;
            if (GameStateManager.InCombat)
            {
                regen *= settings.ParchedCombatRegeneration;
            }
        }

        if (!Mathf.Approximately(max, appliedMax) || !Mathf.Approximately(regen, appliedRegen))
        {
            appliedMax = max;
            appliedRegen = regen;
            stamina.SetEfficiency(max, regen);
        }
    }
}
