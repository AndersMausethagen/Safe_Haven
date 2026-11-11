using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the item icons painted with LocalSpawn (Tools/LocalSpawn/generate_item_icons.py -> Tools/LocalSpawn/ItemIcons/)
/// on their items, in place of the painted placeholders (run with `unity command script run --file
/// AgentScripts/BuildItemIcons.cs --entry BuildItemIcons.Apply`).
/// <para>
/// An item that is alone in using its icon file gets the new picture written into that same file, so the file keeps
/// its GUID and every content builder that loads it (BuildFood, BuildGear...) keeps the new art. Items that shared a
/// file (five meals had the stew) each get a file of their own in Art/Items/Icons. A builder that paints a placeholder
/// every time it runs would still put one back: run this again after it. ItemIconTests says if one has.
/// </para>
/// </summary>
public static class BuildItemIcons
{
    private const string Wanted = "Tools/LocalSpawn/item_icons.json";
    private const string Painted = "Tools/LocalSpawn/ItemIcons/";
    private const string Own = "Assets/_Project/Art/Items/Icons/";
    private const int MaxSize = 256;

    public static string Apply()
    {
        // The ids, from the keys of the JSON (Unity's reader does not take a free-form object).
        var ids = Regex.Matches(File.ReadAllText(Wanted), "^\\s*\"([^\"_][^\"]*)\"\\s*:", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var items = AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).Where(i => i != null).ToList();
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        var users = items.Where(i => i.Icon != null).GroupBy(i => AssetDatabase.GetAssetPath(i.Icon)).ToDictionary(g => g.Key, g => g.Count());

        int inPlace = 0, ownFile = 0;
        var missing = new List<string>();
        AssetDatabase.StartAssetEditing();
        var toImport = new List<(ItemDefinition item, string path)>();
        try
        {
            foreach (string id in ids)
            {
                string source = Painted + Regex.Replace(id.ToLowerInvariant(), "[^a-z0-9_]", "_") + ".png";
                if (!byId.TryGetValue(id, out ItemDefinition item) || !File.Exists(source))
                {
                    missing.Add(id);
                    continue;
                }

                string current = item.Icon != null ? AssetDatabase.GetAssetPath(item.Icon) : null;
                bool alone = current != null && current.EndsWith(".png") && users.TryGetValue(current, out int count) && count == 1;
                string target = alone ? current : Own + Path.GetFileName(source);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
                toImport.Add((item, target));
                if (alone)
                {
                    inPlace++;
                }
                else
                {
                    ownFile++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        foreach ((ItemDefinition item, string path) in toImport)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxSize;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var so = new SerializedObject(item);
            if (so.FindProperty("icon").objectReferenceValue != sprite)
            {
                so.FindProperty("icon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }

            // Only these assets: saving everything would also write what else the Editor has changed in memory.
            AssetDatabase.SaveAssetIfDirty(item);
        }

        return $"Item icons: {inPlace} written into the item's own icon file, {ownFile} given a file of their own"
               + (missing.Count > 0 ? $"; not done ({missing.Count}): {string.Join(", ", missing)}" : ".");
    }
}
