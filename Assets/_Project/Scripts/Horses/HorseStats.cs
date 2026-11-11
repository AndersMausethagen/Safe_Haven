using System;
using UnityEngine;

/// <summary>Mare or stallion: breeding takes one of each (HorseBreeding).</summary>
public enum HorseSex
{
    Mare = 0,
    Stallion = 1,
}

/// <summary>What a horse has learned by living (Horse: traits from use), and passes on to its foals half the time.</summary>
[Flags]
public enum HorseTrait
{
    None = 0,
    /// <summary>Kilometres on rough ground (rock, slopes, forest floor): it doesn't slow on it.</summary>
    SureFooted = 1,
    /// <summary>Hurt often under a rider and kept going: it never panics, throws its rider or shies from monsters.</summary>
    Brave = 2,
    /// <summary>Long swims: it swims faster and tires less in the water.</summary>
    Swimmer = 4,
    /// <summary>A great many kilometres: its stamina comes back faster and a gallop costs it less.</summary>
    Enduring = 8,
}

/// <summary>
/// One horse's own numbers, rolled once from its <see cref="HorseDefinition"/>'s ranges when it is first met (or inherited
/// from its parents, HorseBreeding) and kept for life (saved with a tamed horse). Plain data so it can be tested and saved
/// as it is.
/// </summary>
[Serializable]
public struct HorseStats
{
    public float maxHealth;
    public float maxStamina;
    public float gallopSpeed;
    public float carryWeight;
    /// <summary>Index into the definition's coats, or -1 for the model's own.</summary>
    public int coat;
    /// <summary>Index into the definition's manes, or -1 for the model's own.</summary>
    public int mane;
    /// <summary>Feeds its breaking ride takes, before its temperament.</summary>
    public int feedsNeeded;
    public HorseSex sex;
    public HorseTemperament temperament;
    /// <summary>1 for a grown horse; a foal grows from Foal Size to 1 (HorseBreeding).</summary>
    public float growth;
    /// <summary>0 for a horse met wild, 1 for its foal, 2 for that foal's...</summary>
    public int generation;
    /// <summary>What it has learned (HorseTrait), inherited half the time.</summary>
    public HorseTrait traits;
    /// <summary>1 + its rare coat's index (HorseDefinition › Rare Coats), or 0 for a common coat (so saves from before
    /// rare coats load as common).</summary>
    public int rareCoat;

    public bool IsRolled => maxHealth > 0f;
    public bool IsGrown => growth <= 0f || growth >= 0.999f;
    public string SexName => sex == HorseSex.Mare ? "mare" : "stallion";
    public bool Has(HorseTrait trait) => (traits & trait) != 0;

    public static HorseStats Roll(HorseDefinition definition, System.Random random)
    {
        float Between(Vector2 range) => Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());
        Vector2Int feeds = definition.FeedsNeeded;
        int coat = definition.Coats.Length > 0 ? random.Next(definition.Coats.Length) : -1;
        return new HorseStats
        {
            maxHealth = Mathf.Round(Between(definition.Health)),
            maxStamina = Mathf.Round(Between(definition.Stamina)),
            gallopSpeed = Mathf.Round(Between(definition.GallopSpeed) * 10f) / 10f,
            carryWeight = Mathf.Round(Between(definition.CarryWeight)),
            coat = coat,
            mane = definition.ManeFor(coat, random),
            feedsNeeded = random.Next(Mathf.Min(feeds.x, feeds.y), Mathf.Max(feeds.x, feeds.y) + 1),
            sex = random.NextDouble() < 0.5 ? HorseSex.Mare : HorseSex.Stallion,
            temperament = HorseSettings.Current.RollTemperament(random),
            growth = 1f,
            rareCoat = definition.RollRareCoat(random),
        };
    }

    /// <summary>How good it is against the best its kind can be, 0-1: health, stamina, speed and strength alike.</summary>
    public float Quality(HorseDefinition definition)
    {
        float Share(float value, Vector2 range) => Mathf.Approximately(range.x, range.y) ? 1f : Mathf.InverseLerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), value);
        return (Share(maxHealth, definition.Health) + Share(maxStamina, definition.Stamina) + Share(gallopSpeed, definition.GallopSpeed)
                + Share(carryWeight, definition.CarryWeight)) / 4f;
    }
}
