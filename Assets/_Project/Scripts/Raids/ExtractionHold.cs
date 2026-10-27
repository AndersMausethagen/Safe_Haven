/// <summary>What a lit extraction point is doing right now.</summary>
public enum ExtractionHoldState
{
    /// <summary>Not lit yet.</summary>
    Unlit,
    /// <summary>The player stands in the circle and nothing hostile does: the way home grows.</summary>
    Holding,
    /// <summary>The player has left the circle: progress waits (it is kept).</summary>
    Waiting,
    /// <summary>A hostile creature is inside the circle: progress waits until it is driven out or killed.</summary>
    Contested,
    /// <summary>Held long enough: the player leaves.</summary>
    Done,
}

/// <summary>
/// The rules of holding an extraction point (GDD §8.1): once lit, it fills only while the player stands inside its
/// circle and no hostile creature does. Leaving or a contest pauses it, never resets it. Plain C#, owned by
/// ExtractionPoint (which decides who is inside); tested in ExtractionHoldTests.
/// </summary>
public sealed class ExtractionHold
{
    public bool IsLit { get; private set; }
    public float Duration { get; private set; }
    /// <summary>Seconds held so far.</summary>
    public float Held { get; private set; }
    public ExtractionHoldState State { get; private set; } = ExtractionHoldState.Unlit;

    public float Fraction => Duration > 0f ? Held / Duration : 0f;
    public float Remaining => System.Math.Max(0f, Duration - Held);

    /// <summary>Lights it: from now on it can be held for <paramref name="duration"/> seconds to leave.</summary>
    public void Light(float duration)
    {
        IsLit = true;
        Duration = System.Math.Max(0.01f, duration);
        Held = 0f;
        State = ExtractionHoldState.Waiting;
    }

    /// <summary>The way went dark (its window closed): everything held is lost.</summary>
    public void Douse()
    {
        IsLit = false;
        Held = 0f;
        State = ExtractionHoldState.Unlit;
    }

    /// <summary>
    /// Advances by <paramref name="deltaTime"/> seconds and returns the new state. Only Holding fills it; reaching the
    /// duration returns Done exactly once and stays Done.
    /// </summary>
    public ExtractionHoldState Tick(float deltaTime, bool playerInside, bool contested)
    {
        if (!IsLit || State == ExtractionHoldState.Done)
        {
            return State;
        }

        if (!playerInside)
        {
            State = ExtractionHoldState.Waiting;
            return State;
        }

        if (contested)
        {
            State = ExtractionHoldState.Contested;
            return State;
        }

        Held = System.Math.Min(Duration, Held + System.Math.Max(0f, deltaTime));
        State = Held >= Duration ? ExtractionHoldState.Done : ExtractionHoldState.Holding;
        return State;
    }
}
