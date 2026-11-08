using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Hearthline 2: the carved display face, the panel surface, the title rule, glow and cursor, the hover tick and
/// the blur behind full-screen windows are all wired up, and the display font's licence ships beside it.</summary>
public sealed class Hearthline2Tests
{
    [Test]
    public void TheStyle_HasEverythingHearthline2Draws()
    {
        UIStyle style = AssetDatabase.LoadAssetAtPath<UIStyle>("Assets/_Project/Resources/UIStyle.asset");
        Assert.IsNotNull(style.DisplayFont, "display font");
        Assert.AreEqual(style.DisplayFont.material.mainTexture, style.DisplayMaterial.mainTexture, "the title material is the display font's own atlas, so TMP keeps it");
        Assert.IsTrue(style.DisplayMaterial.IsKeywordEnabled("UNDERLAY_ON"), "titles carry a soft shadow");
        Assert.AreEqual("UI/Hearthline Panel", style.PanelMaterial.shader.name);
        Assert.IsNotNull(style.Divider);
        Assert.IsNotNull(style.Glow);
        Assert.IsNotNull(style.Cursor);
        Assert.IsTrue(style.Cursor.isReadable, "a cursor texture must be readable");
        Assert.IsTrue(File.Exists("Assets/_Project/UI/Fonts/OFL-Cinzel.txt"), "the OFL licence ships with the font");
        foreach (char digit in "0123456789")
        {
            Assert.IsTrue(style.DisplayFont.HasCharacter(digit, false, true), $"Cinzel draws {digit}");
        }
    }

    [Test]
    public void FullScreenWindows_HaveABlurAndButtonsATick()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/_Project/Settings/Rendering/PC_Renderer.asset");
        Assert.IsTrue(renderer.rendererFeatures.OfType<UIBlurFeature>().Any(), "the backdrop blur is on the PC renderer");
        foreach (string guid in AssetDatabase.FindAssets("t:AudioSettingsDefinition"))
        {
            var sounds = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsNotNull(sounds.Hover, "the hover tick");
        }
    }
}
