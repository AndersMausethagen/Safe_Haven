using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>The Hearthline UI: one style, licensed fonts, and the signals the frameless HUD must not lose.</summary>
public sealed class HearthlineTests
{
    private const string Fonts = "Assets/_Project/UI/Fonts/";

    private static GameObject Prefab(string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/{name}.prefab");

    [Test]
    public void TheGame_ShipsOnlyFontsItMayShip()
    {
        Assert.AreEqual("FiraSans-Regular SDF", TMP_Settings.defaultFontAsset.name, "code-built text uses this");
        Assert.AreEqual("Gelasio SDF", UIStyle.Current.TitleFont.name);
        foreach (string licence in new[] { "OFL-FiraSans.txt", "OFL-Gelasio.txt" })
        {
            Assert.IsTrue(File.Exists(Fonts + licence), $"{licence} has to ship with the font");
        }

        Assert.IsEmpty(AssetDatabase.FindAssets("FolktaleDisplay"), "the title was Georgia, baked into the project");
    }

    [Test]
    public void NoUI_StillUsesTheOldFont()
    {
        string old = AssetDatabase.AssetPathToGUID("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var files = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }).Select(AssetDatabase.GUIDToAssetPath).Concat(HearthlineSetup.GameScenes);
        foreach (string file in files)
        {
            StringAssert.DoesNotContain($"m_fontAsset: {{fileID: 11400000, guid: {old}", File.ReadAllText(file), file);
        }
    }

    [Test]
    public void Windows_AreSolid()
    {
        // This project blends in Linear: a faint overlay on a dark panel comes out far brighter than its alpha.
        UIStyle style = UIStyle.Current;
        foreach (Color color in new[] { style.Panel, style.Inset, style.Filled, style.Control, style.ControlLit })
        {
            Assert.AreEqual(1f, color.a);
        }
    }

    [Test]
    public void EveryWindowInTheBag_HasARole()
    {
        var roles = Prefab("ItemHUD").GetComponentsInChildren<UIRole>(true).Select(r => r.Role).ToList();
        CollectionAssert.Contains(roles, UIRole.Kind.Window);
        CollectionAssert.Contains(Prefab("ItemSlot").GetComponentsInChildren<UIRole>(true).Select(r => r.Role).ToList(), UIRole.Kind.Slot);
        CollectionAssert.Contains(roles, UIRole.Kind.PrimaryButton, "the Craft button");
        CollectionAssert.Contains(Prefab("StationWindow").GetComponentsInChildren<UIRole>(true).Select(r => r.Role).ToList(), UIRole.Kind.PrimaryButton, "the Queue button");
    }

    [Test]
    public void TheFramelessVitals_KeepTheirBarsAndSignals()
    {
        var view = new SerializedObject(Prefab("PlayerVitalsHUD").GetComponent<PlayerVitalsView>());
        foreach (string bar in new[] { "health", "stamina", "focus", "hunger", "thirst" })
        {
            Assert.IsNotNull(view.FindProperty(bar).FindPropertyRelative("fill").objectReferenceValue, bar);
        }

        var frame = (UnityEngine.UI.Graphic)view.FindProperty("staminaFrame").objectReferenceValue;
        Assert.IsNotNull(frame, "refused actions flash it, and the dodge's glow is laid around it");
        Assert.IsTrue(frame.gameObject.activeSelf);
        Assert.IsNotNull(view.FindProperty("group").objectReferenceValue, "what dims the bars");
    }

    [Test]
    public void TheWorldPrompt_DrawsOverTheWorld()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        foreach (TMP_Text text in player.GetComponentInChildren<WorldInteractionIndicator>(true).GetComponentsInChildren<TMP_Text>(true))
        {
            StringAssert.Contains("Always On Top", text.fontSharedMaterial.name, "under its always-on-top panel otherwise, and unseen");
            Assert.AreEqual(text.font.atlasTexture, text.fontSharedMaterial.mainTexture, "a material only suits its own font's atlas");
            Assert.AreEqual(FontStyles.Normal, text.fontStyle & FontStyles.Bold, "bold comes from another atlas, without the on-top material");
        }
    }

    [Test]
    public void NoLabel_IsTooShortForItsLine()
    {
        var roots = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Append(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab"));
        foreach (TMP_Text text in roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)))
        {
            if (text.overflowMode == TextOverflowModes.Overflow || text.overflowMode == TextOverflowModes.ScrollRect || text.rectTransform.rect.height <= 0f)
            {
                continue;
            }

            // Truncate and Ellipsis drop a line that doesn't fit outright, so the label would come up empty.
            float line = text.fontSize * text.font.faceInfo.lineHeight / text.font.faceInfo.pointSize;
            Assert.GreaterOrEqual(text.rectTransform.rect.height, line, $"{text.transform.root.name}/{text.name}");
        }
    }

    [Test]
    public void TheOldCameraButtons_AreGone()
    {
        Transform menu = Prefab("UIRoot").transform.Find("Screen Canvas/PauseMenu");
        Assert.IsFalse(menu.Cast<Transform>().Any(child => child.name.StartsWith("Camera ")), "they stood in the middle of the screen");
    }

    [Test]
    public void VitalsCanBeKeptLit()
    {
        CollectionAssert.Contains(GameOptions.Accessibility, GameOptions.AlwaysShowVitals);
        Assert.IsFalse(GameOptions.AlwaysShowVitals.On, "quiet by default");
    }
}
