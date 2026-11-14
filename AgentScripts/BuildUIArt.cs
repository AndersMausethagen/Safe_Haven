#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts LocalSpawn's interface paintings (Tools/LocalSpawn/generate_ui_art.py, generate_item_icons.py --set boons) into the game:
/// residents' portraits for the People page, each raid's place painting and results artwork, and the boon icons beside the vitals
/// (Resources/ConditionIconSettings), raid events' scenes and the Haven window's camp at each rank. Run with `unity command script run
/// --file AgentScripts/BuildUIArt.cs --entry BuildUIArt.All` (or Residents, Raids, Boons, Events, Haven). Tutorial, backstory and
/// creature pictures are BuildJournal's. Re-running only copies what changed.
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

    public static string All() => string.Join("\n", Residents(), Raids(), Boons(), Events(), Haven(), Surfaces(), Windows());

    /// <summary>The windows' paintings (Tools/LocalSpawn/window_art.json): into Resources/WindowArt, where KitBanner and
    /// the portal's window find them by key.</summary>
    public static string Windows()
    {
        const string from = "Tools/LocalSpawn/WindowArt/";
        if (!Directory.Exists(from))
        {
            return "No window art painted yet (generate_ui_art.py windows).";
        }

        int done = 0;
        foreach (string source in Directory.GetFiles(from, "*.png"))
        {
            CopyIn(source.Replace('\\', '/'), "Assets/_Project/Resources/WindowArt/" + Path.GetFileName(source), 2048, false);
            done++;
        }

        return $"{done} window paintings in Resources/WindowArt.";
    }

    /// <summary>
    /// Painted surfaces in the world (Tools/LocalSpawn/surface_art.json): the Haven Banner's cloth (its own material, used by nothing
    /// else), and the notes on the notice board by the portal, which get a material of their own (the board's note shared the
    /// stations' cloth). The note is in SafeHaven.unity, so that part needs the scene open; it is saved only when it was clean.
    /// </summary>
    public static string Surfaces()
    {
        var log = new List<string>();
        Texture2D cloth = TextureIn("Tools/LocalSpawn/SurfaceArt/banner_cloth.png", "Assets/_Project/Art/Haven/Textures/Banner Cloth.png");
        if (cloth != null)
        {
            var banner = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Banner Cloth.mat");
            Paint(banner, cloth);
            log.Add("banner cloth");
        }

        Texture2D notes = TextureIn("Tools/LocalSpawn/SurfaceArt/notice_paper.png", "Assets/_Project/Art/Haven/Textures/Notice Paper.png");
        if (notes != null)
        {
            const string paperPath = "Assets/_Project/Materials/Haven/Notice Paper.mat";
            var paper = AssetDatabase.LoadAssetAtPath<Material>(paperPath);
            if (paper == null)
            {
                paper = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Stations/StationCloth.mat"));
                AssetDatabase.CreateAsset(paper, paperPath);
            }

            Paint(paper, notes);
            log.Add(NoteOnTheBoard(paper));
        }

        AssetDatabase.SaveAssets();
        return "Surfaces: " + (log.Count > 0 ? string.Join(", ", log) : "none painted yet");
    }

    private static void Paint(Material material, Texture2D texture)
    {
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        // White: the painting carries the colour (BuildHaven's MakeMaterial leaves a painted material's colour alone).
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
    }

    private static string NoteOnTheBoard(Material paper)
    {
        UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven")
        {
            return "notice paper (the board's note needs SafeHaven open)";
        }

        foreach (Transform board in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Notice Board"))
        {
            Transform note = board.Find("Note");
            if (note != null && note.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != paper)
            {
                bool wasClean = !scene.isDirty;
                renderer.sharedMaterial = paper;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                if (wasClean)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                }

                return wasClean ? "notice paper on the board (scene saved)" : "notice paper on the board (scene not saved: it had other changes)";
            }
        }

        return "notice paper (the board already has it, or no Notice Board/Note)";
    }

    // A painting copied in as an ordinary texture (mipmapped, for a surface in the world); null until it is painted.
    private static Texture2D TextureIn(string source, string path)
    {
        if (!File.Exists(source))
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source)))
        {
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.maxTextureSize != 1024 || !importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Each raid event's painting, shown above its words and choices.</summary>
    public static string Events()
    {
        int done = 0;
        foreach (RaidEventDefinition raidEvent in Find<RaidEventDefinition>())
        {
            string source = "Tools/LocalSpawn/EventArt/" + raidEvent.name + ".png";
            if (File.Exists(source))
            {
                Set(raidEvent, "artwork", CopyIn(source, "Assets/_Project/Art/UI/Events/" + raidEvent.name + ".png", 2048, false));
                done++;
            }
        }

        return $"{done} raid events given their painting.";
    }

    /// <summary>The Haven window's picture of the camp at each rank (HavenSettings > Ranks, in order: 1.png is the first).</summary>
    public static string Haven()
    {
        var settings = AssetDatabase.LoadAssetAtPath<HavenSettings>("Assets/_Project/Resources/HavenSettings.asset");
        var so = new SerializedObject(settings);
        SerializedProperty ranks = so.FindProperty("ranks");
        int done = 0;
        for (int i = 0; i < ranks.arraySize; i++)
        {
            string source = "Tools/LocalSpawn/HavenArt/" + (i + 1) + ".png";
            if (File.Exists(source))
            {
                ranks.GetArrayElementAtIndex(i).FindPropertyRelative("picture").objectReferenceValue =
                    CopyIn(source, "Assets/_Project/Art/UI/Haven/Rank " + (i + 1) + ".png", 2048, false);
                done++;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(settings);
        return $"{done} of {ranks.arraySize} ranks given their picture.";
    }

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
