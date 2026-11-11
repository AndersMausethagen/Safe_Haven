using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts LocalSpawn's interface paintings (Tools/LocalSpawn/generate_ui_art.py, generate_item_icons.py --set boons) into the game:
/// residents' portraits for the People page, each raid's place painting and results artwork, and the boon icons beside the vitals
/// (Resources/ConditionIconSettings). Run with `unity command script run --file AgentScripts/BuildUIArt.cs --entry BuildUIArt.All`
/// (or Residents, Raids, Boons). Tutorial pictures and creature portraits are BuildJournal's. Re-running only copies what changed.
/// </summary>
public static class BuildUIArt
{
    private const string PaintedResidents = "Tools/LocalSpawn/ResidentPortraits/";
    private const string PaintedRaids = "Tools/LocalSpawn/RaidArt/";
    private const string PaintedBoons = "Tools/LocalSpawn/BoonIcons/";
    private const string ResidentArt = "Assets/_Project/Art/UI/Residents/";
    private const string RaidArt = "Assets/_Project/Art/UI/Raids/";
    private const string BoonArt = "Assets/_Project/Art/Icons/Boons/";
    private const string ConditionIcons = "Assets/_Project/Resources/ConditionIconSettings.asset";

    public static string All() => string.Join("\n", Residents(), Raids(), Boons());

    public static string Residents()
    {
        int done = 0;
        foreach (ResidentDefinition resident in Find<ResidentDefinition>())
        {
            string source = PaintedResidents + resident.name + ".png";
            if (!File.Exists(source))
            {
                continue;
            }

            Set(resident, "portrait", CopyIn(source, ResidentArt + resident.name + ".png", 512));
            done++;
        }

        return $"{done} residents given their portrait.";
    }

    public static string Raids()
    {
        var log = new List<string>();
        foreach (RaidDefinition raid in Find<RaidDefinition>())
        {
            int before = log.Count;
            foreach ((string field, string suffix) in new[] { ("placeArtwork", ""), ("extractedArtwork", "_extracted"), ("failedArtwork", "_failed") })
            {
                string source = PaintedRaids + raid.name + suffix + ".png";
                if (File.Exists(source))
                {
                    Set(raid, field, CopyIn(source, RaidArt + raid.name + suffix + ".png", 2048, false));
                    log.Add(field);
                }
            }

            if (log.Count == before)
            {
                log.Add($"({raid.name}: none painted)");
            }
        }

        int painted = log.Count(l => !l.StartsWith("("));
        string missing = string.Join(" ", log.Where(l => l.StartsWith("(")));
        return "Raid art: " + painted + " paintings put in. " + missing;
    }

    public static string Boons()
    {
        var settings = AssetDatabase.LoadAssetAtPath<ConditionIconSettings>(ConditionIcons);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ConditionIconSettings>();
            AssetDatabase.CreateAsset(settings, ConditionIcons);
        }

        var so = new SerializedObject(settings);
        SerializedProperty list = so.FindProperty("boonIcons");
        list.ClearArray();
        int done = 0;
        foreach (string file in Directory.Exists(PaintedBoons) ? Directory.GetFiles(PaintedBoons, "*.png").OrderBy(f => f) : Enumerable.Empty<string>())
        {
            string key = Path.GetFileNameWithoutExtension(file);
            Sprite icon = CopyIn(file.Replace('\\', '/'), BoonArt + key + ".png", 256);
            if (key == "boon")
            {
                so.FindProperty("otherBoon").objectReferenceValue = icon;
            }
            else
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("idStartsWith").stringValue = key;
                entry.FindPropertyRelative("icon").objectReferenceValue = icon;
            }

            done++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(settings);
        return $"{done} boon icons in ConditionIconSettings.";
    }

    private static IEnumerable<T> Find<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null);

    private static void Set(Object target, string field, Sprite sprite)
    {
        var so = new SerializedObject(target);
        if (so.FindProperty(field).objectReferenceValue != sprite)
        {
            so.FindProperty(field).objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Only this asset: saving everything would also write what else the Editor has changed in memory.
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }

    // A painting copied into the project as a sprite, unless the same picture is there already.
    private static Sprite CopyIn(string source, string path, int maxSize, bool mipmaps = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source)))
        {
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != maxSize || importer.mipmapEnabled != mipmaps)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // Mips for what is also drawn small (a portrait in a list row, a tutorial's thumbnail): none for full-screen art.
            importer.mipmapEnabled = mipmaps;
            importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
