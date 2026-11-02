using UnityEngine;

/// <summary>
/// How gear turns into a gear score (GearScore), edited in the Systems Editor. A full kit of one tier is worth Points Per Tier times
/// that tier, shared out over the slots by how much each matters, the weapon most (as V Rising weighs its weapons). So the score
/// climbs evenly while real power compounds, as item levels do in World of Warcraft, and a raid tier can name the score it expects.
/// </summary>
public sealed class GearScoreSettings : GameSettings<GearScoreSettings>
{
    [Header("The scale")]
    [Tooltip("What a full kit of tier I is worth. A full kit of tier III is worth three times this.")]
    [SerializeField, Min(1f)] private float pointsPerTier = 20f;

    [Header("Each slot's share of a full kit (they should add up to 1)")]
    [Tooltip("The weapon in the main hand. A two-handed weapon also takes the off hand's share, so nothing is lost by carrying one.")]
    [SerializeField, Range(0f, 1f)] private float mainHand = 0.3f;
    [Tooltip("A shield or torch.")]
    [SerializeField, Range(0f, 1f)] private float offHand = 0.08f;
    [SerializeField, Range(0f, 1f)] private float chest = 0.2f;
    [SerializeField, Range(0f, 1f)] private float head = 0.08f;
    [SerializeField, Range(0f, 1f)] private float hands = 0.06f;
    [SerializeField, Range(0f, 1f)] private float waist = 0.05f;
    [SerializeField, Range(0f, 1f)] private float feet = 0.07f;
    [SerializeField, Range(0f, 1f)] private float neck = 0.06f;
    [Tooltip("Each ring. There are two ring slots.")]
    [SerializeField, Range(0f, 1f)] private float finger = 0.05f;

    [Header("What adds to a piece's score")]
    [Tooltip("Multiplies the score by rarity: Common, Uncommon, Rare, Epic, Legendary. Kept small, as the tier says most about a piece.")]
    [SerializeField] private float[] rarity = { 1f, 1.05f, 1.1f, 1.2f, 1.3f };
    [Tooltip("Share of the piece's score each level of empowering adds. +3 at 0.1 gives 30%: a piece kept up stays useful into the next tier.")]
    [SerializeField, Min(0f)] private float perEmpowerLevel = 0.1f;
    [Tooltip("Share of the piece's score an enchantment adds.")]
    [SerializeField, Min(0f)] private float perEnchantment = 0.1f;

    public float PointsPerTier => pointsPerTier;
    public float PerEmpowerLevel => perEmpowerLevel;
    public float PerEnchantment => perEnchantment;

    /// <summary>What a full kit of a tier is worth.</summary>
    public float FullKit(int tier) => pointsPerTier * tier;

    public float RarityFactor(ItemRarity value)
    {
        int index = (int)value;
        return rarity != null && index >= 0 && index < rarity.Length ? rarity[index] : 1f;
    }

    /// <summary>An item's share of a full kit; 0 for what doesn't count, such as a backpack or a tool.</summary>
    public float ShareOf(ItemDefinition item)
    {
        if (item.IsWeapon)
        {
            return item.Weapon.IsShield ? offHand : item.Weapon.RequiresBothHands ? mainHand + offHand : mainHand;
        }

        switch (item.ArmorSlot)
        {
            case ArmorSlot.Head:
                return head;
            case ArmorSlot.Neck:
                return neck;
            case ArmorSlot.Chest:
                return chest;
            case ArmorSlot.Hands:
                return hands;
            case ArmorSlot.Waist:
                return waist;
            case ArmorSlot.Feet:
                return feet;
            case ArmorSlot.Finger:
                return finger;
            default:
                return 0f;
        }
    }

    /// <summary>All the shares of a full kit added up, for the editor to check against 1.</summary>
    public float TotalShare => mainHand + offHand + chest + head + hands + waist + feet + neck + finger * 2f;
}
