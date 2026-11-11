using System;
using UnityEngine;

/// <summary>
/// One horse's own numbers, rolled once from its <see cref="HorseDefinition"/>'s ranges when it is first met and kept for
/// life (saved with a tamed horse). Plain data so it can be tested and saved as it is.
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
    /// <summary>Feeds its breaking ride takes.</summary>
    public int feedsNeeded;

    public bool IsRolled => maxHealth > 0f;

    public static HorseStats Roll(HorseDefinition definition, System.Random random)
    {
        float Between(Vector2 range) => Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());
        Vector2Int feeds = definition.FeedsNeeded;
        return new HorseStats
        {
            maxHealth = Mathf.Round(Between(definition.Health)),
            maxStamina = Mathf.Round(Between(definition.Stamina)),
            gallopSpeed = Mathf.Round(Between(definition.GallopSpeed) * 10f) / 10f,
            carryWeight = Mathf.Round(Between(definition.CarryWeight)),
            coat = definition.Coats.Length > 0 ? random.Next(definition.Coats.Length) : -1,
            feedsNeeded = random.Next(Mathf.Min(feeds.x, feeds.y), Mathf.Max(feeds.x, feeds.y) + 1),
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
