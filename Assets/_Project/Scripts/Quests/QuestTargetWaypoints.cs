using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The way to whoever and whatever the quests under way ask for, so every one of them is on the map, the minimap and under the
/// tracker's arrow, tracked or not (the user, 2026-11-27: "all quests with a target should be shown on the map. not just tracked").
/// For each open objective with a target it puts a QuestWaypoint: at a place of the Haven's land (Reach Place: HavenPlaceVerbs ›
/// Targets), a thing at a place (Use Object: a Find's Quest Target), a region's boundary stone (HavenRegionSettings › Stone
/// Target), or whatever in the scene is that person or thing (a Talkable's person or Quest Target, a carved stone, a
/// Quest Event Trigger, a stranded traveller). A person's waypoint goes with them as they walk about.
/// <para>
/// The journal's "Go there" takes the player only to a place of the land already found: the first walk out is the finding (the
/// user, 2026-11-27: "walking for several minutes is not fun"). An objective that already has a waypoint of its own (placed by
/// hand) is left to it. One in every scene, made by itself; the Haven's (HavenPlaceVerbs.Begin) also knows its land. Looks again
/// whenever the quests move on, and now and then while someone asked for is away or a waypoint's holder has gone.
/// </para>
/// </summary>
public sealed class QuestTargetWaypoints : MonoBehaviour
{
    // A stone stands by a path through its region, not at a place: the journal goes there once a place this near it is found.
    private const float StoneFoundWithin = 90f;
    // A thing out in the land (not in the yard) counts as found once a place this near it is.
    private const float ThingFoundWithin = 60f;
    private const float LookAgainSeconds = 3f;

    private WorldAtlas atlas;
    private HavenPlaceVerbs verbs;
    private bool dirty = true;
    // Someone or something asked for wasn't here (a resident away, a waypoint's holder gone): looked for again now and then.
    private bool missing;
    private float nextLook;
    private readonly List<QuestWaypoint> made = new List<QuestWaypoint>();
    // How long each way takes, for the playtest log.
    private readonly PlaytestTrips trips = new PlaytestTrips();

    /// <summary>This scene's, while there is one.</summary>
    public static QuestTargetWaypoints Current { get; private set; }

    public IReadOnlyList<QuestWaypoint> Made => made;
    public PlaytestTrips Trips => trips;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    // One in every scene: entering Play doesn't reload scripts here, so the hook is taken off before it is put on.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Current == null && scene.IsValid() && scene.isLoaded)
        {
            Ensure(null);
        }
    }

    /// <summary>This scene's, made (under <paramref name="parent"/>, or at the scene's root) if there is none yet.</summary>
    public static QuestTargetWaypoints Ensure(Transform parent)
    {
        if (Current != null)
        {
            return Current;
        }

        var holder = new GameObject("Quest Target Waypoints");
        if (parent != null)
        {
            holder.transform.SetParent(parent, false);
        }

        return holder.AddComponent<QuestTargetWaypoints>();
    }

    /// <summary>The Haven's land, for its places, finds and stones.</summary>
    public void Begin(WorldAtlas world, HavenPlaceVerbs placeVerbs)
    {
        atlas = world;
        verbs = placeVerbs;
        dirty = true;
    }

    private void OnEnable()
    {
        Current = this;
        QuestManager.Changed += MarkDirty;
        HavenTravel.Arrived += OnTravelled;
    }

    private void OnDisable()
    {
        QuestManager.Changed -= MarkDirty;
        HavenTravel.Arrived -= OnTravelled;
        if (Current == this)
        {
            Current = null;
        }
    }

    private void MarkDirty() => dirty = true;

    private void OnTravelled(Vector3 from, Vector3 to, bool rode) => trips.Jumped();

    private void Update()
    {
        if (trips.Open > 0 && !GamePause.IsPaused && Application.isFocused)
        {
            Player player = Player.Current;
            trips.Tick(Time.unscaledDeltaTime, player != null && player.TryGetComponent(out PlayerRiding riding) && riding.IsRiding);
        }

        if (!dirty && Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + LookAgainSeconds;
            dirty = missing || made.Exists(w => w == null);
        }

        if (dirty)
        {
            Refresh();
        }
    }

    /// <summary>Sets the waypoints for the quests under way now: those already made stay (the journal's "Go there" holds their
    /// transform, and a pickup raises QuestManager.Changed), the ones no longer wanted go, new ones are made.</summary>
    public void Refresh()
    {
        dirty = false;
        missing = false;
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        var wanted = new HashSet<QuestWaypoint>();
        // In a friend's Haven, no arrows from the visitor's own log: their finds and quest scenes aren't stood in the host's land.
        if (log != null && !HavenVisit.IsVisiting)
        {
            BoundaryStone[] stones = null;
            SceneTargets here = null;
            foreach (QuestLog.Entry entry in log.Entries)
            {
                if (entry == null || entry.Quest == null || entry.Status != QuestStatus.Active)
                {
                    continue;
                }

                IReadOnlyList<QuestObjective> objectives = entry.Quest.Objectives;
                for (int i = 0; i < objectives.Count; i++)
                {
                    Object about = objectives[i].DeedTarget;
                    if (about == null || i < entry.Counts.Length && (entry.IsObjectiveDone(i) || !entry.IsObjectiveOpen(i)))
                    {
                        continue;
                    }

                    // One placed by hand that shows now (switched on since, perhaps) takes over: the one made here goes.
                    if (HasOwn(entry.Quest, i + 1))
                    {
                        continue;
                    }

                    QuestWaypoint kept = made.Find(w => w != null && w.Quest == entry.Quest && w.Objective == i + 1);
                    if (kept != null)
                    {
                        wanted.Add(kept);
                        continue;
                    }

                    var target = about as QuestTargetDefinition;
                    // Stones only for a target that is one: looked for once per look, not for every person asked after.
                    if (stones == null && target != null && HavenRegionSettings.Current != null && HavenRegionSettings.Current.RegionOfStone(target) != null)
                    {
                        stones = FindObjectsByType<BoundaryStone>(FindObjectsInactive.Exclude);
                    }

                    Transform follow = null;
                    Vector3 at = default;
                    string placeId = null;
                    bool found = target != null && Locate(target, stones ?? NoStones, out at, out placeId);
                    if (!found)
                    {
                        here ??= new SceneTargets();
                        found = here.Find(about, out follow);
                        at = follow != null ? follow.position : default;
                        placeId = found ? FoundBy(at) : null;
                    }

                    if (!found)
                    {
                        // Only people come and go (home from a rescue, back from a foray): a thing not in this scene (a raid's
                        // carved stone) won't turn up, so it isn't looked for again every few seconds.
                        missing |= about is ResidentDefinition;
                        continue;
                    }

                    string label = about is IQuestTarget named ? named.DisplayName : about.name;
                    bool person = follow != null && IsPerson(follow);
                    var holder = new GameObject($"Way to {label}");
                    holder.transform.SetParent(person ? follow : transform, false);
                    holder.transform.position = at;
                    var waypoint = holder.AddComponent<QuestWaypoint>();
                    waypoint.Configure(entry.Quest, i + 1, label);
                    if (person)
                    {
                        waypoint.GoThereOnlyInTheYard();
                    }
                    else if (placeId != null || OutInTheLand(at))
                    {
                        waypoint.GoThereOnceFound(placeId);
                    }

                    made.Add(waypoint);
                    wanted.Add(waypoint);
                    trips.Begin(waypoint, label);
                }
            }
        }

        for (int i = made.Count - 1; i >= 0; i--)
        {
            QuestWaypoint old = made[i];
            if (old == null || !wanted.Contains(old))
            {
                made.RemoveAt(i);
                // A holder gone (someone asked for left the scene) isn't the way walked: only a done objective's trip is written.
                trips.End(old, old != null ? old.Quest : null, old != null ? old.Objective : 0, old != null && IsDone(log, old.Quest, old.Objective));
                if (old != null)
                {
                    // Off at once (out of QuestWaypoint.Active), not only at the frame's end.
                    old.enabled = false;
                    if (Application.isPlaying)
                    {
                        Destroy(old.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(old.gameObject);
                    }
                }
            }
        }
    }

    private static readonly BoundaryStone[] NoStones = new BoundaryStone[0];

    // True when something else (a waypoint placed by hand, a stone's own for The Cold Stones) already shows this objective while the
    // quest is under way: one shown only before it or at its hand-in (the prologue's "Sigrid" pair) doesn't.
    private bool HasOwn(QuestDefinition quest, int objective)
    {
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.Quest == quest && waypoint.When == QuestWaypointWhen.Active
                && (waypoint.Objective == objective || waypoint.Objective == 0) && !made.Contains(waypoint))
            {
                return true;
            }
        }

        return false;
    }

    // The objective (1 is the first) done, or the whole quest past it.
    private static bool IsDone(QuestLog log, QuestDefinition quest, int objective)
    {
        QuestLog.Entry entry = log != null && quest != null ? log.Get(quest) : null;
        if (entry == null)
        {
            return false;
        }

        if (entry.Status == QuestStatus.Completed || entry.Status == QuestStatus.ReadyToTurnIn)
        {
            return true;
        }

        int index = objective - 1;
        return entry.Status == QuestStatus.Active && index >= 0 && index < entry.Counts.Length && entry.IsObjectiveDone(index);
    }

    private static bool IsPerson(Transform holder) => holder.TryGetComponent(out IQuestPerson _) || holder.TryGetComponent(out RescuableTraveler _);

    // Out past the Haven's yard: the journal goes there only once found. In the yard (or anywhere with no Haven), as before.
    private static bool OutInTheLand(Vector3 at) => HomePortal.Current != null && !HomePortal.Current.InBuildLand(at);

    // A thing in the scene out in the land counts as found by a place of the map near it; none near: walked to, never gone to.
    private string FoundBy(Vector3 at) => atlas != null && OutInTheLand(at) ? NearestPlace(new Vector2(at.x, at.z), ThingFoundWithin, null) : null;

    /// <summary>Where a quest target is in the Haven's land (a place, a thing at one, a region's stone), and the place that must be
    /// found before the journal goes there (null: never). False with no land, or when it isn't one of those.</summary>
    public bool Locate(QuestTargetDefinition target, BoundaryStone[] stones, out Vector3 at, out string placeId)
    {
        at = default;
        placeId = null;
        if (atlas == null || verbs == null)
        {
            return false;
        }

        foreach (HavenPlaceVerbs.PlaceTarget entry in verbs.Targets)
        {
            if (entry != null && entry.target == target && TryPlace(entry.placeId, out WorldAtlas.Place place))
            {
                at = HavenPlaceVerbs.Ground(place.at);
                placeId = place.id;
                return true;
            }
        }

        foreach (HavenPlaceVerbs.Find find in verbs.Finds)
        {
            if (find != null && find.questTarget == target && TryPlace(find.placeId, out WorldAtlas.Place place))
            {
                at = HavenPlaceVerbs.Ground(place.at + find.offset);
                placeId = place.id;
                return true;
            }
        }

        string region = HavenRegionSettings.Current != null ? HavenRegionSettings.Current.RegionOfStone(target) : null;
        if (region == null)
        {
            return false;
        }

        foreach (BoundaryStone stone in stones)
        {
            if (stone != null && stone.Region == region)
            {
                at = stone.transform.position;
                placeId = NearestPlace(new Vector2(at.x, at.z), StoneFoundWithin, region);
                return true;
            }
        }

        return false;
    }

    private bool TryPlace(string id, out WorldAtlas.Place found)
    {
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            if (place.id == id)
            {
                found = place;
                return true;
            }
        }

        found = null;
        return false;
    }

    // The nearest place (of the given region, if one is given): for a stone, one found on the way to somewhere else over a border
    // doesn't count as the way there.
    private string NearestPlace(Vector2 from, float within, string region)
    {
        string best = null;
        float nearest = within * within;
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            float d = (place.at - from).sqrMagnitude;
            if (d > nearest)
            {
                continue;
            }

            WorldAtlas.Region of = region != null ? atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y)) : null;
            if (region == null || of != null && of.name == region)
            {
                nearest = d;
                best = place.id;
            }
        }

        return best;
    }

    /// <summary>Who and what in the loaded scenes quests know by name: a Talkable's person or Quest Target, a carved
    /// stone, a Quest Event Trigger, a stranded traveller. Gathered once per look.</summary>
    private sealed class SceneTargets
    {
        private readonly Dictionary<Object, Transform> found = new Dictionary<Object, Transform>();

        public SceneTargets()
        {
            foreach (Talkable talkable in FindObjectsByType<Talkable>(FindObjectsInactive.Exclude))
            {
                if (talkable.GetComponentInParent<HomePortal>() == null)
                {
                    Add(talkable.QuestIdentity, talkable.transform);
                }
            }

            foreach (RescuableTraveler traveller in FindObjectsByType<RescuableTraveler>(FindObjectsInactive.Exclude))
            {
                Add(traveller.Resident, traveller.transform);
            }

            // Not the portal: the map's Home mark already stands there, with how far it is.
            foreach (CarvedStone stone in FindObjectsByType<CarvedStone>(FindObjectsInactive.Exclude))
            {
                Add(stone.QuestTarget, stone.transform);
            }

            foreach (GameEventTrigger trigger in FindObjectsByType<GameEventTrigger>(FindObjectsInactive.Exclude))
            {
                Add(trigger.Target, trigger.transform);
            }
        }

        private void Add(Object identity, Transform at)
        {
            if (identity != null && !found.ContainsKey(identity))
            {
                found.Add(identity, at);
            }
        }

        public bool Find(Object about, out Transform at) => found.TryGetValue(about, out at) && at != null;
    }
}
