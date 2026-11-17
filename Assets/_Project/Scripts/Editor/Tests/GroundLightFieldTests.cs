using NUnit.Framework;
using UnityEditor;
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
        var recipe = new GroundLightField.Recipe { Size = Size, Cell = Cell, Ground = Pit(), Relief = 1f, Dim = 1f, NightLiftMost = 2f };
        GroundLightField.Result night = GroundLightField.Build(recipe);
        Assert.Less(night.MeanGround, 1f);
        Assert.AreEqual(1f / night.MeanGround, night.Lift, 1e-4f);
        recipe.Dim = 0f;
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
            Dim = 1f, NightLiftMost = 1.5f,
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
        var recipe = new GroundLightField.Recipe { Size = Size, Cell = Cell, Ground = Pit(), Albedo = new Vector3[Size * Size], Dim = 1f };
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

    [Test]
    public void SplitIndirect_ADarkeningFactor_IsOcclusionAlone_AsBeforeTheField()
    {
        // Without a field, or wherever the field darkens: exactly `occlusion *= factor`, the indirect light untouched.
        foreach (float factor in new[] { 1f, 0.8f, 0.35f, 0f })
        {
            GroundLightField.SplitIndirect(factor, out float onOcclusion, out float onIndirect);
            Assert.AreEqual(factor, onOcclusion);
            Assert.AreEqual(1f, onIndirect);
        }
    }

    [Test]
    public void SplitIndirect_TheNightsLift_SurvivesURPsOcclusionClamp()
    {
        // URP's lit shaders take min(SSAO, occlusion): without SSAO (1) a factor above 1 on occlusion was thrown away and open ground
        // lost its lift. Split, the lift rides on the indirect light, and what comes out is the factor whole.
        const float ssao = 1f;
        foreach (float factor in new[] { 1.14f, 1.5f, 0.9f })
        {
            GroundLightField.SplitIndirect(factor, out float onOcclusion, out float onIndirect);
            float clamped = Mathf.Min(ssao, onOcclusion);
            Assert.AreEqual(factor, clamped * onIndirect, 1e-6f, $"factor {factor}");
            Assert.LessOrEqual(onOcclusion, 1f);
        }
    }

    [Test]
    public void SplitIndirect_MatchesTheShader()
    {
        string shader = System.IO.File.ReadAllText("Assets/_Project/Shaders/GroundLight.hlsl");
        StringAssert.Contains("occlusion *= min(factor, 1.0);", shader);
        StringAssert.Contains("bakedGI *= max(factor, 1.0);", shader);
        StringAssert.Contains("lerp(1.0, 0.1, saturate(normalWS.y))", shader, "the bounce's facing, as BounceFacing");
    }

    [Test]
    public void EveryShader_PutsTheFieldOnThroughGroundLightApply()
    {
        // A factor written straight into occlusion would lose the night's lift to URP's clamp again.
        var straight = new System.Text.RegularExpressions.Regex(
            @"occlusion\s*\*?=\s*(SkyAmbient|SkyAmbientAtLeastCover|GroundLightOnGround|GroundLightRelief)\s*\(");
        var found = new System.Collections.Generic.List<string>();
        foreach (string path in System.IO.Directory.GetFiles("Assets/_Project/Shaders", "*.*", System.IO.SearchOption.AllDirectories))
        {
            if (!path.EndsWith(".shader") && !path.EndsWith(".hlsl"))
            {
                continue;
            }

            string[] lines = System.IO.File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (straight.IsMatch(lines[i]))
                {
                    found.Add($"{path}:{i + 1}");
                }
            }
        }

        CollectionAssert.IsEmpty(found);
    }

    [Test]
    public void BounceFacing_LevelGroundIsBarelyTintedByItself()
    {
        Assert.AreEqual(0.1f, GroundLightField.BounceFacing(1f), 1e-6f, "level open ground");
        Assert.AreEqual(1f, GroundLightField.BounceFacing(0f), 1e-6f, "a wall, a trunk");
        Assert.AreEqual(1f, GroundLightField.BounceFacing(-1f), 1e-6f, "the underside of a branch or an overhang");
        Assert.Greater(GroundLightField.BounceFacing(0.5f), GroundLightField.BounceFacing(0.9f), "a slope more than level ground");
    }

    [Test]
    public void LiftShare_FollowsHowDarkItIs_NotTheClock()
    {
        const float below = 10.5f;
        Assert.AreEqual(1f, GroundLightField.LiftShare(1f, 0f, below), "night");
        Assert.AreEqual(0f, GroundLightField.LiftShare(0f, 1f, below), "the sun 15° up or more");
        Assert.AreEqual(1f, GroundLightField.LiftShare(0.2f, 0.49f, below), 1e-6f, "a dim dawn (the sun 7° up)");
        Assert.AreEqual(1f, GroundLightField.LiftShare(0.05f, 0.65f, below), 1e-6f, "dusk (the sun 10° up): as before, all of it");
        float halfway = GroundLightField.LiftShare(0f, 0.85f, below);
        Assert.Greater(halfway, 0.3f);
        Assert.Less(halfway, 0.7f, "a lowish sun gets some");
        Assert.AreEqual(1f, GroundLightField.LiftShare(0f, 0.99f, 15f), "a full 15° threshold: any sun below 15° gets it all");
        Assert.AreEqual(0f, GroundLightField.LiftShare(0f, 1f, 15f));
    }

    [Test]
    public void LiftAt_TakesItsShareOfTheNightsLift()
    {
        Assert.AreEqual(1f, GroundLightField.LiftAt(0.8f, 1.5f, 0f), "by day no lift");
        Assert.AreEqual(1.25f, GroundLightField.LiftAt(0.8f, 1.5f, 1f), 1e-5f, "at night all of it");
        Assert.AreEqual(1.125f, GroundLightField.LiftAt(0.8f, 1.5f, 0.5f), 1e-5f, "half of it");
    }

    [Test]
    public void TheRaidsDawnGetsTheLift_AsDuskAndNightDo()
    {
        // The raids' own skies and hours: a dawn at 6.4-6.6 has the sun 5-7° up, dimmer than the dusk at 19.2 that was lifted.
        float below = RenderingSettings.Current.LiftBelowSun;
        foreach (string sky in new[] { "Troll Valley", "Moss Marsh", "Misty Woods" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>($"Assets/_Project/Data/Environment/{sky} Day Night.asset");
            Assert.IsNotNull(profile, sky);
            float Share(float hour)
            {
                DayNightState state = profile.Evaluate(hour);
                return GroundLightField.LiftShare(state.Night, state.IsMoon ? 0f : state.Daylight, below);
            }

            Assert.AreEqual(1f, Share(6.6f), 1e-4f, $"{sky} at dawn");
            Assert.AreEqual(1f, Share(19.2f), 1e-4f, $"{sky} at dusk");
            Assert.AreEqual(1f, Share(23.5f), 1e-4f, $"{sky} at night");
            Assert.AreEqual(0f, Share(11f), 1e-4f, $"{sky} by day");
        }
    }

    [Test]
    public void HalfToFloat_ReadsTheCoverMapsHalvesAsUnityDoes()
    {
        for (int bits = 0; bits <= ushort.MaxValue; bits++)
        {
            float got = GroundLightField.HalfToFloat((ushort)bits);
            if ((bits & 0x7C00) == 0)
            {
                // Zero and the subnormals (far below any height that matters): the mantissa in 2^-24ths, exactly.
                float exact = (bits & 0x3FF) / 16777216f * ((bits & 0x8000) != 0 ? -1f : 1f);
                Assert.AreEqual(exact, got, $"0x{bits:X4}");
                continue;
            }

            float expected = Mathf.HalfToFloat((ushort)bits);
            if (float.IsNaN(expected))
            {
                Assert.IsTrue(float.IsNaN(got), $"0x{bits:X4}");
                continue;
            }

            Assert.AreEqual(expected, got, $"0x{bits:X4}");
        }

        Assert.AreEqual(-1000f, GroundLightField.HalfToFloat(Mathf.FloatToHalf(-1000f)), "the map's clear value: nothing stands here");
    }

    [Test]
    public void ARebuild_KeepsTheLandsShade_AndTakesTheNewCoverIntoTheLiftAndTheBounce()
    {
        // The late setup step works the field out again once caches and nodes stand (GroundLightMap.Rebuild): the land's shade comes
        // from the first build untraced, and the cover placed since counts in the night's lift and keeps the sun off the ground.
        var albedo = new Vector3[Size * Size];
        for (int i = 0; i < albedo.Length; i++)
        {
            albedo[i] = new Vector3(0.4f, 0.35f, 0.25f);
        }

        var open = new float[Size * Size];
        for (int i = 0; i < open.Length; i++)
        {
            open[i] = -1000f;
        }

        var recipe = new GroundLightField.Recipe
        {
            Size = Size, Cell = Cell, Ground = Pit(), Relief = 1f, Dim = 1f, NightLiftMost = 2f, Cover = open, CoverSize = Size,
            CoverSide = Size * Cell, CoverStrength = 0.6f, Albedo = albedo, Bounce = true, TowardLight = Vector3.up, BounceSpread = 3f,
        };
        GroundLightField.Result first = GroundLightField.Build(recipe);
        Assert.IsNotNull(first.Relief, "the land's shade is handed on");

        recipe.KnownRelief = first.Relief;
        GroundLightField.Result same = GroundLightField.Build(recipe);
        Assert.AreEqual(first.MeanGround, same.MeanGround, 1e-6f);
        for (int i = 0; i < Size * Size; i++)
        {
            Assert.AreEqual(first.Light[i], same.Light[i], "reused, the land's shade comes out as traced");
        }

        // A stack of crates placed later over the middle: the second build sees it.
        var placed = (float[])open.Clone();
        for (int z = Size / 2 - 6; z < Size / 2 + 6; z++)
        {
            for (int x = Size / 2 - 6; x < Size / 2 + 6; x++)
            {
                placed[z * Size + x] = 30f;
            }
        }

        recipe.Cover = placed;
        GroundLightField.Result rebuilt = GroundLightField.Build(recipe);
        int middle = Size / 2 * Size + Size / 2;
        Assert.Less(rebuilt.MeanGround, first.MeanGround, "the new cover darkens the ground's average");
        Assert.Greater(rebuilt.Lift, first.Lift, "so the night's lift makes up for it");
        Assert.Less(rebuilt.Light[middle].r, first.Light[middle].r, "and the sun no longer reaches under it to bounce");
        Assert.AreEqual(first.Light[middle].a, rebuilt.Light[middle].a, "the land's shade is the same");
    }

    [Test]
    public void ARedrawnPatch_LeavesNoRingOfOldCover()
    {
        // A building's ground changed (BuildingMargin round its bounds): every field cell whose cover test can see the change (a metre
        // out, and a texel's blend) must lie far enough inside the block drawn again to be worked out again from it.
        const float texel = 0.25f, cell = 0.5f;
        const int fieldSize = 512;
        var changed = Rect.MinMaxRect(60.3f, 80.7f, 71.9f, 88.2f);
        Rect drawn = GroundLightMap.DrawnFor(changed, texel);
        // As SkyOcclusionMap.TexelsOver snaps it: out to whole texels.
        float x0 = Mathf.Floor(drawn.xMin / texel) * texel, z0 = Mathf.Floor(drawn.yMin / texel) * texel;
        float x1 = Mathf.Ceil(drawn.xMax / texel) * texel, z1 = Mathf.Ceil(drawn.yMax / texel) * texel;
        Assert.IsTrue(GroundLightMap.CellsInside(new Vector2(x0, z0), new Vector2(x1 - x0, z1 - z0), cell, fieldSize,
            out int first, out int firstRow, out int last, out int lastRow));

        float seen = GroundLightMap.CoverTapReach + texel;
        for (int z = 0; z < fieldSize; z++)
        {
            for (int x = 0; x < fieldSize; x++)
            {
                float cx = (x + 0.5f) * cell, cz = (z + 0.5f) * cell;
                bool seesTheChange = cx >= changed.xMin - seen && cx <= changed.xMax + seen && cz >= changed.yMin - seen && cz <= changed.yMax + seen;
                if (seesTheChange)
                {
                    Assert.IsTrue(x >= first && x <= last && z >= firstRow && z <= lastRow, $"cell {x}, {z} keeps the old cover");
                    Assert.GreaterOrEqual(cx - x0, GroundLightMap.CellInset - 1e-4f, "and it reads only what was drawn");
                    Assert.GreaterOrEqual(x1 - cx, GroundLightMap.CellInset - 1e-4f);
                }
            }
        }
    }

    [Test]
    public void AddSetup_OutsideASceneSetup_IsRefused()
    {
        Assert.IsFalse(SceneTransition.IsBusy);
        Assert.IsFalse(SceneTransition.AddSetup(new NoSetup()), "nothing is still to come: the caller does its work now");
    }

    private sealed class NoSetup : ISceneSetup
    {
        public int SetupOrder => 0;
        public System.Collections.IEnumerator Setup() { yield break; }
    }
}
