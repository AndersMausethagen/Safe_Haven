using UnityEngine;

/// <summary>
/// How the player gathered during one raid, for the raid summary: swings by result and how angry the forest got.
/// Owned by the RaidSession; the RaidDirector records swings and the ForestAnnoyanceDriver the annoyance.
/// </summary>
public sealed class GatheringStats
{
    public int Swings { get; private set; }
    public int Criticals { get; private set; }
    public int Glancing { get; private set; }

    /// <summary>The highest forest annoyance reached, 0-1 of the wrath threshold. Negative when the raid has no annoyance.</summary>
    public float PeakAnnoyance { get; private set; } = -1f;
    public int Wraths { get; private set; }

    public bool HasAnnoyance => PeakAnnoyance >= 0f;
    public float CriticalRate => Swings > 0 ? (float)Criticals / Swings : 0f;

    public void Record(SwingResult result)
    {
        Swings++;
        if (result == SwingResult.Critical)
        {
            Criticals++;
        }
        else if (result == SwingResult.Glancing)
        {
            Glancing++;
        }
    }

    public void RecordAnnoyance(float peakFill, int wraths)
    {
        PeakAnnoyance = Mathf.Max(PeakAnnoyance, Mathf.Clamp01(peakFill));
        Wraths = Mathf.Max(Wraths, wraths);
    }
}
