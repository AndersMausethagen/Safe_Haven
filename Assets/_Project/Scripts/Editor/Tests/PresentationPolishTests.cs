using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>What a new creature or a new loading screen has to bring with it, and how a window arrives.</summary>
public sealed class PresentationPolishTests
{
    [Test]
    public void EveryCreatureHasAVoice()
    {
        string[] guids = AssetDatabase.FindAssets("t:MonsterDefinition");
        Assert.IsNotEmpty(guids);
        foreach (string guid in guids)
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsTrue(monster.AttackSound != null && monster.AttackSound.HasClips, $"{monster.name} winds up in silence.");
            Assert.IsTrue(monster.HurtSound != null && monster.HurtSound.HasClips, $"{monster.name} takes a blow in silence.");
            Assert.IsTrue(monster.DeathSound != null && monster.DeathSound.HasClips, $"{monster.name} has no death of its own.");
            Assert.AreEqual(SoundPriority.Important, monster.AttackSound.Priority, $"{monster.name}'s wind-up is a warning, and a warning must get a voice.");
            Assert.IsNotEmpty(monster.AttackSound.Caption, $"{monster.name}'s warning has to reach Sound Captions too.");
        }
    }

    [Test]
    public void AHowlAndASummonAreHeard()
    {
        string[] guids = AssetDatabase.FindAssets("t:MonsterDefinition");
        foreach (string guid in guids)
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (MonsterAttack attack in monster.Attacks)
            {
                if (attack.kind == MonsterAttackKind.Howl || attack.kind == MonsterAttackKind.Summon)
                {
                    Assert.IsNotNull(attack.sound, $"{monster.name}'s {attack.kind} sounds like its bite.");
                }
            }
        }
    }

    [Test]
    public void TheHeavyCanBeHeardWalking()
    {
        foreach (string name in new[] { "ForestBittenWolf", "BlightBoar", "Draug", "BarrowShieldbearer", "SmallMossTroll", "MossShaman", "RootGuardian", "OldMossTroll", "TheHeartroot" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemies/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            Assert.IsNotNull(prefab.GetComponent<CreatureFootfalls>(), $"{name} has no tread.");
        }
    }

    [Test]
    public void LoadingScreensSaySomethingAboutWhereYouAreGoing()
    {
        SceneTransitionSettings settings = SceneTransitionSettings.Current;
        Assert.IsNotNull(settings.BackgroundArt, "Without a destination of its own, a screen still has a picture.");
        foreach (string scene in new[] { "Raid_MistyWoods", "SafeHaven", "Prologue_MistyWoods" })
        {
            Assert.AreNotEqual(settings.BackgroundArt, settings.ArtFor(scene), scene);
            Assert.AreNotSame(settings.Tips, settings.TipsFor(scene), scene);
            Assert.Greater(settings.TipsFor(scene).Length, 3, $"{scene} would soon repeat itself.");
        }

        Assert.AreEqual(settings.BackgroundArt, settings.ArtFor("MainMenu"));
        Assert.AreSame(settings.Tips, settings.TipsFor("MainMenu"));
        Assert.AreSame(settings.Tips, settings.TipsFor(null));
    }

    [Test]
    public void AWindowFadesInAndClosesAtOnce()
    {
        var host = new GameObject("Window");
        var content = new GameObject("Content");
        content.transform.SetParent(host.transform);
        content.SetActive(false);
        try
        {
            var window = host.AddComponent<UIWindow>();
            window.Configure(UIWindowId.Items, content);
            window.Open();
            Assert.IsTrue(content.activeSelf);
            var group = content.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "A window brings its own fade.");
            Assert.AreEqual(0f, group.alpha, "It starts from nothing.");

            window.Close();
            Assert.IsFalse(content.activeSelf, "Nothing waits for a window to fade out.");
            Assert.AreEqual(1f, group.alpha, "A fade cut short must not leave it see-through.");

            window.Open();
            Assert.AreEqual(1f, group.alpha, "The next line of a conversation closes and opens the same window: it must not blink.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
