using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>The UI kit's rules that are plain text (HearthKit): numbers made to stand out, icons for headings and actions.</summary>
public sealed class HearthKitTests
{
    private static int Toned(string text) => Regex.Matches(text, "<color=#[0-9A-F]{6}><b>").Count;

    [Test]
    public void LoneNumbers_StandOut_ButNotInsideWordsOrColouredText()
    {
        Assert.AreEqual(3, Toned(HearthKit.ToneNumbers("Ward 16 m · Repaired 2 of 5 stones")));
        Assert.AreEqual(1, Toned(HearthKit.ToneNumbers("Crit ×1.5")), "a multiplier is one number");
        Assert.AreEqual(1, Toned(HearthKit.ToneNumbers("Bag 3/8")), "a fraction is one number");
        Assert.AreEqual(1, Toned(HearthKit.ToneNumbers("40% less")));
        Assert.AreEqual(0, Toned(HearthKit.ToneNumbers("Tier II · Mana3 · B2B")), "no lone number here");
        string coloured = "Needs <color=#FF0000>0 / 3</color> sticks";
        Assert.AreEqual(coloured, HearthKit.ToneNumbers(coloured), "a coloured number keeps its colour");
        string once = HearthKit.ToneNumbers("12 Grey Stone, 8 Wood");
        Assert.AreEqual(once, HearthKit.ToneNumbers(once), "safe to run twice");
        Assert.AreEqual(2, Toned(HearthKit.ToneNumbers("<sprite name=\"Bags\" tint=1> Bag 0/8  Weight 2.5")), "tags are left alone");
    }

    [Test]
    public void Headings_AndActions_GetTheirIcons()
    {
        Assert.AreEqual("Bags", HearthKit.IconForHeading("Bag"));
        Assert.AreEqual("Skull", HearthKit.IconForHeading("Known dangers"));
        Assert.IsNull(HearthKit.IconForHeading("Something else"));
        Assert.AreEqual("Hammer", HearthKit.IconForAction("Repair a stone"));
        Assert.AreEqual("Coin", HearthKit.IconForAction("Trade"));
        Assert.IsNull(HearthKit.IconForAction("Close"));
    }
}
