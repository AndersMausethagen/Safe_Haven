using NUnit.Framework;
using UnityEngine;

/// <summary>The main menu's layout stays within 16:9 on a super-ultrawide screen and fills a 16:9 one.</summary>
public sealed class UIAspectLimitTests
{
    [Test]
    public void WideScreens_KeepTheLayoutCentredAt16By9()
    {
        Assert.AreEqual(3840f, UIAspectLimit.WidthFor(new Vector2(7680f, 2160f), 16f / 9f), 0.5f, "32:9: half the width");
        Assert.AreEqual(1920f, UIAspectLimit.WidthFor(new Vector2(1920f, 1080f), 16f / 9f), 0.5f, "16:9: all of it");
        Assert.AreEqual(1280f, UIAspectLimit.WidthFor(new Vector2(1280f, 1024f), 16f / 9f), 0.5f, "narrower: all of it");
    }

    [Test]
    public void TheMainMenu_HasItsLayoutInTheLimit()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            GameObject menu = System.Array.Find(scene.GetRootGameObjects(), g => g.name == "Main Menu");
            Transform layout = menu.transform.Find("Layout");
            Assert.IsNotNull(layout);
            Assert.IsNotNull(layout.GetComponent<UIAspectLimit>());
            foreach (string name in new[] { "Controls Hint", "Folktale Content", "Folktale Setting" })
            {
                Assert.IsNotNull(layout.Find(name), name);
            }
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }
}
