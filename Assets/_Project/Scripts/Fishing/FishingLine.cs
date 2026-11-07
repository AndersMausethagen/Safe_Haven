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
/// The float and the line while fishing: the float arcs out from the rod tip, bobs on the water, goes under at a bite and
/// is drawn in while reeling, with a line to it that sags until it is taut. What they look like is FishingSettings'
/// (Float and Line); how they move is here. Made the first time a line is cast. Plain C#, owned by
/// <see cref="PlayerFishing"/>, which says where the line was cast; a friend's line in co-op is drawn by the same view
/// on their body (RemoteFishing).
/// </summary>
public sealed class FishingLine
{
    /// <summary>How long a cast float is in the air.</summary>
    public const float FlightSeconds = 0.45f;

    private const int Points = 12;
    // The size of the plain sphere used when the settings name no float.
    private const float PlainFloatSize = 0.14f;

    private readonly Transform angler;
    private Transform bobber;
    private LineRenderer line;

    public FishingLine(Transform angler)
    {
        this.angler = angler;
    }

    /// <summary>The float, or null before the first cast.</summary>
    public Transform Bobber => bobber;

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
    }

    /// <summary>
    /// Places the float and draws the line: in flight until the cast lands, then on the water, under at a bite and drawn
    /// towards the bank as the reel's catch (0 to 1) fills.
    /// </summary>
    public void Tick(in FishingCast cast, PlayerFishing.Phase phase, float reelCatch)
    {
        if (bobber == null || !bobber.gameObject.activeSelf)
        {
            return;
        }

        Vector3 tip = angler.position + Vector3.up * 1.7f + angler.forward * 0.7f + angler.right * 0.25f;
        Vector3 at;
        if (Time.time < cast.LandsAt)
        {
            // In flight: an arc from the rod tip to where it lands.
            float t = 1f - (cast.LandsAt - Time.time) / FlightSeconds;
            at = Vector3.Lerp(tip, cast.Landing, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2f;
        }
        else
        {
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.025f;
            float under = phase == PlayerFishing.Phase.Bite ? -0.18f : phase == PlayerFishing.Phase.Reeling ? -0.06f + Mathf.Sin(Time.time * 17f) * 0.04f : 0f;
            Vector3 bank = new Vector3(cast.From.x, cast.WaterHeight, cast.From.z);
            Vector3 drawn = phase == PlayerFishing.Phase.Reeling ? Vector3.Lerp(cast.Landing, bank, Mathf.Clamp01(reelCatch) * 0.6f) : cast.Landing;
            at = drawn + Vector3.up * (bob + under);
        }

        bobber.position = at;
        float sag = phase == PlayerFishing.Phase.Reeling || phase == PlayerFishing.Phase.Bite ? 0.05f : 0.6f;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (line.positionCount - 1f);
            line.SetPosition(i, Vector3.Lerp(tip, at, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag);
        }
    }

    public void Destroy()
    {
        if (bobber != null) Object.Destroy(bobber.gameObject);
        if (line != null) Object.Destroy(line.gameObject);
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
}
