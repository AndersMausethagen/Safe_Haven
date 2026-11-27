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

    /// <summary>When a find or a stage of a place's scene shows (Docs/HavenWorldStory_2026-11-26.md steps 3-4): always, once a quest
    /// has started or is done, or once the place's region is remembered.</summary>
    public enum Gate
    {
        Always,
        QuestStarted,
        QuestDone,
        Remembered,
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
        [Tooltip("What is taken. Empty: a thing only to use (a lantern to light, marks to read), shown by its Marker, counted for quests " +
                 "by its Quest Target.")]
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
        [Tooltip("Taking it counts as using this, for quests (Use Object).")]
        public QuestTargetDefinition questTarget;
        [Tooltip("When it lies there: always, or only once Quest has started or is done (the clamp to light only on the errand to " +
                 "light it), or once the place's region is remembered.")]
        public Gate gate = Gate.Always;
        public QuestDefinition quest;
        [Tooltip("0: taken once for good. More: a work site's yield, there once in every so many days at home (the relit clamp's " +
                 "charcoal).")]
        [Min(0)] public int everyDays;
        [Tooltip("A yield left untaken builds up for this many spans at most (taken together, so a walk missed isn't a yield lost). " +
                 "1: only this span's.")]
        [Min(1)] public int keepsSpans = 3;
        [Tooltip("Only at these hours (night from 20 to 6, as a place's sounds): a lantern lit after dark.")]
        public When when = When.Always;
        [Tooltip("What doing it takes from the bag, used up (planks laid across the fen). Empty: nothing.")]
        public ItemDefinition costItem;
        [Min(1)] public int costAmount = 1;
    }

    /// <summary>
    /// A place's own scene (Docs/HavenWorldStory_2026-11-26.md §7.3): what the player thinks there in place of its kind's thoughts, and
    /// the stages its story goes through, each standing its dressing (a prefab of props laid out round the place's centre) and its
    /// thoughts once its gate opens. A later stage can take the earlier ones' dressing away (the ruin gives way to the rebuilt hut).
    /// </summary>
    [Serializable]
    public sealed class PlaceScene
    {
        [Tooltip("The place (WorldAtlas.Place.id).")]
        public string placeId;
        [Tooltip("Thought here in place of its kind's (several places share a kind: the Charcoal Camp is a Woodland Camp). Empty: the kind's.")]
        [TextArea(2, 4)] public string[] thoughts = Array.Empty<string>();
        public List<Stage> stages = new List<Stage>();
    }

    [Serializable]
    public sealed class Stage
    {
        [Tooltip("What this stage is, for whoever edits it.")]
        public string note = "";
        public Gate gate = Gate.Always;
        public QuestDefinition quest;
        [Tooltip("Props laid out round the place's centre (its own colliders kept): the scene itself.")]
        public GameObject dressing;
        [Tooltip("Metres from the place's centre (x, z), and degrees the dressing is turned.")]
        public Vector2 offset;
        public float turn;
        [Tooltip("Thought on first coming near once this stage shows (its own, remembered as the place's are).")]
        [TextArea(2, 4)] public string[] thoughts = Array.Empty<string>();
        [Tooltip("More than 0: a column of smoke rises this high from Smoke At (from the stage's centre): the place is worked again, and " +
                 "keeps the land round it in mind (KeptPlace: thinner mist, some colour). From the game's camera it shows as the place " +
                 "comes into view, and far off only from a lookout's wider view.")]
        [Min(0f)] public float smokeRise;
        public Vector3 smokeAt = new Vector3(0f, 1.2f, 0f);
        [Tooltip("The mist stands back from it: this share of the mist that would lie there is held back round it (KeptPlace), for a " +
                 "place in low ground by water, where the mist pools thick even over remembered land and would hide the scene.")]
        [Range(0f, 1f)] public float clearsMist;
        [Tooltip("The earlier stages' dressing goes when this one shows.")]
        public bool hidesEarlier;
        [Tooltip("Who works this place once it shows (PlaceWorkers): they walk out to it in their free time, in Work Hours, and are home " +
                 "at night. Nobody while they aren't home yet.")]
        public ResidentDefinition worker;
        [Tooltip("The hours they are out at it (from, until).")]
        public Vector2 workHours = new Vector2(14f, 18f);
        [Tooltip("Where they stand to work, from the stage's centre (x, z).")]
        public Vector2 workAt = new Vector2(1.5f, -1.5f);
        [Tooltip("A creature that keeps this place while the stage shows (the Old Grey at its den): stood at the stage's centre, at home " +
                 "there. Gone with the stage only while it lives: a body killed keeps its loot.")]
        public GameObject creature;
    }

    [Serializable]
    public sealed class PlaceTarget
    {
        [Tooltip("The place (WorldAtlas.Place.id).")]
        public string placeId;
        [Tooltip("Walking into it counts as reaching this, for quests (Reach Place).")]
        public QuestTargetDefinition target;
    }

    [SerializeField] private List<Kind> kinds = new List<Kind>();
    [SerializeField] private List<Find> finds = new List<Find>();
    [SerializeField] private List<PlaceTarget> targets = new List<PlaceTarget>();
    [SerializeField] private List<PlaceScene> scenes = new List<PlaceScene>();
    [Tooltip("What a worked place's smoke is drawn with (a hearth's).")]
    [SerializeField] private Material smoke;
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
    public IReadOnlyList<PlaceTarget> Targets => targets;
    public IReadOnlyList<PlaceScene> Scenes => scenes;
    public Material Smoke => smoke;

    /// <summary>The place's own scene, or null.</summary>
    public PlaceScene SceneOf(string placeId)
    {
        foreach (PlaceScene scene in scenes)
        {
            if (scene != null && scene.placeId == placeId)
            {
                return scene;
            }
        }

        return null;
    }

    /// <summary>True when a find or stage with this gate shows now, at a place in <paramref name="region"/>.</summary>
    public static bool Passes(Gate gate, QuestDefinition quest, string region)
    {
        switch (gate)
        {
            case Gate.QuestStarted:
                return quest != null && GameSession.Quests.StatusOf(quest) != QuestStatus.NotStarted;
            case Gate.QuestDone:
                return quest != null && GameSession.Quests.StatusOf(quest) == QuestStatus.Completed;
            case Gate.Remembered:
                return HavenLand.IsRemembered(region);
            default:
                return true;
        }
    }

    /// <summary>What reaching a place counts as for quests, or null.</summary>
    public QuestTargetDefinition TargetOf(string placeId)
    {
        foreach (PlaceTarget entry in targets)
        {
            if (entry != null && entry.placeId == placeId)
            {
                return entry.target;
            }
        }

        return null;
    }
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

    /// <summary>Sets the places' scenes at once (the agent's eval that wrote them, tests).</summary>
    public void SetScenes(List<PlaceScene> written) => scenes = written ?? new List<PlaceScene>();

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
            PlaceScene scene = verbs.SceneOf(place.id);
            string[] thoughts = scene != null && scene.thoughts.Length > 0 ? scene.thoughts : kind?.thoughts ?? Array.Empty<string>();
            bool lookout = kind != null && kind.lookout;
            SoundDefinition sound = kind?.sound;
            if (thoughts.Length > 0 || lookout || sound != null)
            {
                Stand(place, kind, thoughts, Ground(place.at), verbs);
            }
        }

        // The finds and the scenes' stages come and go with the story (a quest started, a region remembered), so they are stood by a
        // keeper that looks again whenever it moves on.
        stood.AddComponent<HavenPlaceScenes>().Begin(atlas, verbs);
        // And what is going on out there this span (HavenHappenings): a swarm, a fish run, a pedlar on the old road.
        stood.AddComponent<HavenHappeningsKeeper>().Begin(atlas);
    }

    private static void Stand(WorldAtlas.Place place, Kind kind, string[] thoughts, Vector3 at, HavenPlaceVerbs verbs)
    {
        // Named for its place: CuriousPlace remembers what was thought by its name and position.
        var holder = new GameObject(place.id);
        holder.transform.SetParent(stood.transform, false);
        holder.transform.position = at;
        bool lookout = kind != null && kind.lookout;
        // The thought first: a CuriousPlace answers the Interactable it finds as it wakes, and a lookout's press is the look out, not
        // "Look closer". A place with more to think than its first thought gets "Look closer" of its own.
        if (thoughts.Length > 0)
        {
            if (!lookout && thoughts.Length > 1)
            {
                holder.AddComponent<Interactable>().Configure(InteractionAnimation.GatherLow, 0f, 4f, false, InteractionKind.Use);
            }

            holder.AddComponent<CuriousPlace>().Configure(place.name, thoughts, Mathf.Clamp(place.radius * 0.5f, 7f, 12f), remembered: true);
        }

        if (lookout)
        {
            HavenLookout.AddTo(holder, place, verbs);
        }

        if (kind?.sound != null)
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
