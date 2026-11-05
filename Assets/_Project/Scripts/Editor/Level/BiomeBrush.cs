using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paints a whole biome at once in the Scene view (Safe Haven › World › Paint › Biome Brush): pick a preset (BiomePreset:
/// the Haven's meadow, the Misty Woods' floor, a riverbank...), press Paint and drag. One stroke lays the biome's ground,
/// grows its plants as terrain details (each with its own density, slopes and patches) and scatters its props, all kept
/// off steep slopes, out of the water, and, for bank biomes, close to it. Shift-drag erases the biome's plants and props
/// (the ground stays). [ and ] resize the brush; Esc stops. Each stroke is one undo. Missing ground layers and plants are
/// added to the terrain. Ground painted here counts as hand paint, which the World Builder keeps.
/// </summary>
public sealed class BiomeBrush : EditorWindow
{
    public const string Folder = "Assets/_Project/Data/Level/Biomes";

    private BiomePreset preset;
    private float radius = 6f;
    private float strength = 0.7f;
    private bool paintGround = true;
    private bool paintPlants = true;
    private bool paintProps = true;
    private bool painting;
    private bool stroking;
    private Vector3 lastDab;
    private readonly System.Random random = new System.Random();
    private List<BiomePreset> presets;
    private Vector2 scroll;

    [MenuItem("Safe Haven/World/Paint/Biome Brush")]
    public static void Open() => GetWindow<BiomeBrush>("Biome Brush");

    private void OnDisable() => SetPainting(false);

    private void OnProjectChange() => presets = null;

    private void SetPainting(bool on)
    {
        painting = on;
        SceneView.duringSceneGui -= OnScene;
        if (on) SceneView.duringSceneGui += OnScene;
        SceneView.RepaintAll();
        Repaint();
    }

    private void OnGUI()
    {
        presets ??= AssetDatabase.FindAssets("t:" + nameof(BiomePreset))
            .Select(g => AssetDatabase.LoadAssetAtPath<BiomePreset>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).OrderBy(p => p.name).ToList();
        EditorGUILayout.LabelField("Biome", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (BiomePreset option in presets)
            {
                if (GUILayout.Toggle(option == preset, option.name, EditorStyles.miniButton) && option != preset) preset = option;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            preset = (BiomePreset)EditorGUILayout.ObjectField(preset, typeof(BiomePreset), false);
            if (GUILayout.Button("New", GUILayout.Width(50))) preset = CreatePreset("New Biome");
        }

        radius = EditorGUILayout.Slider(new GUIContent("Brush radius", "[ and ] in the Scene view"), radius, 1f, 40f);
        strength = EditorGUILayout.Slider(new GUIContent("Strength", "How much of the biome each dab lays down"), strength, 0.05f, 1f);
        using (new EditorGUILayout.HorizontalScope())
        {
            paintGround = GUILayout.Toggle(paintGround, "Ground", EditorStyles.miniButtonLeft);
            paintPlants = GUILayout.Toggle(paintPlants, "Plants", EditorStyles.miniButtonMid);
            paintProps = GUILayout.Toggle(paintProps, "Props", EditorStyles.miniButtonRight);
        }

        using (new EditorGUI.DisabledScope(preset == null))
        {
            bool paint = GUILayout.Toggle(painting, painting ? "Painting: drag in the Scene view (Shift erases, Esc stops)" : "Paint", "Button", GUILayout.Height(28));
            if (paint != painting) SetPainting(paint);
        }

        if (preset == null)
        {
            return;
        }

        EditorGUILayout.Space();
        if (!string.IsNullOrWhiteSpace(preset.description)) EditorGUILayout.HelpBox(preset.description, MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var serialized = new SerializedObject(preset);
        SerializedProperty property = serialized.GetIterator();
        property.NextVisible(true);
        while (property.NextVisible(false))
        {
            EditorGUILayout.PropertyField(property, true);
        }

        serialized.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
    }

    /// <summary>A new empty preset in the biomes folder.</summary>
    public static BiomePreset CreatePreset(string name)
    {
        Directory.CreateDirectory(Folder);
        var made = CreateInstance<BiomePreset>();
        AssetDatabase.CreateAsset(made, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{name}.asset"));
        return made;
    }

    private void OnScene(SceneView view)
    {
        if (!painting || preset == null)
        {
            return;
        }

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                SetPainting(false);
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
            {
                radius = Mathf.Clamp(radius * (e.keyCode == KeyCode.RightBracket ? 1.2f : 1f / 1.2f), 1f, 40f);
                Repaint();
                e.Use();
            }
        }

        if (!MouseTerrain(e.mousePosition, out Terrain terrain, out Vector3 point, out Vector3 normal))
        {
            return;
        }

        bool erase = e.shift;
        Handles.color = erase ? new Color(1f, 0.4f, 0.3f, 0.9f) : new Color(0.55f, 0.9f, 1f, 0.9f);
        Handles.DrawWireDisc(point, normal, radius);
        Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.08f);
        Handles.DrawSolidDisc(point, normal, radius);
        Handles.Label(point + Vector3.up * 0.5f, erase ? "Erase " + preset.name : preset.name, EditorStyles.whiteMiniLabel);
        if (e.alt || e.button != 0)
        {
            return;
        }

        if (e.type == EventType.MouseDown)
        {
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(new Object[] { terrain.terrainData }.Concat(terrain.terrainData.alphamapTextures).ToArray(), "Biome Paint");
            stroking = true;
            Dab(terrain, point, erase);
            lastDab = point;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && stroking)
        {
            if (Vector3.Distance(point, lastDab) > radius * 0.35f)
            {
                Dab(terrain, point, erase);
                lastDab = point;
            }

            e.Use();
        }
        else if (e.type == EventType.MouseUp && stroking)
        {
            stroking = false;
            Undo.SetCurrentGroupName(erase ? "Biome Erase" : "Biome Paint: " + preset.name);
            EditorUtility.SetDirty(terrain.terrainData);
            e.Use();
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) view.Repaint();
    }

    private void Dab(Terrain terrain, Vector3 point, bool erase)
    {
        if (paintPlants) PaintPlants(terrain, preset, point, radius, erase ? 0f : strength, erase, random);
        if (erase)
        {
            if (paintProps) foreach (BiomePreset.Props props in preset.props) if (props.palette != null) ScatterBrush.Erase(ScatterBrush.Folder(props.palette, false), props.palette, point, radius);
            return;
        }

        if (paintGround) PaintGround(terrain, preset, point, radius, strength);
        if (paintProps) PaintProps(terrain, preset, point, radius, strength, random);
    }

    // ------------------------------------------------------------------ painting (also used by tests)

    /// <summary>Lays the biome's ground under a dab: blended in towards the middle, with a little noise, off steep slopes.</summary>
    public static void PaintGround(Terrain terrain, BiomePreset biome, Vector3 centre, float radius, float strength)
    {
        if (biome.ground == null || biome.groundStrength <= 0f) return;
        TerrainData data = terrain.terrainData;
        int layer = EnsureLayer(data, biome.ground);
        if (layer < 0) return;
        int res = data.alphamapResolution;
        if (!CellRect(terrain, centre, radius, res, out RectInt rect)) return;
        float[,,] maps = data.GetAlphamaps(rect.x, rect.y, rect.width, rect.height);
        int layers = data.alphamapLayers;
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                Vector3 world = CellWorld(terrain, rect.x + x, rect.y + y, res);
                float fall = Falloff(world, centre, radius);
                if (fall <= 0f || !Allowed(terrain, world, biome.groundSlope, biome.nearWater)) continue;
                float noise = 0.65f + 0.35f * Mathf.PerlinNoise(world.x * 0.35f, world.z * 0.35f);
                float add = Mathf.Clamp01(strength * biome.groundStrength * fall * noise);
                for (int l = 0; l < layers; l++)
                {
                    maps[y, x, l] = l == layer ? maps[y, x, l] + add * (1f - maps[y, x, l]) : maps[y, x, l] * (1f - add);
                }
            }
        }

        data.SetAlphamaps(rect.x, rect.y, maps);
    }

    /// <summary>Grows (or with <paramref name="erase"/> clears) the biome's plants under a dab.</summary>
    public static void PaintPlants(Terrain terrain, BiomePreset biome, Vector3 centre, float radius, float strength, bool erase, System.Random random)
    {
        TerrainData data = terrain.terrainData;
        int res = data.detailResolution;
        if (res <= 0 || !CellRect(terrain, centre, radius, res, out RectInt rect)) return;
        var water = Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Exclude);
        foreach (BiomePreset.Detail detail in biome.details)
        {
            if (detail.prefab == null) continue;
            int index = EnsureDetail(data, detail.prefab);
            if (index < 0) continue;
            int[,] cells = data.GetDetailLayer(rect.x, rect.y, rect.width, rect.height, index);
            int most = GrassSetup.MaxPerCell(detail.prefab.name);
            float seed = (detail.prefab.name.GetHashCode() & 0xffff) * 0.013f;
            for (int y = 0; y < rect.height; y++)
            {
                for (int x = 0; x < rect.width; x++)
                {
                    Vector3 world = CellWorld(terrain, rect.x + x, rect.y + y, res);
                    float fall = Falloff(world, centre, radius);
                    if (fall <= 0f) continue;
                    if (erase)
                    {
                        cells[y, x] = 0;
                        continue;
                    }

                    if (!Allowed(terrain, world, detail.slope, biome.nearWater) || GrassSetup.UnderWater(terrain, world, water)) continue;
                    float patch = Mathf.PerlinNoise(world.x / detail.patchSize + seed, world.z / detail.patchSize + seed);
                    float grows = detail.density * strength * fall * Mathf.Lerp(1f, Mathf.Clamp01(patch * 2f - 0.5f), detail.patchiness);
                    if (random.NextDouble() < grows) cells[y, x] = Mathf.Max(cells[y, x], most);
                }
            }

            data.SetDetailLayer(rect.x, rect.y, index, cells);
        }
    }

    /// <summary>Scatters the biome's props under a dab, removing any that land on too steep a slope or away from water.</summary>
    public static List<GameObject> PaintProps(Terrain terrain, BiomePreset biome, Vector3 centre, float radius, float strength, System.Random random)
    {
        var kept = new List<GameObject>();
        foreach (BiomePreset.Props props in biome.props)
        {
            if (props.palette == null || props.tries <= 0) continue;
            int tries = Mathf.Max(1, Mathf.RoundToInt(props.tries * strength));
            foreach (GameObject placed in ScatterBrush.Paint(props.palette, centre, radius, tries, ScatterBrush.Folder(props.palette, true), random))
            {
                if (Allowed(terrain, placed.transform.position, props.slope, biome.nearWater)) kept.Add(placed);
                else Undo.DestroyObjectImmediate(placed);
            }
        }

        return kept;
    }

    // ------------------------------------------------------------------ helpers

    private static bool Allowed(Terrain terrain, Vector3 world, Vector2 slope, float nearWater)
    {
        TerrainData data = terrain.terrainData;
        Vector3 local = world - terrain.transform.position;
        float steep = data.GetSteepness(Mathf.Clamp01(local.x / data.size.x), Mathf.Clamp01(local.z / data.size.z));
        if (steep < slope.x || steep > slope.y) return false;
        return nearWater <= 0f || NearWater(world, nearWater);
    }

    private static bool NearWater(Vector3 world, float reach)
    {
        foreach (WaterSurface surface in WaterSurface.Surfaces)
        {
            if (surface != null && surface.Contains(world, reach, out _)) return true;
        }

        return false;
    }

    private static float Falloff(Vector3 world, Vector3 centre, float radius)
    {
        float d = new Vector2(world.x - centre.x, world.z - centre.z).magnitude / radius;
        return d >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.5f, 1f, d));
    }

    private static bool CellRect(Terrain terrain, Vector3 centre, float radius, int res, out RectInt rect)
    {
        TerrainData data = terrain.terrainData;
        Vector3 local = centre - terrain.transform.position;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((local.x - radius) / data.size.x * res), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((local.x + radius) / data.size.x * res), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((local.z - radius) / data.size.z * res), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((local.z + radius) / data.size.z * res), 0, res - 1);
        rect = new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        return rect.width > 0 && rect.height > 0;
    }

    private static Vector3 CellWorld(Terrain terrain, int x, int z, int res)
    {
        TerrainData data = terrain.terrainData;
        Vector3 o = terrain.transform.position;
        var world = new Vector3(o.x + (x + 0.5f) / res * data.size.x, 0f, o.z + (z + 0.5f) / res * data.size.z);
        world.y = o.y + terrain.SampleHeight(world);
        return world;
    }

    /// <summary>The ground layer's index on the terrain, added when missing (-1 when the terrain is full).</summary>
    public static int EnsureLayer(TerrainData data, TerrainLayer ground)
    {
        int index = System.Array.IndexOf(data.terrainLayers, ground);
        if (index >= 0) return index;
        if (data.terrainLayers.Length >= 16)
        {
            Debug.LogWarning($"Biome Brush: the terrain already has 16 ground layers; {ground.name} can't be added.");
            return -1;
        }

        Undo.RegisterCompleteObjectUndo(data, "Add Ground Layer");
        data.terrainLayers = data.terrainLayers.Append(ground).ToArray();
        EditorUtility.SetDirty(data);
        return data.terrainLayers.Length - 1;
    }

    /// <summary>The plant's detail index on the terrain, added when missing.</summary>
    public static int EnsureDetail(TerrainData data, GameObject prefab)
    {
        DetailPrototype[] prototypes = data.detailPrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prototype == prefab) return i;
        }

        DetailPrototype added = GroundLayerPainter.Detail(prefab);
        if (!added.Validate(out string error))
        {
            Debug.LogWarning($"Biome Brush: {prefab.name} can't be a terrain detail: {error}", prefab);
            return -1;
        }

        Undo.RegisterCompleteObjectUndo(data, "Add Plant");
        data.detailPrototypes = prototypes.Append(added).ToArray();
        GrassSetup.UseInstanceCounts(data);
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        return data.detailPrototypes.Length - 1;
    }

    private static bool MouseTerrain(Vector2 mouse, out Terrain terrain, out Vector3 point, out Vector3 normal)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (hit.collider is TerrainCollider && hit.collider.TryGetComponent(out terrain))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }
        }

        terrain = null;
        point = Vector3.zero;
        normal = Vector3.up;
        return false;
    }
}
