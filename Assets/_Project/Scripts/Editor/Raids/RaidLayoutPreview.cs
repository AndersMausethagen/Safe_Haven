using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows what a raid's Layout Rules make, without starting a raid: a sheet of maps from new seeds, with the ground shaded by
/// height, trails and clearings in soil colours, and each kind of site in its marker colour. Safe Haven › Raids › Layout
/// Preview. Press New Seeds to see more; tune the rules asset and watch the maps change.
/// </summary>
public sealed class RaidLayoutPreview : EditorWindow
{
    private const int Columns = 3;
    private const int Rows = 2;
    private const int MapPixels = 256;
    private const float Size = 200f;

    private readonly List<Texture2D> maps = new List<Texture2D>();
    private readonly List<string> captions = new List<string>();
    private RaidLayoutRules rules;
    private int firstSeed = 1;

    [MenuItem("Safe Haven/Raids/Layout Preview")]
    private static void Open() => GetWindow<RaidLayoutPreview>("Raid Layouts");

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        rules = (RaidLayoutRules)EditorGUILayout.ObjectField("Rules", rules, typeof(RaidLayoutRules), false);
        bool changed = EditorGUI.EndChangeCheck();
        if (GUILayout.Button("New Seeds"))
        {
            firstSeed += Columns * Rows;
            changed = true;
        }

        if (rules == null)
        {
            return;
        }

        if (changed || maps.Count == 0)
        {
            Rebuild();
        }

        float width = Mathf.Min(MapPixels, (position.width - 12f) / Columns);
        for (int row = 0; row < Rows; row++)
        {
            Rect line = GUILayoutUtility.GetRect(width * Columns, width + 18f);
            for (int column = 0; column < Columns; column++)
            {
                int index = row * Columns + column;
                var cell = new Rect(line.x + column * width, line.y, width - 4f, width - 4f);
                GUI.DrawTexture(cell, maps[index]);
                GUI.Label(new Rect(cell.x, cell.yMax, cell.width, 18f), captions[index], EditorStyles.miniLabel);
            }
        }
    }

    private void Rebuild()
    {
        foreach (Texture2D map in maps)
        {
            DestroyImmediate(map);
        }

        maps.Clear();
        captions.Clear();
        for (int i = 0; i < Columns * Rows; i++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, firstSeed + i);
            maps.Add(Draw(plan, rules, MapPixels));
            captions.Add($"{plan.Shape.shape}, {plan.Mood.name} · {plan.Rooms.Count} clearings, {plan.Links.Count} trails");
        }
    }

    private void OnDisable()
    {
        foreach (Texture2D map in maps)
        {
            DestroyImmediate(map);
        }
    }

    /// <summary>One map as a picture, north up.</summary>
    public static Texture2D Draw(RaidLayoutPlan plan, RaidLayoutRules rules, int pixels)
    {
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        field.Zones(out byte[] path, out byte[] forest, out byte[] clearing);
        int cells = field.Resolution;
        var colours = new Color32[pixels * pixels];
        var map = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color wild = new Color(0.13f, 0.27f, 0.16f);
        Color open = new Color(0.36f, 0.42f, 0.24f);
        Color soil = new Color(0.62f, 0.5f, 0.32f);
        for (int y = 0; y < pixels; y++)
        {
            for (int x = 0; x < pixels; x++)
            {
                float u = (x + 0.5f) / pixels;
                float v = (y + 0.5f) / pixels;
                int cell = Mathf.Min(cells - 1, (int)(v * cells)) * cells + Mathf.Min(cells - 1, (int)(u * cells));
                Color colour = Color.Lerp(open, wild, forest[cell] / 255f);
                colour = Color.Lerp(colour, soil, Mathf.Max(path[cell], clearing[cell]) / 255f);
                float light = 0.75f + Mathf.Clamp01(field.HeightAt(u, v) / 26f) * 0.75f;
                colours[y * pixels + x] = new Color(colour.r * light, colour.g * light, colour.b * light, 1f);
            }
        }

        map.SetPixels32(colours);
        foreach (RaidSite site in plan.Sites)
        {
            Dot(map, site.Position, pixels, Marker(site.Kind));
        }

        map.Apply();
        return map;
    }

    private static Color32 Marker(RaidSiteKind kind) => kind switch
    {
        RaidSiteKind.Side => new Color32(255, 150, 40, 255),
        RaidSiteKind.Apex => new Color32(150, 0, 0, 255),
        RaidSiteKind.Rumour => new Color32(255, 215, 120, 255),
        _ => RaidMapMarkers.ColorOf(kind),
    };

    private static void Dot(Texture2D map, Vector2 position, int pixels, Color32 colour)
    {
        int cx = Mathf.RoundToInt(position.x * pixels);
        int cy = Mathf.RoundToInt(position.y * pixels);
        int radius = Mathf.Max(2, pixels / 64);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius && cx + x >= 0 && cy + y >= 0 && cx + x < pixels && cy + y < pixels)
                {
                    map.SetPixel(cx + x, cy + y, colour);
                }
            }
        }
    }

    /// <summary>Writes a sheet of maps to a PNG, for looking at a lot of them at once.</summary>
    public static void WriteSheet(RaidLayoutRules rules, string path, int firstSeed, int columns, int rows)
    {
        int cell = MapPixels + 4;
        var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
        for (int i = 0; i < columns * rows; i++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, firstSeed + i);
            Texture2D map = Draw(plan, rules, MapPixels);
            sheet.SetPixels(i % columns * cell, (rows - 1 - i / columns) * cell, MapPixels, MapPixels, map.GetPixels());
            DestroyImmediate(map);
        }

        sheet.Apply();
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        DestroyImmediate(sheet);
    }
}
