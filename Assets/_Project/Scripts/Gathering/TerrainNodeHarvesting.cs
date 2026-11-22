using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets the player gather terrain-painted resources that are not trees (a raid map's mineable rocks), which are not objects
/// of their own. A painted kind is gatherable when its prototype prefab carries a ResourceNode and no ChoppableTree. Near the
/// player each one gets a live copy of its prefab in the same pose, undrawn while the terrain draws the original. Once
/// struck the copy is drawn instead (so it shakes and breaks like any node); when it is gathered the painted one stays
/// hidden until the node grows back by its own respawn time. Works on a runtime copy of the terrain data, like
/// TerrainTreeFelling, so the scene's terrain asset never changes.
/// <para>
/// A rock gathered and walked away from, or broken with no stand-in at all (a mason's quarry ring: <see cref="BreakByHand"/>),
/// is kept as gone with its time to grow back, so it stays hidden; where the woods are remembered (TerrainTreeMemory, the Haven)
/// that is saved with them, so a cleared stretch stays cleared after a raid or a reload.
/// </para>
/// <para>Added by itself to every terrain with tree prototypes when a scene loads; nothing to set up.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class TerrainNodeHarvesting : MonoBehaviour
{
    private const float ProxyRadius = 9f;
    private const float ReleaseRadius = 13f;
    private const float RefreshSeconds = 0.25f;

    private sealed class Stand
    {
        public ResourceNode Node;
        public Interactable Use;
        public Renderer[] Renderers;
        public TreeInstance Original;
        public bool Drawn;
        // Not let go before its break has played.
        public float SettledAt;
    }

    private readonly Dictionary<int, Stand> stands = new Dictionary<int, Stand>();
    // Painted rocks down with no stand-in (broken out of sight, walked away from, restored from a save): as they stood, and when
    // they grow back (infinity: never).
    private readonly Dictionary<int, (TreeInstance original, float regrowAt)> gone = new Dictionary<int, (TreeInstance, float)>();
    private readonly List<int> regrown = new List<int>();
    private float nextRegrowAt = float.PositiveInfinity;
    private static readonly List<TerrainNodeHarvesting> active = new List<TerrainNodeHarvesting>();

    /// <summary>Every terrain with painted resources to gather.</summary>
    public static IReadOnlyList<TerrainNodeHarvesting> Active => active;

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    /// <summary>This terrain's harvesting, added if it isn't there yet.</summary>
    public static TerrainNodeHarvesting On(Terrain terrain)
    {
        if (!terrain.TryGetComponent(out TerrainNodeHarvesting harvesting))
        {
            harvesting = terrain.gameObject.AddComponent<TerrainNodeHarvesting>();
        }

        // Added in the Editor (a test) its OnEnable doesn't run.
        if (!active.Contains(harvesting))
        {
            active.Add(harvesting);
        }

        return harvesting;
    }

    private void OnEnable()
    {
        if (!active.Contains(this))
        {
            active.Add(this);
        }
    }

    private void OnDisable() => active.Remove(this);
    private readonly List<Vector3> positions = new List<Vector3>();
    // The indices of the painted resources alone (a few thousand among a hundred thousand trees), for looks round a point.
    private readonly List<int> gatherableIndices = new List<int>();
    private readonly List<int> scratch = new List<int>();
    private Terrain terrain;
    private TerrainData cachedFor;
    private int cachedCount = -1;
    private bool[] gatherable = new bool[0];
    private bool any;
    private float nextRefresh;

    /// <summary>Painted resources gathered and not yet grown back.</summary>
    public int TakenCount
    {
        get
        {
            int taken = gone.Count;
            foreach (Stand stand in stands.Values)
            {
                if (stand.Node != null && stand.Node.IsDepleted) taken++;
            }

            return taken;
        }
    }

    /// <summary>The painted rocks down now, by index on the terrain, with the seconds until each grows back (infinity: never).</summary>
    public List<(int index, float secondsLeft)> GoneNow()
    {
        var list = new List<(int, float)>(gone.Count);
        foreach (var pair in gone)
        {
            list.Add((pair.Key, float.IsPositiveInfinity(pair.Value.regrowAt) ? float.PositiveInfinity : Mathf.Max(0f, pair.Value.regrowAt - Time.time)));
        }

        foreach (var pair in stands)
        {
            ResourceNode node = pair.Value.Node;
            if (node != null && node.IsDepleted)
            {
                bool regrows = node.Definition != null && node.Definition.RegrowsAfter > 0f;
                list.Add((pair.Key, regrows ? node.RespawnRemaining : float.PositiveInfinity));
            }
        }

        return list;
    }

    /// <summary>A number that changes when a painted rock is taken or grows back (co-op compares it, not the countdowns).</summary>
    public int GoneMark
    {
        get
        {
            unchecked
            {
                int mark = 0;
                foreach ((int index, float _) in GoneNow())
                {
                    mark += (index + 1) * -1640531535;
                }

                return mark;
            }
        }
    }

    /// <summary>Takes a painted rock away again as it was saved (nothing breaks): it stays gone for the seconds left.</summary>
    public void RestoreGone(int index, float secondsLeft)
    {
        Cache();
        if (index < 0 || index >= positions.Count || !gatherable[index] || gone.ContainsKey(index) || stands.ContainsKey(index))
        {
            return;
        }

        Take(index, float.IsPositiveInfinity(secondsLeft) ? float.PositiveInfinity : Time.time + Mathf.Max(0f, secondsLeft));
    }

    /// <summary>Whether the painted rock <paramref name="index"/> stands to be gathered (not gone, not a gathered stand-in).</summary>
    public bool IsPresent(int index)
    {
        Cache();
        if (index < 0 || index >= positions.Count || !gatherable[index] || gone.ContainsKey(index))
        {
            return false;
        }

        if (stands.TryGetValue(index, out Stand stand) && stand.Node != null)
        {
            return !stand.Node.IsDepleted;
        }

        return terrain.terrainData.GetTreeInstance(index).heightScale > 0f;
    }

    /// <summary>Where the painted rock <paramref name="index"/> stands.</summary>
    public Vector3 PositionOf(int index)
    {
        Cache();
        return index >= 0 && index < positions.Count ? positions[index] : transform.position;
    }

    /// <summary>The node its prefab carries (its definition and yield), for the painted rock <paramref name="index"/>.</summary>
    public ResourceNode PrefabNodeOf(int index)
    {
        Cache();
        if (index < 0 || index >= positions.Count || !gatherable[index])
        {
            return null;
        }

        TerrainData data = terrain.terrainData;
        GameObject prefab = data.treePrototypes[data.GetTreeInstance(index).prototypeIndex].prefab;
        return prefab != null ? prefab.GetComponent<ResourceNode>() : null;
    }

    /// <summary>The painted rocks standing within <paramref name="radius"/> of <paramref name="centre"/> (flat), by index.</summary>
    public void PresentNear(Vector3 centre, float radius, List<int> into)
    {
        into.Clear();
        Cache();
        float reach = radius * radius;
        foreach (int i in gatherableIndices)
        {
            Vector3 apart = positions[i] - centre;
            apart.y = 0f;
            if (apart.sqrMagnitude <= reach && IsPresent(i))
            {
                into.Add(i);
            }
        }
    }

    /// <summary>
    /// Breaks a painted rock with no player at it (a mason's quarry ring, RingWork): its stand-in, if the player is near and one stands,
    /// breaks as any node does; else it is taken away where it stands. <paramref name="quietly"/>: no sound or burst (broken while the
    /// player was away, laid down on coming home). False when it was already gone.
    /// </summary>
    public bool BreakByHand(int index, bool quietly = false)
    {
        if (!IsPresent(index))
        {
            return false;
        }

        if (stands.TryGetValue(index, out Stand stand) && stand.Node != null)
        {
            return stand.Node.BreakDown(quietly);
        }

        ResourceNode node = PrefabNodeOf(index);
        float respawn = node != null && node.Definition != null ? node.Definition.RegrowsAfter : 0f;
        Take(index, respawn > 0f ? Time.time + respawn : float.PositiveInfinity);
        return true;
    }

    // Hides a painted rock until regrowAt, with no stand-in.
    private void Take(int index, float regrowAt)
    {
        OwnTheData();
        TreeInstance original = terrain.terrainData.GetTreeInstance(index);
        gone[index] = (original, regrowAt);
        nextRegrowAt = Mathf.Min(nextRegrowAt, regrowAt);
        TreeInstance hidden = original;
        hidden.heightScale = 0f;
        hidden.widthScale = 0f;
        terrain.terrainData.SetTreeInstance(index, hidden);
        NodeChanged?.Invoke(positions[index]);
    }

    // Painted rocks gone with no stand-in whose time is up stand again.
    private void GrowBack()
    {
        if (Time.time < nextRegrowAt || gone.Count == 0)
        {
            return;
        }

        regrown.Clear();
        nextRegrowAt = float.PositiveInfinity;
        foreach (var pair in gone)
        {
            if (Time.time >= pair.Value.regrowAt)
            {
                regrown.Add(pair.Key);
            }
            else
            {
                nextRegrowAt = Mathf.Min(nextRegrowAt, pair.Value.regrowAt);
            }
        }

        foreach (int index in regrown)
        {
            OwnTheData();
            terrain.terrainData.SetTreeInstance(index, gone[index].original);
            gone.Remove(index);
            NodeChanged?.Invoke(positions[index]);
        }
    }

    /// <summary>
    /// Raised with where a painted resource stands when it is gathered (gone until it grows back) and when it grows back: what
    /// stands there has changed (GroundLightMap draws the cover there again).
    /// </summary>
    public event System.Action<Vector3> NodeChanged;

    /// <summary>True when painting this prefab makes something to gather: a resource node that isn't a tree.</summary>
    public static bool IsGatherable(GameObject prefab) =>
        prefab != null && prefab.GetComponent<ResourceNode>() != null && prefab.GetComponent<ChoppableTree>() == null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToTerrains();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToTerrains();

    // Every terrain that paints anything: a raid map swaps in its generated forest after loading, so the kinds are read later.
    private static void AttachToTerrains()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain != null && terrain.terrainData != null && terrain.terrainData.treePrototypes.Length > 0
                && !terrain.TryGetComponent(out TerrainNodeHarvesting _))
            {
                terrain.gameObject.AddComponent<TerrainNodeHarvesting>();
            }
        }
    }

    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        // Out of step with the tree felling on the same terrain, which refreshes as often.
        nextRefresh = Time.time + Random.value * RefreshSeconds;
    }

    private void Update()
    {
        if (Time.time < nextRefresh || Player.Current == null || terrain.terrainData == null)
        {
            return;
        }

        nextRefresh = Time.time + RefreshSeconds;
        Cache();
        if (!any)
        {
            return;
        }

        GrowBack();

        Vector3 player = Player.Current.transform.position;
        scratch.Clear();
        foreach (var pair in stands)
        {
            Stand stand = pair.Value;
            if (stand.Node == null)
            {
                scratch.Add(pair.Key);
                continue;
            }

            bool inUse = stand.Use != null && stand.Use.IsInUse;
            if (!stand.Drawn && inUse)
            {
                Draw(pair.Key, stand, true);
            }

            bool far = (positions[pair.Key] - player).sqrMagnitude > ReleaseRadius * ReleaseRadius;
            if (far && !stand.Node.IsDepleted && !inUse)
            {
                scratch.Add(pair.Key);
            }
            else if (far && stand.Node.IsDepleted && !inUse && Time.time >= stand.SettledAt)
            {
                // Gathered and walked away from: kept as gone (hidden, counting down) and its stand-in let go.
                bool regrows = stand.Node.Definition != null && stand.Node.Definition.RegrowsAfter > 0f;
                gone[pair.Key] = (stand.Original, regrows ? Time.time + stand.Node.RespawnRemaining : float.PositiveInfinity);
                nextRegrowAt = Mathf.Min(nextRegrowAt, gone[pair.Key].regrowAt);
                scratch.Add(pair.Key);
            }
        }

        foreach (int index in scratch)
        {
            Release(index);
        }

        for (int i = 0; i < positions.Count; i++)
        {
            // The distance before the lookup: nearly every painted tree is far.
            if (gatherable[i] && (positions[i] - player).sqrMagnitude <= ProxyRadius * ProxyRadius && !stands.ContainsKey(i))
            {
                Spawn(i);
            }
        }
    }

    private void Spawn(int index)
    {
        if (gone.ContainsKey(index))
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        TreeInstance painted = data.GetTreeInstance(index);
        if (painted.heightScale <= 0f)
        {
            return;
        }

        GameObject prefab = data.treePrototypes[painted.prototypeIndex].prefab;
        GameObject copy = Instantiate(prefab, positions[index], Quaternion.Euler(0f, painted.rotation * Mathf.Rad2Deg, 0f), transform);
        copy.name = prefab.name + " " + index;
        copy.transform.localScale = new Vector3(painted.widthScale, painted.heightScale, painted.widthScale);
        var stand = new Stand { Node = copy.GetComponent<ResourceNode>(), Use = copy.GetComponent<Interactable>(), Renderers = copy.GetComponentsInChildren<Renderer>(true), Original = painted };
        SetDrawn(stand, false);
        Vector3 at = positions[index];
        stand.Node.Swung += (_, __) => Draw(index, stand, true);
        stand.Node.Harvested += () =>
        {
            Draw(index, stand, true);
            stand.SettledAt = Time.time + 3f;
            NodeChanged?.Invoke(at);
        };
        stand.Node.Regrown += () =>
        {
            Draw(index, stand, false);
            NodeChanged?.Invoke(at);
        };
        stands[index] = stand;
    }

    // Exactly one of the two is drawn: the painted rock, or the live copy (struck, breaking, or gone until it grows back).
    private void Draw(int index, Stand stand, bool copy)
    {
        if (stand.Drawn == copy)
        {
            return;
        }

        OwnTheData();
        stand.Drawn = copy;
        TreeInstance shown = stand.Original;
        if (copy)
        {
            shown.heightScale = 0f;
            shown.widthScale = 0f;
        }

        terrain.terrainData.SetTreeInstance(index, shown);
        SetDrawn(stand, copy);
    }

    private static void SetDrawn(Stand stand, bool drawn)
    {
        foreach (Renderer part in stand.Renderers)
        {
            if (part != null) part.enabled = drawn && !(stand.Node != null && stand.Node.IsDepleted);
        }
    }

    private void Release(int index)
    {
        if (stands.TryGetValue(index, out Stand stand))
        {
            if (stand.Drawn && stand.Node != null && !stand.Node.IsDepleted && !gone.ContainsKey(index))
            {
                Draw(index, stand, false);
            }

            if (stand.Node != null) Destroy(stand.Node.gameObject);
        }

        stands.Remove(index);
    }

    // Positions of the painted kinds and which can be gathered; again when the data is swapped (a raid map turns its terrain).
    private void Cache()
    {
        // Added in the Editor (a test) its Awake doesn't run.
        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        TerrainData data = terrain.terrainData;
        if (data == cachedFor && data.treeInstanceCount == cachedCount)
        {
            return;
        }

        // Trees are only ever added to the same data (a sapling planted): the instances already counted are the same ones, and a
        // gathered rock waiting to grow back stays so. Treated as a new map, it was put back hidden for the rest of the visit.
        bool sameInstances = data == cachedFor ? data.treeInstanceCount >= cachedCount : data.treeInstanceCount == cachedCount;
        foreach (int index in new List<int>(stands.Keys))
        {
            // A gathered rock keeps counting down to growing back, unless the map itself changed under it.
            if (!sameInstances || !stands[index].Node.IsDepleted) Release(index);
        }

        if (!sameInstances)
        {
            gone.Clear();
            nextRegrowAt = float.PositiveInfinity;
        }

        cachedFor = data;
        cachedCount = data.treeInstanceCount;
        TreePrototype[] prototypes = data.treePrototypes;
        var kinds = new bool[prototypes.Length];
        any = false;
        for (int p = 0; p < prototypes.Length; p++)
        {
            kinds[p] = IsGatherable(prototypes[p].prefab);
            any |= kinds[p];
        }

        TreeInstance[] trees = data.treeInstances;
        positions.Clear();
        gatherableIndices.Clear();
        gatherable = new bool[trees.Length];
        Vector3 size = data.size;
        Vector3 origin = terrain.GetPosition();
        for (int i = 0; i < trees.Length; i++)
        {
            positions.Add(origin + Vector3.Scale(trees[i].position, size));
            gatherable[i] = trees[i].prototypeIndex < kinds.Length && kinds[trees[i].prototypeIndex];
            if (gatherable[i])
            {
                gatherableIndices.Add(i);
            }
        }
    }

    // The scene's terrain data is an asset: change a copy (one TerrainTreeFelling made, or a raid map's generated one, will do).
    private void OwnTheData()
    {
        TerrainData copy = RuntimeTerrain.Own(terrain);
        if (copy != null)
        {
            cachedFor = copy;
        }
    }
}
