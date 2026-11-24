using System;
using System.Globalization;

/// <summary>
/// Frame times over a stretch of play, kept as a histogram so a minute at any frame rate costs nothing to hold: the median, 95th and
/// 99th percentile (to a quarter millisecond under <see cref="TopMs"/>), the worst frame, and the hitches over <see cref="HitchMs"/>.
/// PlaytestLogger writes one Perf row a minute from it (Docs/QualityLevers_2026-11-24.md §2: nothing said how the game ran on a
/// playtester's machine). Plain C#, tested in FrameTallyTests.
/// </summary>
public sealed class FrameTally
{
    /// <summary>A frame at least this long is a hitch: three frames missed at 60 fps, felt as a stutter.</summary>
    public const float HitchMs = 50f;
    /// <summary>Frames longer than this fall in the last bin; their percentile reads as the worst frame.</summary>
    public const float TopMs = 250f;
    private const float BinMs = 0.25f;

    private readonly int[] bins = new int[(int)(TopMs / BinMs) + 1];
    private double cpuSum;
    private double gpuSum;
    private int timed;

    public int Frames { get; private set; }
    public int Hitches { get; private set; }
    public float WorstMs { get; private set; }

    /// <summary>One frame's length in milliseconds. Negative or not-a-number frames are ignored.</summary>
    public void Add(float ms)
    {
        if (!(ms >= 0f))
        {
            return;
        }

        int bin = Math.Min(bins.Length - 1, (int)(ms / BinMs));
        bins[bin]++;
        Frames++;
        if (ms >= HitchMs)
        {
            Hitches++;
        }

        if (ms > WorstMs)
        {
            WorstMs = ms;
        }
    }

    /// <summary>The CPU main thread's and GPU's time of a frame, where the platform reports them (FrameTimingManager).</summary>
    public void AddSplit(double cpuMs, double gpuMs)
    {
        // A reading past a second is the timer's garbage, not a frame (the first Perf row of a Play session read "gpu 8600847833.7").
        if (cpuMs > 0d && gpuMs > 0d && cpuMs < 1000d && gpuMs < 1000d)
        {
            cpuSum += cpuMs;
            gpuSum += gpuMs;
            timed++;
        }
    }

    /// <summary>The frame time below which <paramref name="fraction"/> of the frames fall (0.5 the median); 0 with no frames.</summary>
    public float Percentile(float fraction)
    {
        if (Frames == 0)
        {
            return 0f;
        }

        // The rank of the frame asked for, counted from 1: the median of 4 frames is the 2nd. In doubles, less a hair: 0.99f * 100 is
        // 99.0000005 in floats, which rounded up to the 100th frame.
        int rank = Math.Max(1, (int)Math.Ceiling((double)fraction * Frames - 1e-4));
        int seen = 0;
        for (int i = 0; i < bins.Length; i++)
        {
            seen += bins[i];
            if (seen >= rank)
            {
                // The bin's upper edge, never past the worst frame actually seen.
                return i == bins.Length - 1 ? WorstMs : Math.Min(WorstMs, (i + 1) * BinMs);
            }
        }

        return WorstMs;
    }

    /// <summary>The Perf row's detail: "p50 16.75 p95 17.00 p99 22.25 worst 190.3 hitches 3 frames 3600", then the CPU/GPU split.</summary>
    public string Detail()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        string row = string.Format(c, "p50 {0:0.00} p95 {1:0.00} p99 {2:0.00} worst {3:0.0} hitches {4} frames {5}",
            Percentile(0.5f), Percentile(0.95f), Percentile(0.99f), WorstMs, Hitches, Frames);
        return timed > 0 ? row + string.Format(c, " cpu {0:0.0} gpu {1:0.0}", cpuSum / timed, gpuSum / timed) : row;
    }

    public void Clear()
    {
        Array.Clear(bins, 0, bins.Length);
        Frames = 0;
        Hitches = 0;
        WorstMs = 0f;
        cpuSum = 0d;
        gpuSum = 0d;
        timed = 0;
    }
}
