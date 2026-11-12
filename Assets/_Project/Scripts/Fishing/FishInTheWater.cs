using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fish you can see. In the open water round the player (ponds, lakes, rivers: the Haven's, the prologue's, a raid's) a few
/// fish of the kinds that bite there swim under the surface, each its own length (FishSizes). They keep to the deep part
/// of the water and cruise there unhurried, a few tail beats and a glide, turning in curves and now and then hanging still;
/// every so often one comes up to sip a fly off the top, rings spreading where it took it (WaterRipples), and goes back
/// down. Someone wading in or a float landing on one sends it bolting deeper. When a line goes out, one of those near the
/// float comes to it, and the bite is that fish (PlayerFishing takes it with <see cref="TakeBiter"/>): the big one you saw
/// is the one you hook, if it comes at all (a big fish is wary; good bait helps, FishSizes.BiteWillingness).
/// Drawn with the landed fish's shape and material (FishingLine.SharedFishMesh, FishingSettings › Fish Material, tinted by
/// its kind's colour), its body bending in a travelling wave from head to tail as it swims. How they swim is
/// FishingSettings › Swimming. Each game shows its own: friends see their own fish. Made by itself in every scene.
/// </summary>
public sealed class FishInTheWater : MonoBehaviour
{
    private const float Reach = 36f;
    private const float Forget = 55f;
    private const int Most = 22;
    private const float AttractReach = 14f;
    private const float WaterDepth = 0.55f;
    private const float AnimateWithin = 45f;
    // The mesh runs from the tail fin (z -0.62) to the mouth (z 0.52).
    private const float MeshHead = 0.52f;
    private const float MeshLength = 1.14f;

    private enum Mood
    {
        Cruise,
        Hover,
        Rise,
        Bolt,
        Coming,
    }

    private sealed class Swimmer
    {
        public Transform Body;
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public Vector3[] Bent;
        public WaterSurface Water;
        public FishingSettings.Fish Kind;
        public float Length;
        public Mood Mood;
        public float MoodUntil;
        public Vector3 Target;
        public float Speed;
        public float Cruise;
        public float Yaw;
        public float YawRate;
        public float Pitch;
        public float Depth;
        public float WantDepth;
        public float DepthShare;
        public bool Kicking;
        public float StrokeUntil;
        public float Beat;
        public float BeatRate;
        public float Swing;
        public float NextRise;

        public bool Coming => Mood == Mood.Coming;
    }

    private static Vector3[] baseShape;

    private readonly List<Swimmer> swimmers = new List<Swimmer>();
    private MaterialPropertyBlock block;
    private float nextLook;

    /// <summary>The scene's, or null.</summary>
    public static FishInTheWater Current { get; private set; }

    /// <summary>How many swim here now (tests, probes).</summary>
    public int Count => swimmers.Count;

    /// <summary>How many have come up to take a fly since the scene began (probes).</summary>
    public int Rises { get; private set; }

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
        for (int i = swimmers.Count - 1; i >= 0; i--)
        {
            Remove(swimmers[i]);
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    private static FishingSettings.SwimTuning Tuning => FishingSettings.Current.Swimming;

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
                Bolt(swimmer, landing);
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
                swimmer.Mood = Mood.Coming;
                swimmer.Target = new Vector3(landing.x, swimmer.Body.position.y, landing.z);
                // Up from the deep towards the float, so it is seen coming.
                swimmer.WantDepth = Mathf.Min(swimmer.Depth, 0.3f + swimmer.Length / 400f);
                return;
            }
        }
    }

    /// <summary>The line is in: whatever was coming to it goes back to its wandering.</summary>
    public void Release()
    {
        foreach (Swimmer swimmer in swimmers)
        {
            if (swimmer.Coming)
            {
                Wander(swimmer);
            }
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

        float step = Time.deltaTime;
        if (step <= 0f)
        {
            return;
        }

        Camera eye = Camera.main;
        foreach (Swimmer swimmer in swimmers)
        {
            Swim(swimmer, standing, step);
            if (swimmer.Renderer.isVisible && (eye == null || (swimmer.Body.position - eye.transform.position).sqrMagnitude < AnimateWithin * AnimateWithin))
            {
                Bend(swimmer);
            }
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
        FishingSettings.SwimTuning tuning = Tuning;
        var body = new GameObject($"{(kind.item != null ? kind.item.DisplayName : "Fish")} ({centimetres:0} cm)");
        body.transform.SetParent(transform, false);
        Mesh shape = FishingLine.SharedFishMesh;
        baseShape ??= shape.vertices;
        var mesh = Instantiate(shape);
        mesh.name = "Swimming Fish";
        mesh.MarkDynamic();
        // Room for the tail's swing, so it isn't culled at the edge of the view.
        Bounds room = mesh.bounds;
        room.Expand(new Vector3(0.3f, 0.05f, 0.05f));
        mesh.bounds = room;
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        var drawn = body.AddComponent<MeshRenderer>();
        drawn.sharedMaterial = FishingSettings.Current.FishMaterial;
        drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        drawn.receiveShadows = false;
        // Darker under the water than in the hand.
        block.SetColor("_BaseColor", kind.colour * 0.85f);
        drawn.SetPropertyBlock(block);
        body.transform.localScale = Vector3.one * (centimetres / 100f);
        float size = Mathf.InverseLerp(10f, 120f, centimetres);
        var swimmer = new Swimmer
        {
            Body = body.transform,
            Renderer = drawn,
            Mesh = mesh,
            Bent = new Vector3[baseShape.Length],
            Water = water,
            Kind = kind,
            Length = centimetres,
            Cruise = Mathf.Lerp(tuning.cruiseSpeed.x, tuning.cruiseSpeed.y, Random.value * 0.7f + size * 0.3f),
            Yaw = Random.value * 360f,
            Beat = Random.value * Mathf.PI * 2f,
            // Big fish keep deeper.
            DepthShare = Mathf.Lerp(tuning.depthShare.x, tuning.depthShare.y, Random.value * 0.6f + size * 0.4f),
            NextRise = Time.time + Random.Range(0.2f, 1f) * tuning.riseEvery.y,
        };
        swimmer.Depth = swimmer.WantDepth = PreferredDepth(swimmer, surface);
        swimmer.Speed = swimmer.Cruise * 0.5f;
        body.transform.SetPositionAndRotation(new Vector3(surface.x, surface.y - swimmer.Depth, surface.z), Quaternion.Euler(0f, swimmer.Yaw, 0f));
        swimmers.Add(swimmer);
        Wander(swimmer);
    }

    private void Remove(Swimmer swimmer)
    {
        swimmers.Remove(swimmer);
        if (swimmer.Body != null)
        {
            Destroy(swimmer.Body.gameObject);
        }

        if (swimmer.Mesh != null)
        {
            Destroy(swimmer.Mesh);
        }
    }

    // How deep it likes to be here: its share of the water under it, within the tuning's range and off the bottom.
    private static float PreferredDepth(Swimmer swimmer, Vector3 surface)
    {
        FishingSettings.SwimTuning tuning = Tuning;
        float water = Mathf.Max(0.2f, surface.y - WaterSurface.GroundBelow(surface));
        if (float.IsInfinity(water))
        {
            water = tuning.depthRange.y;
        }

        float depth = Mathf.Clamp(water * swimmer.DepthShare, tuning.depthRange.x, tuning.depthRange.y);
        return Mathf.Min(depth, water - 0.12f - swimmer.Length / 600f);
    }

    private void Swim(Swimmer swimmer, Vector3 player, float step)
    {
        Transform body = swimmer.Body;
        if (body == null)
        {
            return;
        }

        FishingSettings.SwimTuning tuning = Tuning;
        Vector3 at = body.position;
        float now = Time.time;

        // Someone wading in sends it off.
        if (swimmer.Mood != Mood.Bolt && !swimmer.Coming && Flat(at - player).sqrMagnitude < 9f)
        {
            Bolt(swimmer, player);
        }

        Decide(swimmer, at, player, now, tuning);

        // Speed: a few tail beats, then a glide (burst and coast, as fish cruise); a bolt is all beat.
        float wanted;
        switch (swimmer.Mood)
        {
            case Mood.Hover: wanted = 0.03f; break;
            case Mood.Bolt: wanted = Mathf.Lerp(tuning.boltSpeed.x, tuning.boltSpeed.y, Mathf.InverseLerp(10f, 120f, swimmer.Length)); break;
            case Mood.Rise: wanted = swimmer.Cruise * 1.3f; break;
            case Mood.Coming: wanted = Mathf.Clamp(0.45f + swimmer.Length / 120f, 0.45f, 1.1f); break;
            default:
                if (now >= swimmer.StrokeUntil)
                {
                    swimmer.Kicking = !swimmer.Kicking;
                    swimmer.StrokeUntil = now + (swimmer.Kicking ? Random.Range(0.5f, 1.3f) : Random.Range(0.6f, 1.8f));
                }

                wanted = swimmer.Cruise * (swimmer.Kicking ? 1.3f : 0.45f);
                break;
        }

        float speedUp = swimmer.Mood == Mood.Bolt ? 7f : 0.9f;
        float slowDown = swimmer.Mood == Mood.Hover ? 0.6f : 0.35f;
        swimmer.Speed = Mathf.MoveTowards(swimmer.Speed, wanted, (wanted > swimmer.Speed ? speedUp : slowDown) * step);

        // Turning: towards the target, as fast as its size allows, smoothly so the path curves.
        Vector3 toward = Flat(swimmer.Target - at);
        float maxTurn = tuning.turnRate * Mathf.Clamp(Mathf.Sqrt(30f / Mathf.Max(5f, swimmer.Length)), 0.45f, 1.6f)
            * (swimmer.Mood == Mood.Bolt ? 2.2f : swimmer.Mood == Mood.Hover ? 0.35f : 1f);
        float wantedRate = 0f;
        if (toward.sqrMagnitude > 0.01f)
        {
            float off = Mathf.DeltaAngle(swimmer.Yaw, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg);
            wantedRate = Mathf.Clamp(off * 2.5f, -maxTurn, maxTurn);
        }

        swimmer.YawRate = Mathf.MoveTowards(swimmer.YawRate, wantedRate, maxTurn * 4f * step);
        swimmer.Yaw += swimmer.YawRate * step;

        // Depth: towards what it wants, never through the top or into the bed.
        if (!WaterSurface.At(at, 3f, out float surface))
        {
            surface = at.y + swimmer.Depth;
        }

        float bed = WaterSurface.GroundBelow(at);
        float floor = float.IsInfinity(bed) ? 5f : Mathf.Max(0.05f, surface - bed - 0.1f - swimmer.Length / 600f);
        float wantDepth = Mathf.Clamp(swimmer.WantDepth, 0.02f, floor);
        float vertical = swimmer.Mood == Mood.Rise || swimmer.Mood == Mood.Bolt ? 0.6f : 0.22f;
        float before = swimmer.Depth;
        swimmer.Depth = Mathf.MoveTowards(swimmer.Depth, wantDepth, vertical * step);
        float climb = (before - swimmer.Depth) / step;
        float pitch = -Mathf.Atan2(climb, Mathf.Max(0.15f, swimmer.Speed)) * Mathf.Rad2Deg;
        swimmer.Pitch = Mathf.Lerp(swimmer.Pitch, Mathf.Clamp(pitch, -35f, 35f), 1f - Mathf.Exp(-5f * step));

        // Forward, staying in its own open water: at the shore it turns away instead.
        Vector3 forward = Quaternion.Euler(0f, swimmer.Yaw, 0f) * Vector3.forward;
        Vector3 next = at + forward * (swimmer.Speed * step);
        if (WaterSurface.OpenAt(next, 3f, out float nextSurface, WaterDepth * 0.7f) != swimmer.Water)
        {
            next = at;
            nextSurface = surface;
            swimmer.Speed *= 0.5f;
            if (swimmer.Mood == Mood.Rise || swimmer.Mood == Mood.Coming)
            {
                Wander(swimmer);
            }
            else
            {
                swimmer.Target = NewTarget(swimmer, at - forward * 4f);
            }
        }

        // It leans into a turn a little, and the tail's beat sets how the body bends (Bend).
        float roll = Mathf.Clamp(-swimmer.YawRate * 0.12f, -18f, 18f);
        body.SetPositionAndRotation(new Vector3(next.x, nextSurface - swimmer.Depth, next.z), Quaternion.Euler(swimmer.Pitch, swimmer.Yaw, roll));

        float effort = swimmer.Mood == Mood.Hover ? 0.25f : swimmer.Mood == Mood.Cruise && !swimmer.Kicking ? 0.15f
            : Mathf.Clamp(swimmer.Speed / Mathf.Max(0.1f, swimmer.Cruise), 0.6f, 3.2f);
        float beatRate = tuning.tailBeats * (swimmer.Mood == Mood.Bolt ? 2.6f : effort) * Mathf.Sqrt(30f / Mathf.Max(8f, swimmer.Length));
        swimmer.BeatRate = Mathf.Lerp(swimmer.BeatRate, beatRate, 1f - Mathf.Exp(-4f * step));
        swimmer.Beat += swimmer.BeatRate * Mathf.PI * 2f * step;
        float swing = swimmer.Mood == Mood.Bolt ? 0.11f : swimmer.Mood == Mood.Hover ? 0.025f : swimmer.Mood == Mood.Cruise && !swimmer.Kicking ? 0.02f : 0.07f;
        swimmer.Swing = Mathf.Lerp(swimmer.Swing, swing, 1f - Mathf.Exp(-3f * step));
    }

    // What it does next: wander, hang still, rise for a fly, come back from a bolt.
    private void Decide(Swimmer swimmer, Vector3 at, Vector3 player, float now, FishingSettings.SwimTuning tuning)
    {
        float left = Flat(swimmer.Target - at).magnitude;
        switch (swimmer.Mood)
        {
            case Mood.Cruise:
                if (now >= swimmer.NextRise && Flat(at - player).sqrMagnitude > 25f)
                {
                    StartRise(swimmer, at);
                }
                else if (left < 0.6f)
                {
                    if (Random.value < 0.4f)
                    {
                        swimmer.Mood = Mood.Hover;
                        swimmer.MoodUntil = now + Random.Range(1.5f, 4.5f);
                    }
                    else
                    {
                        Wander(swimmer);
                    }
                }

                break;
            case Mood.Hover:
                if (now >= swimmer.MoodUntil)
                {
                    Wander(swimmer);
                }

                break;
            case Mood.Rise:
                if (swimmer.Depth <= 0.08f)
                {
                    TakeFly(swimmer, at, tuning);
                }
                else if (now >= swimmer.MoodUntil)
                {
                    Wander(swimmer);
                }

                break;
            case Mood.Bolt:
                if (left < 0.8f || now >= swimmer.MoodUntil)
                {
                    Wander(swimmer);
                }

                break;
        }
    }

    // Up to the top, a little way ahead, to take a fly.
    private static void StartRise(Swimmer swimmer, Vector3 at)
    {
        Vector3 ahead = Quaternion.Euler(0f, swimmer.Yaw + Random.Range(-40f, 40f), 0f) * Vector3.forward * Random.Range(1.5f, 3f);
        swimmer.Mood = Mood.Rise;
        swimmer.MoodUntil = Time.time + 8f;
        swimmer.Target = at + ahead;
        swimmer.WantDepth = 0.04f;
    }

    // The take: rings spread from where its lips broke the film, a sip of sound, and down it goes.
    private void TakeFly(Swimmer swimmer, Vector3 at, FishingSettings.SwimTuning tuning)
    {
        Rises++;
        float size = swimmer.Length / 100f;
        if (swimmer.Water != null && swimmer.Water.TakesRipples)
        {
            Vector3 mouth = at + Quaternion.Euler(0f, swimmer.Yaw, 0f) * Vector3.forward * (size * 0.45f);
            WaterRipples.AddStamp(mouth, 0.1f + size * 0.25f, 0.1f + size * 0.12f);
        }

        if (tuning.riseSound != null)
        {
            AudioManager.Play(tuning.riseSound, at);
        }

        swimmer.NextRise = Time.time + Random.Range(tuning.riseEvery.x, tuning.riseEvery.y);
        Wander(swimmer);
        // A flick of the tail on the way down.
        swimmer.Speed = Mathf.Max(swimmer.Speed, swimmer.Cruise * 1.5f);
        swimmer.Kicking = true;
        swimmer.StrokeUntil = Time.time + 0.6f;
    }

    private static void Bolt(Swimmer swimmer, Vector3 from)
    {
        Vector3 away = Flat(swimmer.Body.position - from);
        away = away.sqrMagnitude > 0.01f ? away.normalized : Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
        swimmer.Mood = Mood.Bolt;
        swimmer.MoodUntil = Time.time + 3f;
        swimmer.Target = NewTarget(swimmer, swimmer.Body.position + away * Random.Range(5f, 9f));
        // Down, out of sight.
        swimmer.WantDepth = swimmer.Depth + 0.5f;
    }

    // Back to cruising, towards somewhere else in its own water and at the depth it likes there.
    private static void Wander(Swimmer swimmer)
    {
        swimmer.Mood = Mood.Cruise;
        swimmer.Target = NewTarget(swimmer, null);
        if (WaterSurface.At(swimmer.Target, 25f, out float surface))
        {
            swimmer.WantDepth = PreferredDepth(swimmer, new Vector3(swimmer.Target.x, surface, swimmer.Target.z));
        }
    }

    // Somewhere in its own open water: near <paramref name="wanted"/> when given, else a few metres off, mostly ahead so
    // the path flows on rather than doubling back.
    private static Vector3 NewTarget(Swimmer swimmer, Vector3? wanted)
    {
        Vector3 at = swimmer.Body.position;
        for (int i = 0; i < 8; i++)
        {
            Vector3 candidate;
            if (wanted.HasValue && i < 4)
            {
                Vector2 jitter = Random.insideUnitCircle * (1f + i);
                candidate = wanted.Value + new Vector3(jitter.x, 0f, jitter.y);
            }
            else
            {
                float turn = Random.Range(-100f, 100f) * (i < 5 ? 1f : 1.8f);
                candidate = at + Quaternion.Euler(0f, swimmer.Yaw + turn, 0f) * Vector3.forward * Random.Range(3f, 10f);
            }

            candidate.y = at.y;
            if (WaterSurface.OpenAt(candidate, 3f, out _, WaterDepth) == swimmer.Water)
            {
                return candidate;
            }
        }

        // Boxed in: turn round.
        return at - Quaternion.Euler(0f, swimmer.Yaw, 0f) * Vector3.forward * 3f;
    }

    // The body bends in a wave running back from the head, growing to the tail, and curves into a turn.
    private static void Bend(Swimmer swimmer)
    {
        Vector3[] shape = baseShape;
        Vector3[] bent = swimmer.Bent;
        float curve = Mathf.Clamp(swimmer.YawRate / 180f, -1f, 1f) * 0.12f;
        for (int i = 0; i < shape.Length; i++)
        {
            Vector3 v = shape[i];
            float along = Mathf.Clamp01((MeshHead - v.z) / MeshLength);
            float weight = along * along;
            // A little sway of the head against the tail, so the fish doesn't pivot on its nose.
            float wave = Mathf.Sin(swimmer.Beat - along * 3.2f) * (weight + 0.06f * (1f - along));
            v.x += swimmer.Swing * wave + curve * weight;
            bent[i] = v;
        }

        swimmer.Mesh.vertices = bent;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
