using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Needles, twigs and bits of bark drifting down from the crowns around the camera: the canopy shedding. Each piece starts
/// inside the crown of a real tall tree near what the camera looks at (painted terrain trees), so nothing falls over a
/// clearing, then falls. In calm weather (clear, mist, fog) a few drop nearly straight down, turning a little; once it is
/// breezy the shared wind (MossWind's direction and tree strength) carries them, and a storm strips the crowns and drives
/// them along. Purely visual; keeps about <see cref="aloft"/> pieces in the air (half that when calm), fewer by night.
/// <para>Where it belongs: the Canopy Debris prefab in a forest scene (set up for the Misty Woods by BuildMistyWoodsForest,
/// for the Haven by BuildSafeHavenForest).</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class CanopyDebris : MonoBehaviour
{
    private const int MostPerFrame = 4;
    private const float RecacheSeconds = 2f;
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");
    private static readonly int TreeWindId = Shader.PropertyToID("_SafeHavenTreeWind");

    [Tooltip("Pieces in the air at once in calm daylight.")]
    [SerializeField, Min(0)] private int aloft = 90;
    [Tooltip("Metres around what the camera looks at where crowns shed.")]
    [SerializeField, Min(4f)] private float radius = 26f;
    [Tooltip("Only trees at least this tall shed (metres): saplings have no canopy.")]
    [SerializeField, Min(1f)] private float tallerThan = 7f;
    [Tooltip("How far a piece is carried sideways per unit of wind, in metres per second.")]
    [SerializeField, Min(0f)] private float drift = 1.2f;
    [Tooltip("How many more pieces fly in a gale than in still air.")]
    [SerializeField, Min(1f)] private float galeMultiplier = 2.5f;
    [Tooltip("Tree wind (_SafeHavenTreeWind: Clear 0.8, Breezy 1.6, Storm 2.8) up to which the air counts as calm: pieces " +
             "fall nearly straight, and only Calm Share of Aloft are in the air.")]
    [SerializeField, Min(0f)] private float calmWind = 0.9f;
    [Tooltip("Tree wind of a full gale: Gale Multiplier times as many pieces, carried hardest.")]
    [SerializeField, Min(0.1f)] private float galeWind = 2.8f;
    [SerializeField, Range(0f, 1f)] private float calmShare = 0.5f;

    private readonly List<Vector3> crowns = new List<Vector3>();
    private readonly List<int> near = new List<int>();
    private readonly List<int> onScreen = new List<int>();
    private readonly Plane[] frustum = new Plane[6];
    private readonly List<(int index, Vector3 foot, float height)> painted = new List<(int, Vector3, float)>();
    private TerrainData paintedFrom;
    private int paintedCount = -1;
    private ParticleSystem system;
    private float nextCache;

    /// <summary>The scene's canopy, if it has one: RainDrips drips from the same crowns after rain.</summary>
    public static CanopyDebris Current { get; private set; }

    /// <summary>Crowns of the tall trees round what the camera looks at (70 % up each tree), refreshed every two seconds.</summary>
    public IReadOnlyList<Vector3> Crowns => crowns;

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Awake()
    {
        system = GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.RoundToInt(aloft * galeMultiplier) + MostPerFrame;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        system.Play();
    }

    private void Update()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 centre = RainEffect.GroundInView(camera);
        if (Time.time >= nextCache)
        {
            nextCache = Time.time + RecacheSeconds;
            Cache(centre);
        }

        float tree = Shader.GetGlobalFloat(TreeWindId);
        Blow(tree);
        float gale = Mathf.InverseLerp(calmWind, galeWind, tree);
        float day = AtmosphereConditions.Night > 0.5f ? 0.35f : 1f;
        float wanted = aloft * day * (tree <= calmWind ? calmShare : Mathf.Lerp(calmShare, galeMultiplier, gale));
        if (near.Count == 0 || wanted - system.particleCount <= 0)
        {
            return;
        }

        // Only crowns on screen shed, and only their share of the pieces: what is seen falls as thickly as before, and
        // nothing is simulated where no one looks.
        GeometryUtility.CalculateFrustumPlanes(camera, frustum);
        onScreen.Clear();
        foreach (int index in near)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, new Bounds(crowns[index], Vector3.one * 6f))) onScreen.Add(index);
        }

        int missing = Mathf.RoundToInt(wanted * onScreen.Count / near.Count) - system.particleCount;
        if (missing <= 0 || onScreen.Count == 0)
        {
            return;
        }

        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < Mathf.Min(missing, MostPerFrame); i++)
        {
            Vector3 crown = crowns[onScreen[Random.Range(0, onScreen.Count)]];
            Vector2 within = Random.insideUnitCircle * 2.2f;
            emit.position = crown + new Vector3(within.x, Random.Range(-2f, 2f), within.y);
            system.Emit(emit, 1);
        }
    }

    // The wind carries what falls, once there is wind: its direction and strength, read each frame from the shader
    // globals MossWind sets. Calm air lets pieces drop nearly straight, with a little turning.
    private void Blow(float tree)
    {
        Vector4 wind = Shader.GetGlobalVector(WindId);
        var direction = new Vector2(wind.x, wind.y);
        direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
        float strength = drift * Mathf.Max(0f, tree - calmWind) * 1.6f;
        ParticleSystem.NoiseModule turning = system.noise;
        turning.enabled = true;
        turning.strength = Mathf.Lerp(0.12f, 0.7f, Mathf.InverseLerp(calmWind, galeWind, tree));
        ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.x = new ParticleSystem.MinMaxCurve(direction.x * strength * 0.6f, direction.x * strength * 1.4f);
        force.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        force.z = new ParticleSystem.MinMaxCurve(direction.y * strength * 0.6f, direction.y * strength * 1.4f);
    }

    // Crowns of the tall trees near the centre (at 70 % of their height): painted terrain trees, by each prototype's
    // renderer height, and standing scene trees (a World Builder grove's).
    private void Cache(Vector3 centre)
    {
        crowns.Clear();
        near.Clear();
        float reach = radius * radius;
        foreach (ChoppableTree tree in ChoppableTree.Enabled)
        {
            Vector3 foot = tree.transform.position;
            Vector3 offset = foot - centre;
            if (!tree.IsStandIn && tree.IsStanding && tree.Height >= tallerThan && offset.x * offset.x + offset.z * offset.z <= reach)
            {
                near.Add(crowns.Count);
                crowns.Add(foot + Vector3.up * tree.Height * 0.7f);
            }
        }

        // Trees drawn without scripts (NodeStreaming): the live ones were counted above.
        foreach (NodeStreaming streaming in NodeStreaming.Active)
        {
            foreach (NodeStreaming.Entry entry in streaming.Entries)
            {
                Vector3 offset = entry.Foot - centre;
                if (entry.Live == null && entry.Height >= tallerThan && offset.x * offset.x + offset.z * offset.z <= reach)
                {
                    near.Add(crowns.Count);
                    crowns.Add(entry.Foot + Vector3.up * entry.Height * 0.7f);
                }
            }
        }

        Terrain terrain = Terrains.Under(centre);
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        if (data != paintedFrom || data.treeInstanceCount != paintedCount)
        {
            ReadPainted(terrain);
        }

        // The tall painted trees are read once per map; a felled one (scaled to nothing) is skipped here.
        foreach ((int index, Vector3 foot, float height) in painted)
        {
            Vector3 offset = foot - centre;
            if (offset.x * offset.x + offset.z * offset.z > reach || data.GetTreeInstance(index).heightScale <= 0f)
            {
                continue;
            }

            near.Add(crowns.Count);
            crowns.Add(foot + Vector3.up * height * 0.7f);
        }
    }

    private void ReadPainted(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        paintedFrom = data;
        paintedCount = data.treeInstanceCount;
        painted.Clear();
        TreePrototype[] prototypes = data.treePrototypes;
        var heights = new float[prototypes.Length];
        for (int p = 0; p < prototypes.Length; p++)
        {
            heights[p] = HeightOf(prototypes[p].prefab);
        }

        Vector3 origin = terrain.GetPosition();
        Vector3 size = data.size;
        TreeInstance[] trees = data.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            float scale = trees[i].heightScale > 0f ? trees[i].heightScale : 1f;
            float height = heights[trees[i].prototypeIndex] * scale;
            if (height >= tallerThan)
            {
                painted.Add((i, origin + Vector3.Scale(trees[i].position, size), height));
            }
        }
    }

    // Wind trees know their height; anything else (a rock, a log) never counts as a crown.
    private static float HeightOf(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<ChoppableTree>() == null)
        {
            return 0f;
        }

        Renderer drawn = prefab.GetComponentInChildren<Renderer>();
        Material material = drawn != null ? drawn.sharedMaterial : null;
        return material != null && material.HasProperty("_TreeHeight") ? material.GetFloat("_TreeHeight") : 0f;
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
    }
}
