using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The skills that replaced the character level and talents (`unity command script run --file AgentScripts/BuildProficiencies.cs --entry BuildProficiencies.Apply`):
/// Resources/LevelingSettings (the curve, the baseline level the game was tuned for, experience per action and Insight),
/// each skill in Data/Proficiencies with its level bonuses and milestones, and their perks in Data/Proficiencies/Perks. The talents of
/// the four trees (Data/Talents) are removed: their bonuses live on as perks. See Docs/Skills.md for why these numbers.
/// Safe to run again: assets are found by path and filled in afresh.
/// </summary>
public static class BuildProficiencies
{
    private const string Folder = "Assets/_Project/Data/Proficiencies";
    private const string PerkFolder = Folder + "/Perks";
    private const string SettingsPath = "Assets/_Project/Resources/LevelingSettings.asset";
    private const string OldTalents = "Assets/_Project/Data/Talents";
    private const float Pct = 0.01f;

    public static string Apply()
    {
        EnsureFolder("Assets/_Project/Data", "Proficiencies");
        EnsureFolder(Folder, "Perks");

        var skills = new List<ProficiencyDefinition> { Woodcutting(), Mining(), Melee(), Sneaking() };
        string settings = Settings(skills);
        string talents = AssetDatabase.IsValidFolder(OldTalents) && AssetDatabase.DeleteAsset(OldTalents) ? "old talents removed" : "no old talents";
        AssetDatabase.SaveAssets();
        return $"{skills.Count} skills; {settings}; {talents}";
    }

    // ----- Gathering -----

    private static ProficiencyDefinition Woodcutting() => Skill("woodcutting", "Woodcutting",
        "The axe in a practised hand: where to stand, where to cut, when to let the tree do the work.",
        "felling and splitting trees", ProficiencyGroup.Gathering, ProficiencyTraining.Gathering, ToolType.Axe,
        new[]
        {
            Level(StatType.ChopSpeed, -10 * Pct, 25 * Pct),
            Level(StatType.ChopSweetSpot, -30 * Pct, 40 * Pct),
            Level(StatType.ChopYield, 0f, 15 * Pct),
        },
        Milestone(10,
            Perk("wood_clean_cuts", "Clean Cuts", "Strike with the grain and the edge lasts.", Bonus(StatType.ToolCare, 15 * Pct)),
            Perk("wood_woodsmans_eye", "Woodsman's Eye", "You see the second log in the first.", Bonus(StatType.ChopYield, 8 * Pct))),
        Milestone(20,
            Perk("wood_steady_swing", "Steady Swing", "The axe falls where you look.", Bonus(StatType.ChopSweetSpot, 15 * Pct)),
            Perk("wood_quick_hands", "Quick Hands", "No pause between one cut and the next.", Bonus(StatType.ChopSpeed, 10 * Pct))),
        Milestone(30,
            Perk("wood_timber_sense", "Timber Sense", "Sound wood rings; you hear it.", Bonus(StatType.ChopYield, 10 * Pct)),
            Perk("wood_foresters_lungs", "Forester's Lungs", "A day in the woods is a day's breath.", Bonus(StatType.MaxStamina, 10f))),
        Milestone(40,
            Perk("wood_heartwood", "Heartwood", "What grows, you know how to take.", Bonus(StatType.GatherYield, 6 * Pct)),
            Perk("wood_swing_through", "Swing Through", "Let the weight of the head do the lifting.", Bonus(StatType.ChopSpeed, 12 * Pct))),
        Milestone(50,
            Perk("wood_master_feller", "Master Feller", "Trees fall where you tell them.", Bonus(StatType.ChopSweetSpot, 25 * Pct), Bonus(StatType.ChopYield, 5 * Pct)),
            Perk("wood_axe_fighter", "Axe Fighter", "Twenty winters of felling make a fearsome arm.", Bonus(StatType.MeleeDamage, 5 * Pct))));

    private static ProficiencyDefinition Mining() => Skill("mining", "Mining",
        "Reading stone: the seam, the fault, the blow that splits it.",
        "breaking rock and ore", ProficiencyGroup.Gathering, ProficiencyTraining.Gathering, ToolType.Pickaxe,
        new[]
        {
            Level(StatType.MineSpeed, -10 * Pct, 25 * Pct),
            Level(StatType.MineSweetSpot, -30 * Pct, 40 * Pct),
            Level(StatType.MineYield, 0f, 15 * Pct),
        },
        Milestone(10,
            Perk("mine_prospector", "Prospector", "You see the seam before the pick does.", Bonus(StatType.MineYield, 8 * Pct)),
            Perk("mine_careful_hands", "Careful Hands", "A pick lasts as long as the hand is patient.", Bonus(StatType.ToolCare, 15 * Pct))),
        Milestone(20,
            Perk("mine_read_the_seam", "Read the Seam", "Every rock tells you where it will break.", Bonus(StatType.MineSweetSpot, 15 * Pct)),
            Perk("mine_heavy_swing", "Heavy Swing", "Lift, fall, lift: a rhythm you no longer think about.", Bonus(StatType.MineSpeed, 10 * Pct))),
        Milestone(30,
            Perk("mine_deep_veins", "Deep Veins", "The best ore hides a hand's width further in.", Bonus(StatType.MineYield, 10 * Pct)),
            Perk("mine_work_song", "Work Song", "Keep the rhythm and the work keeps you.", Bonus(StatType.MaxStamina, 10f))),
        Milestone(40,
            Perk("mine_wrecker", "Wrecker", "Every wall has a weak joint.", Bonus(StatType.StructureDamage, 15 * Pct)),
            Perk("mine_stonecutter", "Stonecutter", "Stone gives way to the hand that knows it.", Bonus(StatType.MineSpeed, 12 * Pct))),
        Milestone(50,
            Perk("mine_master_miner", "Master Miner", "One blow, and the rock opens like a book.", Bonus(StatType.MineSweetSpot, 25 * Pct), Bonus(StatType.MineYield, 5 * Pct)),
            Perk("mine_iron_arms", "Iron Arms", "Hauling stone builds more than walls.", Bonus(StatType.Strength, 3f))));

    // ----- Combat -----

    private static ProficiencyDefinition Melee() => Skill("melee", "Melee",
        "The weapon in hand and the shield on the arm: striking, blocking, and knowing when to step aside.",
        "weapon blows that hurt monsters, parries and perfect dodges", ProficiencyGroup.Combat, ProficiencyTraining.Melee, ToolType.None,
        new[]
        {
            Level(StatType.MeleeDamage, -12 * Pct, 20 * Pct),
            Level(StatType.MeleeStaminaCost, 25 * Pct, -20 * Pct),
            Level(StatType.BlockStaminaCost, 25 * Pct, -25 * Pct),
        },
        Milestone(10,
            Perk("melee_weapon_drill", "Weapon Drill", "A thousand cuts at a post, for the one that counts.", Bonus(StatType.PhysicalPower, 3f)),
            Perk("melee_thick_skin", "Thick Skin", "Scars are just armour you grew yourself.", Bonus(StatType.MaxHealth, 15f))),
        Milestone(20,
            Perk("melee_shield_arm", "Shield Arm", "Take the blow where it hurts least.", Bonus(StatType.Armor, 8f)),
            Perk("melee_keen_edge", "Keen Edge", "Aim for the gap in the hide.", Bonus(StatType.PhysicalCritChance, 4 * Pct))),
        Milestone(30,
            Perk("melee_endurance", "Endurance", "Twenty winters in the shield wall.", Bonus(StatType.MaxStamina, 15f)),
            Perk("melee_hunters_patience", "Hunter's Patience", "Wait for the moment, then end it.", Bonus(StatType.PhysicalCritDamage, 20 * Pct))),
        Milestone(40,
            Perk("melee_unbroken", "Unbroken", "Knocked down, never out.", Bonus(StatType.PhysicalResistance, 6 * Pct)),
            Perk("melee_red_edge", "Red Edge", "What the blade takes, it gives back.", Bonus(StatType.Lifesteal, 4 * Pct))),
        Milestone(50,
            Perk("melee_warden", "Warden", "Behind you, the Haven. In front, nothing gets past.", Bonus(StatType.MaxHealth, 25f), Bonus(StatType.Armor, 5f)),
            Perk("melee_reaver", "Reaver", "Every blow a finishing blow.", Bonus(StatType.MeleeDamage, 10 * Pct))));

    // ----- Stealth -----

    private static ProficiencyDefinition Sneaking() => Skill("sneaking", "Sneaking",
        "Moving unseen and unheard: where to put a foot, when to stand still, how to open a lock without a sound.",
        "ambushes, slipping past monsters, losing those that hunt you, and silent lock pins", ProficiencyGroup.Stealth,
        ProficiencyTraining.Sneaking, ToolType.None,
        new[]
        {
            Level(StatType.SneakNoise, 30 * Pct, -40 * Pct),
            // Time to be spotted: 85% of the tuned time at level 1 (visibility x 1/0.85), 140% at the top (x 1/1.4).
            Level(StatType.SneakVisibility, 18 * Pct, -29 * Pct),
            Level(StatType.LockpickZone, -20 * Pct, 30 * Pct),
        },
        Milestone(10,
            Perk("sneak_light_feet", "Light Feet", "Moss under the boots, never twigs.", Bonus(StatType.MoveSpeed, 3 * Pct)),
            Perk("sneak_soft_hands", "Soft Hands", "The lock tells your fingers what it wants.", Bonus(StatType.LockpickZone, 15 * Pct))),
        Milestone(20,
            Perk("sneak_shadow_strike", "Shadow Strike", "The first blow is the one they never see.", Bonus(StatType.AmbushDamage, 25 * Pct)),
            Perk("sneak_quiet_step", "Quiet Step", "Heel, then toe, then nothing.", Bonus(StatType.SneakNoise, -15 * Pct))),
        Milestone(30,
            Perk("sneak_keen_eye", "Keen Eye", "You see them first, and where it will hurt.", Bonus(StatType.PhysicalCritChance, 4 * Pct)),
            Perk("sneak_low_profile", "Low Profile", "A shape among shapes.", Bonus(StatType.SneakVisibility, -12 * Pct))),
        Milestone(40,
            Perk("sneak_assassin", "Assassin", "Unaware is as good as undefended.", Bonus(StatType.AmbushDamage, 35 * Pct)),
            Perk("sneak_ghost", "Ghost", "Gone before the forest knows you were there.", Bonus(StatType.SneakNoise, -15 * Pct), Bonus(StatType.SneakVisibility, -8 * Pct))),
        Milestone(50,
            Perk("sneak_nightstalker", "Nightstalker", "The dark is yours, and everything in it.", Bonus(StatType.AmbushDamage, 30 * Pct), Bonus(StatType.MoveSpeed, 3 * Pct)),
            Perk("sneak_cutpurse", "Cutpurse", "The forest's curses come away in your hand.", Bonus(StatType.EssenceFind, 20 * Pct))));

    // ----- Settings -----

    private static string Settings(List<ProficiencyDefinition> skills)
    {
        var settings = AssetDatabase.LoadAssetAtPath<LevelingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LevelingSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("maxLevel").intValue = 50;
        so.FindProperty("firstLevelExperience").intValue = 50;
        so.FindProperty("levelGrowth").floatValue = 1.105f;
        so.FindProperty("baselineLevel").intValue = 10;
        SerializedProperty tiers = so.FindProperty("tierExperience");
        int[] perTier = { 10, 16, 25, 40, 60 };
        tiers.arraySize = perTier.Length;
        for (int i = 0; i < perTier.Length; i++) tiers.GetArrayElementAtIndex(i).intValue = perTier[i];
        so.FindProperty("levelsPerTier").intValue = 10;
        so.FindProperty("oneTierBelow").floatValue = 0.6f;
        so.FindProperty("farBelow").floatValue = 0.25f;
        so.FindProperty("raidMultiplier").floatValue = 1.5f;
        so.FindProperty("killExperiencePerHealth").floatValue = 0.25f;
        so.FindProperty("minimumKillExperience").intValue = 3;
        so.FindProperty("guardExperience").intValue = 3;
        so.FindProperty("guardRewardsPerFoe").intValue = 5;
        so.FindProperty("ambushExperience").intValue = 8;
        so.FindProperty("slipExperience").intValue = 4;
        so.FindProperty("slipSeconds").floatValue = 6f;
        so.FindProperty("slipsPerFoe").intValue = 3;
        so.FindProperty("lostTrackExperience").intValue = 10;
        so.FindProperty("silentPinExperience").intValue = 2;
        so.FindProperty("handGatherExperience").intValue = 2;
        so.FindProperty("insightStep").intValue = 100;
        if (so.FindProperty("levelUpSound").objectReferenceValue == null)
        {
            so.FindProperty("levelUpSound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Quest Completed.asset");
        }

        SerializedProperty list = so.FindProperty("skills");
        list.arraySize = skills.Count;
        for (int i = 0; i < skills.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return "settings " + SettingsPath;
    }

    // ----- Helpers -----

    private static ProficiencyDefinition Skill(string id, string label, string text, string trainedBy, ProficiencyGroup group,
        ProficiencyTraining training, ToolType tool, ProficiencyDefinition.LevelBonus[] bonuses, params ProficiencyDefinition.Milestone[] milestones)
    {
        string path = $"{Folder}/{label.Replace(" ", "")}.asset";
        var skill = AssetDatabase.LoadAssetAtPath<ProficiencyDefinition>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<ProficiencyDefinition>();
            AssetDatabase.CreateAsset(skill, path);
        }

        skill.Configure(id, label, text, trainedBy, group, training, tool, bonuses, milestones);
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static PerkDefinition Perk(string id, string label, string text, params StatBonus[] bonuses)
    {
        string path = $"{PerkFolder}/{label.Replace(" ", "").Replace("'", "")}.asset";
        var perk = AssetDatabase.LoadAssetAtPath<PerkDefinition>(path);
        if (perk == null)
        {
            perk = ScriptableObject.CreateInstance<PerkDefinition>();
            AssetDatabase.CreateAsset(perk, path);
        }

        perk.Configure(id, label, text, bonuses);
        EditorUtility.SetDirty(perk);
        return perk;
    }

    private static ProficiencyDefinition.LevelBonus Level(StatType stat, float atFirst, float atLast) => new ProficiencyDefinition.LevelBonus(stat, atFirst, atLast);

    private static ProficiencyDefinition.Milestone Milestone(int level, PerkDefinition first, PerkDefinition second) =>
        new ProficiencyDefinition.Milestone(level, first, second);

    private static StatBonus Bonus(StatType stat, float value) => new StatBonus(stat, value);

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
