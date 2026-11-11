using UnityEngine;

/// <summary>Work a tamed horse can be put to at the Haven (HorseSettings › Jobs): at a kind of station, it makes every one
/// of that kind near where it works faster, as a worker does.</summary>
[System.Serializable]
public struct HorseJob
{
    public string name;
    [TextArea] public string description;
    public CraftingStationType station;
    [Tooltip("Share faster the stations get (0.35 = 35%).")]
    [Range(0f, 1f)] public float bonus;
    [Tooltip("Metres from where it works that stations get the bonus.")]
    [Min(2f)] public float reach;
}

/// <summary>
/// Horses at work: which bonus a station gets from the horses working near it (CraftingStation.Speed reads it). A
/// station gets one horse's bonus at most, the best; a horse works at one place.
/// </summary>
public static class HorseJobs
{
    /// <summary>The bonus a working horse gives this station, 0 when none works near it.</summary>
    public static float SpeedBonusFor(CraftingStation station)
    {
        if (station == null)
        {
            return 0f;
        }

        float best = 0f;
        var jobs = HorseSettings.Current.Jobs;
        foreach (Horse horse in Horse.All)
        {
            if (horse == null || !horse.IsAlive || !horse.IsTamed || horse.Order != Horse.Orders.Work || horse.Job < 0 || horse.Job >= jobs.Count)
            {
                continue;
            }

            HorseJob job = jobs[horse.Job];
            if (job.station == station.Type && (horse.WorkAt - station.transform.position).sqrMagnitude <= job.reach * job.reach)
            {
                best = Mathf.Max(best, job.bonus);
            }
        }

        return best;
    }

    /// <summary>The nearest station of a kind within <paramref name="reach"/> metres, or null.</summary>
    public static CraftingStation NearestStation(CraftingStationType type, Vector3 at, float reach)
    {
        CraftingStation best = null;
        float bestDistance = reach * reach;
        foreach (CraftingStation station in CraftingStation.Active)
        {
            if (station == null || station.Type != type)
            {
                continue;
            }

            float distance = (station.transform.position - at).sqrMagnitude;
            if (distance <= bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }

        return best;
    }
}
