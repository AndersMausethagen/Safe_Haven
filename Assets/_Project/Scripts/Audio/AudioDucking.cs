using UnityEngine;

/// <summary>
/// The arithmetic of making room in the mix: while someone talks or a big moment is heard, music and background sounds dip and come back.
/// How far it has dipped is one level from 0 (not at all) to 1 (fully), moved at its own speed each way. A quick dip and a slow return keep
/// the music from audibly pumping. AudioManager owns the level; Audio Settings › Making Room has the numbers.
/// </summary>
public static class AudioDucking
{
    /// <summary>The level after a step: towards 1 while something wants the room, back towards 0 once nothing does.</summary>
    public static float Step(float level, bool wanted, float dipSeconds, float returnSeconds, float deltaTime)
    {
        float seconds = wanted ? dipSeconds : returnSeconds;
        float step = seconds > 0f ? deltaTime / seconds : 1f;
        return Mathf.MoveTowards(level, wanted ? 1f : 0f, step);
    }

    /// <summary>What a volume is multiplied by at this level, for something that dips as far as <paramref name="dippedTo"/>.</summary>
    public static float Scale(float level, float dippedTo) => Mathf.Lerp(1f, dippedTo, level);
}
