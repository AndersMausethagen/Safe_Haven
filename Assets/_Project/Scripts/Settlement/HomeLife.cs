using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's homes as the game sees them (Docs/BuildingSystemResearch.md §3.4.2, the user's answers of 2026-11-15): each
/// house's quality read from what stands round it (HomeQuality, kept until something is built, moved or changed), and what
/// good homes give, each kind capped once: those who live in one work faster at their own station (and more when it has
/// what they like), the best home adds to Well Rested comfort and to renown. Comforts are put in a house from a resident's
/// window, paid for and made where the Haven is kept (HavenDecisions). Reads the loaded Haven; worth nothing in a raid.
/// </summary>
public static class HomeLife
{
    private static readonly Dictionary<BuildingObject, HomeQuality.Report> reports = new Dictionary<BuildingObject, HomeQuality.Report>();
    private static readonly HomeQuality.Input input = new HomeQuality.Input();
    private static bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        reports.Clear();
        listening = false;
    }

    /// <summary>Forgets every house's quality: something round them changed.</summary>
    public static void Forget() => reports.Clear();

    private static void Listen()
    {
        if (listening)
        {
            return;
        }

        // Play sessions don't reload scripts here: off first, so a second session doesn't listen twice.
        listening = true;
        BuildingObject.ActiveChanged -= Forget;
        Construction.Finished -= OnFinished;
        HavenHomes.Changed -= Forget;
        BuildingObject.ActiveChanged += Forget;
        Construction.Finished += OnFinished;
        HavenHomes.Changed += Forget;
    }

    private static void OnFinished(Construction _) => Forget();

    /// <summary>How good this house is, and why. Null for a building that isn't a house.</summary>
    public static HomeQuality.Report Of(BuildingObject house)
    {
        if (house == null || house.Beds <= 0)
        {
            return null;
        }

        Listen();
        if (reports.TryGetValue(house, out HomeQuality.Report known))
        {
            return known;
        }

        input.Clear();
        Vector3 door = house.Door(out _);
        Fill(door, house);
        input.Tier = house.TryGetComponent(out Construction construction) ? Mathf.Max(1, construction.Level) : 1;
        input.Comforts.AddRange(GameSession.Here.ComfortsIn(house.Id));
        foreach (ResidentRecord record in HavenHomes.LivingIn(house))
        {
            BuildingObject work = HavenHomes.WorkplaceOf(record, door);
            if (work != null)
            {
                input.Work.Add(Flat(work.transform.position));
            }
        }

        Yards.ForHome(house, out input.YardPoints, out input.YardName);
        HomeQuality.Report report = HomeQuality.Rate(input, HomeLifeSettings.Current);
        reports[house] = report;
        return report;
    }

    /// <summary>
    /// What a house standing with its door at <paramref name="door"/> would be like (the build ghost's readout): its
    /// surroundings, near the work of anyone who works within reach. Works it out afresh each time: call it when the spot moves.
    /// </summary>
    public static HomeQuality.Report Imagine(Vector3 door, List<Vector2> work)
    {
        input.Clear();
        Fill(door, null);
        if (work != null)
        {
            input.Work.AddRange(work);
        }

        return HomeQuality.Rate(input, HomeLifeSettings.Current);
    }

    // What stands round a door: buildings, lights and paved paths within the farthest feature's reach.
    private static void Fill(Vector3 door, BuildingObject except)
    {
        input.Door = Flat(door);
        float reach = 0f;
        foreach (HomeLifeSettings.Feature feature in HomeLifeSettings.Current.Features)
        {
            reach = Mathf.Max(reach, feature.within);
        }

        reach += 2f;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building == except || building.Definition == null || !building.IsStanding)
            {
                continue;
            }

            Vector2 at = Flat(building.transform.position);
            if ((at - input.Door).sqrMagnitude > reach * reach)
            {
                continue;
            }

            input.Buildings.Add(new HomeQuality.Around(building.Definition, at));
        }

        foreach (VisibilitySource light in VisibilitySource.Active)
        {
            // Which building a light belongs to is looked up once (BuildHere.LampOf): the ghost's readout asks this often.
            if (light != null && light.IsLight && BuildHere.LampOf(light) is BuildingObject lamp && lamp != except && lamp.IsStanding)
            {
                input.Lights.Add((Flat(light.transform.position), light.Radius));
            }
        }

        foreach (PathPiece piece in PathPiece.All)
        {
            if (piece.Paved)
            {
                piece.Ends(out Vector2 a, out Vector2 b);
                input.Paved.Add((a, b));
            }
        }
    }

    // ----- What good homes give -----

    /// <summary>
    /// How much faster this resident works at their own station for their home: its level's speed, and more when it has
    /// what they like. 0 for anyone without a house.
    /// </summary>
    public static float WorkBonus(ResidentRecord record)
    {
        // What they ate this morning (ResidentMeals) and a foray's bruises (Forays) count with or without a home.
        float bruised = record != null && record.Bruised ? -ForaySettings.Current.BruisedWork : 0f;
        return HomeBonus(record) + ResidentMeals.WorkBonus(record) + bruised;
    }

    /// <summary>What their home alone adds to their work: its level, and being content in it (the window's "faster for it").</summary>
    public static float HomeBonus(ResidentRecord record)
    {
        HomeQuality.Report report = Of(HavenHomes.HomeOf(record));
        return report == null ? 0f : report.Level.workSpeed + (IsContent(record, report) ? HomeLifeSettings.Current.ContentWorkSpeed : 0f);
    }

    /// <summary>True when their home has what they like (HomeLifeSettings › Likings).</summary>
    public static bool IsContent(ResidentRecord record, HomeQuality.Report report = null)
    {
        HomeLifeSettings.Liking liking = record != null ? HomeLifeSettings.Current.LikingOf(record.Resident) : null;
        report ??= Of(HavenHomes.HomeOf(record));
        return liking != null && report != null && report.Has(liking.likes);
    }

    /// <summary>The best home standing (once, whatever the number of houses), or null when none.</summary>
    public static HomeLifeSettings.Level Best(out BuildingObject house)
    {
        house = null;
        HomeLifeSettings.Level best = null;
        int quality = -1;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            HomeQuality.Report report = Of(building);
            if (report != null && report.Quality > quality)
            {
                quality = report.Quality;
                best = report.Level;
                house = building;
            }
        }

        return best;
    }

    /// <summary>Comfort for Well Rested from homes: the best home's, once.</summary>
    public static int Comfort => Best(out _)?.comfort ?? 0;

    /// <summary>Renown from homes: the best home's, once.</summary>
    public static int Renown => Best(out _)?.renown ?? 0;

    // ----- Comforts put in a house -----

    /// <summary>Pays for a comfort and puts it in the house, made where the Haven is kept.</summary>
    public static bool Furnish(BuildingObject house, HomeLifeSettings.Comfort comfort, GameObject payer)
    {
        if (house == null || comfort == null || house.Id == null || GameSession.Here.HasComfort(house.Id, comfort.id))
        {
            return false;
        }

        MaterialPool pool = MaterialPool.For(payer);
        if (!pool.TrySpend(System.Array.Empty<ResourceAmount>(), comfort.cost))
        {
            pool.CanAfford(System.Array.Empty<ResourceAmount>(), comfort.cost, out string missing);
            GameMessages.Post($"Not enough for {comfort.name}: {missing}", MessageKind.Warning);
            return false;
        }

        return HavenDecisions.Do("house.comfort", house.Id + "|" + comfort.id, null, comfort.cost);
    }

    [HavenDecision("house.comfort")]
    private static HavenDecisions.Outcome PutIn(string args)
    {
        string[] parts = (args ?? "").Split('|');
        BuildingObject house = parts.Length > 1 ? BuildingObject.WithId(parts[0]) : null;
        HomeLifeSettings.Comfort comfort = parts.Length > 1 ? HomeLifeSettings.Current.FindComfort(parts[1]) : null;
        if (house == null || house.Beds <= 0 || comfort == null)
        {
            return HavenDecisions.Outcome.Refused("That house isn't here.");
        }

        if (!GameSession.Home.AddComfort(house.Id, comfort.id))
        {
            return HavenDecisions.Outcome.Refused($"The {house.Definition.DisplayName} has {comfort.name} already.");
        }

        Forget();
        return HavenDecisions.Outcome.Made($"{comfort.name} for the {house.Definition.DisplayName}.");
    }

    private static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);
}
