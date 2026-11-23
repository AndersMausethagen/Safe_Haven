using NUnit.Framework;
using UnityEditor;

/// <summary>
/// What the player makes starts at full durability (the user, 2026-11-23: "Crafting new items doesnt spawn them with 100% durability"):
/// no skill or perk lowers or raises a new piece's condition (Weaponcraft/Armorcraft Finish); the old finish perks give a chance of Fine
/// work instead.
/// </summary>
public sealed class NewGearStartsWholeTests
{
    [Test]
    public void NoSkillOrPerk_TouchesANewPiecesCondition()
    {
        int scanned = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data/Proficiencies" }))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null)
            {
                continue;
            }

            scanned++;
            SerializedProperty it = new SerializedObject(asset).GetIterator();
            while (it.Next(true))
            {
                if (it.name == "stat" && it.propertyType == SerializedPropertyType.Enum)
                {
                    Assert.AreNotEqual((int)StatType.WeaponcraftFinish, it.intValue, asset.name);
                    Assert.AreNotEqual((int)StatType.ArmorcraftFinish, it.intValue, asset.name);
                }
            }
        }

        Assert.Greater(scanned, 10, "the canary: skills and perks were read");
        Assert.AreEqual(1f, CraftedWork.Finish(CraftedGoods.Weapons, new StatSheet()), 1e-5f, "a new weapon starts whole");
    }
}
