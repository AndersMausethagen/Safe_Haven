using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grows a terrain's grass, bushes and Nordic plants from what it is painted with: patchy, by ground layer. Both the editor's
/// Grass and Plants tool and maps generated while a raid loads use it, so a generated forest grows the way a painted one does.
/// Needs a TerrainData that already has the detail prototypes (Short Grass, Tall Grass, Bush and the plants).
/// </summary>
public static class TerrainGrowth
{
    /// <summary>How much of each kind grows on a ground layer, from 0 to 1, and the colour grass takes from it.</summary>
    [Serializable]
    public struct Ground
    {
        public TerrainLayer layer;
        [Range(0f, 1f)] public float shortGrass;
        [Range(0f, 1f)] public float tallGrass;
        [Range(0f, 1f)] public float bushes;
        [Tooltip("Ferns, berry shrubs, heather and wild flowers, in patches.")]
        [Range(0f, 1f)] public float plants;
        [Tooltip("Growth is gone at this share of the layer, so paths and rock stay bare, and thins just before it: terrain blending keeps a layer hidden under about this much. 0: no limit.")]
        [Range(0f, 1f)] public float bareAt;
        [Tooltip("The layer's average colour. Grass takes it on, and a map made while loading can't read it from the texture.")]
        public Color colour;
    }

    /// <summary>One kind of plant grown in patches of its own, over whatever ground grows plants (Ground.plants).</summary>
    [Serializable]
    public sealed class Plant
    {
        [Tooltip("The detail prototype on the terrain, by name.")]
        public string prototype;
        [Tooltip("Off: none of this plant grows.")]
        public bool enabled = true;
        [Tooltip("How wide its patches are, in metres.")]
        [Min(1f)] public float patchSize = 7f;
        [Tooltip("How rare the patches are: 0.5 covers much of the ground, 0.8 only a few spots.")]
        [Range(0.3f, 0.95f)] public float rarity = 0.68f;
        [Tooltip("How thickly it grows inside a patch.")]
        [Range(0f, 1f)] public float thickness = 0.5f;
        [Tooltip("How many cells around it it clears of grass, so it isn't drowned in the lawn.")]
        [Range(0, 3)] public int clearing;
        [Tooltip("How much it loves the wet: towards water and in damp ground its patches spread and thicken. 0: it doesn't care.")]
        [Range(0f, 1f)] public float loveOfWet;

        public Plant()
        {
        }

        public Plant(string prototype, float patchSize, float rarity, float thickness, int clearing, float loveOfWet = 0f)
        {
            this.prototype = prototype;
            this.patchSize = patchSize;
            this.rarity = rarity;
            this.thickness = thickness;
            this.clearing = clearing;
            this.loveOfWet = loveOfWet;
        }
    }

    private const int SeedRange = 10007;
    private const float BareFrom = 0.7f;
    private const float StrayChance = 0.0015f;
    private const float WetSpread = 0.3f;
    private const float WetThickening = 2.5f;

    /// <summary>What a terrain grows when it says nothing else (the World tool's Fill), and what a new raid map starts from.
    /// Glowing plants aren't among them: they are painted by hand here, or named in a raid map's own list.</summary>
    public static readonly IReadOnlyList<Plant> DefaultPlants = new[]
    {
        new Plant("Fern", 1f / 0.11f, 0.64f, 0.5f, 1),
        new Plant("Blueberry", 1f / 0.15f, 0.66f, 0.6f, 1),
        new Plant("Lingonberry", 1f / 0.17f, 0.7f, 0.55f, 1),
        new Plant("Heather", 1f / 0.13f, 0.64f, 0.7f, 0),
        new Plant("White Flowers", 1f / 0.22f, 0.68f, 0.35f, 0),
        new Plant("Yellow Flowers", 1f / 0.2f, 0.74f, 0.3f, 0),
    };

    /// <summary>
    /// Replaces the grass, bush and listed plant layers with growth by ground layer. Other details are kept. Nothing grows where
    /// <paramref name="blocked"/> says so. <paramref name="plants"/> defaults to <see cref="DefaultPlants"/>; <paramref name="wetness"/>
    /// (0 to 1) thickens the plants that love the wet. Returns the number of cells with growth; 0 when the prototypes are missing.
    /// </summary>
    public static int Fill(Terrain terrain, IReadOnlyList<Ground> fills, int seed, Func<Vector3, bool> blocked = null,
        IReadOnlyList<Plant> plants = null, Func<Vector3, float> wetness = null)
    {
        plants ??= DefaultPlants;
        TerrainData data = terrain.terrainData;
        DetailPrototype[] prototypes = data.detailPrototypes;
        int Layer(string name) => Array.FindIndex(prototypes, p => p.prototype != null && p.prototype.name == name);
        int shortLayer = Layer("Short Grass"), tallLayer = Layer("Tall Grass"), bushLayer = Layer("Bush");
        if (shortLayer < 0 || tallLayer < 0 || bushLayer < 0)
        {
            return 0;
        }

        int resolution = data.detailResolution;
        int alphaWidth = data.alphamapWidth, alphaHeight = data.alphamapHeight;
        float[,,] weights = data.GetAlphamaps(0, 0, alphaWidth, alphaHeight);
        var amounts = new Ground[data.terrainLayers.Length];
        foreach (Ground fill in fills)
        {
            int index = Array.IndexOf(data.terrainLayers, fill.layer);
            if (index >= 0)
            {
                amounts[index] = fill;
            }
        }

        var plantLayers = new int[plants.Count];
        var plantCells = new int[plants.Count][,];
        bool anyLoveWet = false;
        for (int k = 0; k < plants.Count; k++)
        {
            plantLayers[k] = Layer(plants[k].prototype);
            plantCells[k] = new int[resolution, resolution];
            anyLoveWet |= plants[k].enabled && plants[k].loveOfWet > 0f;
        }

        var shortCells = new int[resolution, resolution];
        var tallCells = new int[resolution, resolution];
        var bushCells = new int[resolution, resolution];
        Vector3 origin = terrain.transform.position;
        float offset = seed % SeedRange * 17.31f;
        int grown = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (x + 0.5f) / resolution, v = (z + 0.5f) / resolution;
                int ax = Mathf.Min((int)(u * alphaWidth), alphaWidth - 1), az = Mathf.Min((int)(v * alphaHeight), alphaHeight - 1);
                float shortAmount = 0f, tallAmount = 0f, bushAmount = 0f, plantAmount = 0f, room = 1f;
                for (int l = 0; l < amounts.Length; l++)
                {
                    float w = weights[az, ax, l];
                    shortAmount += w * amounts[l].shortGrass;
                    tallAmount += w * amounts[l].tallGrass;
                    bushAmount += w * amounts[l].bushes;
                    plantAmount += w * amounts[l].plants;
                    if (amounts[l].bareAt > 0f)
                    {
                        room = Mathf.Min(room, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(amounts[l].bareAt * BareFrom, amounts[l].bareAt, w)));
                    }
                }

                shortAmount *= room;
                tallAmount *= room;
                bushAmount *= room;
                plantAmount *= room;
                if (shortAmount + tallAmount + bushAmount + plantAmount <= 0.01f)
                {
                    continue;
                }

                var point = new Vector3(origin.x + u * data.size.x, 0f, origin.z + v * data.size.z);
                if (blocked != null && blocked(point))
                {
                    continue;
                }

                // Patches: broad noise thins the lawn, a second noise gathers tall grass, rare spots grow bushes.
                float lawn = Mathf.PerlinNoise(point.x * 0.09f + offset, point.z * 0.09f);
                float tall = Mathf.PerlinNoise(point.x * 0.16f - offset, point.z * 0.16f + 3.7f);
                float bush = Mathf.PerlinNoise(point.x * 0.35f + 9.1f, point.z * 0.35f - offset);
                // Counts per cell, rounded with a little jitter so fractions still grow some clumps.
                float jitter = Mathf.Repeat(Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.547f, 1f);
                shortCells[z, x] = Mathf.FloorToInt(2f * shortAmount * Mathf.Lerp(0.45f, 1f, lawn) + jitter);
                tallCells[z, x] = Mathf.FloorToInt(tallAmount * Mathf.SmoothStep(0f, 1f, (tall - 0.5f) * 3.5f) + jitter);
                bushCells[z, x] = bush > 1f - 0.12f * bushAmount && jitter < 0.15f ? 1 : 0;
                float wet = anyLoveWet && wetness != null ? wetness(point) : 0f;
                for (int k = 0; k < plants.Count; k++)
                {
                    Plant plant = plants[k];
                    if (!plant.enabled)
                    {
                        continue;
                    }

                    // Each plant grows in its own patches (noise), thinly, and a few strays scatter everywhere. Wet ground
                    // spreads the patches and thickens them, for the plants that love it.
                    float scale = 1f / plant.patchSize;
                    float patch = Mathf.PerlinNoise(point.x * scale + 31.7f * (k + 1) + offset, point.z * scale - 17.3f * (k + 1));
                    float roll = Mathf.Repeat(Mathf.Sin(x * 12.9898f + z * 78.233f + (k + 1) * 3.17f) * 43758.547f, 1f);
                    float love = plant.loveOfWet * wet;
                    float threshold = plant.rarity - love * WetSpread;
                    float thickness = plant.thickness * (1f + love * WetThickening);
                    bool inPatch = patch > threshold && roll < thickness * plantAmount * Mathf.InverseLerp(threshold, threshold + 0.12f, patch);
                    plantCells[k][z, x] = inPatch || roll < StrayChance * plantAmount ? 1 : 0;
                }

                grown++;
            }
        }

        ClearGrassAroundPlants(plants, plantCells, shortCells, tallCells, resolution);
        data.SetDetailLayer(0, 0, shortLayer, shortCells);
        data.SetDetailLayer(0, 0, tallLayer, tallCells);
        data.SetDetailLayer(0, 0, bushLayer, bushCells);
        for (int k = 0; k < plants.Count; k++)
        {
            if (plantLayers[k] >= 0)
            {
                data.SetDetailLayer(0, 0, plantLayers[k], plantCells[k]);
            }
        }

        return grown;
    }

    private static void ClearGrassAroundPlants(IReadOnlyList<Plant> plants, int[][,] plantCells, int[,] shortCells, int[,] tallCells, int resolution)
    {
        for (int k = 0; k < plants.Count; k++)
        {
            int clearing = plants[k].clearing;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    if (plantCells[k][z, x] == 0)
                    {
                        continue;
                    }

                    for (int dz = -clearing; dz <= clearing; dz++)
                    {
                        for (int dx = -clearing; dx <= clearing; dx++)
                        {
                            int cz = z + dz, cx = x + dx;
                            if (cz >= 0 && cx >= 0 && cz < resolution && cx < resolution)
                            {
                                shortCells[cz, cx] = 0;
                                tallCells[cz, cx] = 0;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>A top-down colour map of the terrain's paint, for grass to take on (GrassTerrainColour). The caller owns the texture.</summary>
    public static Texture2D BakeColour(Terrain terrain, IReadOnlyList<Ground> fills)
    {
        TerrainData data = terrain.terrainData;
        int width = data.alphamapWidth, height = data.alphamapHeight;
        float[,,] weights = data.GetAlphamaps(0, 0, width, height);
        TerrainLayer[] layers = data.terrainLayers;
        var colours = new Color[layers.Length];
        foreach (Ground fill in fills)
        {
            int index = Array.IndexOf(layers, fill.layer);
            if (index >= 0)
            {
                colours[index] = fill.colour;
            }
        }

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color mixed = Color.black;
                for (int l = 0; l < colours.Length; l++)
                {
                    mixed += colours[l] * weights[y, x, l];
                }

                mixed.a = 1f;
                pixels[y * width + x] = mixed;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = data.name + " Colour", wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>Where trees and rocks stand, as a test for Fill: nothing grows through a trunk or a boulder. Far cheaper than a raycast per cell.</summary>
    public static Func<Vector3, bool> UnderTrees(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int resolution = data.detailResolution;
        var covered = new bool[resolution, resolution];
        TreePrototype[] prototypes = data.treePrototypes;
        var radii = new float[prototypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            CapsuleCollider shape = prototypes[i].prefab != null ? prototypes[i].prefab.GetComponent<CapsuleCollider>() : null;
            radii[i] = shape == null ? 0.5f : shape.direction == 1 ? shape.radius : shape.height * 0.5f;
        }

        float cellsPerMetreX = resolution / data.size.x, cellsPerMetreZ = resolution / data.size.z;
        foreach (TreeInstance tree in data.treeInstances)
        {
            float radius = radii[tree.prototypeIndex] * tree.widthScale + 0.2f;
            int cx = (int)(tree.position.x * resolution), cz = (int)(tree.position.z * resolution);
            int reachX = Mathf.CeilToInt(radius * cellsPerMetreX), reachZ = Mathf.CeilToInt(radius * cellsPerMetreZ);
            for (int z = Mathf.Max(0, cz - reachZ); z <= Mathf.Min(resolution - 1, cz + reachZ); z++)
            {
                for (int x = Mathf.Max(0, cx - reachX); x <= Mathf.Min(resolution - 1, cx + reachX); x++)
                {
                    covered[z, x] = true;
                }
            }
        }

        Vector3 origin = terrain.transform.position;
        return point =>
        {
            int x = Mathf.Clamp((int)((point.x - origin.x) * cellsPerMetreX), 0, resolution - 1);
            int z = Mathf.Clamp((int)((point.z - origin.z) * cellsPerMetreZ), 0, resolution - 1);
            return covered[z, x];
        };
    }
}
