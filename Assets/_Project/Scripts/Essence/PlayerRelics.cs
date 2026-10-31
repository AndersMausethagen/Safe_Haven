using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carries out the relics the player is wearing (RelicEffect): starts each one's effect when the gear goes on or mends, and
/// ends it when the gear comes off or breaks. Added by PlayerEquipment, so there is nothing to set up. What a relic has
/// spent lasts as long as this player object, so for one outing: a raid, or a stay at home.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRelics : MonoBehaviour
{
    private sealed class Timer
    {
        public float Interval;
        public float NextAt;
        public Action Tick;
    }

    private readonly List<Action> worn = new List<Action>();
    private readonly List<Timer> timers = new List<Timer>();
    private readonly HashSet<RelicEffect> spent = new HashSet<RelicEffect>();
    private PlayerEquipment equipment;
    private bool stale;

    public Damageable Health { get; private set; }
    public Combatant Combatant { get; private set; }
    public PlayerStamina Stamina { get; private set; }
    public PlayerNoise Noise { get; private set; }
    public PlayerDodge Dodge { get; private set; }

    /// <summary>How many relic effects are running, for tests.</summary>
    public int ActiveCount => worn.Count;

    public bool IsSpent(RelicEffect effect) => spent.Contains(effect);

    public void Spend(RelicEffect effect) => spent.Add(effect);

    /// <summary>Runs something every so many seconds for as long as it isn't stopped. Returns what stops it.</summary>
    public Action Every(float seconds, Action tick)
    {
        var timer = new Timer { Interval = seconds, NextAt = Time.time, Tick = tick };
        timers.Add(timer);
        return () => timers.Remove(timer);
    }

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        Health = GetComponent<Damageable>();
        Combatant = GetComponent<Combatant>();
        Stamina = GetComponent<PlayerStamina>();
        Noise = GetComponent<PlayerNoise>();
        Dodge = GetComponent<PlayerDodge>();
    }

    private void OnEnable()
    {
        equipment.Changed += OnGearChanged;
        stale = true;
    }

    private void OnDisable()
    {
        equipment.Changed -= OnGearChanged;
        TakeOff();
    }

    // Gear changes in bursts (loading a whole set), so effects start once, when the frame's changes are over.
    private void OnGearChanged() => stale = true;

    private void Update()
    {
        if (stale)
        {
            stale = false;
            TakeOff();
            PutOn();
        }

        // From the end, as a tick may stop itself.
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            Timer timer = timers[i];
            if (Time.time >= timer.NextAt)
            {
                timer.NextAt = Time.time + timer.Interval;
                timer.Tick();
            }
        }
    }

    private void PutOn()
    {
        for (int i = 0; i < GearSlots.Count; i++)
        {
            ItemStack stack = equipment.Gear.Get((GearSlot)i);
            if (stack.IsEmpty || stack.IsBroken || stack.Enchantment == null)
            {
                continue;
            }

            foreach (RelicEffect effect in stack.Enchantment.Effects)
            {
                if (effect != null)
                {
                    worn.Add(effect.Attach(this));
                }
            }
        }
    }

    private void TakeOff()
    {
        foreach (Action end in worn)
        {
            end();
        }

        worn.Clear();
    }
}
