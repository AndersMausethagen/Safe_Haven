using UnityEngine;

/// <summary>
/// Stands this span's happening at its place (HavenHappenings): its dressing and thoughts while its hours last, and what there is to
/// take, once in the span. Looks again as the days and hours turn and as regions are remembered. Stood with the place verbs
/// (HavenPlaceVerbs.Begin) on their holder; nothing of its own is saved.
/// </summary>
[DisallowMultipleComponent]
public sealed class HavenHappeningsKeeper : MonoBehaviour
{
    private const float LookSeconds = 5f;

    private WorldAtlas atlas;
    private HavenHappenings settings;
    private HavenHappenings.Happening showing;
    private int showingSpan = -1;
    private GameObject shown;
    private PlaceFind find;
    private HavenPlaceVerbs.Find findData;
    private float nextLook;
    private bool dirty = true;

    /// <summary>The happening standing now, or null (tests and probes).</summary>
    public HavenHappenings.Happening Showing => shown != null ? showing : null;

    public void Begin(WorldAtlas worldAtlas)
    {
        atlas = worldAtlas;
        settings = HavenHappenings.Current;
        dirty = true;
    }

    private void OnEnable() => Territory.Changed += MarkDirty;

    private void OnDisable() => Territory.Changed -= MarkDirty;

    private void MarkDirty() => dirty = true;

    private void Update()
    {
        if (!dirty && Time.unscaledTime < nextLook)
        {
            return;
        }

        nextLook = Time.unscaledTime + LookSeconds;
        dirty = false;
        Refresh();
    }

    /// <summary>Stands what this span and hour bring, and takes away what they no longer do.</summary>
    public void Refresh()
    {
        if (atlas == null || settings == null || GameSession.Home == null)
        {
            return;
        }

        int span = settings.SpanOf(GameSession.Home.DaysPassed);
        HavenHappenings.Happening now = settings.Pick(span, h => HavenHappenings.Possible(h, atlas));
        bool hours = now != null && PlaceBeacon.PlaysAt(now.when, AtmosphereConditions.Hour);
        if (now != showing || span != showingSpan || !hours)
        {
            Clear();
        }

        if (now == null || !hours || shown != null)
        {
            return;
        }

        WorldAtlas.Place place = HavenHappenings.Find(atlas, now.placeId);
        if (place == null)
        {
            return;
        }

        showing = now;
        showingSpan = span;
        shown = Stand(now, place, span);
        if (now.item != null)
        {
            // Once in the span: a yield of the span's length, under the happening's own name.
            findData = new HavenPlaceVerbs.Find
            {
                placeId = place.id, name = string.IsNullOrWhiteSpace(now.findName) ? now.item.DisplayName : now.findName,
                verb = now.findVerb, item = now.item, amount = now.amount, taken = now.taken, offset = now.offset + now.findOffset,
                marker = now.findMarker, everyDays = settings.SpanDays,
            };
            find = PlaceFind.Stand(transform, place, findData, HavenPlaceVerbs.Ground(place.at + findData.offset));
        }
    }

    private GameObject Stand(HavenHappenings.Happening happening, WorldAtlas.Place place, int span)
    {
        // Named for the happening and its span: its thought is had once each time it comes (CuriousPlace keys by name and place).
        var holder = new GameObject($"{place.id} {happening.id} {span}");
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);
        holder.transform.SetPositionAndRotation(HavenPlaceVerbs.Ground(place.at + happening.offset), Quaternion.Euler(0f, happening.turn, 0f));
        if (happening.dressing != null)
        {
            GameObject dressing = Instantiate(happening.dressing, holder.transform);
            dressing.transform.localPosition = Vector3.zero;
            dressing.transform.localRotation = Quaternion.identity;
            HavenPlaceScenes.Settle(dressing.transform);
        }

        if (happening.thoughts != null && happening.thoughts.Length > 0)
        {
            holder.AddComponent<CuriousPlace>().Configure(place.name, happening.thoughts, Mathf.Clamp(place.radius * 0.5f, 7f, 12f), remembered: true);
        }

        holder.SetActive(true);
        return holder;
    }

    private void Clear()
    {
        if (shown != null)
        {
            Destroy(shown);
        }

        if (find != null)
        {
            Destroy(find.gameObject);
        }

        shown = null;
        find = null;
        findData = null;
        showing = null;
        showingSpan = -1;
    }
}
