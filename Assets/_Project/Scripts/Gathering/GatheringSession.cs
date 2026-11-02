using System;
using UnityEngine;

/// <summary>How well a gathering swing was timed.</summary>
public enum SwingResult
{
    /// <summary>Pressed well before the zones. Loud, wears the tool.</summary>
    Glancing = 0,
    /// <summary>Pressed in the yellow zone, or left to swing by itself: the normal yield.</summary>
    Normal = 1,
    /// <summary>Pressed in the critical zone: one extra of the main yield, and quiet.</summary>
    Critical = 2,
}

/// <summary>The yellow and critical zones of one swing's bar, both centred on the same point (0-1 along the bar).</summary>
public readonly struct SwingZones
{
    public readonly float Center;
    public readonly float NormalWidth;
    public readonly float CriticalWidth;

    public SwingZones(float center, float normalWidth, float criticalWidth)
    {
        NormalWidth = Mathf.Clamp01(normalWidth);
        CriticalWidth = Mathf.Clamp(criticalWidth, 0f, NormalWidth);
        // Keeps the whole yellow zone on the bar.
        float half = NormalWidth * 0.5f;
        Center = Mathf.Clamp(center, half, 1f - half);
    }

    public SwingResult Evaluate(float position)
    {
        float offset = Mathf.Abs(position - Center);
        if (offset <= CriticalWidth * 0.5f)
        {
            return SwingResult.Critical;
        }

        return offset <= NormalWidth * 0.5f ? SwingResult.Normal : SwingResult.Glancing;
    }
}

/// <summary>
/// The timing minigame of one gather, as plain logic: a marker rises up the bar as the tool is raised, and comes down by itself at
/// the top as a plain swing, so leaving it alone finishes the node at a steady pace with nothing lost. Pressing as it crosses the
/// sweet spot makes the swing a critical; a run of them widens the spot up to a cap, any other result resets it. The spot stays
/// put, so it can be felt as a rhythm, like an active reload. Only a press well before the zones goes wrong. Driven by
/// PlayerGathering.
/// </summary>
public sealed class GatheringSession : ITimingBar
{
    private readonly float riseSeconds;
    private readonly float sweetSpot;
    private readonly float normalWidth;
    private readonly float criticalWidth;
    private readonly float criticalPerCombo;
    private readonly int maxCombo;
    private readonly float jitter;
    private readonly System.Random random;
    private float phase;

    /// <param name="swings">Swings needed to finish the gather.</param>
    /// <param name="riseSeconds">Seconds for the marker to rise from the bottom to the top.</param>
    /// <param name="sweetSpot">Where the zones are centred (0-1 up the bar).</param>
    /// <param name="normalWidth">Width of the yellow zone (0-1 of the bar).</param>
    /// <param name="criticalWidth">Width of the critical zone before combo, tool tier included.</param>
    /// <param name="criticalPerCombo">Critical width added per consecutive critical swing.</param>
    /// <param name="maxCombo">Consecutive criticals that still widen the zone.</param>
    /// <param name="jitter">How far the zones' centre may move from the sweet spot each swing.</param>
    public GatheringSession(int swings, float riseSeconds, float sweetSpot, float normalWidth, float criticalWidth, float criticalPerCombo,
        int maxCombo, float jitter, System.Random random)
    {
        SwingsRequired = Mathf.Max(1, swings);
        this.riseSeconds = Mathf.Max(0.1f, riseSeconds);
        this.sweetSpot = sweetSpot;
        this.normalWidth = normalWidth;
        this.criticalWidth = criticalWidth;
        this.criticalPerCombo = Mathf.Max(0f, criticalPerCombo);
        this.maxCombo = Mathf.Max(0, maxCombo);
        this.jitter = Mathf.Max(0f, jitter);
        this.random = random ?? new System.Random();
        NewZones();
    }

    public int SwingsRequired { get; }
    public int SwingsDone { get; private set; }
    public int Criticals { get; private set; }
    public int Glancing { get; private set; }

    /// <summary>Critical swings in a row so far.</summary>
    public int Combo { get; private set; }

    public bool IsComplete => SwingsDone >= SwingsRequired;

    /// <summary>The marker's place on the bar, 0 (bottom) to 1 (top).</summary>
    public float Position => phase;

    public SwingZones Zones { get; private set; }

    public string Counter => $"{Mathf.Min(SwingsDone + 1, SwingsRequired)}/{SwingsRequired}{(Combo > 1 ? $"  x{Combo}" : "")}";

    /// <summary>Moves the marker. At the top of the bar, with no press, the swing comes down by itself as a normal one.</summary>
    /// <returns>True when this tick timed a swing out.</returns>
    public bool Tick(float deltaTime)
    {
        if (IsComplete || deltaTime <= 0f)
        {
            return false;
        }

        phase += deltaTime / riseSeconds;
        if (phase < 1f)
        {
            return false;
        }

        Resolve(SwingResult.Normal);
        return true;
    }

    /// <summary>The player's press: judges the marker against the zones and resolves the swing.</summary>
    public SwingResult Press()
    {
        SwingResult result = Zones.Evaluate(Position);
        Resolve(result);
        return result;
    }

    /// <summary>Counts a swing with a given result (a press, a timeout, or auto-gather) and starts the next.</summary>
    public void Resolve(SwingResult result)
    {
        if (IsComplete)
        {
            return;
        }

        SwingsDone++;
        if (result == SwingResult.Critical)
        {
            Criticals++;
            Combo++;
        }
        else
        {
            Combo = 0;
            if (result == SwingResult.Glancing)
            {
                Glancing++;
            }
        }

        phase = 0f;
        NewZones();
    }

    /// <summary>The critical width for a number of criticals in a row.</summary>
    public static float CriticalWidthFor(float baseWidth, float perCombo, int combo, int maxCombo)
    {
        return baseWidth + perCombo * Mathf.Clamp(combo, 0, Mathf.Max(0, maxCombo));
    }

    private void NewZones()
    {
        float center = sweetSpot + ((float)random.NextDouble() * 2f - 1f) * jitter;
        Zones = new SwingZones(center, normalWidth, CriticalWidthFor(criticalWidth, criticalPerCombo, Combo, maxCombo));
    }
}
