using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets the player fell terrain trees (painted trees, such as a raid map's forest), which are not objects of their own.
/// Near the player each choppable tree (its prototype prefab has a ChoppableTree) gets an invisible stand-in from
/// TreeChoppingSettings › Terrain Tree Proxy: the Tree gathering node, sized like the tree. Felling it hides the terrain
/// tree (scale 0, so it neither shows nor blocks) as a copy comes down in its place (FelledTree), until the node's respawn
/// time has passed, then it grows back. A tree shorter than Small Tree Height isn't felled: its stand-in is Small Tree
/// Proxy, broken down by hand for sticks. Where the woods are remembered (TerrainTreeMemory › Keep Felled: the Haven) a
/// tree that comes down stays down; saplings planted there (PlantedTrees) are what grows back. Felling works on a runtime
/// copy of the terrain data, so the scene's terrain asset is never changed.
/// <para>
/// Added by itself to every terrain with choppable tree prototypes when a scene loads; nothing to set up.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class TerrainTreeFelling : MonoBehaviour
{
    private const float ProxyRadius = 10f;
    private const float ReleaseRadius = 14f;
    private const float RefreshSeconds = 0.25f;
    // The choppable trees by ground cell, so a refresh looks at those round the player, not every tree (the Haven has 130 000).
    private const float CellSize = 16f;

    /// <summary>A painted tree a falling tree might meet.</summary>
    public readonly struct Standing
    {
        public readonly int Index;
        public readonly Vector3 Foot;
        public readonly float Height;
        public readonly float Radius;

        public Standing(int index, Vector3 foot, float height, float radius)
        {
            Index = index;
            Foot = foot;
            Height = height;
            Radius = radius;
        }
    }

    private static readonly List<TerrainTreeFelling> active = new List<TerrainTreeFelling>();

    private readonly Dictionary<int, GameObject> proxies = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, (TreeInstance tree, GameObject copy)> swaying = new Dictionary<int, (TreeInstance, GameObject)>();
    private readonly Dictionary<int, (TreeInstance tree, float regrowAt)> felled = new Dictionary<int, (TreeInstance, float)>();
    private readonly List<int> scratch = new List<int>();
    private readonly List<Vector3> positions = new List<Vector3>();
    private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
    private float nextRegrowAt = float.PositiveInfinity;
    private Terrain terrain;
    private TerrainData cachedFor;
    private int cachedCount = -1;
    private bool[] choppable = new bool[0];
    private float[] prototypeHeight = new float[0];
    private float[] prototypeRadius = new float[0];
    private float nextRefresh;

    /// <summary>Terrain trees felled and not yet grown back.</summary>
    public int FelledCount => felled.Count;

    /// <summary>Trees that come down here stay down (the Haven's remembered woods: TerrainTreeMemory › Keep Felled).</summary>
    public bool KeepsFelled => TryGetComponent(out TerrainTreeMemory memory) && memory.KeepsFelled;

    /// <summary>Raised with a tree's index when it is felled or broken down.</summary>
    public event System.Action<int> TreeDown;

    /// <summary>Raised with a tree's index when it grows back where it was felled.</summary>
    public event System.Action<int> TreeBack;

    /// <summary>The terrain's own runtime copy of its data, made now if it hasn't been: what trees may be added to.</summary>
    public TerrainData OwnedData()
    {
        OwnTheData();
        return terrain.terrainData;
    }

    /// <summary>The trees down now, by index on the terrain, with the seconds until each grows back (infinity: never).</summary>
    public List<(int index, float secondsLeft)> FelledNow()
    {
        var list = new List<(int, float)>(felled.Count);
        foreach (var pair in felled)
        {
            list.Add((pair.Key, float.IsPositiveInfinity(pair.Value.regrowAt) ? float.PositiveInfinity : Mathf.Max(0f, pair.Value.regrowAt - Time.time)));
        }

        return list;
    }

    /// <summary>A number that changes when a tree falls or grows back (co-op compares it, not the countdowns).</summary>
    public int FelledMark
    {
        get
        {
            unchecked
            {
                int mark = felled.Count;
                foreach (int index in felled.Keys)
                {
                    mark += (index + 1) * -1640531535;
                }

                return mark;
            }
        }
    }

    /// <summary>Lays a tree down again as it was saved (no falling copy): it stays down for the seconds left.</summary>
    public void RestoreFelled(int index, float secondsLeft)
    {
        OwnTheData();
        TerrainData data = terrain.terrainData;
        if (index < 0 || index >= data.treeInstanceCount || felled.ContainsKey(index))
        {
            return;
        }

        TreeInstance tree = data.GetTreeInstance(index);
        felled[index] = (tree, KeepsFelled ? float.PositiveInfinity : Time.time + secondsLeft);
        nextRegrowAt = Mathf.Min(nextRegrowAt, felled[index].regrowAt);
        Hide(index, tree);
    }

    /// <summary>Fells a standing tree without a stand-in (a resident's felling ring, RingWork): it goes down as the player's would, and stays
    /// down where the woods are remembered.</summary>
    public void FellByHand(int index)
    {
        if (terrain == null || terrain.terrainData == null || felled.ContainsKey(index) || index < 0 || index >= terrain.terrainData.treeInstanceCount)
        {
            return;
        }

        Fell(index);
    }

    /// <summary>This terrain's felling, added if it isn't there yet.</summary>
    public static TerrainTreeFelling On(Terrain terrain)
    {
        if (!terrain.TryGetComponent(out TerrainTreeFelling felling))
        {
            felling = terrain.gameObject.AddComponent<TerrainTreeFelling>();
            felling.terrain = terrain;
        }

        return felling;
    }

    /// <summary>Every terrain with fellable trees.</summary>
    public static IReadOnlyList<TerrainTreeFelling> Active => active;

    private void OnEnable() => active.Add(this);

    private void OnDisable() => active.Remove(this);

    /// <summary>The fellable trees still standing within a distance of a point.</summary>
    public List<Standing> StandingNear(Vector3 point, float distance)
    {
        var near = new List<Standing>();
        if (terrain == null || terrain.terrainData == null)
        {
            return near;
        }

        Cache();
        TerrainData data = terrain.terrainData;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 offset = positions[i] - point;
            offset.y = 0f;
            if (choppable[i] && !felled.ContainsKey(i) && offset.sqrMagnitude <= distance * distance && offset.sqrMagnitude > 0.25f)
            {
                TreeInstance tree = data.GetTreeInstance(i);
                near.Add(new Standing(i, positions[i], prototypeHeight[tree.prototypeIndex] * Mathf.Max(0.1f, tree.heightScale),
                    prototypeRadius[tree.prototypeIndex] * Mathf.Max(0.1f, tree.widthScale)));
            }
        }

        return near;
    }

    /// <summary>
    /// A falling tree has hit a painted one. A terrain tree can't lean, so a copy stands in its place for as long as it sways,
    /// and the painted tree comes back upright when it stills. Returns its parts for the leaves it sheds.
    /// </summary>
    public TreeParts Knocked(int index, Vector3 direction, float degrees)
    {
        if (swaying.TryGetValue(index, out (TreeInstance tree, GameObject copy) already))
        {
            TreeSway.Knock(already.copy, direction, degrees, 0f);
            return null;
        }

        TerrainData data = terrain.terrainData;
        TreeInstance tree = data.GetTreeInstance(index);
        GameObject prefab = data.treePrototypes[tree.prototypeIndex].prefab;
        TreeParts parts = TreeParts.Of(prefab, positions[index], Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f), new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
        var copy = new GameObject("Swaying Tree");
        copy.transform.position = positions[index];
        parts.BuildModel(copy.transform);
        OwnTheData();
        swaying[index] = (tree, copy);
        Hide(index, tree);
        TreeSway.Knock(copy, direction, degrees, 0f, () => StandBackUp(index));
        return parts;
    }

    private void StandBackUp(int index)
    {
        if (swaying.TryGetValue(index, out (TreeInstance tree, GameObject copy) stood))
        {
            swaying.Remove(index);
            Destroy(stood.copy);
            terrain.terrainData.SetTreeInstance(index, stood.tree);
        }
    }

    private void Hide(int index, TreeInstance tree)
    {
        TreeInstance gone = tree;
        gone.heightScale = 0f;
        gone.widthScale = 0f;
        terrain.terrainData.SetTreeInstance(index, gone);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToTerrains();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToTerrains();

    private static void AttachToTerrains()
    {
        if (TreeChoppingSettings.Current.TerrainTreeProxy == null)
        {
            return;
        }

        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain != null && terrain.terrainData != null && !terrain.TryGetComponent(out TerrainTreeFelling _) && HasChoppable(terrain.terrainData))
            {
                terrain.gameObject.AddComponent<TerrainTreeFelling>();
            }
        }
    }

    private static bool HasChoppable(TerrainData data)
    {
        foreach (TreePrototype prototype in data.treePrototypes)
        {
            if (prototype.prefab != null && prototype.prefab.GetComponent<ChoppableTree>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        // Out of step with the node streamers on the same terrain, which refresh as often.
        nextRefresh = Time.time + Random.value * RefreshSeconds;
    }

    private void Update()
    {
        RegrowDue();
        if (Time.time < nextRefresh || Player.Current == null || terrain.terrainData == null)
        {
            return;
        }

        nextRefresh = Time.time + RefreshSeconds;
        Cache();
        Vector3 player = Player.Current.transform.position;

        scratch.Clear();
        foreach (var pair in proxies)
        {
            if (pair.Value == null || (positions[pair.Key] - player).sqrMagnitude > ReleaseRadius * ReleaseRadius
                && !(pair.Value.TryGetComponent(out Interactable work) && work.IsInUse))
            {
                scratch.Add(pair.Key);
            }
        }

        foreach (int index in scratch)
        {
            Release(index);
        }

        int reach = Mathf.CeilToInt(ProxyRadius / CellSize);
        int cellX = Mathf.FloorToInt(player.x / CellSize), cellZ = Mathf.FloorToInt(player.z / CellSize);
        for (int x = cellX - reach; x <= cellX + reach; x++)
        {
            for (int z = cellZ - reach; z <= cellZ + reach; z++)
            {
                if (!cells.TryGetValue(CellKey(x, z), out List<int> near))
                {
                    continue;
                }

                foreach (int i in near)
                {
                    if ((positions[i] - player).sqrMagnitude <= ProxyRadius * ProxyRadius && !proxies.ContainsKey(i) && !felled.ContainsKey(i))
                    {
                        Spawn(i);
                    }
                }
            }
        }
    }

    private static long CellKey(int x, int z) => ((long)x << 32) | (uint)z;

    // World positions of the trees and which prototypes can be felled; again when the data is swapped (a raid map turns
    // its terrain) or trees are added.
    private void Cache()
    {
        TerrainData data = terrain.terrainData;
        if (data == cachedFor && data.treeInstanceCount == cachedCount)
        {
            return;
        }

        foreach (int index in new List<int>(proxies.Keys))
        {
            Release(index);
        }

        cachedFor = data;
        cachedCount = data.treeInstanceCount;
        TreePrototype[] prototypes = data.treePrototypes;
        prototypeHeight = new float[prototypes.Length];
        prototypeRadius = new float[prototypes.Length];
        bool[] canFell = new bool[prototypes.Length];
        for (int p = 0; p < prototypes.Length; p++)
        {
            GameObject prefab = prototypes[p].prefab;
            canFell[p] = prefab != null && prefab.GetComponent<ChoppableTree>() != null;
            if (canFell[p])
            {
                Measure(prefab, out prototypeHeight[p], out prototypeRadius[p]);
            }
        }

        TreeInstance[] trees = data.treeInstances;
        positions.Clear();
        cells.Clear();
        choppable = new bool[trees.Length];
        Vector3 size = data.size;
        Vector3 origin = terrain.GetPosition();
        for (int i = 0; i < trees.Length; i++)
        {
            Vector3 at = origin + Vector3.Scale(trees[i].position, size);
            positions.Add(at);
            choppable[i] = trees[i].prototypeIndex < canFell.Length && canFell[trees[i].prototypeIndex];
            if (choppable[i])
            {
                long key = CellKey(Mathf.FloorToInt(at.x / CellSize), Mathf.FloorToInt(at.z / CellSize));
                if (!cells.TryGetValue(key, out List<int> cell))
                {
                    cells[key] = cell = new List<int>();
                }

                cell.Add(i);
            }
        }
    }

    private static void Measure(GameObject prefab, out float height, out float radius)
    {
        height = 8f;
        radius = 0.3f;
        var bounds = new Bounds();
        bool any = false;
        foreach (Renderer part in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (any)
            {
                bounds.Encapsulate(part.bounds);
            }
            else
            {
                bounds = part.bounds;
                any = true;
            }
        }

        if (any)
        {
            height = bounds.size.y;
        }

        if (prefab.GetComponentInChildren<CapsuleCollider>(true) is CapsuleCollider trunk && trunk != null)
        {
            radius = trunk.radius * Mathf.Max(trunk.transform.lossyScale.x, trunk.transform.lossyScale.z);
        }
    }

    private void Spawn(int index)
    {
        TreeInstance tree = terrain.terrainData.GetTreeInstance(index);
        TreeChoppingSettings settings = TreeChoppingSettings.Current;
        float standing = prototypeHeight[tree.prototypeIndex] * tree.heightScale;
        if (standing < settings.SmallTreeHeight && settings.SmallTreeProxy != null)
        {
            SpawnSmall(index, tree);
            return;
        }

        GameObject proxy = Instantiate(settings.TerrainTreeProxy, positions[index], Quaternion.identity, transform);
        proxy.name = "Tree " + index;
        float scale = Mathf.Max(0.1f, tree.heightScale);
        if (proxy.TryGetComponent(out CapsuleCollider trunk))
        {
            trunk.radius = prototypeRadius[tree.prototypeIndex] * Mathf.Max(0.1f, tree.widthScale);
            trunk.height = Mathf.Max(trunk.radius * 2f, 3f * scale);
            trunk.center = Vector3.up * trunk.height * 0.5f;
        }

        var choppableTree = proxy.GetComponent<ChoppableTree>();
        choppableTree.SetSize(TreeChoppingSettings.Current.SizeOf(prototypeHeight[tree.prototypeIndex] * scale));
        Vector3 at = positions[index];
        GameObject prefab = terrain.terrainData.treePrototypes[tree.prototypeIndex].prefab;
        Quaternion turned = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
        var sized = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
        // Its stump lasts as long as it takes to grow back: for good where the woods are remembered.
        choppableTree.SetStandIn(() =>
        {
            TreeParts parts = TreeParts.Of(prefab, at, turned, sized);
            parts.RegrowSeconds = KeepsFelled ? -1f : 0f;
            return parts;
        });
        var node = proxy.GetComponent<ResourceNode>();
        int felledIndex = index;
        node.Harvested += () => Fell(felledIndex);
        proxies[index] = proxy;
    }

    // A small tree: broken down by hand (sticks and a sapling), no axe and no fall.
    private void SpawnSmall(int index, TreeInstance tree)
    {
        GameObject proxy = Instantiate(TreeChoppingSettings.Current.SmallTreeProxy, positions[index], Quaternion.identity, transform);
        proxy.name = "Small Tree " + index;
        if (proxy.TryGetComponent(out CapsuleCollider trunk))
        {
            trunk.radius = Mathf.Max(0.25f, prototypeRadius[tree.prototypeIndex] * Mathf.Max(0.1f, tree.widthScale));
            trunk.height = Mathf.Max(trunk.radius * 2f, prototypeHeight[tree.prototypeIndex] * tree.heightScale * 0.5f);
            trunk.center = Vector3.up * trunk.height * 0.5f;
        }

        int broken = index;
        proxy.GetComponent<ResourceNode>().Harvested += () => Break(broken);
        proxies[index] = proxy;
    }

    private void Break(int index)
    {
        OwnTheData();
        TreeInstance tree = terrain.terrainData.GetTreeInstance(index);
        // The tree's own leaves (a young spruce sheds needles, not broad leaves), in its colour, from its crown.
        GameObject prefab = terrain.terrainData.treePrototypes[tree.prototypeIndex].prefab;
        TreeParts parts = TreeParts.Of(prefab, positions[index], Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f),
            new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
        GameObject burst = parts.LeafFall != null ? parts.LeafFall : TreeChoppingSettings.Current.SmallTreeBreak;
        if (parts.HasFoliage)
        {
            TreeEffects.Play(burst, parts.Base + parts.Crown.center, Quaternion.identity, parts.Crown.size, parts.LeafTint,
                Mathf.RoundToInt(Mathf.Clamp(parts.Height * 8f, 15f, 60f)));
        }
        else if (burst != null)
        {
            TreeEffects.Play(burst, positions[index] + Vector3.up, Quaternion.identity, Vector3.one * 1.5f, parts.LeafTint, 25);
        }

        Down(index, tree);
    }

    // The tree is gone from where it stood: until it grows back, or for good where the woods are remembered.
    private void Down(int index, TreeInstance tree)
    {
        float respawn = TreeChoppingSettings.Current.TerrainTreeRegrowSeconds;
        felled[index] = (tree, KeepsFelled || respawn <= 0f ? float.PositiveInfinity : Time.time + respawn);
        nextRegrowAt = Mathf.Min(nextRegrowAt, felled[index].regrowAt);
        Hide(index, tree);
        Release(index);
        TreeDown?.Invoke(index);
    }

    private void Release(int index)
    {
        if (proxies.TryGetValue(index, out GameObject proxy) && proxy != null)
        {
            Destroy(proxy);
        }

        proxies.Remove(index);
    }

    private void Fell(int index)
    {
        OwnTheData();
        TerrainData data = terrain.terrainData;
        TreeInstance tree = data.GetTreeInstance(index);
        // Felled while it sways: the real tree is the one put aside, and it is the copy that goes.
        if (swaying.TryGetValue(index, out (TreeInstance tree, GameObject copy) moving))
        {
            tree = moving.tree;
            swaying.Remove(index);
            Destroy(moving.copy);
        }

        Down(index, tree);
    }

    // Only when the soonest is due (at home nothing grows back: every frame it walked every tree down since the save).
    private void RegrowDue()
    {
        if (felled.Count == 0 || Time.time < nextRegrowAt)
        {
            return;
        }

        scratch.Clear();
        nextRegrowAt = float.PositiveInfinity;
        foreach (var pair in felled)
        {
            if (Time.time >= pair.Value.regrowAt)
            {
                scratch.Add(pair.Key);
            }
            else
            {
                nextRegrowAt = Mathf.Min(nextRegrowAt, pair.Value.regrowAt);
            }
        }

        foreach (int index in scratch)
        {
            terrain.terrainData.SetTreeInstance(index, felled[index].tree);
            felled.Remove(index);
            TreeBack?.Invoke(index);
        }
    }

    // The scene's terrain data is an asset: fell on a copy (a raid map's turned terrain already is one).
    private void OwnTheData()
    {
        TerrainData copy = RuntimeTerrain.Own(terrain);
        if (copy != null)
        {
            cachedFor = copy;
        }
    }
}
