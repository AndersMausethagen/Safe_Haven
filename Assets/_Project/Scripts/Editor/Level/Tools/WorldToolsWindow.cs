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
/// <item>Terrain: sculpting, the World Builder's rivers, paths, cliffs and forests, the look and paint, NavMesh.</item>
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
        Terrain = 1,
        WaterAndPlants = 2,
        SkyAndWeather = 3,
        Scene = 4,
    }

    private static readonly string[] TabNames = { "Place", "Terrain", "Water & Plants", "Sky & Weather", "Scene" };

    /// <summary>Every menu item a button here runs (checked by WorldToolsTests, so a renamed menu is caught).</summary>
    public static readonly string[] MenuPaths =
    {
        "Safe Haven/World/Place/Scatter Brush", "Safe Haven/World/Place/Drop Selection to Ground _END",
        "Safe Haven/World/Place/Drop and Align to Slope #_END",
        "Safe Haven/World/Terrain/Add Gentle Relief...", "Safe Haven/World/Terrain/Add Moss to Selected Terrain",
        "Safe Haven/World/Terrain/Path Painter (paints once)",
        "GameObject/Safe Haven/World/River", "GameObject/Safe Haven/World/River (Haven)", "GameObject/Safe Haven/World/Path (Forest)",
        "GameObject/Safe Haven/World/Path (Haven)", "GameObject/Safe Haven/World/Cliff", "GameObject/Safe Haven/World/Cliff (Haven)",
        "GameObject/Safe Haven/World/Dark Forest Area",
        "GameObject/Safe Haven/World/Haven Grove Area",
        "Safe Haven/World/Build Now", "Safe Haven/World/Rebuild Everything", "Safe Haven/World/Set Up World Assets",
        "Safe Haven/World/Water/Create Pond", "Safe Haven/World/Water/Create River Section", "Safe Haven/World/Water/Create Waterfall...",
        "Safe Haven/World/Water/Make Selected Still Water", "Safe Haven/World/Water/Make Selected Flowing Water",
        "Safe Haven/World/Water/Make Selected Disturb Water", "Safe Haven/World/Water/Carve Terrain Under Selected Water...",
        "Safe Haven/World/Grass and Plants/Add Grass and Bushes to Selected Terrain", "Safe Haven/World/Grass and Plants/Fill Selected Terrain With Grass...",
        "Safe Haven/World/Grass and Plants/Rebake Ground Colours of Selected Terrain", "Safe Haven/World/Grass and Plants/Clear Grass Under Water",
        "Safe Haven/World/Grass and Plants/Make Selected Push Grass",
        "Safe Haven/World/Sky and Weather/Sky Controller", "Safe Haven/World/Sky and Weather/Add Day, Night and Weather to Scene",
        "Safe Haven/World/Sky and Weather/Add Shared Moss Wind", "Safe Haven/World/Sky and Weather/Fog/Set Up Open Scene",
        "Safe Haven/World/Sky and Weather/Fog/Set Up Fog",
        "Safe Haven/World/Sky and Weather/Fog/Set Up Light Shafts",
        "Safe Haven/World/Sky and Weather/Fog/Add Drifting Mist to Selected Terrain",
        "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Clear (no fog)", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Morning Mist",
        "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Ground Blanket", "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Misty Woods",
        "Safe Haven/World/Sky and Weather/Fog/Apply Preset/Marsh", "GameObject/Safe Haven/World/Fog Area",
        "Safe Haven/Scene/Level Check", "Safe Haven/Save/Assign Missing Save Ids",
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
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= PrefabCatalog.Refresh;
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
            case Tab.Terrain:
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawTerrain();
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

    // ------------------------------------------------------------------ Terrain

    private static void DrawTerrain()
    {
        Section("Shape the ground");
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
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Gentle Relief…", "Safe Haven/World/Terrain/Add Gentle Relief...", "Soft rises and dips on flat ground");
            MenuButton("Path (paint once)", "Safe Haven/World/Terrain/Path Painter (paints once)", "The old path painter; World Builder paths can be moved later");
        }

        Section("World Builder (draw with splines; rebuilds as you edit)");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("River", "GameObject/Safe Haven/World/River", "A river in the Mossy Forest: rocky bed, earthy banks, mossy stones and ferns");
            MenuButton("River (Haven)", "GameObject/Safe Haven/World/River (Haven)", "A river in the Forest Lake: rocky bed, banks, lake stones and ferns");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Cliff", "GameObject/Safe Haven/World/Cliff", "A Mossy Forest cliff of cliff boulders, slabs and outcrops (tessellated)");
            MenuButton("Cliff (Haven)", "GameObject/Safe Haven/World/Cliff (Haven)", "A Forest Lake cliff of lake crags (tessellated)");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Path (Forest)", "GameObject/Safe Haven/World/Path (Forest)", "A Mossy Forest path of bare soil");
            MenuButton("Path (Haven)", "GameObject/Safe Haven/World/Path (Haven)", "A trodden Forest Lake track");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Dark Forest", "GameObject/Safe Haven/World/Dark Forest Area", "Mossy Forest: dense cedars, young cedars, mossy logs and stumps, ferns and shrubs, boulders");
            MenuButton("Haven Grove", "GameObject/Safe Haven/World/Haven Grove Area", "Forest Lake: open sugar maple groves, saplings, logs, lake boulders, ferns by the water");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Build Now", "Safe Haven/World/Build Now", "Build whatever changed");
            MenuButton("Rebuild Everything", "Safe Haven/World/Rebuild Everything", "Every terrain, feature and area from scratch");
        }

        Section("Look and paint");
        Terrain terrain = FocusTerrain();
        EditorGUILayout.LabelField(terrain != null ? "Terrain: " + terrain.name : "No terrain in view", EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(terrain == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Dark Forest Look", "The Mossy Forest: tessellated terrain material with its rock on steep ground, its grounds and paint rules")))
            {
                WorldTerrainEditor.UseLook(WorldTerrainOf(terrain), terrain, false);
            }

            if (GUILayout.Button(new GUIContent("Haven Look", "The Forest Lake: tessellated terrain material with its rock on steep ground, its grounds and meadow paint rules")))
            {
                WorldTerrainEditor.UseLook(WorldTerrainOf(terrain), terrain, true);
            }
        }

        using (new EditorGUI.DisabledScope(terrain == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Auto-Paint", "Paint the terrain by its shape with its paint rules, then let the features paint over")))
            {
                WorldTerrain world = WorldTerrainOf(terrain);
                if (world.PaintRules == null)
                {
                    WorldTerrainEditor.UseLook(world, terrain, false);
                }

                WorldPaint.Apply(terrain, world.PaintRules);
                WorldBuilder.RebuildTerrain(terrain);
            }

            MenuButton("Add Moss", "Safe Haven/World/Terrain/Add Moss to Selected Terrain", "Live moss on the terrain's moss layer");
        }

        using (new EditorGUI.DisabledScope(terrain == null))
        {
            if (GUILayout.Button(new GUIContent("Rock on Steep Ground", "Paint the look's rocky ground on steep slopes and Cliff Rock on the steepest drops " +
                                                                      "(over what is there; paint any ground over them to change them)")))
            {
                Debug.Log($"{terrain.name}: {TerrainRock.PaintSteepGround(terrain, WorldLook.Of(terrain))}.", terrain);
            }
        }

        DrawGroundPainting(terrain);
        DrawRelief(terrain);

        Section("Walking");
        if (GUILayout.Button(new GUIContent("Rebake NavMesh", "Monsters and residents walk the ground as it is now")))
        {
            Debug.Log("World Tools: " + WorldBuilder.RebakeNavMesh());
        }
    }

    /// <summary>Every ground layer of a set as a picture: click one to add it to the terrain (tessellated) and paint with it.</summary>
    private static void DrawGroundPainting(Terrain terrain)
    {
        Section("Paint ground (tessellated)");
        int setIndex = Mathf.Max(0, Array.IndexOf(GroundLayerPainter.Sets, groundSet));
        setIndex = GUILayout.Toolbar(setIndex, GroundLayerPainter.Sets, EditorStyles.miniButton);
        groundSet = GroundLayerPainter.Sets[setIndex];
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
        if (GUILayout.Button(new GUIContent("Recalibrate Grounds", "After adding grounds or changing height maps: puts every ground back in proportion (keeps Relief ×)")))
        {
            Debug.Log("Ground relief: " + GroundRelief.CalibrateAll());
        }
    }

    // ------------------------------------------------------------------ Water and plants, sky and weather

    private static void DrawWaterAndPlants()
    {
        Section("Water (winding rivers: Terrain › River)");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Pond", "Safe Haven/World/Water/Create Pond", "A still pond at the middle of the Scene view");
            MenuButton("Straight River", "Safe Haven/World/Water/Create River Section", "A straight piece of flowing water");
            MenuButton("Waterfall…", "Safe Haven/World/Water/Create Waterfall...", "A falling sheet with plunge pool, mist and spray");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Make Still", "Safe Haven/World/Water/Make Selected Still Water", "Turn the selected mesh into still water");
            MenuButton("Make Flowing", "Safe Haven/World/Water/Make Selected Flowing Water", "Turn the selected mesh into flowing water");
            MenuButton("Disturbs Water", "Safe Haven/World/Water/Make Selected Disturb Water", "The selected object makes ripples and splashes");
        }

        MenuButton("Carve Bed Under Selected Water…", "Safe Haven/World/Water/Carve Terrain Under Selected Water...", "Dig a bed with banks under the water");

        Section("Grass and plants");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Add Grass Kinds", "Safe Haven/World/Grass and Plants/Add Grass and Bushes to Selected Terrain", "Give the terrain the grass, bushes and Nordic plants");
            MenuButton("Fill With Grass…", "Safe Haven/World/Grass and Plants/Fill Selected Terrain With Grass...", "Grow grass and plants by ground layer");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Rebake Grass Colour", "Safe Haven/World/Grass and Plants/Rebake Ground Colours of Selected Terrain", "Grass takes the ground's colour");
            MenuButton("Clear Under Water", "Safe Haven/World/Grass and Plants/Clear Grass Under Water", "No grass growing in water");
        }

        MenuButton("Selected Pushes Grass", "Safe Haven/World/Grass and Plants/Make Selected Push Grass", "The selected object bends grass as it moves");

        Terrain terrain = FocusTerrain();
        using (new EditorGUI.DisabledScope(terrain == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (WorldLook look in WorldLook.All)
            {
                if (GUILayout.Button(new GUIContent($"Add {look.Name} Plants", $"Give the terrain the {look.Name} grass, moss, ferns and flowers to paint (Paint Details)")))
                {
                    Debug.Log($"{terrain.name}: {GroundLayerPainter.AddDetails(terrain, look)} {look.Name} plant kind(s) added.");
                }
            }
        }
    }

    private void DrawSky()
    {
        Section("Day, night and weather");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Sky Controller", "Safe Haven/World/Sky and Weather/Sky Controller", "Preview and drive time of day and weather");
            MenuButton("Complete Scene Environment", "Safe Haven/World/Sky and Weather/Add Day, Night and Weather to Scene", "Weather, day/night, weather-driven terrain mist and volumetric sun/moon support");
        }

        MenuButton("Add Shared Wind", "Safe Haven/World/Sky and Weather/Add Shared Moss Wind", "One wind for moss, trees and particles");

        DrawAtmosphere();
        Section("Fog presets and exploration");
        using (new EditorGUILayout.HorizontalScope())
        {
            MenuButton("Set Up Fog of War", "Safe Haven/World/Sky and Weather/Fog/Set Up Open Scene", "Optional gameplay exploration memory; not required for mist or light shafts");
            MenuButton("Fog Area", "GameObject/Safe Haven/World/Fog Area", "Thicker or thinner fog in one place (a hollow, a marsh)");
        }

        DrawFogPresets();
    }

    private void DrawAtmosphere()
        {
            Scene scene = SceneManager.GetActiveScene();
            Section("Atmosphere");
            EditorGUILayout.HelpBox("New scenes include weather, mist and volumetric sun/moon support automatically. Mist and Fog are random weather types; Clear removes both while keeping light shafts. Weather Only Atmosphere on Weather controls this. Use Complete Scene Environment for an older scene. Preview in Game view.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    MenuButton("Set Up Fog", "Safe Haven/World/Sky and Weather/Fog/Set Up Fog",
                        "Optional continuous fog. Does not enable or change light shafts.");
                    MenuButton("Set Up Light Shafts", "Safe Haven/World/Sky and Weather/Fog/Set Up Light Shafts",
                        "Enables sun/moon shafts without adding continuous fog.");
                }
            }

            Terrain terrain = Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject)
                ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (terrain != null && terrain.gameObject.scene != scene)
                terrain = null;
            if (terrain != null && terrain.GetComponent<TerrainMist>() == null)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    MenuButton("Add Mist to Selected Terrain", "Safe Haven/World/Sky and Weather/Fog/Add Drifting Mist to Selected Terrain",
                        "Manual repair for a terrain without mist. Weather scenes automatically support newly added terrains.");
            }
            if (terrain == null)
                EditorGUILayout.HelpBox("Select a Terrain to tune its mist. Terrains in weather scenes receive weather-driven mist automatically.", MessageType.Info);

            var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
                .Where(v => v.gameObject.scene == scene && v.sharedProfile != null && v.sharedProfile.Has<AtmosphericFog>()).ToArray();
            foreach (Volume volume in volumes)
            {
                volume.sharedProfile.TryGet(out AtmosphericFog fog);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(volume.name + " / " + volume.sharedProfile.name, EditorStyles.miniLabel);
                    if (GUILayout.Button("Edit", GUILayout.Width(44f)))
                    {
                        atmosphereTarget = fog;
                        showFogSettings = true;
                    }
                    if (GUILayout.Button("Profile", GUILayout.Width(52f)))
                        Selection.activeObject = volume.sharedProfile;
                }
            }
            if (volumes.Length == 0)
                EditorGUILayout.HelpBox("Atmosphere support is missing. Run Complete Scene Environment.", MessageType.Warning);
            if (atmosphereTarget == null || !volumes.Any(v => v.sharedProfile.components.Contains(atmosphereTarget)))
                atmosphereTarget = volumes.Length > 0 ? volumes[0].sharedProfile.components.OfType<AtmosphericFog>().First() : null;
            if (atmosphereTarget != null)
            {
                showFogSettings = EditorGUILayout.Foldout(showFogSettings, "Fog and light shaft settings", true);
                if (showFogSettings)
                {
                    EditorGUILayout.HelpBox("Editing the authored volume profile. Shared profiles affect every scene that uses them. Higher-priority volumes may override this look.", MessageType.Info);
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

            TerrainMist mist = terrain != null ? terrain.GetComponent<TerrainMist>() : null;
            if (mist != null)
            {
                showMistSettings = EditorGUILayout.Foldout(showMistSettings, "Selected terrain mist settings", true);
                if (showMistSettings)
                {
                    Editor.CreateCachedEditor(mist, null, ref mistEditor);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                        mistEditor.OnInspectorGUI();
                }
            }
            bool hasTerrain = Object.FindObjectsByType<Terrain>().Any(t => t.gameObject.scene == scene);
            if (!hasTerrain)
                EditorGUILayout.HelpBox("No active Terrain: ground-following mist needs a terrain tile.", MessageType.Warning);
            if (!Object.FindObjectsByType<FogGround>().Any(g => g.gameObject.scene == scene))
                EditorGUILayout.HelpBox("Fog Ground is missing. Run the atmosphere or terrain-mist setup.", MessageType.Warning);
            var sun = RenderSettings.sun;
            if (sun == null || sun.gameObject.scene != scene)
                sun = Object.FindObjectsByType<Light>()
                    .FirstOrDefault(l => l.gameObject.scene == scene && l.type == LightType.Directional && l.enabled);
            if (sun == null || !sun.isActiveAndEnabled || sun.type != LightType.Directional)
                EditorGUILayout.HelpBox("An enabled directional sun/moon light is required for shafts.", MessageType.Warning);
            else if (sun.shadows == LightShadows.None)
                EditorGUILayout.HelpBox("Enable shadows on the sun/moon. Branches must also cast shadows to shape the shafts.", MessageType.Warning);
            else if (sun.intensity <= 0f)
                EditorGUILayout.HelpBox("The sun/moon intensity is zero; shafts need direct light.", MessageType.Warning);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || !pipeline.supportsMainLightShadows || pipeline.shadowDistance <= 0f)
                EditorGUILayout.HelpBox("The active URP pipeline needs main-light shadows and a positive shadow distance.", MessageType.Warning);
            EditorGUILayout.LabelField("Local lights: add Atmosphere Light to a Point Light for glow and fog clearing.", EditorStyles.wordWrappedMiniLabel);
        }

    private static void DrawFogPresets()
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);
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
            MenuButton("Assign Save Ids", "Safe Haven/Save/Assign Missing Save Ids", "Give every saveable object an id");
        }
    }

    private void RunCheck() => issues = ScenePlayability.Check(SceneManager.GetActiveScene(), kind);

    // ------------------------------------------------------------------ helpers

    private static void Section(string title)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void MenuButton(string label, string menu, string tooltip)
    {
        if (GUILayout.Button(new GUIContent(label, tooltip)) && !EditorApplication.ExecuteMenuItem(menu))
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
