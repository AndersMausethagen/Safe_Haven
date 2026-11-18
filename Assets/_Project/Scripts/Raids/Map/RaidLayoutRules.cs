using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a kind of site finds its place on a generated map.</summary>
public enum RaidSitePlacement
{
    /// <summary>In one of the map's clearings.</summary>
    Room = 0,
    /// <summary>On a trail.</summary>
    OnPath = 1,
    /// <summary>In a pocket off a trail, with a short side trail leading to it.</summary>
    BesidePath = 2,
    /// <summary>In a clearing only one trail leads to, for what should be worth the walk.</summary>
    DeadEnd = 3,
}

/// <summary>The overall plan of a generated map: how its clearings are joined. Saved nowhere, so safe to reorder.</summary>
public enum RaidLayoutShape
{
    /// <summary>Every trail meets at one great clearing.</summary>
    Hub = 0,
    /// <summary>The clearings stand in a ring around wild ground nobody crosses.</summary>
    Ring = 1,
    /// <summary>One long trail runs across the map, with side trails off it.</summary>
    Spine = 2,
    /// <summary>A tangle of short trails with many ways round.</summary>
    Thicket = 3,
    /// <summary>
    /// A long valley between high walls with a river down its middle (RaidLayoutRules › Valley): the start at its mouth, the main way
    /// out at its head, clearings on alternating banks, so every way up crosses the river at a ford or a bridge.
    /// </summary>
    Valley = 4,
}

/// <summary>
/// What a district of a generated map is for (Docs/WorldDesignReview_2026-11-17.md G1): its role sets how many sites of each kind it
/// holds, so danger rises from the start towards the apex as in Valheim's distance from the centre. Saved by number in moods and rules.
/// </summary>
public enum RaidDistrictRole
{
    /// <summary>On a mood: take the role the district's distance from the start gives it.</summary>
    Any = 0,
    /// <summary>The start's district: the way in, quieter, resources rather than camps.</summary>
    Approach = 1,
    /// <summary>The land between: wild ground, some of everything.</summary>
    Wild = 2,
    /// <summary>Where people built: camps, caches and events gather (a mood asks for it; distance never gives it).</summary>
    Compound = 3,
    /// <summary>The district round the apex: camps and patrols, the great danger's ground.</summary>
    Lair = 4,
}

/// <summary>How much more or less often one kind of site stands in a district (1 as often as anywhere, 0 never).</summary>
[Serializable]
public sealed class RaidSiteDensity
{
    public RaidSiteKind kind = RaidSiteKind.EnemyCamp;
    [Tooltip("Weight of this kind in the district against the others' (1: an even share; 3 against 1: three of every four sites).")]
    [Min(0f)] public float density = 1f;
}

/// <summary>The site weights of one district role on every map of the rules (RaidLayoutRules › District Roles).</summary>
[Serializable]
public sealed class RaidDistrictRule
{
    public RaidDistrictRole role = RaidDistrictRole.Lair;
    [Tooltip("Weights per site kind in districts of this role; a kind not listed weighs 1. Multiplied by the district mood's own.")]
    public List<RaidSiteDensity> siteDensity = new List<RaidSiteDensity>();
}

/// <summary>
/// A fen's ground (the Moss Marsh, Docs/NextRaidResearch_2026-11-17.md §11.3): the trails and clearings stand a little above one water
/// level, the wild ground between them sinks under it, and hummocks and long peat strings (the Norwegian string bog) rise back out. One
/// water sheet covers the map (RaidMapGenerator.PlaceFenWater), so its pools are the ground below it. Works with any shape.
/// </summary>
[Serializable]
public sealed class RaidFenRule
{
    [Tooltip("On: the fen's ground, the wild ground sunk under one water level. Off: it rises in ridges between the trails, as in the Woods.")]
    public bool enabled;
    [Tooltip("Metres the trails and clearings stand above the water. Keep Hills smaller than this, or a trail dips under.")]
    [Min(0.1f)] public float aboveWater = 0.6f;
    [Tooltip("Metres the wild ground sinks below the water at its deepest.")]
    [Min(0f)] public float sink = 1.2f;
    [Tooltip("Metres from a trail's or clearing's edge to the fen's deepest: the bank down into the water.")]
    [Min(0.5f)] public float sinkWidth = 7f;
    [Tooltip("Share of the wild ground that stands above the water as hummocks, 0 to 1.")]
    [Range(0f, 1f)] public float hummocks = 0.3f;
    [Tooltip("Metres across a patch of hummocks.")]
    [Min(1f)] public float hummockScale = 7f;
    [Tooltip("Metres a hummock or a string stands above the water.")]
    [Min(0f)] public float hummockHeight = 0.35f;
    [Tooltip("How strongly long strings of firmer peat cross the fen, 0 (none) to 1.")]
    [Range(0f, 1f)] public float strings = 0.6f;
    [Tooltip("Metres between the strings.")]
    [Min(4f)] public float stringSpacing = 16f;
    [Tooltip("The fen's water. Empty: the rules' Pond Material.")]
    public Material water;
    [Tooltip("How wet the fen's ground stays where it stands well above the water (0 to 1), for the plants that love the wet. Low keeps " +
             "reeds and sedge crowding the banks; high spreads them over the whole fen.")]
    [Range(0f, 1f)] public float damp = 0.25f;
}

/// <summary>How a Valley map is shaped (RaidLayoutShape.Valley): its floor, its walls and the river down its middle.</summary>
[Serializable]
public sealed class RaidValleyRule
{
    [Tooltip("How wide the valley floor is from wall foot to wall foot, in metres, least and most (times the mood's Valley Width).")]
    public Vector2 floorWidth = new Vector2(70f, 90f);
    [Tooltip("How high the walls rise above the floor, in metres, least and most; each side rolls its own.")]
    public Vector2 wallHeight = new Vector2(18f, 24f);
    [Tooltip("Metres across which a wall climbs from its foot to its top.")]
    [Min(4f)] public float wallRun = 26f;
    [Tooltip("How many ledges a wall climbs in: steep faces of scree and rock with level benches between.")]
    [Range(1, 5)] public int benches = 3;
    [Tooltip("How far the valley swings from side to side, in metres, least and most.")]
    public Vector2 meander = new Vector2(10f, 20f);
    [Tooltip("Metres from one bend of the valley to the next, least and most.")]
    public Vector2 bendLength = new Vector2(110f, 160f);
    [Tooltip("How far the floor climbs from the mouth to the head, in metres, least and most: the river runs down it.")]
    public Vector2 tilt = new Vector2(5f, 9f);
    [Tooltip("How wide the river is, in metres, least and most.")]
    public Vector2 riverWidth = new Vector2(6f, 9f);
    [Tooltip("How deep the river is in its middle, in metres, least and most (fords are shallower: Rules › Ford Depth).")]
    public Vector2 riverDepth = new Vector2(1.5f, 2.0f);
    [Tooltip("How many of the trails' river crossings carry a bridge, least and most; the rest are fords. The short way gets them first.")]
    public Vector2Int bridges = new Vector2Int(1, 2);
    [Tooltip("The bridge laid over a crossing: its deck along local Z, its length Bridge Length, its deck top at Bridge Deck above its pivot.")]
    public GameObject bridgePrefab;
    [Tooltip("Metres the bridge prefab spans along its local Z at scale 1.")]
    [Min(1f)] public float bridgeLength = 14f;
    [Tooltip("Metres from the bridge prefab's pivot up to its deck.")]
    public float bridgeDeck = 2f;
    [Tooltip("What the bridge nearest the valley's head becomes (Troll Valley: the Brukam's bridge, GDD §10.7): its guards come out " +
             "there when the raid begins, and its prefab (a camp, a hoard) stands on the far bank. Empty: every bridge is only a bridge.")]
    public RaidSetPieceDefinition bossBridge;
    [Tooltip("What sits at the near end of every other bridge for the whole raid (Troll Valley: the Toll Troll: pay, riddle or fight; " +
             "or ford somewhere else). Empty: the bridges are free.")]
    public RaidEventDefinition bridgeToll;
    [Tooltip("Metres between the Boulder Kit's rocks set into each wall, along its foot, so the walls read as rock rather than painted " +
             "slopes. Only to look at: no colliders. 0: bare walls.")]
    [Min(0f)] public float wallCrags;
    [Tooltip("How far up a wall its crags go, in metres from its foot across the map. Keep it short: rock far from where anyone walks " +
             "costs objects and shows only from above.")]
    [Min(1f)] public float wallCragReach = 25f;
    [Tooltip("Only faces steeper than this (degrees) get a crag; the ledges between stay bare. Never below 46°: crags have no " +
             "colliders, so on a slope creatures can climb (45°) they would be walked through.")]
    [Range(10f, 80f)] public float wallCragSlope = 38f;
    [Tooltip("Chance that a crag's place also has a great block fallen at the wall's foot: solid, kept off trails, clearings and the river.")]
    [Range(0f, 1f)] public float wallBlocks = 0.1f;
    [Tooltip("The most trails that cross the river (Docs/WorldDesignReview_2026-11-17.md G8): the short way's crossings first, then the " +
             "long way's, then the rest; a trail beyond it is dropped unless it is the only way somewhere. Few crossings make the river an " +
             "edge and each bridge's toll count. 0: no limit (as before).")]
    [Min(0)] public int maxCrossings;
}

/// <summary>How many sites of one kind a generated map holds, and where they go.</summary>
[Serializable]
public sealed class RaidSiteQuota
{
    public RaidSiteKind kind = RaidSiteKind.EnemyCamp;
    public RaidSitePlacement placement = RaidSitePlacement.BesidePath;
    [Min(0)] public int count = 4;
    [Tooltip("Metres kept between two sites of this kind.")]
    [Min(0f)] public float separation = 20f;
    [Tooltip("Metres kept from the player's start.")]
    [Min(0f)] public float fromStart = 30f;
    [Tooltip("Beside a Path: how far from the trail, least and most, in metres.")]
    public Vector2 offPath = new Vector2(9f, 16f);
    [Tooltip("Radius of open, level ground kept around the site, in metres.")]
    [Min(0f)] public float clearing = 7f;
    [Tooltip("Take the highest of the places it could go: a cache on a ledge, a reward up the hill.")]
    public bool prefersHeight;
    [Tooltip("On maps with terraces: first at the top of the ramps, looking down on the way up (archers love it).")]
    public bool overlooksRamps;
}

/// <summary>A landmark a generated map can have: something tall to find the way by, on a hill top or in the wild ground.</summary>
[Serializable]
public sealed class RaidLandmarkRule
{
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Scale, least and most.")]
    public Vector2 scale = Vector2.one;
    [Tooltip("Share of its height sunk into the ground.")]
    [Range(0f, 0.5f)] public float sink = 0.05f;
    [Tooltip("Strip what can be gathered, chopped or used from it, so it is only to see (and bump into).")]
    public bool onlyToSee = true;

    [Header("For a top-down camera (G4)")]
    [Tooltip("Stands at a node (a clearing three or more trails meet, the hub, the apex), one in each district first, each kind once a " +
             "map while another is left: the top-down camera sees little of the land, so a landmark has to stand where the ways meet. " +
             "Off: on a hill top or in the wild ground, as before (Rules › Landmark Count).")]
    public bool atNodes;
    [Tooltip("Metres of open ground kept round it at a node (Valheim's ClearArea), so the trunk or rock stands free. 0: none.")]
    [Min(0f)] public float clearing;
    [Tooltip("Metres within which the player finds it, and from then on the minimap marks it (60-80, like a lookout's reveal). 0: never marked.")]
    [Min(0f)] public float revealRadius;
    [Tooltip("Marked on the minimap from the start, wherever the player is.")]
    public bool knownAtStart;
    [Tooltip("The minimap's mark for it. Empty: a dot.")]
    public Sprite icon;
    [Tooltip("A sound it makes now and then, heard as far as the sound's Audible Distance (60-100 m suits: falls, a bell, crows, drums). " +
             "Empty: silent.")]
    public SoundDefinition sound;
    [Tooltip("Seconds between its sounds, least and most.")]
    public Vector2 soundEvery = new Vector2(8f, 20f);
    [Tooltip("The sound loops for as long as it stands (falls, a fire) instead of now and then.")]
    public bool soundLoops;

    /// <summary>True when it needs the runtime part (RaidMapLandmark): a minimap mark or a sound.</summary>
    public bool Marked => revealRadius > 0f || knownAtStart || !(sound is null);
}

/// <summary>One plan a map can be laid out by, with how likely it is and what the briefing says of it.</summary>
[Serializable]
public sealed class RaidShapeRule
{
    public RaidLayoutShape shape;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Chance of an extra trail between two clearings close enough, so there is more than one way round.")]
    [Range(0f, 1f)] public float loopChance = 0.2f;
    [Tooltip("Told in the briefing.")]
    [TextArea] public string briefing;
}

/// <summary>What the forest is like this run: rolled on top of the shape, so the same plan never looks the same.</summary>
[Serializable]
public sealed class RaidMapMood
{
    public string name = "Old Growth";
    [Tooltip("Told in the briefing, after the name.")]
    [TextArea] public string briefing;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Multiplies how dense the forest grows.")]
    [Min(0f)] public float forest = 1f;
    [Tooltip("Multiplies the metres between trees.")]
    [Min(0.1f)] public float treeSpacing = 1f;
    [Tooltip("Weight of each tree in the map's list, in order. Empty keeps the map's own.")]
    public List<float> treeWeights = new List<float>();
    [Tooltip("Multiplies the height of the ground between the trails. Above 1 it stands as cliffs.")]
    [Min(0f)] public float ridges = 1f;
    [Tooltip("Multiplies the size of clearings.")]
    [Min(0.1f)] public float rooms = 1f;
    [Tooltip("Multiplies the width of trails.")]
    [Min(0.1f)] public float trails = 1f;
    [Tooltip("Multiplies the rocks and logs lying in open forest.")]
    [Min(0f)] public float rocks = 1f;
    [Tooltip("Banks of fog laid over clearings, on top of the ones the scene places.")]
    [Min(0)] public int fogBanks;
    [Tooltip("Multiplies how many great hills rise on the map, and how high (Rules › Hills).")]
    [Min(0f)] public float hills = 1f;
    [Tooltip("Multiplies how many clusters of great boulders lie about (Rules › Boulders).")]
    [Min(0f)] public float boulders = 1f;
    [Tooltip("Multiplies how many rivers cross the open land (Rules › Surface Rivers).")]
    [Min(0f)] public float rivers = 1f;
    [Tooltip("On a Valley map: multiplies the width of the valley floor (a gorge narrows it).")]
    [Min(0.3f)] public float valleyWidth = 1f;

    [Header("As a district (G1)")]
    [Tooltip("The role this mood's district takes when it isn't the start's (Any: by its distance from the start; the farthest is the Lair).")]
    public RaidDistrictRole role = RaidDistrictRole.Any;
    [Tooltip("Weights per site kind in this mood's district against the other districts' (a kind not listed weighs 1). With Rules › " +
             "District Roles they say where a map's sites go; the map's count of each kind stays the rules'.")]
    public List<RaidSiteDensity> siteDensity = new List<RaidSiteDensity>();

    /// <summary>This mood's weight for a kind of site (1 when not listed).</summary>
    public float DensityOf(RaidSiteKind kind) => RaidLayoutRules.DensityIn(siteDensity, kind);

    [Header("Cliffs and Ravines")]
    [Tooltip("The raid planner offers a run of this mood as a large raid first. The player can still pick Normal.")]
    public bool largeByDefault;
    [Tooltip("How many ground levels the clearings stand on. Where high ground meets low there is a cliff, and a trail between two " +
             "levels climbs it as a ramp: the way up. 0 or 1: no cliffs.")]
    [Min(0)] public int terraces;
    [Tooltip("Metres between two levels: the height of a cliff.")]
    [Min(0f)] public float terraceHeight = 7f;
    [Tooltip("Metres across one patch of high or low ground.")]
    [Min(10f)] public float terraceScale = 110f;
    [Tooltip("How many ravines a map of the raid's normal size gets, least and most. A large map has as many on every square metre. " +
             "A trail crossing one keeps a neck of solid ground; a fall into one kills.")]
    public Vector2Int ravines = Vector2Int.zero;
    [Tooltip("How far a ravine runs, in metres, least and most.")]
    public Vector2 ravineLength = new Vector2(60f, 150f);
    [Tooltip("How wide a ravine is from rim to rim, in metres, least and most.")]
    public Vector2 ravineWidth = new Vector2(9f, 13f);
    [Tooltip("How far a ravine's floor lies below its rim, at the least, in metres: least and most. Its floor only falls along it, " +
             "so it is deeper where the land rises.")]
    public Vector2 ravineDepth = new Vector2(14f, 20f);
    [Tooltip("Chance that a ravine has a river along its floor.")]
    [Range(0f, 1f)] public float riverChance = 0.6f;
    [Tooltip("Mist lies along the floors of the ravines.")]
    public bool ravineMist = true;
    [Tooltip("How many ravines, on a map of the normal size, have one end that slopes down to the floor: the one way in, to a find at " +
             "the bottom (Rules › Descent Sites). Such a ravine has no river.")]
    [Min(0)] public int ravineDescents;
    [Tooltip("Sheer rock spires in the wild ground, which nobody can climb: least and most on a map of the normal size.")]
    public Vector2Int spires = Vector2Int.zero;
    [Tooltip("How high a spire stands above the ground around it, in metres, least and most.")]
    public Vector2 spireHeight = new Vector2(9f, 16f);
    [Tooltip("Radius of a spire's top, in metres, least and most.")]
    public Vector2 spireRadius = new Vector2(3f, 7f);

    /// <summary>True when the mood shapes the ground with cliffs, ravines or spires.</summary>
    public bool HasCliffs => terraces > 1 || ravines.y > 0 || spires.y > 0;
}

/// <summary>A spawn group the map doesn't place by its own Site, and the kind of site that takes its points.</summary>
[Serializable]
public sealed class RaidGroupRule
{
    [Tooltip("The Spawn Group's name in the scene.")]
    public string group;
    public RaidSiteKind site = RaidSiteKind.EnemyCamp;
}

/// <summary>
/// The rules a raid's map is generated by each run (RaidLayoutPlanner, RaidLayoutField, RaidMapGenerator): how many
/// clearings, the shapes and moods it can take, how many sites of each kind, and how the ground rises between the trails.
/// A raid uses them through its Raid Definition › Layout Rules; without them it plays its hand-painted map. Never changes
/// while playing. To change what a run can look like, add a mood or a shape's weight here: no code.
/// </summary>
[CreateAssetMenu(fileName = "RaidLayoutRules", menuName = "Safe Haven/Raids/Raid Layout Rules")]
public sealed class RaidLayoutRules : ScriptableObject
{
    [Header("Clearings")]
    [Tooltip("How many clearings between the start and the exits, least and most.")]
    [SerializeField] private Vector2Int rooms = new Vector2Int(9, 12);
    [Tooltip("How many clearings hold a way out, across the map from the start. A larger map grows them with its width, not its ground, so the walk grows but stays a walk.")]
    [SerializeField, Min(1)] private int exitRooms = 3;
    private const int MaxFieldResolution = 1024;
    [Tooltip("Metres kept between the middles of two clearings.")]
    [SerializeField, Min(8f)] private float roomSeparation = 34f;
    [Tooltip("Radius of a clearing in metres, smallest and largest.")]
    [SerializeField] private Vector2 roomRadius = new Vector2(8f, 14f);
    [Tooltip("Metres kept from the map's edge.")]
    [SerializeField, Min(0f)] private float edgeMargin = 14f;
    [Tooltip("How many times to lay out a new map if a way to an exit can't be found on the navmesh.")]
    [SerializeField, Range(1, 5)] private int attempts = 3;

    [Header("Trails")]
    [Tooltip("Half the width of a trail in metres.")]
    [SerializeField, Min(0.5f)] private float trailHalfWidth = 2.6f;
    [Tooltip("How far a trail strays from a straight line, as a share of its length.")]
    [SerializeField, Range(0f, 0.5f)] private float trailWander = 0.16f;
    [Tooltip("Metres between two clearings beyond which no extra trail joins them.")]
    [SerializeField, Min(0f)] private float loopReach = 80f;

    [Header("Ground")]
    [Tooltip("Height of the trails above the map's lowest point, in metres.")]
    [SerializeField, Min(0f)] private float groundHeight = 7f;
    [Tooltip("How far the ground rolls up and down, trails included, in metres.")]
    [SerializeField, Min(0f)] private float hills = 2.5f;
    [Tooltip("Metres across one roll of the hills.")]
    [SerializeField, Min(1f)] private float hillScale = 70f;
    [Tooltip("How high the wild ground rises between the trails, in metres.")]
    [SerializeField, Min(0f)] private float ridgeHeight = 7f;
    [Tooltip("Metres from a trail over which it rises. Shorter is steeper.")]
    [SerializeField, Min(1f)] private float ridgeWidth = 16f;
    [Tooltip("Roughness of the wild ground, in metres.")]
    [SerializeField, Min(0f)] private float roughness = 1.2f;
    [Tooltip("How high the land rises at the map's edge, in metres, so nobody sees a wall.")]
    [SerializeField, Min(0f)] private float rimHeight = 9f;
    [Tooltip("How far in from the edge it starts rising, in metres.")]
    [SerializeField, Min(1f)] private float rimWidth = 12f;

    [Header("Hills")]
    [Tooltip("How hilly a run is, least and most, rolled from its seed: multiplies the rolling of the ground and the height of the " +
             "great hills. Some runs are flat, some steep.")]
    [SerializeField] private Vector2 hillVariance = new Vector2(1f, 1f);
    [Tooltip("How many great hills rise in the wild ground, least and most, on a map of the normal size (times the mood's Hills).")]
    [SerializeField] private Vector2Int greatHills = Vector2Int.zero;
    [Tooltip("How high a great hill stands, in metres, least and most (times the run's hilliness). Trails may cross their flanks.")]
    [SerializeField] private Vector2 greatHillHeight = new Vector2(4f, 10f);
    [Tooltip("Radius of a great hill, in metres, least and most. Its steepest flank is 1.5 times height over radius.")]
    [SerializeField] private Vector2 greatHillRadius = new Vector2(24f, 45f);

    [Header("Clutter by camps and caches (2026-11-14)")]
    [Tooltip("Breakable things left about camps, caches and event sites: crates, barrels, fence sections (Breakable).")]
    [SerializeField] private List<GameObject> siteClutter = new List<GameObject>();
    [Tooltip("How many round each such site, least and most.")]
    [SerializeField] private Vector2Int clutterPerSite = new Vector2Int(2, 5);
    [Tooltip("How far from the site's middle, nearest and furthest, in metres.")]
    [SerializeField] private Vector2 clutterReach = new Vector2(3.5f, 8f);

    [Header("Boulders and Lookouts")]
    [Tooltip("The great boulders that lie about in the wild ground, solid to walk into (a Cliff Kit's pieces).")]
    [SerializeField] private CliffKit boulderKit;
    [Tooltip("How many clusters of great boulders, least and most, on a map of the normal size (times the mood's Boulders).")]
    [SerializeField] private Vector2Int boulderClusters = Vector2Int.zero;
    [Tooltip("How many boulders in a cluster, least and most.")]
    [SerializeField] private Vector2Int boulderClusterSize = new Vector2Int(1, 3);
    [Tooltip("Scale of a boulder, least and most.")]
    [SerializeField] private Vector2 boulderScale = new Vector2(0.7f, 1.4f);
    [Tooltip("A cluster's other boulders hug its first one as smaller rocks sunk deeper, so it reads as one outcrop; off, they lie a few " +
             "metres apart. Off keeps the Misty Woods' maps as they are.")]
    [SerializeField] private bool boulderOutcrops;
    [Tooltip("How many of the highest clearings hold a lookout, on a map of the normal size. Looking out reveals the land around.")]
    [SerializeField, Min(0)] private int lookouts;
    [Tooltip("Metres of the map a lookout reveals around it.")]
    [SerializeField, Min(10f)] private float lookoutReveal = 70f;

    [Header("Regions (large maps)")]
    [Tooltip("How many regions a large map is split into, least and most, each with a mood of its own; the start's region has the run's " +
             "mood. A map of the normal size is one region.")]
    [SerializeField] private Vector2Int largeRegions = new Vector2Int(1, 1);
    [Tooltip("Metres over which one region's look gives way to the next.")]
    [SerializeField, Min(1f)] private float regionBlend = 30f;
    [NonSerialized] private bool scaledUp;

    [Header("Districts (normal maps, G1)")]
    [Tooltip("How many districts a map of the normal size is split into, least and most, each a region with its own mood (2-3 suit 200 m). " +
             "The start's is the Approach and the one round the apex the Lair. 1: one mood over the whole map (as before).")]
    [SerializeField] private Vector2Int normalRegions = Vector2Int.one;
    [Tooltip("Where sites go by district role (Approach, Wild, Compound, Lair): weights per site kind, times the district mood's own. " +
             "Each kind's count is shared out by these weights, not by area. Empty: every district weighs the same (as before).")]
    [SerializeField] private List<RaidDistrictRule> districtRoles = new List<RaidDistrictRule>();

    [Header("Trail hierarchy (G5)")]
    [Tooltip("Multiplies the width of the short and long ways to the exit ahead, which become roads; side trails stay as they are. Paint " +
             "them with the map's Road Layer. 1: as wide as any trail (as before).")]
    [SerializeField, Range(0.5f, 2.5f)] private float roadWidth = 1f;
    [Tooltip("Short flags along the two ways to the exit ahead (cairns, carved posts, rune stones): the minimap shows the way, a flag " +
             "says you are still on it. Picked at random. Empty: none.")]
    [SerializeField] private List<GameObject> shortFlags = new List<GameObject>();
    [Tooltip("Metres walked between two flags, least and most.")]
    [SerializeField] private Vector2 shortFlagSpacing = new Vector2(100f, 140f);
    [Tooltip("Metres beyond the trail's edge a flag stands.")]
    [SerializeField, Min(0f)] private float shortFlagOffset = 1.5f;

    [Header("Routes")]
    [Tooltip("Make sure two ways lead to the exit ahead, a short one and a longer one, adding a trail where the map has only one.")]
    [SerializeField] private bool twoRoutes;
    [Tooltip("Camps go first to the clearings on the short way, which makes it the dangerous one and the long way the quiet one.")]
    [SerializeField] private bool campsOnShortRoute = true;

    [Header("Surface Rivers")]
    [Tooltip("How many rivers cross the open land, least and most, on a map of the normal size (times the mood's Rivers). A trail " +
             "crosses one at a ford, shallow enough to wade; elsewhere it is deep enough to swim.")]
    [SerializeField] private Vector2Int surfaceRivers = Vector2Int.zero;
    [Tooltip("How wide a river is, in metres, least and most.")]
    [SerializeField] private Vector2 surfaceRiverWidth = new Vector2(5f, 8f);
    [Tooltip("How deep a river is in its middle, in metres, least and most. The player swims from the Swim Depth.")]
    [SerializeField] private Vector2 surfaceRiverDepth = new Vector2(1.3f, 1.9f);
    [Tooltip("How deep the water is at a ford, in metres.")]
    [SerializeField, Min(0.1f)] private float fordDepth = 0.45f;
    [Tooltip("How far a river runs, in metres, least and most (it may run on to the map's edge).")]
    [SerializeField] private Vector2 surfaceRiverLength = new Vector2(120f, 260f);

    [Header("Landmarks")]
    [Tooltip("Tall things to find the way by: hill tops get them first.")]
    [SerializeField] private List<RaidLandmarkRule> landmarks = new List<RaidLandmarkRule>();
    [Tooltip("How many landmarks, least and most, on a map of the normal size.")]
    [SerializeField] private Vector2Int landmarkCount = Vector2Int.zero;
    [Tooltip("How many of the landmarks marked At Nodes stand at nodes, least and most, on a map of the normal size: one per district " +
             "first. Never more than there are such landmarks, until a large map has used them all.")]
    [SerializeField] private Vector2Int nodeLandmarkCount = new Vector2Int(3, 5);

    [Header("Ravine Descents")]
    [Tooltip("What waits on the floor of a ravine with a way down (the mood's Ravine Descents), one site of each.")]
    [SerializeField] private List<RaidSiteKind> descentSites = new List<RaidSiteKind> { RaidSiteKind.ResourceSite, RaidSiteKind.SupplyCache };

    [Header("Preparing Ahead")]
    [Tooltip("Metres across the raid's map at its normal size, so the raid planner can lay out the run while the player gets ready. " +
             "Must match the map's own size, or the run is laid out again on loading.")]
    [SerializeField, Min(50f)] private float mapSize = 200f;

    [Header("Forest")]
    [Tooltip("How dense the forest grows, 0 to 1, before the mood.")]
    [SerializeField, Range(0f, 1f)] private float forestDensity = 0.8f;
    [Tooltip("Metres across a patch of denser or thinner forest.")]
    [SerializeField, Min(1f)] private float forestPatches = 28f;
    [Tooltip("Cells across the zones and the ground plan. Higher is finer but slower to load.")]
    [SerializeField, Range(64, MaxFieldResolution)] private int fieldResolution = 200;

    [Header("Water")]
    [Tooltip("How many ponds a map gets, least and most, rolled each run.")]
    [SerializeField] private Vector2Int ponds = Vector2Int.zero;
    [Tooltip("Longest radius of a pond in metres, smallest and largest. A run's ponds are spread over this range.")]
    [SerializeField] private Vector2 pondRadius = new Vector2(3.5f, 11f);
    [Tooltip("How deep a pond is at its middle, in metres: the smallest pond's and the largest pond's. The player swims from the Swim Depth in Player Movement Settings.")]
    [SerializeField] private Vector2 pondDepth = new Vector2(0.7f, 1.6f);
    [Tooltip("How long a pond can be for its width. 1 keeps them round.")]
    [SerializeField, Range(1f, 2.5f)] private float pondStretch = 1.6f;
    [Tooltip("What the ponds' surface is drawn with.")]
    [SerializeField] private Material pondMaterial;
    [Tooltip("What the rivers along the floors of ravines are drawn with: a river material that takes its flow from the mesh.")]
    [SerializeField] private Material riverMaterial;
    [Tooltip("The rock-face pieces that dress the walls of ravines (a Cliff Kit). Empty: the terrain's own rock.")]
    [SerializeField] private CliffKit ravineCrags;
    [Tooltip("What a waterfall into a ravine is drawn with (the waterfall material).")]
    [SerializeField] private Material waterfallMaterial;
    [Tooltip("The foam, ripples, mist and spray where a waterfall lands.")]
    [SerializeField] private GameObject waterfallPlunge;
    [Tooltip("Thirst a drink from a pond restores. 0, with no Pond Fill Item either: the ponds can't be used.")]
    [SerializeField, Min(0f)] private float pondDrink;
    [Tooltip("The water container a pond fills, such as a Waterskin. Empty: only for drinking.")]
    [SerializeField] private ItemDefinition pondFillItem;
    [Tooltip("The bag is topped up to this many of the fill item.")]
    [SerializeField, Min(0)] private int pondFillUpTo = 3;
    [Tooltip("How far from the shore the player can use a pond, in metres.")]
    [SerializeField, Min(0.5f)] private float pondReach = 1.5f;
    [Tooltip("How far from the water the ground counts as wet, in metres. Plants that love the wet thicken towards the water.")]
    [SerializeField, Min(0f)] private float wetReach = 12f;
    [Tooltip("How wet the forest's damp patches are, away from any water. 0: only the ponds' shores are wet.")]
    [SerializeField, Range(0f, 1f)] private float dampGround = 0.45f;

    [Header("What can come up")]
    [SerializeField] private List<RaidShapeRule> shapes = new List<RaidShapeRule>();
    [SerializeField] private List<RaidMapMood> moods = new List<RaidMapMood>();

    [Header("What goes on the map")]
    [Tooltip("Placed in this order, so the first get the best spots.")]
    [SerializeField] private List<RaidSiteQuota> sites = new List<RaidSiteQuota>();
    [Tooltip("Spawn groups placed by hand on the painted map, and the kind of site that takes them here.")]
    [SerializeField] private List<RaidGroupRule> groups = new List<RaidGroupRule>();

    [Header("Valley (the Valley shape)")]
    [SerializeField] private RaidValleyRule valley = new RaidValleyRule();

    [Header("Fen (any shape)")]
    [SerializeField] private RaidFenRule fen = new RaidFenRule();

    [Header("Set Pieces")]
    [Tooltip("The authored places a run can have. Every rumour site that has room gets one, as do a few camps.")]
    [SerializeField] private List<RaidSetPieceDefinition> setPieces = new List<RaidSetPieceDefinition>();
    [Tooltip("How many of the map's camps are one, least and most.")]
    [SerializeField] private Vector2Int campPieces = new Vector2Int(1, 2);
    [Tooltip("How many compounds a map has, least and most (world review G3): set pieces that take a clearing of their own and ring its " +
             "yard with walls, their ways in left open. Picked from Set Pieces with Owns Clearing. 0: none.")]
    [SerializeField] private Vector2Int compounds = Vector2Int.zero;

    /// <summary>
    /// A copy of these rules for a map this many times wider: as much on each square metre, so four times as many clearings and sites on
    /// twice the width. Rumour sites only double, as they are the run's few great finds. Made at runtime; the asset never changes.
    /// </summary>
    public RaidLayoutRules ScaledBy(float width)
    {
        RaidLayoutRules copy = Instantiate(this);
        float area = width * width;
        copy.rooms = new Vector2Int(Mathf.RoundToInt(rooms.x * area), Mathf.RoundToInt(rooms.y * area));
        copy.ponds = new Vector2Int(Mathf.RoundToInt(ponds.x * area), Mathf.RoundToInt(ponds.y * area));
        copy.campPieces = new Vector2Int(Mathf.RoundToInt(campPieces.x * area), Mathf.RoundToInt(campPieces.y * area));
        copy.compounds = new Vector2Int(Mathf.RoundToInt(compounds.x * area), Mathf.RoundToInt(compounds.y * area));
        copy.exitRooms = Mathf.RoundToInt(exitRooms * width);
        copy.greatHills = new Vector2Int(Mathf.RoundToInt(greatHills.x * area), Mathf.RoundToInt(greatHills.y * area));
        copy.boulderClusters = new Vector2Int(Mathf.RoundToInt(boulderClusters.x * area), Mathf.RoundToInt(boulderClusters.y * area));
        copy.lookouts = Mathf.RoundToInt(lookouts * area);
        copy.scaledUp = true;
        copy.surfaceRivers = new Vector2Int(Mathf.RoundToInt(surfaceRivers.x * width), Mathf.RoundToInt(surfaceRivers.y * width));
        copy.landmarkCount = new Vector2Int(Mathf.RoundToInt(landmarkCount.x * area), Mathf.RoundToInt(landmarkCount.y * area));
        copy.nodeLandmarkCount = new Vector2Int(Mathf.RoundToInt(nodeLandmarkCount.x * area), Mathf.RoundToInt(nodeLandmarkCount.y * area));
        copy.fieldResolution = Mathf.Min(MaxFieldResolution, Mathf.RoundToInt(fieldResolution * width));
        // Ravines and spires stand on the land like clearings do: as many on every square metre.
        foreach (RaidMapMood mood in copy.moods)
        {
            mood.ravines = new Vector2Int(Mathf.RoundToInt(mood.ravines.x * area), Mathf.RoundToInt(mood.ravines.y * area));
            mood.spires = new Vector2Int(Mathf.RoundToInt(mood.spires.x * area), Mathf.RoundToInt(mood.spires.y * area));
            mood.ravineDescents = Mathf.RoundToInt(mood.ravineDescents * width);
        }

        copy.sites = new List<RaidSiteQuota>(sites.Count);
        foreach (RaidSiteQuota quota in sites)
        {
            if (quota == null)
            {
                continue;
            }

            copy.sites.Add(new RaidSiteQuota
            {
                kind = quota.kind,
                placement = quota.placement,
                count = Mathf.RoundToInt(quota.count * (quota.kind == RaidSiteKind.Rumour ? width : area)),
                separation = quota.separation,
                fromStart = quota.fromStart,
                offPath = quota.offPath,
                clearing = quota.clearing,
                prefersHeight = quota.prefersHeight,
                overlooksRamps = quota.overlooksRamps,
            });
        }

        return copy;
    }

    public IReadOnlyList<RaidSetPieceDefinition> SetPieces => setPieces;
    /// <summary>How a map of the Valley shape is laid out.</summary>
    public RaidValleyRule Valley => valley;
    public RaidFenRule Fen => fen;
    public Vector2Int CampPieces => campPieces;
    public Vector2Int Compounds => compounds;

    public Vector2Int Rooms => rooms;
    public int ExitRooms => exitRooms;
    public float RoomSeparation => roomSeparation;
    public Vector2 RoomRadius => roomRadius;
    public float EdgeMargin => edgeMargin;
    public int Attempts => attempts;
    public float TrailHalfWidth => trailHalfWidth;
    public float TrailWander => trailWander;
    public float LoopReach => loopReach;
    public float GroundHeight => groundHeight;
    public float Hills => hills;
    public float HillScale => hillScale;
    public float RidgeHeight => ridgeHeight;
    public float RidgeWidth => ridgeWidth;
    public float Roughness => roughness;
    public float RimHeight => rimHeight;
    public float RimWidth => rimWidth;
    public float ForestDensity => forestDensity;
    public float ForestPatches => forestPatches;
    public int FieldResolution => fieldResolution;
    public Vector2Int Ponds => ponds;
    public Vector2 PondRadius => pondRadius;
    public Vector2 PondDepth => pondDepth;
    public float PondStretch => pondStretch;
    public Material PondMaterial => pondMaterial;
    public Material RiverMaterial => riverMaterial;
    public CliffKit RavineCrags => ravineCrags;
    public Material WaterfallMaterial => waterfallMaterial;
    public GameObject WaterfallPlunge => waterfallPlunge;
    public Vector2 HillVariance => hillVariance;
    /// <summary>How many regions this map has, least and most: the large-map setting on a scaled copy, otherwise the districts (one by default).</summary>
    public Vector2Int Regions => scaledUp ? largeRegions : new Vector2Int(Mathf.Max(1, normalRegions.x), Mathf.Max(1, normalRegions.y));
    public Vector2Int LargeRegions => largeRegions;
    /// <summary>How many districts a map of the normal size has, least and most.</summary>
    public Vector2Int NormalRegions => normalRegions;
    public IReadOnlyList<RaidDistrictRule> DistrictRoles => districtRoles;
    public float RoadWidth => roadWidth;
    public IReadOnlyList<GameObject> ShortFlags => shortFlags;
    public Vector2 ShortFlagSpacing => shortFlagSpacing;
    public float ShortFlagOffset => shortFlagOffset;
    public Vector2Int NodeLandmarkCount => nodeLandmarkCount;

    /// <summary>
    /// How much a kind of site weighs in a district of this role with this mood: the role's weight (District Roles) times the mood's
    /// (Mood › Site Density), each 1 when not listed.
    /// </summary>
    public float SiteDensity(RaidDistrictRole role, RaidMapMood mood, RaidSiteKind kind)
    {
        float weight = mood != null ? mood.DensityOf(kind) : 1f;
        foreach (RaidDistrictRule rule in districtRoles)
        {
            if (rule != null && rule.role == role)
            {
                weight *= DensityIn(rule.siteDensity, kind);
            }
        }

        return weight;
    }

    /// <summary>A kind's weight in a list of site densities: the first entry for it, 1 when there is none.</summary>
    public static float DensityIn(List<RaidSiteDensity> densities, RaidSiteKind kind)
    {
        if (densities == null)
        {
            return 1f;
        }

        foreach (RaidSiteDensity density in densities)
        {
            if (density != null && density.kind == kind)
            {
                return Mathf.Max(0f, density.density);
            }
        }

        return 1f;
    }
    public float RegionBlend => regionBlend;
    public bool TwoRoutes => twoRoutes;
    public bool CampsOnShortRoute => campsOnShortRoute;
    public Vector2Int SurfaceRivers => surfaceRivers;
    public Vector2 SurfaceRiverWidth => surfaceRiverWidth;
    public Vector2 SurfaceRiverDepth => surfaceRiverDepth;
    public float FordDepth => fordDepth;
    public Vector2 SurfaceRiverLength => surfaceRiverLength;
    public IReadOnlyList<RaidLandmarkRule> Landmarks => landmarks;
    public Vector2Int LandmarkCount => landmarkCount;
    public IReadOnlyList<RaidSiteKind> DescentSites => descentSites;
    public float MapSize => mapSize;
    public Vector2Int GreatHills => greatHills;
    public Vector2 GreatHillHeight => greatHillHeight;
    public Vector2 GreatHillRadius => greatHillRadius;
    public CliffKit BoulderKit => boulderKit;
    public IReadOnlyList<GameObject> SiteClutter => siteClutter;
    public Vector2Int ClutterPerSite => clutterPerSite;
    public Vector2 ClutterReach => clutterReach;
    public Vector2Int BoulderClusters => boulderClusters;
    public Vector2Int BoulderClusterSize => boulderClusterSize;
    public Vector2 BoulderScale => boulderScale;
    public bool BoulderOutcrops => boulderOutcrops;
    public int Lookouts => lookouts;
    public float LookoutReveal => lookoutReveal;
    public float PondDrink => pondDrink;
    public ItemDefinition PondFillItem => pondFillItem;
    public int PondFillUpTo => pondFillUpTo;
    public float PondReach => pondReach;
    public float WetReach => wetReach;
    public float DampGround => dampGround;
    public IReadOnlyList<RaidShapeRule> Shapes => shapes;
    public IReadOnlyList<RaidMapMood> Moods => moods;
    public IReadOnlyList<RaidSiteQuota> Sites => sites;
    public IReadOnlyList<RaidGroupRule> Groups => groups;

#if UNITY_EDITOR
    /// <summary>Fills in the lists (builders and tests).</summary>
    public void Configure(IEnumerable<RaidShapeRule> shapeRules, IEnumerable<RaidMapMood> moodRules, IEnumerable<RaidSiteQuota> siteRules, IEnumerable<RaidGroupRule> groupRules)
    {
        shapes = new List<RaidShapeRule>(shapeRules);
        moods = new List<RaidMapMood>(moodRules);
        sites = new List<RaidSiteQuota>(siteRules);
        groups = new List<RaidGroupRule>(groupRules);
    }

    /// <summary>Adds a mood, or replaces the one of the same name (builders and tests).</summary>
    public void SetMood(RaidMapMood mood)
    {
        int at = moods.FindIndex(one => one != null && one.name == mood.name);
        if (at >= 0)
        {
            moods[at] = mood;
        }
        else
        {
            moods.Add(mood);
        }
    }

    /// <summary>Sets what the rivers in ravines are drawn with (builders and tests).</summary>
    public void ConfigureRivers(Material material, CliffKit crags, Material falls, GameObject plunge)
    {
        riverMaterial = material;
        ravineCrags = crags;
        waterfallMaterial = falls;
        waterfallPlunge = plunge;
    }

    /// <summary>Sets up regions, routes, rivers and landmarks (builders and tests).</summary>
    public void ConfigureVariety(Vector2Int regionCount, float blend, bool routes, Vector2Int rivers, Vector2 riverWidth, Vector2 riverDepth,
        IEnumerable<RaidLandmarkRule> landmarkRules, Vector2Int landmarksWanted)
    {
        largeRegions = regionCount;
        regionBlend = blend;
        twoRoutes = routes;
        surfaceRivers = rivers;
        surfaceRiverWidth = riverWidth;
        surfaceRiverDepth = riverDepth;
        landmarks = new List<RaidLandmarkRule>(landmarkRules);
        landmarkCount = landmarksWanted;
    }

    /// <summary>Sets up districts on maps of the normal size and their roles' site weights (builders and tests).</summary>
    public void ConfigureDistricts(Vector2Int normal, IEnumerable<RaidDistrictRule> roles)
    {
        normalRegions = normal;
        districtRoles = new List<RaidDistrictRule>(roles);
    }

    /// <summary>Sets up the roads and their short flags (builders and tests).</summary>
    public void ConfigureRoads(float width, IEnumerable<GameObject> flags, Vector2 spacing, float offset)
    {
        roadWidth = width;
        shortFlags = new List<GameObject>(flags);
        shortFlagSpacing = spacing;
        shortFlagOffset = offset;
    }

    /// <summary>Sets how many landmarks stand at nodes (builders and tests).</summary>
    public void ConfigureNodeLandmarks(Vector2Int count) => nodeLandmarkCount = count;

    /// <summary>Replaces the landmarks alone (builders and tests).</summary>
    public void ConfigureLandmarks(IEnumerable<RaidLandmarkRule> landmarkRules, Vector2Int landmarksWanted)
    {
        landmarks = new List<RaidLandmarkRule>(landmarkRules);
        landmarkCount = landmarksWanted;
    }

    /// <summary>Sets how a site kind is placed by height (builders and tests).</summary>
    public void ConfigureSiteHeights(RaidSiteKind kind, bool high, bool overRamps)
    {
        foreach (RaidSiteQuota quota in sites)
        {
            if (quota != null && quota.kind == kind)
            {
                quota.prefersHeight = high;
                quota.overlooksRamps = overRamps;
            }
        }
    }

    /// <summary>Sets up hills, boulders and lookouts (builders and tests).</summary>
    public void ConfigureRelief(Vector2 variance, Vector2Int hillCount, Vector2 hillHeight, Vector2 hillRadius, CliffKit kit, Vector2Int clusters,
        Vector2Int clusterSize, Vector2 scale, int lookoutCount, float reveal)
    {
        hillVariance = variance;
        greatHills = hillCount;
        greatHillHeight = hillHeight;
        greatHillRadius = hillRadius;
        boulderKit = kit;
        boulderClusters = clusters;
        boulderClusterSize = clusterSize;
        boulderScale = scale;
        lookouts = lookoutCount;
        lookoutReveal = reveal;
    }

    /// <summary>Sets up the ponds (builders and tests).</summary>
    public void ConfigureWater(Vector2Int count, Vector2 radius, Material material)
    {
        ponds = count;
        pondRadius = radius;
        pondMaterial = material;
    }
#endif

    private void OnValidate()
    {
        ponds.x = Mathf.Max(0, ponds.x);
        ponds.y = Mathf.Max(ponds.x, ponds.y);
        pondRadius.x = Mathf.Max(1f, pondRadius.x);
        pondRadius.y = Mathf.Max(pondRadius.x, pondRadius.y);
        pondDepth.x = Mathf.Max(0.1f, pondDepth.x);
        pondDepth.y = Mathf.Max(pondDepth.x, pondDepth.y);
    }
}
