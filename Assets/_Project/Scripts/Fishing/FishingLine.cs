using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a line was cast: from where the player stood to the spot on the water, and when the float lands there.</summary>
public readonly struct FishingCast
{
    public readonly Vector3 From;
    public readonly Vector3 Landing;
    public readonly float WaterHeight;
    public readonly float LandsAt;

    public FishingCast(Vector3 from, Vector3 landing, float waterHeight, float landsAt)
    {
        From = from;
        Landing = landing;
        WaterHeight = waterHeight;
        LandsAt = landsAt;
    }
}

/// <summary>
/// The float and the line while fishing: the float arcs out from the rod tip, bobs on the water and goes under at a bite;
/// with a fish on it rides where the fish is (jerking while it thrashes), and the line goes taut and reddens as it nears
/// breaking; at the landing the fish comes up out of the water and hangs from the tip. What they look like is
/// FishingSettings' (Float and Line, the splash, the hooked fish's material); how they move is here. The hooked fish is a
/// plain shape made in code, sized by its weight and tinted by its kind. Made the first time a line is cast. Plain C#,
/// owned by <see cref="PlayerFishing"/>, which says where the line was cast and where the fish is; a friend's line in
/// co-op is drawn by the same view on their body (RemoteFishing).
/// </summary>
public sealed class FishingLine
{
    /// <summary>How long a cast float is in the air.</summary>
    public const float FlightSeconds = 0.45f;

    private const int Points = 12;
    // The size of the plain sphere used when the settings name no float.
    private const float PlainFloatSize = 0.14f;
    // How far below the tip a landed fish hangs, in metres of line.
    private const float HangLength = 0.5f;
    private static readonly Color Straining = new Color(0.95f, 0.32f, 0.22f, 0.95f);
    private static Mesh fishMesh;

    private readonly Transform angler;
    private HeldItemView held;
    private GameObject tipOf;
    private MeshFilter tipFilter;
    private Transform bobber;
    private LineRenderer line;
    private Transform fish;
    private MeshRenderer fishRenderer;
    private MaterialPropertyBlock fishColour;
    private Vector3 lastWater;
    private float lastSplash = -10f;

    public FishingLine(Transform angler)
    {
        this.angler = angler;
    }

    /// <summary>The float, or null before the first cast.</summary>
    public Transform Bobber => bobber;

    /// <summary>The landed fish while it hangs from the line, else null.</summary>
    public Transform Fish => fish != null && fish.gameObject.activeSelf ? fish : null;

    public void Show()
    {
        if (bobber == null)
        {
            Build();
        }

        bobber.gameObject.SetActive(true);
        line.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (bobber != null) bobber.gameObject.SetActive(false);
        if (line != null) line.gameObject.SetActive(false);
        if (fish != null) fish.gameObject.SetActive(false);
    }

    /// <summary>
    /// Places the float and draws the line: in flight until the cast lands, then on the water, under at a bite and, for a
    /// friend's line whose fight is only told as the reel's progress (0 to 1), drawn towards the bank.
    /// </summary>
    public void Tick(in FishingCast cast, PlayerFishing.Phase phase, float reelCatch)
    {
        if (bobber == null || !bobber.gameObject.activeSelf)
        {
            return;
        }

        Vector3 tip = RodTip();
        Vector3 at;
        if (Time.time < cast.LandsAt)
        {
            // In flight: an arc from the rod tip to where it lands.
            float t = Mathf.Clamp01(1f - (cast.LandsAt - Time.time) / FlightSeconds);
            at = Vector3.Lerp(tip, cast.Landing, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2f;
        }
        else
        {
            bool fighting = phase == PlayerFishing.Phase.Reeling || phase == PlayerFishing.Phase.Landing;
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.025f;
            float under = phase == PlayerFishing.Phase.Bite ? -0.18f : fighting ? -0.06f + Mathf.Sin(Time.time * 17f) * 0.04f : 0f;
            at = (fighting ? Drawn(cast, reelCatch) : cast.Landing) + Vector3.up * (bob + under);
        }

        lastWater = at;
        bool taut = phase == PlayerFishing.Phase.Reeling || phase == PlayerFishing.Phase.Bite;
        Place(tip, at, taut ? 0.05f : 0.6f, 0f);
    }

    /// <summary>A hooked fish at <paramref name="at"/> on the water: the float rides on it, under and jerking while it
    /// thrashes; the line sags while slack and goes straight, then red, towards breaking (<paramref name="tension"/>, 1 at
    /// breaking point).</summary>
    public void Hooked(Vector3 at, float tension, bool thrashing)
    {
        if (bobber == null || !bobber.gameObject.activeSelf)
        {
            return;
        }

        lastWater = at;
        float jerk = thrashing ? Mathf.Sin(Time.time * 23f) * 0.06f + Mathf.Sin(Time.time * 37f) * 0.03f : Mathf.Sin(Time.time * 2.4f) * 0.02f;
        float under = Mathf.Lerp(-0.02f, -0.12f, Mathf.Clamp01(tension)) + jerk;
        Place(RodTip(), at + Vector3.up * under, Mathf.Lerp(0.5f, 0f, Mathf.Clamp01(tension * 1.6f)), tension);
    }

    /// <summary>
    /// The landing: the fish comes up out of the water where it was (<paramref name="lift"/> 0 to 1) to hang from the
    /// rod tip, head up on the hook; <paramref name="length"/> metres long, in its kind's <paramref name="colour"/>.
    /// </summary>
    public void Landing(float lift, float length, Color colour)
    {
        if (bobber == null || !bobber.gameObject.activeSelf)
        {
            return;
        }

        Vector3 tip = RodTip();
        float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lift));
        Vector3 hook = Vector3.Lerp(lastWater, tip + Vector3.down * HangLength, up);
        Place(tip, hook, 0f, 0.4f);

        ShowFish(length, colour);
        Vector3 ahead = Flat(angler.forward).sqrMagnitude > 0.01f ? Flat(angler.forward).normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, ahead);
        // Swinging on the line and twisting as it flaps, more once it is out of the water.
        float swing = Mathf.Sin(Time.time * 3.1f) * 12f * up;
        float flap = Mathf.Sin(Time.time * 17f) * 25f * up;
        Quaternion hang = Quaternion.AngleAxis(swing, side) * Quaternion.LookRotation(Vector3.up, -ahead) * Quaternion.Euler(0f, 0f, flap);
        fish.SetPositionAndRotation(hook - hang * Vector3.forward * (0.5f * length), hang);
        fish.gameObject.SetActive(up > 0.05f);
    }

    /// <summary>Throws up the water at <paramref name="at"/> (a fish running, or coming out), at most every so often.</summary>
    public void Splash(Vector3 at, float minimumGap = 0.35f)
    {
        GameObject effect = FishingSettings.Current.SplashEffect;
        if (effect == null || Time.time - lastSplash < minimumGap)
        {
            return;
        }

        lastSplash = Time.time;
        Object.Destroy(Object.Instantiate(effect, at, Quaternion.identity), 3f);
    }

    /// <summary>Where a friend's fish is when only the reel's progress is told: drawn from the landing towards the bank.</summary>
    public static Vector3 Drawn(in FishingCast cast, float reelCatch)
    {
        Vector3 bank = new Vector3(cast.From.x, cast.WaterHeight, cast.From.z);
        return Vector3.Lerp(cast.Landing, bank, Mathf.Clamp01(reelCatch) * 0.6f);
    }

    /// <summary>Where the line leaves the rod: the far end of the rod in the angler's hand (HeldItemView), the end of its
    /// model's longest side away from the grip. A fixed spot by the shoulder only while no rod is shown.</summary>
    public Vector3 RodTip()
    {
        held ??= angler.GetComponent<HeldItemView>();
        GameObject rod = held != null ? held.ShownModel : null;
        if (rod == null)
        {
            return angler.position + Vector3.up * 1.7f + angler.forward * 0.7f + angler.right * 0.25f;
        }

        if (rod != tipOf)
        {
            tipOf = rod;
            tipFilter = rod.GetComponentInChildren<MeshFilter>();
        }

        if (tipFilter == null || tipFilter.sharedMesh == null)
        {
            return rod.transform.position;
        }

        // The rod's length runs along its mesh's longest side; the tip is the end of it farther from the hand.
        Bounds local = tipFilter.sharedMesh.bounds;
        Vector3 size = local.size;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 half = Vector3.zero;
        half[axis] = local.extents[axis];
        Transform mesh = tipFilter.transform;
        Vector3 one = mesh.TransformPoint(local.center + half);
        Vector3 other = mesh.TransformPoint(local.center - half);
        Vector3 hand = held.Grip != null ? held.Grip.position : rod.transform.position;
        return (one - hand).sqrMagnitude >= (other - hand).sqrMagnitude ? one : other;
    }

    public void Destroy()
    {
        if (bobber != null) Object.Destroy(bobber.gameObject);
        if (line != null) Object.Destroy(line.gameObject);
        if (fish != null) Object.Destroy(fish.gameObject);
    }

    private void Place(Vector3 tip, Vector3 at, float sag, float tension)
    {
        bobber.position = at;
        Color colour = Color.Lerp(FishingSettings.Current.LineColour, Straining, Mathf.InverseLerp(0.8f, 1f, tension));
        line.startColor = line.endColor = colour;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (line.positionCount - 1f);
            line.SetPosition(i, Vector3.Lerp(tip, at, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag);
        }
    }

    private void Build()
    {
        FishingSettings settings = FishingSettings.Current;
        if (settings.FloatModel != null)
        {
            bobber = Object.Instantiate(settings.FloatModel).transform;
        }
        else
        {
            GameObject plain = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(plain.GetComponent<Collider>());
            plain.transform.localScale = Vector3.one * PlainFloatSize;
            bobber = plain.transform;
        }

        bobber.name = "Fishing Float";
        line = new GameObject("Fishing Line").AddComponent<LineRenderer>();
        line.positionCount = Points;
        line.widthMultiplier = settings.LineWidth;
        line.sharedMaterial = settings.LineMaterial;
        line.startColor = line.endColor = settings.LineColour;
    }

    private void ShowFish(float length, Color colour)
    {
        if (fish == null)
        {
            var go = new GameObject("Hooked Fish");
            go.AddComponent<MeshFilter>().sharedMesh = FishMesh();
            fishRenderer = go.AddComponent<MeshRenderer>();
            fishRenderer.sharedMaterial = FishingSettings.Current.FishMaterial;
            fishColour = new MaterialPropertyBlock();
            fish = go.transform;
            fish.gameObject.SetActive(false);
        }

        fish.localScale = Vector3.one * Mathf.Max(0.1f, length);
        fishColour.SetColor("_BaseColor", colour);
        fishRenderer.SetPropertyBlock(fishColour);
    }

    // A plain fish one metre long along +z (mouth at +z, tail at -z), deeper than it is wide, with a forked tail and a
    // fin on its back. Made once and shared.
    /// <summary>The fish shape (one metre long along +z, mouth forward), shared with the fish swimming in the water (FishInTheWater).</summary>
    public static Mesh SharedFishMesh => FishMesh();

    private static Mesh FishMesh()
    {
        if (fishMesh != null)
        {
            return fishMesh;
        }

        const int rings = 10;
        const int around = 10;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;
            float z = Mathf.Lerp(-0.4f, 0.5f, t);
            // Fullest a little ahead of the middle, narrow at the wrist before the tail.
            float radius = 0.13f * Mathf.Pow(Mathf.Sin(Mathf.Lerp(0.12f, 1f, t) * Mathf.PI), 0.8f) + 0.012f;
            for (int a = 0; a < around; a++)
            {
                float angle = a / (float)around * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius * 0.45f, Mathf.Sin(angle) * radius, z));
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int a = 0; a < around; a++)
            {
                int i0 = r * around + a, i1 = r * around + (a + 1) % around, j0 = i0 + around, j1 = i1 + around;
                triangles.AddRange(new[] { i0, j0, i1, i1, j0, j1 });
            }
        }

        int mouth = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, 0.52f));
        int lastRing = rings * around;
        for (int a = 0; a < around; a++)
        {
            triangles.AddRange(new[] { lastRing + a, mouth, lastRing + (a + 1) % around });
        }

        int wrist = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -0.42f));
        for (int a = 0; a < around; a++)
        {
            triangles.AddRange(new[] { (a + 1) % around, wrist, a });
        }

        // The tail and the back fin are thin blades, drawn from both sides.
        int tail = vertices.Count;
        vertices.Add(new Vector3(0f, 0.17f, -0.62f));
        vertices.Add(new Vector3(0f, 0f, -0.52f));
        vertices.Add(new Vector3(0f, -0.15f, -0.6f));
        triangles.AddRange(new[] { wrist, tail, tail + 1, wrist, tail + 1, tail, wrist, tail + 1, tail + 2, wrist, tail + 2, tail + 1 });
        int fin = vertices.Count;
        vertices.Add(new Vector3(0f, 0.13f, 0.12f));
        vertices.Add(new Vector3(0f, 0.21f, -0.05f));
        vertices.Add(new Vector3(0f, 0.11f, -0.16f));
        triangles.AddRange(new[] { fin, fin + 1, fin + 2, fin, fin + 2, fin + 1 });

        fishMesh = new Mesh { name = "Hooked Fish" };
        fishMesh.SetVertices(vertices);
        fishMesh.SetTriangles(triangles, 0);
        fishMesh.RecalculateNormals();
        fishMesh.RecalculateBounds();
        return fishMesh;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
