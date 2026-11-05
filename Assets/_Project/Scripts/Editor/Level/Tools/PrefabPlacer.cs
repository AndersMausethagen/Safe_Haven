using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Placing one prefab at a time in the Scene view (World Tools › Place): a preview of it follows the mouse over the
/// ground (and over other objects, so a stone can sit on a rock), set down by the bottom of its meshes; click to place
/// one, keep clicking for more (each gets a new random turn and size), Esc or right-click to stop.
/// <list type="bullet">
/// <item>R turns the next object by 15° (Shift+R the other way); [ and ] make it smaller or larger.</item>
/// <item>Placed objects go under "Level Props" (or, in a raid map, under its Map Content, so they turn with the map).</item>
/// <item>The preview is only drawn, never added to the scene, so looking around leaves the scene unchanged.</item>
/// </list>
/// </summary>
[InitializeOnLoad]
public static class PrefabPlacer
{
    private const string PropsRoot = "Level Props";

    private readonly struct Part
    {
        public readonly Mesh Mesh;
        public readonly Material[] Materials;
        public readonly Matrix4x4 Local; // the mesh in the prefab root's space

        public Part(Mesh mesh, Material[] materials, Matrix4x4 local)
        {
            Mesh = mesh;
            Materials = materials;
            Local = local;
        }
    }

    private static GameObject prefab;
    private static readonly List<Part> parts = new List<Part>();
    private static float yaw;
    private static float scale = 1f;
    private static float extraYaw;
    private static float extraScale = 1f;
    private static bool aiming;
    private static Vector3 aimPoint;
    private static Vector3 aimNormal;

    public static GameObject Active => prefab;

    static PrefabPlacer()
    {
        SceneView.duringSceneGui += OnSceneGui;
        EditorApplication.playModeStateChanged += _ => Stop();
    }

    public static void Start(GameObject toPlace)
    {
        prefab = toPlace;
        extraYaw = 0f;
        extraScale = 1f;
        CollectParts();
        Reroll();
        SceneView.lastActiveSceneView?.Focus();
        SceneView.RepaintAll();
    }

    public static void Stop()
    {
        prefab = null;
        parts.Clear();
        SceneView.RepaintAll();
    }

    private static void Reroll()
    {
        yaw = PlacementSettings.RandomTurn ? Random.Range(0f, 360f) : 0f;
        Vector2 range = PlacementSettings.ScaleRange;
        scale = Random.Range(range.x, range.y);
    }

    /// <summary>The prefab's visible meshes (the most detailed level of a LOD Group only).</summary>
    private static void CollectParts()
    {
        parts.Clear();
        var skip = new HashSet<Renderer>();
        foreach (LODGroup group in prefab.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            for (int i = 1; i < lods.Length; i++)
            {
                skip.UnionWith(lods[i].renderers.Where(r => r != null));
            }
        }

        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (skip.Contains(renderer) || filter == null || filter.sharedMesh == null || !renderer.enabled)
            {
                continue;
            }

            parts.Add(new Part(filter.sharedMesh, renderer.sharedMaterials, toRoot * renderer.transform.localToWorldMatrix));
        }
    }

    private static void OnSceneGui(SceneView view)
    {
        if (prefab == null)
        {
            return;
        }

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        Handles.BeginGUI();
        GUI.Label(new Rect(10, 10, 660, 22), $"Placing {prefab.name}: click to place · R turn · [ ] size · Esc or right-click to stop",
                  EditorStyles.helpBox);
        Handles.EndGUI();

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape || e.type == EventType.MouseDown && e.button == 1)
        {
            Stop();
            e.Use();
            return;
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            extraYaw += e.shift ? -15f : 15f;
            e.Use();
        }
        else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
        {
            extraScale = Mathf.Clamp(extraScale * (e.keyCode == KeyCode.RightBracket ? 1.1f : 1f / 1.1f), 0.2f, 5f);
            e.Use();
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Layout)
        {
            aiming = Aim(e.mousePosition, out aimPoint, out aimNormal);
            if (e.type != EventType.Layout)
            {
                view.Repaint();
            }
        }

        if (!aiming)
        {
            return;
        }

        Matrix4x4 pose = Pose(aimPoint, aimNormal);
        if (e.type == EventType.Repaint)
        {
            foreach (Part part in parts)
            {
                for (int sub = 0; sub < part.Mesh.subMeshCount && sub < part.Materials.Length; sub++)
                {
                    if (part.Materials[sub] != null)
                    {
                        Graphics.DrawMesh(part.Mesh, pose * part.Local, part.Materials[sub], 0, view.camera, sub);
                    }
                }
            }

            Handles.color = new Color(1f, 1f, 1f, 0.6f);
            Handles.DrawWireDisc(aimPoint, aimNormal, 0.4f);
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Place(pose);
            e.Use();
        }
    }

    /// <summary>The surface under the mouse: the nearest collider, else nothing.</summary>
    private static bool Aim(Vector2 mouse, out Vector3 point, out Vector3 normal)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = normal = Vector3.zero;
        return false;
    }

    /// <summary>Where the prefab's root goes: turned, sized, tilted with the slope if wanted, and lifted so the bottom of
    /// its meshes sits on the point (minus the sink).</summary>
    private static Matrix4x4 Pose(Vector3 point, Vector3 normal)
    {
        Quaternion tilt = PlacementSettings.AlignToSlope ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
        Quaternion rotation = tilt * Quaternion.Euler(0f, yaw + extraYaw, 0f);
        Vector3 size = prefab.transform.localScale * (scale * extraScale);
        Matrix4x4 shape = Matrix4x4.TRS(Vector3.zero, rotation, size);
        float bottom = float.MaxValue;
        foreach (Part part in parts)
        {
            Bounds b = part.Mesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                bottom = Mathf.Min(bottom, (shape * part.Local).MultiplyPoint3x4(corner).y);
            }
        }

        float lift = bottom == float.MaxValue ? 0f : -bottom;
        return Matrix4x4.TRS(point + Vector3.up * (lift - PlacementSettings.Sink), rotation, size);
    }

    private static void Place(Matrix4x4 pose)
    {
        GroundStick.Suspend();
        Scene scene = SceneManager.GetActiveScene();
        var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(placed, "Place " + prefab.name);
        Transform parent = ParentFor(scene);
        if (parent != null)
        {
            placed.transform.SetParent(parent, true);
        }

        placed.transform.SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
        placed.transform.localScale = pose.lossyScale;
        if (placed.transform.parent != null)
        {
            placed.transform.localScale = new Vector3(
                pose.lossyScale.x / placed.transform.parent.lossyScale.x, pose.lossyScale.y / placed.transform.parent.lossyScale.y,
                pose.lossyScale.z / placed.transform.parent.lossyScale.z);
        }

        if (placed.GetComponentInChildren<SaveableEntity>(true) != null)
        {
            SaveableEntityTools.AssignSceneIds(scene);
        }

        Reroll();
    }

    /// <summary>"Level Props" at the scene root, or Map Content › Props in a raid map (so it turns with the map).</summary>
    private static Transform ParentFor(Scene scene)
    {
        RaidMap map = Object.FindAnyObjectByType<RaidMap>();
        Transform root = map != null && map.gameObject.scene == scene && map.Content != null ? map.Content : null;
        Transform props = root != null ? root.Find("Props") : SceneFolders.Find(scene, PropsRoot)?.transform;
        if (props != null)
        {
            return props;
        }

        var created = new GameObject(root != null ? "Props" : PropsRoot);
        Undo.RegisterCreatedObjectUndo(created, "Place " + prefab.name);
        SceneManager.MoveGameObjectToScene(created, scene);
        if (root != null)
        {
            created.transform.SetParent(root, false);
        }
        else
        {
            SceneFolders.Place(created);
        }

        return created.transform;
    }
}
