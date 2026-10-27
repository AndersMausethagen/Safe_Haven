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
        if (visible)
        {
            foreach (Interactable target in Interactable.Active)
            {
                if (target != null && target.IsAvailable &&
                    (target == hovered || target.DistanceFrom(transform.position) <= target.Range))
                {
                    if (!outlines.ContainsKey(target))
                    {
                        outlines.Add(target, CreateOutlines(target, outlineMaterial, maskMaterial));
                    }
                }
            }
        }

        removed.Clear();
        foreach (var pair in outlines)
        {
            Interactable target = pair.Key;
            if (target == null)
            {
                removed.Add(target);
                continue;
            }

            bool show = visible && target.IsAvailable &&
                (target == hovered || target.DistanceFrom(transform.position) <= target.Range);
            OutlineCopies.SetShown(pair.Value, show);
        }
        foreach (Interactable target in removed)
        {
            outlines.Remove(target);
        }
    }

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
