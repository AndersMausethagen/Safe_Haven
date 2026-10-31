using UnityEngine;
using UnityEngine.Events;

/// <summary>A node that knows its own build better than its bounds do, such as a tree, whose bounds are its crown.</summary>
public interface IHarvestBody
{
    /// <summary>Where a tool swung from this point lands.</summary>
    Vector3 HitPoint(Vector3 from);

    /// <summary>How big it is for its sounds (see AudioManager.Play). 1 is the size they were made for.</summary>
    float HarvestSize { get; }
}

/// <summary>
/// Plays a gathering node's feedback: sound, particles and a rock of the model on every swing, a shrink-and-sink break
/// when it is harvested and a pop when it grows back. What plays comes from the node's HarvestFeedbackProfile (picked on
/// its GatheringNodeDefinition), falling back to the Default profile in Gathering Minigame settings.
/// <para>
/// Where it belongs: Gathering presentation. ResourceNode adds it on the fly, so no setup is needed. Add it to a node
/// prefab yourself only to hook one-off reactions to the events below (an animation, a light, a falling-tree prefab)
/// or to pick which child shakes. Code can listen to ResourceNode.Swung / Harvested / Regrown instead.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ResourceNodeFeedback : MonoBehaviour
{
    private const float ShakeSeconds = 0.35f;
    private const float ShakeWobbles = 2.5f;
    private const float BrokenScale = 0.15f;

    [Tooltip("The part that shakes, breaks and pops. Left empty, the model under this node is used.")]
    [SerializeField] private Transform visualRoot;

    [Header("Events (one-off hooks for this prefab)")]
    [SerializeField] private UnityEvent onCritical = new UnityEvent();
    [SerializeField] private UnityEvent onNormal = new UnityEvent();
    [SerializeField] private UnityEvent onGlancing = new UnityEvent();
    [SerializeField] private UnityEvent onHarvested = new UnityEvent();
    [SerializeField] private UnityEvent onRegrown = new UnityEvent();

    private ResourceNode node;
    private IHarvestBody body;
    private Renderer[] renderers;
    private Vector3 baseScale;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool initialised;

    private float shakeStart = -1f;
    private float shakeDegrees;
    private Vector3 shakeAxis;
    private float breakStart = -1f;
    private float breakSeconds;
    private float regrowStart = -1f;
    private float regrowSeconds;
    private float sinkDepth;

    /// <summary>The profile in use: the node's own, else the default one. May be null.</summary>
    public HarvestFeedbackProfile Profile
    {
        get
        {
            HarvestFeedbackProfile own = node != null && node.Definition != null ? node.Definition.Feedback : null;
            return own != null ? own : DefaultProfile;
        }
    }

    /// <summary>Seconds the node keeps showing while it breaks, before ResourceNode hides it.</summary>
    public float BreakSeconds => visualRoot != null && Profile != null ? Profile.BreakSeconds : 0f;

    private HarvestFeedbackProfile OwnProfile => node != null && node.Definition != null ? node.Definition.Feedback : null;
    private static HarvestFeedbackProfile DefaultProfile => GatheringMinigameSettings.Current.DefaultFeedback;

    // Subscribed for the whole lifetime: the component switches itself off while nothing animates.
    private void Awake()
    {
        Initialise();
        if (node != null)
        {
            node.Swung += OnSwung;
            node.Harvested += OnHarvested;
            node.Regrown += OnRegrown;
        }

        enabled = false;
    }

    private void OnDestroy()
    {
        if (node != null)
        {
            node.Swung -= OnSwung;
            node.Harvested -= OnHarvested;
            node.Regrown -= OnRegrown;
        }
    }

    // Deactivated mid-animation: settle the shake and the pop; a break stays broken until the node regrows.
    private void OnDisable()
    {
        if (gameObject.activeInHierarchy || visualRoot == null)
        {
            return;
        }

        if (shakeStart >= 0f)
        {
            shakeStart = -1f;
            visualRoot.localRotation = baseRotation;
        }

        if (regrowStart >= 0f)
        {
            regrowStart = -1f;
            visualRoot.localScale = baseScale;
        }

        breakStart = -1f;
    }

    private void Initialise()
    {
        if (initialised)
        {
            return;
        }

        initialised = true;
        node = GetComponent<ResourceNode>();
        body = GetComponent<IHarvestBody>();
        visualRoot = visualRoot != null ? visualRoot : FindVisualRoot();
        renderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        if (visualRoot != null)
        {
            baseScale = visualRoot.localScale;
            basePosition = visualRoot.localPosition;
            baseRotation = visualRoot.localRotation;
        }
    }

    // The model nested under the node; the node itself when its renderer sits on the root.
    private Transform FindVisualRoot()
    {
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
        {
            Transform part = candidate.transform;
            if (part == transform)
            {
                return transform;
            }

            while (part.parent != null && part.parent != transform)
            {
                part = part.parent;
            }

            if (part.parent == transform)
            {
                return part;
            }
        }

        return null;
    }

    private void OnSwung(SwingResult result, Vector3 from)
    {
        HarvestMoment moment = HarvestFeedbackProfile.MomentOf(result);
        Vector3 point = body != null ? body.HitPoint(from) : HitPoint(from);
        Play(moment, point, from);

        HarvestFeedbackProfile profile = Profile;
        float degrees = profile != null ? profile.For(moment).ShakeDegrees : 0f;
        if (degrees > 0f && visualRoot != null && breakStart < 0f)
        {
            Vector3 away = transform.position - from;
            away.y = 0f;
            shakeAxis = away.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, away.normalized) : Vector3.right;
            shakeDegrees = degrees;
            shakeStart = Time.unscaledTime;
            enabled = true;
        }

        switch (result)
        {
            case SwingResult.Critical: onCritical.Invoke(); break;
            case SwingResult.Normal: onNormal.Invoke(); break;
            default: onGlancing.Invoke(); break;
        }
    }

    private void OnHarvested()
    {
        Vector3 centre = Bounds(out Bounds bounds) ? bounds.center : transform.position + Vector3.up;
        if (body != null)
        {
            centre = body.HitPoint(transform.position - transform.forward);
        }

        Play(HarvestMoment.Harvested, centre, centre - transform.forward);
        onHarvested.Invoke();

        if (node.Definition != null && node.Definition.DepleteOnGather && BreakSeconds > 0f)
        {
            shakeStart = -1f;
            regrowStart = -1f;
            ResetPose();
            breakSeconds = BreakSeconds;
            sinkDepth = Bounds(out bounds) ? bounds.size.y * 0.3f : 0.3f;
            breakStart = Time.time;
            enabled = true;
        }
    }

    private void OnRegrown()
    {
        breakStart = -1f;
        ResetPose();
        regrowSeconds = Profile != null ? Profile.RegrowSeconds : 0f;
        if (regrowSeconds > 0f && visualRoot != null)
        {
            regrowStart = Time.time;
            visualRoot.localScale = baseScale * BrokenScale;
            enabled = true;
        }

        onRegrown.Invoke();
    }

    private void Play(HarvestMoment moment, Vector3 point, Vector3 facing)
    {
        HarvestFeedbackProfile own = OwnProfile;
        HarvestFeedbackProfile fallback = DefaultProfile;
        AudioManager.Play(HarvestFeedbackProfile.SoundFor(own, fallback, moment), point, body != null ? body.HarvestSize : 1f);

        GameObject effect = HarvestFeedbackProfile.EffectFor(own, fallback, moment);
        if (effect != null && Application.isPlaying)
        {
            Vector3 toward = facing - point;
            toward.y = 0f;
            Quaternion rotation = toward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toward.normalized) : Quaternion.identity;
            PrefabPool.Spawn(effect, point, rotation);
        }
    }

    // Where the tool meets the model: the side facing the player, about waist height.
    private Vector3 HitPoint(Vector3 from)
    {
        if (!Bounds(out Bounds bounds))
        {
            return transform.position + Vector3.up;
        }

        float height = bounds.min.y + Mathf.Min(1f, bounds.size.y * 0.5f);
        return bounds.ClosestPoint(new Vector3(from.x, height, from.z));
    }

    private bool Bounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer part in renderers)
        {
            if (part == null || part is ParticleSystemRenderer)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(part.bounds);
            }
            else
            {
                bounds = part.bounds;
                found = true;
            }
        }

        return found;
    }

    private void Update()
    {
        if (visualRoot == null)
        {
            enabled = false;
            return;
        }

        bool busy = false;
        if (breakStart >= 0f)
        {
            float t = Mathf.Clamp01((Time.time - breakStart) / breakSeconds);
            float eased = t * t;
            visualRoot.localScale = baseScale * Mathf.Lerp(1f, BrokenScale, eased);
            visualRoot.localPosition = basePosition + LocalDown() * (sinkDepth * eased);
            // Stays broken (small and sunk) while hidden, so regrowing pops up from there.
            busy = t < 1f;
        }
        else if (regrowStart >= 0f)
        {
            float t = (Time.time - regrowStart) / regrowSeconds;
            if (t >= 1f)
            {
                regrowStart = -1f;
                visualRoot.localScale = baseScale;
            }
            else
            {
                // Grows with a small overshoot, like a spring settling.
                float grow = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f * (1f - t);
                visualRoot.localScale = baseScale * (Mathf.Lerp(BrokenScale, 1f, 1f - (1f - t) * (1f - t)) * grow);
                busy = true;
            }
        }

        if (shakeStart >= 0f)
        {
            float t = (Time.unscaledTime - shakeStart) / ShakeSeconds;
            if (t >= 1f)
            {
                shakeStart = -1f;
                visualRoot.localRotation = baseRotation;
            }
            else
            {
                float angle = shakeDegrees * Mathf.Sin(t * Mathf.PI * 2f * ShakeWobbles) * (1f - t);
                Vector3 axis = visualRoot.parent != null ? visualRoot.parent.InverseTransformDirection(shakeAxis) : shakeAxis;
                visualRoot.localRotation = Quaternion.AngleAxis(angle, axis) * baseRotation;
                busy = true;
            }
        }

        enabled = busy;
    }

    private Vector3 LocalDown()
    {
        return visualRoot.parent != null ? visualRoot.parent.InverseTransformDirection(Vector3.down) : Vector3.down;
    }

    private void ResetPose()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localScale = baseScale;
        visualRoot.localPosition = basePosition;
        visualRoot.localRotation = baseRotation;
    }
}
