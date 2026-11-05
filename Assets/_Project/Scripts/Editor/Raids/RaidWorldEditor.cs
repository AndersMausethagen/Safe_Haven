using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// The World tab of a raid in the Content Editor: everything a run's map is made of, in one place and without hunting for
/// assets. Layout (clearings, trails, moods, sites), Ground (materials and what grows on them), Trees &amp; Rocks, Plants, Water
/// and Mist, under a strip of maps the rules make, which redraws as they change. It edits the raid's Layout Rules, its Raid Map
/// definition (found through the raid's scene) and, for mist, the raid scene itself.
/// </summary>
internal static class RaidWorldEditor
{
    private const string TabKey = "SafeHaven.ContentEditor.RaidWorldTab";
    private const string StripKey = "SafeHaven.ContentEditor.RaidWorldStrip";
    private const float IconSize = 40f;
    private const float RemoveWidth = 22f;
    private const int SeedsChecked = 50;
    private static readonly string[] Tabs = { "Layout", "Ground", "Trees & Rocks", "Plants", "Water", "Mist" };

    private static readonly Dictionary<RaidLayoutRules, RaidMapSheet> Sheets = new Dictionary<RaidLayoutRules, RaidMapSheet>();
    private static readonly Dictionary<Object, SerializedObject> Serialized = new Dictionary<Object, SerializedObject>();
    private static readonly HashSet<string> OpenCards = new HashSet<string>();
    private static string waterReport;

    private static int Tab
    {
        get => Mathf.Clamp(EditorPrefs.GetInt(TabKey, 0), 0, Tabs.Length - 1);
        set => EditorPrefs.SetInt(TabKey, value);
    }

    /// <summary>The first asset of a kind that a scene points at directly; a raid scene has one map and one terrain.</summary>
    public static T FromScene<T>(string scenePath) where T : Object
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return null;
        }

        foreach (string path in AssetDatabase.GetDependencies(scenePath, false))
        {
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(T))
            {
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }
        }

        return null;
    }

    public static void Draw(RaidDefinition raid, SerializedObject raidData, RaidSceneContext scene)
    {
        // Ranges (smallest, largest) stay on one line beside their label, however narrow the panel.
        bool wide = EditorGUIUtility.wideMode;
        EditorGUIUtility.wideMode = true;
        DrawWorld(raid, raidData, scene);
        EditorGUIUtility.wideMode = wide;
    }

    private static void DrawWorld(RaidDefinition raid, SerializedObject raidData, RaidSceneContext scene)
    {
        RaidLayoutRules rules = raid.LayoutRules;
        var map = FromScene<RaidMapDefinition>(scene.ScenePath);
        var terrain = FromScene<TerrainData>(scene.ScenePath);
        DrawSource(raid, raidData, rules, map);
        if (map == null)
        {
            EditorGUILayout.HelpBox("The raid's scene has no Raid Map, so there is no world to edit here. Pick a scene on the Overview tab.", MessageType.Info);
            return;
        }

        if (rules != null)
        {
            DrawStrip(rules, map);
        }

        EditorGUILayout.Space(6f);
        int tab = GUILayout.Toolbar(Tab, Tabs, EditorStyles.miniButton, GUILayout.Height(22f));
        if (tab != Tab)
        {
            Tab = tab;
            GUI.FocusControl(null);
        }

        SerializedObject mapData = Data(map);
        SerializedObject rulesData = rules != null ? Data(rules) : null;
        switch (Tabs[Tab])
        {
            case "Layout":
                DrawLayout(rules, rulesData, map);
                break;
            case "Ground":
                DrawGround(mapData, terrain);
                break;
            case "Trees & Rocks":
                DrawTreesAndRocks(mapData, rulesData);
                break;
            case "Plants":
                DrawPlants(map, mapData, terrain);
                break;
            case "Water":
                DrawWater(rules, rulesData, map);
                break;
            default:
                DrawMist(scene);
                break;
        }

        mapData.ApplyModifiedProperties();
        rulesData?.ApplyModifiedProperties();
    }

    /// <summary>What the Content Editor should warn about in a raid's world.</summary>
    public static void FindProblems(RaidDefinition raid, string scenePath, List<string> problems)
    {
        var map = FromScene<RaidMapDefinition>(scenePath);
        var terrain = FromScene<TerrainData>(scenePath);
        if (map == null)
        {
            return;
        }

        foreach (RaidMapDefinition.Scatter tree in map.Trees)
        {
            if (tree?.prefab == null)
            {
                problems.Add("World: a tree has no prefab, so it never grows.");
            }
        }

        if (terrain != null)
        {
            foreach (TerrainGrowth.Plant plant in map.Plants)
            {
                if (plant.enabled && Array.FindIndex(terrain.detailPrototypes, prototype => NameOf(prototype) == plant.prototype) < 0)
                {
                    problems.Add($"World: the plant '{plant.prototype}' isn't among the terrain's details, so it never grows.");
                }
            }
        }

        RaidLayoutRules rules = raid.LayoutRules;
        if (rules != null && rules.Ponds.y > 0 && rules.PondMaterial == null)
        {
            problems.Add("World: the map wants ponds but Water has no material.");
        }
    }

    private static SerializedObject Data(Object target)
    {
        if (!Serialized.TryGetValue(target, out SerializedObject data) || data == null || data.targetObject == null)
        {
            data = new SerializedObject(target);
            Serialized[target] = data;
        }

        data.Update();
        return data;
    }

    private static void DrawSource(RaidDefinition raid, SerializedObject raidData, RaidLayoutRules rules, RaidMapDefinition map)
    {
        ContentUtility.BeginPanel("Where the map comes from", rules != null
            ? "A new map is generated from these rules every run. The Raid Map supplies its materials, trees and plants."
            : "This raid plays the scene's hand-painted map, turned. Give it Layout Rules to generate a new map every run.");
        EditorGUILayout.ObjectField(raidData.FindProperty("layoutRules"), typeof(RaidLayoutRules), new GUIContent("Layout Rules"));
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(new GUIContent("Raid Map", "The materials, trees and plants. Comes with the raid's scene."));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(map, typeof(RaidMapDefinition), false);
            }
        }

        if (rules == null && GUILayout.Button("Create Layout Rules", GUILayout.Height(24f)))
        {
            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/_Project/Data/Raids/Maps/{raid.name} Layout.asset");
            var created = ScriptableObject.CreateInstance<RaidLayoutRules>();
            AssetDatabase.CreateAsset(created, path);
            raidData.FindProperty("layoutRules").objectReferenceValue = created;
        }

        ContentUtility.EndPanel();
    }

    private static void DrawStrip(RaidLayoutRules rules, RaidMapDefinition map)
    {
        bool open = EditorPrefs.GetBool(StripKey, true);
        bool nowOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "What these rules make");
        if (nowOpen != open)
        {
            EditorPrefs.SetBool(StripKey, nowOpen);
        }

        if (nowOpen)
        {
            if (!Sheets.TryGetValue(rules, out RaidMapSheet sheet))
            {
                Sheets[rules] = sheet = new RaidMapSheet(3);
            }

            sheet.Draw(rules, map.Size);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawLayout(RaidLayoutRules rules, SerializedObject data, RaidMapDefinition map)
    {
        if (rules == null)
        {
            EditorGUILayout.HelpBox("The painted map's layout is edited in the raid scene: select Raid Map and use its Map Painter.", MessageType.Info);
            return;
        }

        DrawSites(data, map);
        DrawMoods(data, map);
        DrawShapes(data);
        Group(data, "Clearings", "The open places the map is built around.", "rooms", "roomRadius", "roomSeparation", "edgeMargin");
        Group(data, "Trails", "What joins the clearings, and what the player mostly walks.", "trailHalfWidth", "trailWander", "loopReach");
        Group(data, "Land", "How the ground rises between the trails. Trails always run along the valley floors.",
            "groundHeight", "hills", "hillScale", "ridgeHeight", "ridgeWidth", "roughness", "rimHeight", "rimWidth");
        ContentUtility.BeginPanel("Forest", "How thick it stands, before the mood.");
        EditorGUILayout.Slider(data.FindProperty("forestDensity"), 0f, 1f, new GUIContent("Density", data.FindProperty("forestDensity").tooltip));
        Field(data.FindProperty("forestPatches"));
        ContentUtility.EndPanel();
        Group(data, "Advanced", null, "fieldResolution", "attempts", "groups");
    }

    private static void Group(SerializedObject data, string title, string description, params string[] fields)
    {
        ContentUtility.BeginPanel(title, description);
        foreach (string field in fields)
        {
            Field(data.FindProperty(field));
        }

        ContentUtility.EndPanel();
    }

    // A field's [Header] is for the plain inspector; these panels have titles of their own, and PropertyField would repeat it.
    private static void Field(SerializedProperty property, GUIContent label = null)
    {
        label ??= new GUIContent(property.displayName, property.tooltip);
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            EditorGUILayout.PropertyField(property, label, true);
            return;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(label, property.floatValue));
                break;
            case SerializedPropertyType.Vector2Int:
                property.vector2IntValue = EditorGUILayout.Vector2IntField(label, property.vector2IntValue);
                break;
            case SerializedPropertyType.ObjectReference:
                EditorGUILayout.ObjectField(property, label);
                break;
            default:
                EditorGUILayout.PropertyField(property, label, true);
                break;
        }
    }

    // Sites first: they decide what there is to do on a map. A spawn list fills the points a kind of site makes.
    private static void DrawSites(SerializedObject data, RaidMapDefinition map)
    {
        ContentUtility.BeginPanel("Sites", "What goes on every map: how many places of each kind, where they lie and how far apart. " +
                                           "Placed from the top, so the first get the best spots. Spawn lists then fill them.");
        SerializedProperty sites = data.FindProperty("sites");
        using (new EditorGUILayout.HorizontalScope())
        {
            Header("Kind", "What the site is. Spawn groups take the sites of their own kind.", 0f);
            Header("Where", "Room: a clearing. Dead End: a clearing one trail leads to. On Path: on a trail. Beside Path: in a pocket off one.", 92f);
            Header("Count", "How many on each map.", 42f);
            Header("Apart", "Metres kept between two of this kind.", 42f);
            Header("Start", "Metres kept from the player's start.", 42f);
            Header("Open", "Radius of open, level ground kept around it.", 42f);
            Header("Pts", "Spawn points each one makes, set on the Raid Map.", 28f);
            GUILayout.Space(RemoveWidth + 4f);
        }

        int remove = -1;
        for (int i = 0; i < sites.arraySize; i++)
        {
            SerializedProperty site = sites.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(site.FindPropertyRelative("kind"), GUIContent.none);
                EditorGUILayout.PropertyField(site.FindPropertyRelative("placement"), GUIContent.none, GUILayout.Width(92f));
                EditorGUILayout.PropertyField(site.FindPropertyRelative("count"), GUIContent.none, GUILayout.Width(42f));
                EditorGUILayout.PropertyField(site.FindPropertyRelative("separation"), GUIContent.none, GUILayout.Width(42f));
                EditorGUILayout.PropertyField(site.FindPropertyRelative("fromStart"), GUIContent.none, GUILayout.Width(42f));
                EditorGUILayout.PropertyField(site.FindPropertyRelative("clearing"), GUIContent.none, GUILayout.Width(42f));
                GUILayout.Label(map.RuleFor((RaidSiteKind)site.FindPropertyRelative("kind").enumValueIndex).pointsPerSite.ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
                if (GUILayout.Button(new GUIContent("×", "Remove"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)))
                {
                    remove = i;
                }
            }

            if ((RaidSitePlacement)site.FindPropertyRelative("placement").enumValueIndex == RaidSitePlacement.BesidePath)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(site.FindPropertyRelative("offPath"), new GUIContent("Off the trail (m)", "How far from the trail, least and most."));
                }
            }
        }

        if (remove >= 0)
        {
            sites.DeleteArrayElementAtIndex(remove);
        }

        if (GUILayout.Button("+ Add Site", EditorStyles.miniButton, GUILayout.Width(90f)))
        {
            sites.arraySize++;
        }

        ContentUtility.EndPanel();
    }

    private static void Header(string text, string tip, float width)
    {
        var content = new GUIContent(text, tip);
        if (width > 0f)
        {
            GUILayout.Label(content, EditorStyles.miniBoldLabel, GUILayout.Width(width));
        }
        else
        {
            GUILayout.Label(content, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
        }
    }

    // A mood is what makes one run look unlike the last, so each gets a card with its share of all runs.
    private static void DrawMoods(SerializedObject data, RaidMapDefinition map)
    {
        ContentUtility.BeginPanel("Moods", "What the forest is like this run, rolled by chance and named in the briefing.");
        SerializedProperty moods = data.FindProperty("moods");
        float total = 0f;
        for (int i = 0; i < moods.arraySize; i++)
        {
            total += moods.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue;
        }

        int remove = -1;
        for (int i = 0; i < moods.arraySize; i++)
        {
            SerializedProperty mood = moods.GetArrayElementAtIndex(i);
            string key = AssetDatabase.GetAssetPath(data.targetObject) + ":mood:" + i;
            bool open = OpenCards.Contains(key);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nowOpen = EditorGUILayout.Foldout(open, mood.FindPropertyRelative("name").stringValue, true);
                    if (nowOpen != open)
                    {
                        if (nowOpen)
                        {
                            OpenCards.Add(key);
                        }
                        else
                        {
                            OpenCards.Remove(key);
                        }
                    }

                    float weight = mood.FindPropertyRelative("weight").floatValue;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{(total > 0f ? weight / total * 100f : 0f):0}% of runs", EditorStyles.miniLabel, GUILayout.Width(70f));
                    if (GUILayout.Button(new GUIContent("×", "Remove this mood"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)))
                    {
                        remove = i;
                    }
                }

                if (open)
                {
                    foreach (string field in new[] { "name", "weight", "briefing", "forest", "treeSpacing", "ridges", "rooms", "trails", "rocks", "fogBanks" })
                    {
                        EditorGUILayout.PropertyField(mood.FindPropertyRelative(field));
                    }

                    DrawMoodTrees(mood.FindPropertyRelative("treeWeights"), map);
                }
            }
        }

        if (remove >= 0)
        {
            moods.DeleteArrayElementAtIndex(remove);
        }

        if (GUILayout.Button("+ Add Mood", EditorStyles.miniButton, GUILayout.Width(90f)))
        {
            moods.arraySize++;
        }

        ContentUtility.EndPanel();
    }

    // The list runs parallel to the map's trees, which is easy to get wrong by hand, so it is labelled with them here.
    private static void DrawMoodTrees(SerializedProperty weights, RaidMapDefinition map)
    {
        bool custom = weights.arraySize > 0;
        bool nowCustom = EditorGUILayout.Toggle(new GUIContent("Own mix of trees", "Off: the mix on the Trees & Rocks tab."), custom);
        if (nowCustom != custom)
        {
            weights.arraySize = nowCustom ? map.Trees.Count : 0;
            for (int i = 0; nowCustom && i < map.Trees.Count; i++)
            {
                weights.GetArrayElementAtIndex(i).floatValue = map.Trees[i] != null ? map.Trees[i].weight : 1f;
            }
        }

        if (!nowCustom)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < map.Trees.Count && i < weights.arraySize; i++)
            {
                GameObject prefab = map.Trees[i]?.prefab;
                EditorGUILayout.Slider(weights.GetArrayElementAtIndex(i), 0f, 6f, prefab != null ? prefab.name : "(no prefab)");
            }
        }
    }

    private static void DrawShapes(SerializedObject data)
    {
        ContentUtility.BeginPanel("Shapes", "The plan a run's trails are laid out by. Weight says how often it comes up; Loops how many ways round there are.");
        SerializedProperty shapes = data.FindProperty("shapes");
        for (int i = 0; i < shapes.arraySize; i++)
        {
            SerializedProperty shape = shapes.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(ObjectNames.NicifyVariableName(shape.FindPropertyRelative("shape").enumNames[shape.FindPropertyRelative("shape").enumValueIndex]), GUILayout.Width(70f));
                GUILayout.Label("Weight", EditorStyles.miniLabel, GUILayout.Width(40f));
                SerializedProperty weight = shape.FindPropertyRelative("weight");
                weight.floatValue = Mathf.Max(0f, GUILayout.HorizontalSlider(weight.floatValue, 0f, 3f));
                GUILayout.Label(weight.floatValue.ToString("0.0"), EditorStyles.miniLabel, GUILayout.Width(26f));
                GUILayout.Label("Loops", EditorStyles.miniLabel, GUILayout.Width(36f));
                SerializedProperty loops = shape.FindPropertyRelative("loopChance");
                loops.floatValue = GUILayout.HorizontalSlider(loops.floatValue, 0f, 1f);
                GUILayout.Label(loops.floatValue.ToString("0%"), EditorStyles.miniLabel, GUILayout.Width(32f));
            }
        }

        ContentUtility.EndPanel();
    }

    private static void DrawGround(SerializedObject data, TerrainData terrain)
    {
        ContentUtility.BeginPanel("Ground material", "How the whole ground is drawn: its shader, tessellation and displacement.");
        Field(data.FindProperty("terrainMaterial"), new GUIContent("Material"));
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Ground layers", "The grounds a map is painted with, and how much grows on each. Each slider is 0 to 1. " +
                                                   "'Bare at' clears all growth once that much of the layer shows, which keeps paths and rock bare.");
        DrawLayer(data, terrain, "forestLayer", "Forest floor", "Everywhere the forest grows; moss grows on it too, and it takes over wet ground.");
        DrawLayer(data, terrain, "pathLayer", "Soil", "Trails, clearings and pond beds.");
        DrawLayer(data, terrain, "cliffLayer", "Rock", "Slopes too steep for anything else.");
        DrawLayer(data, terrain, "litterLayer", "Litter (optional)", "Needles and leaves in patches under the densest canopy, where the ground is dry.");
        EditorGUILayout.PropertyField(data.FindProperty("litter"), new GUIContent("Litter cover", "How much of the deep forest's floor the litter covers."));
        EditorGUILayout.PropertyField(data.FindProperty("cliffSlope"), new GUIContent("Rock from / full at (°)", "Slopes in degrees where rock starts to show, and where it covers everything."));
        EditorGUILayout.HelpBox("Swapped a layer? Its displacement masks are baked from the old one. Open the raid scene, select the terrain and run " +
                                "Safe Haven › World › Terrain › Enable or Refresh Displacement.", MessageType.None);
        ContentUtility.EndPanel();
    }

    private static void DrawLayer(SerializedObject data, TerrainData terrain, string field, string role, string tip)
    {
        SerializedProperty layer = data.FindProperty(field);
        SerializedProperty growth = data.FindProperty("growth");
        var old = (TerrainLayer)layer.objectReferenceValue;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            Rect icon = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
            if (old != null && old.diffuseTexture != null)
            {
                GUI.DrawTexture(icon, old.diffuseTexture, ScaleMode.ScaleAndCrop);
            }

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(role, tip), EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var chosen = (TerrainLayer)EditorGUILayout.ObjectField(old, typeof(TerrainLayer), false);
                if (EditorGUI.EndChangeCheck() && chosen != null)
                {
                    layer.objectReferenceValue = chosen;
                    SwapLayer(growth, terrain, old, chosen);
                }

                SerializedProperty entry = GrowthOf(growth, (TerrainLayer)layer.objectReferenceValue);
                if (entry == null)
                {
                    if (GUILayout.Button("Let things grow on it", EditorStyles.miniButton))
                    {
                        growth.arraySize++;
                        entry = growth.GetArrayElementAtIndex(growth.arraySize - 1);
                        entry.FindPropertyRelative("layer").objectReferenceValue = layer.objectReferenceValue;
                        entry.FindPropertyRelative("colour").colorValue = GrassSetup.AverageColour((TerrainLayer)layer.objectReferenceValue);
                    }

                    return;
                }

                GrowthSlider(entry, "shortGrass", "Short grass");
                GrowthSlider(entry, "tallGrass", "Tall grass");
                GrowthSlider(entry, "bushes", "Bushes");
                GrowthSlider(entry, "plants", "Plants");
                GrowthSlider(entry, "bareAt", "Bare at");
            }
        }
    }

    private static void GrowthSlider(SerializedProperty entry, string field, string label)
    {
        EditorGUILayout.Slider(entry.FindPropertyRelative(field), 0f, 1f, label);
    }

    private static SerializedProperty GrowthOf(SerializedProperty growth, TerrainLayer layer)
    {
        for (int i = 0; i < growth.arraySize; i++)
        {
            SerializedProperty entry = growth.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("layer").objectReferenceValue == layer)
            {
                return entry;
            }
        }

        return null;
    }

    // Growth is keyed by layer and the terrain asset holds the layers too, so a swap has to carry both over.
    private static void SwapLayer(SerializedProperty growth, TerrainData terrain, TerrainLayer old, TerrainLayer chosen)
    {
        SerializedProperty entry = old != null ? GrowthOf(growth, old) : null;
        if (entry != null)
        {
            entry.FindPropertyRelative("layer").objectReferenceValue = chosen;
            entry.FindPropertyRelative("colour").colorValue = GrassSetup.AverageColour(chosen);
        }

        if (terrain != null)
        {
            TerrainLayer[] layers = terrain.terrainLayers;
            int index = old != null ? Array.IndexOf(layers, old) : -1;
            if (index >= 0)
            {
                Undo.RecordObject(terrain, "Swap Ground Layer");
                layers[index] = chosen;
                terrain.terrainLayers = layers;
                EditorUtility.SetDirty(terrain);
            }
        }
    }

    private static void DrawTreesAndRocks(SerializedObject map, SerializedObject rules)
    {
        ContentUtility.BeginPanel("Trees", "What the forest is made of. Weight sets each tree's share of the forest; a mood can bring its own mix (Layout tab). Drop prefabs below to add them.");
        DrawScatter(map.FindProperty("trees"), rules, true);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(map.FindProperty("treeSpacing"), new GUIContent("Spacing (m)", "Metres between trees where the forest is densest."));
        EditorGUILayout.PropertyField(map.FindProperty("maxTreeSlope"), new GUIContent("Steepest slope (°)", "No trees on slopes steeper than this."));
        EditorGUILayout.PropertyField(map.FindProperty("clearAroundSites"), new GUIContent("Clear around sites (m)", "Trees keep this far from every site."));
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Rocks and logs", "Lying about the forest and at the foot of slopes.");
        DrawScatter(map.FindProperty("rocks"), null, false);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(map.FindProperty("rockSpacing"), new GUIContent("Spacing (m)", "Metres between spots where one could lie."));
        EditorGUILayout.PropertyField(map.FindProperty("rocksAtCliffs"), new GUIContent("Chance at slopes"));
        EditorGUILayout.PropertyField(map.FindProperty("rocksInForest"), new GUIContent("Chance in forest"));
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Undergrowth", "Saplings, shrubs and forest-floor clutter, placed after the trees in the gaps, edges and spots by trunks their habitat favours. They don't block the way.");
        DrawScatter(map.FindProperty("undergrowth"), null, false, solid: false);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(map.FindProperty("undergrowthSpacing"), new GUIContent("Spacing (m)", "Metres between spots where one could grow."));
        EditorGUILayout.PropertyField(map.FindProperty("undergrowthChance"), new GUIContent("Chance", "Chance of undergrowth in each spot that suits something."));
        ContentUtility.EndPanel();
    }

    private static void DrawScatter(SerializedProperty list, SerializedObject rules, bool trees, bool solid = true)
    {
        float total = 0f;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            total += entry.FindPropertyRelative("prefab").objectReferenceValue != null ? entry.FindPropertyRelative("weight").floatValue : 0f;
        }

        int remove = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            var prefab = (GameObject)entry.FindPropertyRelative("prefab").objectReferenceValue;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawIcon(prefab);
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("prefab"), GUIContent.none);
                        float weight = entry.FindPropertyRelative("weight").floatValue;
                        GUILayout.Label($"{(total > 0f && prefab != null ? weight / total * 100f : 0f):0}%", EditorStyles.miniBoldLabel, GUILayout.Width(32f));
                        if (GUILayout.Button(new GUIContent("×", "Remove"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)))
                        {
                            remove = i;
                        }
                    }

                    EditorGUILayout.Slider(entry.FindPropertyRelative("weight"), 0f, 6f, new GUIContent("Weight"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("scale"), new GUIContent("Size (smallest, largest)"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(new GUIContent("Raid tiers (from, to)", "The raid tiers it grows at, on a generated map. 0 = any."));
                        SerializedProperty from = entry.FindPropertyRelative("fromTier");
                        SerializedProperty to = entry.FindPropertyRelative("toTier");
                        from.intValue = Mathf.Clamp(EditorGUILayout.IntField(from.intValue, GUILayout.Width(40f)), 0, Tiers.Max);
                        to.intValue = Mathf.Clamp(EditorGUILayout.IntField(to.intValue, GUILayout.Width(40f)), 0, Tiers.Max);
                        GUILayout.FlexibleSpace();
                    }

                    string warning = ScatterWarning(prefab, trees, solid);
                    if (warning != null)
                    {
                        EditorGUILayout.LabelField(warning, ContentUtility.WrappedMiniLabel);
                    }

                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("habitat"),
                        new GUIContent("Where it belongs", "Slope, forest density, wet or dry ground, edges, closeness to trunks, groves and the room it needs."), true);
                }
            }
        }

        if (remove >= 0)
        {
            RemoveFromMoods(rules, remove, trees);
            list.DeleteArrayElementAtIndex(remove);
        }

        DropZone(list);
    }

    private static string ScatterWarning(GameObject prefab, bool tree, bool solid)
    {
        if (prefab == null)
        {
            return "Pick a prefab; an empty row never grows.";
        }

        if (solid && prefab.GetComponent<CapsuleCollider>() == null)
        {
            return "⚠ No Capsule Collider: the player and creatures walk through it.";
        }

        return tree && prefab.GetComponent<ChoppableTree>() == null ? "⚠ No Choppable Tree: it can't be felled." : null;
    }

    // Moods hold a weight per tree by position, so removing a tree must remove its weight or every later one slips.
    private static void RemoveFromMoods(SerializedObject rules, int tree, bool trees)
    {
        if (!trees || rules == null)
        {
            return;
        }

        SerializedProperty moods = rules.FindProperty("moods");
        for (int i = 0; i < moods.arraySize; i++)
        {
            SerializedProperty weights = moods.GetArrayElementAtIndex(i).FindPropertyRelative("treeWeights");
            if (tree < weights.arraySize)
            {
                weights.DeleteArrayElementAtIndex(tree);
            }
        }
    }

    private static void DropZone(SerializedProperty list)
    {
        Rect zone = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
        GUI.Box(zone, "+ Drop prefabs here", EditorStyles.helpBox);
        Event current = Event.current;
        if (!zone.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
        {
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (Object dropped in DragAndDrop.objectReferences)
            {
                if (dropped is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
                {
                    list.arraySize++;
                    SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    entry.FindPropertyRelative("weight").floatValue = 1f;
                    entry.FindPropertyRelative("scale").vector2Value = new Vector2(0.85f, 1.2f);
                }
            }
        }

        current.Use();
    }

    private static void DrawIcon(GameObject prefab)
    {
        Rect icon = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
        if (prefab == null)
        {
            return;
        }

        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
        if (preview != null)
        {
            GUI.DrawTexture(icon, preview, ScaleMode.ScaleToFit);
        }
        else if (AssetPreview.IsLoadingAssetPreviews())
        {
            HandleUtility.Repaint();
        }
    }

    private static string NameOf(DetailPrototype prototype) => prototype.prototype != null ? prototype.prototype.name : prototype.prototypeTexture != null ? prototype.prototypeTexture.name : "";

    private static void DrawPlants(RaidMapDefinition map, SerializedObject data, TerrainData terrain)
    {
        ContentUtility.BeginPanel("Plants", "What grows in patches on the forest floor. Rarity sets how few patches there are, Thickness how " +
                                            "full each one is, and Loves wet how much it gathers around water and in damp ground. " +
                                            "Grass and bushes are set per ground, on the Ground tab.");
        if (terrain == null)
        {
            EditorGUILayout.HelpBox("The raid's scene has no terrain to grow on.", MessageType.Warning);
            ContentUtility.EndPanel();
            return;
        }

        SerializedProperty plants = data.FindProperty("plants");
        if (plants.arraySize == 0)
        {
            EditorGUILayout.HelpBox("This map grows the standard forest floor. Copy it here to change it.", MessageType.Info);
            if (GUILayout.Button("Copy the Standard Plants", GUILayout.Height(24f)))
            {
                foreach (TerrainGrowth.Plant plant in TerrainGrowth.DefaultPlants)
                {
                    AddPlant(plants, plant);
                }
            }

            ContentUtility.EndPanel();
            return;
        }

        int remove = -1;
        for (int i = 0; i < plants.arraySize; i++)
        {
            SerializedProperty plant = plants.GetArrayElementAtIndex(i);
            string name = plant.FindPropertyRelative("prototype").stringValue;
            DetailPrototype prototype = Array.Find(terrain.detailPrototypes, candidate => NameOf(candidate) == name);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawIcon(prototype?.prototype);
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        SerializedProperty enabled = plant.FindPropertyRelative("enabled");
                        enabled.boolValue = EditorGUILayout.ToggleLeft(new GUIContent(name, "Off: none of this plant grows."), enabled.boolValue, EditorStyles.boldLabel);
                        if (GUILayout.Button(new GUIContent("×", "Stop growing this plant"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)))
                        {
                            remove = i;
                        }
                    }

                    if (prototype == null)
                    {
                        EditorGUILayout.LabelField("⚠ Not among the terrain's details, so it never grows.", ContentUtility.WrappedMiniLabel);
                    }

                    using (new EditorGUI.DisabledScope(!plant.FindPropertyRelative("enabled").boolValue))
                    {
                        EditorGUILayout.PropertyField(plant.FindPropertyRelative("rarity"), new GUIContent("Rarity"));
                        EditorGUILayout.PropertyField(plant.FindPropertyRelative("thickness"), new GUIContent("Thickness"));
                        EditorGUILayout.PropertyField(plant.FindPropertyRelative("loveOfWet"), new GUIContent("Loves wet"));
                        EditorGUILayout.PropertyField(plant.FindPropertyRelative("patchSize"), new GUIContent("Patch size (m)"));
                        EditorGUILayout.PropertyField(plant.FindPropertyRelative("clearing"), new GUIContent("Clears grass (cells)"));
                    }
                }
            }
        }

        if (remove >= 0)
        {
            plants.DeleteArrayElementAtIndex(remove);
        }

        DrawAddPlant(map, plants, terrain);
        ContentUtility.EndPanel();
    }

    private static void AddPlant(SerializedProperty plants, TerrainGrowth.Plant plant)
    {
        plants.arraySize++;
        SerializedProperty entry = plants.GetArrayElementAtIndex(plants.arraySize - 1);
        entry.FindPropertyRelative("prototype").stringValue = plant.prototype;
        entry.FindPropertyRelative("enabled").boolValue = plant.enabled;
        entry.FindPropertyRelative("patchSize").floatValue = plant.patchSize;
        entry.FindPropertyRelative("rarity").floatValue = plant.rarity;
        entry.FindPropertyRelative("thickness").floatValue = plant.thickness;
        entry.FindPropertyRelative("clearing").intValue = plant.clearing;
        entry.FindPropertyRelative("loveOfWet").floatValue = plant.loveOfWet;
    }

    private static void DrawAddPlant(RaidMapDefinition map, SerializedProperty plants, TerrainData terrain)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Add Plant", EditorStyles.miniButton, GUILayout.Width(90f)))
            {
                var menu = new GenericMenu();
                foreach (DetailPrototype prototype in terrain.detailPrototypes)
                {
                    string name = NameOf(prototype);
                    if (name == "Short Grass" || name == "Tall Grass" || name == "Bush")
                    {
                        continue;
                    }

                    bool used = false;
                    foreach (TerrainGrowth.Plant plant in map.Plants)
                    {
                        used |= plant.prototype == name;
                    }

                    if (used)
                    {
                        menu.AddDisabledItem(new GUIContent(name));
                    }
                    else
                    {
                        menu.AddItem(new GUIContent(name), false, () =>
                        {
                            plants.serializedObject.Update();
                            AddPlant(plants, new TerrainGrowth.Plant(name, 6f, 0.7f, 0.4f, 0));
                            plants.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }

                menu.ShowAsContext();
            }

            GUILayout.Label(new GUIContent("New from a prefab:", "A mesh prefab made a detail on the raid's terrain and added to the list."), EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            var added = (GameObject)EditorGUILayout.ObjectField(null, typeof(GameObject), false);
            if (added != null)
            {
                AddPrototype(terrain, added, plants);
            }
        }
    }

    private static void AddPrototype(TerrainData terrain, GameObject prefab, SerializedProperty plants)
    {
        var prototype = new DetailPrototype
        {
            usePrototypeMesh = true,
            prototype = prefab,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = 0.8f,
            maxWidth = 1.3f,
            minHeight = 0.75f,
            maxHeight = 1.3f,
            noiseSpread = 0.35f,
            alignToGround = 0.6f,
            positionJitter = 0.8f,
            useDensityScaling = true,
        };
        if (!prototype.Validate(out string error))
        {
            EditorUtility.DisplayDialog("Can't grow this prefab", error, "OK");
            return;
        }

        Undo.RecordObject(terrain, "Add Detail");
        var prototypes = new List<DetailPrototype>(terrain.detailPrototypes) { prototype };
        terrain.detailPrototypes = prototypes.ToArray();
        terrain.RefreshPrototypes();
        EditorUtility.SetDirty(terrain);
        AddPlant(plants, new TerrainGrowth.Plant(prefab.name, 6f, 0.7f, 0.4f, 0));
    }

    private static void DrawWater(RaidLayoutRules rules, SerializedObject data, RaidMapDefinition map)
    {
        if (rules == null)
        {
            EditorGUILayout.HelpBox("Ponds come with generated maps. A painted map's water is placed in its scene (Safe Haven › World › Water).", MessageType.Info);
            return;
        }

        ContentUtility.BeginPanel("Ponds", "Ponds lie beside a trail or at the edge of a clearing, never on a trail or a site. They are the last thing placed, so one " +
                                           "that finds no room comes out smaller, or not at all.");
        Field(data.FindProperty("ponds"), new GUIContent("How many (least, most)", "Rolled each run."));
        EditorGUILayout.PropertyField(data.FindProperty("pondRadius"), new GUIContent("Radius (smallest, largest)", "Metres. A run's ponds are spread over this range."));
        EditorGUILayout.PropertyField(data.FindProperty("pondDepth"), new GUIContent("Depth (smallest, largest)", "Metres at the middle. The player swims from the Swim Depth in Player Movement Settings."));
        EditorGUILayout.PropertyField(data.FindProperty("pondStretch"), new GUIContent("Longest shape", "How long a pond can be for its width. 1 keeps them round."));
        EditorGUILayout.PropertyField(data.FindProperty("pondMaterial"), new GUIContent("Water material"));
        if (data.FindProperty("ponds").vector2IntValue.y > 0 && data.FindProperty("pondMaterial").objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Without a material there is only an empty hollow.", MessageType.Warning);
        }

        EditorGUILayout.Space(2f);
        if (GUILayout.Button($"Check {SeedsChecked} Runs", EditorStyles.miniButton, GUILayout.Width(110f)))
        {
            data.ApplyModifiedProperties();
            waterReport = CheckPonds(rules, map.Size);
        }

        if (!string.IsNullOrEmpty(waterReport))
        {
            EditorGUILayout.LabelField(waterReport, ContentUtility.WrappedMiniLabel);
        }

        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Drinking and filling", "What the player gets from using a pond with the Interact key, anywhere along its shore. Set Thirst to 0 and clear the item to make ponds only scenery.");
        EditorGUILayout.PropertyField(data.FindProperty("pondDrink"), new GUIContent("Thirst restored"));
        EditorGUILayout.PropertyField(data.FindProperty("pondFillItem"), new GUIContent("Fills"));
        using (new EditorGUI.DisabledScope(data.FindProperty("pondFillItem").objectReferenceValue == null))
        {
            EditorGUILayout.PropertyField(data.FindProperty("pondFillUpTo"), new GUIContent("Tops the bag up to"));
        }

        EditorGUILayout.PropertyField(data.FindProperty("pondReach"), new GUIContent("Reach from shore (m)"));
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Wet ground", "Plants that love the wet (Plants tab) gather where the ground is wet: around water and in the forest's damp patches.");
        EditorGUILayout.PropertyField(data.FindProperty("wetReach"), new GUIContent("Wet around water (m)"));
        EditorGUILayout.PropertyField(data.FindProperty("dampGround"), new GUIContent("Damp patches"));
        ContentUtility.EndPanel();
    }

    private static string CheckPonds(RaidLayoutRules rules, float size)
    {
        int total = 0, most = 0, none = 0;
        float smallest = float.MaxValue, largest = 0f;
        for (int seed = 1; seed <= SeedsChecked; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, size, seed);
            total += plan.Ponds.Count;
            most = Mathf.Max(most, plan.Ponds.Count);
            none += plan.Ponds.Count == 0 ? 1 : 0;
            foreach (RaidLayoutPond pond in plan.Ponds)
            {
                smallest = Mathf.Min(smallest, pond.Radii.x);
                largest = Mathf.Max(largest, pond.Radii.x);
            }
        }

        string sizes = total > 0 ? $", {smallest:0.#} to {largest:0.#} m in radius" : "";
        return $"{(float)total / SeedsChecked:0.#} ponds a run on average, at most {most}{sizes}. {none} of {SeedsChecked} runs have none.";
    }

    // The mist lives in the raid's scene and its volume, as it does for every other scene, so what is tuned here is what the scene view shows.
    private static void DrawMist(RaidSceneContext scene)
    {
        ContentUtility.BeginPanel("Mist and fog", "Three layers. Lower Density and Thickness keep the mist down around the player's feet; the weather and hour decide how much of it comes out.");
        if (!scene.DrawSceneBar("tune its mist"))
        {
            ContentUtility.EndPanel();
            return;
        }

        TerrainMist mist = null;
        FogArea bank = null;
        AtmosphericFog fog = null;
        foreach (GameObject root in scene.Scene.GetRootGameObjects())
        {
            mist = mist != null ? mist : root.GetComponentInChildren<TerrainMist>(true);
            bank = bank != null ? bank : root.GetComponentInChildren<FogArea>(true);
            foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
            {
                if (fog == null && volume.isGlobal && volume.priority <= 0f && volume.sharedProfile != null)
                {
                    volume.sharedProfile.TryGet(out fog);
                }
            }
        }

        DrawGroundMist(mist);
        DrawGlobalFog(fog);
        DrawFogBank(bank);
        if (scene.Scene.isDirty && GUILayout.Button("Save the Raid Scene", GUILayout.Height(24f)))
        {
            EditorSceneManager.SaveScene(scene.Scene);
        }

        ContentUtility.EndPanel();
    }

    private static void DrawGroundMist(TerrainMist mist)
    {
        EditorGUILayout.LabelField("Ground mist", EditorStyles.miniBoldLabel);
        if (mist == null)
        {
            EditorGUILayout.LabelField("The terrain has no Terrain Mist.", ContentUtility.WrappedMiniLabel);
            return;
        }

        SerializedObject data = Data(mist);
        foreach (string field in new[] { "density", "thickness", "bottom", "coverage", "cloudSize" })
        {
            EditorGUILayout.PropertyField(data.FindProperty(field));
        }

        Field(data.FindProperty("waveAmplitude"));
        EditorGUILayout.PropertyField(data.FindProperty("lowlandBoost"));
        EditorGUILayout.PropertyField(data.FindProperty("waterBoost"));

        data.ApplyModifiedProperties();
    }

    private static void DrawGlobalFog(AtmosphericFog fog)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Fog over the land", EditorStyles.miniBoldLabel);
        if (fog == null)
        {
            EditorGUILayout.LabelField("The scene's volume has no Atmospheric Fog.", ContentUtility.WrappedMiniLabel);
            return;
        }

        SerializedObject data = Data(fog);
        FogParameter(data, "density", 0f, 0.1f, "Density");
        FogParameter(data, "followTerrain", 0f, 1f, "Follows the ground");
        FogParameter(data, "baseHeight", 0f, 5f, "Thick up to (m)");
        FogParameter(data, "heightFalloff", 0f, 2f, "Thins with height");
        FogParameter(data, "bankStrength", 0f, 1f, "Banks and gaps");
        FogParameter(data, "noiseStrength", 0f, 1f, "Patchiness");
        data.ApplyModifiedProperties();
    }

    private static void FogParameter(SerializedObject data, string field, float min, float max, string label)
    {
        SerializedProperty parameter = data.FindProperty(field);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.Slider(parameter.FindPropertyRelative("m_Value"), min, max, new GUIContent(label, parameter.tooltip));
        if (EditorGUI.EndChangeCheck())
        {
            parameter.FindPropertyRelative("m_OverrideState").boolValue = true;
        }
    }

    private static void DrawFogBank(FogArea bank)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(new GUIContent("Fog banks over clearings", "The Layout tab's moods say how many more a run gets."), EditorStyles.miniBoldLabel);
        if (bank == null)
        {
            EditorGUILayout.LabelField("The scene has no Fog Area, so a run has no banks.", ContentUtility.WrappedMiniLabel);
            return;
        }

        SerializedObject data = Data(bank);
        foreach (string field in new[] { "density", "radius", "height", "softness" })
        {
            EditorGUILayout.PropertyField(data.FindProperty(field));
        }

        data.ApplyModifiedProperties();
    }
}
