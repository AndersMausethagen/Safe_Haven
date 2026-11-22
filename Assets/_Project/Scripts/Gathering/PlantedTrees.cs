using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saplings planted at home, on the Haven's terrain: each grows over a few in-game days from a hand's height into a tree,
/// and comes down like any other. A planted tree is one of the terrain's own painted trees (added to its runtime copy,
/// after the trees it was made with), so the terrain draws it, and TerrainTreeFelling's stand-ins near the player make it
/// workable: broken down by hand while it is small, felled with an axe once grown. Nothing runs per tree: every few
/// seconds the sizes are worked out from the in-game day (SettlementState.DaysPassed and the hour) and the few that have
/// grown are set, with no distance checks. Saved with the Haven (ISaveable): where each stands, what it becomes, when it
/// was planted. One that comes down is gone (plant another). On the terrain beside its SaveableEntity and TerrainTreeMemory.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Terrain))]
public sealed class PlantedTrees : MonoBehaviour, ISaveable
{
    private const float TickSeconds = 4f;

    [Serializable]
    private sealed class Planted
    {
        public Vector3 position;
        public float rotation;
        public string prototype;
        public float plantedDay;
        public float growDays;
        public float startScale;
        public float fullScale;
        [NonSerialized] public int index = -1;
    }

    [Serializable]
    private sealed class State
    {
        public List<Planted> trees = new List<Planted>();
    }

    private readonly List<Planted> planted = new List<Planted>();
    private Terrain terrain;
    private TerrainTreeFelling felling;
    private int baseCount = -1;
    private float nextTick;

    /// <summary>The planted trees of the loaded home, or null away from home.</summary>
    public static PlantedTrees Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public string SaveKey => "plantedTrees";

    /// <summary>How many trees the terrain was made with: planted ones come after these.</summary>
    public int BaseCount
    {
        get
        {
            Awake();
            return baseCount;
        }
    }

    public int Count => planted.Count;

    /// <summary>
    /// Whether the tree at <paramref name="index"/> on <paramref name="on"/>'s terrain is a sapling still growing (RingWork leaves those:
    /// a ring is cleared of its grown trees, not of the saplings its woodcutter just planted).
    /// </summary>
    public bool IsGrowing(TerrainTreeFelling on, int index)
    {
        if (on == null || on != felling)
        {
            return false;
        }

        float today = Today;
        foreach (Planted tree in planted)
        {
            if (tree.index == index)
            {
                return today - tree.plantedDay < tree.growDays;
            }
        }

        return false;
    }

    /// <summary>The in-game day now, counting from the first dawn, with the hour as its fraction (days start at dawn).</summary>
    public static float Today => (GameSession.Here != null ? GameSession.Here.DaysPassed : 0) + Mathf.Repeat(AtmosphereConditions.Hour - 6f, 24f) / 24f;

    private void Awake()
    {
        if (terrain != null)
        {
            return;
        }

        terrain = GetComponent<Terrain>();
        baseCount = terrain.terrainData != null ? terrain.terrainData.treeInstanceCount : 0;
    }

    private void OnEnable()
    {
        Current = this;
        felling = TerrainTreeFelling.On(terrain);
        felling.TreeDown += OnTreeDown;
    }

    private void OnDisable()
    {
        if (felling != null)
        {
            felling.TreeDown -= OnTreeDown;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Plants a sapling at <paramref name="at"/> (on this terrain): it grows into one of its trees. False, with why, where
    /// it can't: in water, too steep, in the way of something, or too near another tree.
    /// </summary>
    public bool TryPlant(Vector3 at, SaplingDefinition sapling, out string problem)
    {
        TerrainData data = terrain.terrainData;
        int prototype = PrototypeFor(sapling);
        if (prototype < 0)
        {
            problem = "Nothing like it grows here.";
            return false;
        }

        Vector3 local = at - terrain.GetPosition();
        var normalised = new Vector3(local.x / data.size.x, 0f, local.z / data.size.z);
        if (normalised.x < 0f || normalised.x > 1f || normalised.z < 0f || normalised.z > 1f)
        {
            problem = "It won't take root there.";
            return false;
        }

        at.y = terrain.SampleHeight(at) + terrain.GetPosition().y;
        if (WaterSurface.OpenAt(at + Vector3.up * 0.2f, 1f, out _) != null)
        {
            problem = "It would drown there.";
            return false;
        }

        if (data.GetInterpolatedNormal(normalised.x, normalised.z).y < 0.8f)
        {
            problem = "The ground is too steep there.";
            return false;
        }

        if (InTheWay(at))
        {
            problem = "Something is in the way.";
            return false;
        }

        if (TreeNear(at, sapling.Spacing))
        {
            problem = "Too close to another tree. It needs room to grow.";
            return false;
        }

        var tree = new Planted
        {
            position = at,
            rotation = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            prototype = data.treePrototypes[prototype].prefab.name,
            plantedDay = Today,
            growDays = sapling.GrowDays,
            startScale = sapling.StartScale,
            fullScale = UnityEngine.Random.Range(sapling.FullScale.x, sapling.FullScale.y),
        };
        planted.Add(tree);
        Add(tree, prototype);
        problem = null;
        return true;
    }

    private void Update()
    {
        if (Time.time < nextTick || planted.Count == 0)
        {
            return;
        }

        nextTick = Time.time + TickSeconds;
        TerrainData data = terrain.terrainData;
        float today = Today;
        foreach (Planted tree in planted)
        {
            if (tree.index < 0 || tree.index >= data.treeInstanceCount)
            {
                continue;
            }

            TreeInstance instance = data.GetTreeInstance(tree.index);
            float scale = SaplingDefinition.ScaleAt(today - tree.plantedDay, tree.growDays, tree.startScale, tree.fullScale);
            // Hidden (scale 0) is a tree being knocked or brought down: the felling has it.
            if (instance.heightScale <= 0f || Mathf.Abs(instance.heightScale - scale) < 0.005f)
            {
                continue;
            }

            instance.heightScale = scale;
            instance.widthScale = scale;
            data.SetTreeInstance(tree.index, instance);
        }
    }

    private void Add(Planted tree, int prototype)
    {
        TerrainData data = felling.OwnedData();
        Vector3 local = tree.position - terrain.GetPosition();
        float scale = SaplingDefinition.ScaleAt(Today - tree.plantedDay, tree.growDays, tree.startScale, tree.fullScale);
        terrain.AddTreeInstance(new TreeInstance
        {
            position = new Vector3(local.x / data.size.x, local.y / data.size.y, local.z / data.size.z),
            prototypeIndex = prototype,
            heightScale = scale,
            widthScale = scale,
            rotation = tree.rotation,
            color = Color.white,
            lightmapColor = Color.white,
        });
        tree.index = terrain.terrainData.treeInstanceCount - 1;
    }

    private void OnTreeDown(int index)
    {
        if (index < BaseCount)
        {
            return;
        }

        planted.RemoveAll(tree => tree.index == index);
    }

    private int PrototypeFor(SaplingDefinition sapling)
    {
        TreePrototype[] prototypes = terrain.terrainData.treePrototypes;
        var candidates = new List<int>();
        for (int i = 0; i < prototypes.Length; i++)
        {
            foreach (GameObject tree in sapling.GrowsInto)
            {
                if (tree != null && prototypes[i].prefab == tree)
                {
                    candidates.Add(i);
                }
            }
        }

        return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : -1;
    }

    private int PrototypeNamed(string prefabName)
    {
        TreePrototype[] prototypes = terrain.terrainData.treePrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prefab != null && prototypes[i].prefab.name == prefabName)
            {
                return i;
            }
        }

        return -1;
    }

    // Buildings, fences and anything else solid where it would stand (not the ground, not people).
    private static bool InTheWay(Vector3 at)
    {
        foreach (Collider hit in Physics.OverlapSphere(at + Vector3.up * 1f, 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit is TerrainCollider || hit.GetComponentInParent<Damageable>() is Damageable body && body.Category == TargetCategory.Creature)
            {
                continue;
            }

            if (Player.Current != null && hit.transform.IsChildOf(Player.Current.transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // Any standing tree of this terrain (painted or planted) within the distance: from the felling's cache (copying every tree's
    // instance for each sapling was 5 MB on the Haven).
    private bool TreeNear(Vector3 at, float distance) => felling != null && felling.AnyTreeWithin(at, distance);

    public string CaptureState() => planted.Count > 0 ? JsonUtility.ToJson(new State { trees = planted }) : null;

    // Two planted trees closer than this are the same one (positions come back from JSON to the last digit, or near it).
    private const float SameSpot = 0.05f;

    public void RestoreState(string json)
    {
        State state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<State>(json);
        if (state == null || state.trees == null)
        {
            return;
        }

        // Loading puts every tree in. A later restore is a friend's game keeping this copy in step (HavenSync shares the whole state
        // each time it changes): a tree not standing here yet goes in, none twice. Restored only once, every sapling the other game
        // planted after the first never showed here.
        foreach (Planted tree in state.trees)
        {
            int prototype = Standing(tree.position) ? -1 : PrototypeNamed(tree.prototype);
            if (prototype >= 0)
            {
                planted.Add(tree);
                Add(tree, prototype);
            }
        }

        // A guest's copy of the host's Haven (only its owner plants there, ItemInventory.Plant): a tree the host no longer has came
        // down there, so it comes down here too.
        if (!Place.InFriendsHaven || terrain == null)
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        for (int i = planted.Count - 1; i >= 0; i--)
        {
            Planted mine = planted[i];
            if (state.trees.Exists(tree => (tree.position - mine.position).sqrMagnitude < SameSpot * SameSpot))
            {
                continue;
            }

            if (mine.index >= 0 && mine.index < data.treeInstanceCount)
            {
                TreeInstance instance = data.GetTreeInstance(mine.index);
                instance.heightScale = 0f;
                instance.widthScale = 0f;
                data.SetTreeInstance(mine.index, instance);
            }

            planted.RemoveAt(i);
        }
    }

    private bool Standing(Vector3 position) => planted.Exists(tree => (tree.position - position).sqrMagnitude < SameSpot * SameSpot);
}
