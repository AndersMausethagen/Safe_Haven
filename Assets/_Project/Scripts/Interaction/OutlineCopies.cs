using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Outlines made of copies: each mesh renderer of an object gets two hidden copies under it, a stencil mask of the
/// mesh and the outline shell (InteractionOutline shaders), so the object's own materials are never touched. Used by
/// the interaction outline (what can be used) and the archery target outline (what an arrow would hit).
/// </summary>
public static class OutlineCopies
{
    /// <summary>One outlined renderer: the original and its two copies.</summary>
    public readonly struct Entry
    {
        public readonly Renderer Source;
        public readonly Renderer Outline;
        public readonly Renderer Mask;

        public Entry(Renderer source, Renderer outline, Renderer mask)
        {
            Source = source;
            Outline = outline;
            Mask = mask;
        }
    }

    private static readonly HashSet<Renderer> Copies = new HashSet<Renderer>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Copies.Clear();

    /// <summary>True for the copies made here, so one outline never outlines another's copies.</summary>
    public static bool IsCopy(Renderer renderer) => Copies.Contains(renderer);

    /// <summary>Makes hidden outline and mask copies for every mesh renderer under <paramref name="root"/> that
    /// <paramref name="include"/> accepts (all when null).</summary>
    public static List<Entry> Create(Transform root, Material outline, Material mask, string outlineName, string maskName,
        Func<Renderer, bool> include = null)
    {
        if (Copies.Count > 4096)
        {
            Copies.RemoveWhere(copy => copy == null); // copies destroyed with their objects
        }

        var result = new List<Entry>();
        foreach (Renderer source in root.GetComponentsInChildren<Renderer>(true))
        {
            if (IsCopy(source) || include != null && !include(source))
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

            Renderer outlineCopy = Copy(source, mesh, outline, outlineName);
            Renderer maskCopy = Copy(source, mesh, mask, maskName);
            result.Add(new Entry(source, outlineCopy, maskCopy));
        }

        return result;
    }

    /// <summary>True when every original has gone, as when an object swaps its model: its outlines need making again.</summary>
    public static bool AllGone(List<Entry> entries)
    {
        foreach (Entry entry in entries)
        {
            if (entry.Source != null)
            {
                return false;
            }
        }

        return entries.Count > 0;
    }

    /// <summary>Shows or hides the copies; a copy only shows while its original does.</summary>
    public static void SetShown(List<Entry> entries, bool show)
    {
        foreach (Entry entry in entries)
        {
            if (entry.Outline == null)
            {
                continue;
            }

            entry.Outline.enabled = show && entry.Source != null && entry.Source.enabled && entry.Source.gameObject.activeInHierarchy;
            if (entry.Mask != null)
            {
                entry.Mask.enabled = entry.Outline.enabled;
            }
        }
    }

    /// <summary>Gives every outline copy this material instead, e.g. to recolour it.</summary>
    public static void SetOutlineMaterial(List<Entry> entries, Material material)
    {
        foreach (Entry entry in entries)
        {
            if (entry.Outline == null)
            {
                continue;
            }

            Material[] materials = entry.Outline.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            entry.Outline.sharedMaterials = materials;
        }
    }

    public static void Destroy(List<Entry> entries)
    {
        foreach (Entry entry in entries)
        {
            foreach (Renderer renderer in new[] { entry.Outline, entry.Mask })
            {
                Copies.Remove(renderer);
                if (renderer == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(renderer.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                }
            }
        }

        entries.Clear();
    }

    // Mask the unexpanded mesh first, so even thin meshes touching the ground get a hollow, depth-tested contour.
    private static Renderer Copy(Renderer source, Mesh mesh, Material material, string name)
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
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = material;
        }

        copy.sharedMaterials = materials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        copy.enabled = false;
        Copies.Add(copy);
        return copy;
    }
}
