using UnityEngine;

/// <summary>
/// The Stablehand's mornings (Docs/PeopleSystemReview_2026-11-23.md §2.8): whoever holds a job that tends horses (JobDefinition › Tends
/// Horses) feeds, grooms and walks the tamed horses in the stable. Each gains that much bond, times the best one's skill speed (their
/// Horsemanship), and the days the player was away don't wear it. The work trains their Horsemanship a little for each horse.
/// Runs in the people's morning at home (PeopleDay), where the horses are.
/// </summary>
public static class Stablehands
{
    /// <summary>The best stablehand at work tends every tamed horse in a stall. Returns how many were tended.</summary>
    public static int Morning(SettlementState home)
    {
        ResidentRecord best = BestOf(home, out float amount);
        if (best == null)
        {
            return 0;
        }

        int tended = 0;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsAlive && horse.IsTamed && horse.IsStabled && horse.IsMine)
            {
                horse.Tended(amount);
                tended++;
            }
        }

        if (tended > 0)
        {
            ResidentTalents.PractiseWork(best, tended);
        }

        return tended;
    }

    /// <summary>True when a trade that tends horses is known in <paramref name="home"/> (someone could be given it).</summary>
    public static bool Known(SettlementState home)
    {
        if (home == null)
        {
            return false;
        }

        foreach (JobDefinition job in home.KnownJobs())
        {
            if (job != null && job.TendsHorses > 0f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The stablehand at work who tends best, and how much bond a horse gains from them a morning; null when nobody tends.</summary>
    public static ResidentRecord BestOf(SettlementState home, out float amount)
    {
        amount = 0f;
        ResidentRecord best = null;
        if (home == null)
        {
            return null;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            float tends = record?.Job != null && !record.IsAway && !record.WorkPaused ? record.Job.TendsHorses : 0f;
            float now = tends > 0f ? tends * ResidentTalents.StationSpeed(record) : 0f;
            if (now > amount)
            {
                amount = now;
                best = record;
            }
        }

        return best;
    }
}
