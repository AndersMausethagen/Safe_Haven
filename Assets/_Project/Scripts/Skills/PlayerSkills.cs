using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a cast did or didn't happen.</summary>
public enum SkillCastResult
{
    Cast,
    EmptySlot,
    CoolingDown,
    /// <summary>Dead, interacting, dodging or still casting the previous skill.</summary>
    Busy,
    NoStamina,
    NoFocus,
}

/// <summary>
/// The player's skills, V Rising style: a book of known skills, a five-slot bar (the loadout) arranged in the skill
/// window, and casting from the bar by hotkey. A cast pays stamina (an empty pool refuses it), starts the skill's own
/// cooldown and then carries out the definition: lunge, hit through AttackResolver, heal and timed buffs.
/// <para>
/// Where it belongs: on the PlayerArmature prefab, reached through Player.Current.Skills. Input arrives through
/// PlayerStateMachine.SkillRequested. Known skills and the loadout travel between scenes in PlayerSnapshot; cooldowns
/// and buffs are runtime-only.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerMovement), typeof(Combatant))]
[RequireComponent(typeof(Damageable))]
public sealed class PlayerSkills : MonoBehaviour
{
    [SerializeField] private PlayerSkillSettings settings;
    [Tooltip("Layers skill hits can reach. Targets still have to be damageable and hostile.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    private readonly List<SkillDefinition> known = new List<SkillDefinition>();
    private readonly SkillLoadout loadout = new SkillLoadout();
    private readonly SkillCooldowns cooldowns = new SkillCooldowns();
    private readonly Dictionary<SkillDefinition, float> activeBuffs = new Dictionary<SkillDefinition, float>();
    private readonly List<SkillDefinition> expired = new List<SkillDefinition>();

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Combatant combatant;
    private Damageable health;
    private PlayerStamina stamina;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private bool initialised;

    private float busyUntil;
    private SkillDefinition pendingHit;
    private float pendingHitAt;
    private Vector3 pendingDirection;

    public SkillLoadout Loadout => loadout;
    public IReadOnlyList<SkillDefinition> KnownSkills => known;

    /// <summary>Raised when a skill is cast, with its slot. Animation and the skill bar listen.</summary>
    public event Action<int, SkillDefinition> SkillCast;

    /// <summary>Raised when a cast from a slot was refused, and why.</summary>
    public event Action<int, SkillCastResult> CastRefused;

    /// <summary>Raised after the set of known skills changes.</summary>
    public event Action KnownSkillsChanged;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
        stamina = GetComponent<PlayerStamina>();
        combat = GetComponent<PlayerCombat>();
        dodge = GetComponent<PlayerDodge>();
        EnsureInitialised();
    }

    private void OnEnable()
    {
        stateMachine.SkillRequested += OnSkillRequested;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }
    }

    private void OnDisable()
    {
        stateMachine.SkillRequested -= OnSkillRequested;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        pendingHit = null;
        foreach (SkillDefinition skill in activeBuffs.Keys)
        {
            combatant.Stats.RemoveModifiersFrom(skill);
        }

        activeBuffs.Clear();
    }

    private void Update()
    {
        // Death cancels a hit that has not landed yet, just as a dodge does.
        if (!health.IsAlive)
        {
            pendingHit = null;
        }

        if (pendingHit != null && Time.time >= pendingHitAt)
        {
            SkillDefinition skill = pendingHit;
            pendingHit = null;
            float weaponDamage = combat != null && combat.Weapon != null ? combat.Weapon.Damage : 0f;
            // The Runecraft skill scales every spell's damage; its hits carry the Spell technique, which trains it.
            float runecraft = Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.SpellDamage));
            AttackResolver.Execute(combatant, skill.Attack, weaponDamage, transform.position, pendingDirection, hitLayers, Power(skill) * runecraft,
                1f, HitTechnique.Spell);
        }

        if (activeBuffs.Count > 0)
        {
            ExpireBuffs();
        }
    }

    public bool IsKnown(SkillDefinition skill) => skill != null && known.Contains(skill);

    public float CooldownRemaining(SkillDefinition skill) => cooldowns.Remaining(skill, Time.time);

    /// <summary>Share of the skill's cooldown still to go, from 1 (just cast) to 0 (ready).</summary>
    public float CooldownFraction(SkillDefinition skill) => cooldowns.Fraction(skill, Time.time);

    /// <summary>Learns a skill. Returns false when it was already known.</summary>
    public bool Learn(SkillDefinition skill, bool announce = true)
    {
        EnsureInitialised();
        if (!AddKnown(skill))
        {
            return false;
        }

        if (announce)
        {
            GameMessages.Post("New skill: " + skill.DisplayName, MessageKind.Gain);
        }

        KnownSkillsChanged?.Invoke();
        return true;
    }

    /// <summary>Puts a known skill in a bar slot (null clears it). Returns false when nothing changed.</summary>
    public bool Equip(int slot, SkillDefinition skill)
    {
        if (skill != null && !IsKnown(skill))
        {
            return false;
        }

        return loadout.Equip(slot, skill);
    }

    /// <summary>
    /// Makes the book and bar exactly the given ones, for a scene handover. Called by PlayerSnapshot, possibly before
    /// this component's Awake; it replaces the starting setup.
    /// </summary>
    public void Restore(IEnumerable<SkillDefinition> knownSkills, SkillDefinition[] slots)
    {
        EnsureInitialised();
        known.Clear();
        if (knownSkills != null)
        {
            foreach (SkillDefinition skill in knownSkills)
            {
                AddKnown(skill);
            }
        }

        var allowed = new SkillDefinition[SkillLoadout.SlotCount];
        for (int i = 0; i < allowed.Length && slots != null && i < slots.Length; i++)
        {
            allowed[i] = IsKnown(slots[i]) ? slots[i] : null;
        }

        loadout.SetAll(allowed);
        KnownSkillsChanged?.Invoke();
    }

    /// <summary>Casts the skill in a bar slot. Everything that can refuse it is checked before anything is paid.</summary>
    public SkillCastResult TryCast(int slot)
    {
        SkillDefinition skill = loadout.Get(slot);
        SkillCastResult result = CheckCast(skill);
        PlayerFocus focus = GetComponent<PlayerFocus>();
        if (result == SkillCastResult.Cast && skill.FocusCost > 0f && focus != null && !focus.CanSpend(skill.FocusCost))
        {
            focus.TrySpend(skill.FocusCost); // refused: lets the HUD say so
            result = SkillCastResult.NoFocus;
        }

        if (result == SkillCastResult.Cast && skill.StaminaCost > 0f && stamina != null && !stamina.TrySpend(SpellCost(skill)))
        {
            result = SkillCastResult.NoStamina;
        }

        if (result == SkillCastResult.Cast && skill.FocusCost > 0f && focus != null)
        {
            focus.TrySpend(skill.FocusCost);
        }

        if (result != SkillCastResult.Cast)
        {
            CastRefused?.Invoke(slot, result);
            return result;
        }

        Perform(skill);
        SkillCast?.Invoke(slot, skill);
        return result;
    }

    private SkillCastResult CheckCast(SkillDefinition skill)
    {
        if (skill == null)
        {
            return SkillCastResult.EmptySlot;
        }

        if (!health.IsAlive
            || stateMachine.CurrentState == PlayerState.Interacting
            || (dodge != null && dodge.IsDodging)
            || movement.IsDashing
            || (TryGetComponent(out PlayerEating eating) && eating.IsEating)
            || Time.time < busyUntil)
        {
            return SkillCastResult.Busy;
        }

        return cooldowns.IsReady(skill, Time.time) ? SkillCastResult.Cast : SkillCastResult.CoolingDown;
    }

    private void Perform(SkillDefinition skill)
    {
        float now = Time.time;
        // Spell Echo: now and then the words leave no weariness behind.
        if (Techniques.Has(PerkTechnique.SpellEcho) && UnityEngine.Random.value < TechniqueSettings.Current.SpellEchoChance)
        {
            GameMessages.Post($"Spell Echo: {skill.DisplayName} is ready again.", MessageKind.Gain);
        }
        else
        {
            cooldowns.Start(skill, now, CooldownOf(skill));
        }
        busyUntil = now + skill.CastTime;

        // Like the weapon swing, the direction is locked in toward the cursor as the cast begins.
        Vector3 direction = movement.AimDirection;
        float hitAfter = 0f;
        if (skill.LungeDistance > 0f)
        {
            movement.Dash(direction * (skill.LungeDistance / skill.LungeDuration), skill.LungeDuration);
            hitAfter = skill.LungeDuration;
        }

        if (skill.Attack != null)
        {
            pendingHit = skill;
            pendingHitAt = now + hitAfter + skill.Attack.HitDelay;
            busyUntil = Mathf.Max(busyUntil, pendingHitAt);
            pendingDirection = direction;
            GameStateManager gameState = GameStateManager.Current;
            if (gameState != null)
            {
                gameState.TrySetState(GameState.Combat);
            }
        }

        if (skill.Heal > 0f)
        {
            health.Heal(skill.Heal * Power(skill));
        }

        if (skill.HasBuffs)
        {
            ApplyBuffs(skill, now);
        }
    }

    /// <summary>What deepening a skill at the altar multiplies its damage, healing and buffs by.</summary>
    public static float Power(SkillDefinition skill) => 1f + EssenceSettings.Current.PowerAt(GameSession.Progress.RankOf(skill));

    /// <summary>The skill's cooldown at its rank.</summary>
    // A spell's stamina after the Runecraft skill: a beginner's words cost more breath.
    private float SpellCost(SkillDefinition skill) =>
        skill.StaminaCost * (combatant != null ? Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.SpellStaminaCost)) : 1f);

    public static float CooldownOf(SkillDefinition skill) =>
        skill.Cooldown * (1f - EssenceSettings.Current.CooldownCutAt(GameSession.Progress.RankOf(skill)));

    private void ApplyBuffs(SkillDefinition skill, float now)
    {
        // Recasting refreshes the buff rather than stacking it.
        combatant.Stats.RemoveModifiersFrom(skill);
        foreach (SkillStatBuff buff in skill.Buffs)
        {
            float value = buff.value > 0f ? buff.value * Power(skill) : buff.value;
            combatant.Stats.AddModifier(new StatModifier(buff.stat, buff.op, value, skill));
        }

        activeBuffs[skill] = now + skill.BuffDuration;
    }

    private void ExpireBuffs()
    {
        expired.Clear();
        foreach (KeyValuePair<SkillDefinition, float> buff in activeBuffs)
        {
            if (Time.time >= buff.Value)
            {
                expired.Add(buff.Key);
            }
        }

        foreach (SkillDefinition skill in expired)
        {
            activeBuffs.Remove(skill);
            combatant.Stats.RemoveModifiersFrom(skill);
        }
    }

    // A dodge is the way out of trouble: like a weapon swing, a skill hit that hadn't landed yet is lost.
    private void OnDodged(Vector3 direction)
    {
        pendingHit = null;
    }

    private void OnSkillRequested(int slot)
    {
        TryCast(slot);
    }

    private void EnsureInitialised()
    {
        if (initialised)
        {
            return;
        }

        initialised = true;
        if (settings == null)
        {
            Debug.LogWarning("PlayerSkills has no settings; the player starts with no skills.", this);
            return;
        }

        foreach (SkillDefinition skill in settings.StartingSkills)
        {
            AddKnown(skill);
        }

        for (int i = 0; i < settings.StartingLoadout.Count && i < SkillLoadout.SlotCount; i++)
        {
            SkillDefinition skill = settings.StartingLoadout[i];
            if (skill != null)
            {
                AddKnown(skill);
                loadout.Equip(i, skill);
            }
        }
    }

    private bool AddKnown(SkillDefinition skill)
    {
        if (skill == null || known.Contains(skill))
        {
            return false;
        }

        known.Add(skill);
        return true;
    }
}
