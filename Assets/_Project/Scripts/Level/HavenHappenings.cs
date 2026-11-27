using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Foretold happenings at the Haven's places (Docs/HavenWorldStory_2026-11-26.md §6, the Haven events director): now and then something
/// is going on out in the land: bees swarmed in the Mother Maple, the perch running at Stillwater, a pedlar resting on the old road,
/// lights over the tarn at night. One at a time, for a span of days, at a place whose region is remembered (home's always are), told on
/// the homecoming card; peaceful, and something to go and see or take. Which one each span is drawn from the span itself (Pick), so
/// nothing is saved but what the journal notes (told) and the place's find (taken). Stood by HavenHappeningsKeeper.
/// Resources/HavenHappenings.
/// </summary>
[CreateAssetMenu(fileName = "HavenHappenings", menuName = "Safe Haven/World/Haven Happenings")]
public sealed class HavenHappenings : GameSettings<HavenHappenings>
{
    [Serializable]
    public sealed class Happening
    {
        [Tooltip("Stable: what was told and taken is remembered by it.")]
        public string id;
        [Tooltip("The place (WorldAtlas.Place.id).")]
        public string placeId;
        [Tooltip("Only once the place's region is remembered (home's places always are). Off: also in forgotten land (a sight to lure).")]
        public bool needsRemembered = true;
        [Tooltip("Only once this quest has started (empty: from the start): the tarn's lights belong to the story's turn north.")]
        public QuestDefinition afterQuest;
        [Tooltip("Only at these hours (night from 20 to 6): lights over the tarn.")]
        public HavenPlaceVerbs.When when = HavenPlaceVerbs.When.Always;
        [Tooltip("What shows there (props, a light, someone resting), round the place's centre plus Offset.")]
        public GameObject dressing;
        public Vector2 offset;
        public float turn;
        [Tooltip("Thought on coming near.")]
        [TextArea(2, 4)] public string[] thoughts = Array.Empty<string>();
        [Header("Something to take (empty item: nothing)")]
        public string findName = "";
        public string findVerb = "Take";
        public ItemDefinition item;
        [Min(1)] public int amount = 1;
        [TextArea(1, 3)] public string taken = "";
        public Vector2 findOffset;
        [Tooltip("What shows where it lies: empty shows the item's own world model.")]
        public GameObject findMarker;
        [Header("Told")]
        [Tooltip("The homecoming card's line, once in its span.")]
        [TextArea(1, 3)] public string foretold = "";
    }

    [Tooltip("Days one happening lasts; a new one may come with each span.")]
    [SerializeField, Min(1)] private int spanDays = 3;
    [Tooltip("The share of spans that bring one at all (the rest are quiet).")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.65f;
    [SerializeField] private List<Happening> happenings = new List<Happening>();

    public int SpanDays => spanDays;
    public float Chance => chance;
    public IReadOnlyList<Happening> Happenings => happenings;

    /// <summary>The span of days a day at home falls in.</summary>
    public int SpanOf(int daysAtHome) => Mathf.Max(0, daysAtHome) / spanDays;

    /// <summary>
    /// The happening of a span, or null for a quiet one: drawn from the span alone (the same every time it is asked), among those
    /// <paramref name="possible"/> allows now, never the same as the span before's.
    /// </summary>
    public Happening Pick(int span, Func<Happening, bool> possible)
    {
        if (span < 0 || Quiet(span))
        {
            return null;
        }

        // Drawn in a chain from just after the last quiet span (where nothing came before), so a span and the one after it always
        // agree on what came first, and the one after never draws the same again.
        int start = span;
        while (start > 0 && !Quiet(start - 1) && span - start < 500)
        {
            start--;
        }

        var all = happenings.FindAll(h => h != null && !string.IsNullOrEmpty(h.id));
        Happening previous = null, now = null;
        for (int s = start; s <= span; s++)
        {
            now = Draw(s, previous, all, possible);
            previous = now;
        }

        return now;
    }

    private bool Quiet(int span) => new System.Random(unchecked(span * 92821 + 7)).NextDouble() >= chance;

    // The span's own draw: from its seed on through the list, the first that isn't the one before and is possible now.
    private static Happening Draw(int span, Happening previous, List<Happening> all, Func<Happening, bool> possible)
    {
        if (all.Count == 0)
        {
            return null;
        }

        var draw = new System.Random(unchecked(span * 92821 + 7));
        draw.NextDouble();
        int first = draw.Next() % all.Count;
        for (int k = 0; k < all.Count; k++)
        {
            Happening candidate = all[(first + k) % all.Count];
            if (candidate != previous && (possible == null || possible(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Whether a happening can be at its place this span: its region remembered if it asks it. (Its hours only say when it
    /// shows: the lights over the tarn are the span's happening all day, and seen at night.)</summary>
    public static bool Possible(Happening happening, WorldAtlas atlas)
    {
        if (happening == null || atlas == null
            || (happening.afterQuest != null && GameSession.Quests.StatusOf(happening.afterQuest) == QuestStatus.NotStarted))
        {
            return false;
        }

        WorldAtlas.Place place = Find(atlas, happening.placeId);
        if (place == null)
        {
            return false;
        }

        string region = atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y))?.name;
        return !happening.needsRemembered || HavenLand.IsRemembered(region);
    }

    /// <summary>The homecoming card's line for the happening of this span, once (noted in the journal), or null.</summary>
    public static string Foretell(JournalRecord journal)
    {
        HavenHappenings settings = Current;
        WorldAtlas atlas = Territory.Atlas;
        if (settings == null || atlas == null || journal == null || GameSession.Home == null)
        {
            return null;
        }

        int span = settings.SpanOf(GameSession.Home.DaysPassed);
        Happening now = settings.Pick(span, h => Possible(h, atlas));
        if (now == null || string.IsNullOrWhiteSpace(now.foretold))
        {
            return null;
        }

        string key = $"haven.happening.{now.id}.{span}";
        if (journal.HasNoticed(key))
        {
            return null;
        }

        journal.NoteNoticed(key);
        return now.foretold;
    }

    public static WorldAtlas.Place Find(WorldAtlas atlas, string placeId)
    {
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            if (place != null && place.id == placeId)
            {
                return place;
            }
        }

        return null;
    }

    /// <summary>Sets the list at once (the agent's eval that wrote it, tests).</summary>
    public void Set(List<Happening> written, int days, float share)
    {
        happenings = written ?? new List<Happening>();
        spanDays = Mathf.Max(1, days);
        chance = Mathf.Clamp01(share);
    }
}
