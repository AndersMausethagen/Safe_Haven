#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The wood nodes become real, fellable trees (`unity command script run --file AgentScripts/BuildWoodTrees.cs --entry
/// BuildWoodTrees.Apply`). OakWood, Hardwood and Elderwood were primitive stand-ins (a cone and a ball) gathered like a
/// rock; now each is a tree from the Haven's own realistic set (the Sugar Maples: Tree Wind, leaf cards, wind, LODs,
/// ~6.5k triangles), told apart by size and colour so the tier reads at a glance, and it comes down like the forest's
/// trees (ChoppableTree: notch, fall, limbing, cutting up) giving its own wood, every log of it.
/// <list type="bullet">
/// <item>Oak Wood (tier I): summer green, ~10 m.</item>
/// <item>Hardwood (tier II): a hard maple in autumn orange, ~13 m.</item>
/// <item>Elderwood (tier III): an old tree, silver bark and pale leaves that glow faintly, ~15 m.</item>
/// </list>
/// The node prefabs and their nested model prefabs keep their GUIDs (scenes, spawners and saves still find them); the
/// four maples (A-D) become each node's looks (A in the model prefab, B-D as model variants). The generated oaks in
/// Models/Generated didn't match the style (flat neon leaves) and the cedars are 30-80k triangles: too heavy to draw at
/// full detail across the top-down view. Run again after changing a tier; it rebuilds from the maples each time.
/// </summary>
public static class BuildWoodTrees
{
    private const string Maples = "Assets/_Project/Art/Vegetation/Safe Haven/Trees/Sugar Maple ";
    private const string Out = "Assets/_Project/Art/Gathering/Trees/";

    private sealed class Tier
    {
        public string Name;          // "Oak Wood"
        public string Node;          // node prefab
        public string Model;         // nested model prefab (keeps its GUID)
        public string Definition;    // gathering node definition (its model variants)
        public string Wood;          // the item every log is
        public float Height;         // metres, as it stands
        public Color Leaves;         // multiplies the maple's leaf colour
        public Color Bark;           // multiplies the bark
        public Color LeafFall;       // what falls when it's hit (TreeLeafLook)
        public Color LeafGlow;       // Tree Wind _EmissionColor on the leaves (times their colour; black: none)
        public Color BarkGlow;       // and on the bark
    }

    private static readonly Tier[] Tiers =
    {
        new Tier
        {
            Name = "Oak Wood", Node = "Assets/_Project/Prefabs/Resources/OakWoodNode.prefab",
            Model = "Assets/_Project/Environment/ResourceNodes/OakWoodModel.prefab", Definition = "Assets/_Project/Data/Gathering/OakWood.asset",
            Wood = "Assets/_Project/Data/Items/Materials/OakWood.asset", Height = 10f,
            Leaves = new Color(0.92f, 1f, 0.86f), Bark = Color.white, LeafFall = new Color(0.32f, 0.44f, 0.22f),
        },
        new Tier
        {
            Name = "Hardwood", Node = "Assets/_Project/Prefabs/Resources/HardwoodNode.prefab",
            Model = "Assets/_Project/Environment/ResourceNodes/HardwoodModel.prefab", Definition = "Assets/_Project/Data/Gathering/Hardwood.asset",
            Wood = "Assets/_Project/Data/Items/Materials/Hardwood.asset", Height = 13f,
            Leaves = new Color(1.75f, 0.82f, 0.32f), Bark = new Color(0.82f, 0.74f, 0.68f), LeafFall = new Color(0.62f, 0.3f, 0.1f),
        },
        new Tier
        {
            Name = "Elderwood", Node = "Assets/_Project/Prefabs/Resources/ElderwoodNode.prefab",
            Model = "Assets/_Project/Environment/ResourceNodes/ElderwoodModel.prefab", Definition = "Assets/_Project/Data/Gathering/Elderwood.asset",
            Wood = "Assets/_Project/Data/Items/Materials/Elderwood.asset", Height = 15f,
            Leaves = new Color(1.5f, 1.26f, 2.16f), Bark = new Color(1.35f, 1.38f, 1.42f), LeafFall = new Color(0.7f, 0.78f, 0.8f),
            // A faint glow of its own pale colour, so the rare tree is found in the dark like the ores (the user, 2026-11-09).
            LeafGlow = new Color(0.19f, 0.23f, 0.28f), BarkGlow = new Color(0.07f, 0.08f, 0.1f),
        },
    };

    public static string Apply()
    {
        NotReadable();
        if (!AssetDatabase.IsValidFolder(Out.TrimEnd('/')))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Art/Gathering", "Trees");
        }

        var report = new List<string>();
        foreach (Tier tier in Tiers)
        {
            var looks = new List<GameObject>();
            float scale = 1f;
            CapsuleCollider trunk = null;
            GameObject leafFall = null;
            for (int i = 0; i < 4; i++)
            {
                var maple = AssetDatabase.LoadAssetAtPath<GameObject>(Maples + (char)('A' + i) + ".prefab");
                if (i == 0)
                {
                    scale = tier.Height / HeightOf(maple);
                    trunk = maple.GetComponent<CapsuleCollider>();
                    leafFall = maple.TryGetComponent(out TreeLeafLook look) ? (GameObject)new SerializedObject(look).FindProperty("leafFall").objectReferenceValue : null;
                    FillModel(tier, maple, scale);
                }
                else
                {
                    looks.Add(VariantPrefab(tier, maple, i, tier.Height / HeightOf(maple)));
                }
            }

            SetVariants(tier.Definition, looks);
            MakeFellable(tier, trunk, scale, leafFall);
            report.Add($"{tier.Name}: {tier.Height} m, maple A in the model, {looks.Count} more looks");
        }

        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // The maples' meshes were saved Read/Write: a CPU copy of every one, which nothing reads at runtime. Cleared for the
    // four maples (the forest's terrain maples share them and save the memory too).
    private static void NotReadable()
    {
        for (int i = 0; i < 4; i++)
        {
            var maple = AssetDatabase.LoadAssetAtPath<GameObject>(Maples + (char)('A' + i) + ".prefab");
            foreach (MeshFilter filter in maple.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || !AssetDatabase.GetAssetPath(filter.sharedMesh).EndsWith(".asset"))
                {
                    continue;
                }

                var data = new SerializedObject(filter.sharedMesh);
                data.FindProperty("m_IsReadable").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(filter.sharedMesh);
            }
        }
    }

    private static float HeightOf(GameObject prefab)
    {
        float top = float.MinValue, bottom = float.MaxValue;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds b = filter.sharedMesh.bounds;
            Vector3 lo = filter.transform.TransformPoint(b.min) - prefab.transform.position;
            Vector3 hi = filter.transform.TransformPoint(b.max) - prefab.transform.position;
            top = Mathf.Max(top, Mathf.Max(lo.y, hi.y));
            bottom = Mathf.Min(bottom, Mathf.Min(lo.y, hi.y));
        }

        return top > bottom ? top - bottom : 10f;
    }

    // The model prefab keeps its root (and GUID): its children give way to maple A's visual parts, tinted and scaled.
    private static void FillModel(Tier tier, GameObject maple, float scale)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(tier.Model);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            foreach (Collider collider in root.GetComponents<Collider>())
            {
                Object.DestroyImmediate(collider); // the trunk collider is the node's (ChoppableTree reads it there)
            }

            Visual(tier, maple, scale).transform.SetParent(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, tier.Model);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject VariantPrefab(Tier tier, GameObject maple, int index, float scale)
    {
        string path = $"{Out}{tier.Name} {(char)('A' + index)}.prefab";
        GameObject look = Visual(tier, maple, scale);
        try
        {
            return PrefabUtility.SaveAsPrefabAsset(look, path);
        }
        finally
        {
            Object.DestroyImmediate(look);
        }
    }

    // A visual-only copy of the maple (LODGroup and renderers; its own node, collider and tree components dropped),
    // with the tier's tinted materials, at the tier's height.
    private static GameObject Visual(Tier tier, GameObject maple, float scale)
    {
        var copy = (GameObject)Object.Instantiate(maple);
        copy.name = $"{tier.Name} {maple.name.Substring(maple.name.Length - 1)}";
        foreach (Component component in copy.GetComponents<Component>().Reverse())
        {
            if (!(component is Transform) && !(component is LODGroup))
            {
                Object.DestroyImmediate(component);
            }
        }

        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one * scale;
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Tinted(tier, m)).ToArray();
        }

        // Switch like every gathering node: LOD0 across the whole top-down view (the maples' own heights switched a 10 m
        // tree inside it).
        if (copy.TryGetComponent(out LODGroup group))
        {
            LOD[] lods = group.GetLODs();
            Bounds seen = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in lods[0].renderers.Where(r => r != null))
            {
                Bounds b = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                b.size *= scale;
                if (first)
                {
                    seen = b;
                    first = false;
                }
                else
                {
                    seen.Encapsulate(b);
                }
            }

            int[] triangles = lods.Select(l => l.renderers.Where(r => r != null).Sum(r => (int)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3)).ToArray();
            float[] heights = ScreenBudget.LodHeights(seen, triangles, foliage: true);
            for (int i = 0; i < lods.Length; i++)
            {
                lods[i].screenRelativeTransitionHeight = heights[i];
            }

            group.SetLODs(lods);
        }

        return copy;
    }

    private static Material Tinted(Tier tier, Material source)
    {
        if (source == null)
        {
            return null;
        }

        bool leaves = source.name.Contains("Needles") || source.name.Contains("Leaves") || source.IsKeywordEnabled("_ALPHATEST_ON");
        string path = $"{Out}{tier.Name} {source.name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
        }

        Color tint = leaves ? tier.Leaves : tier.Bark;
        Color baseColour = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
        material.SetColor("_BaseColor", new Color(baseColour.r * tint.r, baseColour.g * tint.g, baseColour.b * tint.b, baseColour.a));
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", leaves ? tier.LeafGlow : tier.BarkGlow); // black (none) unless the tier glows
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetVariants(string definitionPath, List<GameObject> looks)
    {
        var definition = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(definitionPath);
        var data = new SerializedObject(definition);
        SerializedProperty list = data.FindProperty("modelVariants");
        list.arraySize = looks.Count;
        for (int i = 0; i < looks.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = looks[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    // The node: the trunk collider (the maple's, scaled), a ChoppableTree with its own wood, and the leaves it sheds.
    private static void MakeFellable(Tier tier, CapsuleCollider mapleTrunk, float scale, GameObject leafFall)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(tier.Node);
        try
        {
            Transform model = root.transform.Find("Model");
            if (model != null)
            {
                model.localScale = Vector3.one;
                foreach (Collider nested in model.GetComponents<Collider>())
                {
                    Object.DestroyImmediate(nested); // a leftover override of the stand-in's collider
                }
            }

            CapsuleCollider trunk = root.TryGetComponent(out CapsuleCollider existing) ? existing : root.AddComponent<CapsuleCollider>();
            trunk.direction = 1;
            trunk.radius = mapleTrunk != null ? mapleTrunk.radius * scale : 0.3f;
            trunk.height = mapleTrunk != null ? mapleTrunk.height * scale : tier.Height * 0.45f;
            trunk.center = mapleTrunk != null ? mapleTrunk.center * scale : Vector3.up * trunk.height * 0.5f;

            ChoppableTree tree = root.TryGetComponent(out ChoppableTree existingTree) ? existingTree : root.AddComponent<ChoppableTree>();
            var treeData = new SerializedObject(tree);
            treeData.FindProperty("wood").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(tier.Wood);
            treeData.ApplyModifiedPropertiesWithoutUndo();

            TreeLeafLook look = root.TryGetComponent(out TreeLeafLook existingLook) ? existingLook : root.AddComponent<TreeLeafLook>();
            var lookData = new SerializedObject(look);
            lookData.FindProperty("colour").colorValue = tier.LeafFall;
            lookData.FindProperty("leafFall").objectReferenceValue = leafFall;
            lookData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, tier.Node);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
