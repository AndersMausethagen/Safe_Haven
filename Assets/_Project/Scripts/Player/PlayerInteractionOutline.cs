using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
public sealed class PlayerInteractionOutline : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material maskMaterial;
    [SerializeField, Min(1f)] private float hoverDistance = 80f;

    private readonly Dictionary<Interactable, List<(Renderer source, Renderer outline, Renderer mask)>> outlines =
        new Dictionary<Interactable, List<(Renderer, Renderer, Renderer)>>();
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
                        outlines.Add(target, CreateOutlines(target));
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
            foreach (var entry in pair.Value)
            {
                if (entry.outline != null)
                {
                    entry.outline.enabled = show && entry.source != null && entry.source.enabled
                        && entry.source.gameObject.activeInHierarchy;
                    if (entry.mask != null) entry.mask.enabled = entry.outline.enabled;
                }
            }
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

    private List<(Renderer source, Renderer outline, Renderer mask)> CreateOutlines(Interactable target)
    {
        var result = new List<(Renderer, Renderer, Renderer)>();
        foreach (Renderer source in target.GetComponentsInChildren<Renderer>(true))
        {
            if (source.GetComponentInParent<Interactable>() != target)
            {
                continue;
            }
            Mesh mesh;
            if (source is SkinnedMeshRenderer skin)
            {
                mesh = skin.sharedMesh;
            }
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter))
            {
                mesh = filter.sharedMesh;
            }
            else
            {
                continue;
            }
            if (mesh == null)
            {
                continue;
            }

            Renderer outline = CopyRenderer(source, mesh, outlineMaterial, "Interaction Outline");
            Renderer mask = CopyRenderer(source, mesh, maskMaterial, "Interaction Mask");
            result.Add((source, outline, mask));
        }
        return result;
    }

    // Mask the unexpanded mesh first, so even thin meshes touching the ground get a hollow, depth-tested contour.
    private static Renderer CopyRenderer(Renderer source, Mesh mesh, Material material, string name)
    {
        var holder = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
        holder.transform.SetParent(source.transform, false);
        Renderer copy;
        if (source is SkinnedMeshRenderer skinned)
        {
            var skin = holder.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = skinned.bones;
            skin.rootBone = skinned.rootBone;
            skin.localBounds = skinned.localBounds;
            copy = skin;
        }
        else
        {
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            copy = holder.AddComponent<MeshRenderer>();
        }
        var materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        copy.sharedMaterials = materials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        copy.enabled = false;
        return copy;
    }

    private void OnDisable()
    {
        foreach (var pair in outlines)
        {
            foreach (var entry in pair.Value)
            {
                if (entry.outline != null) entry.outline.enabled = false;
                if (entry.mask != null) entry.mask.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var pair in outlines)
        {
            foreach (var entry in pair.Value)
            {
                foreach (Renderer renderer in new[] { entry.outline, entry.mask })
                {
                    if (renderer == null) continue;
                    if (Application.isPlaying) Destroy(renderer.gameObject);
                    else DestroyImmediate(renderer.gameObject);
                }
            }
        }
        outlines.Clear();
    }
}
