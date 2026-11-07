using System;
using UnityEngine;

/// <summary>
/// Eating and drinking take time and attention: the player stops to eat (slowed, unable to attack or guard) for a moment,
/// and only then is the food used up; its healing then arrives over several seconds rather than at once. A hit that gets
/// through or a dodge stops the meal, and the food is kept. Healing in a fight is a decision, not a button. Rules are in
/// <see cref="CombatTechniqueSettings"/> (Eating and drinking); ItemInventory.Use sends food and drink here.
/// </summary>
[RequireComponent(typeof(Damageable))]
public sealed class PlayerEating : MonoBehaviour
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

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }
    }

    private void OnDisable()
    {
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
        movement?.SetSpeedScale(this, Rules.EatMoveSpeed);
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

        if (needs != null && (item.HungerRestore > 0f || item.ThirstRestore > 0f))
        {
            needs.TryConsume(item.DisplayName, item.HungerRestore, item.ThirstRestore);
        }

        // Potent Remedies make draughts heal more; Hearty Meals make food heal at all.
        float heal = item.HealAmount * (kind == CraftedGoods.Tonics && Techniques.Has(PerkTechnique.PotentRemedies) ? 1f + techniques.PotentHeal : 1f);
        if (kind == CraftedGoods.Meals && Techniques.Has(PerkTechnique.HeartyMeals))
        {
            heal += health.MaxHealth * techniques.HeartyHeal;
            Techniques.Used(PerkTechnique.HeartyMeals);
        }

        if (kind == CraftedGoods.Tonics && item.HealAmount > 0f && Techniques.Has(PerkTechnique.PotentRemedies))
        {
            Techniques.Used(PerkTechnique.PotentRemedies);
        }

        if (heal > 0f)
        {
            float seconds = Mathf.Max(0.01f, Rules.HealOverSeconds);
            healLeft += heal;
            healPerSecond = Mathf.Max(healPerSecond, heal / seconds);
            if (Rules.HealOverSeconds <= 0f)
            {
                health.Heal(healLeft);
                healLeft = 0f;
            }

            GameMessages.Post($"{item.DisplayName}: +{Mathf.RoundToInt(heal)} health over {Rules.HealOverSeconds:0} s", MessageKind.Gain);
        }

        // A cook knows how to make a meal last, an alchemist a tonic (the Cooking and Alchemy skills).
        float boonSeconds = item.BoonSeconds * BoonScale(item);
        if (item.HasBoon && boons != null && boons.Add(item.Id, item.DisplayName, item.BoonBonuses, boonSeconds, !item.IsTonic))
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

        movement?.SetSpeedScale(this, 1f);
        Ended?.Invoke(item, eaten);
    }

    private float BoonScale(ItemDefinition item) =>
        TryGetComponent(out Combatant self)
            ? Mathf.Max(0.1f, 1f + self.Stats.Get(item.IsTonic ? StatType.TonicDuration : StatType.MealDuration))
            : 1f;
}
