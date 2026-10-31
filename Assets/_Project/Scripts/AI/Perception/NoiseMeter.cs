using UnityEngine;

/// <summary>How loud the player is, in words for the HUD.</summary>
public enum NoiseBand
{
    Silent,
    Quiet,
    Audible,
    Loud,
    Deafening,
}

/// <summary>
/// The arithmetic of the player's noise (tested in PerceptionTests), plain C# owned by PlayerNoise. Every sound adds a
/// share of itself to a build-up that the next sounds carry on top, so a run of loud sounds reaches further and further
/// while one blow doesn't; stopping lets it fade. <see cref="Level"/> is what the HUD shows: how far the last sound
/// carried, held for a moment and then falling. Numbers are in PerceptionSettings.
/// </summary>
public sealed class NoiseMeter
{
    private float buildUp;
    private float holdLeft;

    /// <summary>How many metres the player can be heard right now.</summary>
    public float Level { get; private set; }

    /// <summary>A sound of <paramref name="radius"/> metres. Returns how far it carries with the build-up so far.</summary>
    public float Add(float radius, PerceptionSettings rules)
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        float carried = radius + buildUp;
        buildUp = Mathf.Min(rules.NoiseBuildMax, buildUp + radius * rules.NoiseBuildShare);
        if (carried >= Level)
        {
            Level = carried;
            holdLeft = rules.NoiseHoldSeconds;
        }

        return carried;
    }

    public void Tick(float deltaTime, PerceptionSettings rules)
    {
        buildUp = Mathf.MoveTowards(buildUp, 0f, rules.NoiseBuildDecay * deltaTime);
        holdLeft -= deltaTime;
        if (holdLeft <= 0f)
        {
            Level = Mathf.MoveTowards(Level, 0f, rules.NoiseFallPerSecond * deltaTime);
        }
    }

    /// <summary>The word for a level, by Noise Bands.</summary>
    public static NoiseBand BandOf(float level, PerceptionSettings rules)
    {
        Vector3 bands = rules.NoiseBands;
        if (level <= 0.01f)
        {
            return NoiseBand.Silent;
        }

        return level < bands.x ? NoiseBand.Quiet : level < bands.y ? NoiseBand.Audible : level < bands.z ? NoiseBand.Loud : NoiseBand.Deafening;
    }
}
