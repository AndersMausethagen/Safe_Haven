using UnityEngine;

/// <summary>
/// Armour wears in a fight (the user's playtest, 2026-11-21: "Armor should suffer damage as we get attacked ... I have not needed to
/// ... craft or repair armor"). Every blow a creature lands on the player wears one piece of the body armour they wear, the one it
/// struck, picked by how much of the body each covers (the chest most often, the belt least), by Durability Settings › Armour Wear
/// Per Hit scaled by how hard the blow was; a blocked blow wears the shield instead. Damage over time (a burn, poison) and blows
/// with nobody behind them (a fall) wear nothing. A piece says once when it is worn low; when it breaks, PlayerEquipment says so,
/// and broken armour stops protecting (GearSet) until repaired.
/// <para>One piece a blow, not all of them (the review of 2026-11-21): a set repaired together broke together on one hit, with a
/// line for each, and every write restarted the gear's enchantments (PlayerRelics now rebuilds only on a real change).</para>
/// </summary>
public static class ArmourWear
{
    // Body armour only, and how often a blow finds each: rings, the amulet and a cloak don't take the blows.
    private static readonly (GearSlot slot, float share)[] Struck =
    {
        (GearSlot.Chest, 30f), (GearSlot.Legs, 18f), (GearSlot.Head, 14f), (GearSlot.Shoulders, 10f), (GearSlot.Hands, 10f),
        (GearSlot.Feet, 8f), (GearSlot.Wrists, 6f), (GearSlot.Waist, 4f),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Listen()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    private static void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (victim == null || damage.IsOverTime || damage.Source == null || !Player.IsPlayer(victim.gameObject)
            || !victim.TryGetComponent(out PlayerEquipment equipment))
        {
            return;
        }

        DurabilitySettings settings = DurabilitySettings.Current;
        // The blow as it was struck, before any guard took its share: a good shield is hammered by the same blow as a poor one.
        float blow = damage.Amount;
        if (result.Blocked)
        {
            Wear(equipment.Gear.Slots, (int)GearSlot.OffHand, ShieldWear(blow, settings), true);
            return;
        }

        if (result.Parried || !result.Landed)
        {
            return;
        }

        int slot = PieceStruck(equipment.Gear.Slots, Random.value);
        if (slot >= 0)
        {
            Wear(equipment.Gear.Slots, slot, WearFor(blow, settings), false);
        }
    }

    /// <summary>
    /// The slot of the worn armour piece a blow finds at <paramref name="roll"/> (0-1), each by its share of the body among those worn;
    /// -1 when no armour that can wear is worn.
    /// </summary>
    public static int PieceStruck(ItemContainer slots, float roll)
    {
        float total = 0f;
        foreach ((GearSlot slot, float share) in Struck)
        {
            total += CanWear(slots, (int)slot, false) ? share : 0f;
        }

        if (total <= 0f)
        {
            return -1;
        }

        float at = Mathf.Clamp01(roll) * total;
        int last = -1;
        foreach ((GearSlot slot, float share) in Struck)
        {
            if (!CanWear(slots, (int)slot, false))
            {
                continue;
            }

            last = (int)slot;
            at -= share;
            if (at <= 0f)
            {
                return last;
            }
        }

        return last;
    }

    /// <summary>What a blow of <paramref name="blow"/> damage wears from the piece it strikes.</summary>
    public static float WearFor(float blow, DurabilitySettings settings) =>
        settings == null || blow <= 0f ? 0f
            : settings.ArmourWearPerHit * Mathf.Clamp(blow / settings.ArmourWearReferenceDamage, 0.25f, 3f);

    /// <summary>What a blocked blow of <paramref name="blow"/> damage wears from the shield.</summary>
    public static float ShieldWear(float blow, DurabilitySettings settings) =>
        settings == null || blow <= 0f ? 0f
            : settings.ShieldWearPerBlock * Mathf.Clamp(blow / settings.ArmourWearReferenceDamage, 0.25f, 3f);

    private static bool CanWear(ItemContainer slots, int index, bool shield)
    {
        if (slots == null || index < 0 || index >= slots.SlotCount)
        {
            return false;
        }

        ItemStack stack = slots.GetSlot(index);
        return !stack.IsEmpty && stack.Item.HasDurability && !stack.IsBroken
               && (shield ? stack.Item.IsWeapon && stack.Item.Weapon.IsShield : stack.Item.IsArmor);
    }

    /// <summary>
    /// Wears the piece in slot <paramref name="index"/> by <paramref name="wear"/> (only armour, or a shield when
    /// <paramref name="shield"/>), saying so when it falls to worn. Returns its durability after, or -1 when nothing wore.
    /// </summary>
    public static float Wear(ItemContainer slots, int index, float wear, bool shield)
    {
        if (wear <= 0f || !CanWear(slots, index, shield))
        {
            return -1f;
        }

        ItemStack stack = slots.GetSlot(index);
        float before = stack.Durability;
        float after = Mathf.Max(0f, before - wear);
        slots.SetDurability(index, after);
        float warnAt = DurabilitySettings.Current.WornWarningAt;
        // Broken, PlayerEquipment says so ("... is broken and gives nothing until it is repaired").
        if (after > 0.0001f && before > warnAt && after <= warnAt)
        {
            GameMessages.Post($"{stack.Item.DisplayName} is badly worn ({after:P0}). Repair it at " +
                              $"{DurabilitySettings.Current.RepairPlaces} before it breaks.", MessageKind.Warning);
        }

        return after;
    }
}
