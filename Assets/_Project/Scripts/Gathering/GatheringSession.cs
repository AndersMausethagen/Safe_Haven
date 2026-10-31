using System;
using UnityEngine;

/// <summary>How well a gathering swing was timed.</summary>
public enum SwingResult
{
    /// <summary>Pressed outside the zones, or not pressed in a whole bar cycle. Loud, wears the tool.</summary>
    Glancing = 0,
    /// <summary>Pressed in the yellow zone: the normal yield.</summary>
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
/// The timing minigame of one gather, as plain logic: a marker runs up and down a bar and the player presses once per
/// swing. Each swing gets fresh zones (their centre jitters so it can't be done by rhythm alone); a run of critical swings
/// widens the critical zone up to a cap, any other result resets it. A whole up-and-down cycle without a press is a
/// glancing blow, so leaving the game alone still finishes the node, just badly. Driven by PlayerGathering.
/// </summary>
public sealed class GatheringSession : ITimingBar
{
    private readonly float cycleSeconds;
    private readonly float normalWidth;
    private readonly float criticalWidth;
    private readonly float criticalPerCombo;
    private readonly int maxCombo;
    private readonly float jitter;
    private readonly System.Random random;
    private float phase;

    /// <param name="swings">Swings needed to finish the gather.</param>
    /// <param name="cycleSeconds">Seconds for the marker to go up and back down.</param>
    /// <param name="normalWidth">Width of the yellow zone (0-1 of the bar).</param>
    /// <param name="criticalWidth">Width of the critical zone before combo, tool tier included.</param>
    /// <param name="criticalPerCombo">Critical width added per consecutive critical swing.</param>
    /// <param name="maxCombo">Consecutive criticals that still widen the zone.</param>
    /// <param name="jitter">How far the zones' centre may move from the middle of the bar each swing.</param>
    public GatheringSession(int swings, float cycleSeconds, float normalWidth, float criticalWidth, float criticalPerCombo,
        int maxCombo, float jitter, System.Random random)
    {
        SwingsRequired = Mathf.Max(1, swings);
        this.cycleSeconds = Mathf.Max(0.1f, cycleSeconds);
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
    public float Position => phase < 0.5f ? phase * 2f : 2f - phase * 2f;

    public SwingZones Zones { get; private set; }

    public string Counter => $"{Mathf.Min(SwingsDone + 1, SwingsRequired)}/{SwingsRequired}{(Combo > 1 ? $"  x{Combo}" : "")}";

    /// <summary>Moves the marker. When a whole cycle passes without a press the swing resolves as a glancing blow.</summary>
    /// <returns>True when this tick timed a swing out.</returns>
    public bool Tick(float deltaTime)
    {
        if (IsComplete || deltaTime <= 0f)
        {
            return false;
        }

        phase += deltaTime / cycleSeconds;
        if (phase < 1f)
        {
            return false;
        }

        Resolve(SwingResult.Glancing);
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
        float center = 0.5f + ((float)random.NextDouble() * 2f - 1f) * jitter;
        Zones = new SwingZones(center, normalWidth, CriticalWidthFor(criticalWidth, criticalPerCombo, Combo, maxCombo));
    }
}
