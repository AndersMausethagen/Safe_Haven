using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Time limits shown with a quest in the quest tracker ("Hold the waystones  2:31"): whoever runs a timed moment (the
/// prologue's last stand) sets when it ends and clears it after. Runtime only; nothing here is saved.
/// </summary>
public static class QuestTimers
{
    private readonly struct Clock
    {
        public readonly float End;
        public readonly float Duration;

        public Clock(float duration)
        {
            Duration = Mathf.Max(0f, duration);
            End = Time.time + Duration;
        }
    }

    private static readonly Dictionary<QuestDefinition, Clock> clocks = new Dictionary<QuestDefinition, Clock>();

    public static event Action Changed;

    public static void Set(QuestDefinition quest, float secondsLeft)
    {
        if (quest != null)
        {
            clocks[quest] = new Clock(secondsLeft);
            Changed?.Invoke();
        }
    }

    public static void Clear(QuestDefinition quest)
    {
        if (quest != null && clocks.Remove(quest))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Seconds left on the quest's timer, or false when it has none.</summary>
    public static bool TryGet(QuestDefinition quest, out float secondsLeft)
    {
        secondsLeft = 0f;
        if (quest == null || !clocks.TryGetValue(quest, out Clock clock))
        {
            return false;
        }

        secondsLeft = Mathf.Max(0f, clock.End - Time.time);
        return true;
    }

    /// <summary>The soonest active quest timer authored for the main countdown HUD.</summary>
    public static bool TryGetHudTimer(out QuestDefinition quest, out float secondsLeft, out float duration)
    {
        quest = null;
        secondsLeft = duration = 0f;
        float earliest = float.PositiveInfinity;
        foreach (var pair in clocks)
        {
            if (pair.Key == null || !pair.Key.ShowTimerInHud || GameSession.Quests.StatusOf(pair.Key) != QuestStatus.Active)
                continue;
            if (pair.Value.End > earliest || pair.Value.End == earliest && quest != null &&
                string.CompareOrdinal(pair.Key.Id, quest.Id) >= 0) continue;
            quest = pair.Key;
            earliest = pair.Value.End;
            secondsLeft = Mathf.Max(0f, earliest - Time.time);
            duration = pair.Value.Duration;
        }
        return quest != null;
    }

    /// <summary>Forgets the clocks of the game being played (a new game, or a load: GameSession): a game left in the middle of a
    /// timed moment kept its clock into the next one.</summary>
    public static void ForgetGame()
    {
        if (clocks.Count > 0)
        {
            clocks.Clear();
            Changed?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        clocks.Clear();
        Changed = null;
    }
}
