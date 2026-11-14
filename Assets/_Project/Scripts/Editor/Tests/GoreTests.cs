using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The killing-blow gore's rules and data (DeathRagdoll, BodyCutter; 2026-11-14). The cutting itself was checked in
/// Play on a draug (head, leg) and a wolf (head), and smashing on a wolf and a small troll.</summary>
public sealed class GoreTests
{
    [Test]
    public void WhatRidesWithAHead_GoesWithIt_AndNothingElseDoes()
    {
        var neck = new GameObject("Neck").transform;
        try
        {
            Transform head = Child(neck, "Head", Vector3.up * 0.1f);
            Transform jaw = Child(neck, "Jaw", Vector3.up * 0.1f);
            Transform ear = Child(neck, "Ear1.L", new Vector3(0.05f, 0.2f, 0f));
            Transform shoulder = Child(neck, "Shoulder.L", new Vector3(-0.2f, 0f, 0f));
            Assert.IsTrue(BodyCutter.RidesWith(jaw, head), "a twin on the same joint");
            Assert.IsTrue(BodyCutter.RidesWith(ear, head), "an ear beside the head");
            Assert.IsFalse(BodyCutter.RidesWith(shoulder, head), "not a shoulder");
            Assert.IsFalse(BodyCutter.RidesWith(ear, shoulder), "ears only ride with a head");
        }
        finally
        {
            Object.DestroyImmediate(neck.gameObject);
        }
    }

    [Test]
    public void SmallCreatures_CanBeSmashed_AndTheChancesAreSet()
    {
        foreach (string small in new[] { "Rootling", "ForestBittenWolf", "Boar", "SmallMossTroll" })
        {
            Assert.IsTrue(AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{small}.asset").CanBeSmashed, small);
        }

        Assert.IsFalse(AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/OldMossTroll.asset").CanBeSmashed, "not a giant");
        CombatFeelSettings feel = CombatFeelSettings.Current;
        Assert.That(feel.SmashChance, Is.InRange(0.05f, 0.95f));
        Assert.That(feel.SeverChance, Is.InRange(0.05f, 0.95f));
    }

    private static Transform Child(Transform parent, string name, Vector3 at)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = at;
        return child;
    }
}
