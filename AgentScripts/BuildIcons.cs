using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Puts the icons painted with LocalSpawn (Tools/LocalSpawn/generate_item_icons.py --set NAME -> Tools/LocalSpawn/
/// &lt;Set&gt;Icons/) on their items, buildings, combat skills, skills and perks, in place of the painted placeholders and
/// the 3D renders. Run with `unity command script run --file AgentScripts/BuildIcons.cs --entry BuildIcons.All` (or
/// Items, Buildings, Skills, Proficiencies, Perks, Essences, Conditions for one kind).
/// <para>
/// A definition that is alone in using its icon file gets the new picture written into that same file, so the file
/// keeps its GUID and every content builder that loads it (BuildFood, BuildSkigard...) keeps the new art. Those that
/// shared a file (five meals had the stew, the stone wall had the palisade's) each get a file of their own, and those
/// with none (skills, perks) get one. A builder that renders or paints its icon every time it runs (BuildDock,
/// BuildTorch, BuildTransport...) still puts its own back: run this again after it. ItemIconTests and PaintedIconTests
/// say if one has.
/// </para>
/// </summary>
public static class BuildIcons
{
    private const int MaxSize = 256;

    /// <summary>One kind of icon: its list of what each shows, the painted folder and where new files go.</summary>
    public sealed class Set
    {
        public string Wanted;
        public string Painted;
        public string Own;
        public Func<IEnumerable<(string id, Object definition, Sprite icon)>> Definitions;
    }

    public static readonly Set ItemSet = new Set
    {
        Wanted = "Tools/LocalSpawn/item_icons.json", Painted = "Tools/LocalSpawn/ItemIcons/", Own = "Assets/_Project/Art/Items/Icons/",
        Definitions = () => All<ItemDefinition>().Select(i => (i.Id, (Object)i, i.Icon)),
    };

    public static readonly Set BuildingSet = new Set
    {
        Wanted = "Tools/LocalSpawn/building_icons.json", Painted = "Tools/LocalSpawn/BuildingIcons/", Own = "Assets/_Project/Art/Icons/Buildings/",
        Definitions = () => All<BuildingDefinition>().Select(b => (b.Id, (Object)b, b.Icon)),
    };

    public static readonly Set SkillSet = new Set
    {
        Wanted = "Tools/LocalSpawn/skill_icons.json", Painted = "Tools/LocalSpawn/SkillIcons/", Own = "Assets/_Project/Art/Icons/Skills/",
        Definitions = () => All<SkillDefinition>().Select(s => (s.Id, (Object)s, s.Icon)),
    };

    public static readonly Set ProficiencySet = new Set
    {
        Wanted = "Tools/LocalSpawn/proficiency_icons.json", Painted = "Tools/LocalSpawn/ProficiencyIcons/", Own = "Assets/_Project/Art/Icons/Proficiencies/",
        Definitions = () => All<ProficiencyDefinition>().Select(p => (p.Id, (Object)p, p.Icon)),
    };

    public static readonly Set PerkSet = new Set
    {
        Wanted = "Tools/LocalSpawn/perk_icons.json", Painted = "Tools/LocalSpawn/PerkIcons/", Own = "Assets/_Project/Art/Icons/Perks/",
        Definitions = () => All<PerkDefinition>().Select(p => (p.Id, (Object)p, p.Icon)),
    };

    public static readonly Set ResourceSet = new Set
    {
        Wanted = "Tools/LocalSpawn/resource_icons.json", Painted = "Tools/LocalSpawn/ResourceIcons/", Own = "Assets/_Project/Art/Icons/Resources/",
        Definitions = () => All<ResourceDefinition>().Select(r => (r.Id, (Object)r, r.Icon)),
    };

    public static readonly Set ConditionSet = new Set
    {
        Wanted = "Tools/LocalSpawn/condition_icons.json", Painted = "Tools/LocalSpawn/ConditionIcons/", Own = "Assets/_Project/Art/Icons/Conditions/",
        Definitions = () => All<StatusEffectDefinition>().Select(s => (s.Id, (Object)s, s.Icon)),
    };

    public static string Items() => Apply(ItemSet, "Item");
    public static string Essences() => Apply(ResourceSet, "Essence and portal light");
    public static string Conditions() => Apply(ConditionSet, "Condition");
    public static string Buildings() => Apply(BuildingSet, "Building");
    public static string Skills() => Apply(SkillSet, "Combat skill");
    public static string Proficiencies() => Apply(ProficiencySet, "Skill");
    public static string Perks() => Apply(PerkSet, "Perk");
    public static string All() => string.Join("\n", Items(), Buildings(), Skills(), Proficiencies(), Perks(), Essences(), Conditions());

    /// <summary>The ids in a set's list, from the keys of its JSON (Unity's reader does not take a free-form object).</summary>
    public static List<string> Ids(Set set) =>
        Regex.Matches(File.ReadAllText(set.Wanted), "^\\s*\"([^\"_][^\"]*)\"\\s*:", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

    /// <summary>Where the painting for an id is (generate_item_icons.py's file_name).</summary>
    public static string PaintedFile(Set set, string id) => set.Painted + Regex.Replace(id.ToLowerInvariant(), "[^a-z0-9_]", "_") + ".png";

    private static IEnumerable<T> All<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null);

    private static string Apply(Set set, string label)
    {
        List<string> ids = Ids(set);
        var definitions = set.Definitions().ToList();
        var byId = definitions.GroupBy(d => d.id).ToDictionary(g => g.Key, g => g.First());
        var users = definitions.Where(d => d.icon != null).GroupBy(d => AssetDatabase.GetAssetPath(d.icon)).ToDictionary(g => g.Key, g => g.Count());

        int inPlace = 0, ownFile = 0;
        var missing = new List<string>();
        var toImport = new List<(Object definition, string path, string source)>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string id in ids)
            {
                string source = PaintedFile(set, id);
                if (!byId.TryGetValue(id, out var found) || !File.Exists(source))
                {
                    missing.Add(id);
                    continue;
                }

                string current = found.icon != null ? AssetDatabase.GetAssetPath(found.icon) : null;
                bool alone = current != null && current.EndsWith(".png") && users.TryGetValue(current, out int count) && count == 1;
                string target = alone ? current : set.Own + Path.GetFileName(source);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                WriteHalved(source, target);
                toImport.Add((found.definition, target, source));
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

        foreach ((Object definition, string path, string source) in toImport)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // Icons are drawn from 22 to 96 px: without mips the small ones turn grainy and shimmer as they move.
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = MaxSize;
            importer.userData = PaintedMark(source);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var so = new SerializedObject(definition);
            if (so.FindProperty("icon").objectReferenceValue != sprite)
            {
                so.FindProperty("icon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            // Only these assets: saving everything would also write what else the Editor has changed in memory.
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        return $"{label} icons: {inPlace} written into the definition's own icon file, {ownFile} given a file of their own"
               + (missing.Count > 0 ? $"; not done ({missing.Count}): {string.Join(", ", missing)}" : ".");
    }

    /// <summary>
    /// What an icon's importer remembers of the painting it was made from (its userData): the painting's hash, so a test can tell
    /// the icon in the game is that painting and not a placeholder a builder put back.
    /// </summary>
    public static string PaintedMark(string source)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            return "painted:" + System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(source))).Replace("-", "").ToLowerInvariant();
        }
    }

    // The painting at half its size (512 -> 256, the size it is imported at), so the project doesn't keep a second full-size copy.
    // Each pixel is the alpha-weighted mean of four: transparent pixels don't darken the edges. Any other size is copied as it is.
    private static void WriteHalved(string source, string target)
    {
        var painting = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        painting.LoadImage(File.ReadAllBytes(source));
        if (painting.width != MaxSize * 2 || painting.height != MaxSize * 2)
        {
            Object.DestroyImmediate(painting);
            File.Copy(source, target, true);
            return;
        }

        Color32[] from = painting.GetPixels32();
        var to = new Color32[MaxSize * MaxSize];
        int wide = painting.width;
        for (int y = 0; y < MaxSize; y++)
        {
            for (int x = 0; x < MaxSize; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int dy = 0; dy < 2; dy++)
                {
                    for (int dx = 0; dx < 2; dx++)
                    {
                        Color32 c = from[(y * 2 + dy) * wide + x * 2 + dx];
                        r += c.r * c.a;
                        g += c.g * c.a;
                        b += c.b * c.a;
                        a += c.a;
                    }
                }

                to[y * MaxSize + x] = a > 0f
                    ? new Color32((byte)Mathf.RoundToInt(r / a), (byte)Mathf.RoundToInt(g / a), (byte)Mathf.RoundToInt(b / a), (byte)Mathf.RoundToInt(a / 4f))
                    : new Color32(0, 0, 0, 0);
            }
        }

        var half = new Texture2D(MaxSize, MaxSize, TextureFormat.RGBA32, false);
        half.SetPixels32(to);
        half.Apply();
        File.WriteAllBytes(target, half.EncodeToPNG());
        Object.DestroyImmediate(painting);
        Object.DestroyImmediate(half);
    }
}
