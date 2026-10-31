using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
public sealed class PlayerInteractionOutline : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material maskMaterial;
    [SerializeField, Min(1f)] private float hoverDistance = 80f;

    // The scan over every Interactable (a thousand trees and rocks round the Haven) runs again only once the player has
    // moved this far or this long has passed (something may have become available); outlines already made, and the
    // hovered object, are checked every frame.
    private const float RescanDistance = 0.25f;
    private const float RescanInterval = 0.2f;

    private Vector3 scannedAt;
    private float nextScan;
    private readonly HashSet<Interactable> shown = new HashSet<Interactable>();

    private readonly Dictionary<Interactable, List<OutlineCopies.Entry>> outlines = new Dictionary<Interactable, List<OutlineCopies.Entry>>();
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly List<Interactable> removed = new List<Interactable>();
    private PlayerInteractor interactor;
    private Damageable health;
    private Camera viewCamera;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        health = GetComponent<Damageable>();
        if (outlineMaterial == null || maskMaterial == null)
        {
            Debug.LogError("PlayerInteractionOutline requires outline and mask materials.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        bool visible = interactor.isActiveAndEnabled && (health == null || health.IsAlive)
            && !GamePause.IsPaused && (UIStateManager.Instance == null || !UIStateManager.Instance.IsAnyWindowOpen);
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
            foreach (Interactable target in Interactable.Active)
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
            }
            else
            {
                shown.Remove(target);
            }
        }
        foreach (Interactable target in removed)
        {
            outlines.Remove(target);
            shown.Remove(target);
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
        return hit != null ? hit.GetComponentInParent<Interactable>() : null;
    }

    private static List<OutlineCopies.Entry> CreateOutlines(Interactable target, Material outline, Material mask)
    {
        return OutlineCopies.Create(target.transform, outline, mask, "Interaction Outline", "Interaction Mask",
            source => source.GetComponentInParent<Interactable>() == target);
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
