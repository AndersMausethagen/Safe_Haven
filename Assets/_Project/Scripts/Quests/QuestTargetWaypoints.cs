using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The way to where the Haven's own quests go on out in the land: for each quest under way, a QuestWaypoint on each objective whose
/// target is a place (Reach Place: HavenPlaceVerbs › Targets), a thing at a place (Use Object: a Find's Quest Target) or a region's
/// boundary stone (HavenRegionSettings › Stone Target), so the tracker's arrow points there with the distance. The journal's "Go there"
/// takes the player only to a place already found: the first walk out is the finding (the user, 2026-11-27: "walking for several
/// minutes is not fun"). A quest whose objective already has a waypoint of its own (placed by hand) is left to it. Made by
/// HavenPlaceVerbs.Begin; looks again whenever the quests move on.
/// </summary>
public sealed class HavenQuestWaypoints : MonoBehaviour
{
    // A stone stands by a path through its region, not at a place: the journal goes there once a place this near it is found.
    private const float StoneFoundWithin = 90f;

    private WorldAtlas atlas;
    private HavenPlaceVerbs verbs;
    private bool dirty = true;
    private readonly List<QuestWaypoint> made = new List<QuestWaypoint>();

    public IReadOnlyList<QuestWaypoint> Made => made;

    public void Begin(WorldAtlas world, HavenPlaceVerbs placeVerbs)
    {
        atlas = world;
        verbs = placeVerbs;
        dirty = true;
    }

    private void OnEnable()
    {
        QuestManager.Changed += MarkDirty;
    }

    private void OnDisable()
    {
        QuestManager.Changed -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void Update()
    {
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
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        var wanted = new HashSet<QuestWaypoint>();
        // In a friend's Haven, no arrows from the visitor's own log: their finds and quest scenes aren't stood in the host's land.
        if (atlas != null && verbs != null && log != null && !HavenVisit.IsVisiting)
        {
            BoundaryStone[] stones = null;
            foreach (QuestLog.Entry entry in log.Entries)
            {
                if (entry == null || entry.Quest == null || entry.Status != QuestStatus.Active)
                {
                    continue;
                }

                IReadOnlyList<QuestObjective> objectives = entry.Quest.Objectives;
                for (int i = 0; i < objectives.Count; i++)
                {
                    QuestTargetDefinition target = objectives[i].Target;
                    if (target == null)
                    {
                        continue;
                    }

                    QuestWaypoint kept = made.Find(w => w != null && w.Quest == entry.Quest && w.Objective == i + 1);
                    if (kept != null)
                    {
                        wanted.Add(kept);
                        continue;
                    }

                    stones ??= FindObjectsByType<BoundaryStone>(FindObjectsInactive.Exclude);
                    if (!HasOwn(entry.Quest, i + 1) && Locate(target, stones, out Vector3 at, out string placeId))
                    {
                        var holder = new GameObject($"Way to {target.DisplayName}");
                        holder.transform.SetParent(transform, false);
                        holder.transform.position = at;
                        var waypoint = holder.AddComponent<QuestWaypoint>();
                        waypoint.Configure(entry.Quest, i + 1, target.DisplayName);
                        waypoint.GoThereOnceFound(placeId);
                        made.Add(waypoint);
                        wanted.Add(waypoint);
                    }
                }
            }
        }

        for (int i = made.Count - 1; i >= 0; i--)
        {
            QuestWaypoint old = made[i];
            if (old == null || !wanted.Contains(old))
            {
                made.RemoveAt(i);
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

    // True when something else (a waypoint placed by hand, a stone's own for The Cold Stones) already shows this objective.
    private bool HasOwn(QuestDefinition quest, int objective)
    {
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.Quest == quest && (waypoint.Objective == objective || waypoint.Objective == 0) && !made.Contains(waypoint))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Where a quest target is in this scene's land, and the place that must be found before the journal goes there (null: never).</summary>
    public bool Locate(QuestTargetDefinition target, BoundaryStone[] stones, out Vector3 at, out string placeId)
    {
        at = default;
        placeId = null;
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

    // The nearest place of the stone's own region: one found on the way to somewhere else, over a border, doesn't count as the way there.
    private string NearestPlace(Vector2 from, float within, string region)
    {
        string best = null;
        float nearest = within * within;
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            float d = (place.at - from).sqrMagnitude;
            WorldAtlas.Region of = atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y));
            if (d <= nearest && of != null && of.name == region)
            {
                nearest = d;
                best = place.id;
            }
        }

        return best;
    }
}
