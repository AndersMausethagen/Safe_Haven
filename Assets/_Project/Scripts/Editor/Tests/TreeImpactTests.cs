using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>What a falling tree does to what is in its way: who is crushed and how hard, and what it does to standing trees.</summary>
public sealed class TreeImpactTests
{
    private static TreeChoppingSettings Rules => TreeChoppingSettings.Current;

    [Test]
    public void TheDistanceBetweenTwoLines_IsTheGapAtTheirClosest()
    {
        // A trunk lying along X at 3 m up, and a tree standing at x = 4, 2 m to the side.
        float distance = TreeContact.Distance(new Vector3(0f, 3f, 0f), new Vector3(10f, 3f, 0f), new Vector3(4f, 0f, 2f), new Vector3(4f, 8f, 2f), out float along, out Vector3 point);
        Assert.AreEqual(2f, distance, 0.001f);
        Assert.AreEqual(0.4f, along, 0.001f);
        Assert.AreEqual(4f, point.x, 0.001f);
    }

    [Test]
    public void ALineThatStopsShort_IsMeasuredFromItsEnd()
    {
        float distance = TreeContact.Distance(Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(5f, 8f, 0f), out float along, out _);
        Assert.AreEqual(2f, distance, 0.001f);
        Assert.AreEqual(1f, along, 0.001f);
    }

    [Test]
    public void APoint_IsMeasuredToTheNearestPartOfTheLine()
    {
        // How far along a trunk someone is: they are a point, not a line, and the middle of the trunk must be found.
        var somebody = new Vector3(6f, 1f, 0.5f);
        float distance = TreeContact.Distance(Vector3.zero, new Vector3(10f, 0f, 0f), somebody, somebody, out float along, out Vector3 point);
        Assert.AreEqual(0.6f, along, 0.001f);
        Assert.AreEqual(6f, point.x, 0.001f);
        Assert.AreEqual(Mathf.Sqrt(1.25f), distance, 0.001f);
    }

    [Test]
    public void ALeaningTree_MeetsANeighbourAtTheRightMoment()
    {
        var foot = new Vector3(0f, 0.45f, 0f);
        var neighbour = new Vector3(5f, 0f, 0f);
        float Gap(float lean)
        {
            Vector3 top = foot + Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, Vector3.right)) * Vector3.up * 18f;
            return TreeContact.Distance(foot, top, neighbour, neighbour + Vector3.up * 12f, out _, out _);
        }

        Assert.Greater(Gap(5f), Gap(40f), "it closes in as it falls");
        Assert.Less(Gap(80f), 1f, "and reaches a tree 5 m away");
    }

    [Test]
    public void AHeavierNeighbour_ChecksTheFallHarder_ButNeverStopsIt()
    {
        Assert.Less(TreeContact.SpinKept(1f, 10f), TreeContact.SpinKept(1f, 1f));
        Assert.Less(TreeContact.SpinKept(1f, 1f), TreeContact.SpinKept(10f, 1f));
        Assert.GreaterOrEqual(TreeContact.SpinKept(1f, 100000f), 0.5f, "a tree must always finish falling");
        Assert.Less(TreeContact.SpinKept(100000f, 1f), 1f, "and always feels what it hit");
    }

    [Test]
    public void AHarderBlow_KnocksATreeFurther_AndALightOneMost()
    {
        Assert.Greater(TreeContact.KnockDegrees(12f, 1f, 1f, 11f, 12f), TreeContact.KnockDegrees(3f, 1f, 1f, 11f, 12f));
        Assert.Greater(TreeContact.KnockDegrees(12f, 5f, 1f, 11f, 12f), TreeContact.KnockDegrees(12f, 1f, 5f, 11f, 12f));
        Assert.LessOrEqual(TreeContact.KnockDegrees(100f, 1000f, 0.01f, 11f, 12f), 11f, "never more than the most");
    }

    [Test]
    public void ATreeOnlyHurts_OnceItIsReallyMoving()
    {
        Assert.AreEqual(0f, TreeContact.CrushDamage(90f, 1f, 1f, 3f, 12f), 0.001f, "a tree tipping over hurts nobody");
        Assert.AreEqual(0f, TreeContact.CrushDamage(90f, 1f, 3f, 3f, 12f), 0.001f);
        Assert.AreEqual(45f, TreeContact.CrushDamage(90f, 1f, 7.5f, 3f, 12f), 0.001f);
        Assert.AreEqual(90f, TreeContact.CrushDamage(90f, 1f, 12f, 3f, 12f), 0.001f);
        Assert.AreEqual(90f, TreeContact.CrushDamage(90f, 1f, 40f, 3f, 12f), 0.001f, "no more than its worst");
    }

    [Test]
    public void ABiggerTree_HitsHarder()
    {
        Assert.Greater(TreeContact.CrushDamage(90f, 2.5f, 12f, 3f, 12f), TreeContact.CrushDamage(90f, 0.5f, 12f, 3f, 12f));
    }

    [Test]
    public void AlliesGetClear_ThePlayerTakesAShare_AndEnemiesTakeItAll()
    {
        Assert.AreEqual(1f, Rules.CrushShare(Faction.Enemy), 0.001f);
        Assert.AreEqual(1f, Rules.CrushShare(Faction.Neutral), 0.001f);
        Assert.AreEqual(0f, Rules.CrushShare(Faction.Ally), 0.001f, "a companion is never killed by the player's own tree");
        Assert.That(Rules.CrushShare(Faction.Player), Is.GreaterThan(0f).And.LessThan(1f));
    }

    [Test]
    public void TheBiggestTree_NeverKillsAPlayerAtFullHealth()
    {
        Assert.Less(Rules.CrushDamageTo(Faction.Player, 2.5f, 40f, 100f), 100f);
        Assert.AreEqual(100f * Rules.PlayerCrushMost, Rules.CrushDamageTo(Faction.Player, 2.5f, 40f, 100f), 0.001f);
        Assert.Greater(Rules.CrushDamageTo(Faction.Enemy, 2.5f, 40f, 100f), 100f, "the cap is the player's alone");
        Assert.AreEqual(0f, Rules.CrushDamageTo(Faction.Ally, 2.5f, 40f, 100f), 0.001f);
    }

    [Test]
    public void AKnockedTree_SwingsPastUpright_AndSettles()
    {
        var spring = new TreeSpring();
        spring.Knock(Vector2.right, 10f);
        float furthest = 0f;
        float back = 0f;
        for (int i = 0; i < 2000 && !spring.IsStill; i++)
        {
            spring.Step(0.01f);
            furthest = Mathf.Max(furthest, spring.Lean.x);
            back = Mathf.Min(back, spring.Lean.x);
        }

        Assert.That(furthest, Is.InRange(5f, 12f), "it goes over about as far as it was knocked");
        Assert.Less(back, -0.5f, "and swings back past upright");
        Assert.IsTrue(spring.IsStill, "and comes to rest");
    }

    [Test]
    public void AHardKnock_LeavesATreeStandingCrooked()
    {
        var spring = new TreeSpring { Rest = Vector2.right * 2f };
        spring.Knock(Vector2.right, 10f);
        for (int i = 0; i < 2000 && !spring.IsStill; i++)
        {
            spring.Step(0.01f);
        }

        Assert.AreEqual(2f, spring.Lean.x, 0.1f);
    }

    [Test]
    public void TheFelledTree_HasWhatItNeedsToHitThings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Systems/FelledTree.prefab");
        Assert.IsNotNull(prefab.GetComponent<TreeImpacts>());
        Assert.IsTrue(Rules.StrikeSound.HasClips && Rules.CrushSound.HasClips);
    }

    // A falling tree finds a standing one by any solid collider under its ChoppableTree (TreeImpacts' overlap), so every choppable
    // tree must carry one: every prefab of the kind, not one spruce.
    [Test]
    public void EveryChoppableTree_CanBeMet()
    {
        var unmet = new List<string>();
        int trees = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<ChoppableTree>() == null || path.EndsWith("TerrainTreeProxy.prefab"))
            {
                continue;
            }

            trees++;
            if (!prefab.GetComponentsInChildren<Collider>(true).Any(collider => !collider.isTrigger))
            {
                unmet.Add(path);
            }
        }

        Assert.Greater(trees, 5, "the choppable trees were found");
        Assert.IsEmpty(unmet, "no solid collider, so a falling tree passes through them:\n" + string.Join("\n", unmet));
    }
}
