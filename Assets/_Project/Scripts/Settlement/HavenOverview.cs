using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// What the Haven window shows, read fresh from the loaded scene and GameSession.Settlement: where Safe Haven stands on its
/// climb, what its renown is made of, who of the expedition has come home and what to do next. Plain data, so the window
/// only draws it.
/// </summary>
public sealed class HavenOverview
{
    /// <summary>One thing the player could do next: what, how, the key that starts it (may be empty) and the renown it brings (0: not shown).</summary>
    public readonly struct Step
    {
        public readonly string Title;
        public readonly string Detail;
        public readonly string Key;
        public readonly int Renown;

        public Step(string title, string detail, string key, int renown)
        {
            Title = title;
            Detail = detail;
            Key = key;
            Renown = renown;
        }
    }

    /// <summary>Someone of the lost expedition: home (with their job, if they have one) or still out there.</summary>
    public readonly struct Person
    {
        public readonly string Name;
        public readonly bool Home;
        public readonly string Job;

        public Person(string name, bool home, string job)
        {
            Name = name;
            Home = home;
            Job = job;
        }
    }

    private const int MostIdeas = 2;
    private const int MostNewBuildings = 4;

    public int Renown { get; private set; }
    public int Rank { get; private set; }
    /// <summary>How far up the ladder of ranks Safe Haven stands, 0..1.</summary>
    public float Climb { get; private set; }
    public float WardRadius { get; private set; }
    /// <summary>The portal's tier (PortalTiers): the one ladder the Haven's progress is shown on.</summary>
    public int PortalTier { get; private set; }
    public float ClearedShare { get; private set; }
    public int Buildings { get; private set; }
    public List<HavenRenown.Part> Parts { get; } = new List<HavenRenown.Part>();
    public List<Person> Expedition { get; } = new List<Person>();
    public List<Step> Steps { get; } = new List<Step>();

    public int PeopleHome => Expedition.Count(person => person.Home);

    public static HavenOverview Read(GameObject user)
    {
        HavenSettings settings = HavenSettings.Current;
        var overview = new HavenOverview();
        var parts = new List<HavenRenown.Part>();
        overview.Renown = HavenRenown.Compute(parts);
        overview.Rank = Mathf.Max(GameSession.Settlement.HavenRank, settings.RankFor(overview.Renown));
        overview.Climb = ClimbOf(settings.Ranks, overview.Rank, overview.Renown);
        overview.WardRadius = HomePortal.Current != null ? HomePortal.Current.WardRadius : 0f;
        overview.PortalTier = PortalTiers.Current;
        overview.ClearedShare = Overgrowth.ClearedShare;
        overview.Buildings = BuildingObject.Active.Count(IsStandingBuilding);
        overview.Parts.AddRange(Grouped(parts));
        overview.ReadExpedition(settings);
        overview.ReadSteps(settings, user);
        return overview;
    }

    /// <summary>Stands each rank an equal step apart, however much renown it takes, and fills the step towards the next.</summary>
    public static float ClimbOf(IReadOnlyList<HavenSettings.Rank> ranks, int rank, int renown)
    {
        if (rank >= ranks.Count - 1)
        {
            return 1f;
        }

        return (rank + Mathf.InverseLerp(ranks[rank].renown, ranks[rank + 1].renown, renown)) / (ranks.Count - 1);
    }

    /// <summary>Largest first, with a kind that stands several times on one line ("Campfire ×3").</summary>
    public static IEnumerable<HavenRenown.Part> Grouped(IEnumerable<HavenRenown.Part> parts) => parts
        .GroupBy(part => part.Label)
        .Select(kind => new HavenRenown.Part(kind.Count() > 1 ? $"{kind.Key} ×{kind.Count()}" : kind.Key, kind.Sum(part => part.Renown)))
        .Where(part => part.Renown > 0)
        .OrderByDescending(part => part.Renown);

    private static bool IsStandingBuilding(BuildingObject building)
    {
        BuildingDefinition definition = building.Definition;
        if (definition == null || definition.Faction != Faction.Player)
        {
            return false;
        }

        return !building.TryGetComponent(out Construction construction) || construction.IsStanding;
    }

    // Why the haven is being built: who has made it home, and who still needs its light.
    private void ReadExpedition(HavenSettings settings)
    {
        foreach (ResidentDefinition person in settings.Expedition)
        {
            if (person == null)
            {
                continue;
            }

            ResidentRecord record = GameSession.Settlement.Find(person);
            Expedition.Add(new Person(person.DisplayName, record != null, record != null && record.Job != null ? record.Job.DisplayName : ""));
        }
    }

    // Essence that sits unspent is the one resource here that does nothing for the player.
    private void ReadEssenceStep(GameObject user, string use)
    {
        ResourceDefinition purified = SettlementSettings.Current.PurifiedEssence;
        IReadOnlyList<EssenceSettings.EmpowerLevel> levels = EssenceSettings.Current.EmpowerLevels;
        if (user == null || purified == null || levels.Count == 0 || !BuildingObject.Active.Any(b => b.TryGetComponent(out PurificationAltar _)))
        {
            return;
        }

        int have = MaterialPool.For(user).Have(purified);
        if (have >= levels[0].purified)
        {
            Steps.Add(new Step("Spend your Purified Essence", $"{have} at hand. Empower gear, enchant it or deepen a skill at the altar.", use, 0));
        }
    }

    private void ReadSteps(HavenSettings settings, GameObject user)
    {
        string use = GameInput.KeyLabel(GameInput.Interact);
        string build = GameInput.KeyLabel(GameInput.Build);
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach != null && breach.IsPending)
        {
            Steps.Add(new Step(breach.Definition.DisplayName, $"{breach.Hint}. Prepare, then begin the defense at the portal.", use, 0));
        }

        int hurt = BuildingRepairs.Damaged().Count;
        if (hurt > 0)
        {
            Steps.Add(new Step("Mend the clearing", $"{hurt} building{(hurt == 1 ? " is" : "s are")} damaged. Pay for repairs at the portal.", use, 0));
        }

        // The portal's next tier: its materials, or what it still waits for (decision 8.4).
        SettlementSettings.WardLevel nextTier = PortalTiers.Next(GameSession.Settlement);
        if (nextTier != null)
        {
            string needs = PortalTiers.Missing(nextTier, GameSession.Settlement);
            Steps.Add(new Step($"Raise the portal to {PortalTiers.Title(PortalTiers.Current + 1)}",
                needs.Length > 0 ? needs : $"{AmountText.Describe(nextTier.cost)}, at the portal.", use, 0));
        }

        int left = Overgrowth.All.Count(piece => !piece.IsCleared);
        if (left > 0)
        {
            int worth = settings.ClearingRenown - Mathf.RoundToInt(Overgrowth.ClearedShare * settings.ClearingRenown);
            Steps.Add(new Step("Clear the overgrowth", $"{left} left. Big stumps and boulders need better tools.", "", worth));
        }

        ReadEssenceStep(user, use);
        int ideas = 0;
        var upgrades = new HashSet<BuildingDefinition>();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (ideas >= MostIdeas || !building.TryGetComponent(out Construction construction))
            {
                continue;
            }

            if (construction.IsBuilding && !construction.Paid)
            {
                construction.Cost(out IReadOnlyList<ResourceAmount> _, out IReadOnlyList<ItemAmount> items);
                Steps.Add(new Step($"Repair the ruined {building.Definition.DisplayName}", $"{AmountText.Describe(items)}. Hold {use} at it.", use, building.Definition.Renown));
                ideas++;
            }
            else if (construction.IsBuilding)
            {
                Steps.Add(new Step($"Finish the {building.Definition.DisplayName}", $"Hold {use} at its scaffold.", use, 0));
                ideas++;
            }
            else if (construction.CanUpgrade && upgrades.Add(building.Definition))
            {
                BuildingLevel next = building.Definition.UpgradeTo(construction.Level + 1);
                Steps.Add(new Step($"Upgrade the {construction.Title}", next.CostText, build, Mathf.RoundToInt(next.renown)));
                ideas++;
            }
        }

        PlayerCrafting crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting != null)
        {
            var standing = new HashSet<BuildingDefinition>(BuildingObject.Active.Select(building => building.Definition));
            string[] missing = crafting.Buildings.Where(b => b != null && b.ShowInBuildMenu && crafting.IsKnown(b) && BuildingUnlocks.IsOpen(b) && !standing.Contains(b) && b.Renown > 0)
                .OrderByDescending(b => b.Renown).Take(MostNewBuildings).Select(b => $"{b.DisplayName} +{b.Renown}").ToArray();
            if (missing.Length > 0)
            {
                Steps.Add(new Step("Build something new", string.Join(", ", missing), build, 0));
            }
        }
    }
}
