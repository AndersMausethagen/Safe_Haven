using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Water dripping off things in and after the rain: drops falling from the eaves of roofs and from the crowns of the
/// tall trees round what the camera looks at, each splashing where it lands (RainEffect.SplashAt). It goes on while
/// the world is still wet after the rain stops, thinning as it dries, and stops when the rain falls as snow.
/// Buildings (BuildingObject) drip all round the outline of their roof, at the height their walls' collider ends (half
/// their height if that tells nothing). Anything else sloping (a rock, a mesh-collider roof) is found by touch: a ray
/// dropped on it walks down the slope until it gives way, and the last point drips. Crowns are CanopyDebris's.
/// Added by RainEffect in play, with its drops' look.
/// </summary>
public sealed class RainDrips : MonoBehaviour
{
    private const int MostSpots = 48;
    private const int RaysPerFrame = 6;
    private const float Reach = 24f;
    private const float Gravity = 9.81f;

    [Tooltip("Drips a second from all eaves in a downpour; fewer as the rain lightens, a third as many while it is wet after.")]
    [SerializeField, Min(0f)] private float eavesPerSecond = 70f;
    [Tooltip("Drips a second from all the crowns near the camera.")]
    [SerializeField, Min(0f)] private float leavesPerSecond = 55f;

    /// <summary>Where a drip leaves and where it lands.</summary>
    public struct Spot
    {
        public Vector3 from;
        public Vector3 landing;
        public Vector3 normal;
    }

    private readonly List<Spot> eaves = new List<Spot>();
    private readonly Dictionary<BuildingObject, Spot[]> outlines = new Dictionary<BuildingObject, Spot[]>();
    private readonly List<Spot> houses = new List<Spot>();
    private float nextHouses;
    private readonly List<(float at, Vector3 point, Vector3 normal)> falling = new List<(float, Vector3, Vector3)>();
    private RainEffect rain;
    private ParticleSystem drops;
    private ParticleSystem.EmitParams emit;
    private Vector3 centre;
    private float eaveCarry;
    private float leafCarry;

    public void Initialise(RainEffect owner, ParticleSystemRenderer look)
    {
        rain = owner;
        var go = new GameObject("Drips");
        go.transform.SetParent(transform, false);
        drops = go.AddComponent<ParticleSystem>();
        drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = drops.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startSize = 0.035f;
        main.gravityModifier = 1f;
        main.maxParticles = 400;
        main.playOnAwake = false;
        main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.55f);
        ParticleSystem.EmissionModule emission = drops.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = drops.shape;
        shape.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.045f;
        renderer.lengthScale = 1f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (look != null)
        {
            renderer.sharedMaterial = look.sharedMaterial;
        }

        drops.Play();
    }

    /// <summary>How hard it drips: the rain falling, or a third of that while things are still wet after it.</summary>
    public static float Dripping(float rainfall, float wetness, bool snowing) =>
        snowing ? 0f : Mathf.Max(rainfall, Mathf.Clamp01(wetness) * 0.35f);

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        WeatherSystem weather = WeatherSystem.Active;
        if (camera == null || drops == null || weather == null)
        {
            return;
        }

        float strength = Dripping(weather.Snowing ? 0f : weather.Current.Rain, weather.Wetness, weather.Snowing || rain.AsSnow);
        Land();
        if (strength <= 0.01f)
        {
            return;
        }

        Vector3 now = RainEffect.GroundInView(camera);
        if ((now - centre).sqrMagnitude > 16f)
        {
            centre = now;
            Forget();
            nextHouses = 0f;
        }

        Search();
        if (Time.time >= nextHouses)
        {
            nextHouses = Time.time + 1f;
            NearHouses();
        }

        eaveCarry += eavesPerSecond * strength * Time.deltaTime;
        int spots = eaves.Count + houses.Count;
        while (eaveCarry >= 1f && spots > 0)
        {
            eaveCarry -= 1f;
            int pick = Random.Range(0, spots);
            Drip(pick < eaves.Count ? eaves[pick] : houses[pick - eaves.Count]);
        }

        eaveCarry = Mathf.Min(eaveCarry, 4f);
        CanopyDebris canopy = CanopyDebris.Current;
        leafCarry += leavesPerSecond * strength * Time.deltaTime;
        while (leafCarry >= 1f && canopy != null && canopy.Crowns.Count > 0)
        {
            leafCarry -= 1f;
            FromLeaves(canopy.Crowns[Random.Range(0, canopy.Crowns.Count)]);
        }

        leafCarry = Mathf.Min(leafCarry, 4f);
    }

    private void NearHouses()
    {
        houses.Clear();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building == null)
            {
                continue;
            }

            Vector3 offset = building.transform.position - centre;
            if (offset.x * offset.x + offset.z * offset.z > Reach * Reach)
            {
                continue;
            }

            if (!outlines.TryGetValue(building, out Spot[] outline))
            {
                Construction construction = building.GetComponent<Construction>();
                if (construction != null && !construction.IsStanding)
                {
                    continue; // still going up: try again when it stands
                }

                outline = Outline(building);
                outlines[building] = outline;
            }

            houses.AddRange(outline);
        }
    }

    /// <summary>Drip spots round a building's roof: its outline (the drawn bounds, overhang and all) a step apart, at
    /// the top of its walls' collider, each landing on what is below. None for anything under 2.2 m tall.</summary>
    public static Spot[] Outline(BuildingObject building)
    {
        Bounds local = building.LocalBounds;
        if (local.size.y < 2.2f || local.size.x < 1.2f || local.size.z < 1.2f)
        {
            return System.Array.Empty<Spot>();
        }

        Transform root = building.transform;
        float top = float.MinValue;
        foreach (Collider wall in building.GetComponentsInChildren<Collider>())
        {
            if (!wall.isTrigger)
            {
                top = Mathf.Max(top, root.InverseTransformPoint(wall.bounds.max).y);
            }
        }

        float share = (top - local.min.y) / local.size.y;
        float eave = share > 0.3f && share < 0.85f ? top : local.min.y + local.size.y * 0.5f;
        var spots = new List<Spot>();
        Vector3 min = local.min, max = local.max;
        var corners = new[] { new Vector3(min.x, eave, min.z), new Vector3(max.x, eave, min.z), new Vector3(max.x, eave, max.z),
                              new Vector3(min.x, eave, max.z) };
        for (int side = 0; side < 4; side++)
        {
            Vector3 a = corners[side], b = corners[(side + 1) % 4];
            int steps = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.7f));
            for (int i = 0; i < steps; i++)
            {
                Vector3 from = root.TransformPoint(Vector3.Lerp(a, b, (i + 0.5f) / steps));
                if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 30f, RainEffect.SplashLayers, QueryTriggerInteraction.Ignore))
                {
                    spots.Add(new Spot { from = from, landing = hit.point, normal = hit.normal });
                }
            }
        }

        return spots.ToArray();
    }

    // A drip from under a crown: somewhere out along its branches, falling to whatever is below.
    private void FromLeaves(Vector3 crown)
    {
        Vector2 out_ = Random.insideUnitCircle.normalized * Random.Range(1.2f, 3.2f);
        Vector3 from = crown + new Vector3(out_.x, Random.Range(-1.5f, 0.5f), out_.y);
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 30f, RainEffect.SplashLayers, QueryTriggerInteraction.Ignore))
        {
            Drip(new Spot { from = from, landing = hit.point, normal = hit.normal });
        }
    }

    private void Drip(Spot spot)
    {
        float drop = Mathf.Max(0.2f, spot.from.y - spot.landing.y);
        float seconds = Mathf.Sqrt(2f * drop / Gravity);
        emit.position = spot.from + (Vector3)(Random.insideUnitCircle * 0.04f);
        emit.velocity = Vector3.zero;
        emit.startLifetime = seconds;
        emit.applyShapeToPosition = false;
        drops.Emit(emit, 1);
        falling.Add((Time.time + seconds, spot.landing, spot.normal));
    }

    private void Land()
    {
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            if (Time.time >= falling[i].at)
            {
                rain.SplashAt(falling[i].point, falling[i].normal);
                falling.RemoveAt(i);
            }
        }
    }

    private void Forget()
    {
        for (int i = eaves.Count - 1; i >= 0; i--)
        {
            Vector3 offset = eaves[i].from - centre;
            if (offset.x * offset.x + offset.z * offset.z > Reach * Reach * 1.5f)
            {
                eaves.RemoveAt(i);
            }
        }
    }

    // A few rays a frame: drop one on the ground round the centre; on a sloping roof, walk down it to its edge.
    private void Search()
    {
        for (int r = 0; r < RaysPerFrame; r++)
        {
            Vector2 offset = Random.insideUnitCircle * Reach;
            var from = new Vector3(centre.x + offset.x, centre.y + 40f, centre.z + offset.y);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit roof, 80f, RainEffect.SplashLayers, QueryTriggerInteraction.Ignore)
                || roof.collider is TerrainCollider || roof.normal.y < 0.25f || roof.normal.y > 0.97f
                || roof.collider.GetComponentInParent<Animator>() != null) // a head and shoulders are no eave
            {
                continue;
            }

            if (TryEdge(roof, out Spot spot))
            {
                Keep(spot);
            }
        }
    }

    /// <summary>Walks down a roof from a point on it (downhill is where its normal leans) until the roof falls away
    /// more than a metre and a half: the eave, and where its drips land.</summary>
    public static bool TryEdge(RaycastHit roof, out Spot spot)
    {
        spot = default;
        var downhill = new Vector3(roof.normal.x, 0f, roof.normal.z);
        if (downhill.sqrMagnitude < 1e-4f)
        {
            return false;
        }

        downhill.Normalize();
        Vector3 last = roof.point;
        for (int step = 1; step <= 16; step++)
        {
            Vector3 probe = roof.point + downhill * (0.3f * step);
            if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out RaycastHit below, 40f, RainEffect.SplashLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (below.point.y < last.y - 1.5f)
            {
                spot = new Spot { from = last + downhill * 0.08f + Vector3.down * 0.05f, landing = below.point, normal = below.normal };
                return true;
            }

            if (below.point.y > last.y + 0.2f)
            {
                return false; // ran into something higher: not an eave
            }

            last = below.point;
        }

        return false;
    }

    private void Keep(Spot spot)
    {
        foreach (Spot known in eaves)
        {
            if ((known.from - spot.from).sqrMagnitude < 0.25f)
            {
                return; // a drip within half a metre already
            }
        }

        if (eaves.Count >= MostSpots)
        {
            eaves.RemoveAt(Random.Range(0, eaves.Count));
        }

        eaves.Add(spot);
    }
}
