using UnityEngine;

/// <summary>
/// A floating health bar above a Damageable. It appears when the target is hurt, fades out when the fight goes quiet and
/// disappears on death. It only listens to the Damageable's events and never changes it. Put it under the target's
/// object on a world-space canvas; it follows the target, faces the camera, and ignores the parent's scale so the bar
/// keeps its shape on a stretched target.
/// </summary>
public sealed class WorldHealthBar : MonoBehaviour
{
    [Tooltip("Left empty, the Damageable on a parent is used.")]
    [SerializeField] private Damageable target;
    [Tooltip("Its right edge is anchored to show the health fraction.")]
    [SerializeField] private RectTransform fill;
    [SerializeField] private CanvasGroup group;
    [Tooltip("Height above the target's pivot, in meters.")]
    [SerializeField] private float height = 2.4f;
    [Tooltip("Meters per canvas unit. 0.01 makes a 120 unit wide bar 1.2 m wide.")]
    [SerializeField, Min(0.0001f)] private float unitSize = 0.01f;
    [SerializeField, Min(0f)] private float visibleSeconds = 5f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;

    private Camera viewCamera;
    private float lastChangeTime = float.NegativeInfinity;
    private bool dead;
    private bool started;

    private void Awake()
    {
        target = target != null ? target : GetComponentInParent<Damageable>();
        group.alpha = 0f;
        ApplyScale();
    }

    private void OnEnable()
    {
        if (target == null)
        {
            Debug.LogWarning("WorldHealthBar has no Damageable on it or a parent.", this);
            return;
        }

        target.HealthChanged += OnHealthChanged;
        target.Died += OnDied;

        // On the first enable the target may not have run Awake yet; Start reads its health once every Awake is done.
        if (started)
        {
            SetFill(target.HealthFraction);
        }
    }

    private void Start()
    {
        started = true;
        if (target != null)
        {
            SetFill(target.HealthFraction);
        }
    }

    private void OnDisable()
    {
        if (target != null)
        {
            target.HealthChanged -= OnHealthChanged;
            target.Died -= OnDied;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        viewCamera = viewCamera != null ? viewCamera : Camera.main;

        Transform anchor = target.transform;
        transform.position = anchor.position + Vector3.up * height;
        if (viewCamera != null)
        {
            transform.rotation = viewCamera.transform.rotation;
        }

        ApplyScale();

        float sinceChange = Time.time - lastChangeTime;
        float fade = Mathf.Clamp01((visibleSeconds + fadeSeconds - sinceChange) / fadeSeconds);
        group.alpha = dead ? 0f : fade;
    }

    // Canvas units are pixels; this keeps the bar its intended size in meters whatever the parent's scale.
    private void ApplyScale()
    {
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            unitSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            unitSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            unitSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    private void OnHealthChanged(float current, float max)
    {
        lastChangeTime = Time.time;
        SetFill(max > 0f ? current / max : 0f);
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        dead = true;
    }

    private void SetFill(float fraction)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }
}
