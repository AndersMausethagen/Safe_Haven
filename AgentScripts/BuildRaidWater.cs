using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Ponds, glowing plants and lighter, lower mist for the Misty Woods (`unity command script run --file AgentScripts/BuildRaidWater.cs
/// --entry BuildRaidWater.Apply`). The raid gets its own fog profiles, as the prologue and the story demo share the originals. Run
/// with nothing unsaved. Safe to run again.
/// </summary>
public static class BuildRaidWater
{
    private const string RulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string MapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string WaterPath = "Assets/_Project/Art/Generated/Water/Water Still.mat";
    private const string ScenePath = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
    private const string Volumes = "Assets/_Project/Settings/Rendering/Volumes/";

    public static string Apply()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        var log = new List<string> { Rules(), Plants() };
        AssetDatabase.SaveAssets();
        log.Add(MistInScene());
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static string Rules()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(RulesPath);
        var so = new SerializedObject(rules);
        so.FindProperty("ponds").vector2IntValue = new Vector2Int(1, 3);
        so.FindProperty("pondRadius").vector2Value = new Vector2(3.5f, 11f);
        so.FindProperty("pondDepth").vector2Value = new Vector2(0.7f, 1.6f);
        so.FindProperty("pondStretch").floatValue = 1.6f;
        so.FindProperty("pondMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
        // As Haven's drinking spot, so water works the same wherever the player finds it.
        so.FindProperty("pondDrink").floatValue = 100f;
        so.FindProperty("pondFillItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath("79744f7a29634cb408f4829b51ec5918"));
        so.FindProperty("pondFillUpTo").intValue = 3;
        so.FindProperty("wetReach").floatValue = 12f;
        so.FindProperty("dampGround").floatValue = 0.45f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rules);
        return "rules: 1-3 ponds, 3.5-11 m";
    }

    private static string Plants()
    {
        var map = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(MapPath);
        var plants = new List<TerrainGrowth.Plant>(TerrainGrowth.DefaultPlants)
        {
            new TerrainGrowth.Plant("Glow Flowers", 6f, 0.72f, 0.2f, 0, 0.3f),
            new TerrainGrowth.Plant("Glow Mushrooms", 5f, 0.74f, 0.22f, 0, 1f),
        };
        var so = new SerializedObject(map);
        SerializedProperty list = so.FindProperty("plants");
        list.arraySize = plants.Count;
        for (int i = 0; i < plants.Count; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("prototype").stringValue = plants[i].prototype;
            entry.FindPropertyRelative("enabled").boolValue = true;
            entry.FindPropertyRelative("patchSize").floatValue = Mathf.Round(plants[i].patchSize * 10f) / 10f;
            entry.FindPropertyRelative("rarity").floatValue = plants[i].rarity;
            entry.FindPropertyRelative("thickness").floatValue = plants[i].thickness;
            entry.FindPropertyRelative("clearing").intValue = plants[i].clearing;
            entry.FindPropertyRelative("loveOfWet").floatValue = plants[i].loveOfWet;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
        return $"plants: {plants.Count}, with glow flowers and mushrooms";
    }

    private static string MistInScene()
    {
        Scene raid = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var log = new List<string>();
            foreach (GameObject root in raid.GetRootGameObjects())
            {
                foreach (TerrainMist mist in root.GetComponentsInChildren<TerrainMist>(true))
                {
                    Set(mist, ("density", 0.18f), ("thickness", 2.4f), ("bottom", 0.05f), ("waveAmplitude", 0.35f), ("lowlandBoost", 1f), ("waterBoost", 1.2f));
                    log.Add("ground mist");
                }

                foreach (FogArea bank in root.GetComponentsInChildren<FogArea>(true))
                {
                    Set(bank, ("density", 0.035f), ("height", 2.2f));
                    log.Add("fog bank");
                }

                // The height field the mist lies on: its 2 m grid is coarser than a steep ridge, so thin mist would float off it.
                foreach (FogGround ground in root.GetComponentsInChildren<FogGround>(true))
                {
                    Set(ground, ("cellSize", 1f));
                    log.Add("fog ground");
                }

                foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
                {
                    string profile = volume.sharedProfile != null ? volume.sharedProfile.name : "";
                    if (profile == "MistyWoods_Volume" || profile == "MistyWoods_Raid_Volume")
                    {
                        volume.sharedProfile = Own(volume.sharedProfile, "MistyWoods_Raid_Volume");
                        Tune(volume.sharedProfile, 0.02f, 0.5f, 1f, 0.55f);
                        log.Add("day fog");
                    }
                    else if (profile == "MistyWoods_Night" || profile == "MistyWoods_Raid_Night")
                    {
                        volume.sharedProfile = Own(volume.sharedProfile, "MistyWoods_Raid_Night");
                        Tune(volume.sharedProfile, 0.018f, null, null, null);
                        log.Add("night fog");
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(raid);
            EditorSceneManager.SaveScene(raid);
            return "scene: " + string.Join(", ", log);
        }
        finally
        {
            EditorSceneManager.CloseScene(raid, true);
        }
    }

    private static VolumeProfile Own(VolumeProfile original, string name)
    {
        string path = Volumes + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) == null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), path);
        }

        return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    }

    private static void Tune(VolumeProfile profile, float density, float? baseHeight, float? follow, float? falloff)
    {
        profile.TryGet(out AtmosphericFog fog);
        fog.density.Override(density);
        if (baseHeight.HasValue)
        {
            fog.baseHeight.Override(baseHeight.Value);
        }

        if (follow.HasValue)
        {
            fog.followTerrain.Override(follow.Value);
        }

        if (falloff.HasValue)
        {
            fog.heightFalloff.Override(falloff.Value);
        }

        EditorUtility.SetDirty(fog);
        EditorUtility.SetDirty(profile);
    }

    private static void Set(Object target, params (string Field, float Value)[] values)
    {
        var so = new SerializedObject(target);
        foreach ((string field, float value) in values)
        {
            so.FindProperty(field).floatValue = value;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
