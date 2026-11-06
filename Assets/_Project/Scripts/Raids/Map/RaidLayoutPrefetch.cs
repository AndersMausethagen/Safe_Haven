using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lays out a run ahead, while the player still stands at the portal: the raid planner rolls the seed when it opens, so the plan and its
/// height field (plain arithmetic, RaidLayoutPlanner and RaidLayoutField) can be worked out on a worker thread meanwhile. RaidMap takes
/// it for its first attempt if the seed, size and map match, and otherwise lays the run out itself. Only one run is prepared at a time;
/// asking for another lets the last go.
/// </summary>
public static class RaidLayoutPrefetch
{
    private sealed class Job
    {
        public RaidLayoutRules Source;
        public RaidLayoutRules Rules;
        public int Seed;
        public float Width;
        public float Size;
        public Task<(RaidLayoutPlan plan, RaidLayoutField field)> Work;
    }

    private static Job current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => current = null;

    /// <summary>Starts laying out this run of the raid, unless it is already under way. Main thread only.</summary>
    public static void Prepare(RaidDefinition raid, int seed, bool large)
    {
        RaidLayoutRules source = raid != null ? raid.LayoutRules : null;
        if (source == null)
        {
            return;
        }

        float width = large && raid.CanBeLarge ? raid.LargeRaid.widthScale : 1f;
        if (current != null && current.Source == source && current.Seed == seed && Mathf.Approximately(current.Width, width))
        {
            return;
        }

        Forget();
        // A larger map's rules are a copy made here, on the main thread; the worker only reads it.
        RaidLayoutRules rules = width > 1f ? source.ScaledBy(width) : source;
        float size = source.MapSize * width;
        var job = new Job { Source = source, Rules = rules, Seed = seed, Width = width, Size = size };
        job.Work = Task.Run(() =>
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, size, seed, 0);
            return (plan, RaidLayoutField.Build(plan, rules));
        });
        current = job;
    }

    /// <summary>
    /// True when this run was prepared: <paramref name="ready"/> says whether it is finished (wait a frame and ask again if not). The plan and
    /// field are handed over once, when ready; a failed or mismatched one is dropped.
    /// </summary>
    public static bool TryTake(RaidLayoutRules source, int seed, float width, float size, out bool ready, out RaidLayoutPlan plan, out RaidLayoutField field)
    {
        plan = null;
        field = null;
        ready = false;
        Job job = current;
        if (job == null || job.Source != source || job.Seed != seed || !Mathf.Approximately(job.Width, width) || !Mathf.Approximately(job.Size, size))
        {
            return false;
        }

        if (!job.Work.IsCompleted)
        {
            return true;
        }

        ready = true;
        current = null;
        if (job.Work.IsFaulted || job.Work.IsCanceled)
        {
            Debug.LogWarning($"Laying out the run ahead failed, so it is laid out on loading: {job.Work.Exception?.InnerException?.Message}");
            return false;
        }

        (plan, field) = job.Work.Result;
        return true;
    }

    /// <summary>The prepared plan of this run if it is finished, left in place for RaidMap (null otherwise).</summary>
    public static RaidLayoutPlan Peek(RaidDefinition raid, int seed)
    {
        Job job = current;
        return job != null && raid != null && job.Source == raid.LayoutRules && job.Seed == seed && job.Work.IsCompleted && !job.Work.IsFaulted
            ? job.Work.Result.plan : null;
    }

    /// <summary>Lets the run under preparation go (the rules copy it made goes once it has finished).</summary>
    public static void Forget()
    {
        Job job = current;
        current = null;
        if (job != null && job.Rules != job.Source)
        {
            // Still being read by the worker: left for the collector rather than destroyed under it.
            if (job.Work.IsCompleted && Application.isPlaying)
            {
                Object.Destroy(job.Rules);
            }
        }
    }
}
