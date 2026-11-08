using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Dialogue leaves the world in view (no blurred backdrop), and text tabs answer the pointer.</summary>
public sealed class UIHoverAndBackdropTests
{
    [Test]
    public void Dialogue_DoesNotDimTheWorld()
    {
        var dialogue = new GameObject("Dialogue", typeof(RectTransform)).AddComponent<UIWindow>();
        var haven = new GameObject("Haven", typeof(RectTransform)).AddComponent<UIWindow>();
        try
        {
            dialogue.Configure(UIWindowId.Dialogue, dialogue.gameObject);
            haven.Configure(UIWindowId.Haven, haven.gameObject);
            Assert.IsFalse(UIBackdrop.Dims(dialogue), "a conversation keeps the speakers in view");
            Assert.IsTrue(UIBackdrop.Dims(haven));
        }
        finally
        {
            Object.DestroyImmediate(dialogue.gameObject);
            Object.DestroyImmediate(haven.gameObject);
        }
    }

    [Test]
    public void ATab_BrightensUnderThePointer()
    {
        var tab = new GameObject("Tab", typeof(RectTransform), typeof(Button));
        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(tab.transform, false);
        var line = new GameObject("Line", typeof(RectTransform)).AddComponent<Image>();
        line.transform.SetParent(tab.transform, false);
        try
        {
            var hover = tab.AddComponent<UITabHover>();
            UIStyle style = UIStyle.Current;
            hover.SetOpen(false);
            Assert.AreEqual(style.Muted, label.color, "closed: muted");
            Assert.IsFalse(line.gameObject.activeSelf);
            hover.OnPointerEnter(new PointerEventData(null));
            Assert.AreEqual(style.Text, label.color, "under the pointer: bright");
            Assert.IsTrue(line.gameObject.activeSelf, "a faint line");
            hover.OnPointerExit(new PointerEventData(null));
            Assert.AreEqual(style.Muted, label.color);
            hover.SetOpen(true);
            Assert.AreEqual(style.Accent, label.color, "open: amber");
        }
        finally
        {
            Object.DestroyImmediate(tab);
        }
    }
}
