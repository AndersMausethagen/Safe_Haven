using System.Collections.Generic;

/// <summary>
/// Which call each bound summon came from, and whether killing them toppled the summoner (BoundSummons; tested in BoundSummonsTests).
/// A call topples it when every summon of that call has died within the call's Bond Within seconds. Plain C#: the summons are any
/// objects that stand for them.
/// </summary>
public sealed class SummonBondLedger
{
    private sealed class Call
    {
        public float At;
        public float Within;
        public float ToppleSeconds;
        public int Left;
    }

    private readonly Dictionary<object, Call> callOf = new Dictionary<object, Call>();

    /// <summary>How many summons are still tied to it.</summary>
    public int Bound => callOf.Count;

    /// <summary>A call at <paramref name="now"/> brought up <paramref name="summons"/>, bound for <paramref name="within"/> seconds.</summary>
    public void Called(IReadOnlyList<object> summons, float now, float within, float toppleSeconds)
    {
        if (summons == null || summons.Count == 0)
        {
            return;
        }

        var call = new Call { At = now, Within = within, ToppleSeconds = toppleSeconds, Left = 0 };
        foreach (object summon in summons)
        {
            if (summon != null && !callOf.ContainsKey(summon))
            {
                callOf[summon] = call;
                call.Left++;
            }
        }
    }

    public bool IsBound(object summon) => summon != null && callOf.ContainsKey(summon);

    /// <summary>
    /// A summon died at <paramref name="now"/>. False when it wasn't bound. <paramref name="toppleSeconds"/> is how long the summoner
    /// topples when this was the last of its call and the call was killed in time, else 0.
    /// </summary>
    public bool Died(object summon, float now, out float toppleSeconds)
    {
        toppleSeconds = 0f;
        if (summon == null || !callOf.TryGetValue(summon, out Call call))
        {
            return false;
        }

        callOf.Remove(summon);
        call.Left--;
        if (call.Left <= 0 && call.ToppleSeconds > 0f && now - call.At <= call.Within)
        {
            toppleSeconds = call.ToppleSeconds;
        }

        return true;
    }

    /// <summary>A summon gone without dying (unloaded, removed): its call can no longer be finished.</summary>
    public void Forget(object summon)
    {
        if (summon != null)
        {
            callOf.Remove(summon);
        }
    }
}
