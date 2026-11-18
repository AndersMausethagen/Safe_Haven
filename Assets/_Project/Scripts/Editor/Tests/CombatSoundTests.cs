using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Docs/LookAndFeelResearch_2026-11-18.md A6: every combat sound that plays again and again has at least three takes and some pitch
/// spread, so a repeated blow never sounds like a loop (Tools/Audio/variations.py made the missing takes). Cues that tell the player
/// something (a counter is ready, the bow is drawn, stamina ran out) stay the same each time on purpose and are not listed.
/// </summary>
public sealed class CombatSoundTests
{
    [TestCase("Hit")]
    [TestCase("Critical Hit")]
    [TestCase("Swing")]
    [TestCase("Heavy Swing")]
    [TestCase("Block")]
    [TestCase("Parry")]
    [TestCase("Guard Break")]
    [TestCase("Stagger")]
    [TestCase("CreatureDies")]
    [TestCase("Backstab Hit")]
    [TestCase("Gore Splash")]
    [TestCase("Perfect Dodge")]
    [TestCase("Ice Crack")]
    [TestCase("Impact Quake")]
    [TestCase("Shaman Call")]
    [TestCase("Huldra Call")]
    [TestCase("Light Splits")]
    [TestCase("Wing Alarm")]
    [TestCase("Stone Skin")]
    [TestCase("Root Regrowth")]
    public void ARepeatedCombatSoundHasThreeTakesOrMore(string name)
    {
        SoundDefinition sound = null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:SoundDefinition"))
        {
            var found = AssetDatabase.LoadAssetAtPath<SoundDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (found != null && found.name == name)
            {
                sound = found;
                break;
            }
        }

        Assert.IsNotNull(sound, name);
        int clips = 0;
        foreach (UnityEngine.AudioClip clip in sound.Clips)
        {
            clips += clip != null ? 1 : 0;
        }

        Assert.GreaterOrEqual(clips, 3, $"{name}: three takes or more");
        UnityEngine.Vector2 pitch = new SerializedObject(sound).FindProperty("pitchRange").vector2Value;
        Assert.GreaterOrEqual(pitch.y - pitch.x, 0.04f, $"{name}: some pitch spread");
    }
}
