using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every quest on one canvas: chains as rows, prerequisites as arrows, and under each quest the Quest Events it waits
/// for with the scene objects that raise them, so missing links show up in red. Click a quest to edit it.
/// </summary>
public sealed class QuestMapWindow : EditorWindow
{
    private const float NodeWidth = 240f;
    private const float ColumnGap = 60f;
    private const float RowGap = 18f;
    private const float LineHeight = 16f;
    private const float Margin = 16f;
    private const float BandLabelHeight = 20f;
    private const float MinDragToCreate = 30f;

    private sealed class Line
    {
        public string Text;
        public bool Problem;
    }

    private sealed class Box
    {
        public QuestMapNode Node;
        public Rect Rect;
        public readonly List<Line> Lines = new List<Line>();
    }

    private readonly List<Box> boxes = new List<Box>();
    private readonly List<(string band, float y)> bandLabels = new List<(string, float)>();
    private Box dragFrom;
    private Vector2 dragPosition;
    private Vector2 dragStart;
    private Vector2 scroll;
    private Vector2 canvasSize;
    private bool dirty = true;
    private GUIStyle titleStyle;
    private GUIStyle lineStyle;
    private GUIStyle problemStyle;

    [MenuItem("Safe Haven/Quests/Quest Map")]
    public static void Open()
    {
        var window = GetWindow<QuestMapWindow>();
        window.titleContent = new GUIContent("Quest Map");
        window.minSize = new Vector2(500f, 300f);
        window.dirty = true;
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += MarkDirty;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= MarkDirty;
    }

    private void OnFocus()
    {
        MarkDirty();
    }

    private void MarkDirty()
    {
        dirty = true;
        Repaint();
    }

    private void OnGUI()
    {
        EnsureStyles();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Drag a quest onto another to make it come after it, or onto empty space for a new follow-up. Right-click for more. Red: something is missing.", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ New Quest", EditorStyles.toolbarButton))
            {
                EditorApplication.delayCall += () => CreateQuest(null);
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                QuestSceneIndex.Invalidate();
                dirty = true;
            }
        }

        if (dirty)
        {
            Rebuild();
        }

        if (boxes.Count == 0)
        {
            EditorGUILayout.HelpBox("No quests yet. Create one in the Content Editor under Quests.", MessageType.Info);
            return;
        }

        scroll = GUI.BeginScrollView(new Rect(0f, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight),
            scroll, new Rect(Vector2.zero, canvasSize));
        foreach ((string band, float y) in bandLabels)
        {
            GUI.Label(new Rect(Margin, y, 400f, BandLabelHeight), band, EditorStyles.boldLabel);
        }

        DrawEdges();
        foreach (Box box in boxes)
        {
            DrawBox(box);
        }

        DrawDragLine();
        HandleInput();
        GUI.EndScrollView();
    }

    private void DrawDragLine()
    {
        if (dragFrom == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        Box target = BoxAt(dragPosition);
        bool valid = target != null && QuestMapLayout.CheckLink(dragFrom.Node.Quest, target.Node.Quest) == null;
        Color color = target == null ? Color.white : valid ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.45f, 0.4f);
        var start = new Vector3(dragFrom.Rect.xMax, dragFrom.Rect.y + 12f);
        Handles.DrawBezier(start, dragPosition, start + Vector3.right * 50f, (Vector3)dragPosition + Vector3.left * 50f, color, null, 3f);
        if (target == null && Vector2.Distance(dragPosition, dragStart) > MinDragToCreate)
        {
            GUI.Label(new Rect(dragPosition.x + 10f, dragPosition.y - 18f, 200f, 18f), "+ New follow-up quest", EditorStyles.miniBoldLabel);
        }
        if (target != null)
        {
            Handles.DrawSolidRectangleWithOutline(target.Rect, Color.clear, color);
        }
    }

    // Drag from a quest onto another to make the second come after the first; right-click for more.
    private void HandleInput()
    {
        Event current = Event.current;
        Vector2 mouse = current.mousePosition;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        switch (current.rawType)
        {
            case EventType.MouseDown when current.button == 0 && current.type == EventType.MouseDown:
                dragFrom = BoxAt(mouse);
                if (dragFrom != null)
                {
                    dragPosition = mouse;
                    dragStart = mouse;
                    GUIUtility.hotControl = control;
                    current.Use();
                }

                break;
            case EventType.MouseDrag when dragFrom != null:
                dragPosition = mouse;
                current.Use();
                Repaint();
                break;
            case EventType.MouseUp when dragFrom != null:
                GUIUtility.hotControl = 0;
                Box target = BoxAt(mouse);
                QuestDefinition from = dragFrom.Node.Quest;
                dragFrom = null;
                current.Use();
                Repaint();
                if (target != null && target.Node.Quest != from)
                {
                    Link(from, target.Node.Quest);
                }
                else if (target == null && Vector2.Distance(mouse, dragStart) > MinDragToCreate)
                {
                    // Dropped on empty space: a new quest that comes after the dragged one.
                    EditorApplication.delayCall += () => CreateQuest(from);
                }

                break;
            case EventType.ContextClick:
                ShowMenu(BoxAt(mouse));
                current.Use();
                break;
        }
    }

    private Box BoxAt(Vector2 point)
    {
        for (int i = boxes.Count - 1; i >= 0; i--)
        {
            if (boxes[i].Rect.Contains(point))
            {
                return boxes[i];
            }
        }

        return null;
    }

    private void Link(QuestDefinition from, QuestDefinition to)
    {
        string problem = QuestMapLayout.CheckLink(from, to);
        if (problem != null)
        {
            ShowNotification(new GUIContent(problem));
            return;
        }

        var data = new SerializedObject(to);
        SerializedProperty prerequisites = data.FindProperty("prerequisites");
        prerequisites.arraySize++;
        prerequisites.GetArrayElementAtIndex(prerequisites.arraySize - 1).objectReferenceValue = from;
        if (string.IsNullOrWhiteSpace(to.Chain) && !string.IsNullOrWhiteSpace(from.Chain))
        {
            data.FindProperty("chain").stringValue = from.Chain;
        }

        Save(data, $"{to.DisplayName} now comes after {from.DisplayName}");
    }

    private void Unlink(QuestDefinition from, QuestDefinition to)
    {
        var data = new SerializedObject(to);
        SerializedProperty prerequisites = data.FindProperty("prerequisites");
        for (int i = prerequisites.arraySize - 1; i >= 0; i--)
        {
            if (prerequisites.GetArrayElementAtIndex(i).objectReferenceValue == from)
            {
                prerequisites.GetArrayElementAtIndex(i).objectReferenceValue = null;
                prerequisites.DeleteArrayElementAtIndex(i);
            }
        }

        Save(data, $"{to.DisplayName} no longer needs {from.DisplayName}");
    }

    private void Save(SerializedObject data, string message)
    {
        data.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(data.targetObject);
        ShowNotification(new GUIContent(message));
        MarkDirty();
    }

    private void ShowMenu(Box box)
    {
        var menu = new GenericMenu();
        if (box == null)
        {
            menu.AddItem(new GUIContent("New Quest"), false, () => CreateQuest(null));
            menu.ShowAsContext();
            return;
        }

        QuestDefinition quest = box.Node.Quest;
        menu.AddItem(new GUIContent("Edit"), false, () => Edit(quest));
        menu.AddItem(new GUIContent("New Quest That Follows This One"), false, () => CreateQuest(quest));
        menu.AddSeparator("");
        if (quest.Prerequisites.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("Stop Coming After/(nothing)"));
        }

        foreach (QuestDefinition prerequisite in quest.Prerequisites)
        {
            if (prerequisite != null)
            {
                QuestDefinition captured = prerequisite;
                menu.AddItem(new GUIContent($"Stop Coming After/{prerequisite.DisplayName}"), false, () => Unlink(captured, quest));
            }
        }

        menu.ShowAsContext();
    }

    private static void Edit(QuestDefinition quest)
    {
        ContentEditorWindow window = ContentEditorWindow.Open(typeof(QuestSection));
        window.Select(window.FindSectionFor(quest), quest);
    }

    // Opens a draft in the Content Editor; it is saved there, then shows up here.
    private static void CreateQuest(QuestDefinition after)
    {
        ContentEditorWindow window = ContentEditorWindow.Open(typeof(QuestSection));
        ContentSection section = window.FindSection(typeof(QuestSection));
        window.CreateNew(section, after != null ? draft => QuestSection.MakeFollow(draft, after) : (System.Action<ScriptableObject>)null);
    }

    private void DrawEdges()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        foreach (Box to in boxes)
        {
            foreach (QuestDefinition prerequisite in to.Node.Quest.Prerequisites)
            {
                Box from = boxes.Find(box => box.Node.Quest == prerequisite);
                if (from == null || from == to)
                {
                    continue;
                }

                var start = new Vector3(from.Rect.xMax, from.Rect.y + 12f);
                var end = new Vector3(to.Rect.x, to.Rect.y + 12f);
                float bend = Mathf.Max(30f, Mathf.Abs(end.x - start.x) * 0.5f);
                Handles.DrawBezier(start, end, start + Vector3.right * bend, end + Vector3.left * bend, new Color(0.75f, 0.75f, 0.75f), null, 2f);
                Handles.color = new Color(0.75f, 0.75f, 0.75f);
                Handles.DrawAAConvexPolygon(end, end + new Vector3(-8f, -4f), end + new Vector3(-8f, 4f));
            }
        }
    }

    private void DrawBox(Box box)
    {
        GUI.Box(box.Rect, GUIContent.none, EditorStyles.helpBox);
        var titleRect = new Rect(box.Rect.x + 6f, box.Rect.y + 4f, box.Rect.width - 12f, LineHeight + 2f);
        if (GUI.Button(titleRect, box.Node.Quest.DisplayName, titleStyle))
        {
            Edit(box.Node.Quest);
            GUIUtility.ExitGUI();
        }

        EditorGUIUtility.AddCursorRect(titleRect, MouseCursor.Link);
        var handle = new Rect(box.Rect.xMax - 7f, box.Rect.y + 6f, 12f, 12f);
        GUI.Label(handle, "●", lineStyle);
        EditorGUIUtility.AddCursorRect(new Rect(box.Rect.x, titleRect.yMax, box.Rect.width, box.Rect.height - titleRect.height), MouseCursor.ArrowPlus);
        float y = titleRect.yMax + 2f;
        foreach (Line line in box.Lines)
        {
            GUI.Label(new Rect(box.Rect.x + 6f, y, box.Rect.width - 12f, LineHeight), line.Text, line.Problem ? problemStyle : lineStyle);
            y += LineHeight;
        }
    }

    private void Rebuild()
    {
        dirty = false;
        boxes.Clear();
        bandLabels.Clear();
        List<QuestDefinition> quests = ResourceCatalogTools.LoadAll<QuestDefinition>();
        List<QuestMapNode> nodes = QuestMapLayout.Build(quests);

        var rowHeights = new Dictionary<int, float>();
        foreach (QuestMapNode node in nodes)
        {
            var box = new Box { Node = node };
            Describe(box, quests);
            float height = LineHeight + 10f + box.Lines.Count * LineHeight;
            box.Rect = new Rect(0f, 0f, NodeWidth, height);
            rowHeights.TryGetValue(node.Row, out float tallest);
            rowHeights[node.Row] = Mathf.Max(tallest, height);
            boxes.Add(box);
        }

        // Rows stack top to bottom; each band gets a label above its first row.
        var rowTops = new Dictionary<int, float>();
        float y = Margin;
        string currentBand = null;
        int rowCount = rowHeights.Count == 0 ? 0 : MaxKey(rowHeights) + 1;
        for (int row = 0; row < rowCount; row++)
        {
            QuestMapNode first = nodes.Find(node => node.Row == row);
            if (first != null && first.Band != currentBand)
            {
                currentBand = first.Band;
                bandLabels.Add((currentBand, y));
                y += BandLabelHeight;
            }

            rowTops[row] = y;
            y += (rowHeights.TryGetValue(row, out float height) ? height : 0f) + RowGap;
        }

        float width = 0f;
        foreach (Box box in boxes)
        {
            box.Rect.position = new Vector2(Margin + box.Node.Column * (NodeWidth + ColumnGap), rowTops[box.Node.Row]);
            width = Mathf.Max(width, box.Rect.xMax);
        }

        canvasSize = new Vector2(width + Margin, y + Margin);
    }

    private static int MaxKey(Dictionary<int, float> rows)
    {
        int max = 0;
        foreach (int key in rows.Keys)
        {
            max = Mathf.Max(max, key);
        }

        return max;
    }

    private static string DelayText(QuestDefinition quest) => quest.StartDelay > 0f ? $", {quest.StartDelay:0.#} s later" : "";

    private static void Describe(Box box, List<QuestDefinition> quests)
    {
        QuestDefinition quest = box.Node.Quest;
        switch (quest.Start)
        {
            case QuestStart.ByQuestGiver:
                bool hasGiver = QuestSceneIndex.HasGiver(quest);
                bool byStory = QuestSceneIndex.IsStartedByStory(quest);
                box.Lines.Add(new Line
                {
                    Text = hasGiver ? "Starts: from a Quest Giver" : byStory ? "Starts: from a story" : "Starts: no Quest Giver in any scene",
                    Problem = !hasGiver && !byStory,
                });
                break;
            case QuestStart.OnQuestEvent:
                box.Lines.Add(new Line { Text = "Starts: on an event" + DelayText(quest) });
                AddEvent(box, quest.StartEvent, quests);
                break;
            default:
                box.Lines.Add(new Line { Text = "Starts: automatically" + DelayText(quest) });
                break;
        }

        foreach (QuestObjective objective in quest.Objectives)
        {
            if (objective != null && objective.Type == ObjectiveType.QuestEvent)
            {
                AddEvent(box, objective.QuestEvent, quests);
            }
        }

        if (quest.TurnInToGiver)
        {
            box.Lines.Add(new Line { Text = $"Hand in to {quest.TurnInTo}" });
        }

        int problems = quest.FindProblems().Count + QuestSection.FindSceneProblems(quest, quests).Count;
        if (problems > 0)
        {
            box.Lines.Add(new Line { Text = $"{problems} problem{(problems == 1 ? "" : "s")}: click to see", Problem = true });
        }
    }

    // The event the quest waits for, then each scene object that raises it.
    private static void AddEvent(Box box, GameEventDefinition gameEvent, List<QuestDefinition> quests)
    {
        if (gameEvent == null)
        {
            box.Lines.Add(new Line { Text = "  ⚡ (no event set)", Problem = true });
            return;
        }

        box.Lines.Add(new Line { Text = $"  ⚡ {gameEvent.DisplayName}" });
        int raisers = 0;
        foreach (QuestDefinition other in quests)
        {
            if (other != null && other.CompletionEvent == gameEvent)
            {
                box.Lines.Add(new Line { Text = $"      ← finishing {other.DisplayName}" });
                raisers++;
            }
        }

        foreach (QuestSceneUse use in QuestSceneIndex.UsesOf(gameEvent))
        {
            if (use.Role == QuestSceneRole.Raises || use.Role == QuestSceneRole.Other || use.Role == QuestSceneRole.PrefabOverride)
            {
                box.Lines.Add(new Line { Text = $"      ← {use.Describe()}" });
                raisers++;
            }
        }

        if (raisers == 0)
        {
            box.Lines.Add(new Line { Text = "      nothing raises it", Problem = true });
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = EditorStyles.linkLabel.normal.textColor } };
        lineStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
        problemStyle = new GUIStyle(lineStyle) { normal = { textColor = new Color(1f, 0.45f, 0.4f) } };
    }
}
