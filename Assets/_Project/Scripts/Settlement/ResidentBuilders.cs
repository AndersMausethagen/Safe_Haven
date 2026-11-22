using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The Carpenter and the Mason finish what the player left as scaffolds while the player raids (the user, 2026-11-15:
/// "yes, Tor and Hild finish scaffolds while the player is on a raid"; DQB2's residents and Sons of the Forest's Kelvin).
/// Away from home, SettlementSystem saves up builder-seconds (<see cref="WorkAway"/>: each second times the builders at
/// home then); back home, this spends them on the paid-for scaffolds standing there, oldest first: builder-seconds ×
/// HomeLifeSettings › Builder Work Per Second of hammering. Only builders at home count (review 2026-11-16): not one who
/// went along on the raid, nor one whose work the player paused. The player can still hammer anything themselves. On the
/// portal, added by HomePortal; only in the game that keeps the Haven.
/// </summary>
[DisallowMultipleComponent]
public sealed class ResidentBuilders : MonoBehaviour
{
    // Who did the work while the player was away, for the message home. Not saved: after a load the message names the
    // builders at home now.
    private static readonly List<ResidentDefinition> worked = new List<ResidentDefinition>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => worked.Clear();

    /// <summary>Forgets who built while the player was away in the game being played (a new game, or a load: GameSession): a raid
    /// left by the menu named the old game's builders in the next game's message home.</summary>
    public static void ForgetGame() => worked.Clear();

    /// <summary>
    /// How many who live here build (their job is one of HomeLifeSettings › Builders) and are at home to do it: not
    /// <paramref name="away"/> (who went along on the raid), nor anyone whose work is paused.
    /// </summary>
    public static int Count(SettlementState settlement, ResidentDefinition away = null)
    {
        int count = 0;
        foreach (ResidentRecord record in settlement.Residents)
        {
            count += Builds(record, away) ? 1 : 0;
        }

        return count;
    }

    /// <summary>True when this resident builds while the player is away: a builder's job, at home, not paused.</summary>
    public static bool Builds(ResidentRecord record, ResidentDefinition away = null) =>
        record != null && record.Job != null && !record.WorkPaused && record.Resident != null && record.Resident != away
        && Contains(HomeLifeSettings.Current.Builders, record.Job);

    /// <summary>
    /// True when this resident lends a hand at scaffolds (the user, 2026-11-22: "NPC's in base should help build structures even
    /// before we unlock Keeper's view"): anyone at home and not paused who isn't a builder, at HomeLifeSettings › Helper Share of a
    /// builder's pace, and only while their own work has nothing for them (ResidentLife at home; <see cref="WorkAway"/> away).
    /// </summary>
    public static bool Helps(ResidentRecord record, ResidentDefinition away = null) =>
        record != null && !record.WorkPaused && record.Resident != null && record.Resident != away
        && HomeLifeSettings.Current.HelperShare > 0f && !Builds(record, away);

    /// <summary>How much of a builder's pace this resident hammers at: 1 for a builder, Helper Share for anyone else who helps.</summary>
    public static float ShareOf(ResidentRecord record, ResidentDefinition away = null) =>
        Builds(record, away) ? 1f : Helps(record, away) ? HomeLifeSettings.Current.HelperShare : 0f;

    // Away, a helper helps only with nothing of their own to do: an idle bench (or a stuck one, as at home) and no ring of theirs drawn.
    private static bool FreeToHelp(SettlementState settlement, ResidentRecord record)
    {
        StationQueue bench = WorkerProduction.QueueOf(settlement, record);
        if (bench != null && !bench.IsIdle && !bench.Blocked)
        {
            return false;
        }

        if (RingWork.KindFor(record, WorkerSettings.Current, out RingKind kind))
        {
            foreach (RingOrder ring in settlement.Rings)
            {
                if (ring.Kind == kind)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The names of those who build at home now, for the message home.</summary>
    public static string Names(SettlementState settlement, ResidentDefinition away = null)
    {
        var names = new List<string>();
        foreach (ResidentRecord record in settlement.Residents)
        {
            if (Builds(record, away))
            {
                names.Add(record.Resident.DisplayName);
            }
        }

        return Join(names);
    }

    private static string Join(List<string> names) =>
        names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];

    /// <summary>
    /// The player has been away <paramref name="seconds"/> more (SettlementSystem, in a raid): the builders at home then put
    /// in that much each, and anyone else free to help their share (<see cref="Helps"/>), and are remembered for the message home.
    /// False when nobody could.
    /// </summary>
    public static bool WorkAway(SettlementState settlement, float seconds, ResidentDefinition away)
    {
        float hands = 0f;
        foreach (ResidentRecord record in settlement.Residents)
        {
            // Out of the Haven (a party, an outpost, a foray: ResidentsOut), nobody builds at home.
            float share = ResidentsOut.Now(settlement, record, away) ? 0f
                : Builds(record, away) ? 1f : Helps(record, away) && FreeToHelp(settlement, record) ? HomeLifeSettings.Current.HelperShare : 0f;
            if (share > 0f)
            {
                hands += share;
                if (!worked.Contains(record.Resident))
                {
                    worked.Add(record.Resident);
                }
            }
        }

        if (hands <= 0f || seconds <= 0f)
        {
            return false;
        }

        settlement.AddBuilderSeconds(seconds * hands);
        return true;
    }

    /// <summary>True when a paid-for scaffold stands at home (work waiting for builders).</summary>
    public static bool AnyScaffold()
    {
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building.TryGetComponent(out Construction construction) && construction.IsBuilding && construction.Paid)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// How the work is shared: <paramref name="work"/> seconds of hammering over scaffolds still needing
    /// <paramref name="remaining"/> seconds each, in order, each finished before the next is begun. Pure, for the tests.
    /// </summary>
    public static float[] Spread(float work, IReadOnlyList<float> remaining)
    {
        var given = new float[remaining.Count];
        for (int i = 0; i < remaining.Count && work > 0f; i++)
        {
            given[i] = Mathf.Min(work, Mathf.Max(0f, remaining[i]));
            work -= given[i];
        }

        return given;
    }

    private IEnumerator Start()
    {
        // Saved work is put back in every building's Start: wait until it is.
        yield return null;
        yield return null;
        if (HavenVisit.IsVisiting)
        {
            yield break;
        }

        // Only off a visit (above), where one's own Haven is the one here.
        SettlementState settlement = GameSession.Home;
        // Builder-seconds: each second away already times the builders who were at home then (WorkAway).
        float seconds = settlement.TakeBuilderSeconds();
        var who = new List<string>();
        foreach (ResidentDefinition one in worked)
        {
            if (one != null && HavenPeople.RecordOf(one.Id) != null)
            {
                who.Add(one.DisplayName);
            }
        }

        worked.Clear();
        string names = who.Count > 0 ? Join(who) : Names(settlement);
        if (seconds <= 0f || names.Length == 0)
        {
            yield break;
        }

        var sites = new List<Construction>();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building.TryGetComponent(out Construction construction) && construction.IsBuilding && construction.Paid)
            {
                sites.Add(construction);
            }
        }

        // Oldest first (save ids are stable), so the same work goes to the same place on every machine.
        sites.Sort((a, b) => string.CompareOrdinal(a.GetComponent<BuildingObject>().Id ?? a.name, b.GetComponent<BuildingObject>().Id ?? b.name));
        var remaining = new List<float>();
        foreach (Construction site in sites)
        {
            remaining.Add(site.WorkNeeded - site.Work);
        }

        float[] given = Spread(seconds * HomeLifeSettings.Current.BuilderWorkPerSecond, remaining);
        var finished = new List<string>();
        var raised = new List<string>();
        for (int i = 0; i < sites.Count; i++)
        {
            if (given[i] <= 0f)
            {
                continue;
            }

            string name = sites[i].Definition.DisplayName;
            sites[i].AddWork(given[i]);
            (sites[i].IsBuilding ? raised : finished).Add(name);
        }

        if (finished.Count == 0 && raised.Count == 0)
        {
            yield break;
        }

        var said = new StringBuilder($"While you were away {names} worked on the scaffolds");
        if (finished.Count > 0)
        {
            said.Append($": the {string.Join(", the ", finished)} {(finished.Count == 1 ? "stands" : "stand")}");
        }

        if (raised.Count > 0)
        {
            said.Append(finished.Count > 0 ? ", and " : ": ").Append($"the {string.Join(", the ", raised)} rose further");
        }

        GameMessages.Post(said.Append('.').ToString(), MessageKind.Gain);
    }

    private static bool Contains(IReadOnlyList<JobDefinition> jobs, JobDefinition job)
    {
        foreach (JobDefinition one in jobs)
        {
            if (one == job)
            {
                return true;
            }
        }

        return false;
    }
}
