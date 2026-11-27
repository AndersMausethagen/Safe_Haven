using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Abilities found rather than taught (the user, 2026-11-27: "we could also unlock abilities other ways. through exploring or finding rare
/// items"): lore items teach their ability when read at home, never out on a raid; the raids give one not yet found; the Haven's hidden
/// places hold theirs; Bear-Hide makes blows taken fill Focus faster.
/// </summary>
public sealed class SkillLoreTests
{
    [Test]
    public void EveryLoreItem_TeachesAnAbilityOfItsOwn_FoundNowhereElse()
    {
        SkillLoreSettings settings = SkillLoreSettings.Current;
        Assert.GreaterOrEqual(settings.Lore.Count, 4);
        var taught = settings.Lore.Select(l => l.TeachesSkill).ToList();
        Assert.AreEqual(taught.Count, taught.Distinct().Count(), "one ability each");
        var skillSettings = AssetDatabase.LoadAssetAtPath<PlayerSkillSettings>("Assets/_Project/Data/Player/PlayerSkillSettings.asset");
        foreach (ItemDefinition lore in settings.Lore)
        {
            SkillDefinition skill = lore.TeachesSkill;
            Assert.IsNotNull(skill, lore.name);
            Assert.IsTrue(lore.IsLearnedAtHome, $"{lore.name}: taken in at home, so it has to survive the way back");
            Assert.IsTrue(lore.IsUsable, $"{lore.name}: read from the bag (the bag refused it until IsUsable knew lore, Play 2026-11-27)");
            Assert.AreEqual(ItemRarity.Legendary, lore.Rarity, $"{lore.name} is a rare find");
            Assert.IsNotNull(lore.Icon, $"{lore.name} has its icon");
            Assert.IsNotNull(skill.Icon, $"{skill.name} has its icon");
            Assert.IsFalse(skillSettings.StartingSkills.Contains(skill) || skillSettings.WeaponTechniques.Contains(skill), $"{skill.name}: found, not given");
            foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
            {
                var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsFalse(quest.RewardSkills.Contains(skill), $"{skill.name}: found, not a quest's reward ({quest.name})");
            }
        }

        Assert.AreEqual(2, settings.Lore.Count(l => l.TeachesSkill.Kind == SkillKind.Ultimate), "the two rare ultimates");
        Assert.IsTrue(SkillLoreSettings.Current.EliteChance(1) > 0f && SkillLoreSettings.Current.EliteChance(1) < 0.2f, "an elite's lore is rare");
    }

    [Test]
    public void TheHavensHiddenPlaces_HoldLore_OutInTheFarRegions()
    {
        WorldAtlas atlas = WorldAtlas.For("SafeHaven");
        HavenPlaceVerbs.Find[] finds = HavenPlaceVerbs.Current.Finds.Where(f => f != null && f.item != null && f.item.IsSkillLore).ToArray();
        Assert.GreaterOrEqual(finds.Length, 3, "exploring the Haven's land finds abilities");
        foreach (HavenPlaceVerbs.Find find in finds)
        {
            WorldAtlas.Place place = atlas.Places.FirstOrDefault(p => p.id == find.placeId);
            Assert.IsNotNull(place, $"{find.item.name} lies at a place of the map ({find.placeId})");
            Assert.Greater(place.at.magnitude, 400f, $"{find.item.name}: far out, found by exploring");
            Assert.AreEqual(0, find.everyDays, "found once");
            Assert.IsNotNull(find.marker, "something to see where it lies");
        }

        Assert.AreEqual(finds.Length, finds.Select(f => f.placeId).Distinct().Count(), "one to a place");
    }

    [Test]
    public void ARaid_GivesLoreNotYetFound_AndNoneOnceAllAreFound()
    {
        GameSession.StartNewGame();
        var player = new GameObject("Lore Player");
        try
        {
            player.AddComponent<PlayerStateMachine>();
            var bag = player.AddComponent<ItemInventory>();
            var skills = player.AddComponent<PlayerSkills>();
            typeof(ItemInventory).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(bag, null);
            typeof(PlayerSkills).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(skills, null);
            var random = new System.Random(5);
            Assert.IsFalse(SkillLore.AnyFound(player), "a new game has found none: the first great creature's is sure");
            ItemDefinition first = SkillLore.Pick(player, random);
            Assert.IsNotNull(first);
            bag.Container.Add(first, 1);
            Assert.IsTrue(SkillLore.KnownOrCarried(first, player), "carried home unread counts as found");
            Assert.IsTrue(SkillLore.AnyFound(player));
            for (int i = 0; i < 20; i++)
            {
                Assert.AreNotSame(first, SkillLore.Pick(player, random), "never the same find while others are left");
            }

            foreach (ItemDefinition lore in SkillLoreSettings.Current.Lore)
            {
                skills.Learn(lore.TeachesSkill, false);
            }

            Assert.IsNull(SkillLore.Pick(player, random), "every one found: no more lore");
        }
        finally
        {
            Object.DestroyImmediate(player);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void BearHide_FillsFocusFasterFromBlowsTaken()
    {
        var bear = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/BearHide.asset");
        Assert.Greater(bear.FocusFromBlows, 1.5f, "built around taking hits");
        Assert.AreEqual(SkillKind.Power, bear.Kind);
        Assert.IsTrue(bear.HasBuffs);
        Assert.IsTrue(bear.Buffs.Any(b => b.stat == StatType.PhysicalResistance && b.value > 0.2f), "and it takes far less from them");
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition"))
        {
            var other = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (other != bear)
            {
                Assert.AreEqual(1f, other.FocusFromBlows, $"{other.name}: only Bear-Hide changes it");
            }
        }
    }
}
