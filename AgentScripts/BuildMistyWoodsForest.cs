using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Dresses the Misty Woods in the Mossy Cedrus Forest (most of it) and the Mountain Lake Forest Pack
/// (`unity command script run --file AgentScripts/BuildMistyWoodsForest.cs --entry BuildMistyWoodsForest.Apply`):
/// converts the chosen trees, undergrowth, rocks, logs, plants and ground textures to the project's own shaders
/// (VegetationPackConverter: Tree Wind, tessellated Mesh Surface, Grass, world terrain with displacement), then gives the
/// map definition its new forest with a habitat for every kind (RaidScatterHabitat), its four ground layers, growth and
/// plants, the map terrain its detail prototypes, the layouts' moods weights for the new trees, and the raid terrain fresh
/// displacement masks. Run from a scene with nothing unsaved. Safe to run again: converted assets are kept as they are.
/// </summary>
public static class BuildMistyWoodsForest
{
    private const string Out = "Assets/_Project/Art/Vegetation/Misty Woods";
    private const string Cedrus = "Assets/SeedMesh/Mossy_Cedrus_Forest/Environment";
    private const string Lake = "Assets/VIVID_Arts/Assets/MountainLake/Prefabs";
    private const string MapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string TerrainPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset";
    private const string RaidScene = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
    private const string TreeTemplate = "Assets/_Project/Data/Prologue/Vegetation/Spruce A Wind.prefab";
    private const string RockTemplate = "Assets/_Project/Art/Forest/Prefabs/Boulder A.prefab";
    // The gathering of the old boulder nodes (pickaxe, stone, respawn), given to painted rocks small enough to break.
    private const string StoneNode = "Assets/_Project/Prefabs/Resources/BoulderNode.prefab";
    private const string GreatStoneNode = "Assets/_Project/Prefabs/Resources/GreatBoulderNode.prefab";
    private const string GlowMushrooms = "Assets/_Project/Art/Generated/Grass/Glow Mushrooms.prefab";
    private const string LeafFallPath = "Assets/_Project/Prefabs/Effects/Gathering/LeafFall.prefab";

    private enum Make { Tree, Snag, YoungTree, Sapling, Solid, Loose, Ore, BigOre }

    private sealed class Entry
    {
        public string Source;
        public string Name;
        public Make Make;
        public float Weight;
        public Vector2 Scale;
        public int FromTier;
        public RaidScatterHabitat Habitat;
    }

    // slope (degrees), forest density, wet, edge, near trees, groves, room (m)
    private static RaidScatterHabitat H(float slopeMin, float slopeMax, float forestMin, float forestMax, float wet, float edge, float near, float groves, float room) =>
        new RaidScatterHabitat { slope = new Vector2(slopeMin, slopeMax), forest = new Vector2(forestMin, forestMax), wet = wet, edge = edge, nearTrees = near, groves = groves, room = room };

    private static Entry E(string source, string name, Make make, float weight, float min, float max, RaidScatterHabitat habitat, int fromTier = 0) =>
        new Entry { Source = source, Name = name, Make = make, Weight = weight, Scale = new Vector2(min, max), Habitat = habitat, FromTier = fromTier };

    // The canopy. Giants keep to the deep, damp forest; spires and slender cedars take drier, steeper ground; the young
    // crowd the edges. Order matters: the layouts' moods weigh trees by position (see Moods).
    private static readonly Entry[] Trees =
    {
        E("Cedrus_tree_var1", "Cedar Giant A", Make.Tree, 1.2f, 0.7f, 0.95f, H(0, 22, 0.55f, 1, 0.2f, -0.6f, 0, 0.4f, 3f)),
        E("Cedrus_tree_var6", "Cedar Giant B", Make.Tree, 1.2f, 0.7f, 0.95f, H(0, 22, 0.55f, 1, 0.2f, -0.6f, 0, 0.4f, 3f)),
        E("Cedrus_tree_var3", "Cedar Tall A", Make.Tree, 2f, 0.8f, 1.05f, H(0, 28, 0.4f, 1, 0, -0.3f, 0, 0.5f, 2.4f)),
        E("Cedrus_tree_var4", "Cedar Tall B", Make.Tree, 2f, 0.8f, 1.05f, H(0, 30, 0.35f, 1, -0.3f, -0.2f, 0, 0.5f, 2.3f)),
        E("Cedrus_tree_var10", "Cedar Spire", Make.Tree, 1.5f, 0.85f, 1.1f, H(0, 34, 0.2f, 1, -0.5f, 0, 0, 0.6f, 1.8f)),
        E("Cedrus_tree_var2", "Cedar A", Make.Tree, 2.5f, 0.85f, 1.2f, H(0, 32, 0.2f, 1, 0, 0.2f, 0, 0.3f, 1.8f)),
        E("Cedrus_tree_var5", "Cedar B", Make.Tree, 2.5f, 0.85f, 1.2f, H(0, 32, 0.2f, 1, 0, 0.2f, 0, 0.3f, 1.8f)),
        E("Cedrus_tree_var11", "Cedar Slender", Make.Tree, 2f, 0.85f, 1.2f, H(0, 34, 0.15f, 1, -0.3f, 0.2f, 0, 0.4f, 1.4f)),
        E("Cedrus_tree_var15", "Cedar C", Make.Tree, 2f, 0.85f, 1.2f, H(0, 30, 0.2f, 1, 0.2f, 0, 0, 0.3f, 1.9f)),
        E("Cedrus_tree_var16 Variant", "Cedar D", Make.Tree, 2f, 0.85f, 1.2f, H(0, 30, 0.2f, 1, 0.2f, 0, 0, 0.3f, 1.9f)),
        E("Cedrus_tree_var7", "Young Cedar", Make.Tree, 1.5f, 0.85f, 1.2f, H(0, 34, 0, 0.85f, 0, 0.7f, 0, 0.2f, 1.2f)),
        E("Cedrus_tree_var10", "Dead Cedar", Make.Snag, 0.25f, 0.75f, 1.05f, H(0, 40, 0, 1, 0.5f, 0, 0, 0.5f, 1.5f), fromTier: 2),
    };

    // Saplings take the gaps and edges, shrubs the damp ground, and the floor's clutter lies by the trunks it fell from.
    private static readonly Entry[] Undergrowth =
    {
        E("Cedrus_tree_var8", "Cedar Sapling", Make.YoungTree, 2f, 0.8f, 1.2f, H(0, 35, 0, 1, 0, 0.6f, -0.5f, 0.3f, 0.8f)),
        E("Cedrus_tree_var9", "Cedar Seedling", Make.Sapling, 2f, 0.8f, 1.3f, H(0, 35, 0, 1, 0, 0.3f, -0.3f, 0.5f, 0.3f)),
        E("Cedrus_tree_var14", "Young Cedar Small", Make.YoungTree, 1.2f, 0.8f, 1.1f, H(0, 32, 0, 0.9f, 0, 0.6f, -0.4f, 0.3f, 1f)),
        E("Cedrus_tree_var17 Variant", "Young Cedar Bushy", Make.YoungTree, 1.2f, 0.8f, 1.1f, H(0, 32, 0, 0.9f, 0, 0.6f, -0.4f, 0.3f, 1f)),
        E(Lake + "/MLRT_SugarMaple_Sapling_04", "Maple Sapling A", Make.YoungTree, 0.5f, 0.6f, 0.9f, H(0, 25, 0, 0.8f, 0.6f, 0.8f, -0.3f, 0.4f, 1.2f)),
        E(Lake + "/MLRT_SugarMaple_Sapling_01", "Maple Sapling B", Make.YoungTree, 0.4f, 0.55f, 0.8f, H(0, 25, 0, 0.8f, 0.6f, 0.8f, -0.3f, 0.4f, 1.2f)),
        E("Oreopanax_Xalapensis_var4 Variant", "Broadleaf Shrub", Make.Sapling, 0.8f, 0.8f, 1.2f, H(0, 25, 0, 1, 0.5f, 0.4f, 0, 0.5f, 0.6f)),
        E("Roldana_plant_var8", "Forest Shrub A", Make.Sapling, 0.8f, 0.8f, 1.2f, H(0, 25, 0, 1, 0.6f, 0.3f, 0, 0.6f, 0.6f)),
        E("Roldana_plant_var11", "Forest Shrub B", Make.Sapling, 0.5f, 0.65f, 0.95f, H(0, 25, 0, 1, 0.6f, 0.3f, 0, 0.6f, 0.7f)),
        E("Rotten_cedrus_branchGroup_var1", "Branch Pile", Make.Loose, 1.2f, 0.8f, 1.2f, H(0, 30, 0.2f, 1, 0, 0, 0.7f, 0, 0)),
        E("Short_rotten_branchGroup_var1", "Twig Pile", Make.Loose, 1.2f, 0.8f, 1.3f, H(0, 30, 0.1f, 1, 0, 0, 0.6f, 0, 0)),
        E("Lichen_branch_Debris_2 Variant", "Lichen Branch", Make.Loose, 0.8f, 0.8f, 1.2f, H(0, 30, 0.2f, 1, 0.3f, 0, 0.5f, 0, 0)),
        E("Mossy_rock_var8", "Mossy Stones", Make.Loose, 0.4f, 0.8f, 1.6f, H(0, 40, 0, 1, 0, 0, 0, 0.4f, 0)),
        E(Lake + "/MT_Rock_Small_Composite_03", "Scree", Make.Loose, 0.25f, 0.6f, 1f, H(8, 45, 0, 1, -0.3f, 0, 0, 0.5f, 0)),
        E("Mossy_rock_var5", "Pebbles A", Make.Loose, 0.6f, 0.9f, 1.8f, H(0, 45, 0, 1, 0, 0, 0, 0.6f, 0)),
        E("Mossy_rock_var7", "Pebbles B", Make.Loose, 0.6f, 0.9f, 1.8f, H(0, 45, 0, 1, 0, 0, 0, 0.6f, 0)),
        E(Lake + "/MT_Rock_Small_02", "Small Stone", Make.Loose, 0.5f, 0.8f, 1.6f, H(6, 50, 0, 1, -0.3f, 0, 0, 0.5f, 0)),
        E("Mossy_log_var10", "Mossy Branch Log", Make.Loose, 0.8f, 0.85f, 1.2f, H(0, 25, 0.3f, 1, 0.3f, 0, 0.6f, 0.2f, 0)),
        E("Rotten_cedrus_branch_var3", "Rotten Branch", Make.Loose, 1f, 0.85f, 1.2f, H(0, 30, 0.2f, 1, 0, 0, 0.7f, 0, 0)),
        E("Short_rotten_branch_var1", "Short Branches", Make.Loose, 1f, 0.85f, 1.3f, H(0, 30, 0.2f, 1, 0, 0, 0.6f, 0, 0)),
        E("Forest_ground_scan_var2", "Forest Floor Patch C", Make.Loose, 0.7f, 0.8f, 1.2f, H(0, 25, 0.3f, 1, -0.3f, 0, 0.3f, 0.3f, 0)),
        E("Forest_ground_scan_var1", "Forest Floor Patch A", Make.Loose, 0.8f, 0.8f, 1.2f, H(0, 25, 0.3f, 1, -0.3f, 0, 0.3f, 0.3f, 0)),
        E("Forest_ground_scan_var3", "Forest Floor Patch B", Make.Loose, 0.8f, 0.8f, 1.2f, H(0, 25, 0.3f, 1, -0.3f, 0, 0.3f, 0.3f, 0)),
    };

    // Boulders gather under slopes; moss-covered rocks in the damp forest; logs and stumps by the trunks.
    private static readonly Entry[] Rocks =
    {
        E(Lake + "/MT_Rock_Large_02", "Cliff Boulder A", Make.Solid, 1.5f, 0.3f, 0.5f, H(14, 60, 0, 1, 0, 0, 0, 0.5f, 2f)),
        E(Lake + "/MT_Rock_Med_05", "Cliff Boulder B", Make.Solid, 1.5f, 0.4f, 0.7f, H(14, 60, 0, 1, 0, 0, 0, 0.5f, 1.8f)),
        E(Lake + "/MT_Rock_Med_02", "Cliff Boulder C", Make.Solid, 1.2f, 0.4f, 0.75f, H(12, 60, 0, 1, 0, 0, 0, 0.5f, 1.6f)),
        E(Lake + "/MT_Rock_Large_01", "Cliff Slab", Make.Solid, 0.8f, 0.25f, 0.4f, H(10, 45, 0, 1, 0, 0, 0, 0.4f, 2f)),
        E("Mossy_Rock_var4", "Mossy Boulder A", Make.BigOre, 1.5f, 0.9f, 1.6f, H(0, 30, 0.2f, 1, 0.3f, 0, 0, 0.4f, 0.8f)),
        E("Mossy_Rock_var3", "Mossy Boulder B", Make.BigOre, 1.5f, 0.9f, 1.6f, H(0, 30, 0.2f, 1, 0.3f, 0, 0, 0.4f, 0.8f)),
        E("Mossy_rock_var1", "Mossy Rock", Make.Ore, 1.2f, 0.9f, 1.5f, H(0, 35, 0, 1, 0.2f, 0, 0, 0.3f, 0.7f)),
        E("Mossy_rock_var2", "Mossy Rock Small", Make.Ore, 0.9f, 1f, 1.8f, H(0, 35, 0, 1, 0.2f, 0, 0, 0.5f, 0.6f)),
        E("Volcanic_Mossy_Rocks", "Mossy Rock Bed", Make.Loose, 1f, 0.9f, 1.4f, H(0, 25, 0.2f, 1, 0.5f, 0, 0, 0.4f, 0.8f)),
        E(Lake + "/MLRT_GroundRock_01_Var1", "Ground Slab", Make.Loose, 0.6f, 0.5f, 0.8f, H(4, 30, 0, 1, -0.3f, 0, 0, 0.4f, 1f)),
        E(Lake + "/MT_Rock_Med_04", "Grey Rock", Make.Ore, 1f, 0.35f, 0.55f, H(4, 45, 0, 1, -0.3f, 0, 0, 0.5f, 0.8f)),
        E(Lake + "/MT_Rock_Med_03", "Grey Boulder", Make.BigOre, 0.8f, 0.35f, 0.55f, H(8, 50, 0, 1, -0.3f, 0, 0, 0.5f, 1.2f)),
        E(Lake + "/MLRT_Rock_Composite_005", "Rock Outcrop A", Make.Solid, 0.5f, 0.6f, 0.9f, H(16, 60, 0, 1, 0, 0, 0, 0.6f, 3f)),
        E(Lake + "/MLRT_Rock_Composite_007", "Rock Outcrop B", Make.Solid, 0.5f, 0.5f, 0.8f, H(16, 60, 0, 1, 0, 0, 0, 0.6f, 3f)),
        E("Cedrus_tree_var12", "Fallen Cedar A", Make.Solid, 0.6f, 0.85f, 1.1f, H(0, 14, 0.4f, 1, 0.3f, -0.3f, 0.5f, 0.2f, 2.5f)),
        E("Cedrus_tree_var13", "Fallen Cedar B", Make.Solid, 0.6f, 0.85f, 1.1f, H(0, 14, 0.4f, 1, 0.3f, -0.3f, 0.5f, 0.2f, 2.5f)),
        E("Mossy_Rotten_log_var1", "Mossy Rotten Log", Make.Solid, 0.8f, 0.85f, 1.15f, H(0, 18, 0.3f, 1, 0.5f, 0, 0.4f, 0.2f, 0.8f)),
        E("Tree_stump_var4", "Stump D", Make.Solid, 0.8f, 0.8f, 1.15f, H(0, 30, 0.2f, 1, 0, 0, 0.7f, 0, 0.6f)),
        E("Merged_Stump_var1", "Fallen Trunk", Make.Solid, 0.8f, 0.85f, 1.1f, H(0, 15, 0.3f, 1, 0.2f, 0, 0.4f, 0.3f, 1f)),
        E("Mossy_rotten_log_var8", "Rotten Log A", Make.Solid, 1f, 0.85f, 1.15f, H(0, 18, 0.3f, 1, 0.3f, 0, 0.4f, 0.2f, 0.8f)),
        E("Mossy_rotten_log_var7", "Rotten Log B", Make.Solid, 1f, 0.85f, 1.15f, H(0, 18, 0.3f, 1, 0.3f, 0, 0.4f, 0.2f, 0.8f)),
        E("Rotten_log_var5", "Rotten Log C", Make.Solid, 1f, 0.85f, 1.15f, H(0, 20, 0.2f, 1, 0.2f, 0, 0.4f, 0.2f, 0.7f)),
        E("Large_RottenCedrus_log_1 Variant", "Rotten Log D", Make.Solid, 0.8f, 0.9f, 1.2f, H(0, 20, 0.2f, 1, 0.2f, 0, 0.4f, 0.2f, 0.7f)),
        E("Mossy_Rotten_log_var2", "Mossy Log", Make.Solid, 1f, 0.85f, 1.15f, H(0, 18, 0.3f, 1, 0.5f, 0, 0.4f, 0.2f, 0.8f)),
        E("Tree_stump_var2", "Stump A", Make.Solid, 1f, 0.85f, 1.2f, H(0, 30, 0.2f, 1, 0, 0, 0.7f, 0, 0.6f)),
        E("Tree_stump_var3", "Stump B", Make.Solid, 1f, 0.8f, 1.1f, H(0, 30, 0.2f, 1, 0, 0, 0.7f, 0, 0.7f)),
        E("Tree_stump_var5", "Stump C", Make.Solid, 0.8f, 0.8f, 1.1f, H(0, 30, 0.2f, 1, 0, 0, 0.7f, 0, 0.7f)),
        E("Cedrus_stump_1", "Snag Stump", Make.Solid, 0.6f, 0.9f, 1.3f, H(0, 30, 0.2f, 1, 0.3f, 0, 0.5f, 0, 0.5f)),
    };

    // Terrain details. Short Grass, Tall Grass and Bush are the names TerrainGrowth fills a ground with; the rest are plants.
    private static readonly (string source, string name, VegetationPackConverter.DetailLook look)[] Details =
    {
        ("Moss_cover_var1", "Short Grass", VegetationPackConverter.DetailLook.Low),
        ("Wild_forest_grass_var1", "Tall Grass", VegetationPackConverter.DetailLook.Grass),
        ("Roldana_plant_var6", "Bush", VegetationPackConverter.DetailLook.Plant),
        ("Common_fern_var1", "Common Fern", VegetationPackConverter.DetailLook.Plant),
        ("Eagle_fern_var3", "Eagle Fern", VegetationPackConverter.DetailLook.Plant),
        ("Middle_WildFern_var1 Variant", "Wild Fern", VegetationPackConverter.DetailLook.Plant),
        ("Cretan_brake_fern_var4", "Brake Fern", VegetationPackConverter.DetailLook.Plant),
        (Lake + "/MLRT_Fern_01", "Lake Fern", VegetationPackConverter.DetailLook.Plant),
        ("Wild_fern_Var3", "Small Fern", VegetationPackConverter.DetailLook.Plant),
        ("ForestClover_var2", "Forest Clover", VegetationPackConverter.DetailLook.Low),
        ("Ground_weed_var2", "Ground Weed", VegetationPackConverter.DetailLook.Plant),
        ("Wild_forest_grass_var3", "Forest Grass", VegetationPackConverter.DetailLook.Grass),
        (Lake + "/MLRT_Grass_01_Var2", "Lake Grass", VegetationPackConverter.DetailLook.Grass),
        ("Ground_flower_weed_var1 Variant", "Wild Flowers", VegetationPackConverter.DetailLook.Plant),
        ("Moss_cover_var2", "Moss Cushion", VegetationPackConverter.DetailLook.Low),
        ("Ground_Leaves_var1", "Leaf Litter", VegetationPackConverter.DetailLook.Low),
        ("Red_Couple_VMushroom", "Forest Mushrooms", VegetationPackConverter.DetailLook.Low),
        ("Ground_flower_weed_var3 Variant", "Glow Flowers", Glowing(VegetationPackConverter.DetailLook.Plant)),
        ("Dry_cedrus_debris_Var1", "Needle Debris A", VegetationPackConverter.DetailLook.Flat),
        ("Dry_cedrus_debris_Var5", "Needle Debris B", VegetationPackConverter.DetailLook.Flat),
        ("Dry_cedrus_debris_Var8 Variant", "Needle Debris C", VegetationPackConverter.DetailLook.Flat),
        ("Generic_dry_leaves_var1", "Dry Leaves", VegetationPackConverter.DetailLook.Flat),
        ("Common_fern_var2", "Common Fern B", VegetationPackConverter.DetailLook.Plant),
        ("Eagle_fern_var1", "Eagle Fern B", VegetationPackConverter.DetailLook.Plant),
        ("Middle_WildFern_var3 Variant", "Wild Fern B", VegetationPackConverter.DetailLook.Plant),
        ("ForestClover_var1", "Forest Clover B", VegetationPackConverter.DetailLook.Low),
        ("Moss_cover_var3", "Moss Cushion B", VegetationPackConverter.DetailLook.Low),
    };

    // How each plant spreads: patch size, rarity, thickness, grass cleared round it, love of wet ground.
    private static readonly TerrainGrowth.Plant[] Plants =
    {
        new TerrainGrowth.Plant("Common Fern", 9f, 0.6f, 0.55f, 1, 0.3f),
        new TerrainGrowth.Plant("Eagle Fern", 8f, 0.62f, 0.5f, 1, 0.2f),
        new TerrainGrowth.Plant("Wild Fern", 7f, 0.64f, 0.5f, 1, 0.4f),
        new TerrainGrowth.Plant("Brake Fern", 6f, 0.68f, 0.45f, 1, 0.6f),
        new TerrainGrowth.Plant("Lake Fern", 7f, 0.7f, 0.4f, 1, 0.5f),
        new TerrainGrowth.Plant("Small Fern", 5f, 0.66f, 0.5f, 0, 0.3f),
        new TerrainGrowth.Plant("Forest Clover", 5f, 0.66f, 0.6f, 0, 0.2f),
        new TerrainGrowth.Plant("Ground Weed", 6f, 0.7f, 0.4f, 0),
        new TerrainGrowth.Plant("Forest Grass", 8f, 0.62f, 0.5f, 0),
        new TerrainGrowth.Plant("Lake Grass", 8f, 0.68f, 0.45f, 0, 0.2f),
        new TerrainGrowth.Plant("Wild Flowers", 5f, 0.74f, 0.3f, 0),
        new TerrainGrowth.Plant("Moss Cushion", 6f, 0.6f, 0.6f, 0, 0.8f),
        new TerrainGrowth.Plant("Leaf Litter", 8f, 0.64f, 0.4f, 0),
        new TerrainGrowth.Plant("Forest Mushrooms", 5f, 0.76f, 0.2f, 0, 0.6f),
        new TerrainGrowth.Plant("Glow Mushrooms", 5f, 0.74f, 0.22f, 0, 1f),
        new TerrainGrowth.Plant("Glow Flowers", 6f, 0.72f, 0.2f, 0, 0.3f),
        // The floor under the cedars: needles and twigs everywhere, thickest where nothing else grows.
        new TerrainGrowth.Plant("Needle Debris A", 4f, 0.5f, 0.55f, 0),
        new TerrainGrowth.Plant("Needle Debris B", 5f, 0.52f, 0.5f, 0),
        new TerrainGrowth.Plant("Needle Debris C", 7f, 0.58f, 0.45f, 0),
        new TerrainGrowth.Plant("Dry Leaves", 6f, 0.6f, 0.4f, 0),
        new TerrainGrowth.Plant("Common Fern B", 9f, 0.6f, 0.55f, 1, 0.3f),
        new TerrainGrowth.Plant("Eagle Fern B", 8f, 0.62f, 0.5f, 1, 0.2f),
        new TerrainGrowth.Plant("Wild Fern B", 7f, 0.64f, 0.5f, 1, 0.4f),
        new TerrainGrowth.Plant("Forest Clover B", 5f, 0.66f, 0.6f, 0, 0.2f),
        new TerrainGrowth.Plant("Moss Cushion B", 6f, 0.6f, 0.6f, 0, 0.8f),
    };

    private static VegetationPackConverter.DetailLook Glowing(VegetationPackConverter.DetailLook look)
    {
        look.glow = true;
        return look;
    }

    public static string Apply()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "an open scene has unsaved changes";
        }

        var log = new List<string>();
        needleFall = NeedleFall();
        var treeTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TreeTemplate);
        var rockTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(RockTemplate);
        List<RaidMapDefinition.Scatter> trees = Convert(Trees, treeTemplate, rockTemplate);
        List<RaidMapDefinition.Scatter> undergrowth = Convert(Undergrowth, treeTemplate, rockTemplate);
        List<RaidMapDefinition.Scatter> rocks = Convert(Rocks, treeTemplate, rockTemplate);
        var details = Details.Select(d => VegetationPackConverter.Detail(Source(d.source), Out + "/Details", d.name, d.look)).ToList();
        // The blue glowing mushrooms of the old forest stay: the Misty Woods are known for them at night.
        details.Add(AssetDatabase.LoadAssetAtPath<GameObject>(GlowMushrooms));
        TerrainLayer forest = VegetationPackConverter.Ground(Cedrus + "/Global_textures/Moss/Moss_surface.terrainlayer", Out + "/Ground", "Cedar Moss");
        TerrainLayer soil = VegetationPackConverter.Ground(Cedrus + "/Terrain_Textures/Dirt/Cedrus_Dirt.terrainlayer", Out + "/Ground", "Cedar Soil");
        TerrainLayer rock = VegetationPackConverter.Ground(Cedrus + "/Terrain_Textures/Mossy_Ground_rocks/Mossy_Ground_rocks.terrainlayer", Out + "/Ground", "Cedar Rock");
        TerrainLayer litter = VegetationPackConverter.Ground(Cedrus + "/Terrain_Textures/Dense_Fallen_leaves_1/Dense_fallen_leaves.terrainlayer", Out + "/Ground", "Cedar Litter");
        AssetDatabase.SaveAssets();
        log.Add($"converted {trees.Count} trees, {undergrowth.Count} undergrowth, {rocks.Count} rocks and logs, {details.Count} details, 4 grounds");

        var map = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(MapPath);
        log.Add(Definition(map, trees, undergrowth, rocks, forest, soil, rock, litter));
        log.Add(MapTerrain(details));
        log.Add(Moods());
        AssetDatabase.SaveAssets();
        log.Add(Displacement(map));
        log.Add(SetPieces(rocks, trees));
        log.Add(BaseTerrain(trees, rocks));
        log.Add(Atmosphere());
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    /// <summary>Remakes every converted asset in place (same GUIDs), for when the converter changed. Same rules as Apply.</summary>
    public static string Rebuild()
    {
        VegetationPackConverter.Rebuild = true;
        try
        {
            return Apply();
        }
        finally
        {
            VegetationPackConverter.Rebuild = false;
        }
    }

    private static GameObject needleFall;

    private const string Effects = Cedrus + "/Effects";
    private const string ForestWindClip = Effects + "/Sounds/Book of the Dead/Forest_Background_wind_1.wav";
    private const string FlySoundPath = "Assets/_Project/Audio/Sounds/Library/Forest Fly.asset";

    // What makes the raid sound and move like the pack's forest, in the raid scene: needles and twigs shed by the
    // crowns (CanopyDebris), the recorded forest wind as ambience, and flies buzzing past (AmbientOneShots).
    private static string Atmosphere()
    {
        GameObject debris = CanopyDebrisPrefab();
        SoundDefinition flies = FlySound();
        var wind = AssetDatabase.LoadAssetAtPath<AudioClip>(ForestWindClip);
        if (AssetImporter.GetAtPath(ForestWindClip) is AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        Scene raid = EditorSceneManager.OpenScene(RaidScene, OpenSceneMode.Additive);
        try
        {
            foreach (string old in new[] { "Canopy Debris", "Forest Flies" })
            {
                GameObject found = raid.GetRootGameObjects().FirstOrDefault(root => root.name == old);
                if (found != null) Object.DestroyImmediate(found);
            }

            var placed = (GameObject)PrefabUtility.InstantiatePrefab(debris, raid);
            placed.name = "Canopy Debris";
            var buzz = new GameObject("Forest Flies");
            SceneManager.MoveGameObjectToScene(buzz, raid);
            buzz.AddComponent<AmbientOneShots>().Configure(flies, new Vector2(14f, 40f), new Vector2(1.5f, 6f), 0.1f);

            SceneAudio audio = raid.GetRootGameObjects().Select(root => root.GetComponentInChildren<SceneAudio>(true)).First(a => a != null);
            var so = new SerializedObject(audio);
            SerializedProperty tracks = so.FindProperty("ambience.tracks");
            tracks.arraySize = 1;
            tracks.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = wind;
            tracks.GetArrayElementAtIndex(0).FindPropertyRelative("volume").floatValue = 0.55f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(raid);
            EditorSceneManager.SaveScene(raid);
        }
        finally
        {
            EditorSceneManager.CloseScene(raid, true);
        }

        return "raid scene: canopy debris, forest wind ambience, flies";
    }

    // The pack's falling debris meshes on a URP particle material, as a prefab CanopyDebris drives.
    private static GameObject CanopyDebrisPrefab()
    {
        string folder = Out + "/Effects";
        string path = folder + "/Canopy Debris.prefab";
        VegetationPackConverter.EnsureFolder(folder);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Effects + "/Falling_Tree_debris/Prefabs/Falling_debris_particles.prefab");
        var sourceRenderer = source.GetComponent<ParticleSystemRenderer>();
        var meshes = new Mesh[sourceRenderer.meshCount];
        sourceRenderer.GetMeshes(meshes);
        meshes = meshes.Where(m => m != null).ToArray();

        string materialPath = folder + "/Canopy Debris.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture texture = BaseOf(sourceRenderer.sharedMaterial);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, 1f));
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.3f);
        material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        float meshSize = meshes.Length > 0 ? meshes.Average(m => m.bounds.size.magnitude) : 1f;
        var root = new GameObject("Canopy Debris");
        try
        {
            var system = root.AddComponent<ParticleSystem>();
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(meshes);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.alignment = ParticleSystemRenderSpace.World;
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = 0f;
            // Twigs and sprigs a hand to a forearm long, whatever size the pack's meshes were made at.
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f / meshSize, 0.45f / meshSize);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.06f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.72f, 0.62f), Color.white);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.RotationOverLifetimeModule tumble = system.rotationOverLifetime;
            tumble.enabled = true;
            tumble.separateAxes = true;
            tumble.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            tumble.y = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            tumble.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            ParticleSystem.NoiseModule flutter = system.noise;
            flutter.enabled = true;
            flutter.strength = 0.35f;
            flutter.frequency = 0.35f;
            flutter.scrollSpeed = 0.25f;
            flutter.quality = ParticleSystemNoiseQuality.Low;
            ParticleSystem.SizeOverLifetimeModule fade = system.sizeOverLifetime;
            fade.enabled = true;
            fade.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.06f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            root.AddComponent<CanopyDebris>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Texture BaseOf(Material material)
    {
        SerializedProperty textures = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        foreach (string name in new[] { "_MainTex", "_BaseColorMap", "_BaseMap" })
        {
            for (int i = 0; i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == name && entry.FindPropertyRelative("second.m_Texture").objectReferenceValue is Texture found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    // A fly buzzing past: the bird chirp's sound settings (background mood, positional), with the pack's two fly clips.
    private static SoundDefinition FlySound()
    {
        if (AssetDatabase.LoadAssetAtPath<SoundDefinition>(FlySoundPath) == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Audio/Sounds/Library/Bird Chirp.asset", FlySoundPath);
        }

        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(FlySoundPath);
        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = "Forest Fly";
        so.FindProperty("caption").stringValue = "";
        SerializedProperty clips = so.FindProperty("clips");
        string[] files = { Effects + "/Sounds/Fly/Fly_var1.wav", Effects + "/Sounds/Fly/Fly_var2.wav" };
        clips.arraySize = files.Length;
        for (int i = 0; i < files.Length; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(files[i]);
        so.FindProperty("volume").floatValue = 0.35f;
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = 1.5f;
        so.FindProperty("audibleDistance").floatValue = 9f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    // Cedars drop needle sprigs; the rest (maples, shrubs) the standard broad leaves, each in its own colour.
    private static GameObject Fall(string source) => source.Contains("Cedrus") ? needleFall : null;

    // The standard leaf fall with small needle sprigs instead of broad leaves, tinted per tree like it.
    private static GameObject NeedleFall()
    {
        string folder = Out + "/Effects";
        string path = folder + "/Needle Fall.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        VegetationPackConverter.EnsureFolder(folder);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(LeafFallPath);
        ParticleSystem.TextureSheetAnimationModule sheet = source.GetComponent<ParticleSystem>().textureSheetAnimation;
        int columns = Mathf.Max(1, sheet.numTilesX), rows = Mathf.Max(1, sheet.numTilesY);
        string texturePath = folder + "/Falling Needles.png";
        File.WriteAllBytes(texturePath, NeedleSprigs(256, columns, rows).EncodeToPNG());
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        var material = new Material(source.GetComponent<ParticleSystemRenderer>().sharedMaterial) { name = "Falling Needles" };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        AssetDatabase.CreateAsset(material, folder + "/Falling Needles.mat");

        AssetDatabase.CopyAsset(LeafFallPath, path);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            contents.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ParticleSystem.MainModule main = contents.GetComponent<ParticleSystem>().main;
            // Sprigs a hand long, not leaves the size of a plate.
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // White needle sprigs on transparent, one per tile of the sheet: a twig with short needles along both sides.
    private static Texture2D NeedleSprigs(int size, int columns, int rows)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        int cellX = size / columns, cellY = size / rows;
        var random = new System.Random(7);
        for (int cell = 0; cell < columns * rows; cell++)
        {
            var origin = new Vector2((cell % columns) * cellX, (cell / columns) * cellY);
            var segments = new List<(Vector2 a, Vector2 b, float width)>();
            float tilt = (float)(random.NextDouble() - 0.5) * 0.9f;
            Vector2 up = new Vector2(Mathf.Sin(tilt), Mathf.Cos(tilt));
            Vector2 side = new Vector2(up.y, -up.x);
            Vector2 start = origin + new Vector2(cellX * 0.5f, cellY * 0.08f) - up * 0f;
            float length = cellY * 0.84f;
            segments.Add((start, start + up * length, cellX * 0.018f));
            int needles = 13 + cell * 2;
            for (int n = 1; n <= needles; n++)
            {
                float along = n / (needles + 1f);
                Vector2 at = start + up * (length * along);
                float reach = cellX * 0.17f * (1f - along * 0.55f);
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector2 direction = (side * sign * 0.75f + up * 0.66f).normalized;
                    segments.Add((at, at + direction * reach, cellX * 0.011f));
                }
            }

            for (int y = (int)origin.y; y < origin.y + cellY; y++)
            {
                for (int x = (int)origin.x; x < origin.x + cellX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float alpha = 0f;
                    foreach ((Vector2 a, Vector2 b, float width) in segments)
                    {
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                        float distance = Vector2.Distance(p, a + ab * t);
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(width + 0.75f - distance));
                    }

                    float shade = 0.82f + 0.18f * (y - origin.y) / cellY;
                    pixels[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    // The set pieces were dressed from the old forest kit: their boulders, logs and dead trees become the new ones,
    // each scaled to cover the same ground. Buildings, banners and story props stay as they are.
    private static string SetPieces(List<RaidMapDefinition.Scatter> rocks, List<RaidMapDefinition.Scatter> trees)
    {
        GameObject Named(List<RaidMapDefinition.Scatter> list, string name) => list.First(s => s.prefab.name == name).prefab;
        var swaps = new Dictionary<string, GameObject>
        {
            { "Assets/_Project/Art/Forest/Prefabs/Boulder A.prefab", Named(rocks, "Mossy Boulder A") },
            { "Assets/_Project/Art/Forest/Prefabs/Boulder B.prefab", Named(rocks, "Mossy Boulder B") },
            { "Assets/_Project/Art/Forest/Prefabs/Fallen Log.prefab", Named(rocks, "Rotten Log A") },
            { "Assets/_Project/Art/Forest/Prefabs/Dead Spruce.prefab", Named(trees, "Dead Cedar") },
        };
        int swapped = 0, pieces = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RaidSetPieceDefinition"))
        {
            var definition = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var prefabAsset = new SerializedObject(definition).FindProperty("prefab")?.objectReferenceValue as GameObject;
            if (prefabAsset == null) continue;
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            int here = 0;
            try
            {
                foreach (Transform node in contents.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (node == null) continue;
                    // A missing prefab here is a Dead Cedar from before the trees were remade under new GUIDs.
                    bool missing = PrefabUtility.IsPrefabAssetMissing(node.gameObject) && PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject);
                    if (!missing && !PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject)) continue;
                    string from = missing ? "Assets/_Project/Art/Forest/Prefabs/Dead Spruce.prefab"
                        : AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject));
                    if (!swaps.TryGetValue(from, out GameObject replacement)) continue;

                    float oldSize = missing ? 0f : Footprint(node.gameObject);
                    var made = (GameObject)PrefabUtility.InstantiatePrefab(replacement, node.parent);
                    made.name = node.name;
                    made.transform.SetPositionAndRotation(node.position, node.rotation);
                    made.transform.SetSiblingIndex(node.GetSiblingIndex());
                    made.transform.localScale = Vector3.one;
                    float newSize = Footprint(made);
                    bool tree = replacement.GetComponent<ChoppableTree>() != null;
                    float scale = tree ? node.lossyScale.y : (newSize > 0.01f ? oldSize / newSize : 1f);
                    made.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.3f, 4f);
                    Object.DestroyImmediate(node.gameObject);
                    here++;
                }

                if (here > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    pieces++;
                    swapped += here;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        return $"set pieces: {swapped} old boulders, logs and dead trees replaced in {pieces} places";
    }

    // The larger side of what is drawn, at the object's current scale.
    private static float Footprint(GameObject target)
    {
        Renderer[] drawn = target.GetComponentsInChildren<Renderer>(true);
        if (drawn.Length == 0) return 0f;
        Bounds bounds = drawn[0].bounds;
        foreach (Renderer part in drawn) bounds.Encapsulate(part.bounds);
        return Mathf.Max(bounds.size.x, bounds.size.z);
    }

    // The painted map (played when a raid has no layout rules, and the start of every generated map) still named the
    // old kit: each of its tree kinds becomes a new one in place, so the painted forest keeps its shape.
    private static string BaseTerrain(List<RaidMapDefinition.Scatter> trees, List<RaidMapDefinition.Scatter> rocks)
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        GameObject Named(List<RaidMapDefinition.Scatter> list, string name) => list.First(s => s.prefab.name == name).prefab;
        var swaps = new Dictionary<string, GameObject>
        {
            { "Spruce A", Named(trees, "Cedar A") }, { "Spruce B", Named(trees, "Cedar B") }, { "Spruce C", Named(trees, "Cedar Tall A") },
            { "Pine", Named(trees, "Cedar Slender") }, { "Dead Spruce", Named(trees, "Dead Cedar") },
            { "Boulder A", Named(rocks, "Mossy Boulder A") }, { "Boulder B", Named(rocks, "Mossy Boulder B") }, { "Fallen Log", Named(rocks, "Rotten Log A") },
        };
        TreePrototype[] prototypes = data.treePrototypes;
        // The painted map's eight kinds, in their order (the prologue's copy relies on it): repairs slots left empty too.
        GameObject[] byIndex =
        {
            Named(trees, "Cedar A"), Named(trees, "Cedar B"), Named(trees, "Cedar Tall A"), Named(trees, "Cedar Slender"), Named(trees, "Dead Cedar"),
            Named(rocks, "Mossy Boulder A"), Named(rocks, "Mossy Boulder B"), Named(rocks, "Rotten Log A"),
        };
        int changed = 0;
        for (int i = 0; i < prototypes.Length; i++)
        {
            TreePrototype prototype = prototypes[i];
            if (prototype.prefab != null && swaps.TryGetValue(prototype.prefab.name, out GameObject replacement))
            {
                prototype.prefab = replacement;
                changed++;
            }
            else if (prototype.prefab == null && prototypes.Length == byIndex.Length)
            {
                prototype.prefab = byIndex[i];
                changed++;
            }
        }

        data.treePrototypes = prototypes;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        return $"painted map: {changed} of {prototypes.Length} tree kinds now from the new forest";
    }

    private static List<RaidMapDefinition.Scatter> Convert(Entry[] entries, GameObject treeTemplate, GameObject rockTemplate)
    {
        var list = new List<RaidMapDefinition.Scatter>();
        foreach (Entry entry in entries)
        {
            string source = Source(entry.Source);
            GameObject prefab = entry.Make switch
            {
                Make.Tree => VegetationPackConverter.Tree(source, Out + "/Trees", entry.Name, treeTemplate, leafFall: Fall(source)),
                Make.Snag => VegetationPackConverter.Tree(source, Out + "/Trees", entry.Name, treeTemplate, leafless: true),
                // Young trees stand in the undergrowth but are trees: they block the way and can be felled.
                Make.YoungTree => VegetationPackConverter.Tree(source, Out + "/Undergrowth", entry.Name, treeTemplate, windStrength: 1.2f, leafFall: Fall(source)),
                Make.Sapling => VegetationPackConverter.Tree(source, Out + "/Undergrowth", entry.Name, null, solid: false, windStrength: 1.2f, leafFall: Fall(source)),
                Make.Solid => VegetationPackConverter.Prop(source, Out + "/Rocks and Logs", entry.Name, true, rockTemplate),
                Make.Ore => VegetationPackConverter.Prop(source, Out + "/Rocks and Logs", entry.Name, true, rockTemplate, node: AssetDatabase.LoadAssetAtPath<GameObject>(StoneNode)),
                Make.BigOre => VegetationPackConverter.Prop(source, Out + "/Rocks and Logs", entry.Name, true, rockTemplate, node: AssetDatabase.LoadAssetAtPath<GameObject>(GreatStoneNode)),
                _ => VegetationPackConverter.Prop(source, Out + "/Undergrowth", entry.Name, false),
            };
            list.Add(new RaidMapDefinition.Scatter { prefab = prefab, weight = entry.Weight, scale = entry.Scale, fromTier = entry.FromTier, habitat = entry.Habitat });
        }

        return list;
    }

    // A prefab path: as given when it has a folder, else found by file name under the Cedrus pack.
    private static string Source(string name)
    {
        if (name.StartsWith("Assets/")) return name + ".prefab";
        string file = name + ".prefab";
        string found = AssetDatabase.FindAssets("t:Prefab", new[] { Cedrus }).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => Path.GetFileName(path) == file);
        return found ?? throw new FileNotFoundException("No prefab named " + file + " in " + Cedrus);
    }

    private static string Definition(RaidMapDefinition map, List<RaidMapDefinition.Scatter> trees, List<RaidMapDefinition.Scatter> undergrowth,
        List<RaidMapDefinition.Scatter> rocks, TerrainLayer forest, TerrainLayer soil, TerrainLayer rock, TerrainLayer litter)
    {
        var so = new SerializedObject(map);
        so.FindProperty("forestLayer").objectReferenceValue = forest;
        so.FindProperty("pathLayer").objectReferenceValue = soil;
        so.FindProperty("cliffLayer").objectReferenceValue = rock;
        so.FindProperty("litterLayer").objectReferenceValue = litter;
        so.FindProperty("litter").floatValue = 0.65f;
        so.FindProperty("treeSpacing").floatValue = 1.75f;
        so.FindProperty("maxTreeSlope").floatValue = 36f;
        so.FindProperty("rockSpacing").floatValue = 4.5f;
        so.FindProperty("rocksAtCliffs").floatValue = 0.6f;
        so.FindProperty("rocksInForest").floatValue = 0.42f;
        so.FindProperty("undergrowthSpacing").floatValue = 1.7f;
        so.FindProperty("undergrowthChance").floatValue = 0.65f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // The lists hold plain serializable classes: set them on the asset, then mark it.
        Set(map, "trees", trees);
        Set(map, "undergrowth", undergrowth);
        Set(map, "rocks", rocks);

        // The deep floor is mossy hummocks, ferns and plants; litter thins it; paths and rock stay bare.
        so.Update();
        (TerrainLayer layer, float shortGrass, float tall, float bushes, float plants, float bareAt)[] growth =
        {
            (forest, 0.6f, 0.35f, 0.3f, 0.95f, 0f),
            (soil, 0f, 0f, 0f, 0f, 0.8f),
            (rock, 0f, 0f, 0f, 0f, 0.45f),
            (litter, 0.1f, 0.05f, 0.1f, 0.3f, 0f),
        };
        SerializedProperty list = so.FindProperty("growth");
        list.arraySize = growth.Length;
        for (int i = 0; i < growth.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("layer").objectReferenceValue = growth[i].layer;
            entry.FindPropertyRelative("shortGrass").floatValue = growth[i].shortGrass;
            entry.FindPropertyRelative("tallGrass").floatValue = growth[i].tall;
            entry.FindPropertyRelative("bushes").floatValue = growth[i].bushes;
            entry.FindPropertyRelative("plants").floatValue = growth[i].plants;
            entry.FindPropertyRelative("bareAt").floatValue = growth[i].bareAt;
            entry.FindPropertyRelative("colour").colorValue = GrassSetup.AverageColour(growth[i].layer);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Set(map, "plants", Plants.ToList());
        EditorUtility.SetDirty(map);
        return $"map: {trees.Count} trees, {undergrowth.Count} undergrowth, {rocks.Count} rocks and logs, {Plants.Length} plants, 4 ground layers";
    }

    private static void Set<T>(RaidMapDefinition map, string field, List<T> value) =>
        typeof(RaidMapDefinition).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(map, value);

    // Every generated map starts from a copy of this TerrainData, so its prototypes are the raid's details.
    private static string MapTerrain(List<GameObject> details)
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        var prototypes = new List<DetailPrototype>();
        foreach (GameObject prefab in details)
        {
            bool bush = prefab.name == "Bush";
            bool low = prefab.name == "Short Grass" || prefab.name.Contains("Moss") || prefab.name.Contains("Clover") || prefab.name.Contains("Litter") || prefab.name.Contains("Mushroom");
            var prototype = new DetailPrototype
            {
                usePrototypeMesh = true,
                prototype = prefab,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = bush ? 0.7f : low ? 0.8f : 0.75f,
                maxWidth = bush ? 1.3f : low ? 1.4f : 1.25f,
                minHeight = bush ? 0.7f : low ? 0.8f : 0.75f,
                maxHeight = bush ? 1.3f : low ? 1.3f : 1.25f,
                noiseSpread = 0.35f,
                density = bush ? 0.3f : prefab.name.StartsWith("Glow") ? 0.6f : 1f,
                targetCoverage = prefab.name == "Short Grass" ? 2f : prefab.name == "Tall Grass" ? 1.4f : 1f,
                alignToGround = low ? 0.9f : bush ? 0.2f : 0.6f,
                positionJitter = 0.8f,
                useDensityScaling = true,
            };
            if (!prototype.Validate(out string error)) throw new InvalidOperationException($"{prefab.name}: {error}");
            prototypes.Add(prototype);
        }

        data.detailPrototypes = prototypes.ToArray();
        GrassSetup.UseInstanceCounts(data);
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        return $"terrain: {prototypes.Count} kinds of grass, moss and plants";
    }

    // Moods weigh trees by position. Windthrow is dead and young wood; Cedar Ridge (was Pinewood) slender cedars on dry rock.
    private static string Moods()
    {
        float[] windthrow = { 0.4f, 0.4f, 0.8f, 0.8f, 0.6f, 1f, 1f, 1f, 1f, 1f, 2f, 4f };
        float[] ridge = { 0.3f, 0.3f, 0.5f, 3f, 3f, 0.5f, 0.5f, 4f, 0.5f, 0.5f, 1f, 0.5f };
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RaidLayoutRules"))
        {
            var rules = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(rules);
            SerializedProperty moods = so.FindProperty("moods");
            for (int i = 0; i < moods.arraySize; i++)
            {
                SerializedProperty mood = moods.GetArrayElementAtIndex(i);
                SerializedProperty name = mood.FindPropertyRelative("name");
                SerializedProperty briefing = mood.FindPropertyRelative("briefing");
                SerializedProperty weights = mood.FindPropertyRelative("treeWeights");
                float[] chosen = null;
                if (name.stringValue == "Windthrow") chosen = windthrow;
                else if (name.stringValue == "Pinewood" || name.stringValue == "Cedar Ridge")
                {
                    chosen = ridge;
                    name.stringValue = "Cedar Ridge";
                    briefing.stringValue = "Slender cedars on rising ground, with rock breaking through.";
                }
                else if (name.stringValue == "Old Growth")
                {
                    briefing.stringValue = "The old cedars stand close and the light barely reaches the ground.";
                }

                if (chosen == null && weights.arraySize > 0) chosen = Enumerable.Repeat(1f, Trees.Length).ToArray();
                if (chosen == null) continue;
                weights.arraySize = chosen.Length;
                for (int k = 0; k < chosen.Length; k++) weights.GetArrayElementAtIndex(k).floatValue = chosen[k];
                changed++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rules);
        }

        return $"moods: {changed} weighted for the new trees";
    }

    // The raid terrain's layers change, so its displacement masks are baked again, into its own profile and material.
    private static string Displacement(RaidMapDefinition map)
    {
        Scene raid = EditorSceneManager.OpenScene(RaidScene, OpenSceneMode.Additive);
        try
        {
            Terrain terrain = raid.GetRootGameObjects().Select(root => root.GetComponentInChildren<Terrain>(true)).First(t => t != null);
            TerrainData data = terrain.terrainData;
            data.terrainLayers = map.Layers;
            EditorUtility.SetDirty(data);
            if (terrain.TryGetComponent(out TerrainSurface surface) && surface.Profile != null && !AssetDatabase.GetAssetPath(surface.Profile).StartsWith(Path.ChangeExtension(TerrainPath, null)))
            {
                surface.Profile = null;
            }

            terrain.materialTemplate = map.TerrainMaterial;
            Material material = TerrainDisplacementEditor.Apply(terrain);
            // Deeper relief where the ground has it to give: rock most, mossy hummocks next, leaf litter, then trodden soil.
            TerrainSurfaceProfile profile = terrain.GetComponent<TerrainSurface>().Profile;
            // Each ground's relief in real units (GroundRelief), not hand-picked amplitudes.
            GroundRelief.Calibrate(profile, GroundRelief.ReferenceStrength);
            EditorUtility.SetDirty(profile);
            material = TerrainDisplacementEditor.Apply(terrain);
            // Soft, mingling borders between moss, soil, litter and rock (0 snaps, 1 is softest).
            material.SetFloat("_HeightTransition", 0.85f);
            // Repetition hidden by a turned second sample, and broad light and dark patches across every ground.
            material.SetFloat("_AntiTiling", 0.85f);
            material.SetFloat("_AntiTilingScale", 0.57f);
            material.SetFloat("_MacroVariation", 0.18f);
            // Steep ground is the forest's own mossy rock, not Haven's grey stone, and only where the painted rock can't hold.
            material.SetTexture("_CliffAlbedo", map.CliffLayer.diffuseTexture);
            material.SetTexture("_CliffNormal", map.CliffLayer.normalMapTexture);
            material.SetTexture("_CliffMask", map.CliffLayer.maskMapTexture);
            material.SetFloat("_CliffTiling", 4f);
            material.SetColor("_CliffTint", Color.white);
            material.SetFloat("_CliffSlopeStart", 46f);
            material.SetFloat("_CliffSlopeEnd", 62f);
            EditorUtility.SetDirty(material);
            if (terrain.TryGetComponent(out TerrainMoss moss))
            {
                // The moss that grows over the forest floor is the Cedrus moss now, without the old leafy sprigs.
                var mossData = new SerializedObject(moss);
                mossData.FindProperty("mossLayer").objectReferenceValue = map.ForestLayer;
                mossData.FindProperty("baseColorMap").objectReferenceValue = map.ForestLayer.diffuseTexture;
                mossData.FindProperty("normalMap").objectReferenceValue = map.ForestLayer.normalMapTexture;
                mossData.FindProperty("packedMap").objectReferenceValue = null;
                mossData.FindProperty("leafyTufts").boolValue = false;
                mossData.ApplyModifiedPropertiesWithoutUndo();
            }

            var so = new SerializedObject(map);
            so.FindProperty("terrainMaterial").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(raid);
            EditorSceneManager.SaveScene(raid);
            return $"raid terrain: {map.Layers.Length} displaced layers, material {material.name}";
        }
        finally
        {
            EditorSceneManager.CloseScene(raid, true);
        }
    }
}
