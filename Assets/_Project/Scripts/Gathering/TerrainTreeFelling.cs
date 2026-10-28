using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets the player fell terrain trees (painted trees, such as a raid map's forest), which are not objects of their own.
/// Near the player each choppable tree (its prototype prefab has a ChoppableTree) gets an invisible stand-in from
/// TreeChoppingSettings › Terrain Tree Proxy: the Tree gathering node, sized like the tree. Felling it shrinks the
/// terrain tree away (scale 0, so it neither shows nor blocks) until the node's respawn time has passed, then it grows
/// back. Felling works on a runtime copy of the terrain data, so the scene's terrain asset is never changed.
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

    private readonly Dictionary<int, ChoppableTree> proxies = new Dictionary<int, ChoppableTree>();
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
        float respawn = TreeChoppingSettings.Current.TerrainTreeRegrowSeconds;
        felled[index] = (tree, respawn > 0f ? Time.time + respawn : float.PositiveInfinity);
        TreeInstance gone = tree;
        gone.heightScale = 0f;
        gone.widthScale = 0f;
        data.SetTreeInstance(index, gone);
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
