using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>What a playtest found broken, kept from breaking again.</summary>
public sealed class PlaytestFixesTests
{
    private const string RaidScene = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";

    [Test]
    public void NoMeshOfOurs_HasAVertexWithoutANormal()
    {
        // Such a vertex lights as NaN, and bloom spreads that over the screen as a huge flickering light.
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Mesh mesh && mesh.isReadable)
                {
                    foreach (Vector3 normal in mesh.normals)
                    {
                        Assert.Greater(normal.sqrMagnitude, 0.5f, $"{path}: {mesh.name}");
                    }
                }
            }
        }
    }

    [Test]
    public void TheCommandWheel_ShowsItsChoices()
    {
        var holder = new GameObject("Wheel");
        try
        {
            var wheel = holder.AddComponent<CommandWheel>();
            const BindingFlags inside = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(CommandWheel).GetMethod("BuildView", inside).Invoke(wheel, null);
            System.Type choice = typeof(CommandWheel).GetNestedType("Choice", BindingFlags.NonPublic);
            var choices = (IList)typeof(CommandWheel).GetField("choices", inside).GetValue(wheel);
            choices.Add(System.Activator.CreateInstance(choice, "Follow", null, null));
            typeof(CommandWheel).GetMethod("ShowPage", inside).Invoke(wheel, new object[] { "Choose" });

            var root = (GameObject)typeof(CommandWheel).GetField("root", inside).GetValue(wheel);
            Assert.IsTrue(wheel.IsOpen);
            bool found = false;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                found |= text.text == "Follow";
            }

            Assert.IsTrue(found);
            Assert.IsTrue(CommandWheel.IsHeld, "the camera has to let go of the cursor");
            wheel.Cancel();
            Assert.IsFalse(CommandWheel.IsHeld);
            Object.DestroyImmediate(root);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void EverySkinTone_HasAFaceOfTheSameTone()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterBodyDefinition"))
        {
            var body = AssetDatabase.LoadAssetAtPath<CharacterBodyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.AreEqual(body.Skins.Count, body.Faces.Count, body.name);
            for (int i = 0; i < body.Skins.Count; i++)
            {
                string tone = body.Skins[i].material.name.Replace("_Body_", "_Head_");
                StringAssert.StartsWith(tone, body.Faces[i].material.name, body.name);
            }
        }
    }

    [Test]
    public void ARaidScene_HasTheMenuItsEventsOpen()
    {
        string menu = AssetDatabase.AssetPathToGUID("Assets/_Project/Prefabs/UI/ActionMenuWindow.prefab");
        StringAssert.Contains(menu, System.IO.File.ReadAllText(RaidScene), "raid events with choices have nothing to open");
    }

    [Test]
    public void TheMistyWoods_AlwaysHaveMist()
    {
        var climate = AssetDatabase.LoadAssetAtPath<ClimateDefinition>("Assets/_Project/Data/Environment/Misty Woods Climate.asset");
        Assert.IsNotNull(climate);
        Assert.Greater(climate.Start.State.Mist, 0.5f);
        foreach (ClimateDefinition.Entry entry in climate.Weathers)
        {
            Assert.Greater(entry.weather.State.Mist, 0.5f, entry.weather.name);
        }

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(climate));
        StringAssert.Contains(guid, System.IO.File.ReadAllText(RaidScene));
    }

    [Test]
    public void TheMarker_TakesItsTime_AndTheSweetSpotCanBeHit()
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        GatheringToolProfile axe = settings.Profile(ToolType.Axe);
        Assert.GreaterOrEqual(axe.RiseSeconds, 2f, "faster than this and chopping is a test of reflexes");
        Assert.GreaterOrEqual(axe.CriticalZoneWidth * axe.RiseSeconds, 0.25f, "a quarter of a second to land it in");
        Assert.AreEqual(0f, settings.ZoneJitter, "the spot stays put, so it can be felt as a rhythm");
    }
}
