using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regain (Docs/CombatResearch.md Phase 3, build and spend): part of a blow that got through can be won back for a few seconds by striking
/// back, so a hit is answered with aggression, not by backing off to wait for a parry.
/// </summary>
public sealed class RegainTests
{
    [Test]
    public void WhatCanBeWonBack_DrainsOverTheWindow()
    {
        Assert.AreEqual(12f, RegainRules.Left(12f, 0f, 3f), 1e-4f);
        Assert.AreEqual(6f, RegainRules.Left(12f, 1.5f, 3f), 1e-4f);
        Assert.AreEqual(0f, RegainRules.Left(12f, 3.5f, 3f), 1e-4f, "gone once the window has passed");
    }

    [Test]
    public void StrikingBackWinsSomeBack_NeverMoreThanIsLeft()
    {
        Assert.AreEqual(5f, RegainRules.Won(12f, 10f, 0.5f), 1e-4f);
        Assert.AreEqual(3f, RegainRules.Won(3f, 20f, 0.5f), 1e-4f, "at most what is left");
        Assert.AreEqual(0f, RegainRules.Won(0f, 20f, 0.5f), 1e-4f);
    }

    [Test]
    public void ThePlayerHasRegain_AndTheSettingsTurnItOn()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        Assert.IsNotNull(player.GetComponent<PlayerRegain>(), "the player prefab carries PlayerRegain");
        CombatTechniqueSettings rules = Resources.Load<CombatTechniqueSettings>(CombatTechniqueSettings.ResourceName);
        Assert.Greater(rules.RegainShare, 0f);
        Assert.Greater(rules.RegainSeconds, 0f);
    }
}
