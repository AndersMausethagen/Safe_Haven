using UnityEngine;

/// <summary>
/// A floating health bar above a Damageable. It appears when the target is hurt, fades out when the fight goes quiet and
/// disappears on death. It only listens to the Damageable's events and never changes it. Put it under the target's
/// object on a world-space canvas; it follows the target, faces the camera, and ignores the parent's scale so the bar
/// keeps its shape on a stretched target. Over a creature's bar, the icons of the statuses at work on it (StatusEffectDefinition.Icon),
/// in the order laid; the bar stays up while any lasts.
/// </summary>
[OnAFriendsCopy(CopyRule.Same, "shows the health the host sends")]
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
    [Tooltip("Height of the poise bar under a monster's health, in canvas units.")]
    [SerializeField, Min(1f)] private float poiseHeight = 5f;

    // Status icons over the bar: canvas units each, and at most this many shown.
    private const float StatusIconSize = 36f;
    private const float StatusIconGap = 3f;
    private const int MaxStatusIcons = 4;

    private MonsterAI monster;
    private StatusEffects statuses;
    private bool showsStatuses;
    private RectTransform statusRow;
    private int statusIconsPlaced = -1;
    private readonly System.Collections.Generic.List<UnityEngine.UI.Image> statusIcons = new System.Collections.Generic.List<UnityEngine.UI.Image>();
    // Over each icon, a shade that sweeps round as the status runs out (the UX pass of 2026-11-24: how long it had left was unseen).
    private readonly System.Collections.Generic.List<UnityEngine.UI.Image> statusDrains = new System.Collections.Generic.List<UnityEngine.UI.Image>();
    private RectTransform poiseFill;
    private UnityEngine.UI.Graphic poiseGraphic;
    private GameObject poiseBar;
    // Pulses a reeling creature's poise bar this many times a second.
    private const float ReelingPulseRate = 2.2f;
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
            monster = target.GetComponent<MonsterAI>();
            if (monster != null)
            {
                BuildPoiseBar();
            }

            // The player's own are beside the vitals (PlayerConditionsView).
            showsStatuses = target.GetComponent<Player>() == null;
        }
    }

    // A thin strip under the health bar made of copies of its own parts, so every monster has one without touching its prefab.
    private void BuildPoiseBar()
    {
        var strip = new GameObject("Poise Bar", typeof(RectTransform));
        var rect = (RectTransform)strip.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, poiseHeight);
        rect.anchoredPosition = Vector2.down * PoiseGap;
        foreach (Transform part in fill.parent)
        {
            // The parts in place: everything but this strip, stretched over it as they stretch over the health bar.
            if (part != rect && part.TryGetComponent(out UnityEngine.UI.Graphic _))
            {
                Instantiate(part.gameObject, rect, false).name = part.name;
            }
        }

        poiseFill = rect.Find(fill.name) as RectTransform;
        poiseBar = strip;
        if (poiseFill == null)
        {
            Destroy(strip);
            return;
        }

        poiseGraphic = poiseFill.GetComponent<UnityEngine.UI.Graphic>();
        poiseGraphic.color = UIStyle.Current.Accent;
    }

    private const float PoiseGap = 3f;

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

        UpdateStatusIcons();
        float sinceChange = Time.time - lastChangeTime;
        float fade = Mathf.Clamp01((visibleSeconds + fadeSeconds - sinceChange) / fadeSeconds);
        group.alpha = dead ? 0f : fade;
        UpdatePoise();
    }

    private void UpdateStatusIcons()
    {
        if (!showsStatuses || dead)
        {
            return;
        }

        // Added by itself on the first status, so looked for until there.
        if (statuses == null && !target.TryGetComponent(out statuses))
        {
            return;
        }

        int shown = 0;
        var current = statuses.Current;
        for (int i = 0; i < current.Count && shown < MaxStatusIcons; i++)
        {
            StatusEffectDefinition effect = current[i].Definition;
            if (effect == null || effect.Icon == null)
            {
                continue;
            }

            UnityEngine.UI.Image icon = StatusIcon(shown);
            if (icon.sprite != effect.Icon)
            {
                icon.sprite = effect.Icon;
            }

            if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }

            float left = current[i].SecondsLeft;
            statusDrains[shown].fillAmount = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, Mathf.Max(effect.Seconds, left)));
            shown++;
        }

        for (int i = shown; i < statusIcons.Count; i++)
        {
            if (statusIcons[i].gameObject.activeSelf)
            {
                statusIcons[i].gameObject.SetActive(false);
            }
        }

        if (shown == 0)
        {
            statusIconsPlaced = 0;
            return;
        }

        // Centred over the bar (placed again only when how many changes); and the bar stays up while something is at work on it.
        if (shown != statusIconsPlaced)
        {
            statusIconsPlaced = shown;
            float width = shown * StatusIconSize + (shown - 1) * StatusIconGap;
            for (int i = 0; i < shown; i++)
            {
                ((RectTransform)statusIcons[i].transform).anchoredPosition = new Vector2(-width * 0.5f + i * (StatusIconSize + StatusIconGap), 0f);
            }
        }

        lastChangeTime = Mathf.Max(lastChangeTime, Time.time - visibleSeconds * 0.5f);
    }

    private UnityEngine.UI.Image StatusIcon(int index)
    {
        if (statusRow == null)
        {
            var row = new GameObject("Status Icons", typeof(RectTransform));
            statusRow = (RectTransform)row.transform;
            statusRow.SetParent(transform, false);
            statusRow.anchorMin = statusRow.anchorMax = new Vector2(0.5f, 1f);
            statusRow.pivot = new Vector2(0.5f, 0f);
            statusRow.sizeDelta = Vector2.zero;
            statusRow.anchoredPosition = Vector2.up * StatusIconGap;
        }

        while (statusIcons.Count <= index)
        {
            var icon = new GameObject("Status Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)icon.transform;
            rect.SetParent(statusRow, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(StatusIconSize, StatusIconSize);
            var image = icon.GetComponent<UnityEngine.UI.Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            // A thin dark edge, so a dark-red bleed still reads over the ground.
            var edge = icon.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.7f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            statusIcons.Add(image);

            var drain = new GameObject("Drain", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var drainRect = (RectTransform)drain.transform;
            drainRect.SetParent(rect, false);
            drainRect.anchorMin = Vector2.zero;
            drainRect.anchorMax = Vector2.one;
            drainRect.offsetMin = drainRect.offsetMax = Vector2.zero;
            var shade = drain.GetComponent<UnityEngine.UI.Image>();
            shade.sprite = UISprites.Disc;
            shade.color = new Color(0f, 0f, 0f, 0.55f);
            shade.type = UnityEngine.UI.Image.Type.Filled;
            shade.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            shade.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            shade.fillClockwise = true;
            shade.fillAmount = 0f;
            shade.raycastTarget = false;
            statusDrains.Add(shade);
        }

        return statusIcons[index];
    }

    private void UpdatePoise()
    {
        if (poiseBar == null || monster == null)
        {
            return;
        }

        bool shown = GameOptions.PoiseBars.On && monster.Poise != null;
        if (poiseBar.activeSelf != shown)
        {
            poiseBar.SetActive(shown);
        }

        if (shown && poiseFill != null)
        {
            poiseFill.anchorMax = new Vector2(monster.Poise.Fraction, 1f);
            // Reeling (poise worn low, a stagger near): the bar pulses towards the reeling colour.
            if (poiseGraphic != null)
            {
                float pulse = monster.IsReeling ? 0.5f + 0.5f * Mathf.Sin(Time.time * ReelingPulseRate * Mathf.PI * 2f) : 0f;
                poiseGraphic.color = Color.Lerp(UIStyle.Current.Accent, CombatFeelSettings.Current.ReelingBarColor, pulse);
            }
        }
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
