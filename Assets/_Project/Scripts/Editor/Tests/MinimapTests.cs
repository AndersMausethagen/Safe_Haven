using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The minimap: where things land on it, what it paints, and that it knows no more than the player does.</summary>
public sealed class MinimapTests
{
    private const float Metre = 2f;

    [Test]
    public void ItIsWired_WithAShaderThatCompiles()
    {
        MinimapSettings settings = MinimapSettings.Current;
        Assert.IsNotNull(settings.Material);
        Assert.AreEqual("Safe Haven/UI/Minimap", settings.Material.shader.name);
        Assert.IsTrue(settings.Material.shader.isSupported);
        float[] reaches = GameOptions.MinimapReaches;
        Assert.AreEqual(GameOptions.MinimapDistance.Choices.Length, reaches.Length, "a reach for every Minimap Distance");
        for (int i = 2; i < reaches.Length; i++) Assert.Less(reaches[i - 1], reaches[i], "each step reaches further");
        Assert.Greater(reaches[(int)GameOptions.MinimapDistance.Default], 28f, "the default reaches further than the old 28 m");
    }

    [Test]
    public void NorthIsUp_WhenTheCameraLooksNorth()
    {
        Vector2 north = MinimapHud.Project(Vector3.forward * 10f, 0f, Metre);
        Vector2 east = MinimapHud.Project(Vector3.right * 10f, 0f, Metre);
        Assert.AreEqual(0f, north.x, 0.001f);
        Assert.AreEqual(20f, north.y, 0.001f);
        Assert.AreEqual(20f, east.x, 0.001f);
        Assert.AreEqual(0f, east.y, 0.001f);
    }

    [Test]
    public void WhatTheCameraLooksAt_IsAlwaysUp()
    {
        Vector2 ahead = MinimapHud.Project(Vector3.right * 10f, 90f * Mathf.Deg2Rad, Metre);
        Assert.AreEqual(0f, ahead.x, 0.001f);
        Assert.AreEqual(20f, ahead.y, 0.001f);
        Vector2 leftOfThat = MinimapHud.Project(Vector3.forward * 10f, 90f * Mathf.Deg2Rad, Metre);
        Assert.AreEqual(-20f, leftOfThat.x, 0.001f);
        Assert.AreEqual(0f, leftOfThat.y, 0.001f);
    }

    [Test]
    public void TheMap_ShowsWhereATriangleCanBeWalked()
    {
        var area = new Rect(0f, 0f, 10f, 10f);
        Vector3[] corners = { new Vector3(1f, 0f, 1f), new Vector3(9f, 0f, 1f), new Vector3(1f, 0f, 9f) };
        bool[] cells = MinimapMap.Fill(area, 10, corners, new[] { 0, 1, 2 });
        Assert.IsTrue(cells[2 * 10 + 2], "inside");
        Assert.IsFalse(cells[9 * 10 + 9], "beyond its long edge");
        Assert.IsFalse(cells[0], "outside its corner");
    }

    [Test]
    public void ATriangleWoundTheOtherWay_FillsTheSame()
    {
        var area = new Rect(0f, 0f, 10f, 10f);
        Vector3[] corners = { new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 9f), new Vector3(9f, 0f, 1f) };
        Assert.IsTrue(MinimapMap.Fill(area, 10, corners, new[] { 0, 1, 2 })[2 * 10 + 2]);
    }

    [Test]
    public void ASmallTriangle_IsNotLostBetweenCells()
    {
        var area = new Rect(0f, 0f, 10f, 10f);
        Vector3[] corners = { new Vector3(5.2f, 0f, 5.2f), new Vector3(5.3f, 0f, 5.2f), new Vector3(5.2f, 0f, 5.3f) };
        Assert.IsTrue(MinimapMap.Fill(area, 10, corners, new[] { 0, 1, 2 })[5 * 10 + 5]);
    }

    [Test]
    public void AtNight_TheMapShowsCreaturesOnlyCloseBy()
    {
        MinimapSettings settings = MinimapSettings.Current;
        Assert.Less(settings.CreatureSight(1f), settings.CreatureSight(0f), "it knows no more than the player sees");
    }
}
