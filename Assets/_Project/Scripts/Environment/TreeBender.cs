using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes whole trees ("Safe Haven/Tree Wind", terrain trees and placed ones alike) lean away from this object as it
/// shoves through the wood: the old troll, a giant. The nearer the trunk, the further the crown is pushed, trembling,
/// and it rights itself as the shover moves on. Small plants are already pushed by <see cref="GrassInteractor"/>; this
/// bends trunks. Up to <see cref="Max"/> at once, nearest the camera first. Positions go to the shader as globals
/// (_TreeBenders = x, z, radius, lean in radians; _TreeBenderCount). With a Creak sound, tall trees it reaches groan.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Environment/Tree Bender")]
public sealed class TreeBender : MonoBehaviour
{
    public const int Max = 4;

    [Tooltip("Metres round it in which trees lean away (at the edge hardly, at its middle the most).")]
    [SerializeField, Min(0.5f)] private float radius = 6f;
    [Tooltip("How far a trunk right beside it leans, in degrees.")]
    [SerializeField, Range(0f, 45f)] private float lean = 24f;
    [Tooltip("Optional: played at a tall tree when it first starts to lean (a creak, a groan of wood).")]
    [SerializeField] private SoundDefinition creak;
    [Tooltip("Seconds between two creaks at least.")]
    [SerializeField, Min(0.1f)] private float creakGap = 0.7f;

    private static readonly List<TreeBender> Active = new List<TreeBender>();
    private static readonly Vector4[] Data = new Vector4[Max];
    private static readonly int BendersId = Shader.PropertyToID("_TreeBenders");
    private static readonly int CountId = Shader.PropertyToID("_TreeBenderCount");
    private static int uploadedFrame = -1;

    // Where each terrain's tall trees stand (world), found once per terrain.
    private static readonly Dictionary<TerrainData, Vector3[]> TallTrees = new Dictionary<TerrainData, Vector3[]>();

    private readonly HashSet<Vector3> leaning = new HashSet<Vector3>();
    private readonly List<Vector3> stillLeaning = new List<Vector3>();
    private float nextLook;
    private float nextCreak;

    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0.5f, value);
    }

    public float Lean
    {
        get => lean;
        set => lean = Mathf.Clamp(value, 0f, 45f);
    }

    /// <summary>Benders sent to the shader (after <see cref="Upload"/>).</summary>
    public static int UploadedCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Active.Clear();
        TallTrees.Clear();
        uploadedFrame = -1;
        UploadedCount = 0;
        Shader.SetGlobalFloat(CountId, 0f);
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }

        Upload();
    }

    private void OnDisable()
    {
        Active.Remove(this);
        leaning.Clear();
        Upload();
    }

    private void LateUpdate()
    {
        if (uploadedFrame != Time.frameCount)
        {
            uploadedFrame = Time.frameCount;
            Upload();
        }

        if (creak != null && Time.time >= nextLook)
        {
            nextLook = Time.time + 0.25f;
            Listen();
        }
    }

    // Sorted against a static eye with one cached comparison: a lambda capturing the eye was a new delegate every frame.
    private static Vector3 sortEye;
    private static readonly System.Comparison<TreeBender> NearestFirst =
        (a, b) => (a.transform.position - sortEye).sqrMagnitude.CompareTo((b.transform.position - sortEye).sqrMagnitude);

    /// <summary>Sends the active benders to the tree shader; returns how many were sent.</summary>
    public static int Upload()
    {
        Active.RemoveAll(bender => bender == null);
        if (Active.Count > Max)
        {
            Camera camera = Camera.main;
            sortEye = camera != null ? camera.transform.position : Vector3.zero;
            Active.Sort(NearestFirst);
        }

        int count = Mathf.Min(Active.Count, Max);
        for (int i = 0; i < Max; i++)
        {
            if (i < count)
            {
                TreeBender bender = Active[i];
                Vector3 at = bender.transform.position;
                Data[i] = new Vector4(at.x, at.z, bender.radius, bender.lean * Mathf.Deg2Rad);
            }
            else
            {
                Data[i] = Vector4.zero;
            }
        }

        Shader.SetGlobalVectorArray(BendersId, Data);
        Shader.SetGlobalFloat(CountId, count);
        UploadedCount = count;
        return count;
    }

    // A creak for each tall tree that has just come within reach.
    private void Listen()
    {
        Vector3 at = transform.position;
        float reach = radius * 0.7f;
        stillLeaning.Clear();
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            foreach (Vector3 tree in TallTreesOf(terrain))
            {
                float dx = tree.x - at.x, dz = tree.z - at.z;
                if (dx * dx + dz * dz > reach * reach)
                {
                    continue;
                }

                stillLeaning.Add(tree);
                if (!leaning.Contains(tree) && Time.time >= nextCreak)
                {
                    nextCreak = Time.time + creakGap;
                    AudioManager.Play(creak, tree + Vector3.up * 3f);
                }
            }
        }

        leaning.Clear();
        leaning.UnionWith(stillLeaning);
    }

    private static Vector3[] TallTreesOf(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        if (data == null)
        {
            return System.Array.Empty<Vector3>();
        }

        if (TallTrees.TryGetValue(data, out Vector3[] found))
        {
            return found;
        }

        TreePrototype[] prototypes = data.treePrototypes;
        var height = new float[prototypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            Renderer drawn = prototypes[i].prefab != null ? prototypes[i].prefab.GetComponentInChildren<Renderer>() : null;
            bool tree = drawn != null && drawn.sharedMaterial != null && drawn.sharedMaterial.shader != null
                && drawn.sharedMaterial.shader.name == "Safe Haven/Tree Wind";
            height[i] = tree ? drawn.bounds.size.y : 0f;
        }

        var tall = new List<Vector3>();
        Vector3 size = data.size;
        Vector3 origin = terrain.transform.position;
        foreach (TreeInstance instance in data.treeInstances)
        {
            int kind = instance.prototypeIndex;
            if (kind >= 0 && kind < height.Length && height[kind] * instance.heightScale >= 5f)
            {
                tall.Add(origin + Vector3.Scale(instance.position, size));
            }
        }

        found = tall.ToArray();
        TallTrees[data] = found;
        return found;
    }
}
