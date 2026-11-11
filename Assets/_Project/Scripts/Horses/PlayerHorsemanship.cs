using UnityEngine;

/// <summary>What the player does with horses that trains Horsemanship (HorseSettings › Experience).</summary>
public enum HorseWork
{
    Tame = 0,
    Feed = 1,
    Groom = 2,
    /// <summary>A kilometre ridden.</summary>
    Kilometre = 3,
    /// <summary>A blow struck or an arrow loosed from the saddle that hit.</summary>
    MountedHit = 4,
    /// <summary>A foal born in the player's stable.</summary>
    Foal = 5,
}

/// <summary>
/// The player's Horsemanship, as the horse systems read it: the stats its levels and perks give (StatType › TamingHold,
/// GallopStaminaCost, HorseBond, MountedDamage) and the practice that trains it. 0 while there is no player.
/// </summary>
public static class PlayerHorsemanship
{
    private static float Stat(StatType stat) =>
        Player.Current != null && Player.Current.Combatant != null ? Player.Current.Combatant.Stats.Get(stat) : 0f;

    /// <summary>Share more hold on the rope (taps and leaning against the pull).</summary>
    public static float TamingHold() => Mathf.Max(-0.5f, Stat(StatType.TamingHold));

    /// <summary>What a gallop costs the horse, times its usual cost.</summary>
    public static float GallopCostScale() => Mathf.Max(0.2f, 1f + Stat(StatType.GallopStaminaCost));

    /// <summary>Share more bond from caring for a horse.</summary>
    public static float BondGain() => Mathf.Max(-0.5f, Stat(StatType.HorseBond));

    /// <summary>Share more damage from the saddle.</summary>
    public static float MountedDamage() => Stat(StatType.MountedDamage);

    /// <summary>Trains Horsemanship by this work (times <paramref name="times"/>).</summary>
    public static void Practise(HorseWork work, float times = 1f)
    {
        float amount = HorseSettings.Current.ExperienceFor(work) * times;
        if (amount > 0f)
        {
            ExperienceTracker.Practise(ProficiencyTraining.Horsemanship, 1, amount);
        }
    }
}
