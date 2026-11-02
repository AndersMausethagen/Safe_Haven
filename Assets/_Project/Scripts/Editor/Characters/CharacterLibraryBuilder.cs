using System.Collections.Generic;
using System.Linq;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the character library from the imported Stylized Modular Human packs (Assets/StylizedCharacter): the male and
/// female CharacterBodyDefinitions, the EmoteDefinitions, the CharacterCatalog in Resources, and a GearLookDefinition for
/// every armour item (assigned to the item's worn look). Safe to run again: existing assets are updated in place, so
/// references to them survive. Hand edits to these assets are overwritten.
/// </summary>
public static class CharacterLibraryBuilder
{
    public const string PackRoot = "Assets/StylizedCharacter";
    public const string DataRoot = "Assets/_Project/Data/Characters";
    public const string EmoteRoot = DataRoot + "/Emotes";
    public const string GearRoot = DataRoot + "/GearLooks";
    public const string CatalogPath = "Assets/_Project/Resources/CharacterCatalog.asset";
    public const string MaleClipsPath = PackRoot + "/Animations/Character/Human/Male/A_Hu_M_BasePack.fbx";
    public const string FemaleClipsPath = PackRoot + "/Animations/Character/Human/Female/A_Hu_F_BasePack.fbx";

    private static readonly string[] HairColourCodes = { "Bd", "Bk", "Br", "Gr" };
    private static readonly string[] HairColourNames = { "Blond", "Black", "Brown", "Grey" };

    private static Dictionary<string, GameObject> prefabs;
    private static Dictionary<string, Material> materials;

    [MenuItem("Safe Haven/Characters/Build Character Library")]
    public static void BuildFromMenu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        Index();
        EnsureFolder(DataRoot);
        EnsureFolder(EmoteRoot);
        EnsureFolder(GearRoot);

        CharacterBodyDefinition male = BuildBody(CharacterGender.Male);
        CharacterBodyDefinition female = BuildBody(CharacterGender.Female);
        List<EmoteDefinition> emotes = BuildEmotes();
        CharacterCatalog catalog = BuildCatalog(male, female, emotes);
        int looks = BuildGearLooks();
        BuildPlainClothes(catalog);

        AssetDatabase.SaveAssets();
        return $"Bodies: {male.name}, {female.name}; {emotes.Count} emotes; catalog {AssetDatabase.GetAssetPath(catalog)}; {looks} armour looks.";
    }

    /// <summary>The tunics and trousers (and shoes) worn while no armour is: a different tunic cut from the woven hide jerkin's.</summary>
    public static int BuildPlainClothes(CharacterCatalog catalog)
    {
        Index();
        EnsureFolder(GearRoot);
        // Cuts and colours no armour uses, so putting on the first armour visibly changes something.
        var colours = new[] { ("Undyed", "02_Br"), ("Blue", "01_Bl"), ("Red", "01_Rd") };
        var clothes = new List<NamedLook>();
        foreach ((string label, string cut) in colours)
        {
            string c = cut.Substring(3);
            GearLookDefinition look = Look($"Plain_{label}", g => new[] { Piece($"Hu_{g}_Chest_Peasant_{cut}"), Overlay($"Pants_Peasant_U_{c}") });
            clothes.Add(new NamedLook { label = label, look = look });
        }

        GearLookDefinition shoes = Look("Plain_Shoes", g => new[] { Piece($"Hu_{g}_Boots_Peasant_Br") });
        catalog.EditorSetPlainClothes(clothes, shoes);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return clothes.Count;
    }

    // ---- Bodies -------------------------------------------------------------------------------------------------

    private static CharacterBodyDefinition BuildBody(CharacterGender gender)
    {
        string g = gender == CharacterGender.Female ? "F" : "M";
        string folder = gender == CharacterGender.Female ? "Female" : "Male";
        string modelPath = $"{PackRoot}/Meshes/Character/Human/{folder}/SK_Hu_{g}_FullBody.fbx";
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        if (model == null || avatar == null)
        {
            throw new System.InvalidOperationException($"The {folder} body model or its avatar is missing at {modelPath}.");
        }

        var skins = new List<NamedMaterial>();
        var faces = new List<NamedMaterial>();
        var underclothes = new List<NamedMaterial>();
        for (int i = 1; i <= 5; i++)
        {
            skins.Add(Named($"Tone {i}", $"M_HU_{g}_Body_0{i}"));
            faces.Add(Named($"Face {i}", $"M_HU_{g}_Head_0{i}_A"));
            underclothes.Add(Named(UnderclothesName(i), $"M_HU_{g}_Body_Cloth_0{i}"));
        }

        var eyes = new List<NamedMaterial>
        {
            Named("Blue", "M_HU_Eye_Bl"),
            Named("Brown", "M_HU_Eye_Br"),
            Named("Green", "M_HU_Eye_Gn"),
            Named("Pale", "M_HU_Eye_Pe"),
        };

        var stubble = HairColourCodes.Select(c => Mat($"M_HU_{g}_UH_{c}")).ToList();
        var hairStyles = new List<ColouredPart>();
        var brows = new List<ColouredPart>();
        var browTints = new List<Material>();
        var beards = new List<ColouredPart>();
        var swaps = new List<ClipSwap>();

        if (gender == CharacterGender.Female)
        {
            int[] styles = { 1, 2, 3, 4, 7, 8, 10 };
            for (int i = 0; i < styles.Length; i++)
            {
                hairStyles.Add(Coloured($"Style {i + 1}", c => $"Hu_F_Hair_{styles[i]:00}_{c}"));
            }

            // The female head carries its own brows; they only take the hair colour.
            brows.Add(new ColouredPart { label = "Natural" });
            browTints = HairColourCodes.Select(c => Mat($"M_HU_F_Facials_{c}")).ToList();
            swaps = FemaleClipSwaps();
        }
        else
        {
            int[] styles = { 1, 2, 3, 4, 9 };
            for (int i = 0; i < styles.Length; i++)
            {
                hairStyles.Add(Coloured($"Style {i + 1}", c => $"Hu_M_Hair_{styles[i]:00}_{c}"));
            }

            hairStyles.Add(new ColouredPart { label = "Shaved" });
            for (int i = 1; i <= 4; i++)
            {
                brows.Add(Coloured($"Brows {i}", c => $"Hu_M_Brows_0{i}_{c}"));
            }

            beards.Add(new ColouredPart { label = "None" });
            int[] beardStyles = { 1, 2, 3, 5, 6, 7 };
            for (int i = 0; i < beardStyles.Length; i++)
            {
                beards.Add(Coloured($"Beard {i + 1}", c => $"Hu_M_Beard_{beardStyles[i]:00}_{c}"));
            }
        }

        var body = LoadOrCreate<CharacterBodyDefinition>($"{DataRoot}/CharacterBody_{folder}.asset");
        body.EditorSet(gender, model, avatar, skins, faces, eyes, underclothes, Mat($"M_HU_{g}_Body_Preview"), Mat($"M_HU_{g}_Head_Preview"),
            HairColourNames.ToList(), stubble, hairStyles, brows, browTints, beards, swaps);
        EditorUtility.SetDirty(body);
        return body;
    }

    private static string UnderclothesName(int i) => i switch
    {
        1 => "Linen",
        2 => "Wool",
        3 => "Russet",
        4 => "Moss",
        _ => "Ash",
    };

    // Every clip of the male base pack mapped to the female clip of the same name.
    private static List<ClipSwap> FemaleClipSwaps()
    {
        Dictionary<string, AnimationClip> femaleClips = Clips(FemaleClipsPath);
        var swaps = new List<ClipSwap>();
        foreach (KeyValuePair<string, AnimationClip> pair in Clips(MaleClipsPath))
        {
            if (femaleClips.TryGetValue(pair.Key, out AnimationClip own))
            {
                swaps.Add(new ClipSwap { from = pair.Value, to = own });
            }
        }

        return swaps;
    }

    public static Dictionary<string, AnimationClip> Clips(string path)
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
        {
            if (!clip.name.StartsWith("__preview__") && !clips.ContainsKey(clip.name))
            {
                clips.Add(clip.name, clip);
            }
        }

        return clips;
    }

    // ---- Emotes -------------------------------------------------------------------------------------------------

    private static List<EmoteDefinition> BuildEmotes()
    {
        Dictionary<string, AnimationClip> clips = Clips(MaleClipsPath);
        var list = new List<EmoteDefinition>();

        void OneShot(string asset, string display, string clip, bool wheel)
        {
            list.Add(Emote(asset, display, Clip(clips, clip), null, null, false, wheel));
        }

        void Held(string asset, string display, string stem, bool wheel)
        {
            clips.TryGetValue($"Emote_{stem}_Start", out AnimationClip begin);
            clips.TryGetValue($"Emote_{stem}_End", out AnimationClip end);
            list.Add(Emote(asset, display, Clip(clips, $"Emote_{stem}_Loop"), begin, end, true, wheel));
        }

        // The command wheel offers eight: the first eight marked for it.
        OneShot("Wave", "Wave", "Emote_Wave", true);
        OneShot("Salute", "Salute", "Emote_Salute", true);
        OneShot("Laugh", "Laugh", "Emote_Laugh", true);
        OneShot("Applaud", "Cheer", "Emote_Applaud", true);
        OneShot("Shout", "Shout", "Emote_Shout", true);
        OneShot("Cry", "Weep", "Emote_Cry", true);
        Held("SitGround", "Sit", "SitGround", true);
        Held("Kneel", "Kneel", "Kneel", true);

        OneShot("Roar", "Battle Cry", "Emote_Roar", false);
        OneShot("Rude", "Scorn", "Emote_Rude", false);
        OneShot("Forging", "Hammer", "Emote_Forging", false);
        OneShot("Loot", "Gather", "Loot", false);
        OneShot("Talk", "Talk", "Emote_Talk", false);
        OneShot("Talk2H", "Talk (Both Hands)", "Emote_Talk2H", false);
        OneShot("Talk2HL", "Talk (Explain)", "Emote_Talk2HL", false);
        OneShot("TalkQuestion", "Ask", "Emote_TalkQuestion", false);
        OneShot("TalkExclamation", "Exclaim", "Emote_TalkExclamation", false);
        Held("SitChair", "Sit on a Seat", "SitChair", false);
        Held("Sleep", "Sleep", "Sleep", false);
        Held("Read", "Read", "Read", false);
        Held("Use", "Work", "Use", false);
        Held("Fishing", "Fish", "Fishing", false);
        return list;
    }

    private static EmoteDefinition Emote(string asset, string display, AnimationClip clip, AnimationClip begin, AnimationClip end, bool held, bool wheel)
    {
        var emote = LoadOrCreate<EmoteDefinition>($"{EmoteRoot}/{asset}.asset");
        emote.EditorSet(display, clip, begin, end, held, wheel);
        EditorUtility.SetDirty(emote);
        return emote;
    }

    private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name) =>
        clips.TryGetValue(name, out AnimationClip clip) ? clip : throw new System.InvalidOperationException($"Clip {name} is missing from the pack.");

    // ---- Catalog ------------------------------------------------------------------------------------------------

    private static CharacterCatalog BuildCatalog(CharacterBodyDefinition male, CharacterBodyDefinition female, List<EmoteDefinition> emotes)
    {
        EmoteDefinition Find(string name) => emotes.First(e => e.name == name);
        var catalog = LoadOrCreate<CharacterCatalog>(CatalogPath);
        var start = new CharacterAppearance { gender = CharacterGender.Male, skin = 1, hair = 0, hairColour = 2, brows = 0, beard = 2, eyes = 1 };
        var maleNames = new List<string>
        {
            "Halvar", "Torvald", "Einar", "Leif", "Ragnar", "Sigurd", "Asmund", "Bjorn", "Gunnar", "Ivar", "Ketil", "Ulf", "Orm", "Vidar", "Hakon",
        };
        var femaleNames = new List<string>
        {
            "Astrid", "Freydis", "Gudrun", "Hilde", "Sigrun", "Solveig", "Ylva", "Thora", "Ragna", "Ase", "Embla", "Gyda", "Tove", "Ingeborg", "Valdis",
        };

        var speaking = new List<EmoteDefinition> { Find("Talk"), Find("Talk2HL"), Find("TalkQuestion"), Find("Talk2H") };
        catalog.EditorSet(male, female, start, maleNames, femaleNames, emotes, speaking, Find("TalkExclamation"), Find("SitGround"));
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    // ---- Gear ---------------------------------------------------------------------------------------------------

    private sealed class Tier
    {
        public string Name;
        public string Colour;
        public int Chest;
        public bool Cape;
        public bool PantsMesh;
        public string Head, Body, Hands, Waist, Feet;
    }

    private static int BuildGearLooks()
    {
        var tiers = new[]
        {
            new Tier { Name = "WovenHide", Colour = "Br", Chest = 1, Head = "WovenHideCap", Body = "WovenHideJerkin", Hands = "WovenHideGloves", Waist = "WovenCordBelt", Feet = "WovenHideBoots" },
            new Tier { Name = "CuredLeather", Colour = "Rd", Chest = 2, Head = "Armor/CuredLeatherCap", Body = "Armor/CuredLeatherJack", Hands = "Armor/CuredLeatherGloves", Waist = "Armor/CuredLeatherBelt", Feet = "Armor/CuredLeatherBoots" },
            new Tier { Name = "CopperScale", Colour = "Rd", Chest = 3, Cape = true, Head = "Armor/CopperScaleHelm", Body = "Armor/CopperScaleHauberk", Hands = "Armor/CopperScaleGauntlets", Waist = "Armor/CopperScaleGirdle", Feet = "Armor/CopperScaleGreaves" },
            new Tier { Name = "IronMail", Colour = "Bl", Chest = 3, PantsMesh = true, Head = "Armor/IronMailHelm", Body = "Armor/IronMailHauberk", Hands = "Armor/IronMailGauntlets", Waist = "Armor/IronMailBelt", Feet = "Armor/IronMailBoots" },
            // The moonsilver circlet is a thin band: no helm over the hair.
            new Tier { Name = "Moonsilver", Colour = "Bl", Chest = 2, Cape = true, Head = null, Body = "Armor/MoonsilverHauberk", Hands = "Armor/MoonsilverGloves", Waist = "Armor/MoonsilverSash", Feet = "Armor/MoonsilverBoots" },
        };

        int count = 0;
        foreach (Tier tier in tiers)
        {
            string c = tier.Colour;
            if (tier.Head != null)
            {
                count += Assign(tier.Head, Look($"{tier.Name}_Head", g => new[] { Piece($"Hu_{g}_Helm_Peasant_{c}", BodyCover.Hair) }));
            }

            count += Assign(tier.Body, Look($"{tier.Name}_Body", g =>
            {
                var pieces = new List<WornPiece> { Piece($"Hu_{g}_Chest_Peasant_{tier.Chest:00}_{c}") };
                pieces.Add(Overlay($"Pants_Peasant_U_{c}"));
                if (tier.PantsMesh)
                {
                    pieces.Add(Piece($"Hu_{g}_Pants"));
                }

                if (tier.Cape)
                {
                    pieces.Add(Piece($"Hu_{g}_Cape_Peasant_{c}"));
                }

                return pieces;
            }));
            count += Assign(tier.Hands, Look($"{tier.Name}_Hands", g => new[] { Overlay($"GlovesSkin_Peasant_{c}") }));
            count += Assign(tier.Waist, Look($"{tier.Name}_Waist", g => new[] { Piece($"Hu_{g}_Belt_Peasant_{c}") }));
            count += Assign(tier.Feet, Look($"{tier.Name}_Feet", g => new[] { Piece($"Hu_{g}_Boots_Peasant_{c}") }));
        }

        return count;
    }

    private static GearLookDefinition Look(string name, System.Func<string, IEnumerable<WornPiece>> pieces)
    {
        var look = LoadOrCreate<GearLookDefinition>($"{GearRoot}/{name}.asset");
        look.EditorSet(pieces("M").ToList(), pieces("F").ToList());
        EditorUtility.SetDirty(look);
        return look;
    }

    private static int Assign(string itemPath, GearLookDefinition look)
    {
        string path = $"Assets/_Project/Data/Items/{itemPath}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            throw new System.InvalidOperationException($"No item at {path}.");
        }

        var serialized = new SerializedObject(item);
        serialized.FindProperty("wornLook").objectReferenceValue = look;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    // A worn mesh: the body parts the pack hides under it, and any skin it paints (its mappers).
    private static WornPiece Piece(string prefabName, BodyCover extra = BodyCover.None)
    {
        GameObject prefab = Prefab(prefabName);
        var piece = new WornPiece { prefab = prefab, covers = extra | Covers(prefab.GetComponent<NHItem>()) };
        return piece;
    }

    // Gloves and trousers drawn onto the body skin.
    private static WornPiece Overlay(string prefabName)
    {
        NHItem item = Prefab(prefabName).GetComponent<NHItem>();
        Material overlay = item.Mappers.SelectMany(m => m.Materials).FirstOrDefault(m => m != null);
        return new WornPiece { skinOverlay = overlay, covers = Covers(item) };
    }

    private static BodyCover Covers(NHItem item)
    {
        BodyCover covers = BodyCover.None;
        if (item == null)
        {
            return covers;
        }

        foreach (var wrapper in item.Wrappers.Where(w => w.Enabled))
        {
            covers |= wrapper.Type switch
            {
                TargetBodyparts.Torso => BodyCover.Torso,
                TargetBodyparts.Bracers => BodyCover.Bracers,
                TargetBodyparts.Hands => BodyCover.Hands,
                TargetBodyparts.Pants => BodyCover.Legs,
                TargetBodyparts.Boots => BodyCover.Boots,
                TargetBodyparts.Feet => BodyCover.Feet,
                _ => BodyCover.None,
            };
        }

        return covers;
    }

    // ---- Asset helpers ------------------------------------------------------------------------------------------

    private static void Index()
    {
        prefabs = new Dictionary<string, GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackRoot + "/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!prefabs.ContainsKey(name))
            {
                prefabs.Add(name, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
        }

        materials = new Dictionary<string, Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PackRoot + "/Materials" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!materials.ContainsKey(name))
            {
                materials.Add(name, AssetDatabase.LoadAssetAtPath<Material>(path));
            }
        }
    }

    private static GameObject Prefab(string name) =>
        prefabs.TryGetValue(name, out GameObject prefab) ? prefab : throw new System.InvalidOperationException($"Pack prefab {name} is missing.");

    private static Material Mat(string name) =>
        materials.TryGetValue(name, out Material material) ? material : throw new System.InvalidOperationException($"Pack material {name} is missing.");

    private static NamedMaterial Named(string label, string material) => new NamedMaterial { label = label, material = Mat(material) };

    private static ColouredPart Coloured(string label, System.Func<string, string> prefabName) =>
        new ColouredPart { label = label, byColour = HairColourCodes.Select(c => Prefab(prefabName(c))).ToList() };

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
