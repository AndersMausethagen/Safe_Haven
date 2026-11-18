using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>What a playtest found broken, kept from breaking again.</summary>
public sealed class PlaytestFixesTests
{
    private const string RaidScene = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";

    [Test]
    public void NoMeshOfOurs_HasAVertexWithoutANormal()
    {
        // Such a vertex lights as NaN, and bloom spreads that over the screen as a huge flickering light.
        // Read through MeshData, which in the Editor reads a mesh imported without Read/Write too: nearly all our models are, and
        // mesh.normals would have skipped them.
        var broken = new List<string>();
        int judged = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is Mesh mesh))
                {
                    continue;
                }

                using (Mesh.MeshDataArray meshes = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    Mesh.MeshData data = meshes[0];
                    if (!data.HasVertexAttribute(VertexAttribute.Normal))
                    {
                        continue;
                    }

                    judged++;
                    using (var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                    {
                        data.GetNormals(normals);
                        for (int i = 0; i < normals.Length; i++)
                        {
                            if (!(normals[i].sqrMagnitude > 0.5f))
                            {
                                broken.Add($"{path}: {mesh.name}, vertex {i}");
                                break;
                            }
                        }
                    }
                }
            }
        }

        Assert.Greater(judged, 50, "the project's meshes were read");
        Assert.IsEmpty(broken, "vertices without a normal:\n" + string.Join("\n", broken));
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
        foreach (string scene in new[] { RaidScene, "Assets/_Project/Scenes/Raids/Raid_TrollValley.unity" })
        {
            StringAssert.Contains(menu, System.IO.File.ReadAllText(scene), $"{scene}: raid events with choices have nothing to open");
        }
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
        // The swing became 35% quicker on request (2026-11-09: the rise 2.2 s -> 1.63 s); the zones widened with it, so the
        // time to land a blow stays what playtests asked for, and only the rhythm is quicker.
        Assert.GreaterOrEqual(axe.RiseSeconds, 1.6f, "faster than this and chopping is a test of reflexes");
        Assert.GreaterOrEqual(axe.CriticalZoneWidth * axe.RiseSeconds, 0.25f, "a quarter of a second to land it in");
        GatheringToolProfile pickaxe = settings.Profile(ToolType.Pickaxe);
        Assert.GreaterOrEqual(pickaxe.CriticalZoneWidth * pickaxe.RiseSeconds, 0.25f, "mining too");
        Assert.AreEqual(0f, settings.ZoneJitter, "the spot stays put, so it can be felt as a rhythm");
    }
}
