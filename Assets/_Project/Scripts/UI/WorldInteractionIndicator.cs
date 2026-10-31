using TMPro;
using UnityEngine;

/// <summary>
/// One camera-facing marker above the player's nearby interaction target. Green means ready; red explains a missing
/// requirement; a soft white note (IInteractionNote) says something that isn't a problem, like "Come back when it's done". It observes the same target and readiness query used by the Interact key, without starting any work.
/// Authored as a child of PlayerArmature, so it follows the player's lifetime without scene-specific references.
/// Its images and text use always-on-top materials (Art/UI/Materials), so trees and buildings never hide it.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionIndicator : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private UnityEngine.UI.Image orb;
    [SerializeField] private UnityEngine.UI.Image halo;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Color readyColor = new Color(0.32f, 1f, 0.52f, 1f);
    [SerializeField] private Color blockedColor = new Color(1f, 0.3f, 0.25f, 1f);
    [Tooltip("For things that can't be used now but aren't a problem either, e.g. a quest giver waiting for you to finish.")]
    [SerializeField] private Color noteColor = new Color(0.85f, 0.87f, 0.92f, 1f);
    [Tooltip("Meters per canvas unit, independent of the player's scale.")]
    [SerializeField, Min(0.0001f)] private float unitSize = 0.012f;
    [Tooltip("Seconds to fade in when a target is found.")]
    [SerializeField, Min(0f)] private float fadeInTime = 0.15f;
    [Tooltip("Seconds to fade out when the target is lost.")]
    [SerializeField, Min(0f)] private float fadeOutTime = 0.25f;
    [Tooltip("Size the marker grows from while fading in, as a share of its full size.")]
    [SerializeField, Range(0.5f, 1f)] private float appearScale = 0.85f;

    private PlayerInteractor interactor;
    private Damageable health;
    private Camera viewCamera;
    private Vector3 localAnchor;
    private float nextRefreshAt = -1f;
    private Transform lastTarget;
    private float fade;

    public Interactable Target { get; private set; }
    public bool IsReady { get; private set; }

    private void Awake()
    {
        interactor = GetComponentInParent<PlayerInteractor>();
        health = GetComponentInParent<Damageable>();
    }

    private void OnEnable()
    {
        nextRefreshAt = -1f;
        Hide();
        fade = 0f;
        lastTarget = null;
        SetFade(0f);
    }

    private void OnDisable()
    {
        Hide();
        fade = 0f;
        lastTarget = null;
        SetFade(0f);
    }

    private void LateUpdate()
    {
        viewCamera = viewCamera != null && viewCamera.isActiveAndEnabled ? viewCamera : Camera.main;
        if (interactor == null || !interactor.isActiveAndEnabled || (health != null && !health.IsAlive)
            || (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen) || viewCamera == null)
        {
            Hide();
        }
        else
        {
            if (Time.unscaledTime >= nextRefreshAt)
            {
                RefreshTarget();
                nextRefreshAt = Time.unscaledTime + RefreshInterval;
            }

            if (Target != null && !Target.isActiveAndEnabled)
            {
                Hide();
            }
        }

        // A lost target fades out where it stood, still showing its last name and status.
        bool visible = Target != null;
        if (visible)
        {
            lastTarget = Target.transform;
        }

        float duration = visible ? fadeInTime : fadeOutTime;
        float step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
        fade = Mathf.MoveTowards(fade, visible ? 1f : 0f, step);
        if (fade <= 0f || lastTarget == null || viewCamera == null)
        {
            SetFade(0f);
            return;
        }

        transform.position = lastTarget.TransformPoint(localAnchor);
        transform.rotation = viewCamera.transform.rotation;
        ApplyScale(Mathf.Lerp(appearScale, 1f, Mathf.SmoothStep(0f, 1f, fade)));
        SetFade(viewCamera.WorldToViewportPoint(transform.position).z > 0f ? fade : 0f);
    }

    private void SetFade(float value)
    {
        if (group != null)
        {
            group.alpha = value;
        }
    }

    // The name players know the thing by (IInteractionName on the object), else the object's name.
    private static string NameOf(Interactable target)
    {
        if (target.TryGetComponent(out IInteractionName named) && !string.IsNullOrEmpty(named.InteractionName))
        {
            return named.InteractionName;
        }

        return target.name.Replace("(Clone)", "").Replace('_', ' ').Trim();
    }

    private void RefreshTarget()
    {
        Interactable next = interactor.FindFocus();
        if (next != Target)
        {
            Target = next;
            if (Target != null)
            {
                localAnchor = Target.transform.InverseTransformPoint(FindAnchor(Target));
                SetText(title, NameOf(Target));
            }
        }

        if (Target == null)
        {
            IsReady = false;
            return;
        }

        bool working = interactor.Current == Target;
        string reason = null;
        IsReady = working || interactor.CanInteractWith(Target, out reason);
        bool note = !IsReady && Target.TryGetComponent(out IInteractionNote source) && source.IsNote;
        Color color = IsReady ? readyColor : note ? noteColor : blockedColor;
        if (orb != null)
        {
            orb.color = color;
        }

        if (halo != null)
        {
            halo.color = new Color(color.r, color.g, color.b, 0.25f);
        }

        if (status != null)
        {
            status.color = color;
            if (working && Target.HoldOpen)
            {
                // A minigame runs on its own bar, so there is no share of the work to report.
                SetText(status, "Working...");
            }
            else if (working)
            {
                status.SetText("Working... {0:0}%", Target.Progress * 100f);
            }
            else if (IsReady)
            {
                SetText(status, Target.TryGetComponent(out IInteractionVerb verb) ? "[E] " + verb.Verb : "[E] Interact");
            }
            else
            {
                SetText(status, string.IsNullOrEmpty(reason) ? "Cannot interact." : reason);
            }
        }
    }

    private void Hide()
    {
        Target = null;
        IsReady = false;
        nextRefreshAt = -1f;
    }

    private void ApplyScale(float factor)
    {
        Vector3 scale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float size = unitSize * factor;
        transform.localScale = new Vector3(
            size / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            size / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            size / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }

    private static Vector3 FindAnchor(Interactable target)
    {
        Collider[] parts = target.GetComponentsInChildren<Collider>();
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
        bool found = false;
        foreach (Collider part in parts)
        {
            if (part == null || !part.enabled || part.isTrigger)
            {
                continue;
            }

            if (!found)
            {
                bounds = part.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(part.bounds);
            }
        }

        // Tall trees keep the marker at their trunk instead of losing it above the canopy.
        float baseY = target.transform.position.y;
        return found
            ? new Vector3(bounds.center.x, Mathf.Clamp(bounds.max.y + 0.45f, baseY + 1.3f, baseY + 3f), bounds.center.z)
            : target.transform.position + Vector3.up * 1.5f;
    }

    private static void SetText(TMP_Text label, string text)
    {
        if (label != null && label.text != text)
        {
            label.text = text;
        }
    }
}
