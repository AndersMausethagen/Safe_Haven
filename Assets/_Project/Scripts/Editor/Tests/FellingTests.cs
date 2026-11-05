using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>A tree coming down and the work left in it: what it pays, which way it falls, and the timber it leaves.</summary>
public sealed class FellingTests
{
    private const string Felled = "Assets/_Project/Prefabs/Systems/FelledTree.prefab";

    private static TreeChoppingSettings Rules => TreeChoppingSettings.Current;

    [Test]
    public void WorkingTheLog_PaysMoreThanFellingEverDid()
    {
        foreach (float size in new[] { 0.5f, 1f, 1.5f, 2.5f })
        {
            foreach (int axe in new[] { 1, 2, 3, 4 })
            {
                Assert.Greater(Rules.AllLogs(axe, size), Rules.Logs(axe, size), $"a worked tree of size {size} with axe {axe}");
            }
        }
    }

    [Test]
    public void Felling_LeavesWoodToBeCutOut()
    {
        foreach (float size in new[] { 1f, 2.5f })
        {
            Assert.Less(Rules.FellingLogs(1, size), Rules.AllLogs(1, size));
            Assert.GreaterOrEqual(Rules.FellingLogs(1, size), 1);
        }
    }

    [Test]
    public void ABiggerTree_MeansMoreToCut_AndABetterAxeMoreFromEachCut()
    {
        Assert.Greater(Rules.Sections(2.5f), Rules.Sections(0.5f));
        Assert.GreaterOrEqual(Rules.Sections(0.5f), 1);
        Assert.Greater(Rules.SectionLogs(3), Rules.SectionLogs(1));
    }

    [Test]
    public void RollingACount_GivesThatManyLogs_MergedByWood()
    {
        var logs = new List<ItemAmount>();
        ChoppableTree.Roll(7, 2, 1.5f, () => 0f, logs);
        Assert.AreEqual(7, logs.Sum(log => log.Amount));
        Assert.AreEqual(logs.Count, logs.Select(log => log.Item).Distinct().Count());
    }

    [Test]
    public void ATree_FallsAwayFromTheFeller_WhenThereIsRoom()
    {
        Vector3 fall = FellDirection.Choose(Vector3.right, 75f, 5, way => 1f);
        Assert.Greater(Vector3.Dot(fall, Vector3.right), 0.999f);
    }

    [Test]
    public void ATree_TurnsTowardsOpenGround_RatherThanIntoAWall()
    {
        // Straight ahead is blocked at once; only well off to one side is open.
        Vector3 fall = FellDirection.Choose(Vector3.forward, 75f, 5, way => way.x > 0.5f ? 1f : 0.1f);
        Assert.Greater(fall.x, 0.5f);
        Assert.Greater(fall.z, 0f, "it still falls away from the feller");
    }

    [Test]
    public void ATree_StaysLevel_WhateverTheFellerIsStandingOn()
    {
        Vector3 fall = FellDirection.Choose(new Vector3(1f, 5f, 0f), 75f, 5, way => 1f);
        Assert.AreEqual(0f, fall.y, 0.001f);
        Assert.AreEqual(1f, fall.magnitude, 0.001f);
    }

    [Test]
    public void ALog_IsClosed_WithItsFacesInTheirOwnSubmesh()
    {
        Mesh mesh = LogMesh.Build(0.3f, 0.2f, 2f);
        try
        {
            Assert.AreEqual(2, mesh.subMeshCount);
            Assert.Greater(mesh.GetTriangles(0).Length, 0);
            Assert.Greater(mesh.GetTriangles(1).Length, 0);
            Assert.AreEqual(2f, mesh.bounds.size.y, 0.001f);
            Assert.Less(mesh.bounds.size.x, 0.7f);
            Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
            Assert.IsTrue(mesh.vertices.All(v => !float.IsNaN(v.x)), "a log with no bad vertices");
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void ALogsFaces_LookOutwards()
    {
        Mesh mesh = LogMesh.Build(0.3f, 0.3f, 2f);
        try
        {
            int[] faces = mesh.GetTriangles(1);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < faces.Length; i += 3)
            {
                Vector3 a = vertices[faces[i]];
                Vector3 facing = Vector3.Cross(vertices[faces[i + 1]] - a, vertices[faces[i + 2]] - a);
                Assert.AreEqual(a.y > 1f, facing.y > 0f, "the top face looks up and the bottom one down");
            }
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void SectionsCutFromOneLog_LineUp()
    {
        Mesh lower = LogMesh.Build(0.3f, 0.25f, 1f, 0f, 7);
        Mesh upper = LogMesh.Build(0.25f, 0.2f, 1f, 1f, 7);
        try
        {
            Vector3[] below = lower.vertices;
            Vector3[] above = upper.vertices;
            const int ringSize = 13;
            int topRing = below.Length - 2 * 14 - ringSize;
            for (int i = 0; i < ringSize; i++)
            {
                Assert.AreEqual(below[topRing + i].x, above[i].x, 0.001f, "the bark continues across the cut");
                Assert.AreEqual(below[topRing + i].z, above[i].z, 0.001f);
            }
        }
        finally
        {
            Object.DestroyImmediate(lower);
            Object.DestroyImmediate(upper);
        }
    }

    [Test]
    public void FoliageIsToldFromBark_ByName()
    {
        Assert.IsTrue(Rules.IsFoliage("Scots Pine Needles"));
        Assert.IsTrue(Rules.IsFoliage("Leaves"));
        Assert.IsTrue(Rules.IsFoliage("Vines"));
        Assert.IsFalse(Rules.IsFoliage("pine_tree_01_trunk_a"));
        Assert.IsFalse(Rules.IsFoliage("Bark"));
    }

    [Test]
    public void EveryChoppableTree_HasACrownAndATrunk()
    {
        var checkedAny = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<ChoppableTree>() == null || path.EndsWith("TerrainTreeProxy.prefab"))
            {
                continue;
            }

            TreeParts parts = TreeParts.Of(prefab, Vector3.zero, Quaternion.identity, Vector3.one);
            checkedAny.Add(path);
            Assert.Greater(parts.Parts.Count, 0, path);
            Assert.Greater(parts.Height, 1f, path);
            Assert.Greater(parts.TrunkLength, 1f, path);
            Assert.GreaterOrEqual(parts.TrunkRadius, 0.07f, path);
            Assert.Less(parts.TrunkLength, parts.Height, path);
        }

        Assert.Greater(checkedAny.Count, 5, "the tree prefabs were found");
    }

    [Test]
    public void AStandInTree_ReadsWhereItStands()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/World/Scots Pine A.prefab");
        var at = new Vector3(40f, 3f, -12f);
        TreeParts near = TreeParts.Of(prefab, Vector3.zero, Quaternion.identity, Vector3.one);
        TreeParts moved = TreeParts.Of(prefab, at, Quaternion.identity, Vector3.one * 1.5f);
        Assert.AreEqual(at, moved.Base);
        Assert.AreEqual(near.Height * 1.5f, moved.Height, 0.01f, "its height is measured from its base, not the world");
        Assert.AreEqual(near.Parts[0].Pose.GetColumn(3).y * 1.5f, moved.Parts[0].Pose.GetColumn(3).y, 0.01f);
    }

    [Test]
    public void TheFelledTree_IsSetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Felled);
        Assert.IsNotNull(prefab);
        Assert.AreSame(prefab, Rules.FelledTreePrefab);
        Assert.IsNotNull(prefab.GetComponent<FelledTree>());
        Assert.IsNotNull(prefab.GetComponent<FelledLog>());
        Assert.IsNull(prefab.GetComponent<ChoppableTree>(), "a fallen tree is not felled again");
        GatheringNodeDefinition node = prefab.GetComponent<ResourceNode>().Definition;
        Assert.IsFalse(node.DepleteOnGather, "it is worked again and again");
        Assert.AreEqual(ToolType.Axe, node.GatherTool);
        Assert.AreEqual(0f, node.Feedback.BreakSeconds, 0.001f, "a log must not shrink where it lies");
        Assert.IsNotNull(Rules.LimbingItem);
        Assert.IsNotNull(Rules.LogBark);
        Assert.IsNotNull(Rules.CutFace);
        Assert.IsNotNull(Rules.LeafFall);
        Assert.IsNotNull(Rules.LandingDust);
        Assert.IsTrue(Rules.LandingSound.HasClips && Rules.CreakSound.HasClips && Rules.RushSound.HasClips);
    }

    [Test]
    public void ATree_DoesNotShrinkInPlace_WhileItsCopyFalls()
    {
        var tree = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/Tree.asset");
        Assert.AreEqual(0f, tree.Feedback.BreakSeconds, 0.001f);
    }
}
