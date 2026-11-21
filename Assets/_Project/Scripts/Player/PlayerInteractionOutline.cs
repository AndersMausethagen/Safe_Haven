using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
public sealed class PlayerInteractionOutline : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material maskMaterial;

    /// <summary>The outline and its mask, for other outlines drawn the same way (NeededNearby).</summary>
    public Material OutlineMaterial => outlineMaterial;
    public Material MaskMaterial => maskMaterial;
    [SerializeField, Min(1f)] private float hoverDistance = 80f;

    // The scan over every Interactable (a thousand trees and rocks round the Haven) runs again only once the player has
    // moved this far or this long has passed (something may have become available); outlines already made, and the
    // hovered object, are checked every frame.
    private const float RescanDistance = 0.25f;
    private const float RescanInterval = 0.2f;
    private const float ReleaseSeconds = 10f;

    private Vector3 scannedAt;
    private float nextScan;
    private readonly HashSet<Interactable> shown = new HashSet<Interactable>();
    private readonly Dictionary<Interactable, float> hiddenSince = new Dictionary<Interactable, float>();
    private readonly List<Interactable> nearby = new List<Interactable>();

    private readonly Dictionary<Interactable, List<OutlineCopies.Entry>> outlines = new Dictionary<Interactable, List<OutlineCopies.Entry>>();
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly List<Interactable> removed = new List<Interactable>();
    private PlayerInteractor interactor;
    private PlayerStateMachine stateMachine;
    private Damageable health;
    private Camera viewCamera;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        if (outlineMaterial == null || maskMaterial == null)
        {
            Debug.LogError("PlayerInteractionOutline requires outline and mask materials.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        // Not at the oars or at work on something, like the prompt (WorldInteractionIndicator).
        bool visible = interactor.isActiveAndEnabled && (health == null || health.IsAlive)
            && interactor.Current == null && (stateMachine == null || !stateMachine.IsAboard)
            && !GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen) && !KeeperView.IsOpen;
        Interactable hovered = visible ? FindHovered() : null;
        if (visible && hovered != null && hovered.IsAvailable && !outlines.ContainsKey(hovered))
        {
            outlines.Add(hovered, CreateOutlines(hovered, outlineMaterial, maskMaterial));
        }

        bool scan = visible && (Time.time >= nextScan || (transform.position - scannedAt).sqrMagnitude > RescanDistance * RescanDistance);
        if (scan)
        {
            scannedAt = transform.position;
            nextScan = Time.time + RescanInterval;
            nearby.Clear();
            InteractableGrid.Near(transform.position, InteractableGrid.LongestRange, nearby);
            foreach (Interactable target in nearby)
            {
                if (target != null && !outlines.ContainsKey(target) && target.IsAvailable && InReach(target))
                {
                    outlines.Add(target, CreateOutlines(target, outlineMaterial, maskMaterial));
                }
            }
        }

        removed.Clear();
        foreach (var pair in outlines)
        {
            Interactable target = pair.Key;
            if (target == null || OutlineCopies.AllGone(pair.Value))
            {
                removed.Add(target);
                continue;
            }

            // Hidden outlines wait for the next scan; shown and hovered ones are checked every frame.
            if (!scan && target != hovered && !shown.Contains(target))
            {
                continue;
            }

            bool show = visible && target.IsAvailable && (target == hovered || InReach(target));
            OutlineCopies.SetShown(pair.Value, show);
            if (show)
            {
                shown.Add(target);
                hiddenSince.Remove(target);
            }
            else
            {
                shown.Remove(target);
                if (!hiddenSince.TryGetValue(target, out float since))
                {
                    hiddenSince[target] = Time.time;
                }
                else if (Time.time - since > ReleaseSeconds)
                {
                    // Long out of reach: its copies go, so they don't pile up over every tree walked past.
                    OutlineCopies.Destroy(pair.Value);
                    removed.Add(target);
                }
            }
        }
        foreach (Interactable target in removed)
        {
            outlines.Remove(target);
            shown.Remove(target);
            hiddenSince.Remove(target);
        }
    }

    // The bounds test first: this runs over every Interactable in the scene on each scan.
    private bool InReach(Interactable target) =>
        target.MightBeWithin(transform.position, target.Range) && target.DistanceFrom(transform.position) <= target.Range;

    private Interactable FindHovered()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }
        viewCamera = viewCamera != null && viewCamera.isActiveAndEnabled ? viewCamera : Camera.main;
        if (!CameraPointer.TryGetRay(viewCamera, out Ray ray))
        {
            return null;
        }

        return PickHovered(ray);
    }

    private Interactable PickHovered(Ray ray)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, hoverDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        Collider hit = null;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= closest)
            {
                continue;
            }
            closest = hits[i].distance;
            hit = hits[i].collider;
        }
        if (hit == null) return null;
        // A tree drawn without scripts (NodeStreaming) gets its live copy while it is pointed at.
        Interactable found = hit.GetComponentInParent<Interactable>();
        if (found == null) found = NodeStreaming.Wake(hit);
        NodeStreaming.KeepAwake(found);
        return found;
    }

    // Only the level of detail it is drawn at up close: copies of every level would draw at once, all of them outlined.
    private static List<OutlineCopies.Entry> CreateOutlines(Interactable target, Material outline, Material mask)
    {
        return OutlineCopies.Create(target.transform, outline, mask, "Interaction Outline", "Interaction Mask",
            source => source.GetComponentInParent<Interactable>() == target && InNearestDetail(source));
    }

    private static bool InNearestDetail(Renderer source)
    {
        // A tree's shadows-only proxy (TreeShadowProxies) is never seen, so never outlined.
        if (source.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return false;
        LODGroup group = source.GetComponentInParent<LODGroup>();
        if (group == null || group.lodCount == 0) return true;
        LOD[] lods = group.GetLODs();
        if (System.Array.IndexOf(lods[0].renderers, source) >= 0) return true;
        for (int i = 1; i < lods.Length; i++)
        {
            if (System.Array.IndexOf(lods[i].renderers, source) >= 0) return false;
        }

        return true;
    }

    private void OnDisable()
    {
        foreach (var pair in outlines)
        {
            OutlineCopies.SetShown(pair.Value, false);
        }

        shown.Clear();
        nextScan = 0f;
    }

    private void OnDestroy()
    {
        foreach (var pair in outlines)
        {
            OutlineCopies.Destroy(pair.Value);
        }
        outlines.Clear();
    }
}
