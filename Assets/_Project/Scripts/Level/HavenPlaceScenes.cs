using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the Haven's places as their story stands (Docs/HavenWorldStory_2026-11-26.md steps 3-4): each place's scene stages
/// (HavenPlaceVerbs › Scenes: dressing, thoughts, a worked place's smoke) and the things left there to take (Finds), each shown only
/// while its gate is open. Looks again whenever a quest moves, a region is remembered or a day passes at home, so nothing waits for
/// the next scene load. Stood by HavenPlaceVerbs.Begin on the scene's place-verbs holder; derives everything from the quest log, the
/// territory and the used-stone keys, so it saves nothing of its own.
/// </summary>
[DisallowMultipleComponent]
public sealed class HavenPlaceScenes : MonoBehaviour
{
    private const float DayCheckSeconds = 5f;

    private WorldAtlas atlas;
    private HavenPlaceVerbs verbs;
    private readonly Dictionary<string, WorldAtlas.Place> places = new Dictionary<string, WorldAtlas.Place>();
    private readonly Dictionary<HavenPlaceVerbs.Stage, GameObject> stages = new Dictionary<HavenPlaceVerbs.Stage, GameObject>();
    private readonly Dictionary<HavenPlaceVerbs.Find, PlaceFind> finds = new Dictionary<HavenPlaceVerbs.Find, PlaceFind>();
    private bool dirty = true;
    private int day = -1;
    private float nextDayCheck;

    /// <summary>The stage's stood object, or null while it doesn't show (tests and probes).</summary>
    public GameObject StoodFor(HavenPlaceVerbs.Stage stage) => stage != null && stages.TryGetValue(stage, out GameObject shown) ? shown : null;

    /// <summary>The find's stood object, or null while it isn't there (tests and probes).</summary>
    public PlaceFind StoodFor(HavenPlaceVerbs.Find find) =>
        find != null && finds.TryGetValue(find, out PlaceFind shown) && shown != null && shown.gameObject.activeSelf ? shown : null;

    public void Begin(WorldAtlas worldAtlas, HavenPlaceVerbs placeVerbs)
    {
        atlas = worldAtlas;
        verbs = placeVerbs;
        places.Clear();
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            if (place != null && !string.IsNullOrEmpty(place.id))
            {
                places[place.id] = place;
            }
        }

        Refresh();
    }

    private void OnEnable()
    {
        QuestManager.Changed += MarkDirty;
        Territory.Changed += MarkDirty;
    }

    private void OnDisable()
    {
        QuestManager.Changed -= MarkDirty;
        Territory.Changed -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void Update()
    {
        if (Time.unscaledTime >= nextDayCheck)
        {
            nextDayCheck = Time.unscaledTime + DayCheckSeconds;
            int today = GameSession.Home != null ? GameSession.Home.DaysPassed : 0;
            if (today != day)
            {
                day = today;
                dirty = true;
            }
        }

        if (dirty)
        {
            Refresh();
        }
    }

    /// <summary>Stands what should show and takes away what no longer should.</summary>
    public void Refresh()
    {
        dirty = false;
        if (atlas == null || verbs == null)
        {
            return;
        }

        foreach (HavenPlaceVerbs.PlaceScene scene in verbs.Scenes)
        {
            if (scene != null && places.TryGetValue(scene.placeId ?? "", out WorldAtlas.Place place))
            {
                RefreshScene(scene, place);
            }
        }

        foreach (HavenPlaceVerbs.Find find in verbs.Finds)
        {
            if (find != null && find.item != null && places.TryGetValue(find.placeId ?? "", out WorldAtlas.Place place))
            {
                RefreshFind(find, place);
            }
        }
    }

    private void RefreshScene(HavenPlaceVerbs.PlaceScene scene, WorldAtlas.Place place)
    {
        string region = RegionOf(place);
        // A later stage that shows and takes the earlier ones' dressing away hides every stage before it.
        int firstKept = 0;
        for (int i = scene.stages.Count - 1; i >= 0; i--)
        {
            HavenPlaceVerbs.Stage stage = scene.stages[i];
            if (stage != null && stage.hidesEarlier && HavenPlaceVerbs.Passes(stage.gate, stage.quest, region))
            {
                firstKept = i;
                break;
            }
        }

        for (int i = 0; i < scene.stages.Count; i++)
        {
            HavenPlaceVerbs.Stage stage = scene.stages[i];
            if (stage == null)
            {
                continue;
            }

            bool show = i >= firstKept && HavenPlaceVerbs.Passes(stage.gate, stage.quest, region);
            stages.TryGetValue(stage, out GameObject shown);
            if (show && shown == null)
            {
                stages[stage] = StandStage(place, stage, i);
            }
            else if (!show && shown != null)
            {
                Destroy(shown);
                stages.Remove(stage);
            }
        }
    }

    private GameObject StandStage(WorldAtlas.Place place, HavenPlaceVerbs.Stage stage, int index)
    {
        // Named for its place and stage: CuriousPlace remembers what was thought by its name and position.
        var holder = new GameObject($"{place.id} stage {index}");
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);
        holder.transform.SetPositionAndRotation(HavenPlaceVerbs.Ground(place.at + stage.offset), Quaternion.Euler(0f, stage.turn, 0f));
        if (stage.dressing != null)
        {
            GameObject dressing = Instantiate(stage.dressing, holder.transform);
            dressing.transform.localPosition = Vector3.zero;
            dressing.transform.localRotation = Quaternion.identity;
            Settle(dressing.transform);
        }

        if (stage.thoughts != null && stage.thoughts.Length > 0)
        {
            holder.AddComponent<CuriousPlace>().Configure(place.name, stage.thoughts, Mathf.Clamp(place.radius * 0.5f, 7f, 12f), remembered: true);
        }

        if (stage.smokeRise > 0f && verbs.Smoke != null)
        {
            // Drawn while the camera is within a lookout's wider view of it; nearer than that it is on screen.
            holder.AddComponent<HearthSmoke>().ConfigureColumn(stage.smokeAt, stage.smokeRise, 2.6f, 180f, verbs.Smoke);
        }

        // Someone works here: the land round it is kept in mind, so the smoke and the work show through the forgetting (the mist is
        // drawn over transparent things, and thick mist would hide the column). A place may also hold the mist back from itself.
        if (stage.smokeRise > 0f || stage.clearsMist > 0f)
        {
            holder.AddComponent<KeptPlace>().Configure(stage.smokeRise > 0f, stage.clearsMist);
        }

        holder.SetActive(true);
        return holder;
    }

    // The dressing's props each stand on the ground beneath them, so a scene laid out on flat ground sits on the slope it is stood on.
    // Only its direct children move, and only up or down: what a prop holds keeps its place on it.
    private static void Settle(Transform dressing)
    {
        foreach (Transform prop in dressing)
        {
            Vector3 at = prop.position;
            Vector3 ground = HavenPlaceVerbs.Ground(new Vector2(at.x, at.z));
            prop.position = new Vector3(at.x, ground.y + prop.localPosition.y, at.z);
        }
    }

    private void RefreshFind(HavenPlaceVerbs.Find find, WorldAtlas.Place place)
    {
        bool open = HavenPlaceVerbs.Passes(find.gate, find.quest, RegionOf(place));
        finds.TryGetValue(find, out PlaceFind shown);
        bool there = shown != null && shown.gameObject.activeSelf;
        if (open && !there && !PlaceFind.IsTaken(place, find))
        {
            if (shown != null)
            {
                Destroy(shown.gameObject);
            }

            PlaceFind stood = PlaceFind.Stand(transform, place, find, HavenPlaceVerbs.Ground(place.at + find.offset));
            if (stood != null)
            {
                finds[find] = stood;
            }
            else
            {
                finds.Remove(find);
            }
        }
        else if (!open && shown != null)
        {
            Destroy(shown.gameObject);
            finds.Remove(find);
        }
    }

    private string RegionOf(WorldAtlas.Place place) => atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y))?.name;
}
