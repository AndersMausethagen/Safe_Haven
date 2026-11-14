#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Winter in the Haven (`unity command script run --file AgentScripts/BuildWinter.cs --entry BuildWinter.Apply`):
/// the sounds (Tools/Audio/winter.py) for snow and ice steps, ice creaks and cracks; the Cold Breath puff
/// (Resources); snow settling on rocks, logs, props, bushes and roofs (the "Snow Settles On It" toggle on their
/// materials; thatch and shingle roofs move to the See-Through Lit copy so they can take it); and the SafeHaven
/// scene's Winter Tracks and Winter Ice. Re-runnable. Saves the scene only if it was clean when this began.
/// </summary>
public static class BuildWinter
{
    private const string Sfx = "Assets/_Project/Audio/SFX/Safe Haven Winter/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Winter";
    private const string SnowKeyword = "_SEASON_SNOW";

    // Materials that stay clear: what is indoors, underfoot inside, glowing, wet or skin.
    private static readonly string[] NeverSnowy =
    {
        "floor", "plank", "rug", "carpet", "interior", "ember", "fire", "glow", "water", "glass", "cloth", "skin",
        "body", "hair", "eyes", "copper", "moonsilver", "iron", "candle", "lantern", "spirit",
    };

    private static readonly string[] SnowyShaders =
    {
        "Safe Haven/Mesh Surface (Tessellated)", "Safe Haven/Layered Lit", "Safe Haven/Mossy Stone ASE",
        "Safe Haven/Lit (See-Through)", "Safe Haven/Unlit (See-Through)",
    };

    public static string Apply()
    {
        var report = new List<string>();
        Folder(Sounds);
        SoundDefinition grass = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Footstep Grass.asset");
        SoundDefinition snowStep = Sound("Footstep Snow", "Snow Step", 5, grass, 0.34f, 3f, 11f, 3);
        SoundDefinition iceStep = Sound("Footstep Ice", "Ice Step", 4, grass, 0.3f, 3f, 11f, 3);
        SoundDefinition creak = Sound("Ice Creak", "Ice Creak", 3, grass, 0.45f, 4f, 22f, 1);
        SoundDefinition crack = Sound("Ice Crack", "Ice Crack", 2, grass, 0.6f, 10f, 90f, 1);
        var settings = Resources.Load<AudioSettingsDefinition>("AudioSettings");
        if (settings != null)
        {
            settings.ConfigureWinterSteps(snowStep, iceStep, crack);
            EditorUtility.SetDirty(settings);
        }

        report.Add("4 sounds");
        report.Add(BreathMaterial());
        report.Add(Roofs());
        report.Add(SnowyMaterials());
        report.Add(Scene(creak, crack));
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static SoundDefinition Sound(string name, string files, int count, SoundDefinition like, float volume, float full, float audible, int atOnce)
    {
        string path = $"{Sounds}/{name}.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = like != null ? Object.Instantiate(like) : ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var data = new SerializedObject(sound);
        data.FindProperty("displayName").stringValue = name;
        SerializedProperty clips = data.FindProperty("clips");
        clips.arraySize = 0;
        for (int i = 1; i <= count; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Sfx}{files} {i}.wav");
            if (clip != null)
            {
                clips.arraySize++;
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
            }
        }

        data.FindProperty("volume").floatValue = volume;
        data.FindProperty("fullVolumeDistance").floatValue = full;
        data.FindProperty("audibleDistance").floatValue = audible;
        data.FindProperty("maxAtOnce").intValue = atOnce;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    private static string BreathMaterial()
    {
        const string path = "Assets/_Project/Resources/Cold Breath.mat";
        var smoke = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Effects/Hearth Smoke.mat");
        if (smoke == null)
        {
            return "no Hearth Smoke material for the breath";
        }

        var breath = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (breath == null)
        {
            breath = new Material(smoke);
            AssetDatabase.CreateAsset(breath, path);
        }

        breath.CopyPropertiesFromMaterial(smoke);
        if (breath.HasProperty("_TintColor"))
        {
            breath.SetColor("_TintColor", new Color(0.95f, 0.97f, 1f, 1f));
        }

        EditorUtility.SetDirty(breath);
        return "breath";
    }

    private static bool Excluded(Material material)
    {
        string name = material.name.ToLowerInvariant();
        return NeverSnowy.Any(name.Contains);
    }

    // Thatch and shingle roofs are on URP Lit, which can't take snow: they move to the See-Through copy (same look).
    private static string Roofs()
    {
        var roofs = new List<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string name = material != null ? material.name.ToLowerInvariant() : "";
            if (material != null && material.shader.name == "Universal Render Pipeline/Lit" && (name.Contains("thatch") || name.Contains("shingle")))
            {
                roofs.Add(material);
            }
        }

        int changed = SeeThroughMaterials.Convert(roofs, true);
        return $"{changed} roofs to See-Through Lit";
    }

    private static string SnowyMaterials()
    {
        int on = 0, off = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || !SnowyShaders.Contains(material.shader.name) || !material.HasProperty("_SeasonSnow"))
            {
                continue;
            }

            bool snowy = !Excluded(material);
            if (material.IsKeywordEnabled(SnowKeyword) == snowy && Mathf.Approximately(material.GetFloat("_SeasonSnow"), snowy ? 1f : 0f))
            {
                if (snowy) on++; else off++;
                continue;
            }

            material.SetFloat("_SeasonSnow", snowy ? 1f : 0f);
            if (snowy)
            {
                material.EnableKeyword(SnowKeyword);
                on++;
            }
            else
            {
                material.DisableKeyword(SnowKeyword);
                off++;
            }

            EditorUtility.SetDirty(material);
        }

        return $"snow settles on {on} materials ({off} kept clear)";
    }

    private static string Scene(SoundDefinition creak, SoundDefinition crack)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven")
        {
            return "scene skipped (open SafeHaven)";
        }

        bool wasClean = !scene.isDirty;
        GameObject ambience = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "07 Ambience");
        if (ambience == null)
        {
            return "no 07 Ambience";
        }

        Transform living = ambience.transform.Find("Living World");
        if (living == null)
        {
            return "no Living World (run BuildLivingWorld first)";
        }

        Transform winter = living.Find("Winter");
        if (winter == null)
        {
            winter = new GameObject("Winter").transform;
            winter.SetParent(living, false);
        }

        Ensure<WinterTracks>(winter.gameObject);
        Ensure<WinterIce>(winter.gameObject).Configure(crack, creak);
        EditorUtility.SetDirty(winter.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (wasClean)
        {
            EditorSceneManager.SaveScene(scene);
        }

        return wasClean ? "scene: winter tracks and ice (saved)" : "scene: winter tracks and ice (not saved: it had other changes)";
    }

    private static void Folder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }

    private static T Ensure<T>(GameObject target) where T : Component => target.TryGetComponent(out T found) ? found : target.AddComponent<T>();
}
