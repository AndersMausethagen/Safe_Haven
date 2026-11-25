using UnityEngine;

/// <summary>Something on a Damageable that reshapes a blow after armour and resistances, before health is taken (PlayerBlowGrace). Not
/// asked for damage over time, nor for an exact blow decided elsewhere (Damageable.Apply).</summary>
public interface IBlowShaper
{
    /// <summary>What the blow takes after this: <paramref name="final"/> is what it would take, of <paramref name="maxHealth"/>.</summary>
    float Shape(in DamageInfo hit, float final, float maxHealth);
}

/// <summary>
/// The player's two protections against deaths nobody could answer (the user's playtest, 2026-11-25): a blow from anything but a boss
/// takes at most Heaviest Blow Share of their health (an Old Moss Troll's stomp took two thirds from behind), and after a big blow
/// (Big Blow Share or more) the next ones for Grace Seconds take only Grace Taken of themselves (three Heartroot eruptions, 28, 28 and
/// 33, inside a second). Pure, so it can be tested; PlayerBlowGrace carries it out. Rules in CombatTechniqueSettings.
/// </summary>
public static class BlowShaping
{
    /// <summary>What a blow takes, capped and eased: <paramref name="final"/> after the cap (only a creature's blow that isn't a boss's is
    /// capped: a fall, the cold or deep water keep their own measure), then eased while in grace.</summary>
    public static float Shape(float final, float maxHealth, bool capped, bool inGrace, CombatTechniqueSettings rules)
    {
        if (final <= 0f || maxHealth <= 0f)
        {
            return final;
        }

        if (capped && rules.HeaviestBlowShare > 0f)
        {
            final = Mathf.Min(final, maxHealth * rules.HeaviestBlowShare);
        }

        return inGrace ? final * Mathf.Clamp01(rules.GraceTaken) : final;
    }

    /// <summary>True when a blow that took <paramref name="taken"/> of <paramref name="maxHealth"/> starts a grace.</summary>
    public static bool StartsGrace(float taken, float maxHealth, CombatTechniqueSettings rules) =>
        rules.GraceSeconds > 0f && maxHealth > 0f && taken >= maxHealth * rules.BigBlowShare;
}
