using UnityEngine;

/// <summary>
/// A line along the arc a drawn arrow would fly (PlayerArchery.TryGetShotPreview), with a ring where it would come down on the
/// ground: the high camera makes a shot hard to judge otherwise. It fades in between <see cref="fadeInFrom"/> and
/// <see cref="fullAt"/> metres to the aim point and brightens with the draw, and turns gold while letting go would be a perfect
/// release. A partial draw's arc falls short, as the shot would. Shots stray a little, so it is a guide, not a promise.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerArchery))]
public sealed class ArcheryTrajectoryView : MonoBehaviour
{
    private const int MaxPoints = 48;
    private const float StepSeconds = 0.05f;

    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color color = new Color(1f, 0.95f, 0.8f, 0.35f);
    [Tooltip("Its colour while letting go would be a perfect release.")]
    [SerializeField] private Color perfectColor = new Color(1f, 0.78f, 0.32f, 0.95f);
    [Tooltip("Radius in metres of the ring where the shot comes down on the ground. 0: none.")]
    [SerializeField, Min(0f)] private float landingRadius = 0.4f;
    [SerializeField, Min(0.001f)] private float width = 0.03f;
    [Tooltip("Aim points nearer than this get no line.")]
    [SerializeField, Min(0f)] private float fadeInFrom = 10f;
    [Tooltip("From this far the line is fully shown.")]
    [SerializeField, Min(0f)] private float fullAt = 18f;

    private readonly Vector3[] points = new Vector3[MaxPoints];
    private PlayerArchery archery;
    private LineRenderer line;

    private void Awake()
    {
        archery = GetComponent<PlayerArchery>();
        var holder = new GameObject("Arrow Trajectory") { hideFlags = HideFlags.DontSave };
        holder.transform.SetParent(transform, false);
        line = holder.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock landingBlock;

    private void OnDisable()
    {
        if (line != null)
        {
            line.enabled = false;
        }
    }

    private void LateUpdate()
    {
        float distance = Vector3.Distance(archery.Origin, archery.AimPoint);
        float shown = Mathf.InverseLerp(fadeInFrom, Mathf.Max(fadeInFrom + 0.1f, fullAt), distance) * archery.Draw;
        if (shown <= 0.01f || !archery.TryGetShotPreview(out Vector3 position, out Vector3 velocity, out float gravity))
        {
            line.enabled = false;
            return;
        }

        int count = 1;
        points[0] = position;
        bool landed = false;
        Vector3 landing = Vector3.zero;
        while (count < MaxPoints)
        {
            Vector3 next = position + velocity * StepSeconds + Vector3.down * (0.5f * gravity * StepSeconds * StepSeconds);
            velocity += Vector3.down * (gravity * StepSeconds);
            Vector3 step = next - position;
            if (Physics.Raycast(position, step, out RaycastHit hit, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                points[count++] = hit.point;
                // Only on the ground: a body it would strike has its own outline.
                landed = hit.normal.y > 0.6f;
                landing = hit.point;
                break;
            }

            points[count++] = next;
            position = next;
        }

        line.positionCount = count;
        line.SetPositions(points);
        Color faded = archery.InPerfectWindow ? perfectColor : color;
        faded.a *= archery.InPerfectWindow ? 1f : shown;
        line.startColor = faded;
        line.endColor = faded;
        line.enabled = true;
        if (landed)
        {
            DrawLanding(landing, faded);
        }
    }

    // Drawn with Graphics.RenderMesh, for the reason given in SlashEffect.
    private void DrawLanding(Vector3 at, Color tint)
    {
        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null || landingRadius <= 0f)
        {
            return;
        }

        landingBlock ??= new MaterialPropertyBlock();
        landingBlock.SetColor(BaseColorId, tint);
        landingBlock.SetColor(ColorId, tint);
        at += Vector3.up * 0.05f;
        var draw = new RenderParams(material)
        {
            matProps = landingBlock,
            layer = gameObject.layer,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(at, Vector3.one * (landingRadius * 2f + 1f)),
        };
        Graphics.RenderMesh(draw, AttackTelegraph.FanFor(360f), 0, Matrix4x4.TRS(at, Quaternion.identity, Vector3.one * landingRadius));
    }
}
