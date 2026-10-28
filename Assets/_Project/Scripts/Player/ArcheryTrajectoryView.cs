using UnityEngine;

/// <summary>
/// A faint line along the arc a drawn arrow would fly (PlayerArchery.TryGetShotPreview), ending where it would land. It
/// only shows for long shots, where the drop matters: it fades in between <see cref="fadeInFrom"/> and
/// <see cref="fullAt"/> metres to the aim point, and brightens with the draw. Shots stray a little, so it is a guide,
/// not a promise.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerArchery))]
public sealed class ArcheryTrajectoryView : MonoBehaviour
{
    private const int MaxPoints = 48;
    private const float StepSeconds = 0.05f;

    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color color = new Color(1f, 0.95f, 0.8f, 0.35f);
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
        while (count < MaxPoints)
        {
            Vector3 next = position + velocity * StepSeconds + Vector3.down * (0.5f * gravity * StepSeconds * StepSeconds);
            velocity += Vector3.down * (gravity * StepSeconds);
            Vector3 step = next - position;
            if (Physics.Raycast(position, step, out RaycastHit hit, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                points[count++] = hit.point;
                break;
            }

            points[count++] = next;
            position = next;
        }

        line.positionCount = count;
        line.SetPositions(points);
        Color faded = color;
        faded.a *= shown;
        line.startColor = faded;
        line.endColor = faded;
        line.enabled = true;
    }
}
