using UnityEngine;

/// <summary>
/// How long crafting by hand takes, by what is made, and how slowly the player walks meanwhile (HandCraftQueue). A
/// weapon takes 20 s and a small (one-handed) weapon 10 s (the user, 2026-11-15); the rest are this project's choice:
/// tools and worn gear about as long as a small weapon, a medicine or a meal-like mix a few seconds, a bundle of arrows or
/// a cord or parchment a moment. One asset in Resources.
/// </summary>
[CreateAssetMenu(fileName = "HandCraftingSettings", menuName = "Safe Haven/Crafting/Hand Crafting Settings")]
public sealed class HandCraftingSettings : ScriptableObject
{
    private const string ResourcePath = "HandCraftingSettings";

    [Tooltip("Two-handed weapons, polearms, staffs and bows.")]
    [SerializeField, Min(0f)] private float weaponSeconds = 20f;
    [Tooltip("One-handed weapons.")]
    [SerializeField, Min(0f)] private float smallWeaponSeconds = 10f;
    [Tooltip("Tools on the belt (hatchet, pickaxe, sickle), a fishing rod, a lasso.")]
    [SerializeField, Min(0f)] private float toolSeconds = 10f;
    [Tooltip("Worn gear: armour, packs, lanterns.")]
    [SerializeField, Min(0f)] private float gearSeconds = 10f;
    [Tooltip("Things eaten or drunk: salves, draughts, food.")]
    [SerializeField, Min(0f)] private float consumableSeconds = 5f;
    [Tooltip("Arrows and other things made several at a time, per craft.")]
    [SerializeField, Min(0f)] private float ammunitionSeconds = 3f;
    [Tooltip("Anything else: cord, parchment, a torch, resources.")]
    [SerializeField, Min(0f)] private float otherSeconds = 3f;
    [Tooltip("Share of walking speed while crafting by hand.")]
    [SerializeField, Range(0.1f, 1f)] private float walkShare = 0.5f;

    private static HandCraftingSettings cached;

    /// <summary>The asset in Resources, or defaults when there is none.</summary>
    public static HandCraftingSettings Current
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<HandCraftingSettings>(ResourcePath);
                if (cached == null)
                {
                    cached = CreateInstance<HandCraftingSettings>();
                }
            }

            return cached;
        }
    }

    public float WalkShare => walkShare;

    /// <summary>Seconds one craft of <paramref name="recipe"/> takes by hand.</summary>
    public float SecondsFor(RecipeDefinition recipe)
    {
        ItemDefinition item = recipe != null ? recipe.Output : null;
        if (item == null)
        {
            return otherSeconds;
        }

        if (item.IsWeapon)
        {
            WeaponDefinition weapon = item.Weapon;
            if (weapon.Kind == WeaponKind.Torch)
            {
                return otherSeconds;
            }

            if (weapon.Kind == WeaponKind.FishingRod)
            {
                return toolSeconds;
            }

            bool large = weapon.IsBow || weapon.RequiresBothHands || weapon.Stance == WeaponStance.Polearm
                         || weapon.Signature == WeaponSignature.Channel;
            return large ? weaponSeconds : smallWeaponSeconds;
        }

        if (item.IsConsumable)
        {
            return consumableSeconds;
        }

        if (item.IsEquippable)
        {
            return item.ToolType != ToolType.None ? toolSeconds : gearSeconds;
        }

        return recipe.OutputAmount > 1 ? ammunitionSeconds : item.Weight >= 1f ? toolSeconds : otherSeconds;
    }
}
