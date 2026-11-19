using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One line of a quest's checklist: what is in place (or missing), and what the designer can do about it.</summary>
public sealed class QuestCheck
{
    public enum State { Done, Missing, Note }

    public State Status;
    public string Text;
    public readonly List<(string label, string tip, Action fix)> Fixes = new List<(string, string, Action)>();

    public QuestCheck(State status, string text)
    {
        Status = status;
        Text = text;
    }

    public QuestCheck Fix(string label, string tip, Action fix)
    {
        Fixes.Add((label, tip, fix));
        return this;
    }
}

/// <summary>
/// What a quest needs to work in the game, as a checklist with fixes: how it starts (a giver who offers it, the deed that
/// starts it), its objectives (each person, place and thing present in a scene), how it is handed in, and what it gives.
/// Each missing piece offers what can be done about it: give the quest to a giver in the open scenes or make one, mark
/// the selected object as a place or thing, put a Quest Zone or Inspect Point at the Scene view, add a first objective.
/// Drawn at the top of the quest in the Content Editor and in the New Quest window.
/// </summary>
public static class QuestChecklist
{
    public static List<QuestCheck> For(QuestDefinition quest)
    {
        var checks = new List<QuestCheck>();
        CheckName(quest, checks);
        CheckStart(quest, checks);
        CheckObjectives(quest, checks);
        CheckFinish(quest, checks);
        CheckNamesEarly(quest, checks);
        CheckRewards(quest, checks);
        return checks;
    }

    /// <summary>Draws the checklist as a panel; returns true when a fix changed something.</summary>
    public static bool Draw(QuestDefinition quest)
    {
        List<QuestCheck> checks = For(quest);
        int missing = checks.Count(c => c.Status == QuestCheck.State.Missing);
        ContentUtility.BeginPanel(missing == 0 ? "Ready to Play  ✓" : $"Make It Work  ({missing} to do)",
            "What this quest needs in the game, and buttons that set it up. Fixes in a scene work on the open scenes; save them after.");
        bool changed = false;
        foreach (QuestCheck check in checks)
        {
            string mark = check.Status == QuestCheck.State.Done ? "✓" : check.Status == QuestCheck.State.Missing ? "✗" : "•";
            Color before = GUI.color;
            GUI.color = check.Status == QuestCheck.State.Missing ? new Color(1f, 0.62f, 0.55f) : check.Status == QuestCheck.State.Done ? new Color(0.7f, 1f, 0.72f) : before;
            EditorGUILayout.LabelField($"{mark}  {check.Text}", ContentUtility.WrappedMiniLabel);
            GUI.color = before;
            if (check.Fixes.Count == 0)
            {
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(18f);
                foreach ((string label, string tip, Action fix) in check.Fixes)
                {
                    if (GUILayout.Button(new GUIContent(label, tip), EditorStyles.miniButton))
                    {
                        fix();
                        QuestSceneIndex.Invalidate();
                        changed = true;
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        ContentUtility.EndPanel();
        return changed;
    }

    // ---- Name

    private static void CheckName(QuestDefinition quest, List<QuestCheck> checks)
    {
        bool named = !string.IsNullOrWhiteSpace(new SerializedObject(quest).FindProperty("displayName").stringValue);
        bool described = !string.IsNullOrWhiteSpace(quest.Description);
        checks.Add(named && described
            ? new QuestCheck(QuestCheck.State.Done, $"Named \"{quest.DisplayName}\", with a description for the journal.")
            : new QuestCheck(QuestCheck.State.Missing, named ? "Has no description: the journal and the giver's offer show it." : "Has no name of its own (Identity › Display Name)."));
    }

    // ---- Start

    private static void CheckStart(QuestDefinition quest, List<QuestCheck> checks)
    {
        List<QuestDefinition> all = ResourceCatalogTools.LoadAll<QuestDefinition>();
        switch (quest.Start)
        {
            case QuestStart.Automatically:
                checks.Add(new QuestCheck(QuestCheck.State.Done, quest.Prerequisites.Count > 0
                    ? $"Starts by itself once {string.Join(", ", quest.Prerequisites.Where(p => p != null).Select(p => p.DisplayName))} {(quest.Prerequisites.Count == 1 ? "is" : "are")} done."
                    : "Starts by itself when the game begins (no prerequisites)."));
                break;
            case QuestStart.ByQuestGiver:
                if (QuestSceneIndex.HasGiver(quest) || QuestSceneIndex.IsStartedByStory(quest))
                {
                    string by = string.Join(", ", QuestSceneIndex.UsesOf(quest).Where(u => u.Role == QuestSceneRole.Gives || u.Role == QuestSceneRole.Starts).Select(u => u.Describe()).Distinct());
                    ResidentDefinition resident = QuestSceneIndex.GivingResident(quest);
                    if (resident != null)
                    {
                        by = string.IsNullOrEmpty(by) ? $"{resident.DisplayName}'s quest line at home" : $"{by}, {resident.DisplayName}'s quest line at home";
                    }

                    checks.Add(new QuestCheck(QuestCheck.State.Done, $"Offered by {by}."));
                }
                else
                {
                    checks.Add(GiverFixes(quest, new QuestCheck(QuestCheck.State.Missing, "Starts from a Quest Giver, but no one offers it yet. Give it to someone:")));
                }

                break;
            case QuestStart.OnQuestEvent:
                if (quest.StartEvent == null)
                {
                    checks.Add(new QuestCheck(QuestCheck.State.Missing, "Starts on a Story Event, but none is chosen. For reaching a place or talking to someone, When Player Does needs no event:")
                        .Fix("Use When Player Does", "Start it when the player talks to someone, reaches a place, uses a thing...", () => SetStart(quest, QuestStart.WhenPlayerDoes)));
                }
                else if (!QuestSceneIndex.IsRaised(quest.StartEvent, all))
                {
                    checks.Add(EventFixes(quest.StartEvent, new QuestCheck(QuestCheck.State.Missing, $"Starts on \"{quest.StartEvent.DisplayName}\", but nothing raises it yet.")));
                }
                else
                {
                    checks.Add(new QuestCheck(QuestCheck.State.Done, $"Starts on \"{quest.StartEvent.DisplayName}\" (raised in a scene)."));
                }

                break;
            case QuestStart.WhenPlayerDoes:
                QuestObjective deed = quest.StartWhen;
                string problem = deed?.FindProblem();
                if (deed == null || problem != null)
                {
                    checks.Add(new QuestCheck(QuestCheck.State.Missing, $"Starts when the player does something, but that {problem ?? "is empty"} (Starting › Start When)."));
                }
                else
                {
                    checks.Add(TargetCheck(deed, $"Starts when the player does it: {deed.Describe()}."));
                }

                break;
        }
    }

    // ---- Objectives

    private static void CheckObjectives(QuestDefinition quest, List<QuestCheck> checks)
    {
        if (quest.Objectives.Count == 0)
        {
            var none = new QuestCheck(QuestCheck.State.Missing, "Has no objectives, so it is done the moment it starts. Add a first one:");
            foreach ((string label, ObjectiveType type) in new[]
                     {
                         ("Talk To", ObjectiveType.TalkTo), ("Reach Place", ObjectiveType.ReachPlace), ("Use Object", ObjectiveType.UseObject),
                         ("Kill", ObjectiveType.KillMonster), ("Gather", ObjectiveType.GatherResource), ("Craft", ObjectiveType.CraftItem),
                     })
            {
                none.Fix(label, $"Adds a {label} objective; choose who or what in Objectives.", () => AddObjective(quest, type));
            }

            checks.Add(none);
            return;
        }

        List<QuestDefinition> all = ResourceCatalogTools.LoadAll<QuestDefinition>();
        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            QuestObjective objective = quest.Objectives[i];
            if (objective == null)
            {
                continue;
            }

            string line = $"Objective {i + 1}: {objective.Describe()}";
            string problem = objective.FindProblem();
            if (problem != null)
            {
                checks.Add(new QuestCheck(QuestCheck.State.Missing, $"{line} — {problem} (fill it in under Objectives)."));
            }
            else if (objective.Type == ObjectiveType.QuestEvent && !QuestSceneIndex.IsRaised(objective.QuestEvent, all))
            {
                checks.Add(EventFixes(objective.QuestEvent, new QuestCheck(QuestCheck.State.Missing, $"{line} — nothing raises \"{objective.QuestEvent.DisplayName}\" yet.")));
            }
            else if (AskedBefore(quest, objective, all) is QuestDefinition earlier)
            {
                int index = i;
                checks.Add(new QuestCheck(QuestCheck.State.Missing, $"{line} — counts what was done before the quest began (Count Already Owned), and {earlier.DisplayName} " +
                        "already asks it, so it is done the moment this quest starts. For a conversation, raise a Story Event at its end instead.")
                    .Fix("Count Only After It Starts", "Turns off Count Already Owned for this objective.", () => SetCountOwned(quest, index, false)));
            }
            else if (HomeOnlyIn(quest, objective, all) is QuestDefinition rescue)
            {
                checks.Add(new QuestCheck(QuestCheck.State.Missing, $"{line} — {objective.Person.DisplayName} only comes home in {rescue.DisplayName}, " +
                    "which doesn't come before this quest, so they may not be there. Add it (or a quest after it) to Prerequisites."));
            }
            else if (objective.Type == ObjectiveType.BuildStructure && objective.Building != null && !CanBeLearned(objective.Building))
            {
                checks.Add(new QuestCheck(QuestCheck.State.Missing, $"{line} — {objective.Building.DisplayName} isn't known from the start, and no job or item teaches it."));
            }
            else if (objective.Type == ObjectiveType.CraftItem && objective.Item != null && !ResourceCatalogTools.LoadAll<RecipeDefinition>().Exists(r => r != null && r.Output == objective.Item))
            {
                checks.Add(new QuestCheck(QuestCheck.State.Missing, $"{line} — no recipe makes {objective.Item.DisplayName}."));
            }
            else if (objective.DeedKind.HasValue)
            {
                checks.Add(TargetCheck(objective, line + "."));
            }
            else
            {
                checks.Add(new QuestCheck(QuestCheck.State.Done, line + "."));
            }
        }
    }

    // Every quest that comes before this one (its prerequisites and theirs, and what must have started first).
    private static HashSet<QuestDefinition> Before(QuestDefinition quest)
    {
        var before = new HashSet<QuestDefinition>();
        var open = new Stack<QuestDefinition>(quest.Prerequisites.Concat(quest.StartedFirst));
        while (open.Count > 0)
        {
            QuestDefinition next = open.Pop();
            if (next != null && before.Add(next))
            {
                foreach (QuestDefinition earlier in next.Prerequisites.Concat(next.StartedFirst)) open.Push(earlier);
            }
        }

        return before;
    }

    // A Talk To, Reach or Use that counts what was done before the quest began, which an earlier quest already asks: the
    // Road North counted every talk with the portal since the first morning (2026-11-11). The earlier quest, or null.
    private static QuestDefinition AskedBefore(QuestDefinition quest, QuestObjective objective, List<QuestDefinition> all)
    {
        if (!objective.CountAlreadyOwned || objective.DeedTarget == null
            || objective.Type != ObjectiveType.TalkTo && objective.Type != ObjectiveType.ReachPlace && objective.Type != ObjectiveType.UseObject)
        {
            return null;
        }

        return Before(quest).FirstOrDefault(earlier => earlier.Objectives.Any(o => o != null && o.Type == objective.Type && o.DeedTarget == objective.DeedTarget)
            || earlier.Start == QuestStart.WhenPlayerDoes && earlier.StartWhen != null && earlier.StartWhen.Type == objective.Type && earlier.StartWhen.DeedTarget == objective.DeedTarget);
    }

    // Talking to or giving work to someone who only comes home in a rescue that isn't before this quest: the rescue, or null.
    private static QuestDefinition HomeOnlyIn(QuestDefinition quest, QuestObjective objective, List<QuestDefinition> all)
    {
        if (objective.Person == null || objective.Type != ObjectiveType.TalkTo && objective.Type != ObjectiveType.AssignJob)
        {
            return null;
        }

        QuestDefinition rescue = all.FirstOrDefault(q => q != null && q.Objectives.Any(o => o != null && o.Type == ObjectiveType.RescuePerson && o.Person == objective.Person));
        return rescue == null || rescue == quest || Before(quest).Contains(rescue) ? null : rescue;
    }

    // Known from the start, or taught by a job or an item (blueprints).
    private static bool CanBeLearned(BuildingDefinition building) =>
        building.KnownFromStart
        || ResourceCatalogTools.LoadAll<JobDefinition>().Exists(job => job != null && job.TeachesBuildings.Contains(building))
        || ResourceCatalogTools.LoadAll<ItemDefinition>().Exists(item => item != null && item.TeachesBuildings.Contains(building));

    private static void SetCountOwned(QuestDefinition quest, int objective, bool count)
    {
        var so = new SerializedObject(quest);
        so.FindProperty("objectives").GetArrayElementAtIndex(objective).FindPropertyRelative("countAlreadyOwned").boolValue = count;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(quest);
    }

    // Where the Haven's place layer stands a target: reached at a place (its Targets), used as a thing left there (its Finds).
    private static string HavenPlaceStands(QuestTargetDefinition thing, ObjectiveType type)
    {
        HavenPlaceVerbs verbs = HavenPlaceVerbs.Current;
        if (verbs == null)
        {
            return null;
        }

        if (type == ObjectiveType.ReachPlace)
        {
            foreach (HavenPlaceVerbs.PlaceTarget entry in verbs.Targets)
            {
                if (entry != null && entry.target == thing)
                {
                    return "the place " + entry.placeId;
                }
            }
        }

        if (type == ObjectiveType.UseObject)
        {
            foreach (HavenPlaceVerbs.Find find in verbs.Finds)
            {
                if (find != null && find.questTarget == thing)
                {
                    return $"{find.name} at {find.placeId}";
                }
            }
        }

        return null;
    }

    // A deed's person, place or thing: is anything in a scene it? If not, what can be put there.
    private static QuestCheck TargetCheck(QuestObjective deed, string line)
    {
        Object target = deed.DeedTarget;
        if (target == null)
        {
            return new QuestCheck(QuestCheck.State.Done, line);
        }

        List<QuestSceneUse> marked = QuestSceneIndex.MarkedAs(target).FindAll(use => Fits(deed.Type, use.Component));
        if (marked.Count > 0)
        {
            return new QuestCheck(QuestCheck.State.Done, $"{line} In the game: {string.Join(", ", marked.Select(m => m.Describe()).Distinct().Take(3))}.");
        }

        if (target is ResidentDefinition person)
        {
            // Residents are made in the Haven from the settlement; travellers are found in raids.
            return new QuestCheck(QuestCheck.State.Note, deed.Type == ObjectiveType.RescuePerson
                ? $"{line} {person.DisplayName} is found as a stranded traveller in a raid (a rumour or raid event places them)."
                : $"{line} {person.DisplayName} can be talked to in the Haven once they live there; no scene object is them yet.");
        }

        var thing = (QuestTargetDefinition)target;
        // A Haven place or a thing left at one (Resources/HavenPlaceVerbs) is stood at runtime at its atlas place: no scene object.
        string stood = HavenPlaceStands(thing, deed.Type);
        if (stood != null)
        {
            return new QuestCheck(QuestCheck.State.Done, $"{line} In the game: {stood} (Haven Place Verbs).");
        }

        var check = new QuestCheck(QuestCheck.State.Missing, $"{line} Nothing in a scene is {thing.DisplayName} yet:");
        switch (deed.Type)
        {
            case ObjectiveType.ReachPlace:
                check.Fix("Create Quest Zone Here", "A zone at the Scene view's centre; walking into it counts as reaching the place.", () => Select(QuestTargetTools.CreateZone(thing)));
                break;
            case ObjectiveType.UseObject:
                check.Fix("Create Inspect Point Here", "An object at the Scene view's centre; using it counts.", () => Select(QuestTargetTools.CreateInspectPoint(thing)));
                break;
        }

        check.Fix("Make Selected Object It", deed.Type == ObjectiveType.TalkTo ? "The selected Talkable counts as it when talked to."
                : "The selected object's trigger counts as it (one is added if it has none).",
            () => Notify(QuestTargetTools.MarkSelected(thing, deed.Type == ObjectiveType.TalkTo)));
        return check;
    }

    // A person is talked to through someone talkable, found as a stranded traveller, reached or used through a trigger.
    private static bool Fits(ObjectiveType deed, Type component)
    {
        switch (deed)
        {
            case ObjectiveType.TalkTo:
                return component != typeof(GameEventTrigger) && component != typeof(RescuableTraveler);
            case ObjectiveType.RescuePerson:
                return component == typeof(RescuableTraveler);
            case ObjectiveType.ReachPlace:
                return component == typeof(GameEventTrigger);
            case ObjectiveType.UseObject:
                // A carved stone raises Use itself when read (it stands in raid set pieces and event sites, as a prefab).
                return component == typeof(GameEventTrigger) || component == typeof(CarvedStone);
            default:
                return true;
        }
    }

    // ---- Finishing and rewards

    private static void CheckFinish(QuestDefinition quest, List<QuestCheck> checks)
    {
        if (!quest.TurnInToGiver)
        {
            checks.Add(new QuestCheck(QuestCheck.State.Done, "Finishes the moment its objectives are done."));
        }
        else if (QuestSceneIndex.HasGiver(quest))
        {
            checks.Add(new QuestCheck(QuestCheck.State.Done, $"Handed in to {quest.TurnInTo}."));
        }
        else
        {
            checks.Add(GiverFixes(quest, new QuestCheck(QuestCheck.State.Missing, "Is handed in to a Quest Giver, but no one has it, so it can never finish. Give it to someone:")));
        }
    }

    // People the quest's words name who only come home later: often fine ("Sigrid's voice came through the stones"), but a line
    // that has them at home before their rescue reads wrong ("six of your people" was, 2026-11-11). A note, not a fault.
    private static void CheckNamesEarly(QuestDefinition quest, List<QuestCheck> checks)
    {
        if (quest.Chain != null && quest.Chain.StartsWith("Prologue", System.StringComparison.Ordinal))
        {
            return;
        }

        List<QuestDefinition> all = ResourceCatalogTools.LoadAll<QuestDefinition>();
        HashSet<QuestDefinition> before = Before(quest);
        string words = quest.Description + " " + quest.JournalNotes + " " + string.Join(" ", quest.Objectives.Where(o => o != null).Select(o => o.Describe()));
        foreach (QuestDefinition rescue in all.Where(q => q != null && q != quest && !before.Contains(q)))
        {
            foreach (ResidentDefinition person in rescue.Objectives.Where(o => o != null && o.Type == ObjectiveType.RescuePerson && o.Person != null).Select(o => o.Person).Distinct())
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(words, @"\b" + System.Text.RegularExpressions.Regex.Escape(person.DisplayName) + @"\b"))
                {
                    checks.Add(new QuestCheck(QuestCheck.State.Note, $"Names {person.DisplayName}, who only comes home in {rescue.DisplayName}, which needn't come first: check the words don't have them home already."));
                }
            }
        }
    }

    private static void CheckRewards(QuestDefinition quest, List<QuestCheck> checks)
    {
        string rewards = quest.DescribeRewards();
        checks.Add(rewards.Length > 0
            ? new QuestCheck(QuestCheck.State.Done, $"Rewards: {rewards}.")
            : new QuestCheck(QuestCheck.State.Note, quest.Rank == QuestRank.Haven
                ? "Gives no rewards: a Haven quest's pay is the job or building it sets up (GDD 17.1)."
                : "Gives no rewards (fine for a story step; add some under Rewards otherwise)."));
    }

    // ---- Fixes

    // The quest givers and people in the open scenes, each a button; and a new giver at the Scene view.
    private static QuestCheck GiverFixes(QuestDefinition quest, QuestCheck check)
    {
        foreach (GameObject candidate in GiverCandidates().Take(6))
        {
            GameObject who = candidate;
            check.Fix(who.name, "Gives this quest to them (their Quest Giver; one is added to a person who has none).", () => GiveTo(quest, who));
        }

        check.Fix("Selected Object", "Gives this quest to the object selected in the Hierarchy.", () =>
        {
            if (Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject))
            {
                GiveTo(quest, Selection.activeGameObject);
            }
            else
            {
                Notify("Select someone in a scene first.");
            }
        });
        check.Fix("+ New Quest Giver Here", "A new Quest Giver at the Scene view's centre, offering this quest.", () => GiveTo(quest, QuestObjectPresets.CreateQuestGiver()));
        return check;
    }

    // A Story Event nobody raises: a zone raising it at the Scene view.
    private static QuestCheck EventFixes(GameEventDefinition gameEvent, QuestCheck check)
    {
        return check.Fix("Create Quest Zone Raising It", "A zone at the Scene view's centre that raises this event.", () =>
        {
            GameObject zone = QuestObjectPresets.CreateZone();
            var data = new SerializedObject(zone.GetComponent<GameEventTrigger>());
            data.FindProperty("gameEvent").objectReferenceValue = gameEvent;
            data.ApplyModifiedProperties();
            zone.name = $"Quest Zone - {gameEvent.DisplayName}";
        });
    }

    /// <summary>Quest givers and people (anyone a quest could come from) in the open scenes, givers first.</summary>
    public static List<GameObject> GiverCandidates()
    {
        var givers = new List<GameObject>();
        var people = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                givers.AddRange(root.GetComponentsInChildren<QuestGiver>(true).Select(g => g.gameObject));
                people.AddRange(root.GetComponentsInChildren<Talkable>(true).Where(t => t.GetComponent<IQuestPerson>() != null).Select(t => t.gameObject));
            }
        }

        return givers.Concat(people).Distinct().ToList();
    }

    /// <summary>Makes <paramref name="who"/> offer the quest: its single Quest when free, otherwise an ordered offer.</summary>
    public static void GiveTo(QuestDefinition quest, GameObject who)
    {
        if (!who.TryGetComponent(out QuestGiver giver))
        {
            giver = Undo.AddComponent<QuestGiver>(who);
        }

        var data = new SerializedObject(giver);
        SerializedProperty single = data.FindProperty("quest");
        SerializedProperty offers = data.FindProperty("offers");
        if (single.objectReferenceValue == null && offers.arraySize == 0)
        {
            single.objectReferenceValue = quest;
        }
        else if (single.objectReferenceValue != quest)
        {
            offers.arraySize++;
            offers.GetArrayElementAtIndex(offers.arraySize - 1).FindPropertyRelative("quest").objectReferenceValue = quest;
        }

        data.ApplyModifiedProperties();
        if (who.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(who.scene);
        }

        Select(who);
        Notify($"{who.name} now offers \"{quest.DisplayName}\". Write their lines on the Quest Giver, then save the scene.");
    }

    private static void SetStart(QuestDefinition quest, QuestStart start)
    {
        var data = new SerializedObject(quest);
        data.FindProperty("start").enumValueIndex = (int)start;
        data.ApplyModifiedProperties();
    }

    public static void AddObjective(QuestDefinition quest, ObjectiveType type)
    {
        var data = new SerializedObject(quest);
        SerializedProperty objectives = data.FindProperty("objectives");
        objectives.arraySize++;
        SerializedProperty added = objectives.GetArrayElementAtIndex(objectives.arraySize - 1);
        added.boxedValue = QuestObjective.Create(type, 1);
        added.isExpanded = true;
        data.ApplyModifiedProperties();
    }

    private static void Select(GameObject created)
    {
        if (created == null)
        {
            return;
        }

        Selection.activeGameObject = created;
        EditorGUIUtility.PingObject(created);
        SceneView.FrameLastActiveSceneView();
    }

    private static void Notify(string message)
    {
        EditorWindow window = EditorWindow.focusedWindow;
        if (window != null)
        {
            window.ShowNotification(new GUIContent(message));
        }

        Debug.Log("Quest checklist: " + message);
    }
}
