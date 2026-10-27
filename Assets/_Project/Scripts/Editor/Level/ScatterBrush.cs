using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Paints prefabs on the ground in the Scene view (Safe Haven > World > Place > Scatter Brush): pick a palette, press Paint,
/// then drag in the Scene view. Shift-drag erases what this palette painted, [ and ] resize the brush, Esc stops.
/// Objects keep the palette's spacing, sit on the ground by the bottom of their meshes, get a random turn and size,
/// and go under a "Level Scatter / &lt;palette&gt;" folder object; each stroke is one undo. Painted gameplay objects
/// (resource nodes) get save ids. The Content Editor's Prefab Browser opens it with a single prefab.
/// </summary>
public sealed class ScatterBrush : EditorWindow
{
    public const string PaletteFolder = "Assets/_Project/Data/Level/Palettes";
    public const string RootName = "Level Scatter";

    private ScatterPalette palette;
    private float radius = 5f;
    [Range(1, 20)] private int density = 4;
    private bool painting;
    private Vector3 lastDab;
    private bool stroking;
    private bool placedSaveable;
    private System.Random random = new System.Random();
    private List<ScatterPalette> palettes;
    private Vector2 scroll;

    [MenuItem("Safe Haven/World/Place/Scatter Brush")]
    public static void Open() => GetWindow<ScatterBrush>("Scatter Brush");

    /// <summary>Opens the brush painting only <paramref name="prefab"/> (a palette kept in memory, not saved).</summary>
    public static void OpenWith(GameObject prefab)
    {
        var temporary = CreateInstance<ScatterPalette>();
        temporary.name = prefab.name + " (just this)";
        temporary.hideFlags = HideFlags.DontSave;
        temporary.entries.Add(new ScatterPalette.Entry { prefab = prefab });
        float size = Mathf.Max(1f, PrefabFootprint(prefab));
        temporary.spacing = size * 1.2f;
        ScatterBrush window = GetWindow<ScatterBrush>("Scatter Brush");
        window.palette = temporary;
        window.SetPainting(true);
    }

    private void OnEnable()
    {
        palettes = null;
        EnsureDefaultPalettes();
    }

    private void OnDisable() => SetPainting(false);

    private void OnProjectChange() => palettes = null;

    private void SetPainting(bool on)
    {
        painting = on;
        SceneView.duringSceneGui -= OnScene;
        if (on)
        {
            SceneView.duringSceneGui += OnScene;
        }

        SceneView.RepaintAll();
        Repaint();
    }

    private void OnGUI()
    {
        palettes ??= AssetDatabase.FindAssets("t:" + nameof(ScatterPalette))
            .Select(g => AssetDatabase.LoadAssetAtPath<ScatterPalette>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).ToList();
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (ScatterPalette option in palettes)
            {
                if (GUILayout.Toggle(option == palette, option.name, EditorStyles.miniButton) && option != palette)
                {
                    palette = option;
                }
            }
        }

        palette = (ScatterPalette)EditorGUILayout.ObjectField("Palette", palette, typeof(ScatterPalette), false);
        radius = EditorGUILayout.Slider(new GUIContent("Brush radius", "[ and ] in the Scene view"), radius, 0.5f, 40f);
        density = EditorGUILayout.IntSlider(new GUIContent("Tries per dab", "More fills the brush faster (spacing still applies)"), density, 1, 20);
        using (new EditorGUI.DisabledScope(palette == null))
        {
            bool paint = GUILayout.Toggle(painting, painting ? "Painting: drag in the Scene view (Shift erases, Esc stops)" : "Paint", "Button", GUILayout.Height(28));
            if (paint != painting)
            {
                SetPainting(paint);
            }
        }

        if (palette != null)
        {
            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var serialized = new SerializedObject(palette);
            EditorGUILayout.PropertyField(serialized.FindProperty("spacing"));
            EditorGUILayout.PropertyField(serialized.FindProperty("entries"), true);
            serialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }
    }

    private void OnScene(SceneView view)
    {
        if (!painting || palette == null)
        {
            return;
        }

        Event e = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(control);
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
                radius = Mathf.Clamp(radius * (e.keyCode == KeyCode.RightBracket ? 1.2f : 1f / 1.2f), 0.5f, 40f);
                Repaint();
                e.Use();
            }
        }

        if (!MouseGround(e.mousePosition, out Vector3 point, out Vector3 normal))
        {
            return;
        }

        bool erase = e.shift;
        Handles.color = erase ? new Color(1f, 0.4f, 0.3f, 0.9f) : new Color(0.4f, 1f, 0.5f, 0.9f);
        Handles.DrawWireDisc(point, normal, radius);
        Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.08f);
        Handles.DrawSolidDisc(point, normal, radius);
        Handles.Label(point + Vector3.up * 0.5f, erase ? "Erase" : palette.name, EditorStyles.whiteMiniLabel);

        if (e.alt || e.button != 0)
        {
            return;
        }

        if (e.type == EventType.MouseDown)
        {
            Undo.IncrementCurrentGroup();
            stroking = true;
            placedSaveable = false;
            Dab(point, erase);
            lastDab = point;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && stroking)
        {
            if (Vector3.Distance(point, lastDab) > radius * 0.4f)
            {
                Dab(point, erase);
                lastDab = point;
            }

            e.Use();
        }
        else if (e.type == EventType.MouseUp && stroking)
        {
            stroking = false;
            Undo.SetCurrentGroupName(erase ? "Scatter Erase" : "Scatter Paint");
            if (placedSaveable)
            {
                SaveableEntityTools.AssignSceneIds(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            e.Use();
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            view.Repaint();
        }
    }

    private void Dab(Vector3 point, bool erase)
    {
        Transform folder = Folder(palette, true);
        if (erase)
        {
            Erase(folder, palette, point, radius);
            return;
        }

        foreach (GameObject placed in Paint(palette, point, radius, density, folder, random))
        {
            placedSaveable |= placed.GetComponentInChildren<SaveableEntity>() != null;
        }
    }

    private static bool MouseGround(Vector2 mouse, out Vector3 point, out Vector3 normal)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (hit.collider is TerrainCollider || hit.transform.root.name != RootName)
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }
        }

        point = Vector3.zero;
        normal = Vector3.up;
        return false;
    }

    /// <summary>The scene folder painted objects of <paramref name="palette"/> go under.</summary>
    public static Transform Folder(ScatterPalette palette, bool create)
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            if (!create) return null;
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Scatter Paint");
        }

        Transform folder = root.transform.Find(palette.name);
        if (folder == null && create)
        {
            folder = new GameObject(palette.name).transform;
            Undo.RegisterCreatedObjectUndo(folder.gameObject, "Scatter Paint");
            Undo.SetTransformParent(folder, root.transform, "Scatter Paint");
        }

        return folder;
    }

    /// <summary>Places up to <paramref name="tries"/> objects at random points in the circle, keeping the palette's
    /// spacing from everything already in <paramref name="folder"/> (undoable). Returns what was placed.</summary>
    public static List<GameObject> Paint(ScatterPalette palette, Vector3 centre, float radius, int tries, Transform folder, System.Random random)
    {
        var placed = new List<GameObject>();
        var taken = new List<Vector3>();
        foreach (Transform child in folder)
        {
            taken.Add(child.position);
        }

        for (int i = 0; i < tries; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (taken.Any(t => new Vector2(t.x - point.x, t.z - point.z).sqrMagnitude < palette.spacing * palette.spacing))
            {
                continue;
            }

            ScatterPalette.Entry entry = palette.Pick(random);
            if (entry == null || !GroundPlacement.TryGround(point, out Vector3 ground, out Vector3 normal, folder.root))
            {
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, folder.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Scatter Paint");
            instance.transform.SetParent(folder, true);
            float yaw = entry.randomYaw ? (float)random.NextDouble() * 360f : 0f;
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), entry.alignToSlope);
            float scale = Mathf.Lerp(entry.scale.x, entry.scale.y, (float)random.NextDouble());
            instance.transform.SetPositionAndRotation(ground, tilt * Quaternion.Euler(0f, yaw, 0f));
            instance.transform.localScale = entry.prefab.transform.localScale * scale;
            instance.transform.position = ground + Vector3.up * (GroundPlacement.BottomOffset(instance) - entry.sink);
            taken.Add(ground);
            placed.Add(instance);
        }

        return placed;
    }

    /// <summary>Removes this palette's painted objects inside the circle (undoable); returns how many.</summary>
    public static int Erase(Transform folder, ScatterPalette palette, Vector3 centre, float radius)
    {
        if (folder == null)
        {
            return 0;
        }

        List<GameObject> doomed = folder.Cast<Transform>()
            .Where(t => new Vector2(t.position.x - centre.x, t.position.z - centre.z).sqrMagnitude < radius * radius)
            .Select(t => t.gameObject).ToList();
        foreach (GameObject go in doomed)
        {
            Undo.DestroyObjectImmediate(go);
        }

        return doomed.Count;
    }

    private static float PrefabFootprint(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1f;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return Mathf.Max(b.size.x, b.size.z);
    }

    // ---------------------------------------------------------------- default palettes

    [MenuItem("Safe Haven/World/Place/Create Default Scatter Palettes")]
    private static void CreateDefaultsMenu() => Debug.Log($"Level: {EnsureDefaultPalettes()} scatter palette(s) created in {PaletteFolder}.");

    /// <summary>Forest trees, rocks and logs, wood and stone nodes and forage, from the project's own prefabs. Existing
    /// palettes are kept. Returns how many were created.</summary>
    public static int EnsureDefaultPalettes()
    {
        Directory.CreateDirectory(PaletteFolder);
        const string forest = "Assets/_Project/Art/Forest/Prefabs/";
        const string nodes = "Assets/_Project/Prefabs/Resources/";
        int created = 0;
        created += Palette("Forest Trees", 4f, (forest + "Spruce A.prefab", 1f, 0.15f), (forest + "Spruce B.prefab", 1f, 0.15f),
                           (forest + "Spruce C.prefab", 1f, 0.15f), (forest + "Pine.prefab", 0.8f, 0.15f), (forest + "Dead Spruce.prefab", 0.25f, 0.15f),
                           ("Assets/_Project/Models/Generated/Vegetation/rich_oak/rich_oak_Wind.prefab", 0.5f, 0.1f));
        created += Palette("Rocks and Logs", 3f, (forest + "Boulder A.prefab", 1f, 0.6f), (forest + "Boulder B.prefab", 1f, 0.6f),
                           (forest + "Fallen Log.prefab", 0.4f, 0.9f));
        created += Palette("Wood and Stone Nodes", 5f, (nodes + "OakWoodNode.prefab", 1f, 0.1f), (nodes + "GreyStoneNode.prefab", 1f, 0.4f),
                           (nodes + "HardwoodNode.prefab", 0.3f, 0.1f), (nodes + "GraniteNode.prefab", 0.3f, 0.4f));
        created += Palette("Forage", 3f, (nodes + "BerriesNode.prefab", 1f, 0.3f), (nodes + "HealingHerbNode.prefab", 0.6f, 0.3f),
                           (nodes + "FlaxNode.prefab", 0.6f, 0.3f), (nodes + "NettleFiberNode.prefab", 0.6f, 0.3f));
        if (created > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return created;
    }

    private static int Palette(string name, float spacing, params (string Path, float Weight, float Align)[] prefabs)
    {
        string path = $"{PaletteFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<ScatterPalette>(path) != null)
        {
            return 0;
        }

        var palette = CreateInstance<ScatterPalette>();
        palette.spacing = spacing;
        foreach (var (prefabPath, weight, align) in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                palette.entries.Add(new ScatterPalette.Entry { prefab = prefab, weight = weight, alignToSlope = align });
            }
        }

        if (palette.entries.Count == 0)
        {
            Object.DestroyImmediate(palette);
            return 0;
        }

        AssetDatabase.CreateAsset(palette, path);
        return 1;
    }
}
