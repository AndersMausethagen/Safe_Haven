using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Speech bubbles over whoever talks (DialogueBox shows lines in them since 2026-11-09).</summary>
public sealed class SpeechBubbleTests
{
    private GameObject canvas;
    private GameObject speaker;

    [SetUp]
    public void SetUp()
    {
        canvas = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        speaker = new GameObject("Test Speaker");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(canvas);
        Object.DestroyImmediate(speaker);
    }

    [Test]
    public void ABubble_FitsItsLine_WrappedAtAReadableWidth()
    {
        SpeechBubble bubble = SpeechBubble.Create(canvas.transform);
        Assert.IsFalse(bubble.IsShown);

        bubble.Show("Eirik", "Short.", speaker.transform);
        var box = (RectTransform)bubble.transform.GetChild(0);
        Vector2 small = box.sizeDelta;
        Assert.IsTrue(bubble.IsShown && bubble.gameObject.activeSelf);
        Assert.Greater(small.x, 100f, "measured while shown: hidden text measures 0 and the bubble came out a sliver");

        bubble.Show("Eirik", new string('w', 40) + " " + string.Join(" ", new string[40]).Replace(" ", "word "), speaker.transform);
        Vector2 large = box.sizeDelta;
        Assert.LessOrEqual(large.x, 460f + 2f * 22f + 0.5f, "long lines wrap at the bubble's width");
        Assert.Greater(large.y, small.y * 2f, "and grow downwards in lines");

        bubble.Hide();
        Assert.IsFalse(bubble.IsShown || bubble.gameObject.activeSelf);
    }
}
