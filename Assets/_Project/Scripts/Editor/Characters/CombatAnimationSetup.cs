using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the combat animations (Safe Haven › Characters › Set Up Combat Animations). Step one imports the Quaternius
/// Universal Animation Library 2 (CC0, Assets/Quaternius) as humanoid clips, in place, with readable names and looping
/// where a clip is a loop.
/// </summary>
public static partial class CombatAnimationSetup
{
    public const string LibraryPath = "Assets/Quaternius/Universal Animation Library 2/UAL2_Standard.fbx";

    /// <summary>Humanoid import with in-place clips named without the "Armature|" prefix. Returns the clip count.</summary>
    public static int ConfigureImport()
    {
        var importer = AssetImporter.GetAtPath(LibraryPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Combat animations: {LibraryPath} is missing.");
            return 0;
        }

        bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.materialImportMode != ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = true;
        if (changed)
        {
            importer.SaveAndReimport();
        }

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
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            clips.Add(clip);
        }

        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
        return clips.Count;
    }

    /// <summary>The library's clips by name.</summary>
    public static Dictionary<string, AnimationClip> Clips()
    {
        return AssetDatabase.LoadAllAssetsAtPath(LibraryPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
    }
}
