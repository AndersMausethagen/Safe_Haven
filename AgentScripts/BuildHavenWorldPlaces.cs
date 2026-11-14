#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// More places for the Haven world (`unity command script run --file AgentScripts/BuildHavenWorldPlaces.cs --entry
/// BuildHavenWorldPlaces.Apply`): what stands at a road's end, on a shore, in the bog's heart, in the Haven's meadows, on high
/// ground and in the rock, and the small scenes beside the meadows' paths. All composed from the Haven's, the Misty Woods' and
/// the Haven World's own scanned rocks, logs and trees: scenery only, nothing mineable (a place's reward is the recipe's:
/// resource points round its clearing). Saved under Prefabs/World/Places and Prefabs/World/Along the Way; run
/// BuildHavenWorldRecipe after it, which puts them in the recipe. Re-runnable: it rebuilds the prefabs in place.
/// </summary>
public static class BuildHavenWorldPlaces
{
    public const string Folder = "Assets/_Project/Prefabs/World/Places";
    private const string Way = "Assets/_Project/Prefabs/World/Along the Way";
    private const string Woods = "Assets/_Project/Art/Vegetation/Misty Woods/";
    private const string Haven = "Assets/_Project/Art/Vegetation/Safe Haven/";
    private const string World = "Assets/_Project/Art/Vegetation/Haven World/";
    private const string Old = "Assets/_Project/Prefabs/Environment/World/";

    // A scanned boulder stood on end, as the Waystone scene does: its middle this far up at scale 1.
    private const float Upright = 1.45f;

    public static string Apply()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs/World", "Places");
        }

        var made = new List<string>();

        // The road runs out at an old waystone taller than a man, two slabs leaning by it and a cold fire ring: people came this far.
        made.Add(Scene(Folder, "Wayfarer's Stones",
            (World + "Rocks/Mountain Boulder", new Vector3(0f, Upright * 1.5f, 2.2f), new Vector3(0f, 20f, 90f), 1.5f),
            (World + "Rocks/Mountain Slab", new Vector3(-2.6f, 0.9f, 1.4f), new Vector3(58f, 110f, 0f), 0.9f),
            (World + "Rocks/Mountain Slab", new Vector3(2.7f, 0.8f, 1.2f), new Vector3(62f, 250f, 0f), 0.8f),
            (World + "Props/Stone Fire Pit", new Vector3(0f, 0f, -1.6f), Vector3.zero, 1f),
            (Woods + "Rocks and Logs/Mossy Log", new Vector3(-2.2f, 0f, -2.2f), new Vector3(0f, 55f, 0f), 1f),
            (Woods + "Rocks and Logs/Stump B", new Vector3(2.1f, 0f, -2.4f), Vector3.zero, 0.9f),
            (Woods + "Undergrowth/Mossy Stones", new Vector3(1.6f, 0f, 3.1f), new Vector3(0f, 40f, 0f), 1.3f),
            (Woods + "Undergrowth/Mossy Rock Bed", new Vector3(-1.9f, 0f, 3.2f), new Vector3(0f, 130f, 0f), 1.1f),
            (Woods + "Undergrowth/Twig Pile", new Vector3(1.4f, 0f, -0.6f), new Vector3(0f, 70f, 0f), 0.9f)));

        // A fire ring by the water with logs to sit on, shore stones and reeds.
        made.Add(Scene(Folder, "Fisher's Rest",
            (World + "Props/Stone Fire Pit", Vector3.zero, Vector3.zero, 1f),
            (Woods + "Rocks and Logs/Mossy Log", new Vector3(0.2f, 0f, -2.1f), new Vector3(0f, 6f, 0f), 1f),
            (Woods + "Rocks and Logs/Rotten Log C", new Vector3(-2.3f, 0f, 0.4f), new Vector3(0f, 95f, 0f), 1f),
            (Haven + "Rocks/Lake Boulder A", new Vector3(3.4f, 0f, 1.6f), new Vector3(0f, 30f, 0f), 1f),
            (Haven + "Rocks/Lake Stones A", new Vector3(2.2f, 0f, 3.6f), Vector3.zero, 1.2f),
            (Haven + "Rocks/Lake Stones B", new Vector3(-2.6f, 0f, 3.4f), new Vector3(0f, 80f, 0f), 1.2f),
            (Haven + "Plants/Lake Grass Clump", new Vector3(-0.8f, 0f, 4.2f), Vector3.zero, 1.2f),
            (Haven + "Plants/Lake Grass Clump", new Vector3(1.0f, 0f, 4.6f), new Vector3(0f, 120f, 0f), 1f),
            (Haven + "Plants/Lake Fern B", new Vector3(-3.6f, 0f, 2.0f), Vector3.zero, 1f),
            (Woods + "Undergrowth/Twig Pile", new Vector3(1.7f, 0f, -1.0f), new Vector3(0f, 20f, 0f), 1f)));

        // Three stones leaning out of the bog's heart, sinking; dead cedars stand over them.
        made.Add(Scene(Folder, "Drowned Stones",
            (World + "Rocks/Mountain Boulder", new Vector3(0f, Upright * 1.3f - 0.5f, 3.2f), new Vector3(12f, 0f, 90f), 1.3f),
            (World + "Rocks/Mountain Boulder", new Vector3(-3.0f, Upright * 1.1f - 0.6f, -1.6f), new Vector3(-16f, 120f, 82f), 1.1f),
            (World + "Rocks/Mountain Boulder", new Vector3(3.1f, Upright * 0.95f - 0.5f, -1.9f), new Vector3(20f, 240f, 100f), 0.95f),
            (World + "Rocks/Mountain Slab", new Vector3(0f, 0.15f, 0f), new Vector3(0f, 35f, 0f), 1.1f),
            (Woods + "Trees/Dead Cedar", new Vector3(-5.6f, 0f, 3.4f), new Vector3(0f, 40f, 4f), 0.9f),
            (Woods + "Trees/Dead Cedar", new Vector3(5.8f, 0f, 1.2f), new Vector3(3f, 190f, -5f), 0.75f),
            (Woods + "Rocks and Logs/Snag Stump", new Vector3(4.2f, 0f, -4.6f), Vector3.zero, 1.1f),
            (Woods + "Rocks and Logs/Rotten Log D", new Vector3(-4.4f, 0f, -4.0f), new Vector3(0f, 65f, 0f), 1f),
            (Woods + "Undergrowth/Mossy Rock Bed", new Vector3(1.6f, 0f, 1.4f), new Vector3(0f, 60f, 0f), 1.2f),
            (Woods + "Undergrowth/Mossy Stones", new Vector3(-1.5f, 0f, 1.2f), Vector3.zero, 1.2f)));

        // One maple far older than the rest, alone in the meadow, stones set round its foot.
        made.Add(Scene(Folder, "Mother Maple",
            (Haven + "Trees/Sugar Maple B", Vector3.zero, new Vector3(0f, 15f, 0f), 1.75f),
            (Haven + "Rocks/Haven Mossy Stone", new Vector3(3.6f, 0f, 0.4f), Vector3.zero, 1.3f),
            (Haven + "Rocks/Haven Mossy Stone", new Vector3(1.3f, 0f, 3.5f), new Vector3(0f, 70f, 0f), 1.1f),
            (Haven + "Rocks/Haven Mossy Stone", new Vector3(-2.9f, 0f, 2.3f), new Vector3(0f, 140f, 0f), 1.4f),
            (Haven + "Rocks/Haven Mossy Stone", new Vector3(-3.2f, 0f, -1.9f), new Vector3(0f, 210f, 0f), 1.2f),
            (Haven + "Rocks/Haven Mossy Stone", new Vector3(0.9f, 0f, -3.7f), new Vector3(0f, 290f, 0f), 1.3f),
            (Haven + "Plants/Haven Fern", new Vector3(2.4f, 0f, 2.2f), Vector3.zero, 1.1f),
            (Haven + "Plants/Haven Fern", new Vector3(-1.6f, 0f, 3.4f), new Vector3(0f, 90f, 0f), 1f),
            (Haven + "Plants/Haven Fern", new Vector3(-3.8f, 0f, 0.3f), new Vector3(0f, 180f, 0f), 1.2f),
            (Haven + "Rocks/Haven Fallen Branch", new Vector3(2.6f, 0f, -2.4f), new Vector3(0f, 50f, 0f), 1f)));

        // A ring of field stones with a gap for a gate: a fold for beasts nobody keeps any more.
        made.Add(Scene(Folder, "Shepherd's Fold", Ring(Old + "Field Stone", 4.2f, 11, 1.25f, skip: 2,
            (Haven + "Rocks/Lake Boulder B", new Vector3(-0.8f, 0f, 1.0f), new Vector3(0f, 25f, 0f), 0.8f),
            (Woods + "Rocks and Logs/Stump C", new Vector3(1.4f, 0f, -0.9f), Vector3.zero, 1f),
            (Haven + "Rocks/Haven Fallen Branch", new Vector3(5.6f, 0f, 1.0f), new Vector3(0f, 110f, 0f), 1f))));

        // Slabs stacked as high as a man on the cliff's edge, to be seen from below: a place to look out from.
        made.Add(Scene(Folder, "Lookout Cairn",
            (World + "Rocks/Mountain Slab", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), 1.15f),
            (World + "Rocks/Mountain Slab", new Vector3(0.1f, 0.62f, 0f), new Vector3(0f, 35f, 0f), 0.92f),
            (World + "Rocks/Mountain Slab", new Vector3(-0.06f, 1.12f, 0.05f), new Vector3(0f, 80f, 0f), 0.72f),
            (World + "Rocks/Mountain Slab", new Vector3(0.04f, 1.52f, -0.04f), new Vector3(0f, 125f, 0f), 0.54f),
            (World + "Rocks/Mountain Slab", new Vector3(0f, 1.82f, 0f), new Vector3(0f, 170f, 0f), 0.38f),
            (Woods + "Rocks and Logs/Mossy Log", new Vector3(0.4f, 0f, -3.0f), new Vector3(0f, 12f, 0f), 1f),
            (Woods + "Undergrowth/Mossy Stones", new Vector3(2.2f, 0f, 1.0f), Vector3.zero, 1.2f),
            (Woods + "Undergrowth/Scree", new Vector3(-2.1f, 0f, 1.4f), new Vector3(0f, 60f, 0f), 1f),
            (Woods + "Undergrowth/Mossy Stones", new Vector3(-1.2f, 0f, -1.6f), new Vector3(0f, 200f, 0f), 0.9f)));

        // Where stone was once broken out: rock faces in an arc, cut slabs and scree on its floor.
        made.Add(Scene(Folder, "Old Quarry",
            (World + "Rocks/Mountain Face A", new Vector3(0f, 0f, 5.2f), new Vector3(0f, 180f, 0f), 1f),
            (World + "Rocks/Mountain Face B", new Vector3(-5.0f, 0f, 2.4f), new Vector3(0f, 118f, 0f), 0.9f),
            (World + "Rocks/Mountain Face B", new Vector3(5.1f, 0f, 2.2f), new Vector3(0f, 242f, 0f), 0.9f),
            (World + "Rocks/Mountain Slab", new Vector3(-1.6f, 0.1f, 1.2f), new Vector3(0f, 30f, 8f), 1f),
            (World + "Rocks/Mountain Slab", new Vector3(1.9f, 0.3f, 0.6f), new Vector3(10f, 100f, 0f), 0.8f),
            (Woods + "Rocks and Logs/Cliff Slab", new Vector3(0.2f, 0f, -2.2f), new Vector3(0f, 70f, 0f), 0.8f),
            (Woods + "Undergrowth/Scree", new Vector3(-3.0f, 0f, -1.4f), Vector3.zero, 1.3f),
            (Woods + "Undergrowth/Scree", new Vector3(3.2f, 0f, -2.0f), new Vector3(0f, 140f, 0f), 1.2f),
            (Woods + "Undergrowth/Pebbles A", new Vector3(0.6f, 0f, 2.4f), Vector3.zero, 1.4f),
            (Woods + "Undergrowth/Ground Slab", new Vector3(-0.8f, 0f, -4.4f), new Vector3(0f, 20f, 0f), 1.1f)));

        // A cedar older than the woods with mossed boulders gathered at its roots: the quiet is different here.
        made.Add(Scene(Folder, "Elder Cedar", Ring(Woods + "Rocks and Logs/Mossy Boulder A", 5.2f, 5, 0.8f, skip: 0,
            (Woods + "Trees/Cedar Giant A", Vector3.zero, new Vector3(0f, 80f, 0f), 1.3f),
            (Woods + "Rocks and Logs/Mossy Boulder B", new Vector3(2.4f, 0f, -2.6f), new Vector3(0f, 45f, 0f), 0.7f),
            (Woods + "Rocks and Logs/Mossy Rotten Log", new Vector3(-3.4f, 0f, -3.0f), new Vector3(0f, 130f, 0f), 1f),
            (Woods + "Undergrowth/Forest Shrub A", new Vector3(3.6f, 0f, 2.6f), Vector3.zero, 1f),
            (Woods + "Undergrowth/Forest Shrub B", new Vector3(-3.9f, 0f, 2.1f), new Vector3(0f, 70f, 0f), 1f),
            (Woods + "Undergrowth/Mossy Rock Bed", new Vector3(0.6f, 0f, 3.2f), new Vector3(0f, 20f, 0f), 1.2f))));

        // Beside the paths through the Haven's own meadows.
        var meadow = new List<string>
        {
            Scene(Way, "Boundary Stone",
                (World + "Rocks/Mountain Boulder", new Vector3(0f, Upright * 0.6f, 0f), new Vector3(0f, 30f, 90f), 0.6f),
                (Haven + "Rocks/Haven Mossy Stone", new Vector3(0.9f, 0f, 0.5f), Vector3.zero, 0.9f),
                (Haven + "Plants/Haven Fern", new Vector3(-0.8f, 0f, 0.6f), new Vector3(0f, 60f, 0f), 1f)),
            Scene(Way, "Maple Rest",
                (Haven + "Trees/Sugar Maple C", Vector3.zero, new Vector3(0f, 110f, 0f), 1.1f),
                (Woods + "Rocks and Logs/Mossy Log", new Vector3(2.6f, 0f, 0.8f), new Vector3(0f, 75f, 0f), 0.9f),
                (Woods + "Rocks and Logs/Stump C", new Vector3(-1.9f, 0f, 1.6f), Vector3.zero, 0.9f),
                (Haven + "Rocks/Haven Fallen Branch", new Vector3(0.8f, 0f, -2.4f), new Vector3(0f, 20f, 0f), 1f)),
            Scene(Way, "Field Stones",
                (Old + "Field Stone", Vector3.zero, new Vector3(0f, 15f, 0f), 1.4f),
                (Old + "Field Stone", new Vector3(1.3f, 0f, 0.6f), new Vector3(0f, 100f, 0f), 1f),
                (Old + "Field Stone", new Vector3(-0.9f, 0f, 1.1f), new Vector3(0f, 200f, 0f), 0.8f),
                (Haven + "Plants/Haven Fern", new Vector3(0.5f, 0f, -1.2f), Vector3.zero, 1.1f),
                (Haven + "Rocks/Lake Stones A", new Vector3(-1.6f, 0f, -0.6f), new Vector3(0f, 50f, 0f), 1f)),
            Scene(Way, "Old Stump and Saplings",
                (Old + "Old Stump A", Vector3.zero, new Vector3(0f, 40f, 0f), 1.1f),
                (Haven + "Trees/Maple Sapling C", new Vector3(1.8f, 0f, 0.9f), Vector3.zero, 1f),
                (Haven + "Trees/Maple Sapling D", new Vector3(-1.5f, 0f, 1.4f), new Vector3(0f, 140f, 0f), 1.1f),
                (Haven + "Rocks/Haven Fallen Branch", new Vector3(0.4f, 0f, -1.6f), new Vector3(0f, 80f, 0f), 1f)),
        };

        AssetDatabase.SaveAssets();
        return $"{made.Count} places in {Folder} ({string.Join(", ", made)}); {meadow.Count} meadow scenes in {Way} ({string.Join(", ", meadow)}).";
    }

    // A ring of one prop, each a little turned and sized, with a gap of `skip` places, and whatever else stands in it.
    private static (string prefab, Vector3 at, Vector3 euler, float scale)[] Ring(string prefab, float radius, int count, float scale, int skip,
                                                                                params (string prefab, Vector3 at, Vector3 euler, float scale)[] more)
    {
        var parts = new List<(string, Vector3, Vector3, float)>();
        for (int i = skip; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float wobble = 1f + 0.08f * Mathf.Sin(i * 2.7f);
            parts.Add((prefab, new Vector3(Mathf.Sin(angle) * radius * wobble, 0f, Mathf.Cos(angle) * radius * wobble),
                       new Vector3(0f, i * 67f % 360f, 0f), scale * (0.85f + 0.3f * Mathf.Abs(Mathf.Sin(i * 1.9f)))));
        }

        parts.AddRange(more);
        return parts.ToArray();
    }

    private static string Scene(string folder, string name, params (string prefab, Vector3 at, Vector3 euler, float scale)[] parts)
    {
        var root = new GameObject(name);
        try
        {
            foreach ((string prefab, Vector3 at, Vector3 euler, float scale) in parts)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefab + ".prefab") ?? throw new FileNotFoundException("No prefab at " + prefab);
                var made = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                made.transform.localPosition = at;
                made.transform.localRotation = Quaternion.Euler(euler);
                made.transform.localScale = Vector3.one * scale;
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{name}.prefab");
            return name;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
