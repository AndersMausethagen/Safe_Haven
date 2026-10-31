using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Small birds around the player, for mood and to keep a place from feeling the same twice (BirdProfile has the numbers). Flocks
/// fly in from out of sight and land on ground the player has explored, peck about, and go up when the player comes too
/// close: how close is how far they can be heard (PlayerNoise), so running sends them up early and sneaking gets near.
/// Now and then a flock only passes over. They leave at dusk and in the rain as they would. All the birds are one particle
/// system, its particles placed by hand each frame: one draw, no objects, and a flipbook for the wings.
/// <para>
/// Where it belongs: the Birds prefab in a scene, with the place's profile. Purely visual: nothing in the game hears or sees
/// them, and they change nothing.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class Birds : MonoBehaviour
{
    private const int MostBirds = 48;
    private const int Pictures = 8;
    private const float SettledScanSeconds = 0.15f;
    private const float ArrivalDistance = 34f;
    private const float ArrivalHeight = 11f;
    private const float PassingHeight = 10f;
    private const float PassingDistance = 40f;
    private const float SpotReach = 1.5f;
    private const float ShadowSize = 0.8f;
    private const float NightShade = 0.3f;
    private const float QuietSeconds = 6f;
    private static readonly Color32 ShadowColour = new Color32(0, 0, 0, 56);

    [SerializeField] private BirdProfile profile;

    private readonly List<BirdFlock> flocks = new List<BirdFlock>();
    private ParticleSystem.Particle[] particles;
    private ParticleSystem system;
    private float nextArrival;
    private float nextScan;
    private float nextDeparture;

    /// <summary>The flocks about now.</summary>
    public IReadOnlyList<BirdFlock> Flocks => flocks;

    private void Awake()
    {
        system = GetComponent<ParticleSystem>();
        if (profile == null)
        {
            enabled = false;
            return;
        }

        // Each bird and its shadow.
        particles = new ParticleSystem.Particle[MostBirds * 2];
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 0f;
        main.maxParticles = particles.Length;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 4;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.cycleCount = 1;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        system.Play();
        nextArrival = Time.time + Random.Range(1f, 4f);
    }

    /// <summary>What a particle's remaining life has to be to show a picture of the sheet: the sheet plays over its life.</summary>
    public static float LifeForPicture(int picture) => 1f - (picture + 0.5f) / Pictures;

    private void LateUpdate()
    {
        Player player = Player.Current;
        Camera view = Camera.main;
        if (player == null || view == null)
        {
            return;
        }

        float seconds = Time.deltaTime;
        Vector3 at = player.transform.position;
        int wanted = profile.FlocksFor(AtmosphereConditions.Night, AtmosphereConditions.WeatherRain);
        Arrive(at, wanted);
        Disturb(player, at, wanted);
        for (int i = flocks.Count - 1; i >= 0; i--)
        {
            BirdFlock flock = flocks[i];
            flock.Tick(seconds);
            if (flock.IsGone || TooFar(flock, at))
            {
                flocks.RemoveAt(i);
            }
            else
            {
                Call(flock, seconds);
            }
        }

        Draw(view);
    }

    private void Arrive(Vector3 at, int wanted)
    {
        if (Time.time < nextArrival || Settled() >= wanted || wanted <= 0)
        {
            return;
        }

        nextArrival = Time.time + profile.RandomArrival;
        if (Random.value < profile.FlyoverChance)
        {
            Pass(at);
        }
        else
        {
            TryLand(at);
        }
    }

    private int Settled()
    {
        int settled = 0;
        foreach (BirdFlock flock in flocks)
        {
            if (!flock.IsLeaving)
            {
                settled++;
            }
        }

        return settled;
    }

    private void TryLand(Vector3 at)
    {
        ExplorationMap explored = ExplorationMap.Current;
        Vector2 way = Random.insideUnitCircle.normalized;
        Vector3 direction = new Vector3(way.x, 0f, way.y);
        Vector3 wanted = at + direction * Random.Range(profile.LandingDistance.x, Mathf.Max(profile.LandingDistance.x, profile.LandingDistance.y));
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit centre, SpotReach * 2f, NavMesh.AllAreas)
            || (explored != null && explored.ExploredAt(centre.position) < 0.5f))
        {
            return;
        }

        var spots = new List<Vector3>(profile.FlockSizeMax);
        int count = Random.Range(profile.FlockSizeMin, profile.FlockSizeMax + 1);
        for (int i = 0; i < count * 2 && spots.Count < count; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * profile.FlockSpread;
            if (NavMesh.SamplePosition(centre.position + new Vector3(scatter.x, 0f, scatter.y), out NavMeshHit spot, SpotReach, NavMesh.AllAreas))
            {
                spots.Add(spot.position);
            }
        }

        if (spots.Count > 0)
        {
            Vector3 from = centre.position + direction * ArrivalDistance + Vector3.up * ArrivalHeight;
            flocks.Add(BirdFlock.Landing(profile, from, centre.position, spots.ToArray()));
        }
    }

    private void Pass(Vector3 at)
    {
        float heading = Random.Range(0f, Mathf.PI * 2f);
        var direction = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        Vector3 from = at - direction * PassingDistance * 0.5f + Vector3.up * PassingHeight;
        int count = Random.Range(profile.FlockSizeMin, profile.FlockSizeMax + 1);
        flocks.Add(BirdFlock.Passing(profile, from, direction * profile.RandomSpeed, count));
    }

    // A few times a second: anything too near the player goes up, and at dusk or in the rain one flock at a time makes for cover.
    private void Disturb(Player player, Vector3 at, int wanted)
    {
        if (Time.time < nextScan)
        {
            return;
        }

        nextScan = Time.time + SettledScanSeconds;
        float heardFrom = player.Noise != null ? player.Noise.Level : 0f;
        float startle = profile.StartleDistance(heardFrom);
        foreach (BirdFlock flock in flocks)
        {
            Vector3 offset = flock.Centre - at;
            offset.y = 0f;
            if (flock.IsSettled && offset.sqrMagnitude <= startle * startle)
            {
                GoUp(flock, at);
            }
        }

        if (Settled() > wanted && Time.time >= nextDeparture)
        {
            nextDeparture = Time.time + QuietSeconds;
            foreach (BirdFlock flock in flocks)
            {
                if (flock.IsSettled)
                {
                    // Not frightened, only leaving: from a point of their own, so they don't all fly from the player.
                    flock.Startle(flock.Centre + Random.insideUnitSphere);
                    AudioManager.Play(profile.TakeOff, flock.Centre);
                    break;
                }
            }
        }
    }

    private void GoUp(BirdFlock flock, Vector3 from)
    {
        flock.Startle(from);
        AudioManager.Play(profile.TakeOff, flock.Centre);
        if (profile.CallsOnTakeOff)
        {
            AudioManager.Play(profile.Call, flock.Centre);
        }
    }

    private void Call(BirdFlock flock, float seconds)
    {
        if (flock.IsLeaving)
        {
            return;
        }

        flock.NextCall -= seconds;
        if (flock.NextCall <= 0f)
        {
            flock.NextCall = profile.RandomCall;
            AudioManager.Play(profile.Call, flock.Centre);
        }
    }

    private bool TooFar(BirdFlock flock, Vector3 at)
    {
        Vector3 offset = flock.Centre - at;
        offset.y = 0f;
        return !flock.IsLeaving && offset.sqrMagnitude > profile.LeaveDistance * profile.LeaveDistance;
    }

    private void Draw(Camera view)
    {
        Vector3 right = view.transform.right;
        Vector3 up = view.transform.up;
        float night = Mathf.Lerp(1f, NightShade, Mathf.Clamp01(AtmosphereConditions.Night));
        Terrain terrain = Terrain.activeTerrain;
        ExplorationMap explored = ExplorationMap.Current;
        int count = 0;
        foreach (BirdFlock flock in flocks)
        {
            for (int i = 0; i < flock.Birds.Length && count + 2 <= particles.Length; i++)
            {
                ref Bird bird = ref flock.Birds[i];
                if (bird.State != BirdState.Perched && bird.Timer > 0f)
                {
                    continue;
                }

                float opacity = flock.Opacity(bird);
                if (explored != null && bird.State == BirdState.Perched && explored.ExploredAt(bird.Position) < 0.5f)
                {
                    opacity = 0f;
                }

                var heading = new Vector3(Mathf.Sin(bird.Facing), 0f, Mathf.Cos(bird.Facing));
                float angle = Mathf.Atan2(Vector3.Dot(heading, right), Vector3.Dot(heading, up)) * Mathf.Rad2Deg;
                int picture = BirdFlock.FrameOf(bird);
                Color colour = bird.Colour * night;
                colour.a = opacity;
                particles[count++] = Particle(bird.Position, bird.Size, colour, angle, picture);
                if (bird.State != BirdState.Perched && terrain != null)
                {
                    Vector3 ground = bird.Position;
                    ground.y = terrain.transform.position.y + terrain.SampleHeight(ground) + 0.05f;
                    Color32 shadow = ShadowColour;
                    shadow.a = (byte)(shadow.a * opacity);
                    particles[count++] = Particle(ground, bird.Size * ShadowSize, shadow, angle, picture);
                }
            }
        }

        system.SetParticles(particles, count);
    }

    private static ParticleSystem.Particle Particle(Vector3 position, float size, Color32 colour, float angle, int picture) => new ParticleSystem.Particle
    {
        position = position,
        startSize = size,
        startColor = colour,
        rotation = angle,
        startLifetime = 1f,
        remainingLifetime = LifeForPicture(picture),
    };
}
