using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a tree is made of, read from its model so a falling copy can be built: the meshes of its nearest level of detail
/// where it stands, the shape of its trunk and crown, and the look of its bark and leaves. Trees in a scene and painted
/// terrain trees (a prefab plus a pose) read the same way, so everything after this treats them alike.
/// </summary>
public sealed class TreeParts
{
    public struct Part
    {
        public Mesh Mesh;
        public Material[] Materials;
        /// <summary>From the tree's base, in world orientation.</summary>
        public Matrix4x4 Pose;
    }

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private const float UntintedAbove = 0.8f;

    public readonly List<Part> Parts = new List<Part>();

    /// <summary>Where the tree stands.</summary>
    public Vector3 Base { get; private set; }
    public float Height { get; private set; }
    public float TrunkRadius { get; private set; }
    /// <summary>How much of the tree is straight enough to cut into logs.</summary>
    public float TrunkLength { get; private set; }
    /// <summary>The crown, relative to the base.</summary>
    public Bounds Crown { get; private set; }
    public bool HasFoliage { get; private set; }
    /// <summary>The colour of its falling leaves. Most leaves get their green from a texture and are untinted white, so
    /// those keep the leaf green here rather than fall as white petals.</summary>
    public Color LeafTint { get; private set; } = new Color(0.38f, 0.56f, 0.22f);
    /// <summary>The tree's own leaf-fall effect (TreeLeafLook), or null for Tree Chopping Settings › Leaf Fall.</summary>
    public GameObject LeafFall { get; private set; }

    /// <summary>The tree's own wood (ChoppableTree.Wood), or null: its logs roll by the axe's tier.</summary>
    public ItemDefinition Wood { get; set; }
    /// <summary>The tree's own tier (its gathering node's), or 0 when it has none: what work on it, felled, trains at.</summary>
    public int Tier { get; set; }
    /// <summary>Seconds until it grows back where it stood, for how long its stump lasts: 0 for the terrain's usual regrowth
    /// (Tree Chopping Settings), negative for never (the Haven's remembered woods, a tree with no respawn).</summary>
    public float RegrowSeconds { get; set; }
    /// <summary>The tree's own bark, or null when its bark is made for its model alone and won't wrap a log.</summary>
    public Material Bark { get; private set; }

    /// <summary>Builds a visible copy of the tree under a transform standing at its base.</summary>
    public Transform BuildModel(Transform parent)
    {
        var model = new GameObject("Model").transform;
        model.SetParent(parent, false);
        foreach (Part part in Parts)
        {
            var piece = new GameObject(part.Mesh.name);
            piece.transform.SetParent(model, false);
            piece.transform.SetLocalPositionAndRotation(part.Pose.GetColumn(3), part.Pose.rotation);
            piece.transform.localScale = part.Pose.lossyScale;
            piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = part.Materials;
        }

        return model;
    }

    /// <summary>A tree standing in the scene.</summary>
    public static TreeParts Of(GameObject tree) => Read(tree, tree.transform.localToWorldMatrix, tree.transform.lossyScale);

    /// <summary>A painted terrain tree: its prototype prefab at a pose in the world.</summary>
    public static TreeParts Of(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale) =>
        Read(prefab, Matrix4x4.TRS(position, rotation, scale), scale);

    private static TreeParts Read(GameObject source, Matrix4x4 toWorld, Vector3 scale)
    {
        var parts = new TreeParts { Base = toWorld.GetColumn(3) };
        Matrix4x4 fromBase = Matrix4x4.Translate(-parts.Base) * toWorld * source.transform.worldToLocalMatrix;
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        var everything = new Bounds();
        var crown = new Bounds();
        bool anyPart = false;
        foreach (Renderer renderer in NearestRenderers(source))
        {
            // A shadows-only proxy (TreeShadowProxies) is not part of what is seen.
            if (!(renderer is MeshRenderer) || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 pose = fromBase * renderer.transform.localToWorldMatrix;
            Bounds bounds = Transform(filter.sharedMesh.bounds, pose);
            everything = anyPart ? Merge(everything, bounds) : bounds;
            anyPart = true;
            parts.Parts.Add(new Part { Mesh = filter.sharedMesh, Materials = renderer.sharedMaterials, Pose = pose });
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                if (rules.IsFoliage(material.name))
                {
                    crown = parts.HasFoliage ? Merge(crown, bounds) : bounds;
                    parts.HasFoliage = true;
                    if (material.HasProperty(BaseColor) && IsTinted(material.GetColor(BaseColor)))
                    {
                        parts.LeafTint = material.GetColor(BaseColor);
                    }
                }
                else if (parts.Bark == null && material.shader != null && material.shader.name != "Safe Haven/Tree Wind")
                {
                    parts.Bark = material;
                }
            }
        }

        parts.Height = anyPart ? Mathf.Max(1f, everything.max.y) : 8f;
        parts.Crown = parts.HasFoliage ? crown : new Bounds(Vector3.up * (parts.Height * 0.7f), new Vector3(4f, parts.Height * 0.6f, 4f));
        parts.TrunkRadius = 0.2f;
        parts.TrunkLength = parts.Height * 0.65f;
        if (source.TryGetComponent(out CapsuleCollider trunk))
        {
            parts.TrunkRadius = Mathf.Max(MinTrunkRadius, trunk.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            parts.TrunkLength = Mathf.Min(Mathf.Max(trunk.height * Mathf.Abs(scale.y), parts.Height * 0.5f), parts.Height * 0.8f);
        }

        // A tree can name its own falling leaves (needles from a cedar, not broad leaves) and their colour.
        if (source.TryGetComponent(out TreeLeafLook look))
        {
            parts.LeafTint = look.Colour;
            parts.LeafFall = look.LeafFall;
        }

        return parts;
    }

    // Slender young trunks are real: a thicker floor made their stumps and logs wider than the tree.
    private const float MinTrunkRadius = 0.07f;

    private static bool IsTinted(Color colour) => Mathf.Min(colour.r, Mathf.Min(colour.g, colour.b)) < UntintedAbove;

    // Felling happens at arm's length, so only the most detailed level is ever seen.
    private static IEnumerable<Renderer> NearestRenderers(GameObject source)
    {
        if (source.TryGetComponent(out LODGroup levels) && levels.lodCount > 0)
        {
            return levels.GetLODs()[0].renderers;
        }

        return source.GetComponentsInChildren<Renderer>(true);
    }

    private static Bounds Transform(Bounds bounds, Matrix4x4 pose)
    {
        Vector3 centre = pose.MultiplyPoint3x4(bounds.center);
        Vector3 extents = bounds.extents;
        var size = new Vector3(
            Mathf.Abs(pose.m00) * extents.x + Mathf.Abs(pose.m01) * extents.y + Mathf.Abs(pose.m02) * extents.z,
            Mathf.Abs(pose.m10) * extents.x + Mathf.Abs(pose.m11) * extents.y + Mathf.Abs(pose.m12) * extents.z,
            Mathf.Abs(pose.m20) * extents.x + Mathf.Abs(pose.m21) * extents.y + Mathf.Abs(pose.m22) * extents.z);
        return new Bounds(centre, size * 2f);
    }

    private static Bounds Merge(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }
}
