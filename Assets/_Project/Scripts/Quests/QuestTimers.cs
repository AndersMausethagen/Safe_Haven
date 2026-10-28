using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Time limits shown with a quest in the quest tracker ("Hold the waystones  2:31"): whoever runs a timed moment (the
/// prologue's last stand) sets when it ends and clears it after. Runtime only; nothing here is saved.
/// </summary>
public static class QuestTimers
{
    private static readonly Dictionary<QuestDefinition, float> endsAt = new Dictionary<QuestDefinition, float>();

    public static void Set(QuestDefinition quest, float secondsLeft)
    {
        if (quest != null)
        {
            endsAt[quest] = Time.time + Mathf.Max(0f, secondsLeft);
        }
    }

    public static void Clear(QuestDefinition quest)
    {
        if (quest != null)
        {
            endsAt.Remove(quest);
        }
    }

    /// <summary>Seconds left on the quest's timer, or false when it has none.</summary>
    public static bool TryGet(QuestDefinition quest, out float secondsLeft)
    {
        secondsLeft = 0f;
        if (quest == null || !endsAt.TryGetValue(quest, out float end))
        {
            return false;
        }

        secondsLeft = Mathf.Max(0f, end - Time.time);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => endsAt.Clear();
}
