using UnityEngine;

/// <summary>
/// Decides when the next story beat may come (tested in QuestPacingTests): plain C#, owned by the QuestManager, which tells
/// it each frame whether a fight or a conversation is on. Times are passed in, so nothing here touches the clock. A fight
/// that is still going doesn't count as catching breath: a story line said in one stays a subtitle (DialogueBox).
/// </summary>
public sealed class StoryPacing
{
    private float fightEndedAt = float.NegativeInfinity;
    private float talkEndedAt = float.NegativeInfinity;
    private float questEndedAt = float.NegativeInfinity;

    public bool IsFighting { get; private set; }
    public bool IsTalking { get; private set; }

    public void Tick(float now, bool fighting, bool talking)
    {
        if (IsFighting && !fighting)
        {
            fightEndedAt = now;
        }

        if (IsTalking && !talking)
        {
            talkEndedAt = now;
        }

        IsFighting = fighting;
        IsTalking = talking;
    }

    public void QuestCompleted(float now) => questEndedAt = now;

    /// <summary>True in the quiet right after a fight, before anyone should say anything.</summary>
    public bool IsCatchingBreath(float now, StoryPacingSettings rules) => !IsFighting && now < fightEndedAt + rules.AfterFightSeconds;

    /// <summary>When a quest available from <paramref name="now"/> is due: after its own delay, or else the gap after the last quest.</summary>
    public float ReadyAt(float startDelay, float now, StoryPacingSettings rules)
    {
        return startDelay > 0f ? now + startDelay : Mathf.Max(now, questEndedAt + rules.BetweenQuestsSeconds);
    }

    /// <summary>True when a quest due at <paramref name="readyAt"/> may start now: nothing is in the way, or it has waited long enough.</summary>
    public bool CanStart(float readyAt, float now, StoryPacingSettings rules)
    {
        return now >= readyAt && (IsClear(now, rules) || now >= readyAt + rules.LongestHoldSeconds);
    }

    private bool IsClear(float now, StoryPacingSettings rules)
    {
        return !IsFighting && !IsTalking && !IsCatchingBreath(now, rules) && now >= talkEndedAt + rules.AfterDialogueSeconds;
    }
}
