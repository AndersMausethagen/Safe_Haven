using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes things happen during a raid: when the time comes it picks an event from the raid's pool (RaidEventPicker), puts
/// its site somewhere out of sight, warns the player and carries out what it does at once. Choices are made at the site
/// (RaidEventSite). Reports each event for the playtest log and the raid summary. Installed by RaidEventsSystem.
/// <para>
/// Three kinds of site: the raid's own events (<see cref="OpenSites"/>), the briefing's rumours (<see cref="PinnedSites"/>, placed
/// by RaidRumourDirector) and sites that stand where the map put them (<see cref="StandingSites"/>: a valley's bridge tolls). A
/// standing site is part of the place, not news: it waits quietly, its beacon hidden like a rumour's, and its event begins (the
/// warning, its sound, the raid's log) only when the player first comes upon it (review 2026-11-17, R1).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidEventDirector : MonoBehaviour
{
    private const float ThinkInterval = 1f;

    private readonly List<RaidEventSite> open = new List<RaidEventSite>();
    private readonly List<RaidEventSite> pinned = new List<RaidEventSite>();
    private readonly List<RaidEventSite> standing = new List<RaidEventSite>();
    // Standing sites the player hasn't come upon yet, with what their events need once they do.
    private readonly List<(RaidEventSite site, RaidEventDefinition definition, RaidEventContext context)> unmet =
        new List<(RaidEventSite, RaidEventDefinition, RaidEventContext)>();
    private readonly HashSet<RaidEventDefinition> used = new HashSet<RaidEventDefinition>();
    private RaidEventSettings settings;
    private RaidDirector director;
    private System.Random random;
    private NavMeshPath path;
    private Transform container;
    private float nextThink;
    private float nextEventAt = -1f;
    // The raid's own events, counted against Max Per Raid; the rumours' pinned sites are in `used` too but not counted.
    private int randomEvents;
    // The map's event rooms already given an event (indices into RaidMap.Places).
    private readonly HashSet<int> roomsTaken = new HashSet<int>();
    private const float RoomStretch = 1.6f;
    private RaidMap map;

    private RaidMap Map => map != null ? map : map = FindAnyObjectByType<RaidMap>();

    /// <summary>Sites still waiting for the player, for the HUD.</summary>
    public IReadOnlyList<RaidEventSite> OpenSites => open;

    /// <summary>The rumours' sites, for the HUD: they wait all raid and don't count against the raid's events.</summary>
    public IReadOnlyList<RaidEventSite> PinnedSites => pinned;

    /// <summary>
    /// Sites that stand all raid where the map put them (a valley's bridge tolls), for the HUD: not rumours, so neither counted nor
    /// marked as one. A map shows one once the player has come upon it (<see cref="RaidEventSite.IsFoundFrom"/>).
    /// </summary>
    public IReadOnlyList<RaidEventSite> StandingSites => standing;

    /// <summary>How many events have begun this raid.</summary>
    public int Happened => used.Count;

    public void Configure(RaidEventSettings eventSettings) => settings = eventSettings;

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
        path = new NavMeshPath();
    }

    private void OnEnable() => GameEvents.Raised += OnGameEvent;

    private void OnDisable() => GameEvents.Raised -= OnGameEvent;

    // As the raid ends, every site still open goes to the playtest log: reached and passed by, or never reached
    // (Docs/RaidEventsPlan_2026-11-26.md E1: why players pass events by).
    private void OnGameEvent(GameEvent raised)
    {
        if (raised.Kind != GameEventKind.RaidFinished)
        {
            return;
        }

        foreach (List<RaidEventSite> sites in new[] { open, pinned, standing })
        {
            foreach (RaidEventSite site in sites)
            {
                // A standing site (a bridge's toll) is part of the map from the start: passed by only if the player came to it.
                if (sites == standing && site != null && !site.WasReached)
                {
                    continue;
                }

                if (site != null && !site.IsResolved && site.Definition != null)
                {
                    PlaytestLogger.Note(PlaytestLog.EventSkipped, site.Definition.name, site.WasReached ? 1 : 0,
                        $"began {Mathf.RoundToInt(site.BegunAt)} s, {Mathf.RoundToInt(site.BegunMetres)} m off");
                }
            }
        }
    }

    private void Update()
    {
        if (!director.IsRunning || director.Player == null || Time.time < nextThink)
        {
            return;
        }

        nextThink = Time.time + ThinkInterval;
        MeetStanding(director.Player.transform.position);
        if (settings == null)
        {
            return;
        }

        RaidSession session = director.Session;
        random ??= new System.Random(session.Seed ^ 0x52414944);
        if (nextEventAt < 0f)
        {
            nextEventAt = settings.firstDelay;
        }

        open.RemoveAll(site => site == null || site.IsResolved);
        if (session.Elapsed < nextEventAt || randomEvents >= settings.maxPerRaid || open.Count >= settings.maxOpenSites)
        {
            return;
        }

        // What the briefing named comes first, so it comes true. Each foretold event that can still come keeps its place in the
        // raid's count: the pool's picks used every place up before a late one's window opened (an Oskorei waits for stage 1),
        // and the Watch Post's word never came true.
        int owed = 0;
        foreach (RaidEventDefinition foretold in session.Foretold)
        {
            if (foretold != null && !used.Contains(foretold) && foretold.CanStillHappen(session.TimeRemaining))
            {
                owed++;
            }
        }

        RaidEventDefinition next = RaidEventPicker.Pick(session.Foretold, random, session.Elapsed, session.TimeRemaining, director.StageIndex, used);
        if (next == null && randomEvents + owed < settings.maxPerRaid)
        {
            next = RaidEventPicker.Pick(settings.pool, random, session.Elapsed, session.TimeRemaining, director.StageIndex, used);
        }
        // A failed placement tries again on the next think, with the player somewhere else.
        if (next != null && Begin(next))
        {
            randomEvents++;
            float spread = ((float)random.NextDouble() * 2f - 1f) * settings.intervalSpread;
            nextEventAt = session.Elapsed + Mathf.Max(5f, settings.interval + spread);
        }
    }

    /// <summary>Starts an event now, whatever the clock says. False when there is no place for its site.</summary>
    public bool Begin(RaidEventDefinition definition)
    {
        random ??= new System.Random(director.Session.Seed ^ 0x52414944);
        Vector3 from = director.Player.transform.position;
        Vector3 position = from;
        // An event room the map set aside first (Docs/RaidLayoutAnalysis_2026-11-26.md R3), so the event stands in a clearing made and
        // dressed for it; any spot out of sight when none is free at its distance, or on a painted map.
        // The rooms lie 30 m and more from the start, mostly beyond an early event's reach, so a room a little farther than the event's
        // own distance (RoomStretch) still wins over open forest: the rooms lie inward, on the way to the exits.
        IReadOnlyList<RaidPlace> places = Map != null ? Map.Places : null;
        Vector2 reach = definition.SiteDistance;
        if (definition.SitePrefab != null
            && !RaidSpots.TryTakeRoom(places, from, reach.x, reach.y, roomsTaken, random, path, out position)
            && !RaidSpots.TryTakeRoom(places, from, reach.y, reach.y * RoomStretch, roomsTaken, random, path, out position)
            && !RaidSpots.TryFindSpot(from, reach.x, reach.y, random, path, out position))
        {
            return false;
        }

        Place(definition, position, open);
        return true;
    }

    /// <summary>Stands an event at a given place for the whole raid: what a rumour promised.</summary>
    public void BeginAt(RaidEventDefinition definition, Vector3 position)
    {
        random ??= new System.Random(director.Session.Seed ^ 0x52414944);
        Place(definition, position, pinned);
    }

    /// <summary>
    /// Stands an event's site where the map put it, facing <paramref name="facing"/>, for the whole raid: a valley's bridge toll
    /// (RaidBridgeToll). Quietly: no warning, sound or log line now, not a rumour, and its beacon is hidden beyond a rumour's
    /// distance (<see cref="RaidRumourDirector.BeaconShowsWithin"/>). Its event begins when the player first comes that close.
    /// </summary>
    public void Stand(RaidEventDefinition definition, Vector3 position, Quaternion facing)
    {
        random ??= new System.Random(director.Session.Seed ^ 0x52414944);
        if (definition.SitePrefab == null)
        {
            Debug.LogError($"{definition.DisplayName}: a standing event needs a site prefab to stand there.", definition);
            return;
        }

        used.Add(definition);
        RaidEventContext context = ContextAt(position);
        RaidEventSite site = Spawn(definition, context, facing);
        if (site == null)
        {
            return;
        }

        site.HideBeaconBeyond(RaidRumourDirector.BeaconShowsWithin);
        standing.Add(site);
        unmet.Add((site, definition, context));
    }

    // A standing site's event begins once the player comes upon it: within its hidden beacon's distance. One already done when next
    // looked at (a choice made between two looks) has its choice in the log already, so it is let go without a word.
    private void MeetStanding(Vector3 player)
    {
        for (int i = unmet.Count - 1; i >= 0; i--)
        {
            (RaidEventSite site, RaidEventDefinition definition, RaidEventContext context) = unmet[i];
            if (site == null || site.IsResolved)
            {
                unmet.RemoveAt(i);
            }
            else if (site.IsFoundFrom(player))
            {
                unmet.RemoveAt(i);
                Announce(definition, context);
            }
        }
    }

    private void Place(RaidEventDefinition definition, Vector3 position, List<RaidEventSite> sites)
    {
        RaidEventContext context = ContextAt(position);
        used.Add(definition);
        if (definition.SitePrefab != null)
        {
            RaidEventSite site = Spawn(definition, context, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
            if (site != null)
            {
                sites.Add(site);
            }
        }

        Announce(definition, context);
    }

    private RaidEventContext ContextAt(Vector3 position) => new RaidEventContext
    {
        Session = director.Session,
        Director = director,
        Player = director.Player,
        Position = position,
        Random = random,
    };

    // The event's site, bound to it; null (with an error) when its prefab has no RaidEventSite.
    private RaidEventSite Spawn(RaidEventDefinition definition, RaidEventContext context, Quaternion facing)
    {
        GameObject placed = Instantiate(definition.SitePrefab, context.Position, facing, Container());
        if (!placed.TryGetComponent(out RaidEventSite site))
        {
            Debug.LogError($"{definition.DisplayName}: its site prefab has no RaidEventSite.", placed);
            return null;
        }

        context.Site = site;
        site.Bind(definition, context);
        return site;
    }

    // The event begins: what it does at once, its warning and sound, and its line in the raid's log and the game's events.
    private void Announce(RaidEventDefinition definition, RaidEventContext context)
    {
        foreach (RaidEventOutcome outcome in definition.OnStart)
        {
            outcome?.Apply(context);
        }

        if (!string.IsNullOrWhiteSpace(definition.Warning))
        {
            GameMessages.Post(definition.Warning, MessageKind.Warning);
        }

        if (definition.WarningSound != null)
        {
            AudioManager.Play(definition.WarningSound, context.Position);
        }

        director.Session.RecordEvent(definition.DisplayName);
        GameEvents.Raise(new GameEvent(GameEventKind.RaidEvent, definition, 0, director.Player));
    }

    private Transform Container()
    {
        if (container == null)
        {
            container = new GameObject("Raid Events").transform;
        }

        return container;
    }
}
