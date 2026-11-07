using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The skills that replaced the character level and talents (gathering, crafting, combat, stealth) (`unity command script run --file AgentScripts/BuildProficiencies.cs --entry BuildProficiencies.Apply`):
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

        var skills = new List<ProficiencyDefinition>
        {
            Woodcutting(), Mining(), Foraging(), Fishing(),
            Weaponcrafting(), Armorcrafting(), Cooking(), Alchemy(), Construction(),
            Melee(), Archery(), Runecraft(),
            Sneaking(),
        };
        string techniques = TeachTechniques(skills);
        string settings = Settings(skills);
        string mentors = Mentors(skills);
        string rested = Rested();
        string gates = Gates(skills);
        string teachers = Teachers(skills);
        string talents = AssetDatabase.IsValidFolder(OldTalents) && AssetDatabase.DeleteAsset(OldTalents) ? "old talents removed" : "no old talents";
        AssetDatabase.SaveAssets();
        return $"{skills.Count} skills; {techniques}; {settings}; {mentors}; {rested}; {gates}; {teachers}; {talents}";
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
            null),  // taught a technique: TechniquePerks
        Milestone(30,
            Perk("wood_timber_sense", "Timber Sense", "Sound wood rings; you hear it.", Bonus(StatType.ChopYield, 10 * Pct)),
            Perk("wood_foresters_lungs", "Forester's Lungs", "A day in the woods is a day's breath.", Bonus(StatType.MaxStamina, 10f))),
        Milestone(40,
            Perk("wood_heartwood", "Heartwood", "What grows, you know how to take.", Bonus(StatType.GatherYield, 6 * Pct)),
            null),  // taught a technique: TechniquePerks
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
            null),  // taught a technique: TechniquePerks
        Milestone(30,
            Perk("mine_deep_veins", "Deep Veins", "The best ore hides a hand's width further in.", Bonus(StatType.MineYield, 10 * Pct)),
            Perk("mine_work_song", "Work Song", "Keep the rhythm and the work keeps you.", Bonus(StatType.MaxStamina, 10f))),
        Milestone(40,
            Perk("mine_wrecker", "Wrecker", "Every wall has a weak joint.", Bonus(StatType.StructureDamage, 15 * Pct)),
            null),  // taught a technique: TechniquePerks
        Milestone(50,
            Perk("mine_master_miner", "Master Miner", "One blow, and the rock opens like a book.", Bonus(StatType.MineSweetSpot, 25 * Pct), Bonus(StatType.MineYield, 5 * Pct)),
            Perk("mine_iron_arms", "Iron Arms", "Hauling stone builds more than walls.", Bonus(StatType.Strength, 3f))));

    private static ProficiencyDefinition Foraging() => Skill("foraging", "Foraging",
        "Berries, herbs, mushrooms and fibre: knowing what grows where, and taking it without spoiling it.",
        "picking plants by hand and cutting them with the sickle", ProficiencyGroup.Gathering, ProficiencyTraining.Gathering, ToolType.Sickle,
        new[]
        {
            Level(StatType.ForageSpeed, -15 * Pct, 30 * Pct),
            Level(StatType.ForageYield, 0f, 20 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("forage_forager", "Forager", "You find the berries the birds missed.", Bonus(StatType.ForageYield, 8 * Pct)),
                Perk("forage_quick_fingers", "Quick Fingers", "Pick, drop, pick again.", Bonus(StatType.ForageSpeed, 10 * Pct))),
            Milestone(20,
                Perk("forage_wild_tea", "Wild Tea", "A leaf to chew on the long walk.", Bonus(StatType.MaxStamina, 8f)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("forage_bountiful", "Bountiful", "The forest gives to those who ask kindly.", Bonus(StatType.ForageYield, 10 * Pct)),
                Perk("forage_nimble_picker", "Nimble Picker", "Both hands, never a bruise.", Bonus(StatType.ForageSpeed, 15 * Pct))),
            Milestone(40,
                Perk("forage_forest_friend", "Forest Friend", "Wood and stone give a little more too.", Bonus(StatType.GatherYield, 5 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("forage_master_forager", "Master Forager", "Nothing that grows is hidden from you.", Bonus(StatType.ForageYield, 12 * Pct), Bonus(StatType.ForageSpeed, 10 * Pct)),
                Perk("forage_wildheart", "Wildheart", "The forest's strength runs in you.", Bonus(StatType.MaxHealth, 20f))),
        }, CraftedGoods.None, true);

    private static ProficiencyDefinition Fishing() => Skill("fishing", "Fishing",
        "Line, lure and patience: reading the water and keeping a fighting fish on the hook.",
        "landing fish", ProficiencyGroup.Gathering, ProficiencyTraining.Fishing, ToolType.None,
        new[]
        {
            Level(StatType.ReelZone, -20 * Pct, 30 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("fish_patient_line", "Patient Line", "Let it run, then bring it in.", Bonus(StatType.ReelZone, 10 * Pct)),
                Perk("fish_strong_wrists", "Strong Wrists", "A rod is a weapon of a kind.", Bonus(StatType.MaxStamina, 8f))),
            Milestone(20,
                Perk("fish_wide_reel", "Wide Reel", "Room to give and take.", Bonus(StatType.ReelZone, 12 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("fish_old_lines", "Old Lines", "Knots your grandmother taught you.", Bonus(StatType.ReelZone, 12 * Pct)),
                Perk("fish_fish_supper", "Fish Supper", "Fresh from the water, the best meal there is.", Bonus(StatType.MealDuration, 10 * Pct))),
            Milestone(40,
                Perk("fish_nokkens_respect", "Nokken's Respect", "The water spirit lets you be.", Bonus(StatType.HolyResistance, 8 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("fish_master_angler", "Master Angler", "The river gives up what you ask of it.", Bonus(StatType.ReelZone, 20 * Pct)),
                Perk("fish_sea_legs", "Sea Legs", "Steady on any footing.", Bonus(StatType.MaxHealth, 15f))),
        }, CraftedGoods.Catch);  // the Dock's nets and lines

    // ----- Crafting -----

    private static ProficiencyDefinition Weaponcrafting() => Skill("weaponcrafting", "Weaponcrafting",
        "Edges, hafts and fletching: the feel of a good balance and a true line.",
        "making weapons, tools and arrows", ProficiencyGroup.Crafting, ProficiencyTraining.Crafting, ToolType.None,
        new[]
        {
            // A beginner's work starts worn; from the baseline on, some of it comes out Fine (a quarter of that Masterwork).
            Level(StatType.WeaponcraftFinish, -25 * Pct, 0f),
            Level(StatType.WeaponcraftQuality, 0f, 60 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("weapon_keen_grind", "Keen Grind", "A whetstone and patience.", Bonus(StatType.WeaponcraftQuality, 8 * Pct)),
                Perk("weapon_tool_maker", "Tool Maker", "A haft that fits the hand wears the edge less.", Bonus(StatType.ToolCare, 15 * Pct))),
            Milestone(20,
                Perk("weapon_balanced_blades", "Balanced Blades", "You know where the weight should sit.", Bonus(StatType.PhysicalCritChance, 3 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("weapon_smiths_eye", "Smith's Eye", "The colour of the steel tells you when.", Bonus(StatType.WeaponcraftQuality, 10 * Pct)),
                Perk("weapon_know_your_steel", "Know Your Steel", "You made it; you know what it can do.", Bonus(StatType.MeleeDamage, 4 * Pct))),
            Milestone(40,
                Perk("weapon_hammer_arm", "Hammer Arm", "A thousand blows on the anvil.", Bonus(StatType.Strength, 3f)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("weapon_master_smith", "Master Smith", "Others bring their blades to you.", Bonus(StatType.WeaponcraftQuality, 15 * Pct)),
                Perk("weapon_runed_edges", "Runed Edges", "A mark cut into the fuller, for luck.", Bonus(StatType.PhysicalCritDamage, 15 * Pct))),
        }, CraftedGoods.Weapons | CraftedGoods.Tools | CraftedGoods.Arrows);

    private static ProficiencyDefinition Armorcrafting() => Skill("armorcrafting", "Armorcrafting",
        "Leather, cloth, rings and plates: what turns a blow, and what lets you move.",
        "making armour, shields and packs", ProficiencyGroup.Crafting, ProficiencyTraining.Crafting, ToolType.None,
        new[]
        {
            Level(StatType.ArmorcraftFinish, -25 * Pct, 0f),
            Level(StatType.ArmorcraftQuality, 0f, 60 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("armor_even_stitches", "Even Stitches", "Small, tight, every one the same.", Bonus(StatType.ArmorcraftQuality, 8 * Pct)),
                Perk("armor_padded_lining", "Padded Lining", "Wool under the leather.", Bonus(StatType.MaxHealth, 10f))),
            Milestone(20,
                Perk("armor_fitted_plates", "Fitted Plates", "Made to your own shape.", Bonus(StatType.Armor, 6f)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("armor_armourers_eye", "Armourer's Eye", "You see the weak rivet before it gives.", Bonus(StatType.ArmorcraftQuality, 10 * Pct)),
                Perk("armor_warm_furs", "Warm Furs", "Fur on the inside, where it counts.", Bonus(StatType.FrostResistance, 8 * Pct))),
            Milestone(40,
                Perk("armor_proofed_leather", "Proofed Leather", "Boiled, waxed and boiled again.", Bonus(StatType.PhysicalResistance, 4 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("armor_master_armourer", "Master Armourer", "Others come to be measured.", Bonus(StatType.ArmorcraftQuality, 15 * Pct)),
                Perk("armor_second_skin", "Second Skin", "You forget you are wearing it.", Bonus(StatType.Armor, 10f), Bonus(StatType.MaxHealth, 10f))),
        }, CraftedGoods.Armor);

    private static ProficiencyDefinition Cooking() => Skill("cooking", "Cooking",
        "Fire, pot and patience: food that fills you and keeps you going.",
        "cooking at the campfire and the kitchen", ProficiencyGroup.Crafting, ProficiencyTraining.Crafting, ToolType.None,
        new[]
        {
            Level(StatType.MealDuration, -20 * Pct, 50 * Pct),
            Level(StatType.CookingYield, 0f, 25 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("cook_hearty_portions", "Hearty Portions", "Always enough for one more.", Bonus(StatType.CookingYield, 8 * Pct)),
                Perk("cook_slow_simmer", "Slow Simmer", "The longer it cooks, the longer it lasts.", Bonus(StatType.MealDuration, 15 * Pct))),
            Milestone(20,
                Perk("cook_trail_rations", "Trail Rations", "Food that walks with you.", Bonus(StatType.MaxStamina, 8f)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("cook_spice_lore", "Spice Lore", "A pinch of the right thing.", Bonus(StatType.MealDuration, 20 * Pct)),
                Perk("cook_feast_maker", "Feast Maker", "Cook for one, feed four.", Bonus(StatType.CookingYield, 12 * Pct))),
            Milestone(40,
                Perk("cook_strong_stomach", "Strong Stomach", "Nothing you ate ever did you harm.", Bonus(StatType.MaxHealth, 15f)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("cook_hearth_master", "Hearth Master", "The Haven gathers where you cook.", Bonus(StatType.MealDuration, 25 * Pct), Bonus(StatType.CookingYield, 5 * Pct)),
                Perk("cook_iron_gut", "Iron Gut", "A full belly is a deep breath.", Bonus(StatType.MaxStamina, 15f))),
        }, CraftedGoods.Meals);

    private static ProficiencyDefinition Alchemy() => Skill("alchemy", "Alchemy",
        "Herbs, essences and the cauldron: draughts that turn a fight and cures that save a life.",
        "brewing tonics and remedies", ProficiencyGroup.Crafting, ProficiencyTraining.Crafting, ToolType.None,
        new[]
        {
            Level(StatType.TonicDuration, -20 * Pct, 50 * Pct),
            Level(StatType.AlchemyYield, 0f, 25 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("alch_steady_hand", "Steady Hand", "Not a drop spilled.", Bonus(StatType.AlchemyYield, 8 * Pct)),
                Perk("alch_long_brew", "Long Brew", "Let it steep another night.", Bonus(StatType.TonicDuration, 15 * Pct))),
            Milestone(20,
                Perk("alch_bitter_root", "Bitter Root", "A taste that keeps out the cold and the flame.", Bonus(StatType.FireResistance, 5 * Pct), Bonus(StatType.FrostResistance, 5 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("alch_rune_steeping", "Rune Steeping", "Words spoken over the pot.", Bonus(StatType.SpellPower, 4f)),
                Perk("alch_lasting_draughts", "Lasting Draughts", "The warmth stays in the blood.", Bonus(StatType.TonicDuration, 20 * Pct))),
            Milestone(40,
                Perk("alch_cursed_lore", "Cursed Lore", "You know where the curse gathers.", Bonus(StatType.EssenceFind, 10 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("alch_master_alchemist", "Master Alchemist", "The cauldron does what you ask.", Bonus(StatType.TonicDuration, 25 * Pct), Bonus(StatType.AlchemyYield, 5 * Pct)),
                Perk("alch_spell_distillation", "Spell Distillation", "A rune in a bottle.", Bonus(StatType.SpellCritChance, 5 * Pct))),
        }, CraftedGoods.Tonics);

    private static ProficiencyDefinition Construction() => Skill("construction", "Construction",
        "Beams, stone and joinery: raising the Haven, and readying what it is built from.",
        "hammering buildings up and refining planks, ingots and blocks", ProficiencyGroup.Crafting, ProficiencyTraining.Construction, ToolType.None,
        new[]
        {
            Level(StatType.BuildSpeed, -20 * Pct, 50 * Pct),
            Level(StatType.RefiningYield, 0f, 20 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("build_quick_hammer", "Quick Hammer", "Three blows where others need five.", Bonus(StatType.BuildSpeed, 12 * Pct)),
                Perk("build_careful_saw", "Careful Saw", "Measure twice.", Bonus(StatType.RefiningYield, 8 * Pct))),
            Milestone(20,
                Perk("build_builders_back", "Builder's Back", "Beams on the shoulder all day.", Bonus(StatType.MaxStamina, 10f)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("build_master_joiner", "Master Joiner", "Nothing wasted, every offcut used.", Bonus(StatType.RefiningYield, 10 * Pct)),
                Perk("build_raise_the_roof", "Raise the Roof", "Up before sundown.", Bonus(StatType.BuildSpeed, 15 * Pct))),
            Milestone(40,
                Perk("build_hearth_keeper", "Hearth Keeper", "The Haven keeps you as you keep it.", Bonus(StatType.MaxHealth, 10f)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("build_master_builder", "Master Builder", "The Haven stands in your hands' shape.", Bonus(StatType.BuildSpeed, 25 * Pct), Bonus(StatType.RefiningYield, 5 * Pct)),
                Perk("build_stone_skin", "Stone Skin", "Hard as the walls you raise.", Bonus(StatType.Armor, 8f))),
        }, CraftedGoods.Materials);

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
            null),  // taught a technique: TechniquePerks
        Milestone(30,
            Perk("melee_endurance", "Endurance", "Twenty winters in the shield wall.", Bonus(StatType.MaxStamina, 15f)),
            Perk("melee_hunters_patience", "Hunter's Patience", "Wait for the moment, then end it.", Bonus(StatType.PhysicalCritDamage, 20 * Pct))),
        Milestone(40,
            Perk("melee_unbroken", "Unbroken", "Knocked down, never out.", Bonus(StatType.PhysicalResistance, 6 * Pct)),
            null),  // taught a technique: TechniquePerks
        Milestone(50,
            Perk("melee_warden", "Warden", "Behind you, the Haven. In front, nothing gets past.", Bonus(StatType.MaxHealth, 25f), Bonus(StatType.Armor, 5f)),
            Perk("melee_reaver", "Reaver", "Every blow a finishing blow.", Bonus(StatType.MeleeDamage, 10 * Pct))));

    private static ProficiencyDefinition Archery() => Skill("archery", "Archery",
        "Bow and arrow: the draw, the breath, the release.",
        "arrows that hurt monsters", ProficiencyGroup.Combat, ProficiencyTraining.Archery, ToolType.None,
        new[]
        {
            Level(StatType.ArrowDamage, -12 * Pct, 20 * Pct),
            Level(StatType.DrawSpeed, -15 * Pct, 25 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("bow_steady_draw", "Steady Draw", "A bowstring you know like your own breath.", Bonus(StatType.DrawSpeed, 8 * Pct)),
                Perk("bow_keen_sight", "Keen Sight", "Aim for the gap in the hide.", Bonus(StatType.PhysicalCritChance, 3 * Pct))),
            Milestone(20,
                Perk("bow_broadheads", "Broadheads", "Wide iron, deep wounds.", Bonus(StatType.ArrowDamage, 6 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("bow_hunters_eye", "Hunter's Eye", "Wait for the moment, then end it.", Bonus(StatType.PhysicalCritDamage, 15 * Pct)),
                Perk("bow_quick_nock", "Quick Nock", "The next arrow is on the string before the first lands.", Bonus(StatType.DrawSpeed, 10 * Pct))),
            Milestone(40,
                Perk("bow_deadeye", "Deadeye", "Where you look, the arrow goes.", Bonus(StatType.ArrowDamage, 8 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("bow_master_archer", "Master Archer", "The bow is an arm you were born with.", Bonus(StatType.ArrowDamage, 10 * Pct), Bonus(StatType.DrawSpeed, 5 * Pct)),
                Perk("bow_ranger", "Ranger", "From the shadows, one arrow is enough.", Bonus(StatType.AmbushDamage, 25 * Pct))),
        });

    private static ProficiencyDefinition Runecraft() => Skill("runecraft", "Runecraft",
        "Old words cut and spoken: Purified and Cursed alike.",
        "spells that hurt monsters, and every spell cast", ProficiencyGroup.Combat, ProficiencyTraining.Spells, ToolType.None,
        new[]
        {
            Level(StatType.SpellDamage, -12 * Pct, 20 * Pct),
            Level(StatType.SpellStaminaCost, 25 * Pct, -20 * Pct),
        },
        new[]
        {
            Milestone(10,
                Perk("rune_rune_lore", "Rune Lore", "Old words, carved deeper.", Bonus(StatType.SpellPower, 4f)),
                Perk("rune_warding_signs", "Warding Signs", "Marks against flame and frost.", Bonus(StatType.FireResistance, 5 * Pct), Bonus(StatType.FrostResistance, 5 * Pct))),
            Milestone(20,
                Perk("rune_sharp_runes", "Sharp Runes", "A rune cut true bites twice.", Bonus(StatType.SpellCritChance, 4 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(30,
                Perk("rune_holy_marks", "Holy Marks", "What the dark fears, worn on the skin.", Bonus(StatType.HolyResistance, 8 * Pct)),
                Perk("rune_deep_words", "Deep Words", "Spoken from the chest, not the throat.", Bonus(StatType.SpellDamage, 6 * Pct))),
            Milestone(40,
                Perk("rune_runes_of_ruin", "Runes of Ruin", "The word that means open, said too loud.", Bonus(StatType.SpellCritDamage, 20 * Pct)),
                null),  // taught a technique: TechniquePerks
            Milestone(50,
                Perk("rune_runemaster", "Runemaster", "The old words answer to you.", Bonus(StatType.SpellDamage, 10 * Pct), Bonus(StatType.SpellPower, 4f)),
                Perk("rune_cursed_tongue", "Cursed Tongue", "What the curse takes, you keep.", Bonus(StatType.Lifesteal, 3 * Pct))),
        });

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
            null),  // taught a technique: TechniquePerks
        Milestone(30,
            Perk("sneak_keen_eye", "Keen Eye", "You see them first, and where it will hurt.", Bonus(StatType.PhysicalCritChance, 4 * Pct)),
            Perk("sneak_low_profile", "Low Profile", "A shape among shapes.", Bonus(StatType.SneakVisibility, -12 * Pct))),
        Milestone(40,
            Perk("sneak_assassin", "Assassin", "Unaware is as good as undefended.", Bonus(StatType.AmbushDamage, 35 * Pct)),
            null),  // taught a technique: TechniquePerks
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
        so.FindProperty("beginnerEase").floatValue = 2.5f;
        so.FindProperty("insightFullBelow").intValue = 20;
        so.FindProperty("insightShareAbove").floatValue = 0.5f;
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
        so.FindProperty("weaponWork").floatValue = 3f;
        so.FindProperty("armorWork").floatValue = 3f;
        so.FindProperty("toolWork").floatValue = 2f;
        so.FindProperty("arrowWork").floatValue = 0.5f;
        so.FindProperty("mealWork").floatValue = 1f;
        so.FindProperty("tonicWork").floatValue = 1.5f;
        so.FindProperty("materialWork").floatValue = 0.5f;
        so.FindProperty("buildWork").floatValue = 4f;
        so.FindProperty("upgradeWork").floatValue = 6f;
        so.FindProperty("fineStrength").floatValue = 0.5f;
        so.FindProperty("masterworkStrength").floatValue = 1f;
        so.FindProperty("masterworkShare").floatValue = 0.25f;
        so.FindProperty("catchWork").floatValue = 2f;
        so.FindProperty("castExperience").intValue = 3;
        if (so.FindProperty("levelUpSound").objectReferenceValue == null)
        {
            so.FindProperty("levelUpSound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Quest Completed.asset");
        }

        (int level, string name)[] titles =
        {
            (13, "Newcomer"), (60, "Settler"), (130, "Woodsfolk"), (260, "Haven-Keeper"), (400, "Elder of the Haven"), (600, "Legend of the Forest"),
        };
        SerializedProperty titleList = so.FindProperty("titles");
        titleList.arraySize = titles.Length;
        for (int i = 0; i < titles.Length; i++)
        {
            titleList.GetArrayElementAtIndex(i).FindPropertyRelative("totalLevel").intValue = titles[i].level;
            titleList.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = titles[i].name;
        }

        so.FindProperty("totalLevelsPerRenown").intValue = 20;
        so.FindProperty("watchShare").floatValue = 0.25f;
        so.FindProperty("watchReach").floatValue = 20f;

        SerializedProperty list = so.FindProperty("skills");
        list.arraySize = skills.Count;
        for (int i = 0; i < skills.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return "settings " + SettingsPath;
    }

    // ----- Techniques -----

    // At levels 20 and 40 the second perk of every skill teaches a new way of doing something (PerkTechnique). Which perk
    // teaches which is only this table: change it and run the builder again.
    private static readonly (string skill, int level, string id, string label, string text, PerkTechnique technique)[] TechniquePerks =
    {
        ("woodcutting", 20, "wood_steady_rhythm", "Steady Rhythm", "A sound swing keeps the run going: only a glancing blow breaks your combo.", PerkTechnique.SteadyRhythm),
        ("woodcutting", 40, "wood_heartwood_strike", "Heartwood Strike", "A critical swing bites twice as deep: it counts as two.", PerkTechnique.HeartwoodStrike),
        ("mining", 20, "mine_rich_seam", "Rich Seam", "Every critical blow on rock brings one more ore.", PerkTechnique.RichSeam),
        ("mining", 40, "mine_shatter", "Shatter", "A critical blow splits the rock: it counts as two.", PerkTechnique.Shatter),
        ("foraging", 20, "forage_sweep", "Sweep", "Picking a plant by hand gathers the same plants close by.", PerkTechnique.Sweep),
        ("foraging", 40, "forage_foragers_haste", "Forager's Haste", "Every plant picked puts a spring in your step for a while.", PerkTechnique.ForagersHaste),
        ("fishing", 20, "fish_quick_bite", "Quick Bite", "Fish bite twice as soon.", PerkTechnique.QuickBite),
        ("fishing", 40, "fish_double_catch", "Double Catch", "Now and then two fish come up on the one line.", PerkTechnique.DoubleCatch),
        ("weaponcrafting", 20, "weapon_sure_hand", "Sure Hand", "After four plain pieces in a row, the next is Fine for sure.", PerkTechnique.WeaponSureHand),
        ("weaponcrafting", 40, "weapon_runed_make", "Runed Make", "A Masterwork weapon or tool comes from the anvil already empowered once.", PerkTechnique.WeaponRunedMake),
        ("armorcrafting", 20, "armor_sure_fit", "Sure Fit", "After four plain pieces in a row, the next is Fine for sure.", PerkTechnique.ArmorSureHand),
        ("armorcrafting", 40, "armor_runed_make", "Runed Armour", "A Masterwork piece of armour comes already empowered once.", PerkTechnique.ArmorRunedMake),
        ("cooking", 20, "cook_third_plate", "Third Plate", "Room for one more meal's bonus at a time.", PerkTechnique.ThirdPlate),
        ("cooking", 40, "cook_hearty_meals", "Hearty Meals", "A meal heals a quarter of your health as well.", PerkTechnique.HeartyMeals),
        ("alchemy", 20, "alch_potent_remedies", "Potent Remedies", "Healing draughts heal a third more.", PerkTechnique.PotentRemedies),
        ("alchemy", 40, "alch_spare_dose", "Spare Dose", "A draught or tonic now and then isn't used up.", PerkTechnique.SpareDose),
        ("construction", 20, "build_patch_up", "Patch Up", "Mending what a breach broke costs half.", PerkTechnique.PatchUp),
        ("construction", 40, "build_raise_in_a_day", "Raise in a Day", "Your hammer works twice as fast.", PerkTechnique.RaiseInADay),
        ("melee", 20, "melee_riposte", "Riposte", "A parry readies a counter, as a perfect dodge does.", PerkTechnique.Riposte),
        ("melee", 40, "melee_second_wind", "Second Wind", "A kill with your weapon gives back 20 stamina.", PerkTechnique.SecondWind),
        ("archery", 20, "bow_deadly_draw", "Deadly Draw", "A perfect shot hits half again as hard.", PerkTechnique.DeadlyDraw),
        ("archery", 40, "bow_pinning_shot", "Pinning Shot", "A critical arrow roots what it hits for a moment.", PerkTechnique.PinningShot),
        ("runecraft", 20, "rune_spell_echo", "Spell Echo", "Now and then a spell leaves no cooldown behind.", PerkTechnique.SpellEcho),
        ("runecraft", 40, "rune_soul_siphon", "Soul Siphon", "A kill with a spell heals a tenth of your health.", PerkTechnique.SoulSiphon),
        ("sneaking", 20, "sneak_silent_takedown", "Silent Takedown", "A blow from the shadows finishes an ordinary creature it has nearly done for.", PerkTechnique.SilentTakedown),
        ("sneaking", 40, "sneak_shadowstep", "Shadowstep", "You sneak a third faster.", PerkTechnique.Shadowstep),
    };

    private static string TeachTechniques(List<ProficiencyDefinition> skills)
    {
        int taught = 0;
        foreach (var entry in TechniquePerks)
        {
            ProficiencyDefinition skill = skills.Find(candidate => candidate.Id == entry.skill);
            if (skill == null)
            {
                continue;
            }

            string path = $"{PerkFolder}/{entry.label.Replace(" ", "").Replace("'", "")}.asset";
            var perk = AssetDatabase.LoadAssetAtPath<PerkDefinition>(path);
            if (perk == null)
            {
                perk = ScriptableObject.CreateInstance<PerkDefinition>();
                AssetDatabase.CreateAsset(perk, path);
            }

            perk.Configure(entry.id, entry.label, entry.text, entry.technique);
            EditorUtility.SetDirty(perk);

            var so = new SerializedObject(skill);
            SerializedProperty milestones = so.FindProperty("milestones");
            for (int i = 0; i < milestones.arraySize; i++)
            {
                SerializedProperty milestone = milestones.GetArrayElementAtIndex(i);
                if (milestone.FindPropertyRelative("level").intValue == entry.level)
                {
                    milestone.FindPropertyRelative("second").objectReferenceValue = perk;
                    taught++;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
        }

        // Perks no milestone offers any more go.
        var offered = new HashSet<PerkDefinition>();
        foreach (ProficiencyDefinition skill in skills)
        {
            foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
            {
                offered.Add(milestone.first);
                offered.Add(milestone.second);
            }
        }

        int removed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:PerkDefinition", new[] { PerkFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!offered.Contains(AssetDatabase.LoadAssetAtPath<PerkDefinition>(path)) && AssetDatabase.DeleteAsset(path))
            {
                removed++;
            }
        }

        const string settingsPath = "Assets/_Project/Resources/TechniqueSettings.asset";
        var techniques = AssetDatabase.LoadAssetAtPath<TechniqueSettings>(settingsPath);
        if (techniques == null)
        {
            techniques = ScriptableObject.CreateInstance<TechniqueSettings>();
            AssetDatabase.CreateAsset(techniques, settingsPath);
        }

        var rules = new SerializedObject(techniques);
        rules.FindProperty("pinning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Roots.asset");
        rules.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(techniques);
        return $"{taught} techniques taught, {removed} old perks removed";
    }

    // ----- Mentors and rest -----

    // The Haven building that teaches each skill: every tier above I adds 5% to its experience (LevelingSettings).
    private static readonly Dictionary<string, string> MentorOf = new Dictionary<string, string>
    {
        { "woodcutting", "Sawbench" }, { "mining", "Forge" }, { "foraging", "FarmPlot" }, { "weaponcrafting", "Forge" },
        { "armorcrafting", "Loom" }, { "cooking", "Kitchen" }, { "alchemy", "Cauldron" }, { "construction", "ConstructionTable" },
        { "archery", "TanningRack" }, { "runecraft", "PurificationAltar" }, { "fishing", "Dock" },
    };

    private static string Mentors(List<ProficiencyDefinition> skills)
    {
        int set = 0;
        foreach (ProficiencyDefinition skill in skills)
        {
            BuildingDefinition building = MentorOf.TryGetValue(skill.Id, out string asset)
                ? AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{asset}.asset")
                : null;
            var so = new SerializedObject(skill);
            so.FindProperty("mentor").objectReferenceValue = building;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            set += building != null ? 1 : 0;
        }

        var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<LevelingSettings>(SettingsPath));
        settings.FindProperty("mentorBonusPerTier").floatValue = 0.05f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        return $"{set} skills with a mentor";
    }

    // Rested (setting out from a comfortable Haven) and the sauna's warmth make practice count for more.
    private static string Rested()
    {
        var haven = AssetDatabase.LoadAssetAtPath<HavenSettings>("Assets/_Project/Resources/HavenSettings.asset");
        string rested = AddBonus(new SerializedObject(haven), "restedBonuses", StatType.ExperienceGain, 0.2f) ? "rested +20% experience" : "rested had it";

        // The prefab asset's own component, edited in place: opening and saving the prefab's contents would stamp a save id
        // on its SaveableEntity, which only placed instances may have.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/Sauna.prefab");
        BoonSource source = prefab != null ? prefab.GetComponentInChildren<BoonSource>(true) : null;
        string sauna = source != null && AddBonus(new SerializedObject(source), "bonuses", StatType.ExperienceGain, 0.1f) ? "sauna +10%" : "sauna had it";
        return rested + ", " + sauna;
    }

    // The skill each job's resident gives a lesson in, once between raids. Warden and Tracker are Melee's and Sneaking's
    // teachers, who have no building to learn from; nobody teaches Fishing yet.
    private static readonly Dictionary<string, string> JobTeaches = new Dictionary<string, string>
    {
        { "Smith", "weaponcrafting" }, { "Weaver", "armorcrafting" }, { "Cook", "cooking" }, { "Mason", "construction" },
        { "Carpenter", "woodcutting" }, { "Farmer", "foraging" }, { "Hunter", "archery" }, { "Warden", "melee" },
        { "Tracker", "sneaking" }, { "RuneScholar", "runecraft" }, { "Fisher", "fishing" },
    };

    private static string Teachers(List<ProficiencyDefinition> skills)
    {
        int set = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:JobDefinition"))
        {
            var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (job == null)
            {
                continue;
            }

            ProficiencyDefinition skill = JobTeaches.TryGetValue(job.name, out string id) ? skills.Find(candidate => candidate.Id == id) : null;
            var so = new SerializedObject(job);
            so.FindProperty("teaches").objectReferenceValue = skill;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(job);
            set += skill != null ? 1 : 0;
        }

        return $"{set} jobs teach";
    }

    // A few special crafts only a practised hand can make: the tier V Trollstone work and the finest meal and draught.
    // The tier kits (copper to moonsilver) never are: raid tiers need them.
    private static string Gates(List<ProficiencyDefinition> skills)
    {
        ProficiencyDefinition Find(string id) => skills.Find(skill => skill.Id == id);
        (string recipe, string skill, int level)[] gates =
        {
            ("TrollstoneHelm", "armorcrafting", 30), ("TrollstoneHauberk", "armorcrafting", 30), ("TrollstoneGauntlets", "armorcrafting", 30),
            ("TrollstoneGirdle", "armorcrafting", 30), ("TrollstoneBoots", "armorcrafting", 30), ("TrollwallShield", "armorcrafting", 30),
            ("TrollstoneBlade", "weaponcrafting", 30), ("TrollstoneMaul", "weaponcrafting", 30), ("TrollhornBow", "weaponcrafting", 30),
            ("HavenFeast", "cooking", 25), ("RadiantDraught", "alchemy", 25),
        };
        int set = 0;
        foreach ((string recipe, string skill, int level) in gates)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>($"Assets/_Project/Data/Crafting/Recipes/{recipe}.asset");
            if (asset == null)
            {
                continue;
            }

            var so = new SerializedObject(asset);
            so.FindProperty("requiredSkill").objectReferenceValue = Find(skill);
            so.FindProperty("requiredSkillLevel").intValue = level;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            set++;
        }

        return $"{set} recipes need a skill";
    }

    private static bool AddBonus(SerializedObject so, string field, StatType stat, float value)
    {
        SerializedProperty list = so.FindProperty(field);
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue == (int)stat)
            {
                return false;
            }
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty bonus = list.GetArrayElementAtIndex(list.arraySize - 1);
        bonus.FindPropertyRelative("stat").intValue = (int)stat;
        bonus.FindPropertyRelative("op").intValue = (int)StatModifierOp.Flat;
        bonus.FindPropertyRelative("value").floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(so.targetObject);
        return true;
    }

    // ----- Helpers -----

    private static ProficiencyDefinition Skill(string id, string label, string text, string trainedBy, ProficiencyGroup group,
        ProficiencyTraining training, ToolType tool, ProficiencyDefinition.LevelBonus[] bonuses, params ProficiencyDefinition.Milestone[] milestones) =>
        Skill(id, label, text, trainedBy, group, training, tool, bonuses, milestones, CraftedGoods.None);

    private static ProficiencyDefinition Skill(string id, string label, string text, string trainedBy, ProficiencyGroup group,
        ProficiencyTraining training, ToolType tool, ProficiencyDefinition.LevelBonus[] bonuses, ProficiencyDefinition.Milestone[] milestones,
        CraftedGoods goods) => Skill(id, label, text, trainedBy, group, training, tool, bonuses, milestones, goods, false);

    private static ProficiencyDefinition Skill(string id, string label, string text, string trainedBy, ProficiencyGroup group,
        ProficiencyTraining training, ToolType tool, ProficiencyDefinition.LevelBonus[] bonuses, ProficiencyDefinition.Milestone[] milestones,
        CraftedGoods goods, bool byHand)
    {
        string path = $"{Folder}/{label.Replace(" ", "")}.asset";
        var skill = AssetDatabase.LoadAssetAtPath<ProficiencyDefinition>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<ProficiencyDefinition>();
            AssetDatabase.CreateAsset(skill, path);
        }

        skill.Configure(id, label, text, trainedBy, group, training, tool, bonuses, milestones, goods, byHand);
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
