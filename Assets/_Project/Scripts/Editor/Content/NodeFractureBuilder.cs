using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cracks the models of gathering nodes that break apart (GatheringNodeDefinition › Break Apart) ahead of time: the main parts of each
/// model into pieces, in proportion to their size. Parts too small for a piece of their own fly off whole. Run it after changing a
/// node's models or its number of pieces.
/// </summary>
public static class NodeFractureBuilder
{
    private const string Folder = "Assets/_Project/Data/Gathering/Fractures";

    [MenuItem("Safe Haven/Setup/Resources/Crack Mined Nodes", false, 200)]
    public static void CrackAll()
    {
        int cracked = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:GatheringNodeDefinition"))
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (node.BurstPieces > 0)
            {
                Crack(node);
                cracked++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Cracked the models of {cracked} gathering nodes.");
    }

    public static NodeFracture Crack(GatheringNodeDefinition node)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Data/Gathering", "Fractures");
        }

        string path = $"{Folder}/{node.name} Fracture.asset";
        AssetDatabase.DeleteAsset(path);
        var fracture = ScriptableObject.CreateInstance<NodeFracture>();
        AssetDatabase.CreateAsset(fracture, path);

        var done = new HashSet<Mesh>();
        var models = new List<GameObject> { node.WorldPrefab };
        foreach (GameObject variant in node.ModelVariants) models.Add(variant);
        foreach (GameObject model in models)
        {
            if (model == null) continue;
            List<MeshRenderer> parts = NodeFracture.DrawnParts(model.transform);
            float whole = 0f;
            // Several parts can share a mesh (an ore's rocks): they share its pieces too, so it is what they come to on average.
            var shares = new Dictionary<Mesh, (float volume, int uses)>();
            foreach (MeshRenderer part in parts)
            {
                Mesh shared = part.TryGetComponent(out MeshFilter sharedFilter) ? sharedFilter.sharedMesh : null;
                whole += Volume(part);
                if (shared == null) continue;
                shares.TryGetValue(shared, out (float volume, int uses) so);
                shares[shared] = (so.volume + Volume(part), so.uses + 1);
            }

            foreach (KeyValuePair<Mesh, (float volume, int uses)> share in shares)
            {
                Mesh mesh = share.Key;
                int pieces = Mathf.RoundToInt(node.BurstPieces * share.Value.volume / Mathf.Max(whole, 0.0001f) / share.Value.uses);
                if (pieces < 2 || !done.Add(mesh)) continue;
                // Cut from the next level down where there is one: the same shape from this camera, and a third of the data.
                Mesh cutFrom = NextLevelDown(model.transform, mesh);
                List<Mesh> cracked = MeshCracker.Crack(cutFrom, pieces, node.name.GetHashCode() ^ mesh.name.GetHashCode());
                foreach (Mesh piece in cracked) AssetDatabase.AddObjectToAsset(piece, fracture);
                fracture.Add(mesh, cracked.ToArray());
            }
        }

        EditorUtility.SetDirty(fracture);
        var data = new SerializedObject(node);
        data.FindProperty("fracture").objectReferenceValue = fracture;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);
        return fracture;
    }

    private static Mesh NextLevelDown(Transform model, Mesh finest)
    {
        foreach (LODGroup group in model.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] levels = group.GetLODs();
            if (levels.Length < 2 || levels[0].renderers.Length != 1 || levels[1].renderers.Length != 1) continue;
            if (levels[0].renderers[0] != null && levels[0].renderers[0].TryGetComponent(out MeshFilter top) && top.sharedMesh == finest
                && levels[1].renderers[0] != null && levels[1].renderers[0].TryGetComponent(out MeshFilter next) && next.sharedMesh != null)
            {
                return next.sharedMesh;
            }
        }

        return finest;
    }

    private static float Volume(MeshRenderer part)
    {
        Mesh mesh = part.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        if (mesh == null) return 0f;
        Vector3 size = Vector3.Scale(mesh.bounds.size, part.transform.lossyScale);
        return Mathf.Abs(size.x * size.y * size.z);
    }
}
