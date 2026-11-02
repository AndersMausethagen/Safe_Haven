using UnityEngine;

/// <summary>
/// What one monster remembers of being backstabbed: each one it took or saw makes it likelier to see the next coming, and it forgets
/// them one at a time. Plain C#, owned by a MonsterAI; the numbers are in CombatTechniqueSettings.
/// </summary>
public sealed class BackstabMemory
{
    private int remembered;
    private float lastLearnedAt;

    /// <summary>How many it still remembers at <paramref name="now"/>.</summary>
    public int Remembered(float now, float forgotAfter)
    {
        if (remembered <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, remembered - Mathf.FloorToInt((now - lastLearnedAt) / Mathf.Max(0.01f, forgotAfter)));
    }

    /// <summary>It has taken or seen one more.</summary>
    public void Learn(float now, float forgotAfter)
    {
        remembered = Remembered(now, forgotAfter) + 1;
        lastLearnedAt = now;
    }

    /// <summary>The chance it sees the next one coming.</summary>
    public static float ReadChance(int remembered, float perBackstab, float most) => Mathf.Min(most, Mathf.Max(0, remembered) * perBackstab);
}
