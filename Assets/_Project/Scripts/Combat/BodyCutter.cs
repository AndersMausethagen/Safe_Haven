using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuts a dead body's meshes for the gore of a killing blow (DeathRagdoll): the part of a skinned body that belongs to a
/// limb's bones, as a mesh of its own (a head or an arm that comes off), and a whole body broken into chunks by where each
/// triangle lies (a small creature smashed by a two-handed hammer). Meshes are made as the body is posed when it dies, so
/// the pieces wear its own skin. Plain geometry: the caller places, colours and throws the pieces.
/// </summary>
public static class BodyCutter
{
    // A vertex goes with the cut when at least this share of its skinning is the cut bones'.
    private const float CutWeight = 0.5f;

    /// <summary>
    /// Splits <paramref name="skin"/>, as posed now, into the triangles that move with <paramref name="limb"/> (it and its
    /// children) and the rest. Both meshes are in the skin's own space (its position and rotation, scale applied), with
    /// the skin's submeshes, so they take its materials. False when the skin has no weights or nothing of it is the limb's.
    /// </summary>
    public static bool Split(SkinnedMeshRenderer skin, Transform limb, out Mesh piece, out Mesh rest)
    {
        piece = null;
        rest = null;
        Mesh source = skin != null ? skin.sharedMesh : null;
        if (source == null || limb == null)
        {
            return false;
        }

        Transform[] bones = skin.bones;
        var cutBones = new HashSet<int>();
        for (int i = 0; i < bones.Length; i++)
        {
            // The limb, what hangs from it, and what rides beside it (a jaw or ears under the same neck).
            if (bones[i] != null && (bones[i] == limb || bones[i].IsChildOf(limb) || RidesWith(bones[i], limb)))
            {
                cutBones.Add(i);
            }
        }

        for (int i = 0; i < bones.Length; i++)
        {
            for (int j = 0; j < bones.Length; j++)
            {
                if (bones[i] != null && cutBones.Contains(j) && bones[i].IsChildOf(bones[j]))
                {
                    cutBones.Add(i);
                }
            }
        }

        if (cutBones.Count == 0)
        {
            return false;
        }

        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        bool[] cut = source.isReadable && source.boneWeights.Length == source.vertexCount
            ? ByWeights(source.boneWeights, cutBones)
            : ByNearestBone(baked, skin, bones, cutBones);
        piece = Copy(baked, source.name + " (cut off)");
        rest = Copy(baked, source.name + " (left)");
        int inPiece = 0;
        for (int sub = 0; sub < baked.subMeshCount; sub++)
        {
            int[] triangles = baked.GetTriangles(sub);
            var mine = new List<int>();
            var theirs = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int votes = (cut[triangles[t]] ? 1 : 0) + (cut[triangles[t + 1]] ? 1 : 0) + (cut[triangles[t + 2]] ? 1 : 0);
                List<int> into = votes >= 2 ? mine : theirs;
                into.Add(triangles[t]);
                into.Add(triangles[t + 1]);
                into.Add(triangles[t + 2]);
            }

            piece.SetTriangles(mine, sub, false);
            rest.SetTriangles(theirs, sub, false);
            inPiece += mine.Count;
        }

        Object.Destroy(baked);
        // Only the vertices each half uses: its bounds (and so the piece's collider and weight) are its own, not the body's.
        piece = Compact(piece);
        rest = Compact(rest);
        if (inPiece == 0)
        {
            Object.Destroy(piece);
            Object.Destroy(rest);
            piece = rest = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// A bone beside <paramref name="limb"/> under the same parent that goes where it goes: one on the very same joint (a jaw
    /// beside the head), or one named for a part of a head (ears, jaw, eyes, horns, hair, tongue) when the limb is a head.
    /// </summary>
    public static bool RidesWith(Transform bone, Transform limb)
    {
        if (bone == limb || bone.parent == null || bone.parent != limb.parent)
        {
            return false;
        }

        if ((bone.position - limb.position).sqrMagnitude < 1e-6f)
        {
            return true;
        }

        string name = bone.name.ToLowerInvariant();
        return limb.name.ToLowerInvariant().Contains("head")
               && (name.Contains("ear") || name.Contains("jaw") || name.Contains("eye") || name.Contains("horn") || name.Contains("hair") || name.Contains("tongue"));
    }

    /// <summary>One chunk of a smashed body: its mesh (around <see cref="Centre"/>, in world directions) and the materials
    /// its submeshes take.</summary>
    public struct Chunk
    {
        public Mesh Mesh;
        public Vector3 Centre;
        public Material[] Materials;
    }

    /// <summary>
    /// Breaks <paramref name="renderers"/> (static meshes, as they stand in the world) into up to <paramref name="cells"/>
    /// chunks, by which cell of the body's bounds each triangle's middle falls in. Empty cells give no chunk.
    /// </summary>
    public static List<Chunk> Break(IReadOnlyList<MeshRenderer> renderers, Vector3Int cells)
    {
        var chunks = new List<Chunk>();
        if (renderers.Count == 0)
        {
            return chunks;
        }

        Bounds all = renderers[0].bounds;
        foreach (MeshRenderer part in renderers)
        {
            all.Encapsulate(part.bounds);
        }

        Vector3 size = Vector3.Max(all.size, Vector3.one * 0.01f);
        int count = cells.x * cells.y * cells.z;
        // Per cell: its vertices (world) and, per material, its triangles.
        var verts = new List<Vector3>[count];
        var normals = new List<Vector3>[count];
        var uvs = new List<Vector2>[count];
        var tris = new List<(Material material, List<int> triangles)>[count];
        foreach (MeshRenderer part in renderers)
        {
            if (!part.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
                continue;
            }

            Mesh mesh = filter.sharedMesh;
            Matrix4x4 toWorld = part.transform.localToWorldMatrix;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Vector2[] uv = mesh.uv;
            Material[] materials = part.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                Material material = materials.Length > 0 ? materials[Mathf.Min(sub, materials.Length - 1)] : null;
                int[] triangles = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 a = toWorld.MultiplyPoint3x4(v[triangles[t]]);
                    Vector3 b = toWorld.MultiplyPoint3x4(v[triangles[t + 1]]);
                    Vector3 c = toWorld.MultiplyPoint3x4(v[triangles[t + 2]]);
                    Vector3 local = ((a + b + c) / 3f - all.min);
                    int cx = Mathf.Clamp((int)(local.x / size.x * cells.x), 0, cells.x - 1);
                    int cy = Mathf.Clamp((int)(local.y / size.y * cells.y), 0, cells.y - 1);
                    int cz = Mathf.Clamp((int)(local.z / size.z * cells.z), 0, cells.z - 1);
                    int cell = (cx * cells.y + cy) * cells.z + cz;
                    verts[cell] ??= new List<Vector3>();
                    normals[cell] ??= new List<Vector3>();
                    uvs[cell] ??= new List<Vector2>();
                    tris[cell] ??= new List<(Material, List<int>)>();
                    List<int> into = null;
                    foreach ((Material m, List<int> list) in tris[cell])
                    {
                        if (m == material)
                        {
                            into = list;
                        }
                    }

                    if (into == null)
                    {
                        into = new List<int>();
                        tris[cell].Add((material, into));
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        int index = triangles[t + k];
                        into.Add(verts[cell].Count);
                        verts[cell].Add(k == 0 ? a : k == 1 ? b : c);
                        normals[cell].Add(n.Length == v.Length ? toWorld.MultiplyVector(n[index]).normalized : Vector3.up);
                        uvs[cell].Add(uv.Length == v.Length ? uv[index] : Vector2.zero);
                    }
                }
            }
        }

        for (int cell = 0; cell < count; cell++)
        {
            if (verts[cell] == null || verts[cell].Count < 3)
            {
                continue;
            }

            Vector3 centre = Vector3.zero;
            foreach (Vector3 p in verts[cell])
            {
                centre += p;
            }

            centre /= verts[cell].Count;
            for (int i = 0; i < verts[cell].Count; i++)
            {
                verts[cell][i] -= centre;
            }

            var mesh = new Mesh { name = "Smashed piece" };
            if (verts[cell].Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(verts[cell]);
            mesh.SetNormals(normals[cell]);
            mesh.SetUVs(0, uvs[cell]);
            mesh.subMeshCount = tris[cell].Count;
            var materials = new Material[tris[cell].Count];
            for (int s = 0; s < tris[cell].Count; s++)
            {
                mesh.SetTriangles(tris[cell][s].triangles, s, false);
                materials[s] = tris[cell][s].material;
            }

            mesh.RecalculateBounds();
            chunks.Add(new Chunk { Mesh = mesh, Centre = centre, Materials = materials });
        }

        return chunks;
    }

    private static bool[] ByWeights(BoneWeight[] weights, HashSet<int> cutBones)
    {
        var cut = new bool[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            float share = (cutBones.Contains(w.boneIndex0) ? w.weight0 : 0f) + (cutBones.Contains(w.boneIndex1) ? w.weight1 : 0f)
                          + (cutBones.Contains(w.boneIndex2) ? w.weight2 : 0f) + (cutBones.Contains(w.boneIndex3) ? w.weight3 : 0f);
            cut[i] = share >= CutWeight;
        }

        return cut;
    }

    // Without readable weights (most imported models keep their meshes on the GPU only): each vertex of the posed body goes
    // with the bone whose segment (the bone to its first child) is nearest.
    private static bool[] ByNearestBone(Mesh baked, SkinnedMeshRenderer skin, Transform[] bones, HashSet<int> cutBones)
    {
        // BakeMesh gives vertices in the skin's own position and rotation, in its parent's units (an imported model's 0.01
        // or 0.11 lives on the parent): what a piece placed under that parent, at the skin's pose, shows as it was.
        Transform parent = skin.transform.parent;
        Matrix4x4 local = Matrix4x4.TRS(skin.transform.localPosition, skin.transform.localRotation, Vector3.one);
        Matrix4x4 toBaked = (parent != null ? parent.localToWorldMatrix * local : local).inverse;
        var starts = new Vector3[bones.Length];
        var ends = new Vector3[bones.Length];
        var usable = new bool[bones.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            if (bones[b] == null)
            {
                continue;
            }

            usable[b] = true;
            starts[b] = toBaked.MultiplyPoint3x4(bones[b].position);
            ends[b] = starts[b];
            foreach (Transform child in bones[b])
            {
                if (System.Array.IndexOf(bones, child) >= 0)
                {
                    ends[b] = toBaked.MultiplyPoint3x4(child.position);
                    break;
                }
            }

            // A bone with nothing after it (a head, a hand) reaches on the way its parent pointed, at least a hand's length:
            // as a bare point it tied with the bone before it and never owned anything.
            if (ends[b] == starts[b] && bones[b].parent != null)
            {
                Vector3 from = toBaked.MultiplyPoint3x4(bones[b].parent.position);
                Vector3 way = starts[b] - from;
                if (way.sqrMagnitude > 1e-8f)
                {
                    ends[b] = starts[b] + way.normalized * Mathf.Max(way.magnitude, 0.15f);
                }
            }
        }

        Vector3[] vertices = baked.vertices;
        var cut = new bool[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            float nearest = float.MaxValue;
            int owner = -1;
            for (int b = 0; b < bones.Length; b++)
            {
                if (!usable[b])
                {
                    continue;
                }

                Vector3 along = ends[b] - starts[b];
                float t = along.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(vertices[i] - starts[b], along) / along.sqrMagnitude) : 0f;
                float distance = (vertices[i] - (starts[b] + along * t)).sqrMagnitude;
                // A tie (two bones on one joint) goes to the cut.
                if (distance < nearest || (distance <= nearest + 1e-6f && cutBones.Contains(b)))
                {
                    nearest = distance;
                    owner = b;
                }
            }

            cut[i] = owner >= 0 && cutBones.Contains(owner);
        }

        return cut;
    }

    // A mesh with the vertices none of its triangles use taken out.
    private static Mesh Compact(Mesh mesh)
    {
        var map = new Dictionary<int, int>();
        var keep = new List<int>();
        var subs = new List<int>[mesh.subMeshCount];
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] triangles = mesh.GetTriangles(sub);
            subs[sub] = new List<int>(triangles.Length);
            foreach (int index in triangles)
            {
                if (!map.TryGetValue(index, out int now))
                {
                    now = keep.Count;
                    map[index] = now;
                    keep.Add(index);
                }

                subs[sub].Add(now);
            }
        }

        Vector3[] v = mesh.vertices;
        Vector3[] n = mesh.normals;
        Vector4[] t = mesh.tangents;
        Color[] c = mesh.colors;
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector2>();
        mesh.GetUVs(0, uv0);
        mesh.GetUVs(1, uv1);
        var compact = new Mesh { name = mesh.name, indexFormat = keep.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        compact.SetVertices(keep.ConvertAll(i => v[i]));
        if (n.Length == v.Length) compact.SetNormals(keep.ConvertAll(i => n[i]));
        if (t.Length == v.Length) compact.SetTangents(keep.ConvertAll(i => t[i]));
        if (c.Length == v.Length) compact.SetColors(keep.ConvertAll(i => c[i]));
        if (uv0.Count == v.Length) compact.SetUVs(0, keep.ConvertAll(i => uv0[i]));
        if (uv1.Count == v.Length) compact.SetUVs(1, keep.ConvertAll(i => uv1[i]));
        compact.subMeshCount = mesh.subMeshCount;
        for (int sub = 0; sub < subs.Length; sub++)
        {
            compact.SetTriangles(subs[sub], sub, false);
        }

        compact.RecalculateBounds();
        Object.Destroy(mesh);
        return compact;
    }

    private static Mesh Copy(Mesh baked, string name)
    {
        var copy = new Mesh { name = name, indexFormat = baked.indexFormat };
        copy.SetVertices(baked.vertices);
        copy.SetNormals(baked.normals);
        if (baked.tangents.Length == baked.vertexCount) copy.SetTangents(baked.tangents);
        var uv = new List<Vector2>();
        baked.GetUVs(0, uv);
        if (uv.Count == baked.vertexCount) copy.SetUVs(0, uv);
        baked.GetUVs(1, uv);
        if (uv.Count == baked.vertexCount) copy.SetUVs(1, uv);
        if (baked.colors.Length == baked.vertexCount) copy.colors = baked.colors;
        copy.subMeshCount = baked.subMeshCount;
        return copy;
    }
}
