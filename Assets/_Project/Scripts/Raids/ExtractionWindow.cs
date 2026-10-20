/// <summary>Whether an extraction point can be used right now, by the raid clock.</summary>
public enum ExtractionStatus
{
    Open = 0,
    /// <summary>Opens later in the raid.</summary>
    NotYetOpen = 1,
    /// <summary>Was open earlier and has gone dark.</summary>
    Closed = 2,
}

/// <summary>
/// When an extraction point is open, by seconds left on the raid clock. Kept free of scene objects so it can be tested.
/// A threshold of 0 means "open from the start" or "never closes".
/// </summary>
public static class ExtractionWindow
{
    public static ExtractionStatus StatusAt(float opensAtSecondsLeft, float closesAtSecondsLeft, float secondsLeft)
    {
        if (closesAtSecondsLeft > 0f && secondsLeft <= closesAtSecondsLeft)
        {
            return ExtractionStatus.Closed;
        }

        if (opensAtSecondsLeft > 0f && secondsLeft > opensAtSecondsLeft)
        {
            return ExtractionStatus.NotYetOpen;
        }

        return ExtractionStatus.Open;
    }

    /// <summary>Seconds until the next change (opening or closing), or -1 when nothing more changes.</summary>
    public static float SecondsUntilChange(float opensAtSecondsLeft, float closesAtSecondsLeft, float secondsLeft)
    {
        switch (StatusAt(opensAtSecondsLeft, closesAtSecondsLeft, secondsLeft))
        {
            case ExtractionStatus.NotYetOpen:
                return secondsLeft - opensAtSecondsLeft;
            case ExtractionStatus.Open when closesAtSecondsLeft > 0f:
                return secondsLeft - closesAtSecondsLeft;
            default:
                return -1f;
        }
    }
}
