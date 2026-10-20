using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Quests in the Content Editor: grouped by chain, with the chain around each quest and play-test buttons.</summary>
public sealed class QuestSection : ContentSection
{
    public override string Title => "Quests";
    public override string Noun => "Quest";
    public override string SidebarGroup => "Quests";
    public override Type AssetType => typeof(QuestDefinition);
    public override string Folder => "Assets/_Project/Data/Quests";
    public override string Help =>
        "Quests and quest chains. A quest has objectives (gather, kill, craft, complete raids, or any Quest Event), rewards, " +
        "and a way to start: automatically, on a Quest Event, or from a Quest Giver. To make a chain, list the previous quest " +
        "as a prerequisite and let the next one start automatically.";

    public override string ListGroup(Object asset)
    {
        string chain = ((QuestDefinition)asset).Chain;
        return string.IsNullOrWhiteSpace(chain) ? "No chain" : chain;
    }

    public override int Compare(Object a, Object b)
    {
        int group = string.Compare(ListGroup(a), ListGroup(b), StringComparison.OrdinalIgnoreCase);
        if (group != 0)
        {
            return group;
        }

        int depth = Depth((QuestDefinition)a).CompareTo(Depth((QuestDefinition)b));
        return depth != 0 ? depth : string.Compare(Label(a), Label(b), StringComparison.OrdinalIgnoreCase);
    }

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var quest = (QuestDefinition)asset;
        problems.AddRange(quest.FindProblems());
        problems.AddRange(FindSceneProblems(quest, all.OfType<QuestDefinition>()));
        return problems;
    }

    /// <summary>Loose ends between the quest and the saved scenes and prefabs.</summary>
    public static List<string> FindSceneProblems(QuestDefinition quest, IEnumerable<QuestDefinition> allQuests)
    {
        var problems = new List<string>();
        bool hasGiver = QuestSceneIndex.HasGiver(quest);
        if (quest.Start == QuestStart.ByQuestGiver && !hasGiver)
        {
            problems.Add("Starts from a Quest Giver, but no scene or prefab has one for it (GameObject > Safe Haven > Quest Giver).");
        }
        else if (quest.TurnInToGiver && !hasGiver)
        {
            problems.Add("Is handed in to a Quest Giver, but no scene or prefab has one for it, so it can never finish.");
        }
        else if (hasGiver && quest.Start != QuestStart.ByQuestGiver && !quest.TurnInToGiver)
        {
            problems.Add("Has a Quest Giver, but neither starts from it nor is handed in to it: the giver only shows the \"waiting\" line.");
        }

        List<QuestDefinition> quests = allQuests.ToList();
        if (quest.Start == QuestStart.OnQuestEvent && quest.StartEvent != null && !QuestSceneIndex.IsRaised(quest.StartEvent, quests))
        {
            problems.Add($"Starts on \"{quest.StartEvent.DisplayName}\", but nothing raises it yet (add a Quest Event Trigger).");
        }

        foreach (QuestObjective objective in quest.Objectives)
        {
            if (objective != null && objective.Type == ObjectiveType.QuestEvent && objective.QuestEvent != null
                && !QuestSceneIndex.IsRaised(objective.QuestEvent, quests))
            {
                problems.Add($"Waits for \"{objective.QuestEvent.DisplayName}\", but nothing raises it yet (add a Quest Event Trigger).");
            }
        }

        return problems;
    }

    public override void DrawExtras(Object asset)
    {
        var quest = (QuestDefinition)asset;
        ContentUtility.BeginPanel("In the Game", "How this quest fits with the others and what the player sees.");
        EditorGUILayout.LabelField("Starts", DescribeStart(quest), ContentUtility.WrappedMiniLabel);
        EditorGUILayout.LabelField("Objectives", ContentUtility.WrappedMiniLabel);
        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            EditorGUILayout.LabelField($"   {(quest.ObjectivesInOrder ? $"{i + 1}." : "•")} {quest.Objectives[i].Describe()}", ContentUtility.WrappedMiniLabel);
        }

        string rewards = quest.DescribeRewards();
        EditorGUILayout.LabelField("Rewards", rewards.Length > 0 ? rewards : "none", ContentUtility.WrappedMiniLabel);
        EditorGUILayout.LabelField("Finishes", quest.TurnInToGiver
            ? $"When the player returns to {quest.TurnInTo} (a Quest Giver for it); rewards are given then."
            : "As soon as every objective is done.", ContentUtility.WrappedMiniLabel);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Comes after", EditorStyles.miniBoldLabel);
        DrawLinks(new List<QuestDefinition>(quest.Prerequisites), "Nothing: it is available from the start.");
        EditorGUILayout.LabelField("Leads to", EditorStyles.miniBoldLabel);
        DrawLinks(Followers(quest), "No quest follows it.");
        if (GUILayout.Button("+  New Quest That Follows This One"))
        {
            Host.CreateNew(this, draft => MakeFollow(draft, quest));
            GUIUtility.ExitGUI();
        }

        ContentUtility.EndPanel();

        if (GUILayout.Button("Open Quest Map"))
        {
            QuestMapWindow.Open();
        }

        ContentUtility.BeginPanel("In the Scenes", "Saved scenes and prefabs that give, gate or react to this quest. Click to open and select.");
        DrawSceneUses(QuestSceneIndex.UsesOf(quest), "Nothing in a scene or prefab uses it.");
        DrawSelectInOpenScenes(quest);
        ContentUtility.EndPanel();

        if (EditorApplication.isPlaying && QuestManager.Instance != null)
        {
            DrawPlaytest(quest);
        }
    }

    private void DrawPlaytest(QuestDefinition quest)
    {
        QuestLog log = QuestManager.Instance.Log;
        QuestLog.Entry entry = log.Get(quest);
        ContentUtility.BeginPanel("Play Test", "The game is running: this quest's state for the current player.");
        EditorGUILayout.LabelField("Status", log.StatusOf(quest).ToString());
        if (entry != null)
        {
            for (int i = 0; i < entry.Counts.Length; i++)
            {
                EditorGUILayout.LabelField($"   {quest.Objectives[i].Describe()}", $"{entry.Counts[i]} / {quest.Objectives[i].Amount}");
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(log.StatusOf(quest) != QuestStatus.NotStarted))
            {
                if (GUILayout.Button("Start Now"))
                {
                    if (!QuestManager.Instance.TryStartQuest(quest, out string reason))
                    {
                        Host.Notify(reason);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(log.StatusOf(quest) != QuestStatus.ReadyToTurnIn))
            {
                if (GUILayout.Button(new GUIContent("Turn In Now", "Hands it in as if the player returned to the giver.")))
                {
                    if (!QuestManager.Instance.TryTurnIn(quest, out string reason))
                    {
                        Host.Notify(reason);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(log.StatusOf(quest) == QuestStatus.Completed))
            {
                if (GUILayout.Button(new GUIContent("Complete Now", "Completes it as if every objective were done, with rewards, skipping any hand-in.")))
                {
                    log.ForceComplete(quest);
                }
            }
        }

        ContentUtility.EndPanel();
        Host.Repaint();
    }

    private void DrawLinks(List<QuestDefinition> quests, string none)
    {
        quests.RemoveAll(quest => quest == null);
        if (quests.Count == 0)
        {
            EditorGUILayout.LabelField("   " + none, ContentUtility.WrappedMiniLabel);
            return;
        }

        foreach (QuestDefinition linked in quests)
        {
            if (GUILayout.Button("   " + linked.DisplayName, EditorStyles.linkLabel))
            {
                Host.Select(this, linked);
                GUIUtility.ExitGUI();
            }
        }
    }

    private static readonly Dictionary<QuestSceneRole, string> RoleLabels = new Dictionary<QuestSceneRole, string>
    {
        [QuestSceneRole.Gives] = "Quest Giver",
        [QuestSceneRole.Gates] = "Gated by",
        [QuestSceneRole.Raises] = "Raised by",
        [QuestSceneRole.LimitedTo] = "Trigger active during",
        [QuestSceneRole.Listens] = "Listened for by",
        [QuestSceneRole.Other] = "Referenced by",
        [QuestSceneRole.PrefabOverride] = "Prefab override in",
    };

    /// <summary>Lists scene and prefab uses as links that open the scene and select the object.</summary>
    public static void DrawSceneUses(List<QuestSceneUse> uses, string none)
    {
        if (uses.Count == 0)
        {
            EditorGUILayout.LabelField("   " + none, ContentUtility.WrappedMiniLabel);
            return;
        }

        foreach (QuestSceneUse use in uses)
        {
            if (GUILayout.Button($"   {RoleLabels[use.Role]}: {use.Describe()}", EditorStyles.linkLabel))
            {
                QuestSceneIndex.Open(use);
                GUIUtility.ExitGUI();
            }
        }
    }

    /// <summary>Sets up a new quest draft to come after the given one, in the same chain.</summary>
    public static void MakeFollow(ScriptableObject draft, QuestDefinition quest)
    {
        var data = new SerializedObject(draft);
        SerializedProperty prerequisites = data.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = quest;
        data.FindProperty("chain").stringValue = quest.Chain;

        // A follow-up usually comes from the same person and goes back to them. A start event is not copied:
        // the same event would start both quests.
        var source = new SerializedObject(quest);
        if (quest.Start != QuestStart.OnQuestEvent)
        {
            data.FindProperty("start").enumValueIndex = source.FindProperty("start").enumValueIndex;
        }

        data.FindProperty("turnInToGiver").boolValue = source.FindProperty("turnInToGiver").boolValue;
        data.FindProperty("turnInTo").stringValue = source.FindProperty("turnInTo").stringValue;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Selects every object in the open scenes that references the asset, including unsaved edits.</summary>
    public static void DrawSelectInOpenScenes(Object asset)
    {
        if (!GUILayout.Button("Select All Uses in Open Scenes"))
        {
            return;
        }

        List<GameObject> found = QuestSceneIndex.FindInOpenScenes(asset);
        if (found.Count > 0)
        {
            Selection.objects = found.ToArray();
            EditorGUIUtility.PingObject(found[0]);
            SceneView.FrameLastActiveSceneView();
        }

        EditorWindow.focusedWindow?.ShowNotification(new GUIContent(found.Count > 0
            ? $"Selected {found.Count} object{(found.Count == 1 ? "" : "s")}"
            : "Nothing in the open scenes uses it"));
        GUIUtility.ExitGUI();
    }

    private static string DescribeStart(QuestDefinition quest)
    {
        string after = quest.Prerequisites.Count > 0 ? "after its prerequisites are done" : "from the beginning";
        switch (quest.Start)
        {
            case QuestStart.OnQuestEvent:
                return $"When \"{(quest.StartEvent != null ? quest.StartEvent.DisplayName : "?")}\" happens, {after}.";
            case QuestStart.ByQuestGiver:
                return $"When the player talks to a Quest Giver for it, {after}. Add one with GameObject > Safe Haven > Quest Giver.";
            default:
                return $"Automatically, {after}.";
        }
    }

    private static List<QuestDefinition> Followers(QuestDefinition quest)
    {
        return ResourceCatalogTools.LoadAll<QuestDefinition>().FindAll(other => other != quest && ContainsPrerequisite(other, quest));
    }

    private static bool ContainsPrerequisite(QuestDefinition quest, QuestDefinition prerequisite)
    {
        foreach (QuestDefinition found in quest.Prerequisites)
        {
            if (found == prerequisite)
            {
                return true;
            }
        }

        return false;
    }

    // How far down its chain a quest is, so chains list in the order they are played.
    private static int Depth(QuestDefinition quest)
    {
        int depth = 0;
        var visited = new HashSet<QuestDefinition> { quest };
        QuestDefinition current = quest;
        while (current.Prerequisites.Count > 0 && current.Prerequisites[0] != null && visited.Add(current.Prerequisites[0]))
        {
            current = current.Prerequisites[0];
            depth++;
        }

        return depth;
    }
}

/// <summary>Quest Events in the Content Editor: where each is raised and used, and a button to raise it while playing.</summary>
public sealed class QuestEventSection : ContentSection
{
    public override string Title => "Quest Events";
    public override string Noun => "Quest Event";
    public override string SidebarGroup => "Quests";
    public override Type AssetType => typeof(GameEventDefinition);
    public override string Folder => "Assets/_Project/Data/Quests/Events";
    public override string Help =>
        "Named moments you define yourself: \"Reached the Old Tower\", \"Talked to the Smith\". Raise one with a Quest Event Trigger " +
        "(walking into a zone or interacting with an object; GameObject > Safe Haven has ready-made ones) or from any Inspector " +
        "event, then use it in a quest objective, to start a quest, or in a Quest Event Listener to make something happen in the scene.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var gameEvent = (GameEventDefinition)asset;
        List<QuestDefinition> quests = ResourceCatalogTools.LoadAll<QuestDefinition>();
        bool waitedFor = quests.Exists(quest => quest.StartEvent == gameEvent
            || quest.Objectives.Any(objective => objective != null && objective.QuestEvent == gameEvent));
        if (waitedFor && !QuestSceneIndex.IsRaised(gameEvent, quests))
        {
            problems.Add("A quest waits for it, but nothing raises it yet (add a Quest Event Trigger).");
        }

        return problems;
    }

    public override void DrawExtras(Object asset)
    {
        var gameEvent = (GameEventDefinition)asset;
        ContentUtility.BeginPanel("Used By", "Quests that wait for it, and saved scenes and prefabs that raise or listen for it.");
        bool any = false;
        foreach (QuestDefinition quest in ResourceCatalogTools.LoadAll<QuestDefinition>())
        {
            string use = quest.StartEvent == gameEvent ? "starts it" : quest.CompletionEvent == gameEvent ? "raised when it completes" : null;
            for (int i = 0; use == null && i < quest.Objectives.Count; i++)
            {
                if (quest.Objectives[i].Progress(new GameEvent(GameEventKind.Custom, gameEvent)) > 0)
                {
                    use = "an objective";
                }
            }

            if (use == null)
            {
                continue;
            }

            any = true;
            if (GUILayout.Button($"   Quest: {quest.DisplayName} ({use})", EditorStyles.linkLabel))
            {
                Host.Select(Host.FindSectionFor(quest), quest);
                GUIUtility.ExitGUI();
            }
        }

        if (!any)
        {
            EditorGUILayout.LabelField("   No quest uses it.", ContentUtility.WrappedMiniLabel);
        }

        EditorGUILayout.Space(4f);
        QuestSection.DrawSceneUses(QuestSceneIndex.UsesOf(gameEvent), "No scene or prefab raises or listens for it.");
        QuestSection.DrawSelectInOpenScenes(gameEvent);
        ContentUtility.EndPanel();

        if (EditorApplication.isPlaying)
        {
            ContentUtility.BeginPanel("Play Test", "The game is running.");
            if (GUILayout.Button($"Raise \"{gameEvent.DisplayName}\" Now"))
            {
                gameEvent.Raise();
                Host.Notify($"Raised {gameEvent.DisplayName}.");
            }

            ContentUtility.EndPanel();
        }
    }
}
