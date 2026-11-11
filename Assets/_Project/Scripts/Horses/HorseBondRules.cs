using UnityEngine;

/// <summary>
/// The bond between a horse and the player, 0 to 100, as plain rules. Feeding, grooming (once a day) and riding raise it;
/// days with none of them wear it down. What it gives: more stamina, less panic when hurt, a longer reach for the whistle
/// (and not coming at all below Whistle Bond), and a horse that no longer throws its rider when struck.
/// </summary>
public static class HorseBondRules
{
    public const float Most = 100f;

    public static float Share(float bond) => Mathf.Clamp01(bond / Most);

    public static float Add(float bond, float amount) => Mathf.Clamp(bond + amount, 0f, Most);

    public static float StaminaScale(float bond, HorseSettings settings) => 1f + settings.StaminaAtFullBond * Share(bond);

    /// <summary>How long it runs when hurt, as a share of its temperament's panic.</summary>
    public static float PanicScale(float bond, HorseSettings settings) => 1f - settings.CalmerAtFullBond * Share(bond);

    public static bool HearsWhistle(float bond, float distance, HorseSettings settings) =>
        bond >= settings.WhistleBond && distance <= WhistleReach(bond, settings);

    public static float WhistleReach(float bond, HorseSettings settings) => Mathf.Lerp(settings.WhistleReach.x, settings.WhistleReach.y, Share(bond));

    /// <summary>Bond after whole idle days with no feeding, grooming or riding.</summary>
    public static float AfterIdle(float bond, float idleDays, HorseSettings settings) =>
        Mathf.Max(0f, bond - Mathf.Max(0f, Mathf.Floor(idleDays)) * settings.BondLostPerIdleDay);

    /// <summary>True when a blow should throw the rider: low bond, and a roll of the dice that a bolder nature makes rarer.</summary>
    public static bool Throws(float bond, HorseTemperament temperament, HorseSettings settings, float roll)
    {
        if (bond >= settings.ThrowsBelowBond)
        {
            return false;
        }

        float chance = (1f - bond / Mathf.Max(1f, settings.ThrowsBelowBond)) * (temperament == HorseTemperament.Skittish ? 0.6f : temperament == HorseTemperament.Bold ? 0.15f : 0.35f);
        return roll < chance;
    }
}

/// <summary>
/// A foal from a mare and a stallion: each number the average of its parents' give or take Foal Spread, the coat and mane of
/// one of them, the temperament of one or, now and then, its own. Kept within its kind's ranges, stretched a little at the
/// top so breeding the best can make better. Plain rules, tested.
/// </summary>
public static class HorseBreeding
{
    public static bool CanBreed(HorseStats a, HorseStats b) => a.IsRolled && b.IsRolled && a.sex != b.sex && a.IsGrown && b.IsGrown;

    public static HorseStats Foal(HorseStats mother, HorseStats father, HorseDefinition kind, HorseSettings settings, System.Random random)
    {
        float Mix(float a, float b, Vector2 range)
        {
            float value = (a + b) * 0.5f * (1f + ((float)random.NextDouble() * 2f - 1f) * settings.FoalSpread);
            float low = Mathf.Min(range.x, range.y), high = Mathf.Max(range.x, range.y);
            return Mathf.Clamp(value, low, high * 1.1f);
        }

        HorseStats from = random.NextDouble() < 0.5 ? mother : father;
        return new HorseStats
        {
            maxHealth = Mathf.Round(Mix(mother.maxHealth, father.maxHealth, kind.Health)),
            maxStamina = Mathf.Round(Mix(mother.maxStamina, father.maxStamina, kind.Stamina)),
            gallopSpeed = Mathf.Round(Mix(mother.gallopSpeed, father.gallopSpeed, kind.GallopSpeed) * 10f) / 10f,
            carryWeight = Mathf.Round(Mix(mother.carryWeight, father.carryWeight, kind.CarryWeight)),
            coat = from.coat,
            mane = from.mane,
            // A rare coat comes down with the parent it takes after; what each parent learned, half the time.
            rareCoat = from.rareCoat,
            traits = InheritTraits(mother.traits, father.traits, random),
            feedsNeeded = 0,
            sex = random.NextDouble() < 0.5 ? HorseSex.Mare : HorseSex.Stallion,
            temperament = random.NextDouble() < 0.2 ? settings.RollTemperament(random) : (random.NextDouble() < 0.5 ? mother : father).temperament,
            growth = settings.FoalSize,
            generation = Mathf.Max(mother.generation, father.generation) + 1,
        };
    }

    /// <summary>Each trait either parent has, passed on half the time.</summary>
    public static HorseTrait InheritTraits(HorseTrait mother, HorseTrait father, System.Random random)
    {
        HorseTrait both = mother | father;
        HorseTrait inherited = HorseTrait.None;
        foreach (HorseTrait trait in new[] { HorseTrait.SureFooted, HorseTrait.Brave, HorseTrait.Swimmer, HorseTrait.Enduring })
        {
            if ((both & trait) != 0 && random.NextDouble() < 0.5)
            {
                inherited |= trait;
            }
        }

        return inherited;
    }

    /// <summary>How grown a foal is after some in-game days: from Foal Size to 1 over Days To Grow.</summary>
    public static float GrowthAfter(float bornGrowth, float days, HorseSettings settings) =>
        Mathf.Clamp01(Mathf.Lerp(bornGrowth, 1f, days / Mathf.Max(0.1f, settings.DaysToGrow)));
}
