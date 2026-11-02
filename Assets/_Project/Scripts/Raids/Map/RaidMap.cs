using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A raid's map: the terrain and everything placed on it (spawn groups, exits, story objects), kept under
/// <see cref="Content"/>. While the loading screen is up it makes this run's map, then puts the player at the start. A raid
/// with Layout Rules gets a new map from its seed (RaidLayoutPlanner, RaidMapGenerator); any other plays the hand-painted
/// one, turned by 0–3 quarter turns so one layout feels like four. A Terrain can't be rotated, so either way a copy of its
/// data is rewritten (the asset itself is never changed) and the navmesh is rebuilt. Runs before RaidGenerator, so spawn
/// groups fill points that are already in their place.
/// <para>
/// Where it belongs: one per raid scene. Reshape the map by repainting the definition's images and pressing Bake in this
/// component's inspector. Anything that should turn with the map must sit under Content; the player, cameras, lights and
/// managers stay outside it.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMap : MonoBehaviour, ISceneSetup
{
    [Tooltip("The images this map is baked from. Only the editor's Bake reads it.")]
    [SerializeField] private RaidMapDefinition definition;
    [SerializeField] private Terrain terrain;
    [Tooltip("Everything that stands on the map and turns with it: spawn groups, exits, story objects, quest zones.")]
    [SerializeField] private Transform content;
    [Tooltip("Where the player enters the raid. Set by Bake from the white marker; keep it under Content.")]
    [SerializeField] private Transform playerStart;
    [Tooltip("Rebuilt after turning, so creatures walk the turned map.")]
    [SerializeField] private NavMeshSurface navMesh;

    [Header("Turning")]
    [SerializeField] private bool rotate = true;
    [Tooltip("Always use this many quarter turns (0–3) instead of the seed's, to test one facing. -1 uses the seed.")]
    [SerializeField, Range(-1, 3)] private int fixedQuarterTurns = -1;

    private readonly List<Vector3> rumourSpots = new List<Vector3>();
    private readonly List<Mesh> pondMeshes = new List<Mesh>();
    private RaidLayoutRules generatedBy;
    private TerrainData turnedData;
    private Mesh generatedTrunks;
    private Texture2D generatedColour;

    public RaidMapDefinition Definition => definition;
    public Terrain Terrain => terrain;
    public Transform Content => content;
    public Transform PlayerStart => playerStart;
    public NavMeshSurface NavMesh => navMesh;

    /// <summary>How many quarter turns this raid's map was given.</summary>
    public int QuarterTurns { get; private set; }

    /// <summary>This run's generated map, or null when it plays the painted one.</summary>
    public RaidLayoutPlan Layout { get; private set; }

    /// <summary>Where this run's rumours are to be found on a generated map; empty on a painted one.</summary>
    public IReadOnlyList<Vector3> RumourSpots => rumourSpots;

    // Before RaidGenerator (0): spawn points must already be where the turned map put them.
    public int SetupOrder => -100;

    /// <summary>The middle of the map on the ground, the point everything turns around.</summary>
    public Vector3 Center
    {
        get
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return transform.position;
            }

            Vector3 size = terrain.terrainData.size;
            return terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        }
    }

    public IEnumerator Setup()
    {
        int seed = RaidDirector.Current != null ? RaidDirector.Current.Session.Seed
            : GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed
            : RaidSession.NewSeed();
        RaidDefinition raid = RaidDirector.Current != null ? RaidDirector.Current.Session.Definition : GameSession.ActiveRaid?.Definition;
        RaidLayoutRules rules = raid != null ? raid.LayoutRules : null;
        if (rules != null && definition != null && terrain != null && content != null)
        {
            List<NavMeshAgent> paused = PauseAgents();
            generatedBy = rules;
            yield return Generate(rules, seed);
            Dress(seed);
            ResumeAgents(paused);
            MovePlayerToStart();
            yield break;
        }

        QuarterTurns = !rotate ? 0 : fixedQuarterTurns >= 0 ? fixedQuarterTurns : MapRotation.Pick(seed);

        if (QuarterTurns != 0)
        {
            List<NavMeshAgent> agents = PauseAgents();
            TurnTerrain(QuarterTurns);
            yield return null;
            if (content != null)
            {
                content.RotateAround(Center, Vector3.up, MapRotation.Yaw(QuarterTurns));
            }

            if (terrain != null && terrain.TryGetComponent(out TerrainMoss moss))
            {
                moss.Rebuild();
            }

            yield return RebuildNavMesh();
            ResumeAgents(agents);
        }

        Dress(seed);
        MovePlayerToStart();
        Debug.Log($"Raid map turned {MapRotation.Yaw(QuarterTurns):0}° from seed {seed}.", this);
    }

    // Each attempt is a new map from the same seed, in the same shape and mood. The trails make every exit reachable by
    // design; the navmesh has the last word, because a raid without a way out is the one thing that must never happen.
    private IEnumerator Generate(RaidLayoutRules rules, int seed)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TerrainData source = terrain.terrainData;
        turnedData = Instantiate(source);
        turnedData.name = source.name + " (generated)";
        ApplyGroundLook(turnedData);
        terrain.terrainData = turnedData;
        if (terrain.TryGetComponent(out TerrainCollider ground))
        {
            ground.terrainData = turnedData;
        }

        for (int attempt = 0; attempt < rules.Attempts; attempt++)
        {
            Layout = RaidLayoutPlanner.Plan(rules, definition.Size, seed, attempt);
            yield return null;
            RaidMapGenerator.ShapeGround(definition, turnedData, Layout, rules, GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Tier : 0);
            terrain.Flush();
            yield return null;
            RebuildTrunks();
            rumourSpots.Clear();
            rumourSpots.AddRange(RaidMapGenerator.PlaceContent(this, Layout, rules));
            RaidMapGenerator.PlacePonds(this, Layout, rules, pondMeshes);
            if (terrain.TryGetComponent(out TerrainMoss moss))
            {
                moss.Rebuild();
            }

            Physics.SyncTransforms();
            yield return RebuildNavMesh();
            if (RaidMapGenerator.ExitsReachable(this, Layout))
            {
                Debug.Log($"Raid map generated from seed {seed}: {Layout.Shape.shape}, {Layout.Mood.name}, {Layout.Rooms.Count} clearings, " +
                          $"{Layout.Links.Count} trails, {Layout.Ponds.Count} ponds, {turnedData.treeInstanceCount} trees, attempt {attempt + 1}, {clock.ElapsedMilliseconds} ms.", this);
                yield break;
            }

            Debug.LogWarning($"Raid map from seed {seed}, attempt {attempt + 1}: no way to an exit on the navmesh.", this);
        }

        Debug.LogError($"Raid map from seed {seed}: no attempt gave a way to every exit. The player can still walk the trails.", this);
    }

    // The ground the definition asks for, so a designer's change to it needs no rebake. Painting relies on this order of layers.
    private void ApplyGroundLook(TerrainData data)
    {
        if (definition.ForestLayer != null && definition.PathLayer != null && definition.CliffLayer != null)
        {
            data.terrainLayers = new[] { definition.ForestLayer, definition.PathLayer, definition.CliffLayer };
            if (terrain.TryGetComponent(out TerrainMoss moss))
            {
                moss.MossLayer = definition.ForestLayer;
            }
        }

        if (definition.TerrainMaterial != null)
        {
            terrain.materialTemplate = definition.TerrainMaterial;
        }
    }

    // What this run's ground looks like, from what it was painted with. Done once, on the map that was kept: grass and plants,
    // the colour grass takes from the ground, and the surface shader's view of the new paint.
    private void Dress(int seed)
    {
        if (terrain == null || definition == null)
        {
            return;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        System.Func<Vector3, bool> blocked = TerrainGrowth.UnderTrees(terrain);
        System.Func<Vector3, float> wetness = null;
        if (generatedBy != null && Layout != null)
        {
            Vector3 origin = terrain.transform.position;
            RaidLayoutPlan plan = Layout;
            RaidLayoutRules rules = generatedBy;
            System.Func<Vector3, bool> underTrees = blocked;
            blocked = point => underTrees(point) || plan.InWater(new Vector2(point.x - origin.x, point.z - origin.z));
            wetness = point => plan.Wetness(new Vector2(point.x - origin.x, point.z - origin.z), rules.WetReach, rules.DampGround);
        }

        TerrainGrowth.Fill(terrain, definition.Growth, seed, blocked, definition.Plants, wetness);
        if (terrain.TryGetComponent(out GrassTerrainColour colour))
        {
            generatedColour = TerrainGrowth.BakeColour(terrain, definition.Growth);
            colour.ColourMap = generatedColour;
        }

        if (terrain.TryGetComponent(out TerrainSurface surface))
        {
            surface.Apply();
        }

        terrain.Flush();
        Debug.Log($"Raid map dressed in {clock.ElapsedMilliseconds} ms.", this);
    }

    private void RebuildTrunks()
    {
        Transform holder = content.Find(RaidMapSurface.TrunksName);
        if (holder == null || !holder.TryGetComponent(out MeshCollider trunks))
        {
            return;
        }

        Mesh mesh = RaidMapSurface.TrunkMesh(terrain, holder.position);
        trunks.sharedMesh = mesh;
        if (generatedTrunks != null)
        {
            Destroy(generatedTrunks);
        }

        generatedTrunks = mesh;
    }

    // Creatures already on the map are switched off while the ground moves under them, then dropped back on.
    private List<NavMeshAgent> PauseAgents()
    {
        var agents = new List<NavMeshAgent>();
        if (content != null)
        {
            foreach (NavMeshAgent agent in content.GetComponentsInChildren<NavMeshAgent>())
            {
                if (agent.enabled)
                {
                    agent.enabled = false;
                    agents.Add(agent);
                }
            }
        }

        return agents;
    }

    private void ResumeAgents(List<NavMeshAgent> agents)
    {
        foreach (NavMeshAgent agent in agents)
        {
            if (agent != null)
            {
                agent.enabled = true;
            }
        }

        // Creatures placed on the map may have started before the ground changed; their home moved with them.
        if (content != null)
        {
            foreach (MonsterAI monster in content.GetComponentsInChildren<MonsterAI>(true))
            {
                monster.ResetHome();
            }
        }
    }

    private IEnumerator RebuildNavMesh()
    {
        Physics.SyncTransforms();
        if (navMesh == null || navMesh.navMeshData == null)
        {
            yield break;
        }

        AsyncOperation building = navMesh.UpdateNavMesh(navMesh.navMeshData);
        while (building != null && !building.isDone)
        {
            yield return null;
        }
    }

    private void TurnTerrain(int quarterTurns)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        // A copy, so turning in play mode never rewrites the baked terrain asset.
        TerrainData source = terrain.terrainData;
        turnedData = Instantiate(source);
        turnedData.name = source.name + " (turned)";

        int heights = turnedData.heightmapResolution;
        turnedData.SetHeights(0, 0, MapRotation.RotateGrid(turnedData.GetHeights(0, 0, heights, heights), quarterTurns));
        int paint = turnedData.alphamapResolution;
        if (turnedData.alphamapLayers > 0)
        {
            turnedData.SetAlphamaps(0, 0, MapRotation.RotateLayers(turnedData.GetAlphamaps(0, 0, paint, paint), quarterTurns));
        }

        TreeInstance[] trees = turnedData.treeInstances;
        MapRotation.RotateTrees(trees, quarterTurns);
        turnedData.SetTreeInstances(trees, true);

        terrain.terrainData = turnedData;
        if (terrain.TryGetComponent(out TerrainCollider ground))
        {
            ground.terrainData = turnedData;
        }

        terrain.Flush();
    }

    private void MovePlayerToStart()
    {
        if (playerStart == null || Player.Current == null)
        {
            return;
        }

        Transform player = Player.Current.transform;
        Vector3 from = player.position;
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (wasEnabled)
        {
            body.enabled = false;
        }

        Vector3 forward = Vector3.ProjectOnPlane(playerStart.forward, Vector3.up);
        player.SetPositionAndRotation(playerStart.position, forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : player.rotation);
        if (wasEnabled)
        {
            body.enabled = true;
        }

        // The follow camera would otherwise sweep across the map to catch up once the loading screen lifts.
        Vector3 moved = player.position - from;
        foreach (CinemachineCamera follow in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            if (follow.Follow != null && follow.Follow.IsChildOf(player))
            {
                follow.OnTargetObjectWarped(follow.Follow, moved);
            }
        }
    }

    private void OnDestroy()
    {
        if (turnedData != null)
        {
            Destroy(turnedData);
        }

        if (generatedTrunks != null)
        {
            Destroy(generatedTrunks);
        }

        if (generatedColour != null)
        {
            Destroy(generatedColour);
        }

        foreach (Mesh mesh in pondMeshes)
        {
            Destroy(mesh);
        }
    }

#if UNITY_EDITOR
    /// <summary>For the editor's Bake: points the map at its terrain, content and start.</summary>
    public void Configure(Terrain bakedTerrain, Transform bakedContent, Transform start, NavMeshSurface surface)
    {
        terrain = bakedTerrain;
        content = bakedContent;
        playerStart = start;
        navMesh = surface;
    }

    private void OnDrawGizmosSelected()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        Vector3 size = terrain.terrainData.size;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireCube(Center + Vector3.up * size.y * 0.5f, size);
        Gizmos.DrawLine(Center, Center + Vector3.forward * size.z * 0.5f);
        UnityEditor.Handles.Label(Center + Vector3.forward * size.z * 0.5f, "North (unturned)");
    }
#endif
}
