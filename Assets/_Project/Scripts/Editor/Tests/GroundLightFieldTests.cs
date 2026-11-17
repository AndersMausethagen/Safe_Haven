using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A raid's ground light field (GroundLightField, GroundLightMap): the land's shade on synthetic ground, the thrown-back light's
/// spread, the cover test matching SkyOcclusion.hlsl, the night's lift, and that a raid with nothing set looks as it did.
/// </summary>
public sealed class GroundLightFieldTests
{
    private const int Size = 64;
    private const float Cell = 0.5f;

    private static float[] Level(float height = 10f)
    {
        var heights = new float[Size * Size];
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = height;
        }

        return heights;
    }

    // A round pit, 6 m across and 4 m deep, in the middle of level ground.
    private static float[] Pit()
    {
        float[] heights = Level();
        for (int z = 0; z < Size; z++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - Size / 2) * Cell, dz = (z - Size / 2) * Cell;
                if (dx * dx + dz * dz < 9f)
                {
                    heights[z * Size + x] = 6f;
                }
            }
        }

        return heights;
    }

    [Test]
    public void Relief_LevelGround_SeesAllItsSky()
    {
        float[] open = GroundLightField.Relief(Level(), Size, Cell);
        foreach (float value in open)
        {
            Assert.AreEqual(1f, value, 1e-5f);
        }
    }

    [Test]
    public void Relief_APit_IsDarkerThanThePlain()
    {
        float[] open = GroundLightField.Relief(Pit(), Size, Cell);
        float pit = open[Size / 2 * Size + Size / 2];
        float plain = open[4 * Size + 4];
        Assert.AreEqual(1f, plain, 1e-4f, "level ground far from the pit");
        Assert.Less(pit, 0.6f, "the pit's floor, walls 4 m high 3 m away");
        Assert.Greater(pit, 0.05f, "but it still sees the sky straight up");
    }

    [Test]
    public void Relief_TheWallsFootIsDarkerThanItsTop()
    {
        // A step 5 m high across the middle: the foot of the wall sees less sky than the ground on top of it.
        float[] heights = Level();
        for (int z = 0; z < Size; z++)
        {
            for (int x = Size / 2; x < Size; x++)
            {
                heights[z * Size + x] = 15f;
            }
        }

        float[] open = GroundLightField.Relief(heights, Size, Cell);
        int row = Size / 2 * Size;
        Assert.Less(open[row + Size / 2 - 2], open[row + Size / 2 + 2]);
        Assert.Less(open[row + Size / 2 - 2], 0.9f);
    }

    [Test]
    public void ReliefFactor_StrengthZero_ChangesNothing()
    {
        Assert.AreEqual(1f, GroundLightField.ReliefFactor(0.2f, 0f));
        Assert.AreEqual(0.2f, GroundLightField.ReliefFactor(0.2f, 1f), 1e-6f);
        Assert.AreEqual(0.6f, GroundLightField.ReliefFactor(0.2f, 0.5f), 1e-6f);
    }

    [Test]
    public void Blur_SpreadsLightOverItsRadius_AndKeepsItsAmount()
    {
        var grid = new float[Size * Size];
        grid[Size / 2 * Size + Size / 2] = 100f;
        GroundLightField.Blur(grid, Size, 3);
        GroundLightField.Blur(grid, Size, 3);
        float total = 0f;
        foreach (float value in grid)
        {
            total += value;
        }

        Assert.AreEqual(100f, total, 1e-3f, "nothing lost away from the edges");
        int centre = Size / 2 * Size + Size / 2;
        Assert.Greater(grid[centre + 4], 0f, "reaches 4 cells out (two blurs of 3)");
        Assert.AreEqual(0f, grid[centre + 7], 1e-4f, "but not past 6");
        Assert.AreEqual(grid[centre + 2], grid[centre - 2], 1e-5f, "evenly both ways");
        Assert.AreEqual(grid[centre + 2], grid[centre + 2 * Size], 1e-5f, "and along both axes");
        Assert.Greater(grid[centre], grid[centre + 3], "brightest where it came from");
    }

    [Test]
    public void SunLight_AWallShadesTheGroundBehindIt()
    {
        // A ridge 8 m high across the middle, the sun 45° up in the east (+x): its shadow reaches 8 m west, the ground east of it is
        // in sun.
        float[] ground = Level();
        for (int z = 0; z < Size; z++)
        {
            ground[z * Size + Size / 2] = 18f;
        }

        Vector3 toward = new Vector3(1f, 1f, 0f).normalized;
        float[] lit = GroundLightField.SunLight(ground, (float[])ground.Clone(), Size, Cell, toward);
        int row = Size / 2 * Size;
        Assert.AreEqual(0f, lit[row + Size / 2 - 4], 1e-4f, "west of the ridge, within its shadow");
        Assert.Greater(lit[row + Size / 2 + 4], 0.3f, "east of it, facing the sun");
        Assert.Greater(lit[row + 2], 0.3f, "far west, past the shadow's end");
    }

    [Test]
    public void SunLight_CanopyOverhead_ShadesTheGround()
    {
        float[] ground = Level();
        float[] tops = (float[])ground.Clone();
        tops[Size / 2 * Size + Size / 2] = 25f; // a crown over this one cell
        float[] lit = GroundLightField.SunLight(ground, tops, Size, Cell, Vector3.up);
        Assert.AreEqual(0f, lit[Size / 2 * Size + Size / 2], 1e-5f);
        Assert.AreEqual(1f, lit[Size / 2 * Size + Size / 2 + 3], 1e-4f, "open level ground under a high sun");
    }

    [Test]
    public void SunLight_BelowTheHorizon_LightsNothing()
    {
        float[] lit = GroundLightField.SunLight(Level(), Level(), Size, Cell, new Vector3(1f, -0.2f, 0f));
        foreach (float value in lit)
        {
            Assert.AreEqual(0f, value);
        }
    }

    [Test]
    public void CoverAt_MatchesSkyOcclusionShader()
    {
        // Nothing standing: no dimming. A 10 m crown over every tap: the shader's full-cover value.
        var bare = new float[16 * 16];
        var crowned = new float[16 * 16];
        for (int i = 0; i < bare.Length; i++)
        {
            bare[i] = -1000f;
            crowned[i] = 20f;
        }

        Assert.AreEqual(1f, GroundLightField.CoverAt(bare, 16, 16, Vector2.zero, 1f, 8f, 8f, 10f, 0.6f), 1e-6f);
        Assert.AreEqual(1f - 0.65f * 0.6f, GroundLightField.CoverAt(crowned, 16, 16, Vector2.zero, 1f, 8f, 8f, 10f, 0.6f), 1e-5f);
        Assert.AreEqual(1f, GroundLightField.CoverAt(crowned, 16, 16, Vector2.zero, 1f, 8f, 8f, 10f, 0f), 1e-6f, "strength 0: no dimming");
    }

    [Test]
    public void NightLift_KeepsTheAverage_WithinItsCap()
    {
        Assert.AreEqual(1.25f, GroundLightField.NightLift(0.8f, 1.5f), 1e-5f);
        Assert.AreEqual(1.5f, GroundLightField.NightLift(0.5f, 1.5f), 1e-5f, "capped");
        Assert.AreEqual(1f, GroundLightField.NightLift(1f, 1.5f), 1e-6f, "nothing to make up for");
        Assert.AreEqual(1f, GroundLightField.NightLift(0.8f, 1f), 1e-6f, "lift switched off");
    }

    [Test]
    public void Build_AtNight_LiftsTheLandsShadeBackToItsAverage()
    {
        var recipe = new GroundLightField.Recipe { Size = Size, Cell = Cell, Ground = Pit(), Relief = 1f, Dark = true, NightLiftMost = 2f };
        GroundLightField.Result night = GroundLightField.Build(recipe);
        Assert.Less(night.MeanGround, 1f);
        Assert.AreEqual(1f / night.MeanGround, night.Lift, 1e-4f);
        recipe.Dark = false;
        Assert.AreEqual(1f, GroundLightField.Build(recipe).Lift, "no lift by day");
    }

    [Test]
    public void Build_AtNight_TheCanopysNewShadeOnTheTerrainIsLiftedToo()
    {
        // The terrain read no cover before the field: at night, canopy over half the map is made up for in the ground's average.
        var cover = new float[Size * Size];
        for (int z = 0; z < Size; z++)
        {
            for (int x = 0; x < Size; x++)
            {
                cover[z * Size + x] = x < Size / 2 ? 25f : -1000f;
            }
        }

        var recipe = new GroundLightField.Recipe
        {
            Size = Size, Cell = Cell, Ground = Level(), Cover = cover, CoverSize = Size, CoverSide = Size * Cell, CoverStrength = 0.35f,
            Dark = true, NightLiftMost = 1.5f,
        };
        GroundLightField.Result night = GroundLightField.Build(recipe);
        Assert.Less(night.MeanGround, 0.95f);
        Assert.AreEqual(1f / night.MeanGround, night.Lift, 1e-4f);
        float shaded = night.Ground[Size / 2 * Size + 4].r / 255f * night.Lift;
        Assert.GreaterOrEqual(shaded, 0.85f, "under the canopy, lifted, close to the old floor");
        Assert.Greater(night.Lift, 1f, "and open ground a little lighter than before");
    }

    [Test]
    public void Build_WithNothingSet_ReproducesTodaysLight()
    {
        // A raid's Ground Light left as it comes: no relief, no bounce, no cover. Every texel reads back as "no change".
        var recipe = new GroundLightField.Recipe { Size = Size, Cell = Cell, Ground = Pit(), Albedo = new Vector3[Size * Size], Dark = true };
        GroundLightField.Result result = GroundLightField.Build(recipe);
        Assert.AreEqual(1f, result.Lift);
        for (int i = 0; i < Size * Size; i++)
        {
            Assert.AreEqual(new Color32(0, 0, 0, 255), result.Light[i], "no thrown-back light, the land's shade 1");
            Assert.AreEqual(255, result.Ground[i].r, "the canopy's shade 1");
        }
    }

    [Test]
    public void Build_Bounce_GlowsInShadeNextToSunlitGround()
    {
        // Pale ground in full sun beside a strip under a canopy: the shaded strip's edge gets thrown-back light, its middle less.
        float[] ground = Level();
        var cover = new float[Size * Size];
        var albedo = new Vector3[Size * Size];
        for (int z = 0; z < Size; z++)
        {
            for (int x = 0; x < Size; x++)
            {
                cover[z * Size + x] = x < Size / 2 ? 25f : -1000f; // canopy over the west half
                albedo[z * Size + x] = new Vector3(0.4f, 0.35f, 0.25f);
            }
        }

        var recipe = new GroundLightField.Recipe
        {
            Size = Size, Cell = Cell, Ground = ground, Cover = cover, CoverSize = Size, CoverSide = Size * Cell,
            Albedo = albedo, Bounce = true, TowardLight = Vector3.up, CoverStrength = 0.6f, BounceSpread = 3f,
        };
        GroundLightField.Result result = GroundLightField.Build(recipe);
        int row = Size / 2 * Size;
        byte edge = result.Light[row + Size / 2 - 3].r, deep = result.Light[row + 4].r, sunlit = result.Light[row + Size - 4].r;
        Assert.Greater(edge, deep, "shade by the sunlit ground gets more than deep shade");
        Assert.Greater(sunlit, edge);
        Assert.Greater(edge, 0);
        Assert.Less(result.Ground[row + 4].r, 200, "the terrain under the canopy is shaded");
        Assert.AreEqual(255, result.Ground[row + Size - 4].r, "and open ground isn't");
        Assert.Greater(result.Light[row + Size - 4].r, result.Light[row + Size - 4].b, "it takes on the ground's colour");
    }

    [Test]
    public void RaidGroundLight_IsOffUntilSet()
    {
        var look = new RaidGroundLight();
        Assert.IsFalse(look.Enabled);
        Assert.AreEqual(0f, look.Relief);
        Assert.AreEqual(0f, look.Bounce);
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        try
        {
            Assert.IsFalse(raid.GroundLight.Enabled, "a new raid starts as before the field");
        }
        finally
        {
            Object.DestroyImmediate(raid);
        }
    }

    [Test]
    public void GroundHeights_FollowTheTerrain()
    {
        // A 3×3 heightmap rising west to east over 20 m: the field's cells read it back in metres.
        var heights = new float[3, 3];
        for (int z = 0; z < 3; z++)
        {
            for (int x = 0; x < 3; x++)
            {
                heights[z, x] = x * 0.5f;
            }
        }

        float[] ground = GroundLightMap.GroundHeights(heights, 3, 4, 5f, new Vector3(20f, 10f, 20f), 100f);
        Assert.AreEqual(101.25f, ground[0], 1e-4f, "cell centre 2.5 m in: a quarter of the way to the next post (0.5), of 10 m");
        Assert.Less(ground[0], ground[3]);
        Assert.AreEqual(ground[0], ground[3 * 4], 1e-5f, "level north to south");
    }
}
