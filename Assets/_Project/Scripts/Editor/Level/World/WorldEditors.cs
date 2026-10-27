using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Inspector and Scene view tools for world features: Draw (click points on the ground), the per-knot handles
/// (river and path width beside each knot, cliff height above it), an outline of the footprint, and buttons to edit
/// the knots with Unity's spline tools, reverse the direction, close the loop and build.</summary>
[CustomEditor(typeof(WorldFeature), true)]
public sealed class WorldFeatureEditor : Editor
{
    private static WorldFeature drawing;

    public static void StartDrawing(WorldFeature feature)
    {
        drawing = feature;
        Selection.activeGameObject = feature.gameObject;
        SceneView.lastActiveSceneView?.Focus();
    }

    public override void OnInspectorGUI()
    {
        var feature = (WorldFeature)target;
        EditorGUILayout.HelpBox(HowTo(feature), MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            bool isDrawing = drawing == feature;
            if (GUILayout.Button(isDrawing ? "Stop Drawing" : "Draw"))
            {
                drawing = isDrawing ? null : feature;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Edit Knots"))
            {
                drawing = null;
                Selection.activeGameObject = feature.gameObject;
                ToolManager.SetActiveContext<SplineToolContext>();
            }

            if (!(feature is BiomeArea) && GUILayout.Button(new GUIContent("Reverse", "Swap the start and end (a river runs from the first knot to the last; a cliff's high side swaps)")))
            {
                Undo.RecordObjects(new Object[] { feature.Container, feature }, "Reverse Spline");
                foreach (KnotValues values in feature.PerKnot)
                {
                    values.Reverse();
                }

                SplineUtility.ReverseFlow(feature.Spline);
            }

            if (feature is CliffFeature && GUILayout.Button(feature.Spline.Closed ? "Open Loop" : "Close Loop"))
            {
                Undo.RecordObject(feature.Container, "Close Spline");
                feature.Spline.Closed = !feature.Spline.Closed;
            }
        }

        if (GUILayout.Button("Build Now"))
        {
            WorldBuilder.BuildChanged(false);
        }

        if (feature.Spline != null && feature.Spline.Count < (feature is BiomeArea ? 3 : 2))
        {
            EditorGUILayout.HelpBox("Press Draw and click points on the ground (at least " + (feature is BiomeArea ? 3 : 2) +
                                    ") to give it a shape.", MessageType.Info);
        }

        EditorGUILayout.Space();
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (feature is CliffFeature cliff && cliff.Kit == null)
        {
            EditorGUILayout.HelpBox("No cliff kit: only the terrain is shaped. The Rock Faces kit is in Data/World.", MessageType.None);
        }
    }

    private static string HowTo(WorldFeature feature) => feature switch
    {
        RiverFeature _ => "Draw from the source to the mouth: the water runs from the first knot to the last and never uphill. " +
                          "Drag the white dots beside the knots to make it wider or narrower.",
        PathFeature _ => "Draw along the path. Drag the white dots beside the knots to vary its width.",
        CliffFeature _ => "Draw where the face should stand; the high side is on the left of the drawing direction (inside, " +
                          "for a closed loop). Drag the white dots above the knots to vary the height.",
        BiomeArea _ => "Draw a loop around the area. It fills with the layers below, regrowing when a river or path moves through it.",
        _ => "",
    };

    private void OnSceneGUI()
    {
        var feature = (WorldFeature)target;
        if (feature.Spline == null)
        {
            return;
        }

        if (drawing == feature)
        {
            DrawMode(feature);
        }

        DrawOutline(feature);
        if (drawing != feature)
        {
            KnotHandles(feature);
        }
    }

    // ------------------------------------------------------------------ drawing

    private static void DrawMode(WorldFeature feature)
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(id);
        Handles.BeginGUI();
        GUI.Label(new Rect(10, 10, 520, 22), "Click to add points · Backspace removes the last · Enter or Esc finishes", EditorStyles.helpBox);
        Handles.EndGUI();

        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            Finish(feature);
            e.Use();
            return;
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace && feature.Spline.Count > 0)
        {
            Undo.RecordObject(feature.Container, "Remove Point");
            feature.Spline.RemoveAt(feature.Spline.Count - 1);
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && MouseGround(e.mousePosition, out Vector3 point))
        {
            Undo.RecordObject(feature.Container, "Add Point");
            Vector3 local = feature.transform.InverseTransformPoint(point);
            feature.Spline.Add(new BezierKnot(new float3(local.x, local.y, local.z)), TangentMode.AutoSmooth);
            e.Use();
        }
    }

    private static void Finish(WorldFeature feature)
    {
        drawing = null;
        if (feature is BiomeArea && feature.Spline.Count >= 3 && !feature.Spline.Closed)
        {
            Undo.RecordObject(feature.Container, "Close Area");
            feature.Spline.Closed = true;
        }

        WorldBuilder.Schedule(0.05);
    }

    private static bool MouseGround(Vector2 mouse, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        float best = float.MaxValue;
        point = Vector3.zero;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null && collider.Raycast(ray, out RaycastHit hit, 5000f) && hit.distance < best)
            {
                best = hit.distance;
                point = hit.point;
            }
        }

        return best < float.MaxValue;
    }

    // ------------------------------------------------------------------ outline and handles

    private static void DrawOutline(WorldFeature feature)
    {
        if (feature.Spline.Count < 2)
        {
            return;
        }

        SplineLine line = SplineLine.Sample(feature.Container, 1.5f);
        Color colour = feature switch
        {
            RiverFeature _ => new Color(0.3f, 0.7f, 1f),
            PathFeature _ => new Color(0.95f, 0.8f, 0.45f),
            CliffFeature _ => new Color(1f, 0.55f, 0.35f),
            _ => new Color(0.5f, 1f, 0.5f),
        };
        Handles.color = colour;
        Handles.DrawAAPolyLine(3f, line.Points.Select(p => p + Vector3.up * 0.3f).ToArray());
        float half = feature switch
        {
            RiverFeature river => river.Width * 0.5f,
            PathFeature path => path.Width * 0.5f,
            _ => 0f,
        };
        if (half <= 0f)
        {
            return;
        }

        KnotValues widths = feature is RiverFeature r ? r.WidthAtKnots : ((PathFeature)feature).WidthAtKnots;
        var left = new Vector3[line.Count];
        var right = new Vector3[line.Count];
        for (int i = 0; i < line.Count; i++)
        {
            Vector2 d = line.Directions[i];
            Vector3 side = new Vector3(-d.y, 0f, d.x) * (half * widths.Evaluate(line.KnotT[i], line.KnotCount, line.Closed));
            left[i] = line.Points[i] + side + Vector3.up * 0.3f;
            right[i] = line.Points[i] - side + Vector3.up * 0.3f;
        }

        Handles.color = new Color(colour.r, colour.g, colour.b, 0.6f);
        Handles.DrawAAPolyLine(2f, left);
        Handles.DrawAAPolyLine(2f, right);
    }

    private static void KnotHandles(WorldFeature feature)
    {
        Spline spline = feature.Spline;
        Transform t = feature.transform;
        foreach (KnotValues values in feature.PerKnot)
        {
            values.Resize(spline.Count);
        }

        for (int i = 0; i < spline.Count; i++)
        {
            Vector3 position = t.TransformPoint((Vector3)spline[i].Position);
            float normalised = spline.Count > 1 ? spline.ConvertIndexUnit(i, PathIndexUnit.Knot, PathIndexUnit.Normalized) : 0f;
            Vector3 tangent = t.TransformDirection((Vector3)spline.EvaluateTangent(normalised));
            Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
            Vector3 left = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) * -1f : Vector3.left;
            float size = HandleUtility.GetHandleSize(position) * 0.09f;
            Handles.color = Color.white;
            switch (feature)
            {
                case RiverFeature river:
                    Slide(feature, river.WidthAtKnots, i, position, left, river.Width * 0.5f, size, "Change River Width");
                    break;
                case PathFeature path:
                    Slide(feature, path.WidthAtKnots, i, position, left, path.Width * 0.5f, size, "Change Path Width");
                    break;
                case CliffFeature cliff:
                    Slide(feature, cliff.HeightAtKnots, i, position, Vector3.up, cliff.Height, size, "Change Cliff Height");
                    break;
            }
        }
    }

    private static void Slide(WorldFeature feature, KnotValues values, int knot, Vector3 origin, Vector3 axis, float unit, float size, string undo)
    {
        Vector3 handle = origin + axis * (unit * values[knot]);
        Handles.DrawDottedLine(origin, handle, 3f);
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(handle, axis, size, Handles.DotHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(feature, undo);
            values[knot] = Mathf.Max(0.1f, Vector3.Dot(moved - origin, axis) / Mathf.Max(unit, 0.01f));
            EditorUtility.SetDirty(feature);
            WorldBuilder.Schedule(0.35);
        }
    }
}

/// <summary>The World Terrain Inspector: build now, rebuild everything, auto-paint from rules, the world terrain
/// material, NavMesh, and what the last build did.</summary>
[CustomEditor(typeof(WorldTerrain))]
public sealed class WorldTerrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var world = (WorldTerrain)target;
        var terrain = world.GetComponent<Terrain>();
        DrawDefaultInspector();
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Build Now", "Build whatever changed")))
            {
                WorldBuilder.BuildChanged(false);
            }

            if (GUILayout.Button(new GUIContent("Rebuild Everything", "Every terrain, feature and area from scratch")))
            {
                WorldBuilder.BuildChanged(true);
            }
        }

        using (new EditorGUI.DisabledScope(world.PaintRules == null))
        {
            if (GUILayout.Button(new GUIContent("Auto-Paint with Rules", "Paint the whole terrain by its shape, then let the features paint over it")))
            {
                WorldPaint.Apply(terrain, world.PaintRules);
                WorldBuilder.RebuildTerrain(terrain);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Dark Forest Look", "World terrain material (rock on steep ground) and the dark forest paint rules")))
            {
                UseLook(world, terrain, false);
            }

            if (GUILayout.Button(new GUIContent("Haven Look", "World terrain material, lighter, and the green meadow paint rules")))
            {
                UseLook(world, terrain, true);
            }
        }

        if (GUILayout.Button("Rebake NavMesh"))
        {
            Debug.Log("World Builder: " + WorldBuilder.RebakeNavMesh());
        }

        if (world.Stash != null)
        {
            EditorGUILayout.HelpBox($"Ground kept under features: {world.Stash.HeightCount} height, {world.Stash.AlphaCount} paint and " +
                                    $"{world.Stash.DetailCount} grass cells, {world.Stash.Trees.Count} trees.\n" +
                                    $"Features built here: {world.BuiltFeatures.Count}.\nLast build: {WorldBuilder.LastReport}", MessageType.None);
        }
    }

    /// <summary>The world terrain material (rock on steep ground) and the matching paint rules: the dark raid forest's or
    /// the light, green Haven's.</summary>
    public static void UseLook(WorldTerrain world, Terrain terrain, bool haven)
    {
        WorldAssets.EnsureAll();
        Undo.RecordObjects(new Object[] { world, terrain }, "World Terrain Look");
        terrain.materialTemplate = WorldAssets.TerrainMaterial(haven);
        terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 1000f);
        world.PaintRules = WorldAssets.PaintRules(haven);
        EditorUtility.SetDirty(terrain);
    }
}

/// <summary>Cliff Kit Inspector: Measure Pieces works out each piece's facing and size from its mesh.</summary>
[CustomEditor(typeof(CliffKit))]
public sealed class CliffKitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button(new GUIContent("Measure Pieces", "Find each piece's finished side and its size from the mesh")))
        {
            var kit = (CliffKit)target;
            Undo.RecordObject(kit, "Measure Pieces");
            foreach (CliffKit.Piece piece in kit.Pieces)
            {
                WorldAssets.Measure(piece);
            }

            EditorUtility.SetDirty(kit);
        }
    }
}
