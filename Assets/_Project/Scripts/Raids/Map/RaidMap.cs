using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A raid's hand-made map: the terrain baked from a RaidMapDefinition's images and everything placed on it (spawn
/// groups, exits, story objects), kept under <see cref="Content"/>. While the loading screen is up it turns the whole map
/// by 0–3 quarter turns from the raid's seed, so one layout feels like four, then puts the player at the start.
/// A Terrain can't be rotated, so a copy of its data is turned in its arrays (the asset itself is never changed), the
/// content is turned around the map's centre to match, and the navmesh is rebuilt. Runs before RaidGenerator, so spawn
/// groups fill their already-turned points.
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

    private TerrainData turnedData;

    public RaidMapDefinition Definition => definition;
    public Terrain Terrain => terrain;
    public Transform Content => content;
    public Transform PlayerStart => playerStart;
    public NavMeshSurface NavMesh => navMesh;

    /// <summary>How many quarter turns this raid's map was given.</summary>
    public int QuarterTurns { get; private set; }

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
        QuarterTurns = !rotate ? 0 : fixedQuarterTurns >= 0 ? fixedQuarterTurns : MapRotation.Pick(seed);

        if (QuarterTurns != 0)
        {
            // Creatures already on the map are switched off while the ground moves under them, then dropped back on.
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

            Physics.SyncTransforms();
            if (navMesh != null && navMesh.navMeshData != null)
            {
                AsyncOperation building = navMesh.UpdateNavMesh(navMesh.navMeshData);
                while (building != null && !building.isDone)
                {
                    yield return null;
                }
            }

            foreach (NavMeshAgent agent in agents)
            {
                if (agent != null)
                {
                    agent.enabled = true;
                }
            }

            // Creatures placed on the map may have started before the turn; their home turned with them.
            if (content != null)
            {
                foreach (MonsterAI monster in content.GetComponentsInChildren<MonsterAI>(true))
                {
                    monster.ResetHome();
                }
            }
        }

        MovePlayerToStart();
        Debug.Log($"Raid map turned {MapRotation.Yaw(QuarterTurns):0}° from seed {seed}.", this);
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
