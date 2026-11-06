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
    private static WorldFeature drawingFeature;

    // Drawing runs from the Scene view itself (as the Biome Brush does), not from the Inspector's OnSceneGUI: that one
    // did not get the click before Unity's own picking, so clicking the ground selected the terrain instead.
    private static WorldFeature drawing
    {
        get => drawingFeature;
        set
        {
            drawingFeature = value;
            SceneView.duringSceneGui -= DuringSceneGui;
            if (value != null)
            {
                SceneView.duringSceneGui += DuringSceneGui;
            }

            SceneView.RepaintAll();
        }
    }

    /// <summary>A feature is being drawn: the World Builder waits until it is finished (Enter), so nothing is built
    /// point by point.</summary>
    public static bool IsDrawing => drawingFeature != null;

    private static void DuringSceneGui(SceneView view)
    {
        WorldFeature feature = drawingFeature;
        if (feature == null || feature.Spline == null)
        {
            drawing = null;
            return;
        }

        // Selecting something else finishes the drawing (an area closes its loop) instead of leaving it open.
        if (Selection.activeGameObject != feature.gameObject)
        {
            Finish(feature);
            return;
        }

        DrawMode(feature);
        if (Event.current.type == EventType.Repaint)
        {
            DrawOutline(feature);
        }

        if (feature.IsArea && feature.Spline.Count >= 3)
        {
            Vector3 first = feature.transform.TransformPoint((Vector3)feature.Spline[0].Position);
            Handles.color = new Color(0.5f, 1f, 0.5f, 0.9f);
            Handles.DrawSolidDisc(first, Vector3.up, HandleUtility.GetHandleSize(first) * 0.08f); // click it to close
        }
    }

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

            if (!feature.IsArea && GUILayout.Button(new GUIContent("Reverse", "Swap the start and end (a river runs from the first knot to the last; a cliff's high side swaps)")))
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

        if (feature.Spline != null && feature.Spline.Count < (feature.IsArea ? 3 : 2))
        {
            EditorGUILayout.HelpBox("Press Draw and click points on the ground (at least " + (feature.IsArea ? 3 : 2) +
                                    ") to give it a shape.", MessageType.Info);
        }

        EditorGUILayout.Space();
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (feature is CliffFeature cliff && cliff.Kit == null)
        {
            EditorGUILayout.HelpBox("No rock pieces: the face is the shaped terrain in its Rock Layer (Cliff Rock, tessellated), rubble at its foot. Set a Kit (the looks' Crags, Rock Faces in Data/World) to stand pieces along it.", MessageType.None);
        }
    }

    private static string HowTo(WorldFeature feature) => feature switch
    {
        RiverFeature _ => "Draw from the source to the mouth: the water runs from the first knot to the last and never uphill. " +
                          "Drag the white dots beside the knots to make it wider or narrower.",
        PathFeature _ => "Draw along the path. Drag the white dots beside the knots to vary its width.",
        CliffFeature _ => "Draw where the face should stand; the high side is on the left of the drawing direction (inside, " +
                          "for a closed loop). Drag the white dots above the knots to vary the height.",
        BiomeArea area when area.Biome != null => $"Draw a loop around the area. It is painted with {area.Biome.name} (ground, plants and props) " +
                          "and fills with the layers below, regrowing when a river or path moves through it.",
        BiomeArea _ => "Draw a loop around the area. It fills with the layers below, regrowing when a river or path moves through it. " +
                       "Give it a Biome to paint its ground and grow its plants too.",
        RavineFeature _ => "Draw along the floor of the ravine: it is cut the depth below the ground, with walls on both sides.",
        GroundPaintArea _ => "Draw a loop around the ground to paint: its rules paint it by steepness, height and hollows.",
        _ => "",
    };

    private void OnSceneGUI()
    {
        var feature = (WorldFeature)target;
        if (feature.Spline == null)
        {
            return;
        }

        if (drawing != feature)
        {
            DrawOutline(feature); // while drawing, the Scene view hook draws it
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
        GUI.Label(new Rect(10, 10, 640, 22), feature.IsArea
            ? "Click to add points · click the first point (or Enter) to close the loop and build · Backspace removes the last"
            : "Click to add points · Backspace removes the last · Enter builds it (settings in the Inspector show as you change them)",
            EditorStyles.helpBox);
        Handles.EndGUI();

        // Where the next point would go: a line from the last point to the ground under the mouse.
        Spline spline = feature.Spline;
        if (spline.Count > 0 && e.type == EventType.Repaint && MouseGround(e.mousePosition, feature.gameObject.scene, out Vector3 hover))
        {
            Vector3 last = feature.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            Handles.color = new Color(1f, 1f, 1f, 0.8f);
            Handles.DrawDottedLine(last + Vector3.up * 0.3f, hover + Vector3.up * 0.3f, 4f);
            if (feature.IsArea && spline.Count >= 2)
            {
                Vector3 first = feature.transform.TransformPoint((Vector3)spline[0].Position);
                Handles.DrawDottedLine(hover + Vector3.up * 0.3f, first + Vector3.up * 0.3f, 2f);
            }
        }

        if (e.type == EventType.MouseMove)
        {
            SceneView.RepaintAll();
        }

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

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && MouseGround(e.mousePosition, feature.gameObject.scene, out Vector3 point))
        {
            // Clicking the first point of an area closes its loop.
            if (feature.IsArea && feature.Spline.Count >= 3)
            {
                Vector3 first = feature.transform.TransformPoint((Vector3)feature.Spline[0].Position);
                if (HandleUtility.DistanceToCircle(first, HandleUtility.GetHandleSize(first) * 0.15f) <= 0f)
                {
                    Finish(feature);
                    e.Use();
                    return;
                }
            }

            Undo.RecordObject(feature.Container, "Add Point");
            Vector3 local = feature.transform.InverseTransformPoint(point);
            feature.Spline.Add(new BezierKnot(new float3(local.x, local.y, local.z)), TangentMode.AutoSmooth);
            e.Use();
        }
    }

    private static void Finish(WorldFeature feature)
    {
        drawing = null;
        if (feature.IsArea && feature.Spline.Count >= 3 && !feature.Spline.Closed)
        {
            Undo.RecordObject(feature.Container, "Close Area");
            feature.Spline.Closed = true;
        }

        WorldBuilder.Schedule(0.05);
    }

    // ------------------------------------------------------------------ shape previews

    // The side a sample's "left" points to, flat (left of the direction of travel).
    private static Vector3 LeftOf(SplineLine line, int i)
    {
        Vector2 d = line.Directions[i];
        return new Vector3(-d.y, 0f, d.x);
    }

    /// <summary>The cliff before (and after) it is built: its face as a see-through wall at its height along the line,
    /// and the edge of its plateau on the high side (raised) or the foot of the cut on the low side.</summary>
    private static void PreviewCliff(CliffFeature cliff, SplineLine line, Color colour)
    {
        int n = line.Count;
        var foot = new Vector3[n];
        var top = new Vector3[n];
        var far = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 high = LeftOf(line, i) * (cliff.High == CliffFeature.HighSide.Left ? 1f : -1f);
            float h = cliff.Height * cliff.HeightAtKnots.Evaluate(line.KnotT[i], line.KnotCount, line.Closed);
            Vector3 p = line.Points[i];
            if (cliff.Shape == CliffFeature.Shaping.Raise)
            {
                foot[i] = p - high * cliff.FaceDepth * 0.5f;
                top[i] = p + high * cliff.FaceDepth * 0.5f + Vector3.up * h;
                far[i] = top[i] + high * cliff.Plateau;
            }
            else
            {
                top[i] = p + high * cliff.FaceDepth * 0.5f;
                foot[i] = p - high * cliff.FaceDepth * 0.5f - Vector3.up * h;
                far[i] = foot[i] - high * 4f;
            }
        }

        Wall(foot, top, colour);
        Handles.color = new Color(colour.r, colour.g, colour.b, 0.55f);
        Handles.DrawDottedLines(Pairs(top, far, 4), 3f);
        Handles.DrawAAPolyLine(2f, far);
        Label(top, $"{cliff.Height:0.#} m {(cliff.Shape == CliffFeature.Shaping.Raise ? "high" : "deep")}");
    }

    /// <summary>The ravine before (and after) it is built: both walls as see-through faces from the ground down to its
    /// floor, and the floor's edges, at the depth and widths it is set to.</summary>
    private static void PreviewRavine(RavineFeature ravine, SplineLine line, Color colour)
    {
        int n = line.Count;
        var floorL = new Vector3[n];
        var floorR = new Vector3[n];
        var rimL = new Vector3[n];
        var rimR = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 left = LeftOf(line, i);
            float half = 0.5f * ravine.FloorWidth * ravine.WidthAtKnots.Evaluate(line.KnotT[i], line.KnotCount, line.Closed);
            float depth = ravine.Depth * ravine.DepthAtKnots.Evaluate(line.KnotT[i], line.KnotCount, line.Closed);
            Vector3 p = line.Points[i];
            floorL[i] = p + left * half - Vector3.up * depth;
            floorR[i] = p - left * half - Vector3.up * depth;
            rimL[i] = p + left * (half + ravine.WallRun);
            rimR[i] = p - left * (half + ravine.WallRun);
        }

        Wall(floorL, rimL, colour);
        Wall(floorR, rimR, colour);
        Handles.color = new Color(colour.r, colour.g, colour.b, 0.8f);
        Handles.DrawAAPolyLine(2f, floorL);
        Handles.DrawAAPolyLine(2f, floorR);
        Label(floorL, $"{ravine.Depth:0.#} m deep, floor {ravine.FloorWidth:0.#} m");
    }

    // A see-through face between two lines, with its edges and a few uprights.
    private static void Wall(Vector3[] lower, Vector3[] upper, Color colour)
    {
        Handles.color = new Color(colour.r, colour.g, colour.b, 0.18f);
        for (int i = 0; i + 1 < lower.Length; i++)
        {
            Handles.DrawAAConvexPolygon(lower[i], lower[i + 1], upper[i + 1], upper[i]);
        }

        Handles.color = new Color(colour.r, colour.g, colour.b, 0.9f);
        Handles.DrawAAPolyLine(2.5f, upper);
        Handles.DrawAAPolyLine(1.5f, lower);
        Handles.color = new Color(colour.r, colour.g, colour.b, 0.5f);
        Handles.DrawLines(Pairs(lower, upper, 4));
    }

    // Line pairs from a to b at every step-th sample (and the last), for Handles.DrawLines.
    private static Vector3[] Pairs(Vector3[] a, Vector3[] b, int step)
    {
        var pairs = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < a.Length; i += step)
        {
            pairs.Add(a[i]);
            pairs.Add(b[i]);
        }

        pairs.Add(a[a.Length - 1]);
        pairs.Add(b[b.Length - 1]);
        return pairs.ToArray();
    }

    private static void Label(Vector3[] along, string text)
    {
        Handles.Label(along[along.Length / 2] + Vector3.up * 0.5f, text, EditorStyles.whiteMiniLabel);
    }

    /// <summary>The colour a feature is outlined in (and marked with in World Tools' list).</summary>
    public static Color ColourOf(WorldFeature feature) => feature switch
    {
        RiverFeature _ => new Color(0.3f, 0.7f, 1f),
        PathFeature _ => new Color(0.95f, 0.8f, 0.45f),
        CliffFeature _ => new Color(1f, 0.55f, 0.35f),
        RavineFeature _ => new Color(0.85f, 0.45f, 0.3f),
        GroundPaintArea _ => new Color(0.8f, 0.7f, 1f),
        _ => new Color(0.5f, 1f, 0.5f),
    };

    /// <summary>The ground under the mouse on the feature's own scene's terrains (another open scene can lie on the same
    /// coordinates: the Haven and the prologue do).</summary>
    private static bool MouseGround(Vector2 mouse, UnityEngine.SceneManagement.Scene scene, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        float best = float.MaxValue;
        point = Vector3.zero;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene != scene)
            {
                continue;
            }

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
        Color colour = ColourOf(feature);
        Handles.color = colour;
        Handles.DrawAAPolyLine(3f, line.Points.Select(p => p + Vector3.up * 0.3f).ToArray());
        switch (feature)
        {
            case CliffFeature cliff:
                PreviewCliff(cliff, line, colour);
                return;
            case RavineFeature ravine:
                PreviewRavine(ravine, line, colour);
                return;
        }
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
            if (GUILayout.Button(new GUIContent("Dark Forest Look", "The Mossy Forest: tessellated terrain material with its rock, its grounds and paint rules")))
            {
                UseLook(world, terrain, false);
            }

            if (GUILayout.Button(new GUIContent("Haven Look", "The Forest Lake: tessellated terrain material with its rock, its grounds and meadow paint rules")))
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

    /// <summary>The look's terrain material (tessellated, with its rock on steep ground), its ground layers and its paint
    /// rules: the Mossy Forest of the raids, or the Haven's Forest Lake.</summary>
    public static void UseLook(WorldTerrain world, Terrain terrain, bool haven) =>
        UseLook(world, terrain, haven ? WorldLook.ForestLake : WorldLook.MossyForest);

    public static void UseLook(WorldTerrain world, Terrain terrain, WorldLook look)
    {
        WorldAssets.EnsureAll();
        TerrainData data = terrain.terrainData;
        Undo.RecordObjects(new Object[] { world, terrain, data }, "World Terrain Look");
        TerrainPaintRules rules = look.Rules(look.PaintLooks[0]);
        world.PaintRules = rules;
        if (rules != null)
        {
            // The rules' grounds join the terrain's layers, so the rules (and the brush) can paint them.
            var layers = data.terrainLayers.ToList();
            foreach (TerrainPaintRules.Rule rule in rules.Rules)
            {
                if (rule.layer != null && !layers.Contains(rule.layer) && layers.Count < 16)
                {
                    layers.Add(rule.layer);
                }
            }

            data.terrainLayers = layers.ToArray();
        }

        terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 1000f);
        Material template = look.TerrainTemplate;
        if (template != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data)))
        {
            // The terrain gets its own tessellated material from the look (TerrainDisplacementEditor copies it in).
            terrain.materialTemplate = template;
            TerrainDisplacementEditor.Apply(terrain);
            Debug.Log($"{terrain.name}: {look.Name} look; rock: {TerrainRock.PaintSteepGround(terrain, look)}.", terrain);
        }
        else
        {
            terrain.materialTemplate = WorldAssets.TerrainMaterial(look == WorldLook.ForestLake);
            Debug.LogWarning("World Terrain: save the terrain's TerrainData as an asset to give it tessellation.", terrain);
        }

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
