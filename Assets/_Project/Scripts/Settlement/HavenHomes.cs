using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where Safe Haven's people live and work (Docs/BuildingSystemResearch.md §3.4.1): each resident sleeps in a house (the one
/// the player chose for them, else the one they live in, else the nearest free bed to their work, HomeRules) and works at
/// the building of their job's kind nearest that home. Homes are sticky: someone moves only for a bed clearly nearer their
/// work (HomeLifeSettings › Move Home For) or when their house is gone, and the move is told in a message. Two Smithies mean two places to work; a Cottage by the forge is where the smith walks home to.
/// Anyone without a bed sleeps at the home spots by the portal, as before.
/// <para>
/// Worked out again only when something changes (a building placed, moved, finished or upgraded; a job given; a home
/// chosen), never per frame, and written to the residents' records so it is saved and reaches a co-op friend with the
/// settlement. A friend's game reads what the host worked out. Added to the portal with HavenProgress.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class HavenHomes : MonoBehaviour
{
    private static readonly Dictionary<BuildingObject, int> indoors = new Dictionary<BuildingObject, int>();
    private readonly List<HomeRules.House> houses = new List<HomeRules.House>();
    private readonly List<HomeRules.Person> people = new List<HomeRules.Person>();
    // The houses as last seen standing, by id: one carried off to be moved is switched off for a while, and is still theirs.
    private readonly Dictionary<string, BuildingObject> seenHouses = new Dictionary<string, BuildingObject>();
    private readonly System.Text.StringBuilder seen = new System.Text.StringBuilder();
    private bool dirty = true;
    private string lastSeen = "";
    private string lastComforts = "";

    public static HavenHomes Current { get; private set; }

    /// <summary>Raised when someone's home or workplace changed: residents set off again, windows light up elsewhere.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        indoors.Clear();
    }

    private void OnEnable()
    {
        Current = this;
        BuildingObject.ActiveChanged += MarkDirty;
        Construction.Finished += OnFinished;
        GameSession.Here.Changed += MarkDirty;
        dirty = true;
    }

    private void OnDisable()
    {
        BuildingObject.ActiveChanged -= MarkDirty;
        Construction.Finished -= OnFinished;
        GameSession.Here.Changed -= MarkDirty;
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnFinished(Construction _) => MarkDirty();

    /// <summary>Works everyone's home out again at the end of this frame.</summary>
    public void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        if (!dirty)
        {
            return;
        }

        dirty = false;
        // A friend's Haven is worked out in their game; its answer arrives with the settlement (a Changed, so this runs then).
        bool changed = HavenVisit.IsVisiting ? SeenNew() : Settle();
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    // Shares the beds out and finds each one's workplace. True when anything changed.
    private bool Settle()
    {
        SettlementState settlement = GameSession.Here;
        IReadOnlyList<ResidentRecord> residents = settlement.Residents;
        Vector3 middle = Gather();
        string[] homes = HomeRules.Assign(houses, people, HomeLifeSettings.Current.MoveHomeFor);
        bool changed = false;
        for (int i = 0; i < residents.Count; i++)
        {
            ResidentRecord record = residents[i];
            TellMove(record, record.Home, homes[i] ?? "");
            BuildingObject home = BuildingObject.WithId(homes[i]);
            Vector3 from = home != null ? home.Door(out _) : middle;
            BuildingDefinition kind = record.Job != null ? record.Job.Workplace : null;
            BuildingObject work = kind != null ? BuildingObject.Nearest(kind, from) : null;
            string queue = work != null && work.TryGetComponent(out CraftingStation station) && kind.Station != CraftingStationType.None ? station.Id : "";
            changed |= settlement.SetHome(record, homes[i], work != null ? work.Id : "", queue);
        }

        return changed;
    }

    // The houses with beds and the people to share them among; returns the middle of the Haven.
    private Vector3 Gather()
    {
        IReadOnlyList<ResidentRecord> residents = GameSession.Here.Residents;
        Vector3 middle = HomePortal.Current != null ? HomePortal.Current.transform.position : transform.position;

        houses.Clear();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            int beds = building.Beds;
            string id = building.Id;
            if (beds > 0 && id != null)
            {
                Vector3 door = building.Door(out _);
                houses.Add(new HomeRules.House(id, Flat(door), beds));
                seenHouses[id] = building;
            }
        }

        // A house being carried to a new spot is switched off until it is set down: its people keep it meanwhile.
        foreach (ResidentRecord record in residents)
        {
            if (!string.IsNullOrEmpty(record.Home) && seenHouses.TryGetValue(record.Home, out BuildingObject away) && away != null
                && !away.gameObject.activeInHierarchy && away.Beds > 0 && !Listed(record.Home))
            {
                houses.Add(new HomeRules.House(record.Home, Flat(away.Door(out _)), away.Beds));
            }
        }

        // In id order, so every machine and every load shares the beds the same way.
        houses.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        people.Clear();
        foreach (ResidentRecord record in residents)
        {
            BuildingDefinition kind = record.Job != null ? record.Job.Workplace : null;
            BuildingObject work = kind != null ? BuildingObject.Nearest(kind, middle) : null;
            people.Add(new HomeRules.Person(record.Resident != null ? record.Resident.Id : "", record.HomeChosen,
                Flat(work != null ? work.transform.position : middle), record.Home));
        }

        return middle;
    }

    private bool Listed(string id)
    {
        foreach (HomeRules.House house in houses)
        {
            if (house.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    // Someone leaves the house they lived in: said, with where they go and what stays behind (comforts belong to a house).
    // Not for the player's own choice (that decision says so itself), nor for anyone's first bed.
    private static void TellMove(ResidentRecord record, string from, string to)
    {
        if (string.IsNullOrEmpty(from) || from == to || record.Resident == null || (!string.IsNullOrEmpty(to) && to == record.HomeChosen))
        {
            return;
        }

        string name = record.Resident.DisplayName;
        BuildingObject left = BuildingObject.WithId(from);
        BuildingObject next = BuildingObject.WithId(to);
        string where = next != null ? $"the {Describe(next)}" : "their spot by the portal";
        string said = left == null ? $"{name}'s house is gone: they sleep in {where} now."
            : next != null ? $"{name} moves from the {Describe(left)} to the {Describe(next)}, nearer their work."
            : $"{name} has no bed in the {Describe(left)} now: they sleep by the portal.";
        if (left != null)
        {
            var stay = new List<string>();
            foreach (string comfort in GameSession.Here.ComfortsIn(from))
            {
                HomeLifeSettings.Comfort known = HomeLifeSettings.Current.FindComfort(comfort);
                stay.Add(known != null ? known.name : comfort);
            }

            if (stay.Count > 0)
            {
                said += $" The {string.Join(" and the ", stay)} stay in the {left.Definition.DisplayName}.";
            }
        }

        GameMessages.Post(said);
    }

    // A friend's game: did the host's last word move anyone? (Their settlement arrives every two seconds.) Comforts put in a
    // house there change how good it is: its quality is worked out again then (HomeLife).
    private bool SeenNew()
    {
        SettlementState settlement = GameSession.Here;
        seen.Clear();
        foreach (ResidentRecord record in settlement.Residents)
        {
            seen.Append(record.Home).Append('/').Append(record.Workplace).Append(';');
        }

        string now = seen.ToString();
        bool changed = now != lastSeen;
        lastSeen = now;

        seen.Clear();
        foreach (KeyValuePair<string, List<string>> house in settlement.HouseComforts)
        {
            seen.Append(house.Key).Append(':');
            foreach (string comfort in house.Value)
            {
                seen.Append(comfort).Append(',');
            }

            seen.Append(';');
        }

        string comforts = seen.ToString();
        if (comforts != lastComforts)
        {
            lastComforts = comforts;
            HomeLife.Forget();
        }

        return changed;
    }

    // ----- Asking -----

    /// <summary>The house this resident sleeps in, standing in this scene, or null (the home spots by the portal).</summary>
    public static BuildingObject HomeOf(ResidentRecord record) => record != null ? BuildingObject.WithId(record.Home) : null;

    /// <summary>
    /// The building they work at: the one of their job's kind nearest their home (worked out by HavenHomes), else the
    /// nearest to <paramref name="from"/>. Null when none stands.
    /// </summary>
    public static BuildingObject WorkplaceOf(ResidentRecord record, Vector3 from)
    {
        BuildingDefinition kind = record != null && record.Job != null ? record.Job.Workplace : null;
        if (kind == null)
        {
            return null;
        }

        BuildingObject chosen = BuildingObject.WithId(record.Workplace);
        if (chosen != null && chosen.Definition == kind && chosen.IsStanding)
        {
            return chosen;
        }

        BuildingObject home = HomeOf(record);
        return BuildingObject.Nearest(kind, home != null ? home.Door(out _) : from);
    }

    /// <summary>Everyone whose home this house is, in order of arrival.</summary>
    public static List<ResidentRecord> LivingIn(BuildingObject house)
    {
        var found = new List<ResidentRecord>();
        string id = house != null ? house.Id : null;
        if (id == null)
        {
            return found;
        }

        foreach (ResidentRecord record in GameSession.Here.Residents)
        {
            if (record.Home == id)
            {
                found.Add(record);
            }
        }

        return found;
    }

    /// <summary>How many are inside this house now (asleep): its window is lit while anyone is.</summary>
    public static int Indoors(BuildingObject house) => house != null && indoors.TryGetValue(house, out int count) ? count : 0;

    /// <summary>Someone went into (true) or came out of (false) a house. ResidentLife calls it.</summary>
    public static void NoteIndoors(BuildingObject house, bool inside)
    {
        if (house == null)
        {
            return;
        }

        indoors.TryGetValue(house, out int count);
        count = Mathf.Max(0, count + (inside ? 1 : -1));
        if (count == 0)
        {
            indoors.Remove(house);
        }
        else
        {
            indoors[house] = count;
        }
    }

    /// <summary>
    /// Who would move into a house with <paramref name="beds"/> beds and its door at <paramref name="door"/> if it stood
    /// there now (the build ghost's readout): the beds shared out again with it among the houses.
    /// </summary>
    public static List<ResidentRecord> WouldMoveIn(Vector3 door, int beds)
    {
        var found = new List<ResidentRecord>();
        WouldMoveIn(door, beds, found);
        return found;
    }

    /// <summary>As <see cref="WouldMoveIn(Vector3, int)"/>, into a list of the caller's (cleared first).</summary>
    public static void WouldMoveIn(Vector3 door, int beds, List<ResidentRecord> found)
    {
        found.Clear();
        HavenHomes homes = Current;
        if (homes == null || beds <= 0)
        {
            return;
        }

        const string ghost = "\u0001ghost";
        homes.Gather();
        homes.houses.Add(new HomeRules.House(ghost, Flat(door), beds));
        string[] assigned = HomeRules.Assign(homes.houses, homes.people, HomeLifeSettings.Current.MoveHomeFor);
        IReadOnlyList<ResidentRecord> residents = GameSession.Here.Residents;
        for (int i = 0; i < assigned.Length && i < residents.Count; i++)
        {
            if (assigned[i] == ghost)
            {
                found.Add(residents[i]);
            }
        }
    }

    // ----- Choosing (the resident's window) -----

    /// <summary>The player chooses a house for someone (empty: let them choose), made where the Haven is kept.</summary>
    public static void Choose(ResidentDefinition resident, string house) =>
        HavenDecisions.Do("resident.home", (resident != null ? resident.Id : "") + "|" + (house ?? ""));

    [HavenDecision("resident.home")]
    private static HavenDecisions.Outcome ChooseHome(string args)
    {
        string[] parts = (args ?? "").Split('|');
        ResidentRecord record = HavenPeople.RecordOf(parts[0]);
        if (record == null)
        {
            return HavenDecisions.Outcome.Refused("They don't live here now.");
        }

        string house = parts.Length > 1 ? parts[1] : "";
        if (house.Length == 0)
        {
            GameSession.Home.ChooseHome(record, "");
            return HavenDecisions.Outcome.Made($"{record.Resident.DisplayName} will find a bed of their own.");
        }

        BuildingObject building = BuildingObject.WithId(house);
        if (building == null || building.Beds <= 0)
        {
            return HavenDecisions.Outcome.Refused("That isn't a house with beds.");
        }

        // Room for them: a bed nobody chose, or one already theirs. Someone the player put there keeps their bed.
        int taken = 0;
        foreach (ResidentRecord other in GameSession.Here.Residents)
        {
            taken += other != record && other.HomeChosen == house ? 1 : 0;
        }

        if (taken >= building.Beds)
        {
            return HavenDecisions.Outcome.Refused($"Every bed in the {building.Definition.DisplayName} is promised to someone you put there.");
        }

        GameSession.Home.ChooseHome(record, house);
        return HavenDecisions.Outcome.Made($"{record.Resident.DisplayName} moves into the {building.Definition.DisplayName}.");
    }

    /// <summary>A building as the player would point it out: "Hut by the Well", else "Hut, 23 m north of the portal".</summary>
    public static string Describe(BuildingObject building)
    {
        if (building == null || building.Definition == null)
        {
            return "";
        }

        const float near = 14f;
        BuildingObject by = null;
        float best = near * near;
        foreach (BuildingObject other in BuildingObject.Active)
        {
            BuildingDefinition kind = other.Definition;
            if (other == building || kind == null || kind == building.Definition || kind.PlaceInLines || !other.IsStanding)
            {
                continue;
            }

            float distance = Flat(other.transform.position - building.transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                by = other;
            }
        }

        if (by != null)
        {
            return $"{building.Definition.DisplayName} by the {by.Definition.DisplayName}";
        }

        Vector3 portal = HomePortal.Current != null ? HomePortal.Current.transform.position : Vector3.zero;
        Vector2 away = Flat(building.transform.position - portal);
        return away.magnitude < 3f ? $"{building.Definition.DisplayName} by the portal"
            : $"{building.Definition.DisplayName}, {away.magnitude:0} m {Compass(away)} of the portal";
    }

    /// <summary>Metres on the ground between two places.</summary>
    public static float Walk(Vector3 from, Vector3 to) => Flat(to - from).magnitude;

    private static string Compass(Vector2 direction)
    {
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        float bearing = Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);
        return names[Mathf.RoundToInt(bearing / 45f) % 8];
    }

    private static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);
}
