using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets the player fell terrain trees (painted trees, such as a raid map's forest), which are not objects of their own.
/// Near the player each choppable tree (its prototype prefab has a ChoppableTree) gets an invisible stand-in from
/// TreeChoppingSettings › Terrain Tree Proxy: the Tree gathering node, sized like the tree. Felling it hides the terrain
/// tree (scale 0, so it neither shows nor blocks) as a copy comes down in its place (FelledTree), until the node's respawn
/// time has passed, then it grows back. Felling works on a runtime copy of the terrain data, so the scene's terrain asset
/// is never changed.
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
    private const string RuntimeSuffix = " (runtime)";

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

    private readonly Dictionary<int, ChoppableTree> proxies = new Dictionary<int, ChoppableTree>();
    private readonly Dictionary<int, (TreeInstance tree, GameObject copy)> swaying = new Dictionary<int, (TreeInstance, GameObject)>();
    private readonly Dictionary<int, (TreeInstance tree, float regrowAt)> felled = new Dictionary<int, (TreeInstance, float)>();
    private readonly List<int> scratch = new List<int>();
    private readonly List<Vector3> positions = new List<Vector3>();
    private Terrain terrain;
    private TerrainData cachedFor;
    private int cachedCount = -1;
    private bool[] choppable = new bool[0];
    private float[] prototypeHeight = new float[0];
    private float[] prototypeRadius = new float[0];
    private float nextRefresh;

    /// <summary>Terrain trees felled and not yet grown back.</summary>
    public int FelledCount => felled.Count;

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
        felled[index] = (tree, Time.time + secondsLeft);
        Hide(index, tree);
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

    private void Awake() => terrain = GetComponent<Terrain>();

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
                && !pair.Value.GetComponent<Interactable>().IsInUse)
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
            if (choppable[i] && !proxies.ContainsKey(i) && !felled.ContainsKey(i) && (positions[i] - player).sqrMagnitude <= ProxyRadius * ProxyRadius)
            {
                Spawn(i);
            }
        }
    }

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
        choppable = new bool[trees.Length];
        Vector3 size = data.size;
        Vector3 origin = terrain.GetPosition();
        for (int i = 0; i < trees.Length; i++)
        {
            positions.Add(origin + Vector3.Scale(trees[i].position, size));
            choppable[i] = trees[i].prototypeIndex < canFell.Length && canFell[trees[i].prototypeIndex];
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
        GameObject proxy = Instantiate(TreeChoppingSettings.Current.TerrainTreeProxy, positions[index], Quaternion.identity, transform);
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
        choppableTree.SetStandIn(() => TreeParts.Of(prefab, at, turned, sized));
        var node = proxy.GetComponent<ResourceNode>();
        int felledIndex = index;
        node.Harvested += () => Fell(felledIndex);
        proxies[index] = choppableTree;
    }

    private void Release(int index)
    {
        if (proxies.TryGetValue(index, out ChoppableTree proxy) && proxy != null)
        {
            Destroy(proxy.gameObject);
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

        float respawn = TreeChoppingSettings.Current.TerrainTreeRegrowSeconds;
        felled[index] = (tree, respawn > 0f ? Time.time + respawn : float.PositiveInfinity);
        Hide(index, tree);
        Release(index);
    }

    private void RegrowDue()
    {
        if (felled.Count == 0)
        {
            return;
        }

        scratch.Clear();
        foreach (var pair in felled)
        {
            if (Time.time >= pair.Value.regrowAt)
            {
                scratch.Add(pair.Key);
            }
        }

        foreach (int index in scratch)
        {
            terrain.terrainData.SetTreeInstance(index, felled[index].tree);
            felled.Remove(index);
        }
    }

    // The scene's terrain data is an asset: fell on a copy (a raid map's turned terrain already is one).
    private void OwnTheData()
    {
        TerrainData data = terrain.terrainData;
        if (data.name.EndsWith(RuntimeSuffix) || data.name.EndsWith("(turned)"))
        {
            return;
        }

        TerrainData copy = Instantiate(data);
        copy.name = data.name + RuntimeSuffix;
        terrain.terrainData = copy;
        if (terrain.TryGetComponent(out TerrainCollider ground))
        {
            ground.terrainData = copy;
        }

        cachedFor = copy;
    }
}
