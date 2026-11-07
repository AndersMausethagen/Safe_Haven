using UnityEngine;

/// <summary>
/// The float and the line while fishing: a red-and-white float that arcs out from the rod tip, bobs on the water, goes
/// under at a bite and is drawn in while reeling, with a line to it that sags until it is taut. Made the first time a line
/// is cast. Plain C#, owned by <see cref="PlayerFishing"/>, which says where the float is meant to be.
/// </summary>
public sealed class FishingLine
{
    /// <summary>How long a cast float is in the air.</summary>
    public const float FlightSeconds = 0.45f;

    private const int Points = 12;

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
    /// Places the float and draws the line: in flight until <paramref name="landsAt"/>, then on the water at
    /// <paramref name="landing"/>, under at a bite and drawn towards the bank (<paramref name="castFrom"/> at
    /// <paramref name="waterHeight"/>) as the reel's catch fills.
    /// </summary>
    public void Tick(Vector3 landing, float landsAt, PlayerFishing.Phase phase, ReelSession reel, Vector3 castFrom, float waterHeight)
    {
        if (bobber == null || !bobber.gameObject.activeSelf)
        {
            return;
        }

        Vector3 tip = angler.position + Vector3.up * 1.7f + angler.forward * 0.7f + angler.right * 0.25f;
        Vector3 at;
        if (Time.time < landsAt)
        {
            // In flight: an arc from the rod tip to where it lands.
            float t = 1f - (landsAt - Time.time) / FlightSeconds;
            at = Vector3.Lerp(tip, landing, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2f;
        }
        else
        {
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.025f;
            float under = phase == PlayerFishing.Phase.Bite ? -0.18f : phase == PlayerFishing.Phase.Reeling ? -0.06f + Mathf.Sin(Time.time * 17f) * 0.04f : 0f;
            Vector3 bank = new Vector3(castFrom.x, waterHeight, castFrom.z);
            Vector3 drawn = phase == PlayerFishing.Phase.Reeling && reel != null ? Vector3.Lerp(landing, bank, reel.Catch * 0.6f) : landing;
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
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.name = "Fishing Float";
        body.transform.localScale = Vector3.one * 0.14f;
        body.GetComponent<Renderer>().material.color = new Color(0.85f, 0.15f, 0.1f);
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(body.transform, false);
        cap.transform.localPosition = Vector3.up * 0.45f;
        cap.transform.localScale = Vector3.one * 0.6f;
        cap.GetComponent<Renderer>().material.color = Color.white;
        bobber = body.transform;

        line = new GameObject("Fishing Line").AddComponent<LineRenderer>();
        line.positionCount = Points;
        line.widthMultiplier = 0.012f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(0.9f, 0.9f, 0.85f, 0.8f);
    }
}
