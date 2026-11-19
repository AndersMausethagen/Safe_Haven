using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What each kind of named place in the Haven's land does (Docs/HavenExplorationReview_2026-11-19.md §2): its thoughts as the player
/// comes near (CuriousPlace), a lookout to open the map from (HavenLookout), a sound that carries from it (PlaceBeacon), a thing left
/// there to take once (PlaceFind). Stood at runtime at every place of the scene's atlas (Begin, from ExperienceTracker beside
/// TerritorySpawner), so the scene file never changes and a regeneration of the world can't wipe them; the place props themselves
/// are shared with the raids, so they carry nothing of the Haven's. Resources/HavenPlaceVerbs.
/// </summary>
[CreateAssetMenu(fileName = "HavenPlaceVerbs", menuName = "Safe Haven/World/Haven Place Verbs")]
public sealed class HavenPlaceVerbs : ScriptableObject
{
    public enum When
    {
        Always,
        Day,
        Night,
    }

    [Serializable]
    public sealed class Kind
    {
        [Tooltip("The atlas's kind (WorldAtlas.Place.kind), such as Old Quarry.")]
        public string kind;
        [Tooltip("What the player thinks on coming near, one at a time; the next on each \"Look closer\".")]
        [TextArea(2, 4)] public string[] thoughts = Array.Empty<string>();
        [Tooltip("A lookout: press E to look out over the land (HavenLookout).")]
        public bool lookout;
        [Tooltip("Heard from the place now and then, from as far as the sound carries.")]
        public SoundDefinition sound;
        public When soundWhen = When.Always;
        public Vector2 soundEvery = new Vector2(12f, 25f);
    }

    [Serializable]
    public sealed class Find
    {
        [Tooltip("The place (WorldAtlas.Place.id) it lies at.")]
        public string placeId;
        [Tooltip("What it is called on the prompt.")]
        public string name = "Cut stone";
        [Tooltip("The prompt's verb.")]
        public string verb = "Take";
        public ItemDefinition item;
        [Min(1)] public int amount = 1;
        [Tooltip("Said as it is taken.")]
        [TextArea(1, 3)] public string taken = "";
        [Tooltip("Metres from the place's centre (x, z), so it sits by the props rather than in them.")]
        public Vector2 offset;
        [Tooltip("What shows where it lies (an art prefab, its colliders kept): empty shows the item's own world model, if it has one.")]
        public GameObject marker;
        [Min(0.05f)] public float markerScale = 1f;
        [Tooltip("Degrees it is turned about the vertical.")]
        public float markerTurn;
    }

    [SerializeField] private List<Kind> kinds = new List<Kind>();
    [SerializeField] private List<Find> finds = new List<Find>();
    [Header("Lookouts")]
    [Tooltip("Metres of fog lifted round a lookout.")]
    [SerializeField, Min(10f)] private float lookoutReveal = 110f;
    [Tooltip("Places within this many metres of a lookout become seen: on the map in grey until walked to.")]
    [SerializeField, Min(10f)] private float lookoutSees = 180f;
    [Tooltip("Insight for a lookout's first use.")]
    [SerializeField, Min(0)] private int lookoutInsight = 30;
    [Tooltip("Within this many metres of a lookout the view pulls out (distance, a share of the usual) and flattens (degrees).")]
    [SerializeField, Min(1f)] private float lookoutViewRadius = 6f;
    [SerializeField, Range(1f, 2f)] private float lookoutViewDistance = 1.9f;
    [SerializeField, Range(-30f, 0f)] private float lookoutViewTilt = -16f;

    public IReadOnlyList<Kind> Kinds => kinds;
    public IReadOnlyList<Find> Finds => finds;
    public float LookoutReveal => lookoutReveal;
    public float LookoutSees => lookoutSees;
    public int LookoutInsight => lookoutInsight;
    public float LookoutViewRadius => lookoutViewRadius;
    public float LookoutViewDistance => lookoutViewDistance;
    public float LookoutViewTilt => lookoutViewTilt;

    public static HavenPlaceVerbs Current => current != null ? current : current = Resources.Load<HavenPlaceVerbs>("HavenPlaceVerbs");
    private static HavenPlaceVerbs current;
    private static GameObject stood;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        current = null;
        stood = null;
    }

    public Kind KindOf(string placeKind)
    {
        foreach (Kind kind in kinds)
        {
            if (kind != null && kind.kind == placeKind)
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Sets the data at once (the agent's eval that wrote it, tests).</summary>
    public void Set(List<Kind> described, List<Find> left)
    {
        kinds = described ?? new List<Kind>();
        finds = left ?? new List<Find>();
    }

    /// <summary>Stands what each place does at every place of this scene's atlas (none without one). Runs on every scene with a player;
    /// a scene's objects go with it, so it starts again on the next.</summary>
    public static void Begin(string sceneName)
    {
        HavenPlaceVerbs verbs = Current;
        WorldAtlas atlas = WorldAtlas.For(sceneName);
        if (stood != null)
        {
            Destroy(stood);
        }

        if (verbs == null || atlas == null)
        {
            return;
        }

        stood = new GameObject("Haven Place Verbs");
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            Kind kind = verbs.KindOf(place.kind);
            Vector3 at = Ground(place.at);
            if (kind != null && (kind.thoughts.Length > 0 || kind.lookout || kind.sound != null))
            {
                Stand(place, kind, at, verbs);
            }

            foreach (Find find in verbs.finds)
            {
                if (find != null && find.placeId == place.id && find.item != null)
                {
                    PlaceFind.Stand(stood.transform, place, find, Ground(place.at + find.offset));
                }
            }
        }
    }

    private static void Stand(WorldAtlas.Place place, Kind kind, Vector3 at, HavenPlaceVerbs verbs)
    {
        // Named for its place: CuriousPlace remembers what was thought by its name and position.
        var holder = new GameObject(place.id);
        holder.transform.SetParent(stood.transform, false);
        holder.transform.position = at;
        if (kind.lookout)
        {
            HavenLookout.AddTo(holder, place, verbs);
        }

        if (kind.thoughts.Length > 0)
        {
            holder.AddComponent<CuriousPlace>().Configure(place.name, kind.thoughts, Mathf.Clamp(place.radius * 0.5f, 7f, 12f), remembered: true);
        }

        if (kind.sound != null)
        {
            holder.AddComponent<PlaceBeacon>().Configure(kind.sound, kind.soundWhen, kind.soundEvery);
        }
    }

    /// <summary>The ground at a point of the atlas (world x and z): the terrain's height, or 0 with none under it.</summary>
    public static Vector3 Ground(Vector2 at)
    {
        var point = new Vector3(at.x, 0f, at.y);
        foreach (Terrain terrain in Terrains.Active)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 corner = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            if (point.x >= corner.x && point.x <= corner.x + size.x && point.z >= corner.z && point.z <= corner.z + size.z)
            {
                point.y = terrain.SampleHeight(point) + corner.y;
                break;
            }
        }

        return point;
    }

    /// <summary>The compass word for a direction on the ground ("north", "south-west").</summary>
    public static string Compass(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        if (d.sqrMagnitude < 0.01f)
        {
            return "here";
        }

        // 0 = north (+z), clockwise.
        float angle = Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);
        string[] words = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        return words[Mathf.RoundToInt(angle / 45f) % 8];
    }
}
