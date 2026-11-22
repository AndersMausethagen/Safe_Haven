using System;
using UnityEngine;

/// <summary>
/// Eating and drinking take time and attention: the player stops to eat (slowed, unable to attack or guard) for a moment,
/// and only then is the food used up; its healing then arrives over several seconds rather than at once. A hit that gets
/// through or a dodge stops the meal, and the food is kept. Healing in a fight is a decision, not a button. Rules are in
/// <see cref="CombatTechniqueSettings"/> (Eating and drinking); ItemInventory.Use sends food and drink here.
/// </summary>
[RequireComponent(typeof(Damageable))]
public sealed class PlayerEating : MonoBehaviour, IPlayerActivity
{
    private Damageable health;
    private PlayerMovement movement;
    private PlayerNeeds needs;
    private PlayerDodge dodge;
    private PlayerCombat combat;
    private PlayerGuard guard;
    private PlayerBoons boons;
    private ItemDefinition eating;
    private ItemContainer from;
    private float doneAt;
    private float healLeft;
    private float healPerSecond;

    public bool IsEating => eating != null;

    /// <summary>How far through the current meal or draught, 0-1 (0 when not eating). The use bar shows it.</summary>
    public float Progress => IsEating ? Mathf.Clamp01(1f - (doneAt - Time.time) / Mathf.Max(0.01f, Rules.EatSeconds)) : 0f;

    /// <summary>The meal being eaten, or null.</summary>
    public ItemDefinition Meal => eating;

    /// <summary>Health still to come from meals already eaten.</summary>
    public float HealingToCome => healLeft;

    /// <summary>Raised when a meal starts (animation plays here).</summary>
    public event Action<ItemDefinition> Started;

    /// <summary>Raised when a meal ends: true when eaten, false when interrupted (the food is kept).</summary>
    public event Action<ItemDefinition, bool> Ended;

    private CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        health = GetComponent<Damageable>();
        movement = GetComponent<PlayerMovement>();
        needs = GetComponent<PlayerNeeds>();
        dodge = GetComponent<PlayerDodge>();
        combat = GetComponent<PlayerCombat>();
        guard = GetComponent<PlayerGuard>();
        boons = GetComponent<PlayerBoons>();
    }

    /// <summary>Death alone (PlayerInterrupts tells every activity of it); a blow that gets through or a dodge stop it here.</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.None;

    // Fallen (by a blow, or by poison or fire, which don't stop a meal while alive): nothing is eaten by the dead, and what was to
    // come of the last meal goes with them. Before, a meal went on after death over time: used up on the body, hunger filled, its boon
    // given, and healing after waking.
    public void Interrupt(PlayerInterrupt reason)
    {
        Stop(false);
        healLeft = 0f;
        healPerSecond = 0f;
    }

    private void OnEnable()
    {
        PlayerInterrupts.For(gameObject).Add(this);
        health.Damaged += OnDamaged;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }
    }

    private void OnDisable()
    {
        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

        health.Damaged -= OnDamaged;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        Stop(false);
    }

    /// <summary>Starts eating <paramref name="item"/> from <paramref name="container"/>. False while busy (eating already,
    /// swinging, guarding, dodging or reeling).</summary>
    public bool Begin(ItemDefinition item, ItemContainer container)
    {
        if (item == null || container == null || IsEating || !health.IsAlive
            || (combat != null && (combat.IsAttacking || combat.IsFlinching))
            || (guard != null && (guard.IsGuarding || guard.IsStunned))
            || (dodge != null && dodge.IsDodging))
        {
            return false;
        }

        eating = item;
        from = container;
        doneAt = Time.time + Rules.EatSeconds;
        if (movement != null)
        {
            movement.SetSpeedScale(this, Rules.EatMoveSpeed);
        }

        Started?.Invoke(item);
        return true;
    }

    private void Update()
    {
        if (IsEating && Time.time >= doneAt)
        {
            Finish();
        }

        if (healLeft > 0f && health.IsAlive)
        {
            float amount = Mathf.Min(healLeft, healPerSecond * Time.deltaTime);
            healLeft -= amount;
            health.Heal(amount);
            // All of it given: the rate goes with it. Kept, the strongest draught's rate healed every later meal in a moment.
            if (healLeft <= 0f)
            {
                healPerSecond = 0f;
            }
        }
    }

    private void Finish()
    {
        ItemDefinition item = eating;
        CraftedGoods kind = CraftedWork.Of(item);
        TechniqueSettings techniques = TechniqueSettings.Current;
        // Spare Dose: now and then a draught goes further than it should.
        bool spare = kind == CraftedGoods.Tonics && Techniques.Has(PerkTechnique.SpareDose) && UnityEngine.Random.value < techniques.SpareDoseChance;
        if (!spare && from.Remove(item, 1) <= 0)
        {
            Stop(false); // it left the bag while being eaten
            return;
        }

        if (spare)
        {
            GameMessages.Post($"Spare Dose: there's still some {item.DisplayName} left.", MessageKind.Gain);
            Techniques.Used(PerkTechnique.SpareDose);
        }

        // Only when it feeds: a meal eaten on a full belly for its boon said "You're not hungry." beside the boon's gain.
        if (needs != null && needs.WouldHelp(item.HungerRestore, item.ThirstRestore))
        {
            needs.TryConsume(item.DisplayName, item.HungerRestore, item.ThirstRestore);
        }

        // Potent Remedies make draughts heal more; Hearty Meals make food heal at all.
        float potent = kind == CraftedGoods.Tonics && Techniques.Has(PerkTechnique.PotentRemedies) ? 1f + techniques.PotentHeal : 1f;
        float heal = item.HealAmountFor(health.MaxHealth) * potent;
        // A draught that heals some at once (the Mending Draught) gives that now, the rest over its own time.
        float atOnce = item.HealAtOnceFor(health.MaxHealth) * potent;
        if (atOnce > 0f)
        {
            health.Heal(atOnce);
        }

        if (kind == CraftedGoods.Meals && Techniques.Has(PerkTechnique.HeartyMeals))
        {
            heal += health.MaxHealth * techniques.HeartyHeal;
            Techniques.Used(PerkTechnique.HeartyMeals);
        }

        if (kind == CraftedGoods.Tonics && item.HealTotal > 0f && Techniques.Has(PerkTechnique.PotentRemedies))
        {
            Techniques.Used(PerkTechnique.PotentRemedies);
        }

        if (heal > 0f)
        {
            float over = item.HealSeconds > 0f ? item.HealSeconds : Rules.HealOverSeconds;
            float seconds = Mathf.Max(0.01f, over);
            healLeft += heal;
            healPerSecond = Mathf.Max(healPerSecond, heal / seconds);
            if (over <= 0f)
            {
                health.Heal(healLeft);
                healLeft = 0f;
            }

            GameMessages.Post(atOnce > 0f ? $"{item.DisplayName}: +{Mathf.RoundToInt(atOnce)} health, then +{Mathf.RoundToInt(heal)} over {over:0} s"
                : $"{item.DisplayName}: +{Mathf.RoundToInt(heal)} health over {over:0} s", MessageKind.Gain);
        }
        else if (atOnce > 0f)
        {
            GameMessages.Post($"{item.DisplayName}: +{Mathf.RoundToInt(atOnce)} health", MessageKind.Gain);
        }

        // A cook knows how to make a meal last, an alchemist a tonic (the Cooking and Alchemy skills).
        float boonSeconds = item.BoonSeconds * BoonScale(item);
        if (item.HasBoon && boons != null && boons.Add(item.Id, item.DisplayName, item.BoonBonuses, boonSeconds, !item.IsTonic, item.IsTonic))
        {
            GameMessages.Post($"{item.DisplayName}: {ItemTooltipView.DescribeBonuses(item.BoonBonuses)} for {RaidStats.FormatTime(boonSeconds)}", MessageKind.Gain);
        }

        if (item.Cures.Count > 0 && TryGetComponent(out StatusEffects effects) && effects.Cure(item.Cures) > 0)
        {
            GameMessages.Post($"{item.DisplayName} clears your blood.", MessageKind.Gain);
        }

        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
        Stop(true);
    }

    // Only a hit that gets through stops the meal; blocked and parried ones don't reach here.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (IsEating && result.Damage > 0f && !result.Blocked && !damage.IsOverTime)
        {
            GameMessages.Post($"Stopped eating the {eating.DisplayName}.", MessageKind.Warning);
            Stop(false);
        }
    }

    private void OnDodged(Vector3 direction)
    {
        if (IsEating)
        {
            Stop(false);
        }
    }

    private void Stop(bool eaten)
    {
        if (!IsEating)
        {
            return;
        }

        ItemDefinition item = eating;
        eating = null;
        from = null;
        if (healLeft <= 0f)
        {
            healPerSecond = 0f;
        }

        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }

        Ended?.Invoke(item, eaten);
    }

    private float BoonScale(ItemDefinition item) =>
        TryGetComponent(out Combatant self)
            ? Mathf.Max(0.1f, 1f + self.Stats.Get(item.IsTonic ? StatType.TonicDuration : StatType.MealDuration))
            : 1f;
}
