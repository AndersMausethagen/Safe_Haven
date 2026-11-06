using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A gathering node's models, cracked into pieces ahead of time (Safe Haven › Setup › Resources › Crack Mined Nodes): for each mesh, the
/// pieces it comes apart into. Each piece keeps the model's own surface in the model's own space, so laid together they are the
/// model exactly, plus a second submesh for the rough faces where it broke. Authored data, made by the builder.
/// </summary>
public sealed class NodeFracture : ScriptableObject
{
    [Serializable]
    private sealed class CrackedMesh
    {
        public Mesh source;
        public Mesh[] pieces;
    }

    [SerializeField] private List<CrackedMesh> meshes = new List<CrackedMesh>();

    /// <summary>What a mesh comes apart into; null for one that isn't cracked (a small crystal flies off whole).</summary>
    public Mesh[] PiecesOf(Mesh mesh)
    {
        foreach (CrackedMesh cracked in meshes)
        {
            if (cracked.source == mesh)
            {
                return cracked.pieces;
            }
        }

        return null;
    }

    public void Add(Mesh source, Mesh[] pieces) => meshes.Add(new CrackedMesh { source = source, pieces = pieces });

    /// <summary>The parts of a model that are really drawn up close: every mesh, but of an LOD group only its finest level.</summary>
    public static List<MeshRenderer> DrawnParts(Transform model)
    {
        var skipped = new HashSet<Renderer>();
        foreach (LODGroup group in model.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] levels = group.GetLODs();
            for (int level = 1; level < levels.Length; level++)
            {
                foreach (Renderer part in levels[level].renderers)
                {
                    skipped.Add(part);
                }
            }
        }

        var parts = new List<MeshRenderer>();
        foreach (MeshRenderer part in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!skipped.Contains(part))
            {
                parts.Add(part);
            }
        }

        return parts;
    }
}
