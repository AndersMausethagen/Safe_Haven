using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// The drifting mist answers what moves through it (the user, 2026-11-22, after an Ocarina of Time style interactive ground fog: "remember
/// this is for the mist effect, not the thick plain fog... it reacts to all monsters, npcs and even trees when we fell them"). A
/// top-down wake field follows the player (MistWakeSettings › Size): every body in it, the player, friends, creatures, residents,
/// companions and horses, parts the mist and pushes it aside, harder the faster it goes; a felled tree sweeps it as it falls and blows it
/// out where it lands (Stamp, from FelledTree). Each frame the field rolls on, softens and fills back in (Shaders/MistWakeSim.shader), and
/// the atmosphere's march reads it for Terrain Mist and Mist Volumes only: the base fog is untouched. The field is Stable Fluids (the
/// technique of the user's reference, "Stable Fluids + Stir"): a push becomes swirls that curl round a body and turn on a while. The mist
/// also creeps through it (the user: "it should slowly creep along the terrain, interacting with anything in its way"): the air moves on
/// at the nearest Terrain Mist's creep, and what stands in the square (trees, rocks, logs, walls: GatherObstacles, every Obstacle Gap
/// seconds) is solid, so the creep parts round it and leaves a gap in its lee. Made by the first mist that wakes in play (Ensure); nothing
/// is stirred where there is no mist.
/// </summary>
[DisallowMultipleComponent]
public sealed class MistWakes : MonoBehaviour
{
    private const int MaxStamps = 64;
    private const int MaxObstacles = 96;
    private const int MaxGusts = 24;

    private static readonly int WakeTex = Shader.PropertyToID("_MistWakeTex");
    private static readonly int WakeRectId = Shader.PropertyToID("_MistWakeRect");
    private static readonly int WakeParams = Shader.PropertyToID("_MistWakeParams");
    private static readonly int VelocityId = Shader.PropertyToID("_MistWakeVelocity");
    private static readonly int PressureId = Shader.PropertyToID("_MistWakePressure");
    private static readonly int DivergenceId = Shader.PropertyToID("_MistWakeDivergence");
    private static readonly int StateId = Shader.PropertyToID("_MistWakeState");
    private static readonly int TexelId = Shader.PropertyToID("_WakeTexel");
    private static readonly int ShiftId = Shader.PropertyToID("_MistWakeShift");
    private static readonly int SimId = Shader.PropertyToID("_WakeSim");
    private static readonly int RectId = Shader.PropertyToID("_WakeRect");
    private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
    private static readonly int StampsId = Shader.PropertyToID("_Stamps");
    private static readonly int StampVelocitiesId = Shader.PropertyToID("_StampVelocities");
    private static readonly int SolidId = Shader.PropertyToID("_MistWakeSolid");
    private static readonly int CreepId = Shader.PropertyToID("_WakeCreep");
    private static readonly int ObstacleCountId = Shader.PropertyToID("_ObstacleCount");
    private static readonly int ObstaclesId = Shader.PropertyToID("_Obstacles");
    private static readonly int ObstacleShapesId = Shader.PropertyToID("_ObstacleShapes");

    private static readonly Vector4[] stamps = new Vector4[MaxStamps];
    private static readonly Vector4[] stampVelocities = new Vector4[MaxStamps];
    private static int stampCount;
    private static readonly float[] TrunkPoints = { 0.45f, 0.7f, 0.95f };
    private static readonly List<Terrain> Terrains = new List<Terrain>();
    private static readonly System.Comparison<Obstacle> NearestFirst = (a, b) => a.Distance.CompareTo(b.Distance);

    // One thing in the mist's way: where it stands and how far it reaches (x, z, half widths), its turn and roundness.
    private struct Obstacle
    {
        public Vector4 Place;
        public Vector4 Shape;
        public float Distance;
    }

    // A blow's gust: the mist thrown the way it goes for a moment (Strike).
    private struct Gust
    {
        public Vector3 At;
        public Vector3 Velocity;
        public float Radius;
        public float Strength;
        public float Until;
    }

    private readonly List<Gust> gusts = new List<Gust>(MaxGusts);
    // When each striker last threw a gust: a charge resolves its blow every frame until it lands, and stirs once.
    private readonly Dictionary<Object, float> struckAt = new Dictionary<Object, float>();

    // A felled tree sweeping down: told each physics step (FelledTree.Fall), stamped once a frame here so its push doesn't follow the
    // frame rate.
    private struct Falling
    {
        public Transform Tree;
        public Rigidbody Body;
        public float Height;
        public float Size;
        public float Until;
    }

    private readonly List<Falling> falling = new List<Falling>();
    private Collider[] overlaps = new Collider[512];
    private bool idle;
    private readonly Vector4[] obstacles = new Vector4[MaxObstacles];
    private readonly Vector4[] obstacleShapes = new Vector4[MaxObstacles];
    private readonly List<Obstacle> found = new List<Obstacle>();
    // Whether a collider is part of something that moves by itself (a creature, a player, a horse: stamped as bodies instead); found once.
    private readonly Dictionary<Collider, bool> moving = new Dictionary<Collider, bool>();
    private int obstacleCount;
    private float nextGather;
    private Vector3 gatheredAt;

    // Keyed by the body itself: rebuilt each frame from those seen, so a body gone is gone from it.
    private readonly Dictionary<Transform, Vector3> lastSeen = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> seenNow = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<GameObject, float> radii = new Dictionary<GameObject, float>();
    // The mist's state (parted, carried) and the fluid under it: velocity, pressure and divergence, each pair swapped every pass.
    private RenderTexture current;
    private RenderTexture next;
    private RenderTexture velocity;
    private RenderTexture velocityNext;
    private RenderTexture pressure;
    private RenderTexture pressureNext;
    private RenderTexture divergence;
    private RenderTexture solid;
    private Material material;
    // The whole step in one buffer, sent once a frame: thirty separate blits each went to the GPU on their own and cost ~3.5 ms of its
    // frame (measured 2026-11-22, almost all waiting between them).
    private CommandBuffer commands;
    // Named for the Profiler (and Recorder.Get): the step's CPU (Step) and its GPU work (the buffer's sample).
    private const string GpuSample = "Mist Wakes";
    private static readonly ProfilerMarker Step = new ProfilerMarker("MistWakes.Step");
    private Vector2 min;
    private bool started;

    /// <summary>What the square follows instead of the player (a cinematic's subject, a test); null: the player, else the camera.</summary>
    public Transform Follow { get; set; }

    /// <summary>The wake field in the loaded scene, or null (no mist, or not playing).</summary>
    public static MistWakes Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        stampCount = 0;
        Terrains.Clear();
        Shader.SetGlobalVector(WakeParams, Vector4.zero);
    }

    /// <summary>Makes the wake field when a mist wakes in play (TerrainMist, MistVolume).</summary>
    public static void Ensure()
    {
        if (!Application.isPlaying || Current != null || MistWakeSettings.Current.Simulation == null)
        {
            return;
        }

        new GameObject("Mist Wakes").AddComponent<MistWakes>();
    }

    /// <summary>
    /// Parts the mist round <paramref name="at"/> this frame: <paramref name="radius"/> metres, <paramref name="strength"/> 0-1, pushed along
    /// <paramref name="velocity"/>. Anything moving through the mist that the field doesn't already watch calls this (a falling tree).
    /// </summary>
    public static void Stamp(Vector3 at, float radius, Vector3 velocity, float strength)
    {
        MistWakes wakes = Current;
        if (wakes == null || stampCount >= Mathf.Min(MaxStamps, MistWakeSettings.Current.MostStamps) || strength <= 0f)
        {
            return;
        }

        float size = MistWakeSettings.Current.Size;
        if (at.x < wakes.min.x - radius || at.z < wakes.min.y - radius || at.x > wakes.min.x + size + radius || at.z > wakes.min.y + size + radius)
        {
            return;
        }

        stamps[stampCount] = new Vector4(at.x, at.z, radius, Mathf.Clamp01(strength));
        stampVelocities[stampCount] = new Vector4(velocity.x, velocity.z, 0f, 0f);
        stampCount++;
    }

    /// <summary>
    /// A blow through the mist (the user: "make sure the new mist reacts to weapon swings, creating turbulence in the direction of the
    /// attack"): for Strike Seconds the mist is thrown the way the blow goes across its arc, from where it is struck out to its reach; a
    /// spin (an arc of 300° or more) throws it outward and round. AttackResolver.Execute calls it for every creature's, companion's and
    /// skill's blow, PlayerCombat for the player's swing (whose blade also stirs the mist along its path: Blade).
    /// </summary>
    public static void Strike(Vector3 origin, Vector3 direction, float range, float arc, Object striker = null)
    {
        MistWakes wakes = Current;
        if (wakes == null || wakes.idle || range <= 0f)
        {
            return;
        }

        MistWakeSettings settings = MistWakeSettings.Current;
        if (striker != null)
        {
            if (wakes.struckAt.TryGetValue(striker, out float last) && Time.time < last + settings.StrikeSeconds)
            {
                return;
            }

            if (wakes.struckAt.Count > 256)
            {
                wakes.struckAt.Clear();
            }

            wakes.struckAt[striker] = Time.time;
        }

        direction.y = 0f;
        direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
        float reach = Mathf.Clamp(range, 0.5f, settings.StrikeMostReach);
        float radius = Mathf.Max(0.5f, reach * settings.StrikeWidth);
        if (arc >= 300f)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 outward = Quaternion.Euler(0f, i * 60f, 0f) * direction;
                Vector3 round = Vector3.Cross(Vector3.up, outward);
                wakes.AddGust(origin + outward * (reach * 0.7f), (outward + round * 0.6f) * settings.StrikeSpeed, radius, settings);
            }

            return;
        }

        float spread = Mathf.Clamp(arc, 0f, 180f) * 0.3f;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 along = Quaternion.Euler(0f, spread * i, 0f) * direction;
            wakes.AddGust(origin + along * (reach * 0.65f), along * settings.StrikeSpeed, radius, settings);
        }
    }

    /// <summary>A weapon's blade sweeping low through the mist this frame (WeaponTrail, through a swing): stirred along its path.</summary>
    public static void Blade(Vector3 tip, Vector3 velocity)
    {
        if (Current == null)
        {
            return;
        }

        MistWakeSettings settings = MistWakeSettings.Current;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (speed > 0.5f)
        {
            Stamp(tip, settings.BladeRadius, velocity, settings.BladeStrength * Mathf.Clamp01(speed / settings.FullSpeed));
        }
    }

    private void AddGust(Vector3 at, Vector3 velocity, float radius, MistWakeSettings settings)
    {
        if (gusts.Count >= MaxGusts)
        {
            gusts.RemoveAt(0);
        }

        gusts.Add(new Gust { At = at, Velocity = velocity, Radius = radius, Strength = settings.StrikeStrength, Until = Time.time + settings.StrikeSeconds });
    }

    // Each live gust stamped again this frame, fading as it ends.
    private void StampGusts(MistWakeSettings settings)
    {
        for (int i = gusts.Count - 1; i >= 0; i--)
        {
            Gust gust = gusts[i];
            float left = gust.Until - Time.time;
            if (left <= 0f)
            {
                gusts.RemoveAt(i);
                continue;
            }

            float fade = Mathf.Clamp01(left / Mathf.Max(0.01f, settings.StrikeSeconds));
            Stamp(gust.At, gust.Radius, gust.Velocity * fade, gust.Strength * fade);
        }
    }

    /// <summary>A felled tree sweeping down (FelledTree, while it falls): its trunk parts the mist where it comes low, faster as it rushes.</summary>
    public static void TreeFalling(Transform tree, float height, Rigidbody body, float size)
    {
        MistWakes wakes = Current;
        if (wakes == null || tree == null || body == null)
        {
            return;
        }

        var news = new Falling { Tree = tree, Body = body, Height = height, Size = size, Until = Time.time + 0.15f };
        for (int i = 0; i < wakes.falling.Count; i++)
        {
            if (wakes.falling[i].Tree == tree)
            {
                wakes.falling[i] = news;
                return;
            }
        }

        wakes.falling.Add(news);
    }

    // Each tree still falling: its trunk parts the mist where it comes low, faster as it rushes.
    private void StampFalling(MistWakeSettings settings)
    {
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            Falling tree = falling[i];
            if (tree.Tree == null || tree.Body == null || Time.time > tree.Until)
            {
                falling.RemoveAt(i);
                continue;
            }

            float ground = tree.Tree.position.y;
            foreach (float along in TrunkPoints)
            {
                Vector3 point = tree.Tree.position + tree.Tree.up * (tree.Height * along);
                if (point.y - ground > settings.Reach * 1.5f)
                {
                    continue;
                }

                Vector3 velocity = tree.Body.GetPointVelocity(point);
                Stamp(point, settings.TreeRadius * Mathf.Clamp(tree.Size, 0.4f, 1.6f), velocity, settings.PresenceAt(velocity.magnitude));
            }
        }
    }

    /// <summary>A felled tree landing: the mist is blown out along the whole trunk.</summary>
    public static void TreeLanded(Vector3 foot, Vector3 along, float height, float size)
    {
        if (Current == null)
        {
            return;
        }

        MistWakeSettings settings = MistWakeSettings.Current;
        float radius = settings.TreeRadius * settings.TreeLandingSpread * Mathf.Clamp(size, 0.4f, 1.6f);
        for (int i = 1; i <= 5; i++)
        {
            Stamp(foot + along * (height * i / 5f), radius, Vector3.zero, 1f);
        }
    }

    private void Awake()
    {
        Current = this;
        // Kept across loads (a raid's mists may wake before the last scene's field is gone); each new scene starts it clean.
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        MistWakeSettings settings = MistWakeSettings.Current;
        material = new Material(settings.Simulation) { hideFlags = HideFlags.HideAndDontSave };
        commands = new CommandBuffer { name = "Mist Wakes" };
        int resolution = settings.Resolution;
        velocity = MakeTexture(resolution, RenderTextureFormat.RGHalf, "Mist Wake Velocity");
        velocityNext = MakeTexture(resolution, RenderTextureFormat.RGHalf, "Mist Wake Velocity");
        pressure = MakeTexture(resolution, RenderTextureFormat.RHalf, "Mist Wake Pressure");
        pressureNext = MakeTexture(resolution, RenderTextureFormat.RHalf, "Mist Wake Pressure");
        divergence = MakeTexture(resolution, RenderTextureFormat.RHalf, "Mist Wake Divergence");
        solid = MakeTexture(resolution, RenderTextureFormat.RHalf, "Mist Wake Solid");
        current = MakeTexture(resolution, RenderTextureFormat.ARGBHalf, "Mist Wake State");
        next = MakeTexture(resolution, RenderTextureFormat.ARGBHalf, "Mist Wake State");
    }

    private static RenderTexture MakeTexture(int resolution, RenderTextureFormat format, string name)
    {
        var texture = new RenderTexture(resolution, resolution, 0, format)
        {
            name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };
        texture.Create();
        Clear(texture);
        return texture;
    }

    private static void Clear(RenderTexture texture)
    {
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = active;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            Restart();
            moving.Clear();
        }
    }

    // Everything stirred forgotten: the fields cleared, the square found afresh, obstacles looked for at once.
    private void Restart()
    {
        started = false;
        nextGather = 0f;
        gusts.Clear();
        falling.Clear();
        struckAt.Clear();
        stampCount = 0;
        foreach (RenderTexture texture in new[] { velocity, velocityNext, pressure, pressureNext, divergence, solid, current, next })
        {
            if (texture != null)
            {
                Clear(texture);
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Shader.SetGlobalVector(WakeParams, Vector4.zero);
        if (Current == this)
        {
            Current = null;
        }

        foreach (RenderTexture texture in new[] { velocity, velocityNext, pressure, pressureNext, divergence, solid, current, next })
        {
            if (texture != null)
            {
                texture.Release();
                Discard(texture);
            }
        }

        if (material != null)
        {
            Discard(material);
        }

        if (commands != null)
        {
            commands.Release();
        }
    }

    private static void Discard(Object thing)
    {
        if (Application.isPlaying)
        {
            Destroy(thing);
        }
        else
        {
            DestroyImmediate(thing);
        }
    }

    private void LateUpdate()
    {
        using (Step.Auto())
        {
            Simulate();
        }
    }

    private void Simulate()
    {
        MistWakeSettings settings = MistWakeSettings.Current;
        // Nothing to stir where no mist shows (a clear Haven day, a menu): the field rests, and starts clean when mist comes back.
        if (!AnyMistShowing())
        {
            if (!idle)
            {
                idle = true;
                Shader.SetGlobalVector(WakeParams, Vector4.zero);
            }

            stampCount = 0;
            return;
        }

        if (idle)
        {
            idle = false;
            Restart();
        }

        // Paused (the game stops time): the mist stands as it was.
        if (Time.timeScale <= 0f)
        {
            stampCount = 0;
            return;
        }

        float size = settings.Size;
        int resolution = current.width;
        float texel = size / resolution;
        Vector3 focus = Follow != null ? Follow.position : Player.Current != null ? Player.Current.transform.position
            : Camera.main != null ? Camera.main.transform.position : transform.position;
        // Snapped to a texel, so the stirred mist doesn't swim as the square follows.
        var square = new Vector2(Mathf.Round((focus.x - size * 0.5f) / texel) * texel, Mathf.Round((focus.z - size * 0.5f) / texel) * texel);
        Vector2 shift = started ? (square - min) / size : Vector2.zero;
        min = square;
        started = true;

        StampBodies(settings);
        if (Time.time >= nextGather || (focus - gatheredAt).sqrMagnitude > settings.ObstacleRegatherDistance * settings.ObstacleRegatherDistance)
        {
            nextGather = Time.time + settings.ObstacleGap;
            gatheredAt = focus;
            GatherObstacles(focus, settings);
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        material.SetVector(TexelId, new Vector4(1f / resolution, 1f / resolution, texel, 0f));
        material.SetVector(SimId, new Vector4(dt, settings.FillRate, settings.SettleRate, settings.VelocityDamping));
        material.SetVector(RectId, new Vector4(min.x, min.y, size, settings.Stir));
        material.SetFloat(StampCountId, stampCount);
        material.SetVectorArray(StampsId, stamps);
        material.SetVectorArray(StampVelocitiesId, stampVelocities);
        Vector2 creep = CreepAt(focus);
        material.SetVector(CreepId, new Vector4(creep.x, creep.y, settings.ObstacleParting, 0f));
        material.SetFloat(ObstacleCountId, obstacleCount);
        material.SetVectorArray(ObstaclesId, obstacles);
        material.SetVectorArray(ObstacleShapesId, obstacleShapes);

        // Stable Fluids: carry and stir the air, then take out its divergence so it swirls; then the mist, carried by it. What each pass
        // reads is set as a global between passes in the buffer.
        commands.Clear();
        commands.BeginSample(GpuSample);
        Vector4 shifted = new Vector4(shift.x, shift.y, 0f, 0f);
        Blit(solid, 5, Vector4.zero);
        commands.SetGlobalTexture(SolidId, solid);
        commands.SetGlobalTexture(VelocityId, velocity);
        Blit(velocityNext, 0, shifted);
        (velocity, velocityNext) = (velocityNext, velocity);
        commands.SetGlobalTexture(VelocityId, velocity);
        Blit(divergence, 1, Vector4.zero);
        commands.SetGlobalTexture(DivergenceId, divergence);
        // Started from last frame's pressure (shifted with the square on the first step): the creep round an obstacle needs a pressure
        // wider than a frame's steps reach, and it builds up over frames.
        for (int i = 0; i < settings.PressureSteps; i++)
        {
            commands.SetGlobalTexture(PressureId, pressure);
            Blit(pressureNext, 2, i == 0 ? shifted : Vector4.zero);
            (pressure, pressureNext) = (pressureNext, pressure);
        }

        commands.SetGlobalTexture(PressureId, pressure);
        Blit(velocityNext, 3, Vector4.zero);
        (velocity, velocityNext) = (velocityNext, velocity);
        commands.SetGlobalTexture(VelocityId, velocity);
        commands.SetGlobalTexture(StateId, current);
        Blit(next, 4, shifted);
        (current, next) = (next, current);
        commands.EndSample(GpuSample);
        Graphics.ExecuteCommandBuffer(commands);
        stampCount = 0;

        Shader.SetGlobalTexture(WakeTex, current);
        Shader.SetGlobalVector(WakeRectId, new Vector4(min.x, min.y, 1f / size, 0f));
        Shader.SetGlobalVector(WakeParams, new Vector4(settings.Parting, settings.Swirl, settings.Reach, 1f));
    }

    // One pass into a field, reading what the buffer last set, with the square's shift where it reads its own last frame.
    private void Blit(RenderTexture into, int pass, Vector4 shift)
    {
        commands.SetGlobalVector(ShiftId, shift);
        commands.Blit(Texture2D.blackTexture, into, material, pass);
    }

    private static bool AnyMistShowing()
    {
        IReadOnlyList<TerrainMist> mists = TerrainMist.Active;
        for (int i = 0; i < mists.Count; i++)
        {
            if (mists[i] != null && (mists[i].EffectiveDensity > 0f || mists[i].RegionDensity > 0f))
            {
                return true;
            }
        }

        IReadOnlyList<MistVolume> volumes = MistVolume.Active;
        for (int i = 0; i < volumes.Count; i++)
        {
            if (volumes[i] != null && volumes[i].isActiveAndEnabled && volumes[i].EffectiveDensity > 0f)
            {
                return true;
            }
        }

        return false;
    }

    // The air's creep here: the nearest Terrain Mist's drift times its Creep, sped or slowed with the weather's wind as its pattern is.
    private static Vector2 CreepAt(Vector3 focus)
    {
        TerrainMist nearest = null;
        float best = float.MaxValue;
        IReadOnlyList<TerrainMist> mists = TerrainMist.Active;
        for (int i = 0; i < mists.Count; i++)
        {
            TerrainMist mist = mists[i];
            if (mist != null && mist.Density > 0f)
            {
                float distance = mist.DistanceSquared(focus);
                if (distance < best)
                {
                    best = distance;
                    nearest = mist;
                }
            }
        }

        if (nearest == null)
        {
            return Vector2.zero;
        }

        float weather = Mathf.Max(0f, 1f + (AtmosphereConditions.WeatherWind - 1f) * nearest.WeatherWindInfluence);
        return nearest.Wind * (nearest.Creep * weather);
    }

    // What stands in the square low enough to be in the mist's way: colliders that don't move by themselves (rocks, walls, logs, placed
    // trees, buildings) and the terrain's standing trees, nearest first.
    private void GatherObstacles(Vector3 focus, MistWakeSettings settings)
    {
        found.Clear();
        float half = settings.Size * 0.5f;
        Terrain.GetActiveTerrains(Terrains);
        // Round the ground under the focus (it is the camera, high above, when there is no player).
        var centre = new Vector3(focus.x, GroundAt(focus, focus.y), focus.z);
        var extents = new Vector3(half, settings.ObstacleSearchHeight, half);
        int hits = Physics.OverlapBoxNonAlloc(centre, extents, overlaps, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // A full buffer drops colliders in no order (a town's walls popping in and out): look again with room for all.
        while (hits == overlaps.Length && overlaps.Length < 8192)
        {
            overlaps = new Collider[overlaps.Length * 2];
            hits = Physics.OverlapBoxNonAlloc(centre, extents, overlaps, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        for (int i = 0; i < hits; i++)
        {
            Collider collider = overlaps[i];
            if (collider != null && StandsStill(collider))
            {
                AddCollider(collider, focus, settings);
            }
        }

        IReadOnlyList<TerrainTreeFelling> terrains = TerrainTreeFelling.Active;
        for (int t = 0; t < terrains.Count; t++)
        {
            TerrainTreeFelling trees = terrains[t];
            if (trees == null)
            {
                continue;
            }

            foreach (TerrainTreeFelling.Standing tree in trees.StandingNear(focus, half * 1.42f))
            {
                float radius = Mathf.Max(settings.SmallestObstacle, tree.Radius);
                Add(new Vector2(tree.Foot.x, tree.Foot.z), new Vector2(radius, radius), Vector2.right, 2f, focus);
            }
        }

        found.Sort(NearestFirst);
        obstacleCount = Mathf.Min(found.Count, MaxObstacles);
        for (int i = 0; i < obstacleCount; i++)
        {
            obstacles[i] = found[i].Place;
            obstacleShapes[i] = found[i].Shape;
        }
    }

    private bool StandsStill(Collider collider)
    {
        if (collider is TerrainCollider || collider is CharacterController)
        {
            return false;
        }

        // A log rolling or a tree falling is stamped as it moves; it stands in the way once it lies still.
        Rigidbody body = collider.attachedRigidbody;
        if (body != null && !body.isKinematic && !body.IsSleeping())
        {
            return false;
        }

        if (!moving.TryGetValue(collider, out bool movesByItself))
        {
            if (moving.Count > 4096)
            {
                moving.Clear();
            }

            movesByItself = collider.GetComponentInParent<NavMeshAgent>() != null || collider.GetComponentInParent<Player>() != null
                || collider.GetComponentInParent<NetworkPlayerAvatar>() != null || collider.GetComponentInParent<Horse>() != null;
            moving[collider] = movesByItself;
        }

        return !movesByItself;
    }

    private void AddCollider(Collider collider, Vector3 focus, MistWakeSettings settings)
    {
        Bounds bounds = collider.bounds;
        float ground = GroundAt(bounds.center, bounds.min.y);
        // Only what rises out of the ground and reaches down into the mist: not a floor or a path, not a branch overhead.
        if (bounds.max.y < ground + settings.LowestObstacle || bounds.min.y > ground + settings.Reach)
        {
            return;
        }

        Transform place = collider.transform;
        Vector3 scale = place.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float smallest = settings.SmallestObstacle;
        switch (collider)
        {
            case SphereCollider sphere:
            {
                Vector3 centre = place.TransformPoint(sphere.center);
                float radius = Mathf.Max(smallest, sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)));
                Add(new Vector2(centre.x, centre.z), new Vector2(radius, radius), Vector2.right, 2f, focus);
                return;
            }
            case CapsuleCollider capsule:
            {
                Vector3 centre = place.TransformPoint(capsule.center);
                Vector3 axis = capsule.direction == 0 ? place.right : capsule.direction == 1 ? place.up : place.forward;
                float along = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
                float across = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z) : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z)
                    : Mathf.Max(scale.x, scale.y);
                float radius = Mathf.Max(smallest, capsule.radius * across);
                var flat = new Vector2(axis.x, axis.z);
                if (flat.magnitude < 0.7f)
                {
                    // Standing up: a trunk, a post.
                    Add(new Vector2(centre.x, centre.z), new Vector2(radius, radius), Vector2.right, 2f, focus);
                }
                else
                {
                    // Lying down: a log.
                    float length = Mathf.Max(radius, capsule.height * along * 0.5f * flat.magnitude);
                    Add(new Vector2(centre.x, centre.z), new Vector2(length, radius), flat.normalized, 2.5f, focus);
                }

                return;
            }
            case BoxCollider box when Vector3.Dot(place.up, Vector3.up) > 0.85f:
            {
                // Upright: a wall, a crate, a building's footprint, turned as it stands.
                Vector3 centre = place.TransformPoint(box.center);
                Vector3 size = Vector3.Scale(box.size, scale);
                var turn = new Vector2(place.right.x, place.right.z);
                Add(new Vector2(centre.x, centre.z), new Vector2(Mathf.Max(smallest, size.x * 0.5f), Mathf.Max(smallest, size.z * 0.5f)),
                    turn.sqrMagnitude > 1e-4f ? turn.normalized : Vector2.right, 6f, focus);
                return;
            }
            case MeshCollider mesh when mesh.sharedMesh != null && Vector3.Dot(place.up, Vector3.up) > 0.85f:
            {
                // An upright rock or prop: its mesh's own bounds, turned as it stands (an elongated rock turned 45° isn't its world box).
                Bounds own = mesh.sharedMesh.bounds;
                Vector3 centre = place.TransformPoint(own.center);
                Vector3 size = Vector3.Scale(own.size, scale);
                if (size.x > settings.Size * 0.5f && size.z > settings.Size * 0.5f)
                {
                    return;
                }

                var turn = new Vector2(place.right.x, place.right.z);
                Add(new Vector2(centre.x, centre.z), new Vector2(Mathf.Max(smallest, size.x * 0.42f), Mathf.Max(smallest, size.z * 0.42f)),
                    turn.sqrMagnitude > 1e-4f ? turn.normalized : Vector2.right, 2.5f, focus);
                return;
            }
            default:
            {
                // A rock's mesh, a tilted box: an ellipse a little inside its bounds. Something wider than the square itself (a cliff's
                // whole mesh, a barrier) isn't a shape this can tell.
                Vector3 extents = bounds.extents;
                if (extents.x > settings.Size * 0.25f && extents.z > settings.Size * 0.25f)
                {
                    return;
                }

                Add(new Vector2(bounds.center.x, bounds.center.z),
                    new Vector2(Mathf.Max(smallest, extents.x * 0.85f), Mathf.Max(smallest, extents.z * 0.85f)), Vector2.right, 2f, focus);
                return;
            }
        }
    }

    private void Add(Vector2 centre, Vector2 half, Vector2 turn, float roundness, Vector3 focus)
    {
        found.Add(new Obstacle
        {
            Place = new Vector4(centre.x, centre.y, half.x, half.y),
            Shape = new Vector4(turn.x, turn.y, roundness, 0f),
            Distance = Mathf.Max(0f, Vector2.Distance(centre, new Vector2(focus.x, focus.z)) - Mathf.Max(half.x, half.y)),
        });
    }

    // The ground's height under a point: the terrain there, else the bottom of what stands on it.
    private static float GroundAt(Vector3 point, float otherwise)
    {
        foreach (Terrain terrain in Terrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x >= origin.x && point.z >= origin.z && point.x <= origin.x + size.x && point.z <= origin.z + size.z)
            {
                return terrain.SampleHeight(point) + origin.y;
            }
        }

        return otherwise;
    }

    // Everyone the field watches, nearest first being the player: friends, creatures, residents, companions, horses.
    private void StampBodies(MistWakeSettings settings)
    {
        seenNow.Clear();
        if (Player.Current != null)
        {
            Body(Player.Current.transform, settings.PlayerRadius, settings, settings.PlayerStrength);
        }

        // Blows and falling trees before the crowd: a frame holds only so many stamps.
        StampGusts(settings);
        StampFalling(settings);
        IReadOnlyList<NetworkPlayerAvatar> friends = NetworkPlayerAvatar.All;
        for (int i = 0; i < friends.Count; i++)
        {
            // A friend's root stays where it spawned on this machine; the body seen is what moves.
            NetworkPlayerAvatar friend = friends[i];
            if (friend != null && !friend.IsOwner && friend.IsVisible && friend.Body != null)
            {
                Body(friend.Body.transform, settings.PlayerRadius, settings, settings.PlayerStrength);
            }
        }

        IReadOnlyList<MonsterAI> monsters = MonsterAI.Active;
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster != null && monster.CurrentState != MonsterAI.State.Dead)
            {
                Body(monster.transform, RadiusOf(monster.gameObject), settings);
            }
        }

        IReadOnlyList<ResidentNpc> residents = ResidentNpc.Present;
        for (int i = 0; i < residents.Count; i++)
        {
            if (residents[i] != null)
            {
                Body(residents[i].transform, 0.4f, settings);
            }
        }

        IReadOnlyList<Companion> companions = Companion.Active;
        for (int i = 0; i < companions.Count; i++)
        {
            if (companions[i] != null)
            {
                Body(companions[i].transform, 0.4f, settings);
            }
        }

        IReadOnlyList<Horse> horses = Horse.All;
        for (int i = 0; i < horses.Count; i++)
        {
            if (horses[i] != null && horses[i].isActiveAndEnabled)
            {
                Body(horses[i].transform, 0.9f, settings);
            }
        }

        lastSeen.Clear();
        foreach (KeyValuePair<Transform, Vector3> seen in seenNow)
        {
            lastSeen[seen.Key] = seen.Value;
        }
    }

    // strength: the player's own wake is stronger than anyone else's (Player Strength); parting caps at all of it.
    private void Body(Transform body, float radius, MistWakeSettings settings, float strength = 1f)
    {
        Vector3 at = body.position;
        seenNow[body] = at;
        Vector3 velocity = lastSeen.TryGetValue(body, out Vector3 was) && Time.deltaTime > 0f ? (at - was) / Time.deltaTime : Vector3.zero;
        // A teleport (waking at the portal, a warp) isn't a dash through the mist.
        if (velocity.sqrMagnitude > 30f * 30f)
        {
            velocity = Vector3.zero;
        }

        velocity.y = 0f;
        float speed = velocity.magnitude;
        Stamp(at, radius * settings.BodyScale * (1f + 0.15f * Mathf.Clamp01(speed / settings.FullSpeed)), velocity * strength, settings.PresenceAt(speed) * strength);
    }

    // A creature's width, from its agent and size (a troll parts more than a rootling); found once.
    private float RadiusOf(GameObject creature)
    {
        if (!radii.TryGetValue(creature, out float radius))
        {
            // Creatures come and go all raid long: the widths are found again rather than kept for ever.
            if (radii.Count > 512)
            {
                radii.Clear();
            }

            radius = creature.TryGetComponent(out NavMeshAgent agent) ? Mathf.Clamp(agent.radius * creature.transform.lossyScale.x, 0.25f, 3f) : 0.5f;
            radii[creature] = radius;
        }

        return radius;
    }
}
