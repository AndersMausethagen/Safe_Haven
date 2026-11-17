using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Custom clips (Art/Animations/Custom, from LocalSpawn's video-to-animation workflow) import as Humanoid clips.</summary>
public sealed class CustomClipImportTests
{
    [TestCase("GroundSmash", false)]
    [TestCase("Troll_WalkLoop", true)]
    [TestCase("Lumbering Run", true)]
    [TestCase("CombatIdle", true)]
    [TestCase("Overhead Slam", false)]
    public void ARepeatingClip_Loops(string name, bool loops) => Assert.AreEqual(loops, CustomClipImport.Loops(name));

    [Test]
    public void EveryCustomClip_IsAHumanoidWithAClip()
    {
        string folder = CustomClipImport.Folder.TrimEnd('/');
        Assert.IsTrue(AssetDatabase.IsValidFolder(folder), folder);
        foreach (string path in Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories))
        {
            string asset = path.Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(asset);
            Assert.AreEqual(ModelImporterAnimationType.Human, importer.animationType, asset);
            Avatar avatar = null;
            AnimationClip clip = null;
            foreach (Object part in AssetDatabase.LoadAllAssetsAtPath(asset))
            {
                avatar = part is Avatar found ? found : avatar;
                clip = part is AnimationClip each && !each.name.StartsWith("__preview__") ? each : clip;
            }

            Assert.IsNotNull(avatar, asset);
            Assert.IsTrue(avatar.isValid && avatar.isHuman, $"{asset}: Unity found a human skeleton");
            Assert.IsNotNull(clip, asset);
            Assert.Greater(clip.length, 0.2f, asset);
            Assert.IsTrue(clip.humanMotion, $"{asset}: the clip plays on any Humanoid");
        }
    }
}
