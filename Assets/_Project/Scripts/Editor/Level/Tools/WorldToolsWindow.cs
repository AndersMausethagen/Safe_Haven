using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One place for building the world (Safe Haven › World Tools, or the Scene view's "Safe Haven World" overlay):
/// <list type="bullet">
/// <item>Place: every level prefab (rocks, trees, forest floor, resources, buildings, raid pieces, creatures) as a
/// picture, searchable; click one and click in the Scene view to place it on the ground, or drag it in.</item>
/// <item>Build: the World Builder, by look (the Haven's Forest Lake or the raids' Mossy Forest): draw rivers, paths,
/// cliffs, ravines, forests and ground paint; the look's biomes (draw an area or brush them on); build; every feature in
/// the scene.</item>
/// <item>Ground: sculpting, the terrain's look, auto-paint and ground layers to paint with, relief, NavMesh.</item>
/// <item>Water and Plants, Sky and Weather: the water, grass, moss, day/night, weather and fog tools.</item>
/// <item>Scene: make the scene playable as a home or a raid, and what is missing.</item>
/// </list>
/// The buttons run the same menu items as the Safe Haven › World and Safe Haven › Scene menus.
/// </summary>
public sealed class WorldToolsWindow : EditorWindow
{
    public enum Tab
    {
        Place = 0,
        Build = 1,
        Ground = 2,
        WaterAndPlants = 3,
        SkyAndWeather = 4,
        Scene = 5,
    }

    private static readonly string[] TabNames = { "Place", "Build", "Ground", "Water & Plants", "Sky", "Scene" };

    /// <summary>Every menu item a button here runs (checked by WorldToolsTests, so a renamed menu is caught).</summary>
    public static readonly string[] MenuPaths =
    {
        "Safe Haven/World/Place/Scatter Brush", "Safe Haven/World/Place/Drop Selection to Ground _END",
        "Safe Haven/World/Place/Drop and Align to Slope #_END", "Safe Haven/World/Place/Drop with Physics %_END",
        "Safe Haven/World/Terrain/Add Gentle Relief...", "Safe Haven/World/Terrain/Add Moss to Selected Terrain",
        "Safe Haven/World/Haven World Planner", "Safe Haven/World/Generate Haven World",
        "Safe Haven/World/Build Now", "Safe Haven/World/Rebuild Everything", "Safe Haven/World/Give Hand Paint Back to Features",
        "Safe Haven/World/Terrain Surface", "Safe Haven/World/Mesh Surface",
        "Safe Haven/World/Water/Create Pond", "Safe Haven/World/Water/Create River Section", "Safe Haven/World/Water/Create Waterfall...",
        "Safe Haven/World/Water/Make Selected Still Water", "Safe Haven/World/Water/Make Selected Flowing Water",
        "Safe Haven/World/Water/Make Selected Disturb Water", "Safe Haven/World/Water/Carve Terrain Under Selected Water...",
        "Safe Haven/World/Grass and Plants/Add Grass and Bushes to Selected Terrain", "Safe Haven/World/Grass and Plants/Fill Selected Terrain With Grass...",
        "Safe Haven/World/Grass and Plants/Clear Grass Under Water",
        "Safe Haven/World/Grass and Plants/Make Selected Push Grass",
        "Safe Haven/World/Sky and Weather/Sky Controller", "Safe Haven/World/Sky and Weather/Add Day, Night and Weather to Scene",
        "Safe Haven/World/Sky and Weather/Add Shared Moss Wind", "Safe Haven/World/Sky and Weather/Fog/Set Up Open Scene",
        "Safe Haven/World/Sky and Weather/Fog/Set Up Fog",
        "Safe Haven/World/Sky and Weather/Fog/Set Up Light Shafts",
        "Safe Haven/World/Sky and Weather/Fog/Add Drifting Mist to Selected Terrain",
        "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Clear (no fog)", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Morning Mist",
        "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Ground Blanket", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Misty Woods",
        "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Marsh", "GameObject/Safe Haven/World/Fog Area",
        "Safe Haven/Scene/Level Check", "Safe Haven/Scene/Assign Missing Save Ids", "Safe Haven/Scene/Tidy Hierarchy",
        "Safe Haven/Scene/Play Haven (Isolated Saves)", "Safe Haven/Scene/Play Prologue (Isolated Saves)",
    };

    private Tab tab;
    private string search = "";
    private string category = "All";
    private string set = "All";
    private static string groundSet = "Forest Lake";
    private static Vector2 groundScroll;
    private Vector2 scroll;
    private ScenePlayability.Kind kind;
    private List<ScenePlayability.Issue> issues;
    private string sceneReport = "";
    private Editor fogEditor;
    private Editor mistEditor;
    private AtmosphericFog atmosphereTarget;
    private bool showFogSettings;
    private bool showMistSettings;

    [MenuItem("Safe Haven/World Tools", false, 0)]
    private static void OpenMenu() => Open(Tab.Place);

    public static WorldToolsWindow Open(Tab tab)
    {
        var window = GetWindow<WorldToolsWindow>("World Tools");
        window.minSize = new Vector2(340f, 360f);
        window.tab = tab;
        if (tab == Tab.Scene)
        {
            window.RunCheck();
        }

        window.Show();
        return window;
    }

    private void OnEnable()
    {
        kind = ScenePlayability.Detect(SceneManager.GetActiveScene()) ?? ScenePlayability.Kind.SafeHaven;
        EditorApplication.projectChanged += PrefabCatalog.Refresh;
        EditorApplication.projectChanged += ForgetBiomes;
    }

    private void OnSelectionChange() => Repaint(); // the selection decides what several tabs offer

    private void OnDisable()
    {
        EditorApplication.projectChanged -= PrefabCatalog.Refresh;
        EditorApplication.projectChanged -= ForgetBiomes;
        if (fogEditor != null) DestroyImmediate(fogEditor);
        if (mistEditor != null) DestroyImmediate(mistEditor);
    }

    private void OnGUI()
    {
        tab = (Tab)GUILayout.Toolbar((int)tab, TabNames);
        EditorGUILayout.Space(4f);
        switch (tab)
        {
            case Tab.Place:
                DrawPlace();
                break;
            case Tab.Build:
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawBuild();
                EditorGUILayout.EndScrollView();
                break;
            case Tab.Ground:
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawGround();
                EditorGUILayout.EndScrollView();
                break;
            case Tab.WaterAndPlants:
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawWaterAndPlants();
                EditorGUILayout.EndScrollView();
                break;
            case Tab.SkyAndWeather:
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawSky();
                EditorGUILayout.EndScrollView();
                break;
            case Tab.Scene:
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawScene();
                EditorGUILayout.EndScrollView();
                break;
        }
    }

    // ------------------------------------------------------------------ Place

    private void DrawPlace()
    {
        DrawPlacementSettings();
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (GUILayout.Button(new GUIContent("↻", "Look for new prefabs"), GUILayout.Width(24f)))
            {
                PrefabCatalog.Refresh();
            }
        }

        string[] categories = new[] { "All" }.Concat(PrefabCatalog.Categories).ToArray();
        int index = Mathf.Max(0, Array.IndexOf(categories, category));
        index = GUILayout.SelectionGrid(index, categories, 3, EditorStyles.miniButton);
        category = categories[index];
        string[] sets = new[] { "All" }.Concat(PrefabCatalog.Sets).ToArray();
        int setIndex = Mathf.Max(0, Array.IndexOf(sets, set));
        setIndex = GUILayout.Toolbar(setIndex, sets, EditorStyles.miniButton);
        set = sets[setIndex];

        List<PrefabCatalog.Entry> shown = PrefabCatalog.All
            .Where(e => category == "All" || e.Category == category)
            .Where(e => set == "All" || e.Set == set)
            .Where(e => string.IsNullOrWhiteSpace(search) || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        EditorGUILayout.LabelField($"{shown.Count} prefab(s): click one, then click in the Scene view. Or drag it in.", EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        const float tile = 84f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / (tile + 6f)));
        bool loading = false;
        for (int i = 0; i < shown.Count; i += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int c = 0; c < columns && i + c < shown.Count; c++)
                {
                    loading |= DrawTile(shown[i + c], tile);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Scatter Brush", "Safe Haven/World/Place/Scatter Brush", "Paint many objects from a palette");
            MenuButton("Drop Selection", "Safe Haven/World/Place/Drop Selection to Ground _END", "Set the selected objects on the ground (End)");
            MenuButton("Drop + Align", "Safe Haven/World/Place/Drop and Align to Slope #_END", "...and tilt them with the slope (Shift+End)");
            MenuButton("Physics Drop", "Safe Haven/World/Place/Drop with Physics %_END", "Let them fall and settle naturally (Ctrl+End). A selected folder drops everything in it");
        }

        if (loading)
        {
            Repaint(); // previews are still being rendered
        }
    }

    /// <summary>Draws one prefab; returns true while its picture is still loading.</summary>
    private static bool DrawTile(PrefabCatalog.Entry entry, float size)
    {
        Texture preview = AssetPreview.GetAssetPreview(entry.Prefab);
        bool loading = preview == null && AssetPreview.IsLoadingAssetPreview(entry.Prefab.GetEntityId());
        preview ??= AssetPreview.GetMiniThumbnail(entry.Prefab);
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(size)))
        {
            Rect rect = GUILayoutUtility.GetRect(size, size);
            bool active = PrefabPlacer.Active == entry.Prefab;
            if (active)
            {
                EditorGUI.DrawRect(rect, new Color(0.25f, 0.55f, 0.95f, 0.6f));
            }

            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), preview, ScaleMode.ScaleToFit);
            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (active)
                    {
                        PrefabPlacer.Stop();
                    }
                    else
                    {
                        PrefabPlacer.Start(entry.Prefab);
                    }

                    e.Use();
                }
                else if (e.type == EventType.MouseDrag)
                {
                    // Drag into the Scene view like from the Project window (Stick to Ground sets it down).
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new UnityEngine.Object[] { entry.Prefab };
                    DragAndDrop.StartDrag(entry.Name);
                    e.Use();
                }
            }

            GUILayout.Label(new GUIContent(entry.Name, entry.Path), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(size));
        }

        return loading;
    }

    public static void DrawPlacementSettings()
    {
        PlacementSettings.StickToGround = EditorGUILayout.ToggleLeft(
            new GUIContent("Stick to ground", "Objects dragged in, duplicated or moved with the Move tool settle on the ground"),
            PlacementSettings.StickToGround);
        using (new EditorGUILayout.HorizontalScope())
        {
            PlacementSettings.AlignToSlope = EditorGUILayout.ToggleLeft(new GUIContent("Align to slope", "Tilt with the ground"),
                                                                        PlacementSettings.AlignToSlope, GUILayout.Width(120f));
            PlacementSettings.RandomTurn = EditorGUILayout.ToggleLeft(new GUIContent("Random turn", "Each placed object faces a new way"),
                                                                      PlacementSettings.RandomTurn, GUILayout.Width(110f));
        }

        Vector2 scale = PlacementSettings.ScaleRange;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(new GUIContent("Size", "Random size of each placed object"), GUILayout.Width(36f));
            EditorGUILayout.MinMaxSlider(ref scale.x, ref scale.y, 0.3f, 2f);
            EditorGUILayout.LabelField($"{scale.x:0.00}–{scale.y:0.00}", GUILayout.Width(70f));
        }

        PlacementSettings.ScaleRange = scale;
        PlacementSettings.Sink = EditorGUILayout.Slider(new GUIContent("Sink", "Metres sunk into the ground"), PlacementSettings.Sink, 0f, 0.5f);
    }

    // ------------------------------------------------------------------ the look tabs (Build and Ground)

    // The look the Build and Ground tabs work in; first the look of the terrain in view.
    private static WorldLook look;
    private static readonly Dictionary<WorldLook, List<BiomePreset>> Biomes = new Dictionary<WorldLook, List<BiomePreset>>();
    private static bool showFeatures = true;
    private static bool showReport;
    private static Vector2 featureScroll;

    private static void ForgetBiomes() => Biomes.Clear();

    private static WorldLook DrawLookTabs(Terrain terrain)
    {
        IReadOnlyList<WorldLook> looks = WorldLook.All;
        look ??= WorldLook.Of(terrain);
        int index = 0;
        for (int i = 0; i < looks.Count; i++)
        {
            if (looks[i] == look) index = i;
        }

        index = GUILayout.Toolbar(index, looks.Select(l => new GUIContent(LookLabel(l), LookTip(l))).ToArray(), GUILayout.Height(24f));
        look = looks[index];
        return look;
    }

    private static string LookLabel(WorldLook l) =>
        l == WorldLook.ForestLake ? "Haven  ·  Forest Lake" : l == WorldLook.MossyForest ? "Dark Forest  ·  Mossy Forest" : l.Name;

    private static string LookTip(WorldLook l) =>
        l == WorldLook.ForestLake ? "The light, green Haven: lake crags, maples, meadows, trodden tracks"
        : l == WorldLook.MossyForest ? "The raids' dark forest: cedars, moss, cliff boulders, bare soil paths" : l.Name;

    // ------------------------------------------------------------------ Build

    private void DrawBuild()
    {
        Terrain terrain = FocusTerrain();
        WorldLook current = DrawLookTabs(terrain);
        bool haven = current == WorldLook.ForestLake;

        Section("Draw", "Click points on the ground, Enter to finish; it builds as you edit (drag knots, widths, heights)");
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawButton("River", haven ? "Rocky bed, banks, lake stones and ferns" : "Rocky bed, earthy banks, mossy stones and ferns", () => WorldMenus.MakeRiver(current));
            DrawButton("Path", haven ? "A trodden Forest Lake track" : "A Mossy Forest path of bare soil", () => WorldMenus.MakePath(current));
            DrawButton("Cliff", haven ? "A face of lake crags (tessellated)" : "A face of cliff boulders, slabs and outcrops (tessellated)", () => WorldMenus.MakeCliff(current));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawButton("Ravine", "A cut with steep walls on both sides, ledges, moss on the shaded wall, rubble on the floor", () => WorldMenus.MakeRavine(current));
            DrawButton(haven ? "Grove" : "Forest", haven
                ? "Open groves of sugar maples, saplings, logs, lake boulders, ferns by the water"
                : "Dense cedars, young cedars, mossy logs and stumps, ferns, boulders, on the Misty Woods floor", () => WorldMenus.MakeForest(current));
            if (GUILayout.Button(new GUIContent("Ground Paint ▾", "A loop painted by a set of paint rules (steepness, height, hollows)"), GUILayout.Height(24f)))
            {
                var menu = new GenericMenu();
                foreach (string rules in current.PaintLooks)
                {
                    menu.AddItem(new GUIContent(rules), false, () => WorldMenus.MakeGroundPaint(current, rules));
                }

                menu.ShowAsContext();
            }
        }

        DrawBiomes(current);
        DrawBuildControls(terrain);
        DrawFeatureList();
    }

    private static void DrawButton(string label, string tooltip, Func<WorldFeature> create)
    {
        if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(24f)))
        {
            create();
            GUIUtility.ExitGUI(); // the selection changed under this layout
        }
    }

    /// <summary>The look's biomes: each paints its ground, plants and props over a drawn loop, or by hand with the brush.</summary>
    private static void DrawBiomes(WorldLook current)
    {
        Section("Biomes", "Ground, plants and props that belong together (Data/Level/Biomes)");
        if (!Biomes.TryGetValue(current, out List<BiomePreset> biomes) || biomes.Any(b => b == null))
        {
            Biomes[current] = biomes = WorldMenus.BiomesFor(current);
        }

        foreach (BiomePreset biome in biomes)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(biome.name, biome.description), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Draw Area", "Draw a loop: the World Builder paints it with this biome and keeps it painted as you edit"),
                                         EditorStyles.miniButtonLeft, GUILayout.Width(70f)))
                    {
                        WorldMenus.MakeBiomeArea(biome);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button(new GUIContent("Brush", "Paint it by hand in the Scene view (Shift erases)"), EditorStyles.miniButtonMid, GUILayout.Width(48f)))
                    {
                        BiomeBrush.Open(biome);
                    }

                    if (GUILayout.Button(new GUIContent("Edit", "Its ground, plants and props (areas painted with it rebuild)"), EditorStyles.miniButtonRight, GUILayout.Width(38f)))
                    {
                        Selection.activeObject = biome;
                    }
                }

                if (!string.IsNullOrWhiteSpace(biome.description))
                {
                    GUILayout.Label(biome.description, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        if (GUILayout.Button(new GUIContent("New Biome…", $"A new biome preset for the {current.Name} look, to fill in")))
        {
            BiomePreset made = BiomeBrush.CreatePreset("New " + current.Name + " Biome");
            made.look = current.Name;
            EditorUtility.SetDirty(made);
            AssetDatabase.SaveAssetIfDirty(made);
            Biomes.Clear();
            Selection.activeObject = made;
        }
    }

    private static void DrawBuildControls(Terrain terrain)
    {
        Section("Build");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Build Now", "Safe Haven/World/Build Now", "Build whatever changed");
            MenuButton("Rebuild Everything", "Safe Haven/World/Rebuild Everything", "Every terrain, feature and area from scratch");
        }

        WorldTerrain world = terrain != null ? terrain.GetComponent<WorldTerrain>() : null;
        if (world != null)
        {
            var serialized = new SerializedObject(world);
            SerializedProperty automatic = serialized.FindProperty("buildAutomatically");
            if (automatic != null)
            {
                automatic.boolValue = EditorGUILayout.ToggleLeft(new GUIContent($"Build {terrain.name} as I edit",
                    "Off: features on this terrain wait for Build Now (for very large terrains)"), automatic.boolValue);
                serialized.ApplyModifiedProperties();
            }

            int hand = world.Stash != null ? world.Stash.HandPaintedCount + world.Stash.HandKeptDetailCount : 0;
            if (hand > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent($"{hand} cell(s) painted by hand win over the features",
                        "Ground painted and grass grown or cleared by hand where a feature paints stay as they are through every build"), EditorStyles.miniLabel);
                    MenuButton("Give Back", "Safe Haven/World/Give Hand Paint Back to Features",
                        "Let the features paint them again: under the selected features, or everywhere", 80f);
                }
            }
        }

        if (!string.IsNullOrEmpty(WorldBuilder.LastReport))
        {
            showReport = EditorGUILayout.Foldout(showReport, "Last build", true);
            if (showReport)
            {
                EditorGUILayout.LabelField(WorldBuilder.LastReport, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    /// <summary>Every World Builder feature in the active scene: its kind (in its outline colour), on or off, select it.</summary>
    private static void DrawFeatureList()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<WorldFeature> features = Object.FindObjectsByType<WorldFeature>(FindObjectsInactive.Include)
            .Where(f => f.gameObject.scene == scene)
            .OrderBy(f => f.Order).ThenBy(f => f.name).ToList();
        EditorGUILayout.Space(6f);
        showFeatures = EditorGUILayout.Foldout(showFeatures, $"Features in {scene.name} ({features.Count})", true, EditorStyles.foldoutHeader);
        if (!showFeatures)
        {
            return;
        }

        if (features.Count == 0)
        {
            EditorGUILayout.LabelField("None yet: draw one above.", EditorStyles.miniLabel);
            return;
        }

        featureScroll = EditorGUILayout.BeginScrollView(featureScroll, GUILayout.MaxHeight(240f));
        foreach (WorldFeature feature in features)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect swatch = GUILayoutUtility.GetRect(10f, 18f, GUILayout.Width(10f));
                EditorGUI.DrawRect(new Rect(swatch.x + 1f, swatch.y + 4f, 7f, 10f), WorldFeatureEditor.ColourOf(feature));
                bool on = feature.gameObject.activeSelf && feature.enabled;
                bool now = EditorGUILayout.Toggle(GUIContent.none, on, GUILayout.Width(16f));
                if (now != on)
                {
                    Undo.RecordObject(feature.gameObject, now ? "Turn Feature On" : "Turn Feature Off");
                    feature.gameObject.SetActive(now);
                    if (now && !feature.enabled)
                    {
                        Undo.RecordObject(feature, "Turn Feature On");
                        feature.enabled = true;
                    }

                    WorldBuilder.Schedule(0.1);
                }

                bool selected = Selection.activeGameObject == feature.gameObject;
                string detail = KindOf(feature) + (feature is BiomeArea area && area.Biome != null ? " · " + area.Biome.name : "");
                bool shapeless = feature.Spline == null || feature.Spline.Count < (feature.IsArea ? 3 : 2);
                GUILayout.Label(new GUIContent(feature.name, detail + (shapeless ? " (no shape yet: select it and press Draw)" : "")),
                                selected ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.MinWidth(60f));
                GUILayout.FlexibleSpace();
                GUILayout.Label(shapeless ? "no shape" : detail, EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("Select", "Select it and frame it in the Scene view"), EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    Selection.activeGameObject = feature.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static string KindOf(WorldFeature feature) => feature switch
    {
        RiverFeature _ => "River",
        PathFeature _ => "Path",
        CliffFeature _ => "Cliff",
        RavineFeature _ => "Ravine",
        GroundPaintArea _ => "Ground paint",
        BiomeArea area => area.Layers.Count > 0 ? "Forest" : "Biome",
        _ => feature.GetType().Name,
    };

    // ------------------------------------------------------------------ Ground

    private static void DrawGround()
    {
        Terrain terrain = FocusTerrain();
        WorldLook current = DrawLookTabs(terrain);
        EditorGUILayout.LabelField(terrain != null ? $"Terrain in view: {terrain.name} ({WorldLook.Of(terrain).Name} grounds)" : "No terrain in view",
                                   EditorStyles.miniLabel);

        Section("Shape");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Sculpt & Paint", "Select the terrain under the Scene view: its Inspector has Unity's sculpt and paint brushes")))
            {
                SelectTerrain();
            }

            if (GUILayout.Button(new GUIContent("Terrain Toolbox", "Unity's Terrain Tools: noise, erosion, stamps, more brushes")))
            {
                EditorApplication.ExecuteMenuItem("Window/Terrain/Terrain Toolbox");
            }

            MenuButton("Gentle Relief…", "Safe Haven/World/Terrain/Add Gentle Relief...", "Soft rises and dips on flat ground");
        }

        Section("Whole World");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Haven World Planner", "Safe Haven/World/Haven World Planner", "The Haven world's recipe drawn as a map and counted, before anything is generated");
            MenuButton("Generate Haven World…", "Safe Haven/World/Generate Haven World", "Turns the recipe into the Haven's world (asks first)");
        }

        Section("Look");
        using (new EditorGUI.DisabledScope(terrain == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent($"Use the {current.Name} Look", "Tessellated terrain material with the look's rock on steep ground, its grounds and paint rules")))
                {
                    WorldTerrainEditor.UseLook(WorldTerrainOf(terrain), terrain, current);
                }

                if (GUILayout.Button(new GUIContent("Auto-Paint", "Paint the terrain by its shape with its paint rules, then let the features paint over")))
                {
                    WorldTerrain world = WorldTerrainOf(terrain);
                    if (world.PaintRules == null)
                    {
                        WorldTerrainEditor.UseLook(world, terrain, current);
                    }

                    WorldPaint.Apply(terrain, world.PaintRules);
                    WorldBuilder.RebuildTerrain(terrain);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Rock on Steep Ground", "Paint the look's rocky ground on steep slopes and Cliff Rock on the steepest drops " +
                                                                          "(over what is there; paint any ground over them to change them)")))
                {
                    Debug.Log($"{terrain.name}: {TerrainRock.PaintSteepGround(terrain, WorldLook.Of(terrain))}.", terrain);
                }

                MenuButton("Add Moss", "Safe Haven/World/Terrain/Add Moss to Selected Terrain", "Live moss on the terrain's moss layer");
            }
        }

        DrawGroundPainting(terrain, current);
        DrawRelief(terrain);

        Section("Walking");
        if (GUILayout.Button(new GUIContent("Rebake NavMesh", "Monsters and residents walk the ground as it is now")))
        {
            Debug.Log("World Tools: " + WorldBuilder.RebakeNavMesh());
        }
    }

    /// <summary>Every ground layer of a set as a picture: click one to add it to the terrain (tessellated) and paint with it.</summary>
    private static void DrawGroundPainting(Terrain terrain, WorldLook current)
    {
        Section("Paint ground (tessellated)");
        string[] sets = { current.Name, GroundLayerPainter.OriginalSet };
        int setIndex = groundSet == GroundLayerPainter.OriginalSet ? 1 : 0;
        setIndex = GUILayout.Toolbar(setIndex, new[] { current.Name + " grounds", "Other grounds" }, EditorStyles.miniButton);
        groundSet = sets[setIndex];
        EditorGUILayout.LabelField("Click a ground: it joins the terrain if missing (with its relief) and the Paint Texture brush opens on it.",
                                   EditorStyles.wordWrappedMiniLabel);
        List<TerrainLayer> layers = GroundLayerPainter.Layers(groundSet).ToList();
        const float tile = 64f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 30f) / (tile + 6f)));
        groundScroll = EditorGUILayout.BeginScrollView(groundScroll, GUILayout.Height(Mathf.Min(2, Mathf.CeilToInt(layers.Count / (float)columns)) * (tile + 22f) + 6f));
        using (new EditorGUI.DisabledScope(terrain == null))
        {
            for (int i = 0; i < layers.Count; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < columns && i + c < layers.Count; c++)
                    {
                        TerrainLayer layer = layers[i + c];
                        bool onTerrain = terrain != null && terrain.terrainData.terrainLayers.Contains(layer);
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(tile)))
                        {
                            Texture picture = layer.diffuseTexture != null ? layer.diffuseTexture : AssetPreview.GetMiniThumbnail(layer);
                            string tip = layer.name + (onTerrain ? " (on this terrain)" : " (click to add it and paint)");
                            if (GUILayout.Button(new GUIContent(picture, tip), GUILayout.Width(tile), GUILayout.Height(tile)))
                            {
                                GroundLayerPainter.PaintWith(terrain, layer);
                            }

                            GUILayout.Label(new GUIContent((onTerrain ? "• " : "") + layer.name, tip), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(tile));
                        }
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();
        using (new EditorGUI.DisabledScope(terrain == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Refresh Relief", "Rebuild the terrain's displacement from its ground layers (done by itself when layers change)")))
            {
                Debug.Log(GroundLayerPainter.RefreshDisplacement(terrain)
                    ? $"{terrain.name}: displacement rebuilt for {terrain.terrainData.terrainLayers.Length} ground layer(s)."
                    : $"{terrain.name}: not a tessellated world terrain; use a Look first.");
            }

            if (GUILayout.Button(new GUIContent("Layer Palettes", "Make Terrain Tools layer palettes for each look (Paint Texture › Layer Palette)")))
            {
                int made = GroundLayerPainter.EnsureLayerPalettes();
                Debug.Log($"World Tools: {made} layer palette(s) made, in each look's Data/World folder.");
            }
        }
    }

    /// <summary>One live slider for how deep every ground and the steep rock are on the terrain in view.</summary>
    private static void DrawRelief(Terrain terrain)
    {
        Section("Ground relief (tessellated)");
        Material material = terrain != null ? terrain.materialTemplate : null;
        if (material == null || material.shader.name != TerrainDisplacementEditor.ShaderName)
        {
            EditorGUILayout.LabelField("The terrain in view has no tessellated ground (use a Look first).", EditorStyles.miniLabel);
            return;
        }

        float scale = GroundRelief.Scale(material);
        EditorGUI.BeginChangeCheck();
        scale = EditorGUILayout.Slider(new GUIContent("Relief ×", "Deepens every ground and the steep rock together, live. " +
                                                                 "×1: moss 2 cm, soil 3 cm, rock 6 cm, cliffs 8 cm"), scale, 0f, 10f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(material, "Ground Relief");
            GroundRelief.SetScale(material, scale);
            SceneView.RepaintAll();
        }

        EditorGUILayout.LabelField($"Moss {GroundRelief.SoftRelief * scale * 100:0.#} cm · soil {GroundRelief.SoilRelief * scale * 100:0.#} cm · " +
                                   $"rock {GroundRelief.RockRelief * scale * 100:0.#} cm · cliffs {GroundRelief.CliffRelief * scale * 100:0.#} cm", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Recalibrate Grounds", "After adding grounds or changing height maps: puts every ground back in proportion (keeps Relief ×)")))
            {
                Debug.Log("Ground relief: " + GroundRelief.CalibrateAll());
            }

            MenuButton("Per Ground…", "Safe Haven/World/Terrain Surface", "Each ground layer's relief, tiling and wetness on this terrain (Terrain Surface)");
            MenuButton("Meshes…", "Safe Haven/World/Mesh Surface", "Tessellated relief on a selected rock or mesh (Mesh Surface)");
        }
    }

    // ------------------------------------------------------------------ Water and plants, sky and weather

    private static void DrawWaterAndPlants()
    {
        Section("Water", "Winding rivers are World Builder features: Build › River");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Pond", "Safe Haven/World/Water/Create Pond", "A still pond at the middle of the Scene view");
            MenuButton("Straight River", "Safe Haven/World/Water/Create River Section", "A straight piece of flowing water");
            MenuButton("Waterfall…", "Safe Haven/World/Water/Create Waterfall...", "A falling sheet with plunge pool, mist and spray");
        }

        // What works on the selection, only when something is selected.
        GameObject selected = Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject) ? Selection.activeGameObject : null;
        Section(selected != null ? $"Selected: {selected.name}" : "Selected object", "Tools that change the selected object");
        if (selected == null)
        {
            EditorGUILayout.LabelField("Select a mesh to make it water, or a moving object to make it ripple water and bend grass.", EditorStyles.wordWrappedMiniLabel);
        }
        else
        {
            bool mesh = selected.GetComponentInChildren<MeshFilter>() != null;
            using (new EditorGUI.DisabledScope(!mesh))
            using (new EditorGUILayout.HorizontalScope())
            {
                MenuButton("Still Water", "Safe Haven/World/Water/Make Selected Still Water", "Turn the selected mesh into still water");
                MenuButton("Flowing Water", "Safe Haven/World/Water/Make Selected Flowing Water", "Turn the selected mesh into flowing water");
                MenuButton("Carve Bed…", "Safe Haven/World/Water/Carve Terrain Under Selected Water...", "Dig a bed with banks under the selected water");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                MenuButton("Ripples Water", "Safe Haven/World/Water/Make Selected Disturb Water", "The selected object makes ripples and splashes as it moves through water");
                MenuButton("Bends Grass", "Safe Haven/World/Grass and Plants/Make Selected Push Grass", "The selected object bends grass as it moves");
            }
        }

        Terrain terrain = FocusTerrain();
        Section(terrain != null ? $"Plants on {terrain.name}" : "Plants", "Grass and small plants are terrain details");
        using (new EditorGUI.DisabledScope(terrain == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Add Plants ▾", "Give the terrain a look's grass, moss, ferns and flowers to paint with (Paint Details, the Biome Brush)")))
            {
                var menu = new GenericMenu();
                foreach (WorldLook look in WorldLook.All)
                {
                    WorldLook chosen = look;
                    menu.AddItem(new GUIContent($"{look.Name} plants"), false,
                                 () => Debug.Log($"{terrain.name}: {GroundLayerPainter.AddDetails(terrain, chosen)} {chosen.Name} plant kind(s) added."));
                }

                menu.AddItem(new GUIContent("Grass and bushes (the original set)"), false,
                             () => EditorApplication.ExecuteMenuItem("Safe Haven/World/Grass and Plants/Add Grass and Bushes to Selected Terrain"));
                menu.ShowAsContext();
            }

            MenuButton("Fill With Grass…", "Safe Haven/World/Grass and Plants/Fill Selected Terrain With Grass...", "Grow grass and plants over the whole terrain, by ground layer");
            MenuButton("Clear Under Water", "Safe Haven/World/Grass and Plants/Clear Grass Under Water", "No grass growing in water");
        }

        EditorGUILayout.LabelField("Paint plants with a biome (Build › Biomes › Brush) or the terrain's Paint Details brush. Grass takes the ground's colour by itself.",
                                   EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawSky()
    {
        Section("Time and weather");
        if (GUILayout.Button(new GUIContent("Open Sky Controller", "Drag the hour, jump to dawn, noon or night, switch weather, strike lightning; " +
                                                                    "previews in edit mode without saving"), GUILayout.Height(26f)))
        {
            EditorApplication.ExecuteMenuItem("Safe Haven/World/Sky and Weather/Sky Controller");
        }

        Section("Fog", "Presets apply to the scene's atmosphere; a Fog Area thickens or thins it in one place");
        DrawFogPresets();
        DrawAtmosphere();
        DrawSkySetup();
    }

    private static bool showSkySetup;

    /// <summary>The scene's atmosphere volumes (pick one to edit its fog and light shafts) and the selected terrain's mist.</summary>
    private void DrawAtmosphere()
    {
        Scene scene = SceneManager.GetActiveScene();
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .Where(v => v.gameObject.scene == scene && v.sharedProfile != null && v.sharedProfile.Has<AtmosphericFog>()).ToArray();
        foreach (Volume volume in volumes)
        {
            volume.sharedProfile.TryGet(out AtmosphericFog fog);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(volume.name + " / " + volume.sharedProfile.name, "An atmosphere volume in this scene"), EditorStyles.miniLabel);
                if (GUILayout.Button("Edit", EditorStyles.miniButtonLeft, GUILayout.Width(44f)))
                {
                    atmosphereTarget = fog;
                    showFogSettings = true;
                }

                if (GUILayout.Button("Profile", EditorStyles.miniButtonRight, GUILayout.Width(52f)))
                    Selection.activeObject = volume.sharedProfile;
            }
        }

        if (atmosphereTarget == null || !volumes.Any(v => v.sharedProfile.components.Contains(atmosphereTarget)))
            atmosphereTarget = volumes.Length > 0 ? volumes[0].sharedProfile.components.OfType<AtmosphericFog>().First() : null;
        if (atmosphereTarget != null)
        {
            showFogSettings = EditorGUILayout.Foldout(showFogSettings, new GUIContent("Fog and light shaft settings",
                "The authored volume profile: shared profiles change every scene that uses them, and higher-priority volumes may override it"), true);
            if (showFogSettings)
            {
                Editor.CreateCachedEditor(atmosphereTarget, null, ref fogEditor);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    fogEditor.OnInspectorGUI();
                if (!AssetDatabase.Contains(atmosphereTarget))
                    EditorGUILayout.HelpBox("Save the scene once to store its private atmosphere profile.", MessageType.Info);
                using (new EditorGUI.DisabledScope(!AssetDatabase.Contains(atmosphereTarget)))
                if (GUILayout.Button("Save This Fog Profile"))
                {
                    AssetDatabase.SaveAssetIfDirty(atmosphereTarget);
                    foreach (Volume volume in volumes.Where(v => v.sharedProfile.components.Contains(atmosphereTarget)))
                        AssetDatabase.SaveAssetIfDirty(volume.sharedProfile);
                }
            }
        }

        Terrain terrain = Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject)
            ? Selection.activeGameObject.GetComponent<Terrain>() : null;
        TerrainMist mist = terrain != null ? terrain.GetComponent<TerrainMist>() : null;
        if (mist != null)
        {
            showMistSettings = EditorGUILayout.Foldout(showMistSettings, $"Mist on {terrain.name}", true);
            if (showMistSettings)
            {
                Editor.CreateCachedEditor(mist, null, ref mistEditor);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    mistEditor.OnInspectorGUI();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Select a terrain to tune its ground mist.", EditorStyles.miniLabel);
        }
    }

    /// <summary>What a scene needs once (weather, fog, light shafts, wind, fog of war) and what is missing, folded away;
    /// its title says how many problems there are.</summary>
    private static void DrawSkySetup()
    {
        Scene scene = SceneManager.GetActiveScene();
        var problems = new List<string>();
        if (!Object.FindObjectsByType<Volume>(FindObjectsInactive.Include).Any(v => v.gameObject.scene == scene && v.sharedProfile != null && v.sharedProfile.Has<AtmosphericFog>()))
            problems.Add("No atmosphere volume: press Complete Scene Environment.");
        if (!Object.FindObjectsByType<Terrain>().Any(t => t.gameObject.scene == scene))
            problems.Add("No active terrain: ground-following mist needs one.");
        if (!Object.FindObjectsByType<FogGround>().Any(g => g.gameObject.scene == scene))
            problems.Add("Fog Ground is missing: press Complete Scene Environment.");
        Light sun = RenderSettings.sun;
        if (sun == null || sun.gameObject.scene != scene)
            sun = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.gameObject.scene == scene && l.type == LightType.Directional && l.enabled);
        if (sun == null || !sun.isActiveAndEnabled || sun.type != LightType.Directional)
            problems.Add("Light shafts need an enabled directional sun/moon.");
        else if (sun.shadows == LightShadows.None)
            problems.Add("Turn on the sun/moon's shadows (branches cast the shafts).");
        else if (sun.intensity <= 0f)
            problems.Add("The sun/moon's intensity is zero: shafts need direct light.");
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null || !pipeline.supportsMainLightShadows || pipeline.shadowDistance <= 0f)
            problems.Add("The URP pipeline needs main-light shadows and a shadow distance above zero.");

        EditorGUILayout.Space(6f);
        string title = problems.Count == 0 ? "Scene setup" : $"Scene setup  ⚠ {problems.Count} problem{(problems.Count == 1 ? "" : "s")}";
        showSkySetup = EditorGUILayout.Foldout(showSkySetup, title, true, EditorStyles.foldoutHeader);
        if (!showSkySetup)
        {
            return;
        }

        foreach (string problem in problems)
        {
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        EditorGUILayout.LabelField("New scenes get weather, mist and sun/moon shafts by themselves; these bring an older scene up to date.",
                                   EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                MenuButton("Complete Scene Environment", "Safe Haven/World/Sky and Weather/Add Day, Night and Weather to Scene",
                           "Weather, day and night, weather-driven terrain mist and volumetric sun/moon support");
                MenuButton("Shared Wind", "Safe Haven/World/Sky and Weather/Add Shared Moss Wind", "One wind for moss, trees and particles");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                MenuButton("Continuous Fog", "Safe Haven/World/Sky and Weather/Fog/Set Up Fog", "Fog that is always there (not only in Fog and Mist weather)");
                MenuButton("Light Shafts", "Safe Haven/World/Sky and Weather/Fog/Set Up Light Shafts", "Sun and moon shafts without continuous fog");
                MenuButton("Fog of War", "Safe Haven/World/Sky and Weather/Fog/Set Up Open Scene", "Gameplay: the map remembers what the player has explored");
            }

            Terrain terrain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (terrain != null && terrain.GetComponent<TerrainMist>() == null)
            {
                MenuButton($"Add Mist to {terrain.name}", "Safe Haven/World/Sky and Weather/Fog/Add Drifting Mist to Selected Terrain",
                           "For a terrain without mist (terrains in weather scenes get it by themselves)");
            }
        }

        EditorGUILayout.LabelField("Local lights: add Atmosphere Light to a Point Light for glow and fog clearing.", EditorStyles.wordWrappedMiniLabel);
    }

    private static void DrawFogPresets()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Clear", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Clear (no fog)", "No fog");
            MenuButton("Morning Mist", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Morning Mist", "Thin mist (the Haven)");
            MenuButton("Ground Blanket", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Ground Blanket", "Low fog on the ground");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Misty Woods", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Misty Woods", "Thick, drifting forest mist (raids)");
            MenuButton("Marsh", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Marsh", "Heavy fog gathering in hollows");
            MenuButton("+ Fog Area", "GameObject/Safe Haven/World/Fog Area", "Thicker or thinner fog in one place (a hollow, a marsh)");
        }
    }

    // ------------------------------------------------------------------ Scene

    private void DrawScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        Section("Make this scene playable");
        EditorGUILayout.LabelField(string.IsNullOrEmpty(scene.path) ? "Unsaved scene" : scene.path, EditorStyles.miniLabel);
        kind = (ScenePlayability.Kind)GUILayout.Toolbar((int)kind, new[] { "As Safe Haven (home)", "As a Raid" });
        if (GUILayout.Button(new GUIContent($"Make Playable as {(kind == ScenePlayability.Kind.Raid ? "Raid" : "Safe Haven")}",
                                            "Copy the player, cameras, HUD, sky and the rest from the real scene of this kind, stand the player on the ground, bake the NavMesh"),
                             GUILayout.Height(28f)))
        {
            sceneReport = ScenePlayability.MakePlayable(scene, kind);
            RunCheck();
        }

        if (!string.IsNullOrEmpty(sceneReport))
        {
            EditorGUILayout.HelpBox(sceneReport, MessageType.None);
        }

        Section("What is missing");
        if (GUILayout.Button("Check Again"))
        {
            RunCheck();
        }

        if (issues == null)
        {
            RunCheck();
        }

        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing missing: press Play.", MessageType.Info);
        }

        foreach (ScenePlayability.Issue issue in issues)
        {
            MessageType type = issue.Severity == ScenePlayability.Severity.Error ? MessageType.Error
                : issue.Severity == ScenePlayability.Severity.Warning ? MessageType.Warning : MessageType.Info;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(issue.Message, type);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(120f)))
                {
                    if (issue.Target != null && GUILayout.Button("Select"))
                    {
                        Selection.activeGameObject = issue.Target;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }

                    if (issue.Fix != null && GUILayout.Button(issue.FixLabel))
                    {
                        issue.Fix();
                        RunCheck();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        Section("More");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Level Check", "Safe Haven/Scene/Level Check", "Floating, buried, unreachable objects, save ids, leftovers");
            MenuButton("Assign Save Ids", "Safe Haven/Scene/Assign Missing Save Ids", "Give every saveable object an id");
            MenuButton("Tidy Hierarchy", "Safe Haven/Scene/Tidy Hierarchy", "Sort the scene's objects into its numbered folders");
        }

        Section("Play", "Plays with a separate test save, so your real save is never touched");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Play the Haven", "Safe Haven/Scene/Play Haven (Isolated Saves)", "Play the Haven from a fresh test save");
            MenuButton("Play the Prologue", "Safe Haven/Scene/Play Prologue (Isolated Saves)", "Play the prologue from the start, with a test save");
        }
    }

    private void RunCheck() => issues = ScenePlayability.Check(SceneManager.GetActiveScene(), kind);

    // ------------------------------------------------------------------ helpers

    private static void Section(string title, string tooltip = null)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.boldLabel);
    }

    private static void MenuButton(string label, string menu, string tooltip, float width = 0f)
    {
        if (GUILayout.Button(new GUIContent(label, tooltip), width > 0f ? new[] { GUILayout.Width(width) } : Array.Empty<GUILayoutOption>()) &&
            !EditorApplication.ExecuteMenuItem(menu))
        {
            Debug.LogWarning($"World Tools: '{menu}' is not available here (select what it works on first).");
        }
    }

    /// <summary>The selected terrain, else the one under the middle of the Scene view, else the active one.</summary>
    public static Terrain FocusTerrain()
    {
        if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Terrain>() is Terrain selected)
        {
            return selected;
        }

        SceneView view = SceneView.lastActiveSceneView;
        Terrain under = view != null ? WorldGround.TerrainAt(new Vector2(view.pivot.x, view.pivot.z)) : null;
        return under != null ? under : Terrain.activeTerrain;
    }

    /// <summary>Selects the terrain in view so its Inspector shows Unity's sculpt and paint tools.</summary>
    public static void SelectTerrain()
    {
        Terrain terrain = FocusTerrain();
        if (terrain == null)
        {
            Debug.LogWarning("World Tools: no terrain in the scene.");
            return;
        }

        Selection.activeGameObject = terrain.gameObject;
        EditorGUIUtility.PingObject(terrain.gameObject);
    }

    private static WorldTerrain WorldTerrainOf(Terrain terrain)
    {
        var world = terrain.GetComponent<WorldTerrain>();
        return world != null ? world : Undo.AddComponent<WorldTerrain>(terrain.gameObject);
    }
}
