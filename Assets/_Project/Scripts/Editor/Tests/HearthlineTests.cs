using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
    public void CodeBuiltWindows_TakeTheirColoursFromTheStyle()
    {
        foreach (string file in new[] { "UI/SkillsWindow.cs", "UI/HavenWindow.cs", "UI/AltarWindow.cs", "UI/CargoWindow.cs", "UI/RaidPreparationWindow.cs", "Settlement/HomePortal.cs" })
        {
            string source = File.ReadAllText("Assets/_Project/Scripts/" + file);
            Assert.IsFalse(Regex.IsMatch(source, "<color=#[0-9A-Fa-f]{6}"), $"{file}: changing a colour in UIStyle has to change the game");
            StringAssert.DoesNotContain("new Color(0.", source, file);
        }
    }

    [Test]
    public void TextBuiltInCode_TakesItsColoursFromTheStyle()
    {
        // Rich text built in code colours itself with UIStyle's tokens (UIStyle.Current.DangerHex and the like), so the
        // same message is never two different reds and a change in UIStyle changes the game.
        var offenders = new System.Collections.Generic.List<string>();
        foreach (string file in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/Editor/"))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith("//") && Regex.IsMatch(lines[i], "<color=#[0-9A-Fa-f]{6}|\"#[0-9A-Fa-f]{6}\""))
                {
                    offenders.Add($"{file}:{i + 1}");
                }
            }
        }

        Assert.IsEmpty(offenders, "Hard-coded text colours: " + string.Join(", ", offenders));
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
    public void CarvedArtwork_IsWiredAndUsesDifferentShapesForMapMeanings()
    {
        UIStyle style = UIStyle.Current;
        Sprite[] artwork = { style.Hearthmark, style.CarvedCorner, style.ExitMark, style.QuestMark,
            style.CompanionMark, style.EventMark, style.RumourMark };
        Assert.IsTrue(artwork.All(sprite => sprite != null), "the authored artwork must ship with the style");
        Assert.AreEqual(artwork.Length, artwork.Distinct().Count(), "each meaning needs its own shape");
        Assert.AreNotEqual(style.AccentFor(false), style.AccentFor(true), "raids use colder decorative ink");
        Assert.AreEqual(style.Rim.a, style.RimFor(true).a, "the decorative rim keeps its opacity");
    }

    [Test]
    public void CarvedFrame_IsIdempotentAndDoesNotInterceptInputOrLayout()
    {
        var panel = new GameObject("Panel", typeof(RectTransform));
        try
        {
            UIStyle style = UIStyle.Current;
            GameObject frame = HearthlineArt.AddFrame((RectTransform)panel.transform, style, false);
            Assert.AreSame(frame, HearthlineArt.AddFrame((RectTransform)panel.transform, style, true));
            Assert.AreEqual(1, panel.transform.childCount);
            Assert.IsTrue(frame.GetComponent<LayoutElement>().ignoreLayout);
            Image[] corners = frame.GetComponentsInChildren<Image>();
            Assert.AreEqual(4, corners.Length);
            Assert.IsTrue(corners.All(corner => !corner.raycastTarget && corner.sprite == style.CarvedCorner));
            Assert.IsTrue(corners.All(corner => Mathf.Approximately(corner.color.r, style.RaidAccent.r)));
        }
        finally
        {
            Object.DestroyImmediate(panel);
        }
    }

    [Test]
    public void ActionCrosshair_IsNeverDecoratedAsAWindow()
    {
        GameObject root = Object.Instantiate(Prefab("UIRoot"));
        try
        {
            ActionCrosshair crosshair = root.GetComponentInChildren<ActionCrosshair>(true);
            Assert.IsNotNull(crosshair);
            Assert.IsNull(crosshair.GetComponent<UIRole>(), "the aiming indicator is not a window");

            crosshair.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
            HearthlineSetup.Restyle(root);
            Assert.IsNull(crosshair.GetComponent<UIRole>(), "restyling removes a legacy window role");
            HearthlineSetup.Restyle(root);
            Assert.IsNull(crosshair.GetComponent<UIRole>(), "restyling must not add the role back");
        }
        finally
        {
            Object.DestroyImmediate(root);
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
    public void TheRaidPlanner_IsAnEmptyShellItBuildsItselfIn()
    {
        var planner = new SerializedObject(Prefab("RaidPreparationWindow").GetComponent<RaidPreparationWindow>());
        var content = (RectTransform)planner.FindProperty("content").objectReferenceValue;
        Assert.IsNotNull(content, "what the window builds itself in");
        Assert.AreEqual(0, content.childCount, "old children would show under the built window");
        Assert.AreEqual(content.gameObject, new SerializedObject(planner.FindProperty("window").objectReferenceValue).FindProperty("content").objectReferenceValue, "the UIWindow shows and hides the same object");
        StringAssert.Contains("lost", planner.FindProperty("lossSummary").stringValue, "GDD §18.6: what can be lost is said before the raid");
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
    public void TheWorldPrompt_IsReadableEvenOverAWhiteSurface()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        WorldInteractionIndicator prompt = player.GetComponentInChildren<WorldInteractionIndicator>(true);
        var data = new SerializedObject(prompt);
        var plate = prompt.transform.Find("Slate").GetComponent<Image>();
        var status = (TMP_Text)data.FindProperty("status").objectReferenceValue;
        Assert.GreaterOrEqual(plate.color.a, 0.96f);
        Assert.GreaterOrEqual(status.fontSize, 18f);
        StringAssert.Contains("Always On Top", plate.material.name);

        Color background = plate.color.linear * plate.color.a + Color.white * (1f - plate.color.a);
        Color foreground = WorldInteractionIndicator.ReadableStatusColor(
            data.FindProperty("blockedColor").colorValue, UIStyle.Current.Text).linear;
        Assert.GreaterOrEqual((Luminance(foreground) + 0.05f) / (Luminance(background) + 0.05f), 4.5f);
    }

    private static float Luminance(Color colour) => 0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b;

    [TestCase(QuestRank.MainStory, "Main quest")]
    [TestCase(QuestRank.SideQuest, "Side quest")]
    [TestCase(QuestRank.Optional, "Optional quest")]
    public void MapQuestRank_IsAlsoWrittenInWords(QuestRank rank, string words)
    {
        StringAssert.Contains(words, WorldMapHud.QuestLabel("The old path", rank));
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
        // The default, not the value now: that is the player's own choice, kept in their preferences.
        Assert.AreEqual(0f, GameOptions.AlwaysShowVitals.Default, "quiet by default");
    }
}
