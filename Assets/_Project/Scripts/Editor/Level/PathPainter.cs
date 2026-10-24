using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paints a path into the terrain (Safe Haven > Level > Path Painter): press Draw, click points along the way in the
/// Scene view (drag a point to move it, Backspace removes the last, Esc stops), pick the ground layer, width and soft
/// edge, then Paint Path. The path follows a smooth curve through the points; the edge is ragged so it does not look
/// ruled. It clears grass and plants along the middle and can smooth the ground under it. Undoable.
/// </summary>
public sealed class PathPainter : EditorWindow
{
    public struct Settings
    {
        public int Layer;
        public float Width;
        public float Softness;
        public float Strength;
        public bool ClearDetails;
        public float Smooth;
    }

    private readonly List<Vector3> points = new List<Vector3>();
    private bool drawing;
    private int layer;
    private float width = 2.5f;
    private float softness = 1f;
    private float strength = 1f;
    private bool clearDetails = true;
    private float smooth;

    [MenuItem("Safe Haven/Level/Path Painter")]
    public static void Open() => GetWindow<PathPainter>("Path Painter");

    private void OnDisable() => SetDrawing(false);

    private void SetDrawing(bool on)
    {
        drawing = on;
        SceneView.duringSceneGui -= OnScene;
        if (on || points.Count > 0)
        {
            SceneView.duringSceneGui += OnScene; // keep showing the path while it waits to be painted
        }

        SceneView.RepaintAll();
        Repaint();
    }

    private void OnGUI()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            EditorGUILayout.HelpBox("The open scene has no terrain.", MessageType.Info);
            return;
        }

        string[] layers = terrain.terrainData.terrainLayers.Select(l => l != null ? l.name : "(none)").ToArray();
        layer = EditorGUILayout.Popup("Ground layer", Mathf.Clamp(layer, 0, layers.Length - 1), layers);
        width = EditorGUILayout.Slider("Width (m)", width, 0.5f, 12f);
        softness = EditorGUILayout.Slider(new GUIContent("Soft edge (m)", "How far the edge fades and frays"), softness, 0f, 4f);
        strength = EditorGUILayout.Slider("Strength", strength, 0.1f, 1f);
        clearDetails = EditorGUILayout.Toggle(new GUIContent("Clear grass and plants", "Along the middle of the path"), clearDetails);
        smooth = EditorGUILayout.Slider(new GUIContent("Smooth the ground", "Evens out bumps under the path"), smooth, 0f, 1f);
        EditorGUILayout.Space();
        bool draw = GUILayout.Toggle(drawing, drawing ? "Drawing: click the ground to add points (Backspace removes, Esc stops)" : "Draw", "Button", GUILayout.Height(26));
        if (draw != drawing)
        {
            SetDrawing(draw);
        }

        EditorGUILayout.LabelField($"{points.Count} point(s), {Length():0.0} m");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(points.Count < 2))
            {
                if (GUILayout.Button("Paint Path", GUILayout.Height(26)))
                {
                    int changed = Paint(terrain, points, new Settings
                    {
                        Layer = layer, Width = width, Softness = softness, Strength = strength, ClearDetails = clearDetails, Smooth = smooth
                    });
                    Debug.Log($"Level: painted a {Length():0} m path over {changed} terrain texel(s).");
                }
            }

            if (GUILayout.Button("Clear Points", GUILayout.Height(26)))
            {
                points.Clear();
                SetDrawing(drawing);
            }
        }
    }

    private float Length()
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
        return length;
    }

    private void OnScene(SceneView view)
    {
        Event e = Event.current;
        List<Vector3> curve = Curve(points, 6);
        Handles.color = new Color(1f, 0.85f, 0.4f, 0.9f);
        if (curve.Count > 1)
        {
            Handles.DrawAAPolyLine(4f, curve.Select(p => p + Vector3.up * 0.05f).ToArray());
            for (int i = 0; i < curve.Count - 1; i += 2)
            {
                Vector3 along = (curve[i + 1] - curve[i]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, along) * (width * 0.5f);
                Handles.DrawLine(curve[i] - side, curve[i] + side);
            }
        }

        for (int i = 0; i < points.Count; i++)
        {
            float size = HandleUtility.GetHandleSize(points[i]) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(points[i], size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck() && GroundPlacement.TryGround(moved, out Vector3 ground, out _))
            {
                points[i] = ground;
                Repaint();
            }
        }

        if (!drawing)
        {
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            SetDrawing(false);
            e.Use();
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace && points.Count > 0)
        {
            points.RemoveAt(points.Count - 1);
            Repaint();
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
            {
                points.Add(hit.point);
                Repaint();
                e.Use();
            }
        }
    }

    /// <summary>A smooth curve (Catmull-Rom) through the points, <paramref name="steps"/> samples per span.</summary>
    public static List<Vector3> Curve(IReadOnlyList<Vector3> points, int steps)
    {
        var curve = new List<Vector3>();
        if (points.Count < 2)
        {
            curve.AddRange(points);
            return curve;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Count - 1)];
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                curve.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
            }
        }

        curve.Add(points[points.Count - 1]);
        return curve;
    }

    /// <summary>Paints the path into the terrain (undoable): the ground layer, cleared details, smoothed heights.
    /// Returns the number of terrain paint texels changed.</summary>
    public static int Paint(Terrain terrain, IReadOnlyList<Vector3> points, Settings settings)
    {
        TerrainData data = terrain.terrainData;
        List<Vector3> curve = Curve(points, 8);
        if (curve.Count < 2 || settings.Layer < 0 || settings.Layer >= data.alphamapLayers)
        {
            return 0;
        }

        var undo = new List<Object> { data };
        undo.AddRange(data.alphamapTextures);
        Undo.RegisterCompleteObjectUndo(undo.ToArray(), "Paint Path");
        Vector3 origin = terrain.transform.position;
        float half = settings.Width * 0.5f;
        float reach = half + settings.Softness;
        float minX = curve.Min(p => p.x) - reach, maxX = curve.Max(p => p.x) + reach;
        float minZ = curve.Min(p => p.z) - reach, maxZ = curve.Max(p => p.z) + reach;

        float Distance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < curve.Count - 1; i++)
            {
                Vector2 a = new Vector2(curve[i].x, curve[i].z), b = new Vector2(curve[i + 1].x, curve[i + 1].z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }

            return best;
        }

        // Weight 1 in the middle, fading over the soft edge, which frays with noise so it does not look ruled.
        float Weight(float x, float z)
        {
            float d = Distance(x, z) + (Mathf.PerlinNoise(x * 0.7f, z * 0.7f) - 0.5f) * settings.Softness * 0.8f;
            return settings.Strength * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - (half - settings.Softness * 0.5f)) / Mathf.Max(settings.Softness, 0.01f))));
        }

        // Paint.
        int res = data.alphamapResolution;
        int ax0 = Mathf.Clamp(Mathf.FloorToInt((minX - origin.x) / data.size.x * res), 0, res - 1);
        int ax1 = Mathf.Clamp(Mathf.CeilToInt((maxX - origin.x) / data.size.x * res), 0, res - 1);
        int az0 = Mathf.Clamp(Mathf.FloorToInt((minZ - origin.z) / data.size.z * res), 0, res - 1);
        int az1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - origin.z) / data.size.z * res), 0, res - 1);
        int w = ax1 - ax0 + 1, h = az1 - az0 + 1;
        float[,,] maps = data.GetAlphamaps(ax0, az0, w, h);
        int changed = 0;
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float weight = Weight(origin.x + (ax0 + x + 0.5f) / res * data.size.x, origin.z + (az0 + z + 0.5f) / res * data.size.z);
                if (weight <= 0.001f) continue;
                for (int l = 0; l < data.alphamapLayers; l++)
                {
                    maps[z, x, l] = Mathf.Lerp(maps[z, x, l], l == settings.Layer ? 1f : 0f, weight);
                }

                changed++;
            }
        }

        data.SetAlphamaps(ax0, az0, maps);

        // Clear grass and plants along the middle.
        if (settings.ClearDetails && data.detailResolution > 0)
        {
            int dres = data.detailResolution;
            int dx0 = Mathf.Clamp(Mathf.FloorToInt((minX - origin.x) / data.size.x * dres), 0, dres - 1);
            int dx1 = Mathf.Clamp(Mathf.CeilToInt((maxX - origin.x) / data.size.x * dres), 0, dres - 1);
            int dz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - origin.z) / data.size.z * dres), 0, dres - 1);
            int dz1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - origin.z) / data.size.z * dres), 0, dres - 1);
            for (int l = 0; l < data.detailPrototypes.Length; l++)
            {
                int[,] cells = data.GetDetailLayer(dx0, dz0, dx1 - dx0 + 1, dz1 - dz0 + 1, l);
                for (int z = 0; z < cells.GetLength(0); z++)
                {
                    for (int x = 0; x < cells.GetLength(1); x++)
                    {
                        if (Weight(origin.x + (dx0 + x + 0.5f) / dres * data.size.x, origin.z + (dz0 + z + 0.5f) / dres * data.size.z) > 0.55f)
                        {
                            cells[z, x] = 0;
                        }
                    }
                }

                data.SetDetailLayer(dx0, dz0, l, cells);
            }
        }

        // Smooth the ground under the path (a 3x3 blur blended in by the path's weight).
        if (settings.Smooth > 0f)
        {
            int hres = data.heightmapResolution;
            int hx0 = Mathf.Clamp(Mathf.FloorToInt((minX - origin.x) / data.size.x * (hres - 1)) - 1, 0, hres - 1);
            int hx1 = Mathf.Clamp(Mathf.CeilToInt((maxX - origin.x) / data.size.x * (hres - 1)) + 1, 0, hres - 1);
            int hz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - origin.z) / data.size.z * (hres - 1)) - 1, 0, hres - 1);
            int hz1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - origin.z) / data.size.z * (hres - 1)) + 1, 0, hres - 1);
            int hw = hx1 - hx0 + 1, hh = hz1 - hz0 + 1;
            float[,] heights = data.GetHeights(hx0, hz0, hw, hh);
            var result = (float[,])heights.Clone();
            for (int z = 1; z < hh - 1; z++)
            {
                for (int x = 1; x < hw - 1; x++)
                {
                    float weight = Weight(origin.x + (hx0 + x) / (float)(hres - 1) * data.size.x, origin.z + (hz0 + z) / (float)(hres - 1) * data.size.z);
                    if (weight <= 0.001f) continue;
                    float sum = 0f;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                            sum += heights[z + dz, x + dx];
                    result[z, x] = Mathf.Lerp(heights[z, x], sum / 9f, weight * settings.Smooth);
                }
            }

            data.SetHeights(hx0, hz0, result);
        }

        EditorUtility.SetDirty(data);
        terrain.Flush();
        if (terrain.GetComponent<GrassTerrainColour>() != null)
        {
            GrassSetup.BakeGroundColour(terrain);
        }

        return changed;
    }
}
