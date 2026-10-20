using UnityEngine;

/// <summary>
/// Makes gathering heard in raids: each swing (PlayerGathering.Swung) wakes sleeping creatures within its noise radius
/// and sends idle creatures inside it to look at where the sound came from. Crits are quiet, glancing blows loud; the
/// radii are in GatheringMinigameSettings. Lives next to the RaidDirector, which adds it, so noise only exists in raids.
/// </summary>
[RequireComponent(typeof(RaidDirector))]
public sealed class GatheringNoise : MonoBehaviour
{
    private CreatureSleep sleep;

    private void OnEnable()
    {
        PlayerGathering.Swung += OnSwung;
    }

    private void OnDisable()
    {
        PlayerGathering.Swung -= OnSwung;
    }

    private void OnSwung(GatheringSwing swing)
    {
        if (swing.NoiseRadius <= 0f)
        {
            return;
        }

        if (sleep == null)
        {
            sleep = GetComponent<CreatureSleep>();
        }

        if (sleep != null)
        {
            sleep.WakeWithin(swing.Position, swing.NoiseRadius);
        }

        Investigate(swing.Position, swing.NoiseRadius);
    }

    /// <summary>Sends idle creatures within the radius to the position.</summary>
    public static int Investigate(Vector3 position, float radius)
    {
        float radiusSqr = radius * radius;
        int sent = 0;
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai == null || !ai.enabled || ai.CurrentState != MonsterAI.State.Idle)
            {
                continue;
            }

            Vector3 offset = ai.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr)
            {
                ai.Roam(position);
                sent++;
            }
        }

        return sent;
    }
}
