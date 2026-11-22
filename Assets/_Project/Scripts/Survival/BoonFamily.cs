using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which family a meal's or tonic's boon belongs to, by what it raises most: Hearty (health, armour), Fierce (strength, power, damage),
/// Swift (stamina, speed) or Warding (fire, frost, holy). One of each family works at a time (PlayerBoons; the user, 2026-11-22: "one
/// of each boon", Valheim's rule): extra slots add breadth, not a second helping of armour (Docs/PowerCurveResearch_2026-11-22.md #2).
/// The tooltip, the boon slots and the tier balance (GearBalance) all ask here.
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

        float hearty = 0f, swift = 0f, fierce = 0f, warding = 0f;
        foreach (StatBonus bonus in bonuses)
        {
            switch (bonus.stat)
            {
                case StatType.MaxHealth: case StatType.Armor: case StatType.PhysicalResistance: hearty += 1f; break;
                case StatType.MaxStamina: case StatType.MoveSpeed: swift += 1f; break;
                case StatType.Strength: case StatType.PhysicalPower: case StatType.DamageBonus: case StatType.MeleeDamage: fierce += 1.5f; break;
                case StatType.FireResistance: case StatType.FrostResistance: case StatType.HolyResistance: warding += 1f; break;
            }
        }

        float most = Mathf.Max(Mathf.Max(hearty, swift), Mathf.Max(fierce, warding));
        return most <= 0f ? "" : fierce == most ? "Fierce" : hearty == most ? "Hearty" : warding == most ? "Warding" : "Swift";
    }
}
