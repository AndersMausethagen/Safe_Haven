using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The RaidMap inspector's Map Painter: edits a RaidMapDefinition's layout straight on the scene's terrain.
/// <list type="bullet">
/// <item>Zones: a round brush paints the selected zone (Shift erases, [ and ] resize); every zone shows as a see-through
/// colour overlay hugging the terrain, and spawn zones preview where their candidate points would go.</item>
/// <item>Markers: coloured handles to drag; click empty ground to add the chosen kind, Delete removes the selected one.</item>
/// </list>
/// Heights are sculpted with Unity's own terrain tools (the Sculpt button selects the terrain). Owned by RaidMapEditor;
/// brush settings last for the editor session.
/// </summary>
public sealed class RaidMapPainter
{
    public enum Mode
    {
        Off = 0,
        Zones = 1,
        Markers = 2,
    }

    private const string OverlayShader = "Hidden/Safe Haven/Raid Map Overlay";
    private const int OverlaySegments = 128;
    private const float OverlayLift = 0.15f;
    private const float LabelDistance = 18f;
    private static readonly int ControlHash = "RaidMapPainter".GetHashCode();

    // Session-wide, so they survive reselecting the map.
    private static Mode mode = Mode.Off;
    private static int selectedZone;
    private static float brushRadius = 6f;
    private static float brushStrength = 0.5f;
    private static float brushHardness = 0.4f;
    private static bool showAllZones = true;
    private static float opacity = 0.6f;
    private static bool previewPoints = true;
    private static RaidSiteKind newMarkerKind = RaidSiteKind.EnemyCamp;
    private static int selectedMarker = -1;

    private readonly RaidMap map;
    private Texture2D overlay;
    private Mesh overlayMesh;
    private Material overlayMaterial;
    private bool overlayDirty = true;
    private bool meshDirty = true;
    private List<Vector2> preview;
    private bool previewDirty = true;
    private bool painting;
    private Vector2 lastDab;
    private bool toolsHidden;

    public RaidMapPainter(RaidMap map)
    {
        this.map = map;
        Undo.undoRedoPerformed += OnUndoRedo;
        TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
        ShowTools(mode == Mode.Off);
    }

    public static Mode CurrentMode => mode;
    private RaidMapDefinition Definition => map != null ? map.Definition : null;
    private bool Ready => Definition != null && Definition.HasPaintedLayout && map.Terrain != null && map.Terrain.terrainData != null;

    public void Dispose()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        TerrainCallbacks.heightmapChanged -= OnHeightmapChanged;
        ShowTools(true);
        if (overlay != null) Object.DestroyImmediate(overlay);
        if (overlayMesh != null) Object.DestroyImmediate(overlayMesh);
        if (overlayMaterial != null) Object.DestroyImmediate(overlayMaterial);
    }

    private void OnUndoRedo()
    {
        overlayDirty = true;
        previewDirty = true;
        SceneView.RepaintAll();
    }

    private void OnHeightmapChanged(Terrain changed, RectInt region, bool synched)
    {
        if (map != null && changed == map.Terrain)
        {
            meshDirty = true;
            previewDirty = true;
        }
    }

    // ---------------------------------------------------------------- Inspector

    public void DrawInspector()
    {
        RaidMapDefinition definition = Definition;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Painter", EditorStyles.boldLabel);
        if (definition == null)
        {
            EditorGUILayout.HelpBox("Assign a Raid Map definition to paint.", MessageType.Info);
            return;
        }

        if (!definition.HasPaintedLayout)
        {
            if (definition.HasLegacyImages)
            {
                EditorGUILayout.HelpBox($"{definition.name} is still made of images. Import them once: heights go onto the " +
                                        "terrain, the mask into Path/Forest/Clearing zones and the dots into markers.", MessageType.Warning);
                if (GUILayout.Button("Import from Images", GUILayout.Height(24f)))
                {
                    Debug.Log(RaidMapImages.ImportLegacy(map), map);
                    MarkAllDirty();
                }
            }
            else if (GUILayout.Button("Start Painting (adds Path, Forest and Clearing zones)", GUILayout.Height(24f)))
            {
                Undo.RecordObject(definition, "Start raid map");
                definition.EnsureZones();
                EditorUtility.SetDirty(definition);
                MarkAllDirty();
            }

            return;
        }

        if (map.Terrain == null)
        {
            EditorGUILayout.HelpBox("Bake once to create the terrain, then paint on it.", MessageType.Info);
            return;
        }

        Mode chosen = (Mode)GUILayout.Toolbar((int)mode, new[] { "Off", "Paint Zones", "Markers" }, GUILayout.Height(24f));
        if (chosen != mode)
        {
            mode = chosen;
            painting = false;
            overlayDirty = true;
            ShowTools(mode == Mode.Off);
            SceneView.RepaintAll();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Top-down View")) LookAtMap(true);
            if (GUILayout.Button("Perspective")) LookAtMap(false);
            if (GUILayout.Button(new GUIContent("Sculpt Heights", "Selects the terrain: use Unity's Raise/Lower, Smooth and " +
                                                                  "Set Height tools, then come back here and Bake.")))
            {
                mode = Mode.Off;
                ShowTools(true);
                Selection.activeGameObject = map.Terrain.gameObject;
            }
        }

        if (mode == Mode.Zones) DrawZonesInspector(definition);
        else if (mode == Mode.Markers) DrawMarkersInspector(definition);
        else EditorGUILayout.HelpBox("Paint Zones: paths, forest, clearings and spawn zones. Markers: start, exits, camps, " +
                                     "sites. Sculpt Heights: Unity's terrain tools. Bake when done.", MessageType.None);

        DrawFiles(definition);
    }

    private void DrawZonesInspector(RaidMapDefinition definition)
    {
        List<RaidMapZone> zones = definition.Zones;
        selectedZone = Mathf.Clamp(selectedZone, 0, zones.Count - 1);

        EditorGUILayout.Space(4f);
        for (int i = 0; i < zones.Count; i++)
        {
            RaidMapZone zone = zones[i];
            using (new EditorGUILayout.HorizontalScope(i == selectedZone ? EditorStyles.helpBox : GUIStyle.none))
            {
                Rect swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + 2f, 14f, 14f), zone.color);
                string usage = zone.IsSpawnZone ? UsedBy(zone.name) : "";
                if (GUILayout.Button($"{zone.name}  ({zone.role}){usage}", i == selectedZone ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    SelectZone(i);
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("SaveAs"), EditorStyles.iconButton, GUILayout.Width(18f)))
                {
                    ExportZone(zone);
                }

                using (new EditorGUI.DisabledScope(!CanRemove(zones, zone)))
                {
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20f))
                        && EditorUtility.DisplayDialog("Remove zone", $"Remove the zone '{zone.name}' and its paint?", "Remove", "Cancel"))
                    {
                        Undo.RecordObject(definition, "Remove raid zone");
                        zones.RemoveAt(i);
                        EditorUtility.SetDirty(definition);
                        SelectZone(Mathf.Min(selectedZone, zones.Count - 1));
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        if (GUILayout.Button("Add Spawn Zone"))
        {
            Undo.RecordObject(definition, "Add raid zone");
            var zone = new RaidMapZone
            {
                name = UniqueName(zones, "Spawn Zone"),
                color = Color.HSVToRGB((zones.Count * 0.13f + 0.05f) % 1f, 0.85f, 1f),
            };
            zone.EnsureResolution(definition.ZoneResolution);
            zones.Add(zone);
            EditorUtility.SetDirty(definition);
            SelectZone(zones.Count - 1);
        }

        if (zones.Count == 0) return;

        // The selected zone's settings, through the serialized definition so Undo and prefab-style dirtying work.
        var serialized = new SerializedObject(definition);
        SerializedProperty property = serialized.FindProperty("zones").GetArrayElementAtIndex(selectedZone);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, new GUIContent($"Zone: {zones[selectedZone].name}"), true);
        if (EditorGUI.EndChangeCheck())
        {
            serialized.ApplyModifiedProperties();
            MarkAllDirty();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        brushRadius = EditorGUILayout.Slider(new GUIContent("Radius (m)", "[ and ] in the Scene view"), brushRadius, 0.5f, 60f);
        brushStrength = EditorGUILayout.Slider("Strength", brushStrength, 0.02f, 1f);
        brushHardness = EditorGUILayout.Slider(new GUIContent("Hardness", "1 = hard edge, 0 = soft all the way"), brushHardness, 0f, 1f);
        EditorGUI.BeginChangeCheck();
        opacity = EditorGUILayout.Slider("Overlay Opacity", opacity, 0.1f, 1f);
        showAllZones = EditorGUILayout.Toggle(new GUIContent("Show All Zones", "Off shows only the selected zone"), showAllZones);
        previewPoints = EditorGUILayout.Toggle(new GUIContent("Preview Points", "Spawn zones: where Bake would put candidate points"), previewPoints);
        if (EditorGUI.EndChangeCheck())
        {
            overlayDirty = true;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox("Click and drag on the terrain to paint, Shift to erase, [ and ] to resize, Alt to orbit.\n" +
                                "Spawn zones give candidate points to the Spawn Groups whose Zone has the zone's name; the raid " +
                                "fills only some of them (the group's count or the raid's spawn list, scaled by density).",
                                MessageType.None);
        if (zones[selectedZone].IsSpawnZone && previewPoints && preview != null)
        {
            EditorGUILayout.LabelField($"Preview: {preview.Count} candidate points (max {zones[selectedZone].maxPoints})", EditorStyles.miniLabel);
        }
    }

    private void DrawMarkersInspector(RaidMapDefinition definition)
    {
        newMarkerKind = (RaidSiteKind)EditorGUILayout.EnumPopup(new GUIContent("Add Kind", "Click empty ground to add one"), newMarkerKind);
        if (newMarkerKind == RaidSiteKind.None) newMarkerKind = RaidSiteKind.EnemyCamp;
        EditorGUILayout.HelpBox("Click empty ground to add a marker, drag a marker to move it, Delete removes the selected " +
                                "one. Bake moves the player start, exits and site groups to them.", MessageType.None);

        List<RaidMapMarker> markers = definition.Markers;
        for (int i = 0; i < markers.Count; i++)
        {
            RaidMapMarker marker = markers[i];
            using (new EditorGUILayout.HorizontalScope(i == selectedMarker ? EditorStyles.helpBox : GUIStyle.none))
            {
                Rect swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + 2f, 14f, 14f), RaidMapMarkers.ColorOf(marker.kind));
                Vector2 metres = marker.position * definition.Size;
                string text = $"{ObjectNames.NicifyVariableName(marker.kind.ToString())}  ({metres.x:0}, {metres.y:0})" +
                              (string.IsNullOrEmpty(marker.label) ? "" : $"  {marker.label}");
                if (GUILayout.Button(text, i == selectedMarker ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    selectedMarker = i;
                    SceneView.lastActiveSceneView?.LookAt(Ground(marker.position));
                }

                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20f)))
                {
                    RemoveMarker(i);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (selectedMarker >= 0 && selectedMarker < markers.Count)
        {
            var serialized = new SerializedObject(definition);
            SerializedProperty property = serialized.FindProperty("markers").GetArrayElementAtIndex(selectedMarker);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent("Selected Marker"), true);
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
        }
    }

    private void DrawFiles(RaidMapDefinition definition)
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Export Heights PNG", "16-bit greyscale, black = lowest")))
            {
                string file = EditorUtility.SaveFilePanel("Export heights", "", $"{definition.name} Heights", "png");
                if (!string.IsNullOrEmpty(file)) RaidMapImages.ExportHeights(map.Terrain.terrainData, file);
            }

            if (GUILayout.Button("Import Heights PNG"))
            {
                string file = EditorUtility.OpenFilePanel("Import heights", "", "png");
                if (!string.IsNullOrEmpty(file))
                {
                    Undo.RegisterCompleteObjectUndo(map.Terrain.terrainData, "Import heights");
                    if (RaidMapImages.ImportHeights(map.Terrain.terrainData, file)) EditorUtility.SetDirty(map.Terrain.terrainData);
                    else Debug.LogWarning($"Could not read {file}.");
                }
            }

            if (mode == Mode.Zones && definition.Zones.Count > 0 && GUILayout.Button("Import Zone PNG"))
            {
                string file = EditorUtility.OpenFilePanel($"Import {definition.Zones[selectedZone].name}", "", "png");
                if (!string.IsNullOrEmpty(file))
                {
                    Undo.RegisterCompleteObjectUndo(definition, "Import zone");
                    if (RaidMapImages.ImportZone(definition.Zones[selectedZone], file)) EditorUtility.SetDirty(definition);
                    else Debug.LogWarning($"Could not read {file}.");
                    MarkAllDirty();
                }
            }
        }
    }

    // ---------------------------------------------------------------- Scene view

    public void OnSceneGUI()
    {
        if (mode == Mode.Off || !Ready)
        {
            return;
        }

        RaidMapDefinition definition = Definition;
        Event e = Event.current;
        if (mode == Mode.Zones)
        {
            definition.EnsureZones();
            if (e.type == EventType.Repaint) DrawOverlay(definition);
            DrawMarkers(definition);
            HandleBrush(definition, e);
        }
        else
        {
            if (e.type == EventType.Repaint && showAllZones) DrawOverlay(definition);
            HandleMarkers(definition, e);
        }
    }

    private void HandleBrush(RaidMapDefinition definition, Event e)
    {
        int id = GUIUtility.GetControlID(ControlHash, FocusType.Passive);
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(id);
        }

        bool onTerrain = TryHit(e.mousePosition, out Vector3 point, out Vector2 uv);
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
        {
            brushRadius = Mathf.Clamp(brushRadius * (e.keyCode == KeyCode.LeftBracket ? 0.85f : 1.18f), 0.5f, 60f);
            e.Use();
            RepaintInspector();
        }

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown when e.button == 0 && !e.alt && HandleUtility.nearestControl == id && onTerrain:
                GUIUtility.hotControl = id;
                painting = true;
                Undo.RegisterCompleteObjectUndo(definition, "Paint raid zone");
                Dab(definition, uv, e.shift);
                lastDab = uv;
                e.Use();
                break;
            case EventType.MouseDrag when GUIUtility.hotControl == id && painting:
                if (onTerrain && Vector2.Distance(uv, lastDab) * definition.Size >= brushRadius * 0.15f)
                {
                    Dab(definition, uv, e.shift);
                    lastDab = uv;
                }

                e.Use();
                break;
            case EventType.MouseUp when GUIUtility.hotControl == id:
                GUIUtility.hotControl = 0;
                painting = false;
                EditorUtility.SetDirty(definition);
                previewDirty = true;
                e.Use();
                break;
            case EventType.MouseMove:
                HandleUtility.Repaint();
                break;
            case EventType.Repaint when onTerrain:
                DrawBrush(point, e.shift);
                break;
        }

        if (e.type == EventType.Repaint) DrawPreview(definition);
    }

    private void Dab(RaidMapDefinition definition, Vector2 uv, bool erase)
    {
        RaidMapZone zone = definition.Zones[selectedZone];
        RectInt touched = RaidZoneGrid.Paint(zone.Weights, zone.Resolution, uv, brushRadius / definition.Size, brushStrength,
            erase ? 0f : 1f, brushHardness);
        if (touched.width > 0)
        {
            UpdateOverlay(definition, touched);
        }
    }

    private void DrawBrush(Vector3 centre, bool erase)
    {
        Color colour = erase ? new Color(1f, 0.35f, 0.3f, 1f) : Color.white;
        float size = Definition.Size;
        DrawGroundCircle(centre, brushRadius, colour, size);
        if (brushHardness < 0.99f) DrawGroundCircle(centre, brushRadius * brushHardness, new Color(colour.r, colour.g, colour.b, 0.4f), size);
    }

    private void DrawGroundCircle(Vector3 centre, float radius, Color colour, float size)
    {
        const int segments = 48;
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 world = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            points[i] = GroundWorld(world) + Vector3.up * (OverlayLift + 0.05f);
        }

        Handles.color = colour;
        Handles.DrawAAPolyLine(3f, points);
    }

    private void DrawPreview(RaidMapDefinition definition)
    {
        if (!previewPoints || selectedZone >= definition.Zones.Count) return;
        RaidMapZone zone = definition.Zones[selectedZone];
        if (!zone.IsSpawnZone) return;

        if (previewDirty || preview == null)
        {
            TerrainData data = map.Terrain.terrainData;
            preview = RaidZoneGrid.Scatter(spot => zone.Sample(spot.x, spot.y), definition.Size, zone.pointSpacing, zone.maxPoints,
                RaidZoneGrid.SeedFor(definition.ScatterSeed, zone.name), spot => data.GetSteepness(spot.x, spot.y) <= zone.maxSlope);
            previewDirty = false;
            RepaintInspector();
        }

        Color colour = Color.Lerp(zone.color, Color.white, 0.35f);
        foreach (Vector2 spot in preview)
        {
            Vector3 world = Ground(spot) + Vector3.up * (OverlayLift + 0.1f);
            Handles.color = colour;
            Handles.DrawSolidDisc(world, Vector3.up, Mathf.Max(0.4f, zone.clearTrees > 0f ? 0.6f : 0.45f));
            if (zone.clearTrees > 0f)
            {
                Handles.color = new Color(colour.r, colour.g, colour.b, 0.5f);
                Handles.DrawWireDisc(world, Vector3.up, zone.clearTrees);
            }
        }
    }

    private void HandleMarkers(RaidMapDefinition definition, Event e)
    {
        int defaultId = GUIUtility.GetControlID(ControlHash, FocusType.Passive);
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(defaultId);
        }

        List<RaidMapMarker> markers = definition.Markers;
        for (int i = 0; i < markers.Count; i++)
        {
            RaidMapMarker marker = markers[i];
            Vector3 position = Ground(marker.position);
            float handleSize = MarkerSize(position);
            int id = GUIUtility.GetControlID(ControlHash + 1 + i, FocusType.Passive);
            Handles.color = RaidMapMarkers.ColorOf(marker.kind);
            DrawPole(position, handleSize, i == selectedMarker);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(id, position + Vector3.up * handleSize * 3f, handleSize, Vector3.zero, Handles.SphereHandleCap);
            if (GUIUtility.hotControl == id && selectedMarker != i)
            {
                selectedMarker = i;
                RepaintInspector();
            }

            if (EditorGUI.EndChangeCheck())
            {
                Vector2 uv = TryHit(e.mousePosition, out _, out Vector2 hit) ? hit : ToUv(moved);
                Undo.RecordObject(definition, "Move raid marker");
                marker.position = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
                EditorUtility.SetDirty(definition);
                RepaintInspector();
            }

            if (i == selectedMarker || HandleUtility.GetHandleSize(position) < LabelDistance) DrawMarkerLabel(marker, position, handleSize);
        }

        if (e.GetTypeForControl(defaultId) == EventType.MouseDown && e.button == 0 && !e.alt
            && HandleUtility.nearestControl == defaultId && TryHit(e.mousePosition, out _, out Vector2 clicked))
        {
            Undo.RecordObject(definition, "Add raid marker");
            markers.Add(new RaidMapMarker { kind = newMarkerKind, position = clicked });
            selectedMarker = markers.Count - 1;
            EditorUtility.SetDirty(definition);
            RepaintInspector();
            e.Use();
        }

        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            && selectedMarker >= 0 && selectedMarker < markers.Count)
        {
            RemoveMarker(selectedMarker);
            e.Use();
        }
    }

    // While painting zones the markers are only landmarks: small, unlabelled and not clickable.
    private void DrawMarkers(RaidMapDefinition definition)
    {
        if (Event.current.type != EventType.Repaint) return;
        foreach (RaidMapMarker marker in definition.Markers)
        {
            Vector3 position = Ground(marker.position);
            Handles.color = RaidMapMarkers.ColorOf(marker.kind);
            DrawPole(position, MarkerSize(position) * 0.6f, false);
        }
    }

    // Screen-sized when close, but never more than a couple of metres across, so a whole-map view stays readable.
    private static float MarkerSize(Vector3 position) => Mathf.Min(HandleUtility.GetHandleSize(position) * 0.12f, 1.5f);

    private static void DrawPole(Vector3 ground, float handleSize, bool selected)
    {
        Handles.DrawAAPolyLine(selected ? 5f : 3f, ground, ground + Vector3.up * handleSize * 3f);
        Handles.DrawWireDisc(ground + Vector3.up * OverlayLift, Vector3.up, handleSize * (selected ? 2.2f : 1.6f), selected ? 3f : 2f);
    }

    private static GUIStyle labelStyle;

    private static void DrawMarkerLabel(RaidMapMarker marker, Vector3 ground, float handleSize)
    {
        if (Event.current.type != EventType.Repaint) return;
        labelStyle ??= new GUIStyle(EditorStyles.whiteMiniLabel) { alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold };
        string text = ObjectNames.NicifyVariableName(marker.kind.ToString()) + (string.IsNullOrEmpty(marker.label) ? "" : "\n" + marker.label);
        Handles.Label(ground + Vector3.up * handleSize * 4.5f, text, labelStyle);
    }

    private void RemoveMarker(int index)
    {
        RaidMapDefinition definition = Definition;
        Undo.RecordObject(definition, "Remove raid marker");
        definition.Markers.RemoveAt(index);
        selectedMarker = -1;
        EditorUtility.SetDirty(definition);
        RepaintInspector();
        SceneView.RepaintAll();
    }

    // ---------------------------------------------------------------- Overlay

    private void DrawOverlay(RaidMapDefinition definition)
    {
        if (overlayMaterial == null)
        {
            Shader shader = Shader.Find(OverlayShader);
            if (shader == null) return;
            overlayMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        if (overlay == null || overlay.width != definition.ZoneResolution || overlayDirty)
        {
            RebuildOverlay(definition);
        }

        if (overlayMesh == null || meshDirty)
        {
            RebuildMesh();
        }

        overlayMaterial.mainTexture = overlay;
        // Zone cell i sits at i / resolution, the GPU's texel i at (i + 0.5) / resolution.
        overlayMaterial.mainTextureOffset = Vector2.one * (0.5f / overlay.width);
        overlayMaterial.SetFloat("_Opacity", opacity);
        overlayMaterial.SetPass(0);
        Graphics.DrawMeshNow(overlayMesh, Matrix4x4.identity);
    }

    private void RebuildOverlay(RaidMapDefinition definition)
    {
        int cells = definition.ZoneResolution;
        if (overlay == null || overlay.width != cells)
        {
            if (overlay != null) Object.DestroyImmediate(overlay);
            overlay = new Texture2D(cells, cells, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        UpdateOverlay(definition, new RectInt(0, 0, cells, cells));
        overlayDirty = false;
    }

    // Each cell blends the zones' colours by their strength; the selected zone is drawn last and strongest.
    private void UpdateOverlay(RaidMapDefinition definition, RectInt area)
    {
        if (overlay == null) return;
        List<RaidMapZone> zones = definition.Zones;
        int cells = overlay.width;
        var pixels = new Color32[area.width * area.height];
        for (int y = 0; y < area.height; y++)
        {
            for (int x = 0; x < area.width; x++)
            {
                int index = (area.y + y) * cells + area.x + x;
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int z = 0; z <= zones.Count; z++)
                {
                    // Every other zone first (dimmed while painting), then the selected one on top.
                    bool selected = z == zones.Count;
                    int zoneIndex = selected ? selectedZone : z;
                    if (zoneIndex >= zones.Count || (!selected && (zoneIndex == selectedZone || !showAllZones))) continue;

                    RaidMapZone zone = zones[zoneIndex];
                    if (zone.Weights == null || zone.Resolution != cells) continue;
                    float strength = zone.Weights[index] / 255f * (selected || mode != Mode.Zones ? 1f : 0.45f);
                    if (strength <= 0f) continue;
                    float blend = a <= 0f ? 1f : strength;
                    r = Mathf.Lerp(r, zone.color.r, blend);
                    g = Mathf.Lerp(g, zone.color.g, blend);
                    b = Mathf.Lerp(b, zone.color.b, blend);
                    a = 1f - (1f - a) * (1f - strength);
                }

                pixels[y * area.width + x] = new Color(r, g, b, a);
            }
        }

        overlay.SetPixels32(area.x, area.y, area.width, area.height, pixels);
        overlay.Apply(false);
    }

    private void RebuildMesh()
    {
        Terrain terrain = map.Terrain;
        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;
        int side = OverlaySegments + 1;
        var vertices = new Vector3[side * side];
        var uvs = new Vector2[side * side];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                float u = x / (float)OverlaySegments;
                float v = y / (float)OverlaySegments;
                Vector3 world = origin + new Vector3(u * size.x, 0f, v * size.z);
                world.y = origin.y + terrain.SampleHeight(world) + OverlayLift;
                vertices[y * side + x] = world;
                uvs[y * side + x] = new Vector2(u, v);
            }
        }

        var triangles = new int[OverlaySegments * OverlaySegments * 6];
        int t = 0;
        for (int y = 0; y < OverlaySegments; y++)
        {
            for (int x = 0; x < OverlaySegments; x++)
            {
                int i = y * side + x;
                triangles[t++] = i;
                triangles[t++] = i + side;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + side;
                triangles[t++] = i + side + 1;
            }
        }

        if (overlayMesh == null)
        {
            overlayMesh = new Mesh { name = "Raid Map Overlay", hideFlags = HideFlags.HideAndDontSave };
        }

        overlayMesh.Clear();
        overlayMesh.vertices = vertices;
        overlayMesh.uv = uvs;
        overlayMesh.triangles = triangles;
        overlayMesh.RecalculateBounds();
        meshDirty = false;
    }

    // ---------------------------------------------------------------- Helpers

    private bool TryHit(Vector2 mouse, out Vector3 point, out Vector2 uv)
    {
        point = default;
        uv = default;
        Terrain terrain = map.Terrain;
        if (terrain == null || !terrain.TryGetComponent(out TerrainCollider collider)) return false;

        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        if (!collider.Raycast(ray, out RaycastHit hit, 100000f)) return false;
        point = hit.point;
        uv = ToUv(hit.point);
        return uv.x >= 0f && uv.y >= 0f && uv.x <= 1f && uv.y <= 1f;
    }

    private Vector2 ToUv(Vector3 world)
    {
        Terrain terrain = map.Terrain;
        Vector3 local = world - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return new Vector2(local.x / size.x, local.z / size.z);
    }

    private Vector3 Ground(Vector2 uv)
    {
        Terrain terrain = map.Terrain;
        Vector3 size = terrain.terrainData.size;
        return GroundWorld(terrain.transform.position + new Vector3(uv.x * size.x, 0f, uv.y * size.z));
    }

    private Vector3 GroundWorld(Vector3 world)
    {
        Terrain terrain = map.Terrain;
        world.y = terrain.transform.position.y + terrain.SampleHeight(world);
        return world;
    }

    private void LookAtMap(bool topDown)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        float size = Definition.Size;
        Vector3 centre = map.Center + Vector3.up * Definition.Height * 0.5f;
        view.LookAt(centre, topDown ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(50f, 0f, 0f), size * (topDown ? 0.55f : 0.7f), topDown);
        view.Repaint();
    }

    private string UsedBy(string zoneName)
    {
        if (map.Content == null) return "  — no group";
        var names = new List<string>();
        foreach (SpawnGroup group in map.Content.GetComponentsInChildren<SpawnGroup>(true))
        {
            if (group.Zone == zoneName) names.Add(group.name);
        }

        return names.Count == 0 ? "  — no group" : "  → " + string.Join(", ", names);
    }

    private static bool CanRemove(List<RaidMapZone> zones, RaidMapZone zone)
    {
        // Path, Forest and Clearing paint the terrain; keep at least one of each.
        return zone.IsSpawnZone || zones.FindAll(other => other.role == zone.role).Count > 1;
    }

    private static string UniqueName(List<RaidMapZone> zones, string wanted)
    {
        string name = wanted;
        for (int n = 2; zones.Exists(zone => zone.name == name); n++)
        {
            name = $"{wanted} {n}";
        }

        return name;
    }

    private void ExportZone(RaidMapZone zone)
    {
        string file = EditorUtility.SaveFilePanel($"Export {zone.name}", "", $"{Definition.name} {zone.name}", "png");
        if (!string.IsNullOrEmpty(file)) RaidMapImages.ExportZone(zone, file);
    }

    private void SelectZone(int index)
    {
        selectedZone = Mathf.Max(0, index);
        MarkAllDirty();
    }

    private void MarkAllDirty()
    {
        overlayDirty = true;
        previewDirty = true;
        meshDirty = true;
        SceneView.RepaintAll();
    }

    private void ShowTools(bool show)
    {
        if (show == !toolsHidden) return;
        Tools.hidden = !show;
        toolsHidden = !show;
    }

    private static void RepaintInspector()
    {
        foreach (Editor editor in ActiveEditorTracker.sharedTracker.activeEditors)
        {
            if (editor is RaidMapEditor) editor.Repaint();
        }
    }
}
