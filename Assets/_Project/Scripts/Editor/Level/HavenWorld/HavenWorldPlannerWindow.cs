using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Safe Haven › World › Haven World Planner: the Haven world's recipe on the left, the world it plans drawn as a map on the
/// right (HavenWorldPreview), and what is in it counted underneath. Changing a number replans in about a second, so a world
/// is tuned by eye (how many cliffs, where the bog lies, how far a walk goes with nothing on it) before the minutes it takes
/// to generate one. Seeds can be rolled until one pleases; Keep This Seed writes it to the recipe. Point at the map to read a
/// spot: its bench, its height, how far from the portal it is and what stands there. Nothing here touches the scene until
/// Generate World is pressed.
/// </summary>
public sealed class HavenWorldPlannerWindow : EditorWindow
{
    private const int PictureSize = 768;
    private const float PanelWidth = 380f;

    private HavenWorldRecipe recipe;
    private Editor recipeEditor;
    private HavenWorldPlan plan;
    private Texture2D picture;
    private string stats = "";
    private int seed;
    private HavenWorldPreview.Layers layers = HavenWorldPreview.Layers.All & ~HavenWorldPreview.Layers.Mist;
    private Vector2 scroll;
    private double replanAt = -1.0;
    private long lastMilliseconds;

    [MenuItem("Safe Haven/World/Haven World Planner")]
    public static void Open() => GetWindow<HavenWorldPlannerWindow>("Haven World Planner").minSize = new Vector2(900f, 560f);

    private void OnEnable()
    {
        recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        seed = recipe != null ? recipe.seed : 1;
        Replan();
    }

    private void OnDisable()
    {
        DestroyImmediate(picture);
        DestroyImmediate(recipeEditor);
    }

    private void Update()
    {
        if (replanAt > 0.0 && EditorApplication.timeSinceStartup >= replanAt)
        {
            Replan();
            Repaint();
        }
    }

    private void Replan()
    {
        replanAt = -1.0;
        if (recipe == null)
        {
            plan = null;
            stats = "No recipe.";
            return;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        plan = HavenWorldPlan.Make(recipe, seed);
        DestroyImmediate(picture);
        picture = HavenWorldPreview.Texture(plan, PictureSize, layers);
        stats = HavenWorldPreview.Stats(plan);
        lastMilliseconds = clock.ElapsedMilliseconds;
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PanelWidth)))
            {
                DrawPanel();
            }

            DrawMap(GUILayoutUtility.GetRect(100f, 4000f, 100f, 4000f));
        }
    }

    private void DrawPanel()
    {
        EditorGUI.BeginChangeCheck();
        recipe = (HavenWorldRecipe)EditorGUILayout.ObjectField("Recipe", recipe, typeof(HavenWorldRecipe), false);
        if (EditorGUI.EndChangeCheck())
        {
            DestroyImmediate(recipeEditor);
            seed = recipe != null ? recipe.seed : seed;
            Replan();
        }

        if (recipe == null)
        {
            EditorGUILayout.HelpBox($"Pick a Haven World Recipe (the Haven's own is at {HavenWorldGenerator.RecipePath}).", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            seed = EditorGUILayout.DelayedIntField(new GUIContent("Seed", "The same recipe and seed always plan the same world"), seed);
            if (GUILayout.Button(new GUIContent("Roll", "Another world from the same recipe"), GUILayout.Width(44f)))
            {
                seed = Random.Range(1, 100000);
                GUI.changed = true;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Replan();
            }

            using (new EditorGUI.DisabledScope(seed == recipe.seed))
            {
                if (GUILayout.Button(new GUIContent("Keep This Seed", "Write this seed to the recipe: Generate World makes this world"), GUILayout.Width(110f)))
                {
                    Undo.RecordObject(recipe, "Keep Haven World Seed");
                    recipe.seed = seed;
                    EditorUtility.SetDirty(recipe);
                }
            }
        }

        EditorGUI.BeginChangeCheck();
        layers = (HavenWorldPreview.Layers)EditorGUILayout.EnumFlagsField(new GUIContent("Show", "Problems: ground nothing climbs to (red hatching), walks that pass nothing (red lines), " +
                                                                                               "paths that end at nothing (red crosses), empty ground (pink dots)"), layers);
        if (EditorGUI.EndChangeCheck() && plan != null)
        {
            DestroyImmediate(picture);
            picture = HavenWorldPreview.Texture(plan, PictureSize, layers);
        }

        EditorGUILayout.HelpBox(stats + $"\n(planned and drawn in {lastMilliseconds} ms)", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Replan"))
            {
                Replan();
            }

            if (GUILayout.Button(new GUIContent("Save Picture…", "The map as a PNG, to share or compare")))
            {
                string path = EditorUtility.SaveFilePanel("Save the plan's picture", "", $"Haven World seed {seed}.png", "png");
                if (!string.IsNullOrEmpty(path) && picture != null)
                {
                    File.WriteAllBytes(path, picture.EncodeToPNG());
                }
            }

            using (new EditorGUI.DisabledScope(seed != recipe.seed))
            {
                if (GUILayout.Button(new GUIContent("Generate World…", seed != recipe.seed ? "Keep This Seed first: the world is generated from the recipe's seed"
                                                                                           : "Turns the recipe into the Haven's world (asks first; several minutes)")))
                {
                    EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("Safe Haven/World/Generate Haven World");
                }
            }
        }

        Legend();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (recipeEditor == null || recipeEditor.target != recipe)
        {
            DestroyImmediate(recipeEditor);
            recipeEditor = Editor.CreateEditor(recipe);
        }

        EditorGUI.BeginChangeCheck();
        recipeEditor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            // Once the typing or dragging has stopped for a moment.
            replanAt = EditorApplication.timeSinceStartup + 0.6;
        }

        EditorGUILayout.EndScrollView();
    }

    private static void Legend()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Swatch(new Color32(238, 84, 204, 255), "place");
            Swatch(HavenWorldPreview.GladeColour(HavenWorldPlan.GladeKind.Meadow), "meadow glade");
            Swatch(HavenWorldPreview.GladeColour(HavenWorldPlan.GladeKind.Rocky), "rocky");
            Swatch(HavenWorldPreview.GladeColour(HavenWorldPlan.GladeKind.Lair), "lair");
            Swatch(HavenWorldPreview.GladeColour(HavenWorldPlan.GladeKind.Wet), "wet");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            Swatch(new Color32(232, 62, 50, 255), "creatures");
            Swatch(new Color32(96, 232, 132, 255), "game");
            Swatch(new Color32(40, 32, 30, 255), "cliff with rock");
            Swatch(new Color32(214, 190, 120, 255), "scramble");
            Swatch(Color.white, "fall");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            Swatch(HavenWorldPreview.NodeColour(ResourceNodeKind.Rock), "stone");
            Swatch(HavenWorldPreview.NodeColour(ResourceNodeKind.Wood), "wood");
            Swatch(HavenWorldPreview.NodeColour(ResourceNodeKind.Forage), "forage");
            Swatch(HavenWorldPreview.NodeColour(ResourceNodeKind.Plant), "plants");
            Swatch(new Color32(196, 120, 60, 255), "grove");
        }
    }

    private static void Swatch(Color colour, string label)
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 14f, GUILayout.Width(10f));
        rect.y += 3f;
        rect.height = 10f;
        EditorGUI.DrawRect(rect, colour);
        GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        GUILayout.Space(4f);
    }

    private void DrawMap(Rect area)
    {
        if (picture == null || plan == null)
        {
            return;
        }

        float side = Mathf.Min(area.width, area.height);
        var rect = new Rect(area.x + (area.width - side) * 0.5f, area.y + (area.height - side) * 0.5f, side, side);
        GUI.DrawTexture(rect, picture, ScaleMode.StretchToFill, false);
        Event current = Event.current;
        if (!rect.Contains(current.mousePosition))
        {
            return;
        }

        // North is up: the picture's rows run south to north, the window's top to bottom.
        float size = plan.Recipe.size;
        var point = new Vector2((current.mousePosition.x - rect.x) / side * size - plan.Half, (1f - (current.mousePosition.y - rect.y) / side) * size - plan.Half);
        string what = What(point);
        string text = $"{point.x:0}, {point.y:0}   {point.magnitude:0} m from the portal, bearing {HavenWorldPlan.BearingOf(point):0}°\n" +
                      $"bench {plan.Tier(point)}, {plan.Height(point):0.0} m" + (plan.Reachable(point) ? "" : ", cannot be walked to") + (what.Length > 0 ? "\n" + what : "");
        Vector2 box = EditorStyles.helpBox.CalcSize(new GUIContent(text));
        var label = new Rect(Mathf.Min(current.mousePosition.x + 14f, rect.xMax - box.x - 4f), Mathf.Min(current.mousePosition.y + 14f, rect.yMax - box.y - 4f), box.x + 4f, box.y);
        EditorGUI.DrawRect(label, new Color(0f, 0f, 0f, 0.75f));
        GUI.Label(label, text, EditorStyles.whiteLabel);
        if (current.type == EventType.MouseMove)
        {
            Repaint();
        }

        wantsMouseMove = true;
    }

    // What stands at a point, in words.
    private string What(Vector2 point)
    {
        foreach (HavenWorldPlan.Poi poi in plan.Pois)
        {
            if (Vector2.Distance(poi.At, point) < poi.Rule.clearing + 6f)
            {
                return poi.Rule.name;
            }
        }

        foreach (HavenWorldPlan.Zone zone in plan.Zones)
        {
            if (Vector2.Distance(zone.At, point) < zone.Radius)
            {
                return (zone.Band.peaceful ? "Game: " : "Creatures: ") + zone.Band.name;
            }
        }

        foreach (HavenWorldPlan.Glade glade in plan.Glades)
        {
            if (glade.Open(point) > 0.3f)
            {
                return glade.Kind + " glade";
            }
        }

        for (int i = 0; i < plan.Meres.Count; i++)
        {
            if (plan.Meres[i].Edge(point) < 0f)
            {
                return plan.MereNames[i];
            }
        }

        return plan.Lake.Edge(point) < 0f ? "Lake" : plan.BogWeight(point) > 0.5f ? "Bog" : plan.MountainWeight(point) > 0.5f ? "Mountain" : "";
    }
}
