using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// The HUD review of 2026-11-18 (the user: "right now its cluttered"): what the screen shows when nothing is happening (HudRules). The
/// rest of it is seen in Play, where the HUD lives: these are the rules that can be checked outside it.
/// </summary>
public sealed class HudTests
{
    [Test]
    public void TheHud_IsContextualUnlessChosenOtherwise()
    {
        Assert.AreEqual(HudRules.Density.Contextual, (HudRules.Density)Mathf.RoundToInt(GameOptions.Hud.Default),
            "a new player gets the HUD that shows what matters");
    }

    [Test]
    public void TheDaysTimeline_ShowsBeforeNightfallAndDawn_AndNotAtMidday()
    {
        const float sunrise = 6f, sunset = 20f;
        Assert.IsFalse(EnvironmentHudView.TimeMatters(12f, sunrise, sunset), "midday: nothing to plan by");
        Assert.IsTrue(EnvironmentHudView.TimeMatters(19f, sunrise, sunset), "an hour before nightfall");
        Assert.IsFalse(EnvironmentHudView.TimeMatters(20.5f, sunrise, sunset), "night has fallen: the clock says so");
        Assert.IsTrue(EnvironmentHudView.TimeMatters(5f, sunrise, sunset), "an hour before dawn");
        Assert.IsFalse(EnvironmentHudView.TimeMatters(2f, sunrise, sunset), "the dead of night");
    }

    [Test]
    public void TheSameMessage_WhileItIsUp_IsOneLineCounted()
    {
        var root = new GameObject("Feed");
        try
        {
            var template = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            template.transform.SetParent(root.transform, false);
            template.gameObject.SetActive(false);
            var feed = root.AddComponent<MessageFeedView>();
            typeof(MessageFeedView).GetField("lineTemplate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(feed, template);

            feed.Show("A wolf howls!", MessageKind.Warning);
            feed.Show("A wolf howls!", MessageKind.Warning);
            feed.Show("Something else.", MessageKind.Info);

            var shown = new System.Collections.Generic.List<string>();
            foreach (TMP_Text line in root.GetComponentsInChildren<TMP_Text>(false))
            {
                shown.Add(line.text);
            }

            CollectionAssert.AreEquivalent(new[] { "A wolf howls!  ×2", "Something else." }, shown);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
