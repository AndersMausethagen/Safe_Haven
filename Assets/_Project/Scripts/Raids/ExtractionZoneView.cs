using UnityEngine;

/// <summary>
/// Draws an extraction point's circle on the ground: a thin ring at its hold radius, following the terrain, that fills
/// clockwise with the way home as it is held, and a light that wakes when the beacon is lit. Colours tell the state:
/// dim while unlit, pale gold while waiting for the player, green while held, red while a creature contests it. A lit
/// beacon's light also glows in the fog (AtmosphereLight) and lifts the night veil over its circle. Only
/// presentation; ExtractionPoint adds it and owns the rules. Uses the attack telegraph's material (Combat Feel). Built once
/// when the raid starts, after the map is turned, so ground heights match the turned terrain.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ExtractionPoint))]
public sealed class ExtractionZoneView : MonoBehaviour
{
    private const int Segments = 72;
    private const float RingWidth = 0.22f;
    private const float FillWidth = 0.5f;
    private const float GroundOffset = 0.06f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color unlitColor = new Color(0.75f, 0.85f, 0.75f, 0.25f);
    [SerializeField] private Color waitingColor = new Color(1f, 0.85f, 0.45f, 0.75f);
    [SerializeField] private Color holdingColor = new Color(0.55f, 0.95f, 0.55f, 0.9f);
    [SerializeField] private Color contestedColor = new Color(1f, 0.32f, 0.25f, 0.9f);
    [SerializeField] private Color closedColor = new Color(0.4f, 0.4f, 0.45f, 0.15f);
    [Tooltip("Intensity of the beacon light while lit; it is off before.")]
    [SerializeField, Min(0f)] private float lightIntensity = 4f;
    [SerializeField, Min(0f)] private float lightRange = 9f;

    private ExtractionPoint point;
    private MeshRenderer ringRenderer;
    private MeshRenderer fillRenderer;
    private MeshFilter fillFilter;
    private Mesh fillMesh;
    private Light beacon;
    private VisibilitySource beaconSight;
    private MaterialPropertyBlock block;
    private float[] groundHeights;
    private int shownSteps = -1;

    private void Awake()
    {
        point = GetComponent<ExtractionPoint>();
    }

    private void Start()
    {
        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null)
        {
            Debug.LogWarning("Combat Feel has no telegraph material, so extraction circles aren't drawn.", this);
            enabled = false;
            return;
        }

        block = new MaterialPropertyBlock();
        SampleGround();
        ringRenderer = CreatePart("Extraction Ring", material, out MeshFilter ringFilter);
        ringFilter.sharedMesh = BuildArc(1f, point.HoldRadius - RingWidth, point.HoldRadius);
        fillRenderer = CreatePart("Extraction Fill", material, out fillFilter);
        fillMesh = new Mesh { name = "Extraction Fill" };
        fillMesh.MarkDynamic();
        fillFilter.sharedMesh = fillMesh;

        var lightObject = new GameObject("Beacon Light");
        lightObject.transform.SetParent(transform, false);
        Unscale(lightObject.transform);
        lightObject.transform.position = transform.position + Vector3.up * 2.5f;
        beacon = lightObject.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = waitingColor;
        beacon.range = lightRange;
        beacon.intensity = 0f;
        beacon.shadows = LightShadows.None;
        // A lit beacon glows in the fog and keeps the night veil off its circle.
        lightObject.AddComponent<AtmosphereLight>();
        beaconSight = lightObject.AddComponent<VisibilitySource>();
        beaconSight.Configure(point.HoldRadius + 3f, 0.4f, false, 0f);
        beaconSight.enabled = false;
    }

    private void OnDestroy()
    {
        // Leaving play mode destroys the scene after the editor is already out of it.
        if (fillMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(fillMesh);
            }
            else
            {
                DestroyImmediate(fillMesh);
            }
        }
    }

    private void LateUpdate()
    {
        if (ringRenderer == null)
        {
            return;
        }

        Color color = ColorFor(point);
        Tint(ringRenderer, color);
        Tint(fillRenderer, color);

        // Rebuilt per percent, not per frame.
        int steps = point.IsLit ? Mathf.RoundToInt(point.HoldFraction * 100f) : 0;
        if (steps != shownSteps)
        {
            shownSteps = steps;
            BuildArc(steps / 100f, point.HoldRadius - FillWidth, point.HoldRadius, fillMesh);
            fillRenderer.enabled = steps > 0;
        }

        float pulse = point.HoldState == ExtractionHoldState.Contested ? 0.6f + 0.4f * Mathf.Sin(Time.time * 10f) : 1f;
        float target = point.IsLit ? lightIntensity * pulse : 0f;
        beacon.intensity = Mathf.MoveTowards(beacon.intensity, target, lightIntensity * 2f * Time.deltaTime);
        beacon.color = new Color(color.r, color.g, color.b, 1f);
        beaconSight.enabled = point.IsLit;
    }

    private Color ColorFor(ExtractionPoint extraction)
    {
        if (!extraction.IsOpen)
        {
            return closedColor;
        }

        switch (extraction.HoldState)
        {
            case ExtractionHoldState.Holding:
            case ExtractionHoldState.Done:
                return holdingColor;
            case ExtractionHoldState.Contested:
                return contestedColor;
            case ExtractionHoldState.Waiting:
                return waitingColor;
            default:
                return unlitColor;
        }
    }

    private void Tint(Renderer target, Color color)
    {
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        target.SetPropertyBlock(block);
    }

    private MeshRenderer CreatePart(string partName, Material material, out MeshFilter filter)
    {
        var part = new GameObject(partName);
        part.transform.SetParent(transform, false);
        Unscale(part.transform);
        filter = part.AddComponent<MeshFilter>();
        var meshRenderer = part.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return meshRenderer;
    }

    // Ground height under each segment of the circle (local), so the ring lies on slopes instead of cutting into them.
    private void SampleGround()
    {
        groundHeights = new float[Segments + 1];
        float radius = point.HoldRadius - RingWidth * 0.5f;
        for (int i = 0; i <= Segments; i++)
        {
            Vector3 world = transform.position + transform.rotation * Direction(i / (float)Segments) * radius + Vector3.up * 4f;
            groundHeights[i] = Physics.Raycast(world, Vector3.down, out RaycastHit hit, 12f, 1, QueryTriggerInteraction.Ignore)
                ? hit.point.y - transform.position.y
                : 0f;
        }
    }

    // A flat band between two radii, from 12 o'clock clockwise for the given share of the circle.
    private Mesh BuildArc(float share, float inner, float outer, Mesh mesh = null)
    {
        mesh = mesh != null ? mesh : new Mesh { name = "Extraction Ring" };
        mesh.Clear();
        int count = Mathf.CeilToInt(Segments * Mathf.Clamp01(share));
        if (count <= 0)
        {
            return mesh;
        }

        var vertices = new Vector3[(count + 1) * 2];
        var triangles = new int[count * 6];
        for (int i = 0; i <= count; i++)
        {
            float t = Mathf.Min(i / (float)Segments, share);
            Vector3 direction = Direction(t);
            float height = groundHeights[Mathf.Clamp(Mathf.RoundToInt(t * Segments), 0, Segments)] + GroundOffset;
            vertices[i * 2] = direction * inner + Vector3.up * height;
            vertices[i * 2 + 1] = direction * outer + Vector3.up * height;
            if (i < count)
            {
                int v = i * 2;
                int k = i * 6;
                triangles[k] = v;
                triangles[k + 1] = v + 1;
                triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 2;
                triangles[k + 4] = v + 1;
                triangles[k + 5] = v + 3;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // The circle is measured in metres whatever the point's own scale (a scaled beacon model).
    private void Unscale(Transform part)
    {
        Vector3 scale = transform.lossyScale;
        part.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }

    private static Vector3 Direction(float share)
    {
        float angle = share * Mathf.PI * 2f;
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
    }
}
