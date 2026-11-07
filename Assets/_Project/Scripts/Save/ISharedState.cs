/// <summary>
/// Optional, beside <see cref="ISaveable"/>: how a component's saved state is shared in co-op, where
/// <c>HavenSync</c> keeps a friend's copy of the Haven in step with the host's. Without it, a component's whole saved
/// state is shared, and compared each time to see whether it changed.
/// </summary>
public interface ISharedState
{
    /// <summary>False for state that is each player's own (the explored map) or kept in step another way (the clock).</summary>
    bool IsShared { get; }

    /// <summary>
    /// A number that changes whenever a friend would see a change. Timers that only count down are left out, or the state
    /// would be sent again every moment.
    /// </summary>
    int SharedMark { get; }
}
