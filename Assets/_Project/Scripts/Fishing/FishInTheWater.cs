using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fish you can see. In the open water round the player (ponds, lakes, rivers: the Haven's, the prologue's, a raid's) a few
/// fish of the kinds that bite there swim under the surface, each its own length (FishSizes), wandering and resting, and
/// scattering from someone wading in or a float landing on them. When a line goes out, one of those near the float comes to
/// it, and the bite is that fish (PlayerFishing takes it with <see cref="TakeBiter"/>): the big one you saw is the one you
/// hook, if it comes at all (a big fish is wary; good bait helps, FishSizes.BiteWillingness). Drawn with the landed fish's
/// shape and material (FishingLine.SharedFishMesh, FishingSettings › Fish Material, tinted by its kind's colour). Each game
/// shows its own: friends see their own fish. Made by itself in every scene.
/// </summary>
public sealed class FishInTheWater : MonoBehaviour
{
    private const float Reach = 36f;
    private const float Forget = 55f;
    private const int Most = 22;
    private const float AttractReach = 14f;
    private const float WaterDepth = 0.55f;

    private sealed class Swimmer
    {
        public Transform Body;
        public MeshRenderer Renderer;
        public WaterSurface Water;
        public FishingSettings.Fish Kind;
        public float Length;
        public Vector3 Target;
        public float Speed;
        public float RestUntil;
        public float Depth;
        public float Phase;
        public bool Coming;
    }

    private readonly List<Swimmer> swimmers = new List<Swimmer>();
    private MaterialPropertyBlock block;
    private float nextLook;

    /// <summary>The scene's, or null.</summary>
    public static FishInTheWater Current { get; private set; }

    /// <summary>How many swim here now (tests, probes).</summary>
    public int Count => swimmers.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || (Current != null && Current.gameObject.scene == scene) || FishingSettings.Current.FishList.Count == 0)
        {
            return;
        }

        var holder = new GameObject("Fish in the Water");
        SceneManager.MoveGameObjectToScene(holder, scene);
        holder.AddComponent<FishInTheWater>();
    }

    private void Awake()
    {
        Current = this;
        block = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// A line has landed at <paramref name="landing"/>: what swims right under it scatters, and one fish near it (by how
    /// willing it is, big ones less so unless the bait is good) turns and comes to the float.
    /// </summary>
    public void Attract(Vector3 landing, float baitEase)
    {
        Release();
        float total = 0f;
        foreach (Swimmer swimmer in swimmers)
        {
            float apart = Flat(swimmer.Body.position - landing).magnitude;
            if (apart < 2.2f)
            {
                Scatter(swimmer, landing);
            }
            else if (apart < AttractReach)
            {
                total += FishSizes.BiteWillingness(swimmer.Length, baitEase);
            }
        }

        // Now and then nothing in sight takes an interest, and the bite comes from the water's unseen fish.
        float roll = Random.value * total * 1.25f;
        foreach (Swimmer swimmer in swimmers)
        {
            float apart = Flat(swimmer.Body.position - landing).magnitude;
            if (apart < 2.2f || apart >= AttractReach)
            {
                continue;
            }

            roll -= FishSizes.BiteWillingness(swimmer.Length, baitEase);
            if (roll <= 0f)
            {
                swimmer.Coming = true;
                swimmer.Target = new Vector3(landing.x, swimmer.Body.position.y, landing.z);
                swimmer.Speed = Mathf.Clamp(0.45f + swimmer.Length / 120f, 0.45f, 1.1f);
                swimmer.RestUntil = 0f;
                return;
            }
        }
    }

    /// <summary>The line is in: whatever was coming to it goes back to its wandering.</summary>
    public void Release()
    {
        foreach (Swimmer swimmer in swimmers)
        {
            swimmer.Coming = false;
        }
    }

    /// <summary>
    /// The fish that bites at the float: the one that came to it (or failing that one right under it), which leaves the
    /// water here to be the hooked one. False when none did: the bite is one of the water's unseen fish.
    /// </summary>
    public bool TakeBiter(Vector3 landing, out FishingSettings.Fish kind, out float centimetres)
    {
        kind = null;
        centimetres = 0f;
        Swimmer best = null;
        float nearest = 3f;
        foreach (Swimmer swimmer in swimmers)
        {
            float apart = Flat(swimmer.Body.position - landing).magnitude;
            if (swimmer.Coming && apart < 6f)
            {
                best = swimmer;
                break;
            }

            if (apart < nearest)
            {
                nearest = apart;
                best = swimmer;
            }
        }

        if (best == null)
        {
            return false;
        }

        kind = best.Kind;
        centimetres = best.Length;
        Remove(best);
        return true;
    }

    private void Update()
    {
        Player player = Player.Current;
        if (player == null)
        {
            return;
        }

        Vector3 standing = player.transform.position;
        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 1f;
            for (int i = swimmers.Count - 1; i >= 0; i--)
            {
                if (swimmers[i].Body == null || swimmers[i].Water == null || Flat(swimmers[i].Body.position - standing).magnitude > Forget)
                {
                    Remove(swimmers[i]);
                }
            }

            Populate(standing);
        }

        foreach (Swimmer swimmer in swimmers)
        {
            Swim(swimmer, standing);
        }
    }

    // A few at a time, in open water round the player deep enough to swim in, as many as the water's size allows.
    private void Populate(Vector3 standing)
    {
        FishingSettings settings = FishingSettings.Current;
        for (int tries = 0; tries < 8 && swimmers.Count < Most; tries++)
        {
            Vector2 offset = Random.insideUnitCircle * Reach;
            var at = new Vector3(standing.x + offset.x, standing.y, standing.z + offset.y);
            WaterSurface water = WaterSurface.OpenAt(at, 25f, out float height, WaterDepth);
            if (water == null || CountIn(water) >= Capacity(water))
            {
                continue;
            }

            FishingSettings.Fish kind = settings.PickFish(water.IsStill, Random.value);
            if (kind == null)
            {
                continue;
            }

            Spawn(water, kind, FishSizes.Roll(Random.value, Random.value, kind.lengthScale), new Vector3(at.x, height, at.z));
        }
    }

    private int CountIn(WaterSurface water)
    {
        int count = 0;
        foreach (Swimmer swimmer in swimmers)
        {
            count += swimmer.Water == water ? 1 : 0;
        }

        return count;
    }

    // About one fish to each 60 square metres of open water, two to eight.
    private static int Capacity(WaterSurface water)
    {
        Bounds bounds = water.TryGetComponent(out Renderer drawn) ? drawn.bounds : new Bounds(water.transform.position, Vector3.one * 10f);
        return Mathf.Clamp(Mathf.RoundToInt(bounds.size.x * bounds.size.z / 60f), 2, 8);
    }

    private void Spawn(WaterSurface water, FishingSettings.Fish kind, float centimetres, Vector3 surface)
    {
        var body = new GameObject($"{(kind.item != null ? kind.item.DisplayName : "Fish")} ({centimetres:0} cm)");
        body.transform.SetParent(transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = FishingLine.SharedFishMesh;
        var drawn = body.AddComponent<MeshRenderer>();
        drawn.sharedMaterial = FishingSettings.Current.FishMaterial;
        drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        drawn.receiveShadows = false;
        // Darker under the water than in the hand.
        block.SetColor("_BaseColor", kind.colour * 0.85f);
        drawn.SetPropertyBlock(block);
        body.transform.localScale = Vector3.one * (centimetres / 100f);
        var swimmer = new Swimmer
        {
            Body = body.transform,
            Renderer = drawn,
            Water = water,
            Kind = kind,
            Length = centimetres,
            Depth = Mathf.Lerp(0.1f, 0.26f, Random.value) + centimetres / 500f,
            Phase = Random.value * 10f,
            Speed = Mathf.Lerp(0.25f, 0.7f, Random.value),
        };
        body.transform.SetPositionAndRotation(new Vector3(surface.x, surface.y - swimmer.Depth, surface.z), Quaternion.Euler(0f, Random.value * 360f, 0f));
        swimmer.Target = NewTarget(swimmer);
        swimmers.Add(swimmer);
    }

    private void Remove(Swimmer swimmer)
    {
        swimmers.Remove(swimmer);
        if (swimmer.Body != null)
        {
            Destroy(swimmer.Body.gameObject);
        }
    }

    private void Swim(Swimmer swimmer, Vector3 player)
    {
        Transform body = swimmer.Body;
        if (body == null)
        {
            return;
        }

        Vector3 at = body.position;
        // Someone wading in sends it off.
        if (!swimmer.Coming && Flat(at - player).sqrMagnitude < 9f && swimmer.Speed < 1.5f)
        {
            Scatter(swimmer, player);
        }

        float tail = Mathf.Sin((Time.time + swimmer.Phase) * Mathf.Lerp(4f, 14f, swimmer.Speed / 2f)) * Mathf.Lerp(4f, 12f, swimmer.Speed / 2f);
        Vector3 toward = Flat(swimmer.Target - at);
        bool resting = Time.time < swimmer.RestUntil || toward.magnitude < 0.4f;
        if (resting)
        {
            if (!swimmer.Coming && Time.time >= swimmer.RestUntil)
            {
                swimmer.RestUntil = Time.time + Random.Range(0.6f, 3.5f);
                swimmer.Target = NewTarget(swimmer);
                swimmer.Speed = Mathf.Lerp(0.25f, 0.7f, Random.value);
            }

            body.rotation = Quaternion.Euler(0f, body.eulerAngles.y + tail * Time.deltaTime * 0.5f, 0f);
            return;
        }

        Quaternion facing = Quaternion.LookRotation(toward.normalized);
        float turned = Mathf.MoveTowardsAngle(body.eulerAngles.y, facing.eulerAngles.y, 140f * Time.deltaTime);
        Vector3 forward = Quaternion.Euler(0f, turned, 0f) * Vector3.forward;
        Vector3 next = at + forward * swimmer.Speed * Time.deltaTime;
        // Never out of its own water, nor into the shallows.
        if (WaterSurface.OpenAt(next, 3f, out float surface, WaterDepth * 0.7f) != swimmer.Water)
        {
            swimmer.Target = NewTarget(swimmer);
            swimmer.Speed = Mathf.Min(swimmer.Speed, 0.7f);
            return;
        }

        body.SetPositionAndRotation(new Vector3(next.x, surface - swimmer.Depth, next.z), Quaternion.Euler(0f, turned + tail, 0f));
        if (swimmer.Speed > 0.9f && !swimmer.Coming)
        {
            swimmer.Speed = Mathf.MoveTowards(swimmer.Speed, 0.6f, 0.5f * Time.deltaTime);
        }
    }

    private static void Scatter(Swimmer swimmer, Vector3 from)
    {
        Vector3 away = Flat(swimmer.Body.position - from);
        away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
        swimmer.Target = swimmer.Body.position + Flat(away) * Random.Range(5f, 9f);
        swimmer.Speed = Random.Range(1.8f, 2.6f);
        swimmer.RestUntil = 0f;
        swimmer.Coming = false;
    }

    // Somewhere else in its own open water, a few metres off.
    private static Vector3 NewTarget(Swimmer swimmer)
    {
        Vector3 at = swimmer.Body.position;
        for (int i = 0; i < 6; i++)
        {
            Vector2 step = Random.insideUnitCircle.normalized * Random.Range(3f, 10f);
            var candidate = new Vector3(at.x + step.x, at.y, at.z + step.y);
            if (WaterSurface.OpenAt(candidate, 3f, out _, WaterDepth) == swimmer.Water)
            {
                return candidate;
            }
        }

        return at;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
