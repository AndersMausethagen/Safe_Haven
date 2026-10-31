/// <summary>What TimingBarView draws: a marker running along a bar with its zones. Gathering and lockpicking are both one.</summary>
public interface ITimingBar
{
    /// <summary>The marker's place on the bar, 0 (bottom) to 1 (top).</summary>
    float Position { get; }

    SwingZones Zones { get; }

    /// <summary>How far along it is, shown under the bar.</summary>
    string Counter { get; }
}
