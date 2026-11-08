using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Outlines follow an object's edge: cut-out leaf cards don't draw outline (their quads traced lines all through a
/// crown) but still mask; only a LOD group's most detailed level is copied; the mask ignores depth.</summary>
public sealed class OutlineCopiesTests
{
    [Test]
    public void ATreesOutline_LeavesOutItsLeafCards_ButMasksThem()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/CedarSapling.asset").Sapling.GrowsInto[0];
        GameObject tree = Object.Instantiate(prefab);
        var outline = new Material(Shader.Find("Safe Haven/Interaction Outline"));
        var mask = new Material(Shader.Find("Safe Haven/Interaction Outline Mask"));
        try
        {
            var entries = OutlineCopies.Create(tree.transform, outline, mask, "Outline", "Mask");
            Assert.Greater(entries.Count, 0);
            int leftOut = 0, drawn = 0;
            foreach (OutlineCopies.Entry entry in entries)
            {
                Material[] source = entry.Source.sharedMaterials;
                Material[] lines = entry.Outline.sharedMaterials;
                Assert.IsTrue(entry.Mask.sharedMaterials.All(m => m == mask), "everything masks");
                for (int i = 0; i < lines.Length && i < source.Length; i++)
                {
                    bool cutOut = source[i] != null && source[i].IsKeywordEnabled("_ALPHATEST_ON");
                    if (cutOut) { Assert.IsNull(lines[i], source[i].name); leftOut++; }
                    else { Assert.AreSame(outline, lines[i], source[i] != null ? source[i].name : "?"); drawn++; }
                }
            }

            Assert.Greater(leftOut, 0, "the leaves are left out");
            Assert.Greater(drawn, 0, "the trunk and branches are outlined");
            LODGroup group = tree.GetComponentInChildren<LODGroup>();
            if (group != null && group.lodCount > 1)
            {
                var detailed = group.GetLODs()[0].renderers;
                Assert.IsTrue(entries.All(e => detailed.Contains(e.Source)), "the most detailed level only");
            }
        }
        finally
        {
            Object.DestroyImmediate(tree);
            Object.DestroyImmediate(outline);
            Object.DestroyImmediate(mask);
        }
    }

    [Test]
    public void TheInteractionMask_IgnoresDepth()
    {
        var mask = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutlineMask.mat");
        Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.Always, mask.GetFloat("_ZTest"));
    }
}
