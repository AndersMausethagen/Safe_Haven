using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Safe Haven › Quests › Story Graph: draws the selected Story Script as nodes and lets the designer build the story.
/// Right-click (or Space) adds a node, grouped as Triggers, Talk, Flow, Quest, World and Camera & Sound; drag from an
/// output (right) to an input (left) to say what comes next; Delete removes. Each node shows its settings, and a +
/// button beside an empty scene field makes what it needs in the scene (a Mob Spawn Zone, an object to use, a point to
/// look at) where the Scene view is looking. Problems are listed at the bottom; in Play mode nodes light up as they run.
/// Everything is undoable.
/// </summary>
public sealed class StoryGraphWindow : EditorWindow
{
    private StoryScript script;
    private StoryGraphView view;
    private Label title;
    private Label problems;

    [MenuItem("Safe Haven/Quests/Story Graph")]
    public static void Open()
    {
        var window = GetWindow<StoryGraphWindow>("Story Graph");
        window.Show();
        window.FromSelection();
    }

    [MenuItem("GameObject/Safe Haven/Story Script", false, 9)]
    private static void CreateScript(MenuCommand command)
    {
        Create("Story Script", command.context as GameObject, null);
    }

    /// <summary>
    /// A new story in the open scene that begins with <paramref name="trigger"/>, opened in the graph: how the quest designer
    /// makes the moments around a quest. The scene has to be saved to keep it.
    /// </summary>
    public static StoryScript CreateFor(string title, StoryTrigger trigger)
    {
        return Create(title, null, trigger);
    }

    private static StoryScript Create(string title, GameObject parent, StoryTrigger trigger)
    {
        var go = new GameObject(title);
        go.AddComponent<SaveableEntity>();
        var story = go.AddComponent<StoryScript>();
        if (trigger != null)
        {
            story.AddNode(trigger);
        }

        if (parent != null)
        {
            GameObjectUtility.SetParentAndAlign(go, parent);
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            go.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(go, "Create Story Script");
        Selection.activeGameObject = go;
        Open();
        GetWindow<StoryGraphWindow>().Load(story);
        return story;
    }

    private void OnEnable()
    {
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        var bar = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 6, paddingTop = 3, paddingBottom = 3 } };
        title = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } };
        bar.Add(title);
        bar.Add(new Button(() => view?.FrameAll()) { text = "Frame All" });
        bar.Add(new Button(() =>
        {
            if (script != null)
            {
                Selection.activeObject = script.gameObject;
                EditorGUIUtility.PingObject(script.gameObject);
            }
        }) { text = "Select in Scene" });
        rootVisualElement.Add(bar);

        view = new StoryGraphView(this) { style = { flexGrow = 1 } };
        rootVisualElement.Add(view);
        problems = new Label { style = { color = new Color(1f, 0.6f, 0.4f), paddingLeft = 6, paddingBottom = 4, whiteSpace = WhiteSpace.Normal } };
        rootVisualElement.Add(problems);

        Selection.selectionChanged += FromSelection;
        Undo.undoRedoPerformed += Reload;
        EditorApplication.playModeStateChanged += OnPlayMode;
        FromSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= FromSelection;
        Undo.undoRedoPerformed -= Reload;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        Watch(null);
    }

    private void OnPlayMode(PlayModeStateChange change) => FromSelection();

    private void FromSelection()
    {
        if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out StoryScript selected))
        {
            Load(selected);
        }
        else if (script == null)
        {
            Load(null);
        }
    }

    public void Load(StoryScript target)
    {
        if (target != script)
        {
            Watch(target);
        }

        script = target;
        title.text = script != null ? $"{script.name}  ({script.gameObject.scene.name})" : "Select a Story Script (or GameObject › Safe Haven › Story Script)";
        view.Show(script);
        ShowProblems();
    }

    private StoryScript watched;

    // In Play mode, nodes light up as they run.
    private void Watch(StoryScript target)
    {
        if (watched != null)
        {
            watched.NodeStarted -= OnNodeStarted;
        }

        watched = target;
        if (watched != null && Application.isPlaying)
        {
            watched.NodeStarted += OnNodeStarted;
        }
    }

    private void OnNodeStarted(StoryNode node) => view.Flash(node);

    public void Reload()
    {
        if (script != null)
        {
            view.Show(script);
        }

        ShowProblems();
    }

    public void ShowProblems()
    {
        List<string> found = script != null ? script.FindProblems() : new List<string>();
        problems.text = found.Count == 0 ? "" : "• " + string.Join("\n• ", found);
    }
}

/// <summary>The canvas of the Story Graph window.</summary>
public sealed class StoryGraphView : GraphView
{
    private readonly StoryGraphWindow window;
    private readonly Dictionary<string, StoryNodeView> views = new Dictionary<string, StoryNodeView>();
    private StoryScript script;
    private bool rebuilding;

    public StoryGraphView(StoryGraphWindow owner)
    {
        window = owner;
        SetupZoom(0.25f, 2f);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();
        graphViewChanged = OnChanged;
        nodeCreationRequest = request => ShowAddMenu(request.screenMousePosition);
    }

    public StoryScript Script => script;

    public void Show(StoryScript target)
    {
        rebuilding = true;
        script = target;
        DeleteElements(graphElements.ToList());
        views.Clear();
        if (script != null)
        {
            foreach (StoryNode node in script.Nodes)
            {
                if (node != null)
                {
                    var nodeView = new StoryNodeView(this, node);
                    views[node.id] = nodeView;
                    AddElement(nodeView);
                }
            }

            foreach (StoryLink link in script.Links)
            {
                if (views.TryGetValue(link.from, out StoryNodeView from) && views.TryGetValue(link.to, out StoryNodeView to)
                    && link.output < from.Outputs.Count && to.Input != null)
                {
                    AddElement(from.Outputs[link.output].ConnectTo(to.Input));
                }
            }
        }

        rebuilding = false;
    }

    public void Flash(StoryNode node)
    {
        if (node != null && views.TryGetValue(node.id, out StoryNodeView nodeView))
        {
            nodeView.Flash();
        }
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) =>
        ports.ToList().Where(port => port.direction != startPort.direction && port.node != startPort.node).ToList();

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        if (script == null)
        {
            return;
        }

        Vector2 at = contentViewContainer.WorldToLocal(evt.mousePosition);
        foreach (Type type in NodeTypes())
        {
            var sample = (StoryNode)Activator.CreateInstance(type);
            evt.menu.AppendAction($"Add/{sample.Category}/{sample.Title}", _ => Add(type, at));
        }

        base.BuildContextualMenu(evt);
    }

    private void ShowAddMenu(Vector2 screen)
    {
        var menu = new GenericMenu();
        Vector2 at = contentViewContainer.WorldToLocal(window.rootVisualElement.ChangeCoordinatesTo(this, screen - window.position.position));
        foreach (Type type in NodeTypes())
        {
            var sample = (StoryNode)Activator.CreateInstance(type);
            menu.AddItem(new GUIContent($"{sample.Category}/{sample.Title}"), false, () => Add(type, at));
        }

        menu.ShowAsContext();
    }

    private static IEnumerable<Type> NodeTypes() =>
        TypeCache.GetTypesDerivedFrom<StoryNode>().Where(t => !t.IsAbstract)
            .OrderBy(t => ((StoryNode)Activator.CreateInstance(t)).Category == "Triggers" ? 0 : 1)
            .ThenBy(t => ((StoryNode)Activator.CreateInstance(t)).Category)
            .ThenBy(t => ((StoryNode)Activator.CreateInstance(t)).Title);

    private void Add(Type type, Vector2 at)
    {
        Undo.RecordObject(script, "Add Story Node");
        var node = (StoryNode)Activator.CreateInstance(type);
        node.position = at;
        script.AddNode(node);
        Changed();
        window.Reload();
    }

    private GraphViewChange OnChanged(GraphViewChange change)
    {
        if (rebuilding || script == null)
        {
            return change;
        }

        Undo.RecordObject(script, "Edit Story Graph");
        if (change.elementsToRemove != null)
        {
            foreach (GraphElement element in change.elementsToRemove)
            {
                if (element is StoryNodeView nodeView)
                {
                    script.RemoveNode(nodeView.Node);
                }
                else if (element is Edge edge && edge.output?.node is StoryNodeView from && edge.input?.node is StoryNodeView to)
                {
                    script.Disconnect(from.Node, from.Outputs.IndexOf(edge.output), to.Node);
                }
            }
        }

        if (change.edgesToCreate != null)
        {
            foreach (Edge edge in change.edgesToCreate)
            {
                if (edge.output.node is StoryNodeView from && edge.input.node is StoryNodeView to)
                {
                    script.Connect(from.Node, from.Outputs.IndexOf(edge.output), to.Node);
                }
            }
        }

        if (change.movedElements != null)
        {
            foreach (GraphElement element in change.movedElements)
            {
                if (element is StoryNodeView nodeView)
                {
                    nodeView.Node.position = nodeView.GetPosition().position;
                }
            }
        }

        Changed();
        window.ShowProblems();
        return change;
    }

    public void Changed()
    {
        EditorUtility.SetDirty(script);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(script.gameObject.scene);
        }
    }

    /// <summary>A node's settings changed so its outputs did (a Choice's answers): redraw the graph.</summary>
    public void Rebuild() => window.Reload();
}

/// <summary>One node on the Story Graph canvas: its ports and its settings.</summary>
public sealed class StoryNodeView : Node
{
    private static readonly Color TriggerColour = new Color(0.55f, 0.35f, 0.15f);
    private static readonly Dictionary<string, Color> CategoryColours = new Dictionary<string, Color>
    {
        { "Talk", new Color(0.25f, 0.4f, 0.6f) }, { "Flow", new Color(0.35f, 0.35f, 0.35f) }, { "Quest", new Color(0.55f, 0.45f, 0.15f) },
        { "World", new Color(0.25f, 0.45f, 0.3f) }, { "Camera & Sound", new Color(0.45f, 0.3f, 0.5f) },
    };

    private readonly StoryGraphView graph;
    private int drawnOutputs;

    public StoryNodeView(StoryGraphView owner, StoryNode node)
    {
        graph = owner;
        Node = node;
        title = node.Title;
        viewDataKey = node.id;
        style.width = 300;
        SetPosition(new Rect(node.position, Vector2.zero));
        titleContainer.style.backgroundColor = node.IsTrigger ? TriggerColour
            : CategoryColours.TryGetValue(node.Category, out Color colour) ? colour : new Color(0.3f, 0.3f, 0.3f);

        if (!node.IsTrigger)
        {
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "";
            inputContainer.Add(Input);
        }

        drawnOutputs = node.OutputCount;
        for (int i = 0; i < node.OutputCount; i++)
        {
            Port output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            output.portName = node.OutputName(i);
            outputContainer.Add(output);
            Outputs.Add(output);
        }

        extensionContainer.Add(new IMGUIContainer(DrawSettings) { style = { paddingLeft = 6, paddingRight = 6, paddingBottom = 6 } });
        RefreshExpandedState();
        RefreshPorts();
    }

    public StoryNode Node { get; }
    public Port Input { get; }
    public List<Port> Outputs { get; } = new List<Port>();

    public void Flash()
    {
        titleContainer.style.borderBottomColor = Color.yellow;
        titleContainer.style.borderBottomWidth = 3;
        schedule.Execute(() => titleContainer.style.borderBottomWidth = 0).StartingIn(1500);
    }

    private void DrawSettings()
    {
        StoryScript script = graph.Script;
        if (script == null)
        {
            return;
        }

        int index = script.Nodes.ToList().IndexOf(Node);
        if (index < 0)
        {
            return;
        }

        var data = new SerializedObject(script);
        SerializedProperty element = data.FindProperty("nodes").GetArrayElementAtIndex(index);
        EditorGUIUtility.labelWidth = 110;
        EditorGUI.BeginChangeCheck();
        SerializedProperty child = element.Copy();
        SerializedProperty end = element.GetEndProperty();
        bool enter = true;
        while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
        {
            enter = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(child, true);
                CreateButton(child, script);
            }
        }

        string problem = Node.FindProblem();
        if (problem != null)
        {
            EditorGUILayout.HelpBox(problem, MessageType.None);
        }

        if (EditorGUI.EndChangeCheck())
        {
            data.ApplyModifiedProperties();
            graph.Changed();
            title = Node.Title;
            if (Node.OutputCount != drawnOutputs)
            {
                EditorApplication.delayCall += graph.Rebuild;
            }
        }
    }

    // A + beside an empty scene field: makes what the node needs in the scene, where the Scene view looks.
    private void CreateButton(SerializedProperty property, StoryScript script)
    {
        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != null || property.depth > 1)
        {
            return;
        }

        FieldInfo field = Node.GetType().GetField(property.name, BindingFlags.Public | BindingFlags.Instance);
        Type type = field != null ? field.FieldType : null;
        if (type != typeof(MobSpawnZone) && type != typeof(Interactable) && type != typeof(Transform) && type != typeof(GameObject))
        {
            return;
        }

        if (GUILayout.Button(new GUIContent("+", $"Create a {Readable(type)} in the scene for this"), GUILayout.Width(22)))
        {
            GameObject made = StorySceneObjects.Create(type, script, Node.Title);
            property.objectReferenceValue = type == typeof(GameObject) ? (UnityEngine.Object)made
                : type == typeof(Transform) ? made.transform
                : made.GetComponent(type);
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    private static string Readable(Type type) =>
        type == typeof(MobSpawnZone) ? "Mob Spawn Zone" : type == typeof(Interactable) ? "usable object" : "point";
}

/// <summary>What a story's + buttons make in the scene.</summary>
public static class StorySceneObjects
{
    private const string ZonePrefab = "Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab";

    public static GameObject Create(Type type, StoryScript script, string forNode)
    {
        Vector3 at = Ground(SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : script.transform.position);
        GameObject made;
        if (type == typeof(MobSpawnZone))
        {
            made = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ZonePrefab), script.gameObject.scene);
            made.name = $"{script.name} - Spawn Zone";
        }
        else if (type == typeof(Interactable))
        {
            made = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.name = $"{script.name} - Object";
            made.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            at += Vector3.up * 0.5f;
            var interactable = made.AddComponent<Interactable>();
            var data = new SerializedObject(interactable);
            data.FindProperty("duration").floatValue = 1f;
            data.FindProperty("range").floatValue = 2f;
            data.FindProperty("deactivateOnComplete").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            made = new GameObject($"{script.name} - {forNode} Point");
        }

        SceneManagerMove(made, script);
        made.transform.position = at;
        Undo.RegisterCreatedObjectUndo(made, "Create " + made.name);
        Selection.activeGameObject = made;
        return made;
    }

    private static void SceneManagerMove(GameObject made, StoryScript script)
    {
        if (made.scene != script.gameObject.scene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(made, script.gameObject.scene);
        }
    }

    private static Vector3 Ground(Vector3 point) =>
        Physics.Raycast(point + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point
            : point;
}
