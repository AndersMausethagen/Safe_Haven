using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes things happen during a raid: when the time comes it picks an event from the raid's pool (RaidEventPicker), puts
/// its site somewhere out of sight, warns the player and carries out what it does at once. Choices are made at the site
/// (RaidEventSite). Reports each event for the playtest log and the raid summary. Installed by RaidEventsSystem.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidEventDirector : MonoBehaviour
{
    private const float ThinkInterval = 1f;

    private readonly List<RaidEventSite> open = new List<RaidEventSite>();
    private readonly List<RaidEventSite> pinned = new List<RaidEventSite>();
    private readonly HashSet<RaidEventDefinition> used = new HashSet<RaidEventDefinition>();
    private RaidEventSettings settings;
    private RaidDirector director;
    private System.Random random;
    private NavMeshPath path;
    private Transform container;
    private float nextThink;
    private float nextEventAt = -1f;

    /// <summary>Sites still waiting for the player, for the HUD.</summary>
    public IReadOnlyList<RaidEventSite> OpenSites => open;

    /// <summary>The rumours' sites, for the HUD: they wait all raid and don't count against the raid's events.</summary>
    public IReadOnlyList<RaidEventSite> PinnedSites => pinned;

    /// <summary>How many events have begun this raid.</summary>
    public int Happened => used.Count;

    public void Configure(RaidEventSettings eventSettings) => settings = eventSettings;

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
        path = new NavMeshPath();
    }

    private void Update()
    {
        if (settings == null || !director.IsRunning || director.Player == null || Time.time < nextThink)
        {
            return;
        }

        nextThink = Time.time + ThinkInterval;
        RaidSession session = director.Session;
        random ??= new System.Random(session.Seed ^ 0x52414944);
        if (nextEventAt < 0f)
        {
            nextEventAt = settings.firstDelay;
        }

        open.RemoveAll(site => site == null || site.IsResolved);
        if (session.Elapsed < nextEventAt || used.Count >= settings.maxPerRaid || open.Count >= settings.maxOpenSites)
        {
            return;
        }

        RaidEventDefinition next = RaidEventPicker.Pick(settings.pool, random, session.Elapsed, session.TimeRemaining, director.StageIndex, used);
        // A failed placement tries again on the next think, with the player somewhere else.
        if (next != null && Begin(next))
        {
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
        if (definition.SitePrefab != null
            && !RaidSpots.TryFindSpot(from, definition.SiteDistance.x, definition.SiteDistance.y, random, path, out position))
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

    private void Place(RaidEventDefinition definition, Vector3 position, List<RaidEventSite> sites)
    {
        var context = new RaidEventContext
        {
            Session = director.Session,
            Director = director,
            Player = director.Player,
            Position = position,
            Random = random,
        };

        used.Add(definition);
        if (definition.SitePrefab != null)
        {
            GameObject placed = Instantiate(definition.SitePrefab, position, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), Container());
            if (placed.TryGetComponent(out RaidEventSite site))
            {
                site.Bind(definition, context);
                sites.Add(site);
            }
            else
            {
                Debug.LogError($"{definition.DisplayName}: its site prefab has no RaidEventSite.", placed);
            }
        }

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
            AudioManager.Play(definition.WarningSound, position);
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
