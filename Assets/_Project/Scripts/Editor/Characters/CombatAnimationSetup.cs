using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the combat animations (Safe Haven › Setup › Characters › Set Up Combat Animations). Step one imports the Quaternius
/// Universal Animation Library 2 (CC0, Assets/Quaternius) as humanoid clips, in place, with readable names and looping
/// where a clip is a loop.
/// </summary>
public static partial class CombatAnimationSetup
{
    public const string LibraryPath = "Assets/Quaternius/Universal Animation Library 2/UAL2_Standard.fbx";
    public const string Library1Path = "Assets/Quaternius/Universal Animation Library/UAL1_Standard.fbx";
    // Quaternius' own setup (Unity_Setup.png): bake axis conversion, and the rig's root bone drives root motion. Without
    // these the humanoid root is solved from the wrong axes and every clip tips the body over.
    private const string RootMotionNode = "Armature/root";

    /// <summary>Both libraries as humanoid clips the way Quaternius sets them up, named without the "Armature|" prefix.
    /// Returns the UAL2 clip count.</summary>
    public static int ConfigureImport()
    {
        ConfigureLibrary(Library1Path);
        return ConfigureLibrary(LibraryPath);
    }

    private static int ConfigureLibrary(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Combat animations: {path} is missing.");
            return 0;
        }

        bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.materialImportMode != ModelImporterMaterialImportMode.None
                       || !importer.bakeAxisConversion || importer.motionNodeName != RootMotionNode;
        importer.bakeAxisConversion = true;
        importer.motionNodeName = RootMotionNode;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = true;
        if (changed)
        {
            importer.SaveAndReimport();
        }

        RegenerateStaleAvatar(importer, path);

        // Keep clips already cut by hand or by this setup (the heavy slam), refreshing their settings.
        List<ModelImporterClipAnimation> kept = importer.clipAnimations
            .Where(c => c.name != c.takeName && c.name != c.takeName.Replace("Armature|", "")).ToList();
        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        var clips = new List<ModelImporterClipAnimation>();
        foreach (ModelImporterClipAnimation clip in defaults)
        {
            string name = clip.takeName.Replace("Armature|", "");
            // The file carries a few duplicated takes with a stray letter on the end (Sword_Blockj); skip those.
            if (clips.Any(c => c.name == name) || defaults.Any(d => d != clip && name.Length > 1 && d.takeName.Replace("Armature|", "") == name.Substring(0, name.Length - 1)))
            {
                continue;
            }

            clip.name = name;
            clip.loopTime = name.EndsWith("_Loop") || name == "Sword_Block";
            InPlace(clip);
            clips.Add(clip);
        }

        foreach (ModelImporterClipAnimation clip in kept)
        {
            InPlace(clip);
            clips.Add(clip);
        }

        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
        return clips.Count;
    }

    // The avatar keeps the skeleton pose from the import that made it. Turning on Bake Axis Conversion afterwards changes
    // the bones' axes but not that stored pose, and every retargeted pose then comes out rotated (the body lies flat).
    // When the stored pose disagrees with the model, it is rewritten from the model's bones (the bone mapping is kept).
    private static void RegenerateStaleAvatar(ModelImporter importer, string path)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Transform armature = model != null ? model.transform.Find("Armature") : null;
        HumanDescription description = importer.humanDescription;
        SkeletonBone stored = description.skeleton.FirstOrDefault(b => b.name == "Armature");
        if (armature == null || string.IsNullOrEmpty(stored.name) || Quaternion.Angle(stored.rotation, armature.localRotation) < 1f)
        {
            return;
        }

        var bones = model.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        SkeletonBone[] skeleton = description.skeleton;
        for (int i = 0; i < skeleton.Length; i++)
        {
            if (bones.TryGetValue(skeleton[i].name, out Transform bone))
            {
                skeleton[i].position = bone.localPosition;
                skeleton[i].rotation = bone.localRotation;
                skeleton[i].scale = bone.localScale;
            }
        }

        description.skeleton = skeleton;
        importer.humanDescription = description;
        importer.SaveAndReimport();
    }

    // Root motion is not baked into the pose (as Quaternius sets it up); the player doesn't apply root motion, so the
    // body stays over its collider and gameplay moves it (lunges).
    private static void InPlace(ModelImporterClipAnimation clip)
    {
        clip.lockRootRotation = false;
        clip.keepOriginalOrientation = false;
        clip.lockRootHeightY = false;
        clip.keepOriginalPositionY = true;
        clip.lockRootPositionXZ = false;
        clip.keepOriginalPositionXZ = false;
    }

    /// <summary>The library's clips by name.</summary>
    public static Dictionary<string, AnimationClip> Clips()
    {
        return AssetDatabase.LoadAllAssetsAtPath(LibraryPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
    }
}
