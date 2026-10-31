using UnityEngine;

/// <summary>
/// Makes gathering heard in raids: each swing (PlayerGathering.Swung) is a WorldNoise of the swing's noise radius, so
/// it wakes sleeping creatures inside it and makes idle ones suspicious enough to come and look. Crits are quiet,
/// glancing blows loud; the radii are in GatheringMinigameSettings. Lives next to the RaidDirector, which adds it, so
/// gathering is only heard in raids.
/// </summary>
[RequireComponent(typeof(RaidDirector))]
public sealed class GatheringNoise : MonoBehaviour
{
    private void OnEnable()
    {
        PlayerGathering.Swung += OnSwung;
    }

    private void OnDisable()
    {
        PlayerGathering.Swung -= OnSwung;
    }

    private static void OnSwung(GatheringSwing swing)
    {
        PlayerNoise noise = Player.Current != null ? Player.Current.Noise : null;
        if (noise != null)
        {
            noise.Make(swing.Position, swing.NoiseRadius);
        }
        else
        {
            WorldNoise.Emit(swing.Position, swing.NoiseRadius);
        }
    }

    /// <summary>Sends idle, roaming and investigating creatures within the radius to look at the position.</summary>
    public static int Investigate(Vector3 position, float radius)
    {
        float radiusSqr = radius * radius;
        int sent = 0;
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai == null || !ai.enabled)
            {
                continue;
            }

            Vector3 offset = ai.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            MonsterAI.State before = ai.CurrentState;
            ai.Investigate(position);
            if (ai.CurrentState == MonsterAI.State.Investigating && before != MonsterAI.State.Investigating)
            {
                sent++;
            }
        }

        return sent;
    }
}
