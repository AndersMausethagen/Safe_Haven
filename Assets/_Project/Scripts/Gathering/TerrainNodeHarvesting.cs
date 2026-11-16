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
    }

    private readonly Dictionary<int, Stand> stands = new Dictionary<int, Stand>();
    private readonly List<Vector3> positions = new List<Vector3>();
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
            int taken = 0;
            foreach (Stand stand in stands.Values)
            {
                if (stand.Node != null && stand.Node.IsDepleted) taken++;
            }

            return taken;
        }
    }

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
        stand.Node.Swung += (_, __) => Draw(index, stand, true);
        stand.Node.Harvested += () => Draw(index, stand, true);
        stand.Node.Regrown += () => Draw(index, stand, false);
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
            if (stand.Drawn && stand.Node != null && !stand.Node.IsDepleted)
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
        gatherable = new bool[trees.Length];
        Vector3 size = data.size;
        Vector3 origin = terrain.GetPosition();
        for (int i = 0; i < trees.Length; i++)
        {
            positions.Add(origin + Vector3.Scale(trees[i].position, size));
            gatherable[i] = trees[i].prototypeIndex < kinds.Length && kinds[trees[i].prototypeIndex];
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
