using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The portal's briefing shows what is out there as pictures with names, the words on hover (the user, 2026-11-21: "it should be
/// artwork of the different events, with its name underneath ... Same with monsters"). Every card needs its picture: each rumour its
/// site's painting and name, each known creature its portrait, one of the Lost its painting. And the cards themselves lay out in rows
/// and crop a painting to fill (KitPictureCard).
/// </summary>
public sealed class PortalBriefingArtTests
{
    private readonly List<GameObject> made = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in made)
        {
            Object.DestroyImmediate(go);
        }

        made.Clear();
    }

    private static List<T> All<T>() where T : Object
    {
        var found = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" }))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                found.Add(asset);
            }
        }

        return found;
    }

    [Test]
    public void EveryRumour_HasAPaintingAndAName()
    {
        List<RaidRumourDefinition> rumours = All<RaidRumourDefinition>();
        Assert.Greater(rumours.Count, 20, "the shipped rumours are found");
        foreach (RaidRumourDefinition rumour in rumours)
        {
            Assert.IsNotNull(rumour.Site, rumour.name + " stands somewhere");
            Assert.IsNotNull(rumour.Site.Artwork, rumour.name + ": its card's painting is its site's");
            Assert.IsFalse(string.IsNullOrWhiteSpace(rumour.Site.DisplayName), rumour.name + ": its card's name is its site's");
        }
    }

    [Test]
    public void EveryKnownDanger_HasAPortrait()
    {
        int checkedCreatures = 0;
        foreach (RaidDefinition raid in All<RaidDefinition>())
        {
            foreach (MonsterDefinition monster in raid.KnownEnemies)
            {
                if (monster != null)
                {
                    Assert.IsNotNull(monster.Portrait, $"{raid.name} lists {monster.name} as a known danger: its card is its portrait");
                    checkedCreatures++;
                }
            }
        }

        Assert.Greater(checkedCreatures, 5, "the raids' known dangers are checked");
    }

    [Test]
    public void OneOfTheLostWalkingTheRun_HasAPainting()
    {
        Assert.IsNotNull(Resources.Load<LostOneSettings>("LostOneSettings").BriefingArt, "LostOneSettings › Briefing Art");
    }

    private RectTransform Parent()
    {
        var go = new GameObject("Cards", typeof(RectTransform));
        made.Add(go);
        return (RectTransform)go.transform;
    }

    [Test]
    public void Cards_LayOutInRows_AcrossTheWidth()
    {
        RectTransform parent = Parent();
        var cards = new List<KitPictureCard>();
        for (int i = 0; i < 5; i++)
        {
            cards.Add(new KitPictureCard(parent, "Card " + i, 100f, 60f, 2, 15f, null, null));
        }

        // 100 wide with 10 between: three fit in 320, so five make two rows.
        float height = KitPictureCard.Lay(cards, 5, 0f, 0f, 320f, 10f);
        Assert.AreEqual(cards[0].Height * 2f + 10f, height, 0.01f);
        Assert.AreEqual(new Vector2(220f, 0f), cards[2].Root.anchoredPosition);
        Assert.AreEqual(new Vector2(0f, -(cards[0].Height + 10f)), cards[3].Root.anchoredPosition);
        Assert.AreEqual(0f, KitPictureCard.Lay(cards, 0, 0f, 0f, 320f, 10f), "none take no room");
    }

    [Test]
    public void APainting_IsCroppedToFill_FromItsMiddle()
    {
        var texture = new Texture2D(400, 100);
        Sprite wide = Sprite.Create(texture, new Rect(0f, 0f, 400f, 100f), Vector2.one * 0.5f);
        var card = new KitPictureCard(Parent(), "Card", 106f, 56f, 1, 15f, null, null);
        card.ShowPainting(wide);
        RawImage painting = card.Root.Find("Frame/Painting").GetComponent<RawImage>();
        Assert.IsTrue(painting.enabled);
        // The frame inside the rim is 100 x 50 (2:1); the painting is 4:1, so half its width is kept, a quarter cut at each side.
        Assert.AreEqual(0.5f, painting.uvRect.width, 0.001f);
        Assert.AreEqual(0.25f, painting.uvRect.x, 0.001f);
        Assert.AreEqual(1f, painting.uvRect.height, 0.001f);
        Object.DestroyImmediate(wide);
        Object.DestroyImmediate(texture);
    }

    // The raid report's "What you learned" was a line a skill; beside the haul's pictures it is a card a skill (RaidReportCards).
    [Test]
    public void WhatARaidTaught_IsCards_WhereTheHaulIsPictures_AndWordsWhereItIsNot()
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        var skill = ScriptableObject.CreateInstance<ProficiencyDefinition>();
        try
        {
            var none = new RaidModifierDefinition[0];
            var session = new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven");
            session.End(RaidOutcome.Extracted);
            RaidReport report = RaidReport.Create(session, null, null);
            report.Lessons.Add(new SkillLesson(skill, 3, 5, 40, 10, true));

            StringAssert.Contains("What you learned", RaidReportText.Describe(report), "the plain report keeps the lines");
            StringAssert.DoesNotContain("What you learned", RaidReportText.Describe(report, true), "beside the pictures they are cards");
            StringAssert.Contains("+10 brought home", RaidReportText.LessonsHeading(report));

            RectTransform parent = Parent();
            parent.sizeDelta = new Vector2(600f, 400f);
            var first = new KitHaul(parent, "First", 0f, 64f);
            var second = new KitHaul(parent, "Second", 0f, 64f);
            var lessons = new KitCardRow(parent, "Lessons", 112f, 64f);
            float without = RaidReportCards.Show(report, first, second, 0f);
            float with = RaidReportCards.Show(report, first, second, lessons, 0f, 1000f, out bool shown);
            Assert.IsTrue(shown);
            Assert.Greater(with, without, "the lessons' cards take their room under the haul");
            Assert.AreEqual(1, parent.Find("Lessons").GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Length, "one card for the one skill");

            // No room under the haul (the review of 2026-11-21: the results report was squeezed out): the words keep them.
            float tight = RaidReportCards.Show(report, first, second, lessons, 0f, without + 20f, out bool squeezed);
            Assert.IsFalse(squeezed, "no lesson cards where they don't fit");
            Assert.AreEqual(without, tight, 0.01f);
            Assert.IsFalse(parent.Find("Lessons").gameObject.activeSelf);
            StringAssert.Contains("What you learned", RaidReportText.Describe(report, true, squeezed));

            // Many skills stay one row: the last card counts the rest.
            for (int i = 0; i < 11; i++)
            {
                report.Lessons.Add(new SkillLesson(skill, 1, 1, 5, 0, false));
            }

            float many = RaidReportCards.Show(report, first, second, lessons, 0f, 1000f, out _);
            Assert.AreEqual(with, many, 0.01f, "twelve skills take the room of one row");
        }
        finally
        {
            Object.DestroyImmediate(raid);
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void TheSky_IsNamedAndSigned_ForItsChip()
    {
        Assert.AreEqual("Sun", RaidWeather.IconFor(null));
        Assert.AreEqual("", RaidWeather.NameFor(null, false));
        int checkedSkies = 0;
        foreach (WeatherDefinition weather in All<WeatherDefinition>())
        {
            Assert.AreEqual(weather.DisplayName, RaidWeather.NameFor(weather, false));
            StringAssert.Contains(RaidWeather.NameFor(weather, false), RaidWeather.BriefingLine(weather, false, null), "the chip's name is the line's");
            Assert.IsNotNull(UIStyle.Current.Icon(RaidWeather.IconFor(weather)), weather.name + ": its sign is a kit icon");
            checkedSkies++;
        }

        Assert.Greater(checkedSkies, 2);
    }
}
