using UnityEngine;

/// <summary>
/// Armour wears in a fight (the user's playtest, 2026-11-21: "Armor should suffer damage as we get attacked ... I have not needed to
/// ... craft or repair armor"). Every blow that reaches the player wears each piece of body armour they wear by Durability Settings ›
/// Armour Wear Per Hit, scaled by how hard it was; a blocked blow wears the shield instead. Damage over time (a burn, poison) wears
/// nothing. A piece says once when it is worn low and again when it breaks; broken armour stops protecting (GearSet) until repaired.
/// </summary>
public static class ArmourWear
{
    // Body armour only: rings, the amulet and a cloak don't take the blows.
    private static readonly GearSlot[] Worn =
    {
        GearSlot.Head, GearSlot.Shoulders, GearSlot.Chest, GearSlot.Wrists, GearSlot.Hands, GearSlot.Waist, GearSlot.Legs, GearSlot.Feet,
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Listen()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    private static void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (victim == null || damage.IsOverTime || !Player.IsPlayer(victim.gameObject) || !victim.TryGetComponent(out PlayerEquipment equipment))
        {
            return;
        }

        if (result.Blocked || result.Parried)
        {
            // A parry turns the blow aside: the shield isn't hammered by it.
            if (result.Blocked)
            {
                Wear(equipment.Gear.Slots, (int)GearSlot.OffHand, ShieldWear(result.Incoming, DurabilitySettings.Current), true);
            }

            return;
        }

        if (!result.Landed)
        {
            return;
        }

        float wear = WearFor(result.Incoming, DurabilitySettings.Current);
        foreach (GearSlot slot in Worn)
        {
            Wear(equipment.Gear.Slots, (int)slot, wear, false);
        }
    }

    /// <summary>What a blow of <paramref name="incoming"/> damage (before armour) wears from each piece of armour.</summary>
    public static float WearFor(float incoming, DurabilitySettings settings) =>
        settings == null || incoming <= 0f ? 0f
            : settings.ArmourWearPerHit * Mathf.Clamp(incoming / settings.ArmourWearReferenceDamage, 0.25f, 3f);

    /// <summary>What a blocked blow of <paramref name="incoming"/> damage wears from the shield.</summary>
    public static float ShieldWear(float incoming, DurabilitySettings settings) =>
        settings == null || incoming <= 0f ? 0f
            : settings.ShieldWearPerBlock * Mathf.Clamp(incoming / settings.ArmourWearReferenceDamage, 0.25f, 3f);

    /// <summary>
    /// Wears the piece in slot <paramref name="index"/> by <paramref name="wear"/> (only armour, or a shield when
    /// <paramref name="shield"/>), saying so when it falls to worn and when it breaks. Returns its durability after.
    /// </summary>
    public static float Wear(ItemContainer slots, int index, float wear, bool shield)
    {
        if (slots == null || index < 0 || index >= slots.SlotCount || wear <= 0f)
        {
            return -1f;
        }

        ItemStack stack = slots.GetSlot(index);
        bool fits = !stack.IsEmpty && stack.Item.HasDurability && !stack.IsBroken
                    && (shield ? stack.Item.IsWeapon && stack.Item.Weapon.IsShield : stack.Item.IsArmor);
        if (!fits)
        {
            return -1f;
        }

        float before = stack.Durability;
        float after = Mathf.Max(0f, before - wear);
        slots.SetDurability(index, after);
        float warnAt = DurabilitySettings.Current.WornWarningAt;
        if (after <= 0.0001f)
        {
            GameMessages.Post($"{stack.Item.DisplayName} broke: it protects nothing until it is repaired at the " +
                              $"{DurabilitySettings.Current.RepairStation.DisplayName()}.", MessageKind.Warning);
        }
        else if (before > warnAt && after <= warnAt)
        {
            GameMessages.Post($"{stack.Item.DisplayName} is badly worn ({after:P0}). Repair it before it breaks.", MessageKind.Warning);
        }

        return after;
    }
}
