using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skin weight fixes made as a model is imported, for the bodies the rig audit (RigAudit) found tearing (2026-11-18). Kept here, not in
/// the FBX, so a model exported again from its pipeline gets them too.
/// <list type="bullet">
/// <item><b>Between the legs:</b> a vertex weighted to both legs (a skirt's front and back, a loincloth, the crotch of a generated body)
/// was pulled apart as the legs split: in a lunge or a run the Lost Ones' mail skirt fanned into long wedges, the Fossegrim's grass
/// skirt into spikes. The share a vertex has on both legs (spread a few vertices out over the mesh, so the triangles that bridge the
/// legs go too) moves to the hips, which the cloth then follows. A vertex on one leg alone keeps its weights.</item>
/// <item><b>A root that never moves:</b> a humanoid clip moves the mapped bones, never the rig's root above the hips. The Draug's 72
/// vertices on its root ("Main") stayed where the standing body had been as it lay down, a pole and ring rising out of it. They move
/// to the hips.</item>
/// </list>
/// </summary>
public sealed class RigWeightFixes : AssetPostprocessor
{
    public sealed class Fix
    {
        public string Model;
        public string Hips;
        public string[] LeftLeg = new string[0];
        public string[] RightLeg = new string[0];
        /// <summary>How much of a vertex's both-legs share goes to the hips (1 all of it).</summary>
        public float Strength = 0.8f;
        /// <summary>Bones whose weight goes to the hips whole.</summary>
        public string[] ToHips = new string[0];
        /// <summary>Rounds of smoothing over the band where the waist's weight gives way to the legs' (a skirt, a loincloth): the weights
        /// of these bones averaged with each vertex's neighbours, so no triangle spans a sudden step from waist to thigh. 0: none.</summary>
        public int SmoothRounds;
        public string[] Waist = new string[0];
    }

    private static readonly string[] MixamoLeft = { "mixamorig:LeftUpLeg", "mixamorig:LeftLeg" };
    private static readonly string[] MixamoRight = { "mixamorig:RightUpLeg", "mixamorig:RightLeg" };

    // A generated body's waist-to-thigh band smoothed (a skirt or loincloth over the hips), nothing moved between the legs.
    private static Fix Smoothed(string model, int rounds = 8) =>
        new Fix { Model = model, Hips = "mixamorig:Hips", Waist = new[] { "mixamorig:Spine" }, LeftLeg = MixamoLeft, RightLeg = MixamoRight, Strength = 0f, SmoothRounds = rounds };

    private static Fix Mixamo(string model, float strength = 0.8f) =>
        new Fix { Model = model, Hips = "mixamorig:Hips", LeftLeg = MixamoLeft, RightLeg = MixamoRight, Strength = strength };

    /// <summary>The models, by path.</summary>
    public static readonly Fix[] Fixes =
    {
        new Fix { Model = "Assets/_Project/Art/Enemies/Downloaded/Boneman/Boneman.fbx", Hips = "LowerSpine", ToHips = new[] { "Main" } },
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/LostMailA/LostMailA.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/Fossegrim/Fossegrim.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/BogTroll/BogTroll.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/TrollRunt/TrollRunt.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/LostMailB/LostMailB.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/LostLeatherA/LostLeatherA.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/LostLeatherB/LostLeatherB.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/LostClothA/LostClothA.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/LostClothB/LostClothB.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/Trollkjerring/Trollkjerring.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/Brukam/Brukam.fbx"),
        Smoothed("Assets/_Project/Art/Enemies/Downloaded/StoneTroll/StoneTroll.fbx"),
        // The generated (Make-It-Animatable) bodies were tried with the between-the-legs rule (2026-11-18) and left out: their worst
        // triangles are skirt slivers stretched between a thigh and the waist in a lunge, which the rule didn't touch, and spreading it
        // gave vertices sharing a place different weights (seams opening up to 0.14). Their fix is a weight-smoothing pass in Blender.
    };

    private const int Spread = 3;

    private void OnPostprocessModel(GameObject root)
    {
        Fix fix = Fixes.FirstOrDefault(f => f.Model == assetPath);
        if (fix == null)
        {
            return;
        }

        foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh != null)
            {
                string[] names = skin.bones.Select(b => b != null ? b.name : "").ToArray();
                Apply(skin.sharedMesh, names, fix);
                if (fix.SmoothRounds > 0)
                {
                    SmoothBand(skin.sharedMesh, names, fix);
                }
            }
        }
    }

    /// <summary>The fix on one mesh, given its bones' names in order. Returns how many vertices changed.</summary>
    public static int Apply(Mesh mesh, string[] boneNames, Fix fix)
    {
        int hips = System.Array.IndexOf(boneNames, fix.Hips);
        if (hips < 0)
        {
            return 0;
        }

        var left = new HashSet<int>(fix.LeftLeg.Select(n => System.Array.IndexOf(boneNames, n)).Where(i => i >= 0));
        var right = new HashSet<int>(fix.RightLeg.Select(n => System.Array.IndexOf(boneNames, n)).Where(i => i >= 0));
        var whole = new HashSet<int>(fix.ToHips.Select(n => System.Array.IndexOf(boneNames, n)).Where(i => i >= 0));
        int count = mesh.vertexCount;
        byte[] perVertex = mesh.GetBonesPerVertex().ToArray();
        BoneWeight1[] weights = mesh.GetAllBoneWeights().ToArray();
        var starts = new int[count];
        for (int v = 1; v < count; v++)
        {
            starts[v] = starts[v - 1] + perVertex[v - 1];
        }

        // How much each vertex is shared between the legs: 1 when split evenly, 0 on one leg alone; then spread over the mesh.
        var shared = new float[count];
        for (int v = 0; v < count; v++)
        {
            float onLeft = 0f, onRight = 0f;
            for (int i = 0; i < perVertex[v]; i++)
            {
                BoneWeight1 w = weights[starts[v] + i];
                if (left.Contains(w.boneIndex)) onLeft += w.weight;
                if (right.Contains(w.boneIndex)) onRight += w.weight;
            }

            shared[v] = onLeft + onRight > 1e-4f ? Mathf.Clamp01(2f * Mathf.Min(onLeft, onRight) / (onLeft + onRight)) : 0f;
        }

        if (left.Count > 0 && right.Count > 0)
        {
            SpreadOver(mesh, shared, Spread);
        }

        var made = new List<BoneWeight1>(weights.Length + count);
        var madePerVertex = new byte[count];
        int changed = 0;
        var gathered = new Dictionary<int, float>();
        for (int v = 0; v < count; v++)
        {
            gathered.Clear();
            float move = left.Count > 0 && right.Count > 0 ? fix.Strength * shared[v] : 0f;
            bool touched = false;
            for (int i = 0; i < perVertex[v]; i++)
            {
                BoneWeight1 w = weights[starts[v] + i];
                float keep = w.weight;
                float toHips = 0f;
                if (whole.Contains(w.boneIndex))
                {
                    toHips = keep;
                    keep = 0f;
                }
                else if (move > 0f && (left.Contains(w.boneIndex) || right.Contains(w.boneIndex)))
                {
                    toHips = keep * move;
                    keep -= toHips;
                }

                touched |= toHips > 1e-5f;
                Add(gathered, w.boneIndex, keep);
                Add(gathered, hips, toHips);
            }

            if (touched)
            {
                changed++;
            }

            // At most four bones (the import's own limit), the largest first, summing to one.
            List<KeyValuePair<int, float>> kept = gathered.Where(p => p.Value > 1e-5f).OrderByDescending(p => p.Value).Take(4).ToList();
            float total = kept.Sum(p => p.Value);
            foreach (KeyValuePair<int, float> pair in kept)
            {
                made.Add(new BoneWeight1 { boneIndex = pair.Key, weight = pair.Value / Mathf.Max(total, 1e-6f) });
            }

            madePerVertex[v] = (byte)kept.Count;
        }

        if (changed > 0)
        {
            using var perVertexArray = new NativeArray<byte>(madePerVertex, Allocator.Temp);
            using var weightArray = new NativeArray<BoneWeight1>(made.ToArray(), Allocator.Temp);
            mesh.SetBoneWeights(perVertexArray, weightArray);
        }

        return changed;
    }

    /// <summary>
    /// Smooths the waist and leg bones' weights over the band where one gives way to the other, each vertex keeping its total on them
    /// and every other bone's weight. Vertices that share a place end with the same weights, so no seam opens. Returns the band's size.
    /// </summary>
    public static int SmoothBand(Mesh mesh, string[] boneNames, Fix fix)
    {
        int Index(string name) => System.Array.IndexOf(boneNames, name);
        var waist = new HashSet<int>(fix.Waist.Append(fix.Hips).Select(Index).Where(i => i >= 0));
        var legs = new HashSet<int>(fix.LeftLeg.Concat(fix.RightLeg).Select(Index).Where(i => i >= 0));
        if (waist.Count == 0 || legs.Count == 0)
        {
            return 0;
        }

        int[] band = waist.Concat(legs).Distinct().ToArray();
        int count = mesh.vertexCount;
        byte[] perVertex = mesh.GetBonesPerVertex().ToArray();
        BoneWeight1[] weights = mesh.GetAllBoneWeights().ToArray();
        var all = new Dictionary<int, float>[count];
        var share = new float[count];
        var split = new float[count][];
        var inBand = new float[count];
        int at = 0;
        for (int v = 0; v < count; v++)
        {
            all[v] = new Dictionary<int, float>();
            for (int i = 0; i < perVertex[v]; i++)
            {
                Add(all[v], weights[at + i].boneIndex, weights[at + i].weight);
            }

            at += perVertex[v];
            split[v] = new float[band.Length];
            float onWaist = 0f, onLegs = 0f;
            for (int b = 0; b < band.Length; b++)
            {
                float w = all[v].TryGetValue(band[b], out float had) ? had : 0f;
                split[v][b] = w;
                share[v] += w;
                if (waist.Contains(band[b]))
                {
                    onWaist += w;
                }
                else
                {
                    onLegs += w;
                }
            }

            for (int b = 0; b < band.Length && share[v] > 1e-5f; b++)
            {
                split[v][b] /= share[v];
            }

            inBand[v] = onWaist > 0.02f && onLegs > 0.02f ? 1f : 0f;
        }

        Joined(mesh, out List<int>[] neighbours, out List<int[]> places);
        SpreadOver(neighbours, inBand, 2);
        var next = new float[count][];
        for (int round = 0; round < fix.SmoothRounds; round++)
        {
            for (int v = 0; v < count; v++)
            {
                next[v] = split[v];
                if (inBand[v] <= 0f || share[v] <= 1e-5f)
                {
                    continue;
                }

                var mean = new float[band.Length];
                int counted = 0;
                foreach (int u in neighbours[v])
                {
                    if (share[u] > 1e-5f)
                    {
                        for (int b = 0; b < band.Length; b++)
                        {
                            mean[b] += split[u][b];
                        }

                        counted++;
                    }
                }

                if (counted == 0)
                {
                    continue;
                }

                // Half way to the neighbours' mean, as much as the vertex is in the band.
                float pull = 0.5f * Mathf.Min(1f, inBand[v]);
                var smoothed = new float[band.Length];
                for (int b = 0; b < band.Length; b++)
                {
                    smoothed[b] = Mathf.Lerp(split[v][b], mean[b] / counted, pull);
                }

                next[v] = smoothed;
            }

            // Vertices sharing a place take one split between them.
            foreach (int[] place in places)
            {
                var mean = new float[band.Length];
                foreach (int v in place)
                {
                    for (int b = 0; b < band.Length; b++)
                    {
                        mean[b] += next[v][b] / place.Length;
                    }
                }

                foreach (int v in place)
                {
                    next[v] = mean;
                }
            }

            System.Array.Copy(next, split, count);
        }

        var made = new List<BoneWeight1>(weights.Length);
        var madePerVertex = new byte[count];
        int members = 0;
        for (int v = 0; v < count; v++)
        {
            if (inBand[v] > 0f && share[v] > 1e-5f)
            {
                members++;
                foreach (int bone in band)
                {
                    all[v].Remove(bone);
                }

                for (int b = 0; b < band.Length; b++)
                {
                    Add(all[v], band[b], split[v][b] * share[v]);
                }
            }
        }

        // Every vertex sharing a place ends with exactly the same weights (their average): a seam can't open by a hair.
        foreach (int[] place in places)
        {
            var mean = new Dictionary<int, float>();
            foreach (int v in place)
            {
                foreach (KeyValuePair<int, float> pair in all[v])
                {
                    Add(mean, pair.Key, pair.Value / place.Length);
                }
            }

            foreach (int v in place)
            {
                all[v] = new Dictionary<int, float>(mean);
            }
        }

        for (int v = 0; v < count; v++)
        {
            List<KeyValuePair<int, float>> kept = all[v].Where(p => p.Value > 1e-5f).OrderByDescending(p => p.Value).Take(4).ToList();
            float total = kept.Sum(p => p.Value);
            foreach (KeyValuePair<int, float> pair in kept)
            {
                made.Add(new BoneWeight1 { boneIndex = pair.Key, weight = pair.Value / Mathf.Max(total, 1e-6f) });
            }

            madePerVertex[v] = (byte)kept.Count;
        }

        using var perVertexArray = new NativeArray<byte>(madePerVertex, Allocator.Temp);
        using var weightArray = new NativeArray<BoneWeight1>(made.ToArray(), Allocator.Temp);
        mesh.SetBoneWeights(perVertexArray, weightArray);
        return members;
    }

    private static void Add(Dictionary<int, float> into, int bone, float weight)
    {
        if (weight <= 0f)
        {
            return;
        }

        into[bone] = into.TryGetValue(bone, out float had) ? had + weight : weight;
    }

    // Spreads a 0-1 field over the mesh's edges, halving with each ring, so the triangles that bridge a split are taken in too. Vertices
    // that share a place (UV seams) count as joined.
    private static void SpreadOver(Mesh mesh, float[] field, int rings)
    {
        Joined(mesh, out List<int>[] neighbours, out _);
        SpreadOver(neighbours, field, rings);
    }

    // Each vertex's neighbours along the mesh's edges, vertices that share a place (UV seams) counted as joined; and those places.
    private static void Joined(Mesh mesh, out List<int>[] neighbours, out List<int[]> sharedPlaces)
    {
        int count = mesh.vertexCount;
        neighbours = new List<int>[count];
        for (int v = 0; v < count; v++)
        {
            neighbours[v] = new List<int>(6);
        }

        int[] triangles = mesh.triangles;
        for (int t = 0; t < triangles.Length; t += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = triangles[t + e], b = triangles[t + (e + 1) % 3];
                neighbours[a].Add(b);
                neighbours[b].Add(a);
            }
        }

        Vector3[] vertices = mesh.vertices;
        var places = new Dictionary<Vector3Int, List<int>>();
        for (int v = 0; v < count; v++)
        {
            var key = Vector3Int.RoundToInt(vertices[v] * 10000f);
            if (!places.TryGetValue(key, out List<int> list))
            {
                places[key] = list = new List<int>();
            }

            list.Add(v);
        }

        sharedPlaces = new List<int[]>();
        foreach (List<int> same in places.Values.Where(l => l.Count > 1))
        {
            sharedPlaces.Add(same.ToArray());
            foreach (int a in same)
            {
                foreach (int b in same)
                {
                    if (a != b)
                    {
                        neighbours[a].Add(b);
                    }
                }
            }
        }
    }

    private static void SpreadOver(List<int>[] neighbours, float[] field, int rings)
    {
        int count = field.Length;
        var next = new float[count];
        for (int ring = 0; ring < rings; ring++)
        {
            for (int v = 0; v < count; v++)
            {
                float most = field[v];
                foreach (int n in neighbours[v])
                {
                    most = Mathf.Max(most, field[n] * 0.5f);
                }

                next[v] = most;
            }

            System.Array.Copy(next, field, count);
        }
    }

    [MenuItem("Safe Haven/Characters/Reimport Rig Weight Fixes")]
    public static void ReimportAll()
    {
        foreach (Fix fix in Fixes)
        {
            AssetDatabase.ImportAsset(fix.Model, ImportAssetOptions.ForceUpdate);
        }
    }
}
