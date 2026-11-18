using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Fish sizes (FishSizes): the curve, the fight and the bait, the length a fish keeps in the bag and the journal's records.</summary>
public sealed class FishSizeTests
{
    private static ItemDefinition Perch => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Fish/Perch.asset");

    [Test]
    public void Lengths_AreAWideCurve_MostlyTwentyToSixty_WithASharpFallPastIt()
    {
        var random = new System.Random(11);
        float[] lengths = Enumerable.Range(0, 20000).Select(_ => FishSizes.Roll(random)).ToArray();
        float Share(System.Func<float, bool> test) => lengths.Count(test) / (float)lengths.Length;
        Assert.That(lengths.Min(), Is.GreaterThanOrEqualTo(FishSizes.Shortest));
        Assert.That(lengths.Max(), Is.LessThanOrEqualTo(FishSizes.Longest));
        Assert.That(Share(cm => cm >= 20f && cm <= 60f), Is.GreaterThan(0.75f), "Most fish are 20-60 cm.");
        Assert.That(Share(cm => cm > 60f), Is.InRange(0.05f, 0.15f), "Some are bigger than that...");
        Assert.That(Share(cm => cm > 100f), Is.LessThan(0.02f), "...few much bigger.");
        Assert.That(Share(cm => cm < 20f), Is.InRange(0.05f, 0.18f));
        Assert.That(FishSizes.Roll(0.5f, 0f, 1.5f), Is.GreaterThan(FishSizes.Roll(0.5f, 0f, 0.75f)), "A pike runs longer than a perch.");
        Assert.That(FishSizes.Roll(1e-9f, 0f, 3f), Is.EqualTo(FishSizes.Longest), "Never past 2 m.");
    }

    [Test]
    public void ABiggerFish_FightsHarder_AndBitesLessReadily_GoodBaitEasesBoth()
    {
        Assert.That(FishSizes.Difficulty(40f), Is.EqualTo(1f).Within(0.001f), "A 40 cm fish is the tuned fight.");
        Assert.That(FishSizes.Difficulty(120f), Is.GreaterThan(FishSizes.Difficulty(60f)));
        Assert.That(FishSizes.Difficulty(15f), Is.LessThan(1f));
        float hard = FishSizes.Difficulty(120f);
        Assert.That(FishSizes.Eased(hard, 0.35f), Is.LessThan(hard).And.GreaterThan(1f), "Good bait takes some fight out of a big one.");
        Assert.That(FishSizes.Eased(0.7f, 0.5f), Is.EqualTo(0.7f), "A small fish is small whatever the bait.");
        Assert.That(FishSizes.BiteWillingness(120f, 0f), Is.LessThan(FishSizes.BiteWillingness(40f, 0f)), "Big fish are wary.");
        Assert.That(FishSizes.BiteWillingness(120f, 0.35f), Is.GreaterThan(FishSizes.BiteWillingness(120f, 0f)), "Good bait brings them on.");
    }

    [Test]
    public void AFishWeighs_ByTheCubeOfItsLength_FromItsKindsTypicalOne()
    {
        var kind = new FishingSettings.Fish { size = new Vector2(0.4f, 1.2f), lengthScale = 1f };
        Assert.That(FishSizes.Kilograms(kind, FishSizes.Median), Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(FishSizes.Kilograms(kind, FishSizes.Median * 2f), Is.EqualTo(6.4f).Within(0.01f));
    }

    [Test]
    public void AFishKeepsItsLength_InTheBag_AStackTheBiggestsAndASaveToo()
    {
        ItemDefinition perch = Perch;
        Assert.That(perch, Is.Not.Null);
        var bag = new ItemContainer(6, 1);
        bag.Add(perch, 1, 31f);
        bag.Add(perch, 1, 48f);
        bag.Add(perch, 1, 22f);
        Assert.That(bag.GetSlot(0).Count, Is.EqualTo(3), "Fish of any length still stack.");
        Assert.That(bag.GetSlot(0).Size, Is.EqualTo(48f), "The stack shows its biggest.");
        bag.RemoveAt(0, 1);
        Assert.That(bag.GetSlot(0).Size, Is.EqualTo(48f), "Eating one changes nothing about the others' measure.");

        var stash = new ItemContainer(6, 1);
        bag.TransferTo(0, stash);
        Assert.That(stash.GetSlot(0).Size, Is.EqualTo(48f), "It goes with them into the stash.");

        List<SlotSaveData> saved = SaveMapper.ToSlots(stash);
        Assert.That(saved[0].size, Is.EqualTo(48f));
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var loaded = new ItemContainer(6, 1);
        SaveMapper.ReadSlots(saved, loaded, content, new List<string>());
        Assert.That(loaded.GetSlot(0).Size, Is.EqualTo(48f), "And through a save.");
        Assert.That(new ItemStack(perch, 1).Size, Is.EqualTo(0f), "An unmeasured one has no length.");
    }

    [Test]
    public void TheJournal_KeepsTheBiggestOfEachKind()
    {
        var journal = new JournalRecord();
        Assert.IsTrue(journal.NoteCatch("perch", 30f, out float before));
        Assert.That(before, Is.EqualTo(0f), "The first is a record with nothing before it.");
        Assert.IsFalse(journal.NoteCatch("perch", 25f, out _), "A smaller one isn't.");
        Assert.IsTrue(journal.NoteCatch("perch", 41f, out before));
        Assert.That(before, Is.EqualTo(30f));
        Assert.IsTrue(journal.NoteCatch("pike", 90f, out _));
        Assert.That(journal.BiggestFish["perch"], Is.EqualTo(41f));
        Assert.That(journal.BiggestFish.Count, Is.EqualTo(2));
    }

    // The fish's shape had NaN corners at the mouth (Pow of a sin(pi) a hair below zero), drawn as a black head on a pale
    // body: the "tadpoles" the user saw (2026-11-12). Every corner and normal must be a real number, every normal a length.
    [Test]
    public void TheFishShape_HasNoBrokenCorners()
    {
        Mesh mesh = FishingLine.SharedFishMesh;
        foreach (Vector3 v in mesh.vertices) Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), "a corner is NaN");
        foreach (Vector3 n in mesh.normals) Assert.That(n.magnitude, Is.EqualTo(1f).Within(0.01f), "a normal is NaN or cancelled out");
        Assert.Greater(mesh.bounds.size.x, 0.05f, "the body has width");
    }
}
