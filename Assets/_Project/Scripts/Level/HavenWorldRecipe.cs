using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A point of interest the Haven world can have: a prefab, how far from the portal and how many.</summary>
[Serializable]
public sealed class HavenPointOfInterest
{
    public string name = "Ruin";
    public GameObject prefab;
    [Tooltip("Metres from the portal, least and most.")]
    public Vector2 distance = new Vector2(200f, 450f);
    [Min(0)] public int count = 1;
    [Tooltip("Open ground kept around it, in metres.")]
    [Min(0f)] public float clearing = 10f;
    [Tooltip("Goes on a hill top or high ground if it can.")]
    public bool high;
    [Tooltip("Goes on one of the lake's islands (only reached by boat).")]
    public bool onIsland;
    [Tooltip("A path leads to it from the nearest path.")]
    public bool path = true;
    [Tooltip("Stands at the end of a road that runs out to the edge of the woods, one per road until they run out: no road ends at nothing.")]
    public bool atRoadEnd;
    [Tooltip("Stands in the bog, on dry ground among the pools.")]
    public bool inBog;
    [Tooltip("Stands by the water: the lake's or a mere's shore.")]
    public bool onShore;
    [Tooltip("Stands in the Haven's own meadows, inside the woods' edge (its distance still applies).")]
    public bool inMeadows;
    [Header("What it gives")]
    [Tooltip("Resource points standing round it, so the walk there pays: how many (0: none), of what kind and tiers.")]
    [Min(0)] public int rewards;
    public ResourceNodeKind rewardKind = ResourceNodeKind.Forage;
    public Vector2Int rewardTiers = new Vector2Int(1, 2);
}

/// <summary>Creatures the Haven world spawns between two distances from the portal, in drawn spawn zones (MobSpawnZone).</summary>
[Serializable]
public sealed class HavenCreatureBand
{
    public string name = "Near woods";
    [Tooltip("Metres from the portal, least and most.")]
    public Vector2 distance = new Vector2(220f, 320f);
    [Tooltip("How many spawn zones in this band.")]
    [Min(0)] public int zones = 4;
    [Tooltip("Creatures in a zone, least and most.")]
    public Vector2Int group = new Vector2Int(1, 2);
    [Tooltip("Seconds before a cleared zone fills again.")]
    [Min(0f)] public float respawnSeconds = 600f;
    [Tooltip("Game to hunt (and one day tame), not a threat: its zones may lie in the Haven's meadows, take the open glades rather " +
             "than the lairs, and fill again by day as well as by night.")]
    public bool peaceful;
    [Tooltip("Only in the bog.")]
    public bool bogOnly;
    public List<SpawnTable.Entry> creatures = new List<SpawnTable.Entry>();
}

/// <summary>Resource spawn points (RandomResourceNode) between two distances from the portal: denser and of higher tiers further out.</summary>
[Serializable]
public sealed class HavenResourceBand
{
    public string name = "Stone";
    public Vector2 distance = new Vector2(80f, 250f);
    public ResourceNodeKind kind = ResourceNodeKind.Rock;
    public Vector2Int tiers = new Vector2Int(1, 1);
    [Tooltip("How many clusters in this band.")]
    [Min(0)] public int clusters = 10;
    [Tooltip("Nodes in a cluster, least and most.")]
    public Vector2Int perCluster = new Vector2Int(2, 4);
    [Tooltip("Only in the mountains (rock and ore), or anywhere in the band.")]
    public bool mountainOnly;
    [Tooltip("Only in the bog (its plants, bog ore).")]
    public bool bogOnly;
}

/// <summary>A smaller lake in the woods or the hills, with islands of its own.</summary>
[Serializable]
public sealed class HavenMere
{
    public string name = "Mere";
    [Tooltip("Direction from the portal (degrees, 0 = north, clockwise) and distance to its middle. It moves a little to find room.")]
    public Vector2 at = new Vector2(240f, 300f);
    [Tooltip("Half its length and half its width, in metres.")]
    public Vector2 size = new Vector2(45f, 30f);
    [Min(0.5f)] public float depth = 3f;
    [Tooltip("Islands, least and most.")]
    public Vector2Int islands = new Vector2Int(1, 2);
}

/// <summary>
/// The recipe the Haven world is generated from (Safe Haven › World › Generate Haven World): rings around the portal from the Haven's own
/// light meadows out into the Misty Woods, a lake with islands and a mountain in their own directions, rivers, paths, cliffs, ravines,
/// points of interest and what spawns where. Nothing here runs in the game; the generator turns it into ordinary World Builder features,
/// terrain, spawn zones and resource points that a designer then edits by hand. Same recipe and seed, same world.
/// </summary>
[CreateAssetMenu(fileName = "Haven World Recipe", menuName = "Safe Haven/World/Haven World Recipe")]
public sealed class HavenWorldRecipe : ScriptableObject
{
    [Header("Rings (metres from the portal)")]
    [Tooltip("Kept gentle and open for building: the ward grows to about 50 m.")]
    [Min(10f)] public float buildable = 70f;
    [Tooltip("The Haven's light meadows and maple groves reach this far, then give way.")]
    [Min(20f)] public float haven = 170f;
    [Tooltip("From here out it is the Misty Woods: dark cedars, more relief, more ravines.")]
    [Min(40f)] public float forest = 330f;

    [Header("Terrain")]
    [Min(100f)] public float size = 1000f;
    [Tooltip("Metres the terrain reaches below the portal's ground (for lake beds and river cuts) and its whole height.")]
    public Vector2 depthAndHeight = new Vector2(30f, 150f);
    [Tooltip("Heightmap resolution: 1025 gives cliffs and banks about 1 m of detail on 1 km.")]
    public int heightResolution = 1025;
    public int paintResolution = 1024;
    public int grassResolution = 1024;
    [Tooltip("How high the land rises at the edge of the world, and over how many metres, so nobody walks off it.")]
    public Vector2 rim = new Vector2(28f, 45f);

    [Header("Relief")]
    [Tooltip("How far the Haven's ground rolls up and down, in metres.")]
    [Min(0f)] public float havenHills = 3f;
    [Tooltip("Metres across one roll of the hills.")]
    [Min(10f)] public float hillScale = 90f;

    [Header("Terraces")]
    [Tooltip("Height of one terrace, in metres. Outside the Haven the land is flat benches with a cliff between each and the next: " +
             "from above a cliff reads at a glance, a slope does not.")]
    [Min(2f)] public float terraceStep = 7f;
    [Tooltip("How high the land has climbed, in metres, where the rim begins: about this many metres over the step is how many cliffs " +
             "a walk straight out crosses.")]
    [Min(0f)] public float terraceRise = 21f;
    [Tooltip("How far the land swells above and sinks below that climb, in metres: it makes the cliffs wander into bays, headlands and " +
             "lone table rocks instead of rings.")]
    [Min(0f)] public float terraceWander = 10f;
    [Tooltip("Metres across one such swell.")]
    [Min(40f)] public float terraceScale = 180f;
    [Tooltip("How deep the roads out from the Haven run below the land beside them, in terraces (0: none): a road in a valley has " +
             "cliffs for walls, and the benches above it are rooms a trail climbs to.")]
    [Range(0f, 1.5f)] public float roadValley = 0.9f;
    [Tooltip("Metres from a road at which its valley has risen back to the land.")]
    [Min(10f)] public float roadValleyWidth = 40f;
    [Tooltip("How far a bench rolls and tilts, as a share of the Haven's hills (0 = dead flat).")]
    [Range(0f, 1f)] public float benchRoll = 0.3f;
    [Tooltip("Metres a cliff face takes from foot to top, seen from above.")]
    [Min(1f)] public float riserWidth = 3.5f;
    [Tooltip("Metres a path takes to climb one terrace: every path crossing a cliff gets a ramp this long.")]
    [Min(8f)] public float rampLength = 30f;
    [Tooltip("A way up without a path, where the cliff has crumbled into a slope: one about every so many metres along each cliff, " +
             "least and most, so no bench is cut off.")]
    public Vector2 scrambleSpacing = new Vector2(90f, 150f);
    [Tooltip("Half the width of such a scramble, in metres.")]
    [Min(3f)] public float scrambleRadius = 8f;
    [Tooltip("Cliffs shorter than this get no rock pieces stood along them (the terrain's own rock still shows).")]
    [Min(5f)] public float cliffMinLength = 24f;
    [Tooltip("Rock pieces stand only along cliffs within this many metres of a path, a glade or a place, where they are walked beside; " +
             "further off the terrain's own rock does. Rock pieces are the heaviest thing in the world: about 2 scene objects a metre.")]
    [Min(0f)] public float cliffRockNear = 30f;

    [Header("Lake")]
    [Tooltip("Direction from the portal (degrees, 0 = north, clockwise) and distance to the lake's middle.")]
    public Vector2 lakeAt = new Vector2(100f, 300f);
    [Tooltip("Half its length and half its width, in metres.")]
    public Vector2 lakeSize = new Vector2(110f, 75f);
    [Min(1f)] public float lakeDepth = 6f;
    [Tooltip("A boat landing (a jetty running out along its +Z from the shore): one where the lake path meets the water, one on the island with a place.")]
    public GameObject landing;
    [Tooltip("Islands, least and most, and their radius.")]
    public Vector2Int islands = new Vector2Int(2, 3);
    public Vector2 islandRadius = new Vector2(10f, 22f);

    [Header("Mountain")]
    [Tooltip("Direction from the portal (degrees) and the angle it spans.")]
    public Vector2 mountainAt = new Vector2(315f, 80f);
    [Tooltip("Metres from the portal where it begins to rise.")]
    [Min(50f)] public float mountainFrom = 260f;
    [Tooltip("How high it rises above the woods, in metres.")]
    [Min(0f)] public float mountainHeight = 60f;
    [Tooltip("Rock faces and boulders strewn over its flanks: each faces down the slope if its shape faces one way.")]
    public List<GameObject> mountainRocks = new List<GameObject>();
    [Tooltip("How many, least and most.")]
    public Vector2Int mountainRockCount = new Vector2Int(40, 55);
    [Tooltip("Their size, least and most (1 = as modelled).")]
    public Vector2 mountainRockScale = new Vector2(0.7f, 1.5f);

    [Header("Meres and Bog")]
    [Tooltip("Smaller lakes with islands of their own.")]
    public List<HavenMere> meres = new List<HavenMere>();
    [Tooltip("The bog: direction from the portal (degrees) and distance to its middle. 0 distance: no bog.")]
    public Vector2 bogAt = new Vector2(195f, 345f);
    [Tooltip("How far it reaches from its middle, in metres.")]
    [Min(0f)] public float bogRadius = 95f;
    [Tooltip("How much of it stands under water, 0 to 1: pools and channels between the hummocks.")]
    [Range(0f, 0.8f)] public float bogWater = 0.38f;
    [Tooltip("How thin the woods stand in the bog, as a share of the woods around it.")]
    [Range(0f, 1f)] public float bogWoods = 0.22f;

    [Header("Water and Rock")]
    [Tooltip("Rivers, least and most: from the mountain or the edge down to the lake.")]
    public Vector2Int rivers = new Vector2Int(2, 2);
    [Tooltip("Their width, least and most, in metres.")]
    public Vector2 riverWidth = new Vector2(4.5f, 6f);
    [Tooltip("Streams, least and most: out of the meres and the bog and from springs in the hills, down to the nearest river or lake.")]
    public Vector2Int streams = new Vector2Int(4, 5);
    [Tooltip("Their width, least and most, in metres.")]
    public Vector2 streamWidth = new Vector2(1.6f, 2.6f);
    [Tooltip("Ponds in the Haven's ring, least and most.")]
    public Vector2Int ponds = new Vector2Int(2, 4);
    public Vector2 pondRadius = new Vector2(5f, 12f);
    [Tooltip("Rock outcrops (cliffs all round, a knoll) in the Haven's ring and the woods, least and most.")]
    public Vector2Int outcrops = new Vector2Int(6, 9);
    [Tooltip("Ravines in the woods, least and most; more of them further out.")]
    public Vector2Int ravines = new Vector2Int(6, 9);
    public Vector2 ravineDepth = new Vector2(10f, 18f);
    public Vector2 ravineLength = new Vector2(70f, 160f);

    [Header("Paths")]
    [Tooltip("Paths running out from the Haven into the woods.")]
    [Min(0)] public int radialPaths = 6;
    [Tooltip("Metres from the portal of the path that rings the Haven, joining the others (0: none).")]
    [Min(0f)] public float ringPath = 230f;
    [Tooltip("Trails looping between neighbouring paths deep in the woods, where nothing blocks them.")]
    public bool outerLoops = true;
    [Tooltip("Widths of the main paths, the ring, spurs to places, loops and woodland trails (metres).")]
    public float roadWidth = 3f;
    public float ringWidth = 2.6f;
    public float spurWidth = 2.2f;
    public float loopWidth = 2.2f;
    public float trailWidth = 1.7f;
    [Tooltip("How far each side of a main path (and of a trail) the woods thin out, past the tread (metres).")]
    public Vector2 pathClearing = new Vector2(11f, 7f);

    [Header("Glades")]
    [Tooltip("Open glades in the woods, least and most: more of them further out. Each is joined to the paths by a trail.")]
    public Vector2Int glades = new Vector2Int(16, 20);
    public Vector2 gladeRadius = new Vector2(14f, 28f);
    [Tooltip("Chance a glade gets a second trail, to another glade or path, so the woods are a network rather than dead ends.")]
    [Range(0f, 1f)] public float gladeLinks = 0.55f;
    [Tooltip("Chance a place with a path gets a second way out, a trail on to the nearest glade: a walk to it carries on somewhere.")]
    [Range(0f, 1f)] public float placeLinks = 0.6f;
    [Tooltip("What can stand in a glade's middle, so it reads from above (an old tree, stones); one is picked per glade that gets one.")]
    public List<GameObject> gladeLandmarks = new List<GameObject>();
    [Range(0f, 1f)] public float gladeLandmarkChance = 0.4f;

    [Header("Forest")]
    [Tooltip("The raids' forest: its trees, undergrowth and mineable rocks are painted as terrain trees in the woods.")]
    public RaidMapDefinition woods;
    [Tooltip("How dense the woods grow, 0 to 1, from the Haven's edge to the edge of the world.")]
    public Vector2 woodsDensity = new Vector2(0.15f, 0.95f);
    [Tooltip("Maple groves (World Builder areas) in the Haven's ring, least and most.")]
    public Vector2Int groves = new Vector2Int(4, 5);

    [Header("Along the Way")]
    [Tooltip("Small scenes beside the woods' paths (a fallen giant, a log stack, a waystone...), one every so many metres: " +
             "something to come across between the glades.")]
    public List<GameObject> trailScenes = new List<GameObject>();
    [Tooltip("The same for paths through the Haven's own meadows (an old fence, boundary stones...). Empty: the woods' scenes are used there too.")]
    public List<GameObject> meadowScenes = new List<GameObject>();
    [Tooltip("How far a walk goes with nothing on it before a scene is set beside the path, least and most, in metres.")]
    public Vector2 trailSceneSpacing = new Vector2(45f, 70f);
    [Tooltip("The longest walk along a path with nothing to come across, in metres: the planner marks any longer one in red.")]
    [Min(20f)] public float longestDullWalk = 90f;
    [Tooltip("Stands where a trail leaves another path (a cairn): the way can be found without the map.")]
    public GameObject junctionMarker;
    [Tooltip("Stones lining a ravine's lip, every so many metres, so the drop is seen before it is fallen into.")]
    public List<GameObject> rimStones = new List<GameObject>();
    [Min(1f)] public float rimStoneSpacing = 4f;
    [Tooltip("Metres past a ravine's lip kept clear of trees (the first 2 m of undergrowth too).")]
    [Min(0f)] public float rimClearing = 7f;
    [Tooltip("Metres added round a place's clearing for the trees' crowns, so a place is never roofed over from above.")]
    [Min(0f)] public float crownMargin = 6f;

    [Header("Glade Dressing")]
    [Tooltip("What lies about each kind of glade, so a lair, a rocky glade, a meadow and a pool each look their part.")]
    public List<GameObject> lairDressing = new List<GameObject>();
    public List<GameObject> rockyDressing = new List<GameObject>();
    public List<GameObject> meadowDressing = new List<GameObject>();
    public List<GameObject> wetDressing = new List<GameObject>();
    public Vector2Int dressingPerGlade = new Vector2Int(5, 9);

    [Header("Wildlife")]
    [Tooltip("Timid grazers (deer, stag) in the meadow and wet glades and the Haven's groves, about at dawn and dusk.")]
    public List<Wildlife> grazers = new List<Wildlife>();
    [Tooltip("Foxes, in some of the rocky glades and lairs, about from dusk to dawn.")]
    public List<Wildlife> foxes = new List<Wildlife>();
    [Tooltip("A hollowed grazer that may stand among a herd as the blight spreads (a Glimpse: it watches, then is gone).")]
    public Glimpse hollowedGrazer;

    [Header("What Is Found There")]
    public List<HavenPointOfInterest> pointsOfInterest = new List<HavenPointOfInterest>();
    public List<HavenCreatureBand> creatures = new List<HavenCreatureBand>();
    public List<HavenResourceBand> resources = new List<HavenResourceBand>();

    [Header("Mist")]
    [Tooltip("How much mist always lies in the Misty Woods, 0 to 1 (the Haven's own meadows keep only the weather's and the morning's).")]
    [Range(0f, 1f)] public float woodsMist = 0.55f;
    [Tooltip("How much always lies over the bog.")]
    [Range(0f, 1f)] public float bogMist = 1f;
    [Tooltip("How much lies in the glades, as a share of the woods around them: a glade is a clear room in the mist.")]
    [Range(0f, 1f)] public float gladeMist = 0.35f;

    [Header("Seed")]
    public int seed = 1;
}
