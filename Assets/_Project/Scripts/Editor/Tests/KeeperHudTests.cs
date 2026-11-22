using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Keeper's view's HUD (KeeperHud and its parts; the user's UX ask of 2026-11-22): it builds without a scene, every order in the
/// command bar has its icon and a word under it, and the stores strip shows a picture for every store it names.
/// </summary>
public sealed class KeeperHudTests
{
    [Test]
    public void TheHud_Builds_AndEveryOrderHasItsIconAndWord()
    {
        var owner = new GameObject("Keeper HUD Test");
        try
        {
            KeeperHud hud = KeeperHud.Create(owner.transform);
            RectTransform bar = null;
            foreach (RectTransform part in hud.Parts)
            {
                bar = bar == null ? part : bar;
            }

            Assert.IsNotNull(bar, "the command bar");
            int orders = 0;
            foreach (Button button in bar.GetComponentsInChildren<Button>(true))
            {
                orders++;
                Image icon = null;
                foreach (Image image in button.GetComponentsInChildren<Image>(true))
                {
                    icon = image.name.EndsWith(" Icon") ? image : icon;
                }

                Assert.IsNotNull(icon, $"{button.name} has an icon");
                Assert.IsNotNull(icon.sprite, $"{button.name}'s icon ({icon.name}) is in the kit");
                TMP_Text caption = button.transform.Find("Caption").GetComponent<TMP_Text>();
                Assert.IsFalse(string.IsNullOrWhiteSpace(caption.text), $"{button.name} has a word under it");
            }

            Assert.GreaterOrEqual(orders, 8, "work, defence and the Haven's orders");
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void EveryStoreShown_HasAPicture()
    {
        Assert.Greater(KeeperViewSettings.Current.Stores.Count, 3, "the stores building and work use");
        foreach (ItemDefinition item in KeeperViewSettings.Current.Stores)
        {
            Assert.IsNotNull(item);
            Assert.IsNotNull(item.Icon, $"{item.name} has a picture for the strip");
        }
    }
}
