using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which family a meal's or tonic's boon belongs to, by what it raises most: Hearty (health, armour, lifesteal), Fierce (strength,
/// power, damage, crits), Swift (stamina, speed, what swings and blocks cost), Warding (fire, frost, blight) or Cunning (arrows,
/// stealth, ambushes, spells). One of each family works at a time (PlayerBoons; the user, 2026-11-22: "one of each boon", Valheim's
/// rule): extra slots add breadth, not a second helping of armour (Docs/PowerCurveResearch_2026-11-22.md #2). Work boons (learning,
/// fishing, gathering and felling yields, tool care, horses: <see cref="IsWork"/>) belong to no family: they fill a slot and replace
/// nothing, as they never make a fight easier. The tooltip, the boon slots and the tier balance (GearBalance) all ask here.
/// </summary>
public static class BoonFamily
{
    /// <summary>The family's name, or empty for a boon that raises none of them.</summary>
    public static string Of(IReadOnlyList<StatBonus> bonuses)
    {
        if (bonuses == null)
        {
            return "";
        }

        float hearty = 0f, swift = 0f, fierce = 0f, warding = 0f, cunning = 0f;
        foreach (StatBonus bonus in bonuses)
        {
            switch (bonus.stat)
            {
                // Lifesteal (Blood Sausage) keeps you standing as health does.
                case StatType.MaxHealth: case StatType.Armor: case StatType.PhysicalResistance: case StatType.Lifesteal: hearty += 1f; break;
                // What swinging and blocking cost (Bog Myrtle Ale, Bone Broth) is stamina by another road.
                case StatType.MaxStamina: case StatType.MoveSpeed: case StatType.MeleeStaminaCost: case StatType.BlockStaminaCost: swift += 1f; break;
                // Crits too (Venison with Lingonberries): another way to more damage, so it takes a fierce meal's place, not a seat beside it.
                case StatType.Strength: case StatType.PhysicalPower: case StatType.DamageBonus: case StatType.MeleeDamage:
                case StatType.PhysicalCritChance: case StatType.PhysicalCritDamage: fierce += 1.5f; break;
                case StatType.FireResistance: case StatType.FrostResistance: case StatType.HolyResistance: warding += 1f; break;
                case StatType.ArrowDamage: case StatType.DrawSpeed: case StatType.SneakNoise: case StatType.SneakVisibility: case StatType.AmbushDamage:
                case StatType.SpellDamage: case StatType.SpellStaminaCost: cunning += 1f; break;
            }
        }

        float most = Mathf.Max(Mathf.Max(Mathf.Max(hearty, swift), Mathf.Max(fierce, warding)), cunning);
        return most <= 0f ? "" : fierce == most ? "Fierce" : hearty == most ? "Hearty" : warding == most ? "Warding" : cunning == most ? "Cunning" : "Swift";
    }

    /// <summary>
    /// A boon for work, not fights: learning, fishing, yields, tool care, horses. These have no family (they fill a slot and replace
    /// nothing); every other boon stat must belong to one.
    /// </summary>
    public static bool IsWork(StatType stat)
    {
        switch (stat)
        {
            case StatType.GatherYield: case StatType.ForageYield: case StatType.ChopYield: case StatType.MineYield: case StatType.ToolCare:
            case StatType.ExperienceGain: case StatType.ReelZone: case StatType.HorseBond: case StatType.TamingHold: case StatType.GallopStaminaCost:
                return true;
            default:
                return false;
        }
    }
}
