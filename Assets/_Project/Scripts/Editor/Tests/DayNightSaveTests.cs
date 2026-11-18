using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DayNightSaveTests
{
    private GameObject sky;

    [TearDown]
    public void TearDown()
    {
        if (sky != null)
        {
            Object.DestroyImmediate(sky);
        }
    }

    [Test]
    public void TheHourIsSavedAndRestored()
    {
        sky = new GameObject("Sky");
        var cycle = sky.AddComponent<DayNightCycle>();
        cycle.Hour = 15.5f;
        string state = cycle.CaptureState();
        cycle.Hour = 3f;

        // Saved in another run of the game (loaded from disk): no way to know the time away, so the hour as saved.
        string fromDisk = "15.5|" + new string('0', 32) + "|" + state.Split('|')[2];
        cycle.RestoreState(fromDisk);
        Assert.AreEqual(15.5f, cycle.Hour, 0.001f);

        cycle.Hour = 3f;
        cycle.RestoreState(state);
        Assert.AreEqual(15.5f, cycle.Hour, 0.05f, "back in the same run a moment later: the clock barely moved");
    }

    [Test]
    public void TimeAwayOnARaidMovesTheClockOn()
    {
        sky = new GameObject("Sky");
        var cycle = sky.AddComponent<DayNightCycle>();
        var data = new SerializedObject(cycle);
        data.FindProperty("profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DayNightProfile>(
            AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:DayNightProfile")[0]));
        data.ApplyModifiedPropertiesWithoutUndo();
        cycle.Hour = 10f;
        string[] parts = cycle.CaptureState().Split('|');

        // Captured an in-game hour's worth of real seconds ago in this run.
        double hourSeconds = cycle.Profile.DayLengthMinutes * 60.0 / 24.0;
        double then = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) - hourSeconds;
        cycle.RestoreState($"{parts[0]}|{parts[1]}|{then.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
        Assert.AreEqual(11f, cycle.Hour, 0.05f);
    }

    [Test]
    public void TimeAway_IsGameTime_AndOnlyInTheSameRun()
    {
        // The one clock time away is counted on (the day, the nodes' regrowth): a stamp from this run gives the game seconds since,
        // one from another run (from disk, a friend's game) nothing.
        double then = DayNightCycle.AwayStamp - 30.0;
        Assert.AreEqual(30f, DayNightCycle.SecondsAwaySince(DayNightCycle.Run, then), 0.01f);
        Assert.AreEqual(0f, DayNightCycle.SecondsAwaySince(new string('0', 32), then));
        Assert.AreEqual(0f, DayNightCycle.SecondsAwaySince(DayNightCycle.Run, DayNightCycle.AwayStamp + 5.0), "never less than nothing");
        Assert.AreEqual(Time.timeAsDouble, DayNightCycle.AwayStamp, 0.01, "game time: still while paused, unlike real time");
    }

    [Test]
    public void TheHavensClockIsSaved()
    {
        string scene = System.IO.File.ReadAllText("Assets/_Project/Scenes/SafeHaven.unity");
        string cycleGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/Environment/DayNightCycle.cs");
        string entityGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/Save/SaveableEntity.cs");
        int cycleAt = scene.IndexOf(cycleGuid, System.StringComparison.Ordinal);
        Assert.Greater(cycleAt, 0);
        // The SaveableEntity sits on the same GameObject as the cycle.
        string objectOfCycle = Owner(scene, cycleAt);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.Matches(scene, entityGuid).Count > 0);
        bool together = false;
        int at = -1;
        while ((at = scene.IndexOf(entityGuid, at + 1, System.StringComparison.Ordinal)) >= 0)
        {
            together |= Owner(scene, at) == objectOfCycle;
        }

        Assert.IsTrue(together, "the Day Night object has a SaveableEntity");
    }

    // The m_GameObject fileID of the component document containing position <paramref name="at"/>.
    private static string Owner(string scene, int at)
    {
        int start = scene.LastIndexOf("--- !u!", at, System.StringComparison.Ordinal);
        int owner = scene.IndexOf("m_GameObject: {fileID: ", start, System.StringComparison.Ordinal) + "m_GameObject: {fileID: ".Length;
        return scene.Substring(owner, scene.IndexOf('}', owner) - owner);
    }
}
