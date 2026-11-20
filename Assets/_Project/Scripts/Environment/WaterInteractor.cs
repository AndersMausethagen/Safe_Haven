using System.Collections.Generic;
using UnityEngine;

/// <summary>Makes "Safe Haven/Water" react to this object: ripples and wake foam when it moves through a
/// <see cref="WaterSurface"/>, a splash when it drops in fast (a thrown stone, a jump), or, as Constant, endless churn
/// and foam (a waterfall's plunge pool, rapids around a rock). Ripples go into <see cref="WaterRipples"/> (play mode
/// only); foam spots go to the shader as globals (_WaterFoamSpots = x, z, radius, strength; up to
/// <see cref="MaxFoamSpots"/>, nearest the camera first) and also show in edit mode.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class WaterInteractor : MonoBehaviour
{
    public const int MaxFoamSpots = 8;

    public enum Kind
    {
        Moving,
        Constant
    }

    [Tooltip("Moving: ripples, wake foam and splashes from this object's own movement (the player, a thrown stone). " +
             "Constant: always churns the water around it (a waterfall's plunge pool, rapids).")]
    [SerializeField] private Kind kind = Kind.Moving;
    [Tooltip("Metres around the point that it disturbs.")]
    [SerializeField, Min(0.05f)] private float radius = 0.45f;
    [Tooltip("Point that touches the water, relative to this object (e.g. the feet).")]
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField, Range(0f, 4f)] private float ripples = 1f;
    [Tooltip("Foam around it: always for Constant, when moving fast (3 m/s and up) for Moving.")]
    [SerializeField, Range(0f, 2f)] private float foam = 0.5f;
    [Tooltip("Spawned where it drops into the water fast enough (e.g. Art/Generated/Water/Water Splash).")]
    [SerializeField] private GameObject splash;
    [Tooltip("Spawned under each planted foot in water or a rain puddle (e.g. Art/Generated/Water/Footstep Splash). " +
             "Falls back to Splash when empty.")]
    [SerializeField] private GameObject footstepSplash;
    [Tooltip("Downward speed (m/s) at which entering the water splashes.")]
    [SerializeField, Min(0f)] private float splashSpeed = 2.5f;

    private const float MoveRipples = 0.08f;   // stamp depth per second per m/s of horizontal speed
    private const float ChurnRipples = 0.4f;  // stamp depth per second of a Constant interactor
    private const float DropRipples = 0.06f;  // stamp depth per m/s of downward speed on entering

    private static readonly List<WaterInteractor> Active = new List<WaterInteractor>();
    private static readonly List<WaterInteractor> Spotted = new List<WaterInteractor>();
    private static readonly Vector4[] Spots = new Vector4[MaxFoamSpots];
    private static readonly int SpotsId = Shader.PropertyToID("_WaterFoamSpots");
    private static readonly int SpotCountId = Shader.PropertyToID("_WaterFoamSpotCount");
    private static int updatedFrame = -1;

    private Vector3 lastPoint;
    private bool hasLast;
    private bool inWater;
    private float pending;
    // How far it moved (x, z) since its last stamp: what floats under it is dragged along (WaterRipples' drift).
    private Vector3 pendingMoved;
    // Its wake a share stronger than the Moving rate (a swimmer's bow: PlayerSwimming).
    private float wake = 1f;
    private float spotStrength;
    private float surface;

    public Kind Mode
    {
        get => kind;
        set => kind = value;
    }

    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0.05f, value);
    }

    public float Foam
    {
        get => foam;
        set => foam = Mathf.Max(0f, value);
    }

    public GameObject Splash
    {
        get => splash;
        set => splash = value;
    }

    public GameObject FootstepSplash
    {
        get => footstepSplash;
        set => footstepSplash = value;
    }

    public Vector3 Offset
    {
        get => offset;
        set => offset = value;
    }

    public Vector3 Point => transform.TransformPoint(offset);

    /// <summary>How much stronger than walking its wake is while it moves through the water (1 as made; a swimmer's is more).</summary>
    public float Wake
    {
        get => wake;
        set => wake = Mathf.Max(0f, value);
    }

    public bool InWater => inWater;

    /// <summary>Foam spots sent to the shader by the last <see cref="UpdateAll"/>.</summary>
    public static int SpotCount { get; private set; }

    /// <summary>Splashes on water or a terrain rain puddle; pond and river contacts also add a ripple.</summary>
    public bool SplashFootstep(Vector3 point)
    {
        if (!Application.isPlaying || kind != Kind.Moving)
        {
            return false;
        }

        bool surfaceWater = WaterSurface.TryFind(point + Vector3.up * 0.1f, 0.3f, out float height);
        if (!surfaceWater && !TerrainPuddles.TryFind(point, out height))
        {
            return false;
        }

        Vector3 at = new Vector3(point.x, height, point.z);
        if (surfaceWater)
        {
            WaterRipples.AddStamp(at, Mathf.Max(radius * 0.75f, 0.15f), 0.24f * ripples);
        }
        SpawnSplash(at, footstepSplash != null ? footstepSplash : splash);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Active.Clear();
        Spotted.Clear();
        updatedFrame = -1;
        SpotCount = 0;
        Shader.SetGlobalFloat(SpotCountId, 0f);
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }

        hasLast = false;
        if (Application.isPlaying)
        {
            WaterRipples.Prewarm();
        }

        UpdateAll(0f);
    }

    private void OnDisable()
    {
        Active.Remove(this);
        UpdateAll(0f);
    }

    private void LateUpdate()
    {
        if (updatedFrame != Time.frameCount)
        {
            updatedFrame = Time.frameCount;
            UpdateAll(Application.isPlaying ? Time.deltaTime : 0f);
        }
    }

    /// <summary>Senses every interactor's contact with the water, feeds the ripple simulation (when
    /// <paramref name="deltaTime"/> &gt; 0) and uploads the foam spots. Returns the number of foam spots.</summary>
    public static int UpdateAll(float deltaTime)
    {
        Active.RemoveAll(interactor => interactor == null);
        int steps = deltaTime > 0f ? WaterRipples.Advance(deltaTime) : 0;
        Camera camera = Camera.main;
        Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
        Spotted.Clear();
        WaterInteractor focus = null;
        foreach (WaterInteractor interactor in Active)
        {
            interactor.Sense(deltaTime, steps > 0);
            if (interactor.spotStrength > 0.01f)
            {
                Spotted.Add(interactor);
            }

            if (interactor.kind == Kind.Moving && interactor.inWater &&
                (focus == null || (interactor.Point - eye).sqrMagnitude < (focus.Point - eye).sqrMagnitude))
            {
                focus = interactor;
            }
        }

        // Only where there is water to ripple: once made, the texture kept the blit running in every scene after.
        if (steps > 0 && WaterSurface.Surfaces.Count > 0 && (focus != null || WaterRipples.PendingStamps > 0 || WaterRipples.Texture != null))
        {
            Vector3 centre = focus != null ? focus.Point : camera != null ? GroundPoint(camera) : Vector3.zero;
            WaterRipples.Run(steps, centre);
        }
        else if (WaterSurface.Surfaces.Count == 0 && WaterRipples.Texture != null)
        {
            // A scene with no water: the last one's ripple textures go, and the shader's ripples with them.
            WaterRipples.Release();
        }

        if (Spotted.Count > MaxFoamSpots)
        {
            Spotted.Sort((a, b) => (a.Point - eye).sqrMagnitude.CompareTo((b.Point - eye).sqrMagnitude));
        }

        int count = Mathf.Min(Spotted.Count, MaxFoamSpots);
        for (int i = 0; i < MaxFoamSpots; i++)
        {
            Spots[i] = i < count
                ? new Vector4(Spotted[i].Point.x, Spotted[i].Point.z, Spotted[i].radius, Spotted[i].spotStrength)
                : Vector4.zero;
        }

        Shader.SetGlobalVectorArray(SpotsId, Spots);
        Shader.SetGlobalFloat(SpotCountId, count);
        SpotCount = count;
        return count;
    }

    private static Vector3 GroundPoint(Camera camera)
    {
        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        return Mathf.Abs(ray.direction.y) > 1e-3f && ray.direction.y < 0f
            ? ray.GetPoint(ray.origin.y / -ray.direction.y)
            : camera.transform.position;
    }

    private void Sense(float deltaTime, bool stepping)
    {
        Vector3 point = Point;
        Vector3 velocity = hasLast && deltaTime > 0f ? (point - lastPoint) / deltaTime : Vector3.zero;
        lastPoint = point;
        hasLast = true;
        bool wet = WaterSurface.TryFind(point, radius, out float height);
        float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
        spotStrength = 0f;
        if (!wet)
        {
            pending = 0f;
            pendingMoved = Vector3.zero;
            inWater = false;
            return;
        }

        surface = height;
        if (!inWater && deltaTime > 0f && -velocity.y >= splashSpeed)
        {
            pending += Mathf.Min(-velocity.y, 8f) * DropRipples * ripples;
            SpawnSplash(new Vector3(point.x, height, point.z));
        }

        inWater = true;
        pending += (kind == Kind.Constant ? ChurnRipples : horizontal * MoveRipples * wake) * ripples * deltaTime;
        if (kind == Kind.Moving && inWater)
        {
            // At most a sprint: a jump of the point itself (a teleport, the swimmer's point moving to the chest) drags nothing.
            pendingMoved += Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z), 8f) * deltaTime;
        }
        if (stepping && pending > 1e-4f)
        {
            Vector3 at = point;
            float size = radius;
            if (kind == Kind.Constant)
            {
                Vector2 random = Random.insideUnitCircle * radius * 0.8f;
                at += new Vector3(random.x, 0f, random.y);
                size = radius * 0.35f;
            }

            WaterRipples.AddStamp(at, size, pending, pendingMoved);
            pending = 0f;
            pendingMoved = Vector3.zero;
        }

        spotStrength = kind == Kind.Constant ? foam : foam * Mathf.Clamp01(horizontal / 3f);
    }

    private void SpawnSplash(Vector3 at) => SpawnSplash(at, splash);

    private static void SpawnSplash(Vector3 at, GameObject prefab)
    {
        if (prefab != null && Application.isPlaying)
        {
            Instantiate(prefab, at, Quaternion.identity);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(Point, radius);
        if (inWater)
        {
            Gizmos.DrawLine(Point, new Vector3(Point.x, surface, Point.z));
        }
    }
}
