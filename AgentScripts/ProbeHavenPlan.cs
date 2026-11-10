using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Haven world's plan as a picture and a count, without touching the scene
/// (`unity command script run --file AgentScripts/ProbeHavenPlan.cs --entry ProbeHavenPlan.Shoot --args '["out.png", 0, 1024]'`;
/// seed 0 is the recipe's own). The Haven World Planner window (Safe Haven › World › Haven World Planner) shows the same.
/// </summary>
public static class ProbeHavenPlan
{
    public static string Shoot(string outPath, int seed, int size)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        var clock = Stopwatch.StartNew();
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed == 0 ? (int?)null : seed);
        long planned = clock.ElapsedMilliseconds;
        Color32[] pixels = HavenWorldPreview.Render(plan, size, HavenWorldPreview.Layers.All & ~HavenWorldPreview.Layers.Mist);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        File.WriteAllBytes(outPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        return $"plan {planned} ms, picture {clock.ElapsedMilliseconds - planned} ms\n{HavenWorldPreview.Stats(plan)}";
    }

    /// <summary>The counts alone, for several seeds: how steady the recipe is.</summary>
    public static string Seeds(int from, int count)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        var text = new System.Text.StringBuilder();
        for (int seed = from; seed < from + count; seed++)
        {
            HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
            text.AppendLine($"seed {seed}: places {plan.Pois.Count}, glades {plan.Glades.Count}, meres {plan.Meres.Count}, streams {plan.Rivers.Count - 2}, falls {plan.Falls.Count}, " +
                            $"cliffs {plan.DressedCliffMetres:0} m, scrambles {plan.Scrambles.Count}, zones {plan.Zones.Count}, nodes {plan.Nodes.Count}, " +
                            $"reachable {plan.ReachableShare * 100f:0.0}%, dull {plan.LongestDullWalk:0} m ({plan.DullWalks.Count}), dead ends {plan.DeadEnds.Count}, empty {plan.EmptyShare * 100f:0.0}%");
        }

        return text.ToString();
    }
}
