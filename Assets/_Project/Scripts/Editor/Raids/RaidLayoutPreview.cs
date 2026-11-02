using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A row of maps from a raid's Layout Rules, with a legend, that redraws when the rules change. Shared by the Layout Preview
/// window and the raid editor's World tab. Each map costs tens of milliseconds, so it waits for a slider to be let go.
/// </summary>
internal sealed class RaidMapSheet
{
    private const float MaxMap = 300f;
    private const float MinMap = 150f;
    private const float CaptionHeight = 30f;

    private static readonly (string Label, Color Colour)[] Legend =
    {
        ("Start", RaidMapMarkers.ColorOf(RaidSiteKind.PlayerStart)),
        ("Exit", RaidMapMarkers.ColorOf(RaidSiteKind.Extraction)),
        ("Camp", RaidMapMarkers.ColorOf(RaidSiteKind.EnemyCamp)),
        ("Resources", RaidMapMarkers.ColorOf(RaidSiteKind.ResourceSite)),
        ("Cache", RaidMapMarkers.ColorOf(RaidSiteKind.SupplyCache)),
        ("Rumour", RaidLayoutPreview.Marker(RaidSiteKind.Rumour)),
        ("Water", RaidLayoutPreview.WaterColour),
    };

    private readonly int count;
    private readonly List<Texture2D> maps = new List<Texture2D>();
    private readonly List<string> captions = new List<string>();
    private RaidLayoutRules drawn;
    private int drawnChanges = -1;
    private int firstSeed = 1;
    private float width = 600f;

    public RaidMapSheet(int count)
    {
        this.count = count;
    }

    public void Draw(RaidLayoutRules rules, float size)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Seed", EditorStyles.miniLabel, GUILayout.Width(30f));
            int seed = EditorGUILayout.DelayedIntField(firstSeed, GUILayout.Width(64f));
            if (GUILayout.Button(new GUIContent("New Seeds", "See other runs from the same rules."), EditorStyles.miniButton, GUILayout.Width(80f)))
            {
                seed = firstSeed + count;
            }

            if (seed != firstSeed)
            {
                firstSeed = seed;
                drawnChanges = -1;
            }

            GUILayout.FlexibleSpace();
            DrawLegend();
        }

        // Only once the mouse is let go: a slider being dragged would otherwise redraw every map every frame.
        int changes = EditorUtility.GetDirtyCount(rules);
        bool stale = rules != drawn || changes != drawnChanges || maps.Count != count || maps.Contains(null);
        if (stale && GUIUtility.hotControl == 0 && Event.current.type == EventType.Layout)
        {
            Rebuild(rules, size);
            drawn = rules;
            drawnChanges = changes;
        }

        if (maps.Count != count)
        {
            return;
        }

        float cell = Mathf.Clamp(width / count - 4f, MinMap, MaxMap);
        int columns = Mathf.Clamp(Mathf.FloorToInt(width / (cell + 4f)), 1, count);
        int rows = Mathf.CeilToInt((float)count / columns);
        Rect area = GUILayoutUtility.GetRect(0f, rows * (cell + CaptionHeight), GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            width = Mathf.Max(area.width, 1f);
        }

        var captionStyle = ContentUtility.WrappedMiniLabel;
        for (int i = 0; i < count; i++)
        {
            float x = area.x + i % columns * (cell + 4f);
            float y = area.y + i / columns * (cell + CaptionHeight);
            var picture = new Rect(x, y, cell, cell);
            if (maps[i] != null)
            {
                GUI.DrawTexture(picture, maps[i], ScaleMode.StretchToFill);
            }

            GUI.Label(new Rect(x, picture.yMax, cell, CaptionHeight), new GUIContent(captions[i], $"Seed {firstSeed + i}"), captionStyle);
        }
    }

    private static void DrawLegend()
    {
        foreach ((string label, Color colour) in Legend)
        {
            Rect swatch = GUILayoutUtility.GetRect(9f, 9f, GUILayout.Width(9f), GUILayout.Height(9f));
            swatch.y += 4f;
            EditorGUI.DrawRect(swatch, colour);
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        }
    }

    private void Rebuild(RaidLayoutRules rules, float size)
    {
        Clear();
        for (int i = 0; i < count; i++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, size, firstSeed + i);
            maps.Add(RaidLayoutPreview.Draw(plan, rules, RaidLayoutPreview.MapPixels));
            captions.Add($"{plan.Shape.shape}, {plan.Mood.name}\n{plan.Rooms.Count} clearings, {plan.Links.Count} trails, {plan.Ponds.Count} ponds");
        }
    }

    public void Clear()
    {
        foreach (Texture2D map in maps)
        {
            if (map != null)
            {
                Object.DestroyImmediate(map);
            }
        }

        maps.Clear();
        captions.Clear();
    }
}

/// <summary>
/// Shows what a raid's Layout Rules make, without starting a raid: a sheet of maps from new seeds, with the ground shaded by
/// height, trails and clearings in soil colours, water in blue, and each kind of site in its marker colour. Safe Haven › Raids ›
/// Layout Preview. Pick a raid, or any rules asset, and tune it in the raid editor's World tab: the maps follow.
/// </summary>
public sealed class RaidLayoutPreview : EditorWindow
{
    internal const int MapPixels = 256;
    internal static readonly Color WaterColour = new Color(0.18f, 0.42f, 0.72f);
    private const int Maps = 6;

    private readonly RaidMapSheet sheet = new RaidMapSheet(Maps);
    private RaidDefinition raid;
    private RaidLayoutRules rules;
    private Vector2 scroll;

    [MenuItem("Safe Haven/Raids/Layout Preview")]
    private static void Open() => GetWindow<RaidLayoutPreview>("Raid Layouts");

    private void OnEnable()
    {
        if (rules != null)
        {
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var found = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (found != null && found.LayoutRules != null)
            {
                raid = found;
                rules = found.LayoutRules;
                break;
            }
        }
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        raid = (RaidDefinition)EditorGUILayout.ObjectField(new GUIContent("Raid", "Whose rules to show."), raid, typeof(RaidDefinition), false);
        if (EditorGUI.EndChangeCheck() && raid != null)
        {
            rules = raid.LayoutRules;
        }

        EditorGUI.BeginChangeCheck();
        rules = (RaidLayoutRules)EditorGUILayout.ObjectField(new GUIContent("Layout Rules"), rules, typeof(RaidLayoutRules), false);
        if (EditorGUI.EndChangeCheck() && (raid == null || raid.LayoutRules != rules))
        {
            raid = null;
        }

        if (rules == null)
        {
            EditorGUILayout.HelpBox("Pick a raid with Layout Rules, or a rules asset.", MessageType.Info);
            return;
        }

        if (raid != null && GUILayout.Button("Edit in the Raid Editor", EditorStyles.miniButton, GUILayout.Width(150f)))
        {
            RaidSection.ShowWorld(raid);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        var map = raid != null ? RaidWorldEditor.FromScene<RaidMapDefinition>(RaidSection.FindScenePath(raid.SceneName)) : null;
        sheet.Draw(rules, map != null ? map.Size : 200f);
        EditorGUILayout.EndScrollView();
    }

    private void OnDisable() => sheet.Clear();

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
                if (plan.InWater(new Vector2(u * plan.Size, v * plan.Size)))
                {
                    colours[y * pixels + x] = WaterColour;
                    continue;
                }

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

    internal static Color32 Marker(RaidSiteKind kind) => kind switch
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
    public static void WriteSheet(RaidLayoutRules rules, string path, int firstSeed, int columns, int rows, float size = 200f)
    {
        int cell = MapPixels + 4;
        var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
        for (int i = 0; i < columns * rows; i++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, size, firstSeed + i);
            Texture2D map = Draw(plan, rules, MapPixels);
            sheet.SetPixels(i % columns * cell, (rows - 1 - i / columns) * cell, MapPixels, MapPixels, map.GetPixels());
            DestroyImmediate(map);
        }

        sheet.Apply();
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        DestroyImmediate(sheet);
    }
}
