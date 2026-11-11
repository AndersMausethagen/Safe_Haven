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
        var toImport = new List<(Object definition, string path)>();
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
                File.Copy(source, target, true);
                toImport.Add((found.definition, target));
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

        foreach ((Object definition, string path) in toImport)
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
}
