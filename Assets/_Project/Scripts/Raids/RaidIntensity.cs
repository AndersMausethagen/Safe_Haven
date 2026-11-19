using UnityEngine;

/// <summary>
/// A raid's pacing for its roamers (Docs/EnemyAIReview_2026-11-19.md §9; after Warframe's and Left 4 Dead's directors): an intensity score
/// that rises with what the player takes (100 × the share of their health each blow takes), with each kill (+8) and with every creature
/// hunting the player's side (+2 a second each, at most +10), and falls 8 a second after 4 s with none of that. At 100 it peaks and holds
/// 6 s; then it fades, sending no roamers until it is below 25; then a breather of 15-30 s ("The woods go quiet"), ended early by 40 m of
/// travel; then it builds again. Only the roamers are held: events, alarms, stage spawns and the caps are untouched. Plain C#, the
/// RoamingSpawner's (the host's alone in co-op, counting every player's fight).
/// </summary>
public sealed class RaidIntensity
{
    public enum Phase
    {
        Build,
        Peak,
        Fade,
        Relax,
    }

    public const float PeakAt = 100f;
    public const float FadeBelow = 25f;
    private const float PeakSeconds = 6f;
    private const float QuietAfter = 4f;
    private const float FallPerSecond = 8f;
    private const float RelaxTravel = 40f;

    private float quietFor;
    private float phaseUntil;
    private Vector3 relaxFrom;

    public float Score { get; private set; }
    public Phase Now { get; private set; } = Phase.Build;

    /// <summary>Roamers are sent only while it builds.</summary>
    public bool AllowsRoamers => Now == Phase.Build;

    /// <summary>A blow on the player that took <paramref name="healthShare"/> of their health.</summary>
    public void Hurt(float healthShare)
    {
        Score += 100f * Mathf.Max(0f, healthShare);
        quietFor = 0f;
    }

    public void Kill()
    {
        Score += 8f;
        quietFor = 0f;
    }

    /// <summary>Moves the pacing on by <paramref name="seconds"/>, with <paramref name="hunting"/> creatures after the player's side.</summary>
    public Phase Tick(float seconds, int hunting, Vector3 player, float now, System.Random random)
    {
        if (hunting > 0)
        {
            Score += Mathf.Min(10f, 2f * hunting) * seconds;
            quietFor = 0f;
        }
        else
        {
            quietFor += seconds;
        }

        if (quietFor > QuietAfter)
        {
            Score = Mathf.Max(0f, Score - FallPerSecond * seconds);
        }

        switch (Now)
        {
            case Phase.Build:
                if (Score >= PeakAt)
                {
                    Now = Phase.Peak;
                    phaseUntil = now + PeakSeconds;
                }

                break;
            case Phase.Peak:
                if (now >= phaseUntil)
                {
                    Now = Phase.Fade;
                }

                break;
            case Phase.Fade:
                if (Score < FadeBelow)
                {
                    Now = Phase.Relax;
                    phaseUntil = now + 15f + (float)(random != null ? random.NextDouble() : 0.5) * 15f;
                    relaxFrom = player;
                }

                break;
            default:
                Vector3 moved = player - relaxFrom;
                moved.y = 0f;
                if (now >= phaseUntil || moved.magnitude > RelaxTravel)
                {
                    Now = Phase.Build;
                }

                break;
        }

        return Now;
    }
}
