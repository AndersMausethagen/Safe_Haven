using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Turns store-bought vegetation and props, made for any render pipeline (HDRP Lit, Shader Graphs, URP Lit), into Safe
/// Haven assets that use the project's own shaders, so they sway, tessellate and grow like everything else:
/// <list type="bullet">
/// <item><see cref="Tree"/>: a terrain tree on <c>Safe Haven/Tree Wind</c>, its LOD meshes baked with wind weights
/// (uv1 sway/flex, uv2 phase/flutter, see TreeWind.hlsl), a trunk capsule and the gameplay of a template tree (felling, wood).</item>
/// <item><see cref="Prop"/>: a rock, log or stump on <c>Safe Haven/Mesh Surface (Tessellated)</c> with its height map, and
/// a capsule the navmesh cuts around when it is solid.</item>
/// <item><see cref="Detail"/>: a terrain detail on <c>Safe Haven/Grass</c> (bend, phase and glow in the vertex colour).</item>
/// <item><see cref="Ground"/>: a terrain layer whose mask carries the source's height (blue), which the world terrain's
/// displacement reads.</item>
/// </list>
/// Sources are never changed. Outputs are made once and kept: delete one to make it again (so tuning survives a rerun).
/// </summary>
public static class VegetationPackConverter
{
    public const string WindShader = "Safe Haven/Tree Wind";
    /// <summary>
    /// Remake prefabs and materials that already exist, in place: same paths and GUIDs, so terrains, set pieces and scenes
    /// that use them keep their references. Off: existing outputs are kept as they are (tuning survives a rerun).
    /// </summary>
    public static bool Rebuild;
    // How far branches bend (fraction of the Tree Wind shader's branch motion) and how much leaf cards flutter.
    private const float BranchFlex = 0.6f;
    private const float LeafFlutter = 0.15f;
    private static readonly string[] BaseNames = { "_BaseColorMap", "_BaseMap", "_MainTex", "_Albedo", "_AlbedoMap" };
    private static readonly string[] NormalNames = { "Normal_vegetation", "_NormalMap", "_BumpMap", "_Normal" };
    private static readonly string[] MaskNames = { "mask_vegetation", "_MaskMap" };
    private static readonly string[] HeightNames = { "_HeightMap", "_Height_Map", "_ParallaxMap" };
    // Shader Graph materials name their slots freely; the textures follow "<name>_basecolor", "<name>_normal" and so on.
    private static readonly string[] NormalSuffixes = { "normal", "normal_gl", "nor_gl", "nrm" };
    private static readonly string[] MaskSuffixes = { "mask", "maskmap", "hdrp_mask" };
    private static readonly string[] HeightSuffixes = { "height" };
    private static readonly string[] FoliageWords = { "leaf", "leaves", "needle", "frond", "branches", "fern", "grass", "plant", "billboard", "foliage", "moss_cover", "clover", "weed", "flower" };

    /// <summary>How a detail plant moves and shines on the Grass shader.</summary>
    public struct DetailLook
    {
        public float wind;
        public float push;
        public float flatten;
        public float normalUp;
        public bool glow;
        public int maxVertices;

        public static DetailLook Plant => new DetailLook { wind = 0.06f, push = 0.25f, flatten = 0.35f, normalUp = 0.45f, maxVertices = 900 };
        public static DetailLook Grass => new DetailLook { wind = 0.1f, push = 0.3f, flatten = 0.5f, normalUp = 0.6f, maxVertices = 400 };
        public static DetailLook Low => new DetailLook { wind = 0.015f, push = 0.06f, flatten = 0.1f, normalUp = 0.5f, maxVertices = 400 };
        // Lies on the ground (needle debris, dry leaves): no sway, lit like the floor it covers.
        public static DetailLook Flat => new DetailLook { wind = 0f, push = 0.02f, flatten = 0f, normalUp = 0.85f, maxVertices = 400 };
    }

    // ---------------------------------------------------------------- trees

    /// <summary>
    /// A wind tree from <paramref name="source"/>, saved as <c>folder/name.prefab</c>. <paramref name="gameplay"/> lends its
    /// components (felling, wood, obstacle) when given; <paramref name="leafless"/> drops every foliage material (a dead snag).
    /// <paramref name="solid"/> false makes a shrub: no trunk, nothing to fell, nothing that blocks. <paramref name="leafFall"/>
    /// is what falls from its crown (empty: the standard leaf fall), in its foliage's own colour.
    /// </summary>
    public static GameObject Tree(string source, string folder, string name, GameObject gameplay, bool leafless = false, bool solid = true,
        float windStrength = 1f, GameObject leafFall = null)
    {
        string path = $"{folder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !Rebuild) return existing;

        EnsureFolder(folder);
        GameObject root = Unpacked(source, name);
        try
        {
            List<MeshRenderer> parts = LodRenderers(root);
            float height = 0f, crown = 0f;
            foreach (MeshRenderer part in FirstLod(root, parts))
            {
                Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(vertex);
                    height = Mathf.Max(height, p.y);
                    crown = Mathf.Max(crown, new Vector2(p.x, p.z).magnitude);
                }
            }

            height = Mathf.Max(height, 0.5f);
            // The artist's own trunk collider when the pack has one: estimating from the mesh catches low branches too.
            float trunk = AuthoredTrunk(root) ?? TrunkRadius(root, FirstLod(root, parts), height);
            float seed = Mathf.Repeat(name.GetHashCode() * 0.6180339f, 1f);
            var made = new Dictionary<Material, Material>();
            int saved = 0;
            foreach (MeshRenderer part in parts.ToArray())
            {
                Material[] sources = part.sharedMaterials;
                bool[] foliage = sources.Select(IsFoliage).ToArray();
                bool[] keep = foliage.Select(leaf => !(leafless && leaf)).ToArray();
                if (!keep.Any(k => k))
                {
                    // A snag's billboard is all foliage: nothing of it is left to draw.
                    parts.Remove(part);
                    continue;
                }

                Mesh baked = BakeToRoot(root.transform, part, keep, (p, submesh, piece) =>
                {
                    float h = Mathf.Clamp01(p.y / height);
                    float r = new Vector2(p.x, p.z).magnitude;
                    float flex = Mathf.Pow(Mathf.Clamp01((r - trunk) / Mathf.Max(0.05f, crown - trunk)), 1.5f) * BranchFlex;
                    bool leaf = foliage[submesh];
                    if (leaf) flex = Mathf.Max(flex, 0.15f * h * BranchFlex);
                    // One phase for each whole piece (a branch, a spray of needles): it swings as one, as wood does. A phase
                    // per vertex sends neighbouring points different ways and the branch ripples like cloth.
                    float phase = Mathf.Repeat(Mathf.Atan2(piece.z, piece.x) / (2f * Mathf.PI) + piece.y / height * 0.35f + seed, 1f);
                    float flutter = leaf ? LeafFlutter * Mathf.Clamp01(flex / BranchFlex * 2f) : 0f;
                    return (new Vector2(Mathf.Pow(h, 1.5f), flex), new Vector2(phase, flutter), Color.clear);
                });
                // Room for the crown to sway, sideways and up only: tools set objects on the ground by their bounds' bottom.
                Bounds bounds = baked.bounds;
                float sway = height * 0.08f;
                bounds.SetMinMax(bounds.min - new Vector3(sway, 0f, sway), bounds.max + new Vector3(sway, sway, sway));
                baked.bounds = bounds;
                baked = SaveMesh(baked, $"{folder}/Meshes/{name} {saved++} {part.name}.asset");
                part.GetComponent<MeshFilter>().sharedMesh = baked;

                var materials = new List<Material>();
                for (int i = 0; i < sources.Length; i++)
                {
                    if (!keep[i] || sources[i] == null) continue;
                    if (!made.TryGetValue(sources[i], out Material wind))
                    {
                        string kind = foliage[i] ? (Words(sources[i].name, "leaf", "leaves", "frond") ? "Leaves" : "Needles") : "Bark";
                        wind = WindMaterial(sources[i], $"{folder}/Materials/{name} {kind} {made.Count + 1}.mat", foliage[i], height, windStrength);
                        made[sources[i]] = wind;
                    }

                    materials.Add(wind);
                }

                part.sharedMaterials = materials.ToArray();
                part.shadowCastingMode = ShadowCastingMode.On;
            }

            ClearExtras(root, parts);
            if (root.TryGetComponent(out LODGroup detail)) KeepDetailLonger(detail);
            if (solid)
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = Mathf.Clamp(trunk, 0.07f, 0.8f);
                capsule.height = Mathf.Clamp(height * 0.5f, 2f, 10f);
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                CopyGameplay(gameplay, root, capsule);
            }

            Material leaves = made.Values.FirstOrDefault(m => m.GetFloat("_AlphaClip") > 0.5f);
            if (leaves != null)
            {
                root.AddComponent<TreeLeafLook>().Configure(leafFall, FoliageColour(leaves));
            }

            return Save(root, path);
        }
        finally
        {
            Discard(root);
        }
    }

    private static Material WindMaterial(Material source, string path, bool leaf, float height, float strength)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null && !Rebuild) return material;
        Material kept = material;

        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        material = new Material(Shader.Find(WindShader)) { name = Path.GetFileNameWithoutExtension(path) };
        bool seedMesh = IsSeedMesh(source);
        material.SetTexture("_BaseMap", seedMesh ? TextureOf(source, new[] { "_MainTex" }) ?? TextureOf(source, BaseNames) : TextureOf(source, BaseNames));
        Color tint = seedMesh ? Colour(source, "_Color", Color.white) : Colour(source, "_BaseColor", Color.white);
        // VIVID foliage is a grey frond coloured by its shader's healthy and dry tints.
        Vector4 healthy = Vector(source, "_HealthyColor", new Vector4(float.NaN, 0f, 0f, 0f));
        if (!seedMesh && !float.IsNaN(healthy.x))
        {
            Vector4 dry = Vector(source, "_DryColor", healthy);
            tint = Color.Lerp((Color)healthy, (Color)dry, 0.25f);
        }
        material.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
        Texture normal = NormalMap(source);
        material.SetTexture("_BumpMap", normal);
        float normalScale = seedMesh ? Number(source, new[] { "Normal_intensity_vegetation" }, 1f) : Number(source, new[] { "_NormalScale", "_BumpScale" }, 1f);
        material.SetFloat("_BumpScale", normal != null ? Mathf.Clamp(normalScale, 0.2f, 2f) : 0f);
        material.SetFloat("_Metallic", 0f);
        Texture mask = MaskMap(source);
        if (mask != null)
        {
            // HDRP and SeedMesh masks: metallic R, occlusion G, smoothness A.
            material.SetTexture("_ORMMap", mask);
            material.SetFloat("_MaskHDRP", 1f);
            Keyword(material, "_MASK_HDRP", true);
            // VIVID masks are glossier than SeedMesh's: at the same setting their leaves mirror the sky and turn cyan.
            material.SetFloat("_Smoothness", leaf ? (seedMesh ? 0.6f : 0.2f) : 0.8f);
            material.SetFloat("_OcclusionStrength", leaf ? 0.5f : 1f);
            material.SetFloat("_Metallic", 1f);
        }
        else
        {
            material.SetFloat("_OcclusionStrength", 0f);
            material.SetFloat("_Roughness", Mathf.Clamp(1f - Number(source, new[] { "_SmoothnessRemapMax", "_Smoothness", "_Glossiness" }, 0.2f), 0.55f, 1f));
        }

        material.SetFloat("_AlphaClip", leaf ? 1f : 0f);
        float cutoff = seedMesh ? Number(source, new[] { "_cutoff" }, 0.15f) : Number(source, new[] { "_AlphaCutoff", "_Cutoff", "_AlphaClipThreshold" }, 0.25f);
        material.SetFloat("_Cutoff", leaf ? Mathf.Clamp(cutoff, 0.1f, 0.4f) : 0.5f);
        if (leaf) KeepLeafCoverage(material.GetTexture("_BaseMap"), material.GetFloat("_Cutoff"));
        // Full lighting (normal map, light through the leaf) for these leaves: fast per-vertex lighting made the crowns flat and thin.
        material.SetFloat("_FastLeafLighting", 0f);
        material.SetFloat("_Translucency", leaf ? 0.55f : 0f);
        material.SetFloat("_Cull", leaf ? (float)CullMode.Off : (float)CullMode.Back);
        Keyword(material, "_ALPHATEST_ON", leaf);
        Keyword(material, "_FAST_LEAF_LIGHTING", false);
        if (!leaf) Moss(source, material);
        material.renderQueue = leaf ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        material.SetOverrideTag("RenderType", leaf ? "TransparentCutout" : "Opaque");
        material.SetFloat("_WindStrength", strength);
        material.SetFloat("_TreeHeight", height);
        material.enableInstancing = true;
        SaveMaterial(material, kept, path);
        return kept != null ? kept : material;
    }

    /// <summary>
    /// Smaller mips of a leaf texture keep as much of the card opaque as the full one, so crowns don't thin out to
    /// sticks with distance (alpha-tested leaves lose coverage in every mip otherwise).
    /// </summary>
    public static void KeepLeafCoverage(Texture texture, float cutoff)
    {
        if (texture == null || !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer)) return;
        if (importer.mipMapsPreserveCoverage && Mathf.Approximately(importer.alphaTestReferenceValue, cutoff)) return;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = cutoff;
        importer.SaveAndReimport();
    }

    /// <summary>Holds each finer level of detail to a smaller share of the screen: a top-down camera sees crowns, not silhouettes.</summary>
    public static void KeepDetailLonger(LODGroup group, float scale = 0.55f)
    {
        LOD[] lods = group.GetLODs();
        for (int i = 0; i < lods.Length; i++) lods[i].screenRelativeTransitionHeight *= scale;
        group.SetLODs(lods);
    }

    // The lowest upright capsule the pack placed on the trunk, in the root's space; null when it has none.
    private static float? AuthoredTrunk(GameObject root)
    {
        CapsuleCollider trunk = root.GetComponentsInChildren<CapsuleCollider>(true)
            .Where(c => c.direction == 1 && Vector3.Dot(c.transform.up, Vector3.up) > 0.9f)
            .OrderBy(c => c.transform.TransformPoint(c.center).y).FirstOrDefault();
        if (trunk == null) return null;
        Vector3 scale = trunk.transform.lossyScale;
        float radius = trunk.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        return radius > 0.02f ? radius : (float?)null;
    }

    /// <summary>The average colour of a leaf material where it is drawn (past its cutoff), tinted: what its falling leaves look like.</summary>
    public static Color FoliageColour(Material leaves)
    {
        Texture texture = leaves.GetTexture("_BaseMap");
        Color tint = leaves.HasProperty("_BaseColor") ? leaves.GetColor("_BaseColor") : Color.white;
        if (texture == null) return new Color(0.31f, 0.32f, 0.2f);
        float cutoff = leaves.HasProperty("_Cutoff") ? leaves.GetFloat("_Cutoff") : 0.3f;
        Color sum = Color.clear;
        int count = 0;
        foreach (Color pixel in Read(texture, 64, Color.gray))
        {
            if (pixel.a <= cutoff) continue;
            sum += pixel;
            count++;
        }

        Color average = count > 0 ? (sum / count).gamma : new Color(0.31f, 0.32f, 0.2f);
        return new Color(average.r * tint.r, average.g * tint.g, average.b * tint.b, 1f);
    }

    // The trunk's radius at chest height (above the root flare): what a capsule should hug.
    private static float TrunkRadius(GameObject root, IEnumerable<MeshRenderer> lod0, float height)
    {
        var distances = new List<float>();
        float from = Mathf.Min(1f, height * 0.12f), to = Mathf.Min(2f, height * 0.25f);
        foreach (MeshRenderer part in lod0)
        {
            Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
            Material[] materials = part.sharedMaterials;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
            {
                if (IsFoliage(materials[s])) continue;
                foreach (int index in mesh.GetTriangles(s))
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(vertices[index]);
                    if (p.y > from && p.y < to) distances.Add(new Vector2(p.x, p.z).magnitude);
                }
            }
        }

        if (distances.Count == 0) return 0.3f;
        distances.Sort();
        return distances[distances.Count * 3 / 4];
    }

    // ---------------------------------------------------------------- props

    /// <summary>
    /// A rock, log or stump on the tessellated mesh shader. <paramref name="solid"/>: a capsule (upright, or along x for
    /// something lying down) and the template's obstacle, so the player and creatures go round it.
    /// </summary>
    public static GameObject Prop(string source, string folder, string name, bool solid, GameObject template = null, float heightRange = 0.03f, GameObject node = null)
    {
        string path = $"{folder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !Rebuild) return existing;

        EnsureFolder(folder);
        GameObject root = Unpacked(source, name);
        try
        {
            List<MeshRenderer> parts = LodRenderers(root);
            Bounds bounds = BoundsOf(root, FirstLod(root, parts));
            bool lying = Mathf.Max(bounds.size.x, bounds.size.z) > Mathf.Max(1.6f * bounds.size.y, 1.2f);
            // Lying things are turned to run along x: the navmesh's trunk shapes assume it.
            Quaternion turn = lying && bounds.size.z > bounds.size.x ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            Vector3 shift = -(turn * new Vector3(bounds.center.x, 0f, bounds.center.z));
            int saved = 0;
            // Without a LOD group the terrain only draws a renderer on the root, so every part is merged into one there.
            bool merge = !root.TryGetComponent(out LODGroup _);
            Material fallback = parts.SelectMany(p => p.sharedMaterials).FirstOrDefault(m => m != null);
            foreach (MeshRenderer part in parts)
            {
                Mesh baked = BakeToRoot(root.transform, part, null, null, turn, shift);
                if (!merge) baked = SaveMesh(baked, $"{folder}/Meshes/{name} {saved++} {part.name}.asset");
                part.GetComponent<MeshFilter>().sharedMesh = baked;
                part.sharedMaterials = part.sharedMaterials.Select(m => SurfaceMaterial(m != null ? m : fallback, folder, heightRange)).ToArray();
                if (!merge && part.GetComponent<MeshSurface>() == null) part.gameObject.AddComponent<MeshSurface>();
            }

            if (merge)
            {
                // One sub-mesh per material, not per piece: a pile of 30 stones is one draw, not 30.
                var groups = new Dictionary<Material, List<CombineInstance>>();
                foreach (MeshRenderer part in parts)
                {
                    Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        Material material = part.sharedMaterials[Mathf.Min(s, part.sharedMaterials.Length - 1)];
                        if (!groups.TryGetValue(material, out List<CombineInstance> group)) groups[material] = group = new List<CombineInstance>();
                        group.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = Matrix4x4.identity });
                    }
                }

                var pieces = new List<CombineInstance>();
                foreach (List<CombineInstance> group in groups.Values)
                {
                    var joined = new Mesh { indexFormat = IndexFormat.UInt32 };
                    joined.CombineMeshes(group.ToArray(), true, false);
                    pieces.Add(new CombineInstance { mesh = joined, transform = Matrix4x4.identity });
                }

                var merged = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                merged.CombineMeshes(pieces.ToArray(), false, false);
                merged = SaveMesh(merged, $"{folder}/Meshes/{name}.asset");
                foreach (MeshRenderer part in parts.Where(p => p.gameObject != root)) Object.DestroyImmediate(part.gameObject);
                if (!root.TryGetComponent(out MeshFilter filter)) filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = merged;
                if (!root.TryGetComponent(out MeshRenderer drawn)) drawn = root.AddComponent<MeshRenderer>();
                drawn.sharedMaterials = groups.Keys.ToArray();
                if (root.GetComponent<MeshSurface>() == null) root.AddComponent<MeshSurface>();
                parts = new List<MeshRenderer> { drawn };
            }

            ClearExtras(root, parts);
            if (solid)
            {
                Bounds turned = BoundsOf(root, FirstLod(root, parts));
                var capsule = root.AddComponent<CapsuleCollider>();
                if (lying)
                {
                    capsule.direction = 0;
                    capsule.radius = Mathf.Max(0.12f, 0.5f * Mathf.Min(turned.size.y, turned.size.z));
                    capsule.height = Mathf.Max(turned.size.x * 0.9f, capsule.radius * 2f);
                    capsule.center = new Vector3(0f, capsule.radius, 0f);
                }
                else
                {
                    capsule.direction = 1;
                    capsule.radius = Mathf.Max(0.15f, 0.4f * Mathf.Min(turned.size.x, turned.size.z));
                    capsule.height = Mathf.Max(turned.size.y, capsule.radius * 2f);
                    capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                }

                CopyGameplay(template, root, capsule);
                // A node's gathering (pickaxe, yield, respawn), painted on a terrain it is mined through TerrainNodeHarvesting.
                CopyGameplay(node, root, capsule);
                if (root.TryGetComponent(out ResourceNode gathered))
                {
                    var nodeData = new SerializedObject(gathered);
                    nodeData.FindProperty("keepOwnModel").boolValue = true;
                    nodeData.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return Save(root, path);
        }
        finally
        {
            Discard(root);
        }
    }

    private static Material SurfaceMaterial(Material source, string folder, float heightRange)
    {
        string path = $"{folder}/Materials/{source.name} Surface.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null && !Rebuild) return material;
        Material kept = material;

        EnsureFolder($"{folder}/Materials");
        material = new Material(Shader.Find(MeshSurface.ShaderName)) { name = source.name + " Surface" };
        material.SetTexture("_BaseMap", BaseMap(source));
        material.SetColor("_BaseColor", Colour(source, "_BaseColor", Color.white));
        Texture normal = NormalMap(source);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", normal != null ? Number(source, new[] { "_NormalScale", "_BumpScale" }, 1f) : 0f);
        Texture mask = MaskMap(source);
        // HDRP and VIVID masks: metallic R, occlusion G, smoothness A, which the mesh shader reads as they are.
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetTexture("_OcclusionMap", mask);
        material.SetFloat("_OcclusionStrength", mask != null ? 1f : 0f);
        material.SetFloat("_Metallic", mask != null ? 1f : 0f);
        material.SetFloat("_Smoothness", mask != null ? Number(source, new[] { "_SmoothnessRemapMax" }, 1f) : Mathf.Min(0.35f, Number(source, new[] { "_Smoothness" }, 0.25f)));
        Texture height = HeightMap(source);
        material.SetTexture("_HeightMap", height);
        material.SetFloat("_HeightStrength", height != null ? heightRange : 0f);
        material.SetVector("_HeightChannel", new Vector4(1f, 0f, 0f, 0f));
        bool clip = Number(source, new[] { "_AlphaCutoffEnable", "_AlphaClip" }, 0f) > 0.5f;
        material.SetFloat("_AlphaClip", clip ? 1f : 0f);
        Keyword(material, "_ALPHATEST_ON", clip);
        material.SetFloat("_Cull", clip ? (float)CullMode.Off : (float)CullMode.Back);
        material.enableInstancing = true;
        SaveMaterial(material, kept, path);
        return kept != null ? kept : material;
    }

    // ---------------------------------------------------------------- details

    /// <summary>A terrain detail mesh on the Grass shader, from the lightest LOD that still looks like the plant.</summary>
    public static GameObject Detail(string source, string folder, string name, DetailLook look)
    {
        string path = $"{folder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !Rebuild) return existing;

        EnsureFolder(folder);
        GameObject root = Unpacked(source, name);
        try
        {
            MeshRenderer part = LightestFitting(root, look.maxVertices);
            // A plant drawn by one material: its foliage, without stems or bark when it has both (they would take the leaf texture).
            Material[] materials = part.sharedMaterials;
            bool[] leafy = materials.Select(IsFoliage).ToArray();
            bool[] keep = leafy.Any(l => l) ? leafy : null;
            Material sourceMaterial = keep != null ? materials.Where((m, i) => leafy[i]).First() : materials.FirstOrDefault(m => m != null);
            float height = Mathf.Max(0.05f, BoundsOf(root, new[] { part }).max.y);
            float shade = Mathf.Repeat(name.GetHashCode() * 0.3819660f, 1f);
            Mesh baked = BakeToRoot(root.transform, part, keep, (p, submesh, piece) =>
            {
                float bend = Mathf.Pow(Mathf.Clamp01(p.y / height), 1.1f);
                float glow = look.glow ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, p.y / height)) : 0f;
                return (Vector2.zero, Vector2.zero, new Color(bend, shade, glow, 0f));
            });
            // One material for the whole plant: the Grass shader draws a single sub-mesh per detail.
            baked.SetTriangles(Enumerable.Range(0, baked.subMeshCount).SelectMany(s => baked.GetTriangles(s)).ToArray(), 0);
            baked.subMeshCount = 1;
            baked.RecalculateBounds();
            baked = SaveMesh(baked, $"{folder}/Meshes/{name}.asset");

            var prefab = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prefab, root.scene);
            try
            {
                prefab.AddComponent<MeshFilter>().sharedMesh = baked;
                var renderer = prefab.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = DetailMaterial(sourceMaterial, $"{folder}/Materials/{name}.mat", look);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                return Save(prefab, path);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }
        finally
        {
            Discard(root);
        }
    }

    private static Material DetailMaterial(Material source, string path, DetailLook look)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null && !Rebuild) return material;
        Material kept = material;

        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        material = new Material(Shader.Find(GrassSetup.ShaderName)) { name = Path.GetFileNameWithoutExtension(path) };
        material.SetTexture("_BaseMap", source != null ? BaseMap(source) : null);
        float cutoff = source != null ? Mathf.Clamp(IsSeedMesh(source) ? Number(source, new[] { "_cutoff" }, 0.2f) : Number(source, new[] { "_AlphaCutoff", "_Cutoff" }, 0.3f), 0.1f, 0.4f) : 0.3f;
        material.SetFloat("_Cutoff", cutoff);
        KeepLeafCoverage(material.GetTexture("_BaseMap"), cutoff);
        material.SetColor("_BottomColor", Color.white * 0.85f);
        material.SetColor("_TopColor", Color.white);
        material.SetFloat("_GroundColor", 0.08f);
        material.SetFloat("_GroundTip", 0f);
        material.SetFloat("_DryAmount", 0f);
        material.SetFloat("_BladeVariation", 0.12f);
        material.SetFloat("_ClumpHeight", 0.15f);
        material.SetFloat("_FacingBlades", 0f);
        material.SetFloat("_NormalUp", look.normalUp);
        material.SetFloat("_Translucency", 0.4f);
        material.SetFloat("_WindStrength", look.wind);
        material.SetFloat("_PushStrength", look.push);
        material.SetFloat("_Flatten", look.flatten);
        if (look.glow) material.SetColor("_GlowColor", GrassSetup.GlowColour);
        material.enableInstancing = true;
        SaveMaterial(material, kept, path);
        return kept != null ? kept : material;
    }

    private static MeshRenderer LightestFitting(GameObject root, int maxVertices)
    {
        if (root.TryGetComponent(out LODGroup group) && group.lodCount > 0)
        {
            foreach (LOD lod in group.GetLODs())
            {
                MeshRenderer fit = lod.renderers.OfType<MeshRenderer>().FirstOrDefault(r => r != null && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null && f.sharedMesh.vertexCount <= maxVertices);
                if (fit != null) return fit;
            }
        }

        return root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null)
            .OrderBy(r => r.GetComponent<MeshFilter>().sharedMesh.vertexCount).First();
    }

    // ---------------------------------------------------------------- ground

    /// <summary>
    /// A terrain layer from <paramref name="source"/>, with a mask the world terrain reads (metallic R, occlusion G,
    /// height B, smoothness A) whose height comes from the source's height map, so the ground is displaced by its shapes.
    /// </summary>
    public static TerrainLayer Ground(string source, string folder, string name, int maskSize = 2048)
    {
        string path = $"{folder}/{name}.terrainlayer";
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        // Never remade in place: a new layer asset would leave the terrain and growth pointing at a deleted one.
        if (existing != null) return existing;

        EnsureFolder(folder);
        var from = AssetDatabase.LoadAssetAtPath<TerrainLayer>(source);
        if (from == null) throw new FileNotFoundException("No terrain layer at " + source);

        Color[] mask = Read(from.maskMapTexture, maskSize, new Color(0f, 1f, 0.5f, 0.2f));
        Color[] heights = SourceHeights(from, maskSize);
        var packed = new Texture2D(maskSize, maskSize, TextureFormat.RGBA32, false, true);
        var pixels = new Color[mask.Length];
        Vector4 min = from.maskMapRemapMin, max = from.maskMapRemapMax;
        for (int i = 0; i < pixels.Length; i++)
        {
            float occlusion = Mathf.Lerp(min.y, max.y, mask[i].g);
            float smooth = Mathf.Lerp(min.w, max.w, mask[i].a);
            pixels[i] = new Color(0f, Mathf.Clamp01(occlusion), heights[i].r, Mathf.Clamp01(smooth));
        }

        packed.SetPixels(pixels);
        packed.Apply(false);
        string maskPath = $"{folder}/{name}_Mask.png";
        File.WriteAllBytes(maskPath, packed.EncodeToPNG());
        Object.DestroyImmediate(packed);
        ImportMask(maskPath, maskSize);

        var layer = new TerrainLayer
        {
            name = name,
            diffuseTexture = from.diffuseTexture,
            normalMapTexture = from.normalMapTexture,
            normalScale = from.normalScale,
            maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath),
            tileSize = from.tileSize,
            tileOffset = from.tileOffset,
            diffuseRemapMin = from.diffuseRemapMin,
            diffuseRemapMax = from.diffuseRemapMax,
            maskMapRemapMin = Vector4.zero,
            maskMapRemapMax = Vector4.one,
            metallic = 0f,
            smoothness = from.smoothness,
        };
        AssetDatabase.CreateAsset(layer, path);
        return layer;
    }

    /// <summary>Rewrites the height (blue) of a converted ground's mask from its source's height map, keeping the rest:
    /// for grounds converted before heights were read correctly. The mask keeps its file and GUID.</summary>
    public static string RebuildHeight(TerrainLayer converted, string source)
    {
        var from = AssetDatabase.LoadAssetAtPath<TerrainLayer>(source);
        if (from == null || converted == null || converted.maskMapTexture == null) return converted?.name + ": nothing to rebuild";
        string maskPath = AssetDatabase.GetAssetPath(converted.maskMapTexture);
        int size = converted.maskMapTexture.width;
        Color[] mask = ReadRaw(maskPath, size);
        Color[] heights = SourceHeights(from, size);
        for (int i = 0; i < mask.Length; i++) mask[i].b = heights[i].r;
        var packed = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        packed.SetPixels(mask);
        packed.Apply(false);
        File.WriteAllBytes(maskPath, packed.EncodeToPNG());
        Object.DestroyImmediate(packed);
        ImportMask(maskPath, size);
        return $"{converted.name}: height from {Path.GetFileName(HeightMapFor(from) ?? "the pack's mask")}";
    }

    // Uncompressed: block compression turns a height map into steps, which tessellation shows as ridges.
    private static void ImportMask(string maskPath, int size)
    {
        AssetDatabase.ImportAsset(maskPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);
        importer.sRGBTexture = false;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// The pack's own height map for a terrain layer: a texture with "height" in its name, beside the layer or its colour
    /// texture, whose name shares the most with the colour texture's (packs keep them apart: Mountain Lake's layers are in
    /// TerrainLayers, their textures in Textures). Null when there is none.
    /// </summary>
    public static string HeightMapFor(TerrainLayer from)
    {
        string diffusePath = from.diffuseTexture != null ? AssetDatabase.GetAssetPath(from.diffuseTexture) : null;
        string diffuseName = diffusePath != null ? Path.GetFileNameWithoutExtension(diffusePath).ToLowerInvariant() : "";
        var folders = new[] { Path.GetDirectoryName(AssetDatabase.GetAssetPath(from)), diffusePath != null ? Path.GetDirectoryName(diffusePath) : null }
            .Where(f => f != null).Select(f => f.Replace('\\', '/')).Distinct();
        string best = null;
        int bestShared = 0;
        foreach (string folder in folders)
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                string stem = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (!stem.Contains("height") || extension != ".png" && extension != ".jpg") continue;
                int shared = 0;
                while (shared < stem.Length && shared < diffuseName.Length && stem[shared] == diffuseName[shared]) shared++;
                bool beside = folder == Path.GetDirectoryName(AssetDatabase.GetAssetPath(from)).Replace('\\', '/');
                if (beside) shared = Mathf.Max(shared, 6); // the layer's own folder: its height, whatever the names
                if (shared >= 6 && shared > bestShared) { best = file.Replace('\\', '/'); bestShared = shared; }
            }
        }

        return best;
    }

    /// <summary>The layer's heights (0-1 in red), as authored. From its height map, read straight from the file: packs import
    /// height maps as colour (sRGB), and through that gamma curve the hollows flatten and the peaks turn sharp. Without
    /// one, the pack's mask blue as stored; the mask's height remap is HDRP's blend amplitude and base (centimetres),
    /// which only ranks layers for height blending, not a range to stretch heights to.</summary>
    private static Color[] SourceHeights(TerrainLayer from, int size)
    {
        string height = HeightMapFor(from);
        if (height != null) return ReadRaw(height, size);
        if (from.maskMapTexture == null) return Enumerable.Repeat(Color.gray, size * size).ToArray();
        Color[] mask = ReadRaw(AssetDatabase.GetAssetPath(from.maskMapTexture), size);
        for (int i = 0; i < mask.Length; i++) mask[i].r = mask[i].b;
        return mask;
    }

    /// <summary>A PNG or JPG's pixels exactly as stored (no colour-space conversion), resampled to size x size.</summary>
    private static Color[] ReadRaw(string path, int size)
    {
        var file = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            if (!file.LoadImage(File.ReadAllBytes(path), false)) throw new IOException("Can't read " + path);
            file.wrapMode = TextureWrapMode.Repeat;
            return Read(file, size, Color.gray);
        }
        finally
        {
            Object.DestroyImmediate(file);
        }
    }

    private static Color[] Read(Texture texture, int size, Color fallback)
    {
        if (texture == null) return Enumerable.Repeat(fallback, size * size).ToArray();
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readback.Apply(false);
            return readback.GetPixels();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(readback);
        }
    }

    // ---------------------------------------------------------------- shared

    // Works in its own preview scene, so converting never touches (or dirties) the scenes that are open.
    private static GameObject Unpacked(string source, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (prefab == null) throw new FileNotFoundException("No prefab at " + source);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.NewPreviewScene());
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = name;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        root.layer = 0;
        GameObjectUtility.SetStaticEditorFlags(root, 0);
        MergeNestedLods(root);
        return root;
    }

    /// <summary>
    /// A model built from several LOD groups (a maple's trunk and branches, a rock composite's stones) gets one group on
    /// its root: each level holds every child group's renderers of that level (or their last, when a group has fewer).
    /// The child groups go; what no level draws (glow cards, shadow proxies) is left out.
    /// </summary>
    private static void MergeNestedLods(GameObject root)
    {
        if (root.TryGetComponent(out LODGroup _)) return;
        LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
        if (groups.Length == 0) return;

        int levels = groups.Max(g => g.lodCount);
        LOD[] first = groups.OrderByDescending(g => g.lodCount).First().GetLODs();
        var merged = new LOD[levels];
        for (int level = 0; level < levels; level++)
        {
            var renderers = new List<Renderer>();
            foreach (LODGroup group in groups)
            {
                LOD[] lods = group.GetLODs();
                if (lods.Length == 0) continue;
                renderers.AddRange(lods[Mathf.Min(level, lods.Length - 1)].renderers.Where(r => r != null));
            }

            merged[level] = new LOD(first[level].screenRelativeTransitionHeight, renderers.Distinct().ToArray());
        }

        foreach (LODGroup group in groups) Object.DestroyImmediate(group);
        LODGroup made = root.AddComponent<LODGroup>();
        made.SetLODs(merged);
        made.RecalculateBounds();
    }

    // The renderers the LOD group draws, or every mesh renderer when there is none; never the hidden collider meshes or
    // shadow-only proxies (a pack's cheap shadow caster: drawn with a real material it would show twice).
    private static List<MeshRenderer> LodRenderers(GameObject root)
    {
        IEnumerable<Renderer> all = root.TryGetComponent(out LODGroup group) && group.lodCount > 0
            ? group.GetLODs().SelectMany(lod => lod.renderers)
            : root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.GetComponent<Collider>() == null);
        return all.OfType<MeshRenderer>().Where(r => r != null && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null
            && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !r.name.Contains("ShadowProxy")).Distinct().ToList();
    }

    private static IEnumerable<MeshRenderer> FirstLod(GameObject root, List<MeshRenderer> parts) =>
        root.TryGetComponent(out LODGroup group) && group.lodCount > 0 ? group.GetLODs()[0].renderers.OfType<MeshRenderer>().Where(r => r != null) : parts;

    private static Bounds BoundsOf(GameObject root, IEnumerable<MeshRenderer> parts)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshRenderer part in parts)
        {
            if (part == null || !part.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(vertex);
                if (any) bounds.Encapsulate(p);
                else bounds = new Bounds(p, Vector3.zero);
                any = true;
            }
        }

        return bounds;
    }

    /// <summary>
    /// A copy of a part's mesh in the root's space (its renderer then sits on the root's origin, unscaled), optionally turned
    /// and shifted, keeping only the sub-meshes in <paramref name="keep"/>, with per-vertex wind or colour from <paramref name="paint"/>.
    /// </summary>
    private static Mesh BakeToRoot(Transform root, MeshRenderer part, bool[] keep, Func<Vector3, int, Vector3, (Vector2 wind, Vector2 phase, Color colour)> paint,
        Quaternion? turn = null, Vector3 shift = default)
    {
        Mesh source = part.GetComponent<MeshFilter>().sharedMesh;
        Matrix4x4 toRoot = Matrix4x4.TRS(shift, turn ?? Quaternion.identity, Vector3.one) * root.worldToLocalMatrix * part.transform.localToWorldMatrix;
        Matrix4x4 normalsToRoot = toRoot.inverse.transpose;
        var mesh = new Mesh { name = root.name + " " + part.name, indexFormat = source.indexFormat };
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = toRoot.MultiplyPoint3x4(vertices[i]);
            if (i < normals.Length) normals[i] = normalsToRoot.MultiplyVector(normals[i]).normalized;
            if (i < tangents.Length)
            {
                Vector3 t = toRoot.MultiplyVector(tangents[i]).normalized;
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
        }

        mesh.vertices = vertices;
        if (normals.Length == vertices.Length) mesh.normals = normals;
        if (tangents.Length == vertices.Length) mesh.tangents = tangents;
        var uv = new List<Vector2>();
        source.GetUVs(0, uv);
        if (uv.Count == vertices.Length) mesh.SetUVs(0, uv);
        if (source.colors.Length == vertices.Length && paint == null) mesh.colors = source.colors;

        var submeshes = new List<int[]>();
        var owner = new int[vertices.Length];
        for (int s = 0; s < source.subMeshCount; s++)
        {
            if (keep != null && s < keep.Length && !keep[s]) continue;
            int[] triangles = source.GetTriangles(s);
            foreach (int index in triangles) owner[index] = s;
            submeshes.Add(triangles);
        }

        mesh.subMeshCount = submeshes.Count;
        for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s, false);

        if (paint != null)
        {
            Vector3[] centres = PieceCentres(vertices, submeshes);
            var wind = new Vector2[vertices.Length];
            var phase = new Vector2[vertices.Length];
            var colours = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                (wind[i], phase[i], colours[i]) = paint(vertices[i], owner[i], centres[i]);
            }

            mesh.SetUVs(1, wind);
            mesh.SetUVs(2, phase);
            mesh.colors = colours;
        }

        mesh.RecalculateBounds();
        if (part.transform != root)
        {
            part.transform.SetParent(root, false);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one;
        }

        return mesh;
    }

    /// <summary>
    /// The centre of the connected piece each vertex belongs to (joined by triangles, or by sharing a position across a UV
    /// seam). Vertices drawn by no triangle are their own piece.
    /// </summary>
    private static Vector3[] PieceCentres(Vector3[] vertices, List<int[]> triangles)
    {
        var parent = new int[vertices.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i)
        {
            while (parent[i] != i) i = parent[i] = parent[parent[i]];
            return i;
        }

        void Join(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b) parent[b] = a;
        }

        foreach (int[] list in triangles)
        {
            for (int t = 0; t + 2 < list.Length; t += 3)
            {
                Join(list[t], list[t + 1]);
                Join(list[t + 1], list[t + 2]);
            }
        }

        var welded = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 1000f);
            if (welded.TryGetValue(key, out int first)) Join(first, i);
            else welded[key] = i;
        }

        var sums = new Dictionary<int, (Vector3 sum, int count)>();
        for (int i = 0; i < vertices.Length; i++)
        {
            int piece = Find(i);
            sums.TryGetValue(piece, out var total);
            sums[piece] = (total.sum + vertices[i], total.count + 1);
        }

        var centres = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            (Vector3 sum, int count) = sums[Find(i)];
            centres[i] = sum / count;
        }

        return centres;
    }

    // Everything but the drawn parts and the LOD group goes: colliders, helper meshes, package scripts, empty holders.
    private static void ClearExtras(GameObject root, List<MeshRenderer> parts)
    {
        var kept = new HashSet<GameObject>(parts.Select(p => p.gameObject)) { root };
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true).Reverse())
        {
            if (node == null) continue;
            if (!kept.Contains(node.gameObject))
            {
                Object.DestroyImmediate(node.gameObject);
                continue;
            }

            foreach (Component component in node.GetComponents<Component>())
            {
                if (component is Transform || component is MeshFilter || component is MeshRenderer || component is LODGroup || component is MeshSurface) continue;
                Object.DestroyImmediate(component);
            }
        }

        if (root.TryGetComponent(out LODGroup group))
        {
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++) lods[i].renderers = lods[i].renderers.Where(r => r != null).ToArray();
            group.SetLODs(lods);
            group.RecalculateBounds();
        }
    }

    // The template's gameplay (felling, wood, the obstacle), fitted to this trunk.
    private static void CopyGameplay(GameObject template, GameObject root, CapsuleCollider capsule)
    {
        if (template == null) return;
        foreach (Component component in template.GetComponents<Component>())
        {
            if (component is Transform || component is MeshFilter || component is MeshRenderer || component is Collider || component is LODGroup) continue;
            // A placed node's own identity and blight decal: wrong on a painted rock that thousands share.
            if (component.GetType().Name == "SaveableEntity" || component.GetType().Name == "Overgrowth" || root.GetComponent(component.GetType()) != null) continue;
            UnityEditorInternal.ComponentUtility.CopyComponent(component);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(root);
        }

        if (root.TryGetComponent(out NavMeshObstacle obstacle))
        {
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = capsule.center;
            obstacle.radius = capsule.direction == 1 ? capsule.radius : capsule.height * 0.5f;
            obstacle.height = capsule.direction == 1 ? capsule.height : capsule.radius * 2f;
        }
    }

    private static void Discard(GameObject root)
    {
        UnityEngine.SceneManagement.Scene scene = root.scene;
        Object.DestroyImmediate(root);
        if (EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene);
    }

    // A remade material is copied into the one already there, so every reference to it stays.
    private static void SaveMaterial(Material made, Material kept, string path)
    {
        if (kept == null)
        {
            AssetDatabase.CreateAsset(made, path);
            return;
        }

        string name = kept.name;
        EditorUtility.CopySerialized(made, kept);
        kept.name = name;
        EditorUtility.SetDirty(kept);
        Object.DestroyImmediate(made);
    }

    private static GameObject Save(GameObject root, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success) throw new InvalidOperationException("Could not save " + path);
        return saved;
    }

    // Returns the mesh asset to use: the bake itself, or the existing asset it was copied into.
    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Overwritten in place: deleting and recreating left references to it empty until the next import.
        string name = existing.name;
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    public static bool IsFoliage(Material material)
    {
        if (material == null) return false;
        // Leaves are what is cut out by alpha: an alpha-tested material whose colour texture really has alpha. Names lie
        // ("rotten branches" are solid geometry), and so do stale HDRP flags left on Shader Graph materials.
        bool clipped = material.IsKeywordEnabled("_ALPHATEST_ON") || Number(material, new[] { "_AlphaCutoffEnable", "_AlphaClip" }, 0f) > 0.5f;
        Texture baseMap = BaseMap(material);
        bool hasAlpha = baseMap != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(baseMap)) is TextureImporter importer
            && importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None;
        if (IsSeedMesh(material)) return clipped && hasAlpha;
        return hasAlpha && (clipped || Words(material.name, FoliageWords));
    }

    /// <summary>A material made for the SeedMesh vegetation Shader Graphs (Mossy Cedrus Forest): its own property names.</summary>
    public static bool IsSeedMesh(Material material) => TextureOf(material, new[] { "Normal_vegetation", "mask_vegetation" }) != null
        || HasNumber(material, "Normal_intensity_vegetation");

    private static Texture BaseMap(Material material) =>
        IsSeedMesh(material) ? TextureOf(material, new[] { "_MainTex" }) ?? TextureOf(material, BaseNames) : TextureOf(material, BaseNames);

    // The SeedMesh covering (moss on what faces up), on the Tree Wind shader's own moss layer.
    private static void Moss(Material source, Material target)
    {
        if (!IsSeedMesh(source) || Number(source, new[] { "_Covering_sm" }, 0f) < 0.5f) return;
        Texture moss = TextureOf(source, new[] { "_Snow_Base_Color_sm" });
        if (moss == null) return;
        Texture mossNormal = TextureOf(source, new[] { "_Snow_Normal_sm" });
        if (mossNormal != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mossNormal)) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        target.SetTexture("_MossMap", moss);
        target.SetTexture("_MossNormal", mossNormal);
        target.SetTexture("_MossMask", TextureOf(source, new[] { "_Snow_Mask_sm" }));
        Color colour = Colour(source, "_Covering_Color", new Color(0.75f, 0.75f, 0.75f));
        target.SetColor("_MossColor", new Color(colour.r, colour.g, colour.b, 1f));
        Vector4 tiling = Vector(source, "_Covering_Tiling", new Vector4(2f, 2f));
        target.SetFloat("_MossTiling", Mathf.Max(0.5f, tiling.x * Number(source, new[] { "_Covering_Tilling_Multiply_sm" }, 1f)));
        Vector4 remap = Vector(source, "_Overall_Covering_Remap", new Vector4(0f, 1f));
        // The graph's remap of "facing up"; inverted or empty ranges fall back to moss from level to steep.
        if (remap.y <= remap.x + 0.05f) remap = new Vector4(0f, 0.6f);
        target.SetVector("_MossRemap", new Vector4(remap.x, Mathf.Min(remap.y, 0.9f), 0f, 0f));
        target.SetFloat("_MossAmount", 1f);
        target.SetFloat("_MossCover", 1f);
        Keyword(target, "_MOSS_COVER", true);
    }

    private static bool Words(string name, params string[] words)
    {
        string lower = name.ToLowerInvariant();
        return words.Any(lower.Contains);
    }

    private static void Keyword(Material material, string keyword, bool on)
    {
        if (on) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }

    // Saved properties, read even when the source's shader is missing or belongs to another render pipeline.
    private static Texture TextureOf(Material material, string[] names)
    {
        SerializedProperty textures = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        foreach (string name in names)
        {
            for (int i = 0; i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == name && entry.FindPropertyRelative("second.m_Texture").objectReferenceValue is Texture texture)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private static Texture NormalMap(Material material)
    {
        Texture normal = TextureOf(material, NormalNames) ?? Sibling(material, NormalSuffixes);
        if (normal == null || !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) is TextureImporter importer)) return null;
        if (importer.textureType != TextureImporterType.NormalMap)
        {
            // Some packs ship their normal maps imported as colour; drawn so they would light the surface wrongly.
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        return normal;
    }

    private static Texture MaskMap(Material material) => TextureOf(material, MaskNames) ?? Sibling(material, MaskSuffixes);

    private static Texture HeightMap(Material material) => TextureOf(material, HeightNames) ?? Sibling(material, HeightSuffixes);

    // The texture named like the base colour with another ending (Bark_basecolor → Bark_normal), on the material or beside it.
    private static Texture Sibling(Material material, string[] suffixes)
    {
        Texture baseMap = TextureOf(material, BaseNames);
        if (baseMap == null) return null;
        string name = baseMap.name.ToLowerInvariant();
        int cut = name.LastIndexOf('_');
        if (cut <= 0) return null;
        string prefix = name.Substring(0, cut + 1);
        bool Matches(Object texture) => texture != null && suffixes.Any(suffix => texture.name.ToLowerInvariant() == prefix + suffix);

        SerializedProperty textures = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; i < textures.arraySize; i++)
        {
            Object texture = textures.GetArrayElementAtIndex(i).FindPropertyRelative("second.m_Texture").objectReferenceValue;
            if (Matches(texture)) return (Texture)texture;
        }

        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(baseMap)).Replace('\\', '/');
        return AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetDirectoryName(path).Replace('\\', '/') == folder)
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(Matches);
    }

    private static float Number(Material material, string[] names, float fallback)
    {
        SerializedProperty floats = new SerializedObject(material).FindProperty("m_SavedProperties.m_Floats");
        foreach (string name in names)
        {
            for (int i = 0; i < floats.arraySize; i++)
            {
                SerializedProperty entry = floats.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == name) return entry.FindPropertyRelative("second").floatValue;
            }
        }

        return fallback;
    }

    private static Color Colour(Material material, string name, Color fallback)
    {
        SerializedProperty colours = new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
        for (int i = 0; i < colours.arraySize; i++)
        {
            SerializedProperty entry = colours.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name) return entry.FindPropertyRelative("second").colorValue;
        }

        return fallback;
    }

    private static Vector4 Vector(Material material, string name, Vector4 fallback)
    {
        Color value = Colour(material, name, new Color(float.NaN, 0f, 0f, 0f));
        return float.IsNaN(value.r) ? fallback : new Vector4(value.r, value.g, value.b, value.a);
    }

    private static bool HasNumber(Material material, string name) => !float.IsNaN(Number(material, new[] { name }, float.NaN));
}
