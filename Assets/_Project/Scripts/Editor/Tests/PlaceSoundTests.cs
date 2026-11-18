using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The place's sound (Docs/LookAndFeelResearch_2026-11-18.md A1-A4): a biome's beds come in by the hour and the rain; air dulls a sound
/// with distance and a ridge or a wall between dulls it further; swimming makes everything dull; a rustle needs plants that rustle
/// under the foot; and a still-water sheet laid under the land (the Moss Marsh's fen) only sounds where its water shows.
/// </summary>
public sealed class PlaceSoundTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        made.Clear();
    }

    private AmbienceBed.Layer Layer(AnimationCurve byHour, float volume = 1f, float inRain = 1f)
    {
        var sound = ScriptableObject.CreateInstance<SoundDefinition>();
        made.Add(sound);
        return new AmbienceBed.Layer { sound = sound, byHour = byHour, volume = volume, inRain = inRain };
    }

    [Test]
    public void ADayBedIsUpByDayAndGoneByNight()
    {
        var day = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(6f, 0f), new Keyframe(8f, 1f), new Keyframe(19f, 1f), new Keyframe(21f, 0f), new Keyframe(24f, 0f));
        AmbienceBed.Layer layer = Layer(day);
        float sound = layer.sound.Volume;
        Assert.AreEqual(sound, AmbienceBed.LevelOf(layer, 12f, 0f), 1e-4f, "noon: the bed at its sound's own volume");
        Assert.AreEqual(0f, AmbienceBed.LevelOf(layer, 2f, 0f), 1e-4f, "the small hours: silent");
        Assert.That(AmbienceBed.LevelOf(layer, 7f, 0f), Is.GreaterThan(0f).And.LessThan(sound), "dawn: coming in");
    }

    [Test]
    public void RainQuietsOrSwellsABedByItsShare()
    {
        AmbienceBed.Layer insects = Layer(new AnimationCurve(), 0.5f, 0.2f);
        AmbienceBed.Layer drips = Layer(new AnimationCurve(), 0.5f, 1.8f);
        float dry = AmbienceBed.LevelOf(insects, 12f, 0f);
        Assert.AreEqual(insects.sound.Volume * 0.5f, dry, 1e-4f, "an empty curve: always, at the layer's volume");
        Assert.AreEqual(dry * 0.2f, AmbienceBed.LevelOf(insects, 12f, 1f), 1e-4f, "insects go quiet in full rain");
        Assert.AreEqual(dry * 0.6f, AmbienceBed.LevelOf(insects, 12f, 0.5f), 1e-4f, "half rain: halfway");
        Assert.AreEqual(AmbienceBed.LevelOf(drips, 12f, 0f) * 1.8f, AmbienceBed.LevelOf(drips, 12f, 1f), 1e-4f, "drips grow");
        Assert.AreEqual(0f, AmbienceBed.LevelOf(new AmbienceBed.Layer(), 12f, 0f), "no sound: nothing");
    }

    [Test]
    public void AirDullsWithDistanceAndAWallFurther()
    {
        float near = AudioSpace.Cutoff(0f, 35f, false, false);
        float mid = AudioSpace.Cutoff(17.5f, 35f, false, false);
        float far = AudioSpace.Cutoff(35f, 35f, false, false);
        Assert.AreEqual(AudioSpace.OpenCutoff, near, 1f, "at the ears: unfiltered");
        Assert.AreEqual(AudioSpace.FarCutoff, far, 1f, "at its audible distance: the most air takes");
        Assert.AreEqual(Mathf.Sqrt(near * far), mid, 5f, "geometric: halfway out, halfway down in octaves");
        Assert.AreEqual(far, AudioSpace.Cutoff(80f, 35f, false, false), 1f, "beyond: no further");
        Assert.AreEqual(mid * AudioSpace.OccludedShare, AudioSpace.Cutoff(17.5f, 35f, true, false), 5f, "behind the ground: far duller");
        Assert.LessOrEqual(AudioSpace.Cutoff(0f, 35f, false, true), AudioSpace.SwimmingCutoff, "swimming: dull even close by");
        Assert.GreaterOrEqual(AudioSpace.Cutoff(35f, 35f, true, true), 400f, "never closed altogether");
    }

    [Test]
    public void ATreeTrunkIsNotTheGroundInBetween()
    {
        // Unity reports a trunk's capsule as the terrain's collider: only a hit at the ground's own surface muffles a sound.
        Assert.IsTrue(AudioSpace.CountsAsGround(10.2f, 10f), "the ridge itself");
        Assert.IsFalse(AudioSpace.CountsAsGround(11.4f, 10f), "a trunk a metre and more up");
    }

    [Test]
    public void ARustleNeedsRustlingPlantsUnderfoot()
    {
        TerrainData data = Ground(out Terrain terrain);
        var reed = new Texture2D(4, 4) { name = "Marsh Reed" };
        var moss = new Texture2D(4, 4) { name = "Low Moss" };
        made.Add(reed);
        made.Add(moss);
        data.SetDetailResolution(16, 8);
        data.detailPrototypes = new[]
        {
            new DetailPrototype { prototypeTexture = reed, renderMode = DetailRenderMode.GrassBillboard },
            new DetailPrototype { prototypeTexture = moss, renderMode = DetailRenderMode.GrassBillboard },
        };
        var reeds = new int[16, 16];
        var mosses = new int[16, 16];
        reeds[2, 3] = 4; // row z 2, column x 3
        mosses[10, 10] = 6;
        data.SetDetailLayer(0, 0, 0, reeds);
        data.SetDetailLayer(0, 0, 1, mosses);
        // A cell is 32 m / 16 = 2 m: x 3 -> 7 m, z 2 -> 5 m.
        Assert.AreEqual(4, PlayerAudio.PlantsUnder(terrain, new Vector3(7f, 0f, 5f), "reed, sedge"), "reeds rustle");
        Assert.AreEqual(0, PlayerAudio.PlantsUnder(terrain, new Vector3(21f, 0f, 21f), "reed, sedge"), "moss doesn't");
        Assert.AreEqual(6, PlayerAudio.PlantsUnder(terrain, new Vector3(21f, 0f, 21f), "moss"), "unless named");
        Assert.AreEqual(0, PlayerAudio.PlantsUnder(terrain, new Vector3(7f, 0f, 5f), " "), "no words: nothing");
    }

    [Test]
    public void StillWaterUnderTheLandDoesNotShow()
    {
        Ground(out Terrain terrain, 2f);
        Assert.IsTrue(PlacedAmbience.Buried(terrain, new Vector3(10f, 1.5f, 10f)), "a sheet half a metre under the ground");
        Assert.IsFalse(PlacedAmbience.Buried(terrain, new Vector3(10f, 2.5f, 10f)), "water over the ground shows");
        Assert.IsFalse(PlacedAmbience.Buried(null, new Vector3(10f, -5f, 10f)), "no terrain: nothing buries it");
    }

    // A 32 m flat terrain at the given height.
    private TerrainData Ground(out Terrain terrain, float height = 0f)
    {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 10f, 32f) };
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        {
            for (int x = 0; x < 33; x++)
            {
                heights[z, x] = height / 10f;
            }
        }

        data.SetHeights(0, 0, heights);
        made.Add(data);
        GameObject holder = Terrain.CreateTerrainGameObject(data);
        made.Add(holder);
        terrain = holder.GetComponent<Terrain>();
        return data;
    }
}
