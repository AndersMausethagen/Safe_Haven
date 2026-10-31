using UnityEngine;

/// <summary>
/// Picking one lock, as plain logic (tested in LockpickingTests): a marker runs up and down a bar and each press tries to
/// set a pin. The zones are the gathering bar's: green sets it silently, yellow with a click, and anywhere else slips and
/// drops a pin already set. Every pin set narrows the zones. Enough slips and the lock gives way by force, so a chest can
/// always be opened, only not always quietly. The marker never times out: the raid's clock is the pressure. Driven by
/// PlayerLockpicking.
/// </summary>
public sealed class LockSession : ITimingBar
{
    private readonly float cycleSeconds;
    private readonly float normalWidth;
    private readonly float criticalWidth;
    private readonly float narrowingPerPin;
    private readonly float jitter;
    private readonly System.Random random;
    private float phase;

    /// <param name="pins">Pins to set.</param>
    /// <param name="slipsAllowed">Slips after which the lock gives way by force.</param>
    /// <param name="cycleSeconds">Seconds for the marker to go up and back down.</param>
    /// <param name="normalWidth">Width of the yellow zone on the first pin (0-1 of the bar).</param>
    /// <param name="criticalWidth">Width of the green zone on the first pin.</param>
    /// <param name="narrowingPerPin">Share of both widths lost per pin set.</param>
    /// <param name="jitter">How far the zones' centre may move from the middle of the bar each press.</param>
    public LockSession(int pins, int slipsAllowed, float cycleSeconds, float normalWidth, float criticalWidth, float narrowingPerPin,
        float jitter, System.Random random)
    {
        Pins = Mathf.Max(1, pins);
        SlipsAllowed = Mathf.Max(1, slipsAllowed);
        this.cycleSeconds = Mathf.Max(0.1f, cycleSeconds);
        this.normalWidth = normalWidth;
        this.criticalWidth = criticalWidth;
        this.narrowingPerPin = Mathf.Clamp01(narrowingPerPin);
        this.jitter = Mathf.Max(0f, jitter);
        this.random = random ?? new System.Random();
        NewZones();
    }

    public int Pins { get; }
    public int SlipsAllowed { get; }
    public int PinsSet { get; private set; }
    public int Slips { get; private set; }

    /// <summary>Every pin is set: it opened as a lock should.</summary>
    public bool IsPicked => PinsSet >= Pins;

    /// <summary>Too many slips: it gave way by force, loudly.</summary>
    public bool IsForced => Slips >= SlipsAllowed;

    public bool IsDone => IsPicked || IsForced;

    public float Position => phase < 0.5f ? phase * 2f : 2f - phase * 2f;

    public SwingZones Zones { get; private set; }

    public string Counter => Slips > 0 ? $"{PinsSet}/{Pins}  slips {Slips}/{SlipsAllowed}" : $"{PinsSet}/{Pins}";

    /// <summary>Moves the marker.</summary>
    public void Tick(float deltaTime)
    {
        if (!IsDone && deltaTime > 0f)
        {
            phase = Mathf.Repeat(phase + deltaTime / cycleSeconds, 1f);
        }
    }

    /// <summary>The player's press: judges the marker against the zones and resolves it.</summary>
    public SwingResult Press()
    {
        SwingResult result = Zones.Evaluate(Position);
        Resolve(result);
        return result;
    }

    /// <summary>Counts a press with a given result (a press, or automatic picking) and starts the next.</summary>
    public void Resolve(SwingResult result)
    {
        if (IsDone)
        {
            return;
        }

        if (result == SwingResult.Glancing)
        {
            Slips++;
            PinsSet = Mathf.Max(0, PinsSet - 1);
        }
        else
        {
            PinsSet++;
        }

        NewZones();
    }

    private void NewZones()
    {
        float scale = Mathf.Max(0.1f, 1f - narrowingPerPin * PinsSet);
        float center = 0.5f + ((float)random.NextDouble() * 2f - 1f) * jitter;
        Zones = new SwingZones(center, normalWidth * scale, criticalWidth * scale);
    }
}
