using UnityEngine;

/// <summary>How much a monster knows about a target: nothing, a suspicion worth checking, or a target it hunts.</summary>
public enum AwarenessStage
{
    Unaware,
    Suspicious,
    Alerted,
}

/// <summary>
/// One monster's awareness: a level from 0 (calm) to 1 (spotted) and the place its last glimpse or sound came from.
/// Sight fills it over time, sounds lift it to a level (never all the way, so a sound alone can't spot anyone), and it
/// drains while nothing is seen. Alerted is sticky until the monster calms down or loses track. Plain C#, owned by
/// MonsterSenses; the rules that feed it are in PerceptionRules.
/// </summary>
public sealed class Awareness
{
    public float Level { get; private set; }
    public bool IsAlerted { get; private set; }
    /// <summary>Where the last thing it noticed was (a glimpse, a sound, or where it last saw its target).</summary>
    public Vector3 Stimulus { get; private set; }
    public bool HasStimulus { get; private set; }

    public AwarenessStage StageAt(float suspiciousAt)
    {
        if (IsAlerted)
        {
            return AwarenessStage.Alerted;
        }

        return Level >= suspiciousAt ? AwarenessStage.Suspicious : AwarenessStage.Unaware;
    }

    /// <summary>Adds what it saw this time. Returns true when that fills it and it has just spotted its target.</summary>
    public bool Notice(float amount, Vector3 at)
    {
        if (amount <= 0f)
        {
            return false;
        }

        Stimulus = at;
        HasStimulus = true;
        if (IsAlerted)
        {
            return false;
        }

        Level = Mathf.Min(1f, Level + amount);
        if (Level < 1f)
        {
            return false;
        }

        IsAlerted = true;
        return true;
    }

    /// <summary>A sound: lifts the level to <paramref name="level"/> (never lowers it) and points it at the sound.</summary>
    public void Hear(float level, Vector3 at)
    {
        if (IsAlerted || level <= 0f)
        {
            return;
        }

        Level = Mathf.Max(Level, Mathf.Min(level, 0.99f));
        Stimulus = at;
        HasStimulus = true;
    }

    /// <summary>Drains while it sees nothing. Alerted monsters don't calm down this way.</summary>
    public void Decay(float amount)
    {
        if (IsAlerted || amount <= 0f)
        {
            return;
        }

        Level = Mathf.Max(0f, Level - amount);
        if (Level <= 0f)
        {
            HasStimulus = false;
        }
    }

    /// <summary>It knows exactly where its target is (spotted it, was hit by it, or was told by its pack).</summary>
    public void Alert(Vector3 at)
    {
        Level = 1f;
        IsAlerted = true;
        Stimulus = at;
        HasStimulus = true;
    }

    /// <summary>It lost its target: no longer alerted, but suspicious of where it last saw it.</summary>
    public void LoseTrack(Vector3 lastKnown, float level)
    {
        IsAlerted = false;
        Level = Mathf.Clamp(level, 0f, 0.99f);
        Stimulus = lastKnown;
        HasStimulus = true;
    }

    public void Calm()
    {
        Level = 0f;
        IsAlerted = false;
        HasStimulus = false;
    }
}
