using UnityEditor;

/// <summary>
/// Custom clips made outside Unity import ready to play (2026-11-17): an FBX put in <see cref="Folder"/> (LocalSpawn's
/// video-to-animation workflow, animate_from_video.py, or any Blender retarget onto a Mixamo-named rig) becomes a
/// Humanoid with an avatar of its own and no materials, and a clip whose name says it repeats (ends in "Loop", or is a
/// walk, run or idle) loops. Its height and facing are baked into the pose; its way across the ground stays root motion.
/// Only a first import is set up, so whatever is changed in the Inspector afterwards stays.
/// Mecanim moves a Humanoid clip onto any Humanoid character, so one clip serves the player, the residents and the
/// humanoid monsters alike.
/// </summary>
public sealed class CustomClipImport : AssetPostprocessor
{
    public const string Folder = "Assets/_Project/Art/Animations/Custom/";

    /// <summary>True for a clip that should loop, by its name.</summary>
    public static bool Loops(string clipName)
    {
        string name = clipName.ToLowerInvariant();
        return name.EndsWith("loop") || name.Contains("walk") || name.Contains("run") || name.Contains("idle");
    }

    private bool IsCustom => assetPath.StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase)
                             && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

    private void OnPreprocessModel()
    {
        if (!IsCustom || !assetImporter.importSettingsMissing)
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = true;
    }

    private void OnPreprocessAnimation()
    {
        if (!IsCustom || !assetImporter.importSettingsMissing)
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = Loops(clip.name);
            clip.loopPose = clip.loopTime;
            // Height and facing are part of the move (a crouch, a jump, a turn) and baked into the pose, as recorded; the way
            // across the ground stays root motion, which the player doesn't apply, so the body stays over its collider as
            // with the other combat clips (CombatAnimationSetup.InPlace). Without the bake a crouch lifted the feet instead.
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootPositionXZ = false;
        }

        importer.clipAnimations = clips;
    }
}
