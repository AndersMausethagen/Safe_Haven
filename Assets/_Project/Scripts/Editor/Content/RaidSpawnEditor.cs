using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What the raid editor knows about a raid's scene: whether it is loaded, its Spawn Groups by name (the places spawn
/// lists can fill) and the player start the entry bands measure from.
/// </summary>
internal sealed class RaidSceneContext
{
    public string ScenePath;
    public Scene Scene;
    public bool Loaded;
    public Vector3? Entry;
    public readonly SortedDictionary<string, SpawnGroup> Groups = new SortedDictionary<string, SpawnGroup>();

    public static RaidSceneContext For(RaidDefinition raid, string scenePath)
    {
        var context = new RaidSceneContext { ScenePath = scenePath };
        if (string.IsNullOrEmpty(scenePath))
        {
            return context;
        }

        context.Scene = SceneManager.GetSceneByPath(scenePath);
        context.Loaded = context.Scene.IsValid() && context.Scene.isLoaded;
        if (!context.Loaded)
        {
            return context;
        }

        foreach (GameObject root in context.Scene.GetRootGameObjects())
        {
            RaidMap map = root.GetComponentInChildren<RaidMap>(true);
            if (map != null && map.PlayerStart != null)
            {
                context.Entry = map.PlayerStart.position;
            }

            // Groups inside spawned prefabs (event sites) aren't in the scene until a raid places them.
            foreach (SpawnGroup group in root.GetComponentsInChildren<SpawnGroup>(false))
            {
                if (!context.Groups.ContainsKey(group.name))
                {
                    context.Groups.Add(group.name, group);
                }
            }
        }

        return context;
    }

    /// <summary>A list's points in its entry band, over the named groups that exist.</summary>
    public List<Transform> PointsOf(RaidSpawnList list, SpawnRules rules)
    {
        var points = new List<Transform>();
        foreach (string name in list.pointGroups)
        {
            if (name != null && Groups.TryGetValue(name, out SpawnGroup group))
            {
                points.AddRange(group.PointsFor(rules, list.entryBand));
            }
        }

        return points;
    }

    /// <summary>
    /// How many points a group has on a generated map: its share of the sites of its kind, each making the map's points per site.
    /// Null when the raid plays its painted map or the group isn't placed by a kind of site, where the scene's points are the real ones.
    /// </summary>
    public int? GeneratedPoints(RaidDefinition raid, string groupName)
    {
        RaidLayoutRules rules = raid.LayoutRules;
        var map = RaidWorldEditor.FromScene<RaidMapDefinition>(ScenePath);
        if (rules == null || map == null || !Groups.TryGetValue(groupName, out SpawnGroup group))
        {
            return null;
        }

        RaidSiteKind kind = KindOf(rules, group);
        if (kind == RaidSiteKind.None)
        {
            return null;
        }

        int sites = 0;
        foreach (RaidSiteQuota quota in rules.Sites)
        {
            sites += quota != null && quota.kind == kind ? quota.count : 0;
        }

        // Whole sites go to the groups of a kind in turn, by name, as RaidMapGenerator.PlaceGroups gives them out.
        var sharing = new List<string>();
        foreach (KeyValuePair<string, SpawnGroup> other in Groups)
        {
            if (KindOf(rules, other.Value) == kind)
            {
                sharing.Add(other.Key);
            }
        }

        sharing.Sort(string.CompareOrdinal);
        int own = sharing.IndexOf(groupName);
        int owned = 0;
        foreach (int owner in RaidMapMarkers.ShareOut(sites, sharing.Count))
        {
            owned += owner == own ? 1 : 0;
        }

        return owned * map.RuleFor(kind).pointsPerSite;
    }

    private static RaidSiteKind KindOf(RaidLayoutRules rules, SpawnGroup group)
    {
        RaidSiteKind kind = group.Site;
        foreach (RaidGroupRule rule in rules.Groups)
        {
            if (rule != null && rule.group == group.name)
            {
                kind = rule.site;
            }
        }

        return kind;
    }

    /// <summary>What a list has to fill: the generated map's points where it has them, else the scene's, in its entry band.</summary>
    public int PointCount(RaidDefinition raid, RaidSpawnList list, SpawnRules rules)
    {
        int count = 0;
        foreach (string name in list.pointGroups)
        {
            if (name == null)
            {
                continue;
            }

            int? generated = GeneratedPoints(raid, name);
            if (generated.HasValue)
            {
                count += generated.Value;
            }
            else if (Groups.TryGetValue(name, out SpawnGroup group))
            {
                count += group.PointsFor(rules, list.entryBand).Count;
            }
        }

        return count;
    }

    /// <summary>The list that already uses a group, so each group serves one list (as in play).</summary>
    public static RaidSpawnList OwnerOf(RaidDefinition raid, string group)
    {
        foreach (RaidSpawnList list in raid.SpawnLists)
        {
            if (list != null && list.pointGroups.Contains(group))
            {
                return list;
            }
        }

        return null;
    }

    /// <summary>Load / unload buttons; returns true when the scene is loaded.</summary>
    public bool DrawSceneBar(string why)
    {
        if (string.IsNullOrEmpty(ScenePath))
        {
            EditorGUILayout.HelpBox("Pick the raid's scene on the Overview tab first.", MessageType.Info);
            return false;
        }

        if (!Loaded)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"The raid scene isn't open. Load it to {why}.", ContentUtility.WrappedMiniLabel);
                if (GUILayout.Button("Load Raid Scene", GUILayout.Width(130f)))
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                }
            }

            return false;
        }

        if (SceneManager.sceneCount > 1 && SceneManager.GetActiveScene() != Scene)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Scene.isDirty ? "Save And Unload Raid Scene" : "Unload Raid Scene", EditorStyles.miniButton, GUILayout.Width(170f))
                    && (!Scene.isDirty || EditorSceneManager.SaveScene(Scene)))
                {
                    EditorSceneManager.CloseScene(Scene, true);
                    GUIUtility.ExitGUI();
                }
            }
        }

        return true;
    }
}

/// <summary>
/// The Enemies, Resources, Loot &amp; Events and Escalation tabs of a raid in the Content Editor: the raid's spawn lists
/// of that kind as cards. Each card says where it spawns (the scene's Spawn Groups), how many and what, with each entry's
/// share in percent; changing one share rescales the others so the list always adds up to 100%.
/// </summary>
internal static class RaidSpawnListEditor
{
    private const float ShareWidth = 64f;
    private const float CountWidth = 44f;
    private const float RemoveWidth = 22f;
    private const float IconSize = 20f;
    private const float TierWidth = 32f;

    private static readonly HashSet<string> Collapsed = new HashSet<string>();

    public static string Describe(SpawnCategory category)
    {
        switch (category)
        {
            case SpawnCategory.Enemies:
                return "Creatures placed while the raid loads. Each list picks its count among the points of its spawn groups, " +
                       "then picks what goes there by share. Enemy density (Rules tab) scales enemy counts.";
            case SpawnCategory.Resources:
                return "Ore, timber, herbs and other things to gather. Shares say how much of the total each kind is; Max caps " +
                       "one kind per raid and Min guarantees some (for quest items).";
            case SpawnCategory.LootAndEvents:
                return "Supply caches, event sites and other loot spots.";
            default:
                return "Creatures that arrive later: when a danger stage begins (stages are set at the top of this tab) or " +
                       "when an alarm goes off, such as an extraction point's. They can hunt the player straight away.";
        }
    }

    public static void Draw(RaidDefinition raid, SpawnCategory category, RaidSceneContext scene)
    {
        if (raid.EditorSpawnLists == null)
        {
            return;
        }

        scene.DrawSceneBar("pick spawn groups and see how many points each list has");
        SpawnRules rules = raid.GetSpawnRules(scene.Entry);
        List<RaidSpawnList> lists = raid.EditorSpawnLists;

        // Recorded before any change this event so edits, adds and removes undo in one step.
        if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
        {
            Undo.RecordObject(raid, "Edit Raid Spawns");
        }

        EditorGUI.BeginChangeCheck();
        int shown = 0;
        for (int i = 0; i < lists.Count; i++)
        {
            if (lists[i] != null && lists[i].category == category)
            {
                shown++;
                if (DrawList(raid, lists, i, scene, rules))
                {
                    break;
                }
            }
        }

        if (shown == 0)
        {
            EditorGUILayout.HelpBox($"No {Label(category).ToLowerInvariant()} spawn lists yet, so nothing of this kind spawns " +
                                    "(except from scene groups no list names). Add one below.", MessageType.Info);
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button($"+ Add {Label(category)} List", GUILayout.Height(24f)))
        {
            lists.Add(new RaidSpawnList
            {
                name = $"New {Label(category).ToLowerInvariant()} list",
                category = category,
                trigger = category == SpawnCategory.Escalation ? SpawnTrigger.Stage : SpawnTrigger.RaidStart,
                huntPlayer = category == SpawnCategory.Escalation,
                count = category == SpawnCategory.Escalation ? new Vector2Int(2, 2) : new Vector2Int(3, 5),
            });
            GUI.changed = true;
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(raid);
        }
    }

    public static string Label(SpawnCategory category) => category == SpawnCategory.LootAndEvents ? "Loot & Events" : category.ToString();

    // Returns true when the lists changed shape (removed, moved) so the caller stops iterating.
    private static bool DrawList(RaidDefinition raid, List<RaidSpawnList> lists, int index, RaidSceneContext scene, SpawnRules rules)
    {
        RaidSpawnList list = lists[index];
        string key = AssetDatabase.GetAssetPath(raid) + ":" + index;
        bool open = !Collapsed.Contains(key);
        Vector2Int scaled = list.CountFor(rules);
        int points = scene.Loaded ? scene.PointCount(raid, list, rules) : -1;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool nowOpen = EditorGUILayout.Foldout(open, GUIContent.none, true, EditorStyles.foldout);
                if (nowOpen != open)
                {
                    if (nowOpen)
                    {
                        Collapsed.Remove(key);
                    }
                    else
                    {
                        Collapsed.Add(key);
                    }
                }

                GUILayout.Space(-36f);
                list.name = EditorGUILayout.TextField(list.name, EditorStyles.boldLabel, GUILayout.MinWidth(120f));
                string summary = $"{Range(scaled)} per raid" + (points >= 0 ? $"  ·  {points} points" : "") + $"  ·  {list.entries.Count} kinds";
                if (list.trigger != SpawnTrigger.RaidStart)
                {
                    summary = (list.trigger == SpawnTrigger.Stage ? $"stage {list.stage}" : "alarm") + "  ·  " + summary;
                }

                GUILayout.Label(summary, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                if (GUILayout.Button(new GUIContent("⋯", "Duplicate, move to another tab or delete"), EditorStyles.miniButton, GUILayout.Width(24f)))
                {
                    ShowListMenu(raid, lists, index);
                }
            }

            if (!open)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(list.name))
            {
                EditorGUILayout.HelpBox("Give the list a name; it also seeds the list's roll.", MessageType.Warning);
            }

            DrawWhere(raid, list, scene, rules);
            DrawHowMany(list, rules, scaled, points);
            DrawWhat(list);
        }

        return false;
    }

    private static void ShowListMenu(RaidDefinition raid, List<RaidSpawnList> lists, int index)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Duplicate"), false, () => Change(raid, "Duplicate Spawn List", () =>
        {
            RaidSpawnList copy = JsonUtility.FromJson<RaidSpawnList>(JsonUtility.ToJson(lists[index]));
            copy.name += " (copy)";
            copy.pointGroups.Clear();
            lists.Insert(index + 1, copy);
        }));
        foreach (SpawnCategory category in System.Enum.GetValues(typeof(SpawnCategory)))
        {
            SpawnCategory target = category;
            if (target != lists[index].category)
            {
                menu.AddItem(new GUIContent("Move to tab/" + Label(target)), false,
                    () => Change(raid, "Move Spawn List", () => lists[index].category = target));
            }
        }

        if (index > 0)
        {
            menu.AddItem(new GUIContent("Move up"), false, () => Change(raid, "Reorder Spawn Lists", () =>
            {
                (lists[index - 1], lists[index]) = (lists[index], lists[index - 1]);
            }));
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Delete spawn list", $"Delete '{lists[index].name}'?", "Delete", "Cancel"))
            {
                Change(raid, "Delete Spawn List", () => lists.RemoveAt(index));
            }
        });
        menu.ShowAsContext();
    }

    private static void Change(RaidDefinition raid, string undo, System.Action change)
    {
        Undo.RecordObject(raid, undo);
        change();
        EditorUtility.SetDirty(raid);
    }

    private static void DrawWhere(RaidDefinition raid, RaidSpawnList list, RaidSceneContext scene, SpawnRules rules)
    {
        EditorGUILayout.LabelField(new GUIContent("Where", "The Spawn Groups (under 'Raid Layout' in the raid scene) whose points this list fills."),
            EditorStyles.miniBoldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < list.pointGroups.Count; i++)
            {
                string name = list.pointGroups[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    string info;
                    int? generated = name != null && scene.Loaded ? scene.GeneratedPoints(raid, name) : null;
                    if (!scene.Loaded)
                    {
                        info = "";
                    }
                    else if (generated.HasValue)
                    {
                        info = $"{generated.Value} points on a generated map";
                    }
                    else if (name != null && scene.Groups.TryGetValue(name, out SpawnGroup group))
                    {
                        info = $"{group.PointsFor(rules, list.entryBand).Count} of {group.CollectPoints().Count} points";
                    }
                    else
                    {
                        info = "not in the scene!";
                    }

                    EditorGUILayout.LabelField(name, info);
                    if (scene.Loaded && name != null && scene.Groups.TryGetValue(name, out SpawnGroup found)
                        && GUILayout.Button(new GUIContent("Show", "Select the group in the scene"), EditorStyles.miniButton, GUILayout.Width(44f)))
                    {
                        Selection.activeGameObject = found.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }

                    if (GUILayout.Button(new GUIContent("×", "Stop using this group"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)))
                    {
                        list.pointGroups.RemoveAt(i);
                        GUI.changed = true;
                        GUIUtility.ExitGUI();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (scene.Loaded)
                {
                    if (GUILayout.Button("+ Add Spawn Group", EditorStyles.miniButton, GUILayout.Width(130f)))
                    {
                        ShowGroupMenu(raid, list, scene);
                    }
                }
                else
                {
                    GUILayout.Label("Load the raid scene to pick spawn groups.", EditorStyles.miniLabel);
                }
            }

            list.entryBand = (EntryBand)EditorGUILayout.EnumPopup(new GUIContent("Distance from start",
                "Only uses points this far from the player start. The Near/Far distances are on the Rules tab."), list.entryBand);
            list.separation = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Keep apart (m)",
                "Chosen points are at least this far apart, useful when one spawn is a whole pack. 0 = no limit."), list.separation));
        }
    }

    private static void ShowGroupMenu(RaidDefinition raid, RaidSpawnList list, RaidSceneContext scene)
    {
        var menu = new GenericMenu();
        foreach (KeyValuePair<string, SpawnGroup> pair in scene.Groups)
        {
            string name = pair.Key;
            RaidSpawnList owner = RaidSceneContext.OwnerOf(raid, name);
            string label = $"{name}   ({pair.Value.CollectPoints().Count} points)";
            if (owner != null)
            {
                menu.AddDisabledItem(new GUIContent(owner == list ? label : $"{label} - used by '{owner.name}'"));
            }
            else
            {
                menu.AddItem(new GUIContent(label), false, () => Change(raid, "Add Spawn Group", () => list.pointGroups.Add(name)));
            }
        }

        if (scene.Groups.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("The scene has no Spawn Groups"));
        }

        menu.ShowAsContext();
    }

    private static void DrawHowMany(RaidSpawnList list, SpawnRules rules, Vector2Int scaled, int points)
    {
        EditorGUILayout.LabelField("How many", EditorStyles.miniBoldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Per raid (min - max)", "How many points are filled each raid, among all the groups' points."));
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                int min = Mathf.Max(0, EditorGUILayout.IntField(list.count.x, GUILayout.Width(CountWidth)));
                GUILayout.Label("to", GUILayout.Width(16f));
                int max = Mathf.Max(min, EditorGUILayout.IntField(list.count.y, GUILayout.Width(CountWidth)));
                list.count = new Vector2Int(min, max);
                if (list.SpawnsEnemies && !Mathf.Approximately(rules.EnemyDensity, 1f))
                {
                    GUILayout.Label($"x{rules.EnemyDensity:0.##} density = {Range(scaled)}", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel = indent;
            }

            if (list.category == SpawnCategory.Escalation || list.trigger != SpawnTrigger.RaidStart)
            {
                list.trigger = (SpawnTrigger)EditorGUILayout.EnumPopup(new GUIContent("When",
                    "RaidStart: while the raid loads. Stage: when that danger stage begins. Alarm: when something sets off one " +
                    "of its spawn groups, such as an extraction point's alarm."), list.trigger);
                if (list.trigger == SpawnTrigger.Stage)
                {
                    list.stage = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Stage", "1 is the first danger stage."), list.stage));
                }

                list.huntPlayer = EditorGUILayout.Toggle(new GUIContent("Hunt the player",
                    "Creatures from this list head straight for the player and never give up the chase."), list.huntPlayer);
            }

            if (points >= 0 && scaled.y > points)
            {
                EditorGUILayout.HelpBox($"Wants up to {scaled.y} but its groups only have {points} points at this distance, so it " +
                                        $"places at most {points}. Add groups or points, or widen the distance.", MessageType.Warning);
            }
        }
    }

    private static void DrawWhat(RaidSpawnList list)
    {
        EditorGUILayout.LabelField("What", EditorStyles.miniBoldLabel);
        List<SpawnTable.Entry> entries = list.entries;
        DrawShareBar(entries);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(15f + IconSize + 3f);
            GUILayout.Label("Prefab", EditorStyles.miniLabel);
            GUILayout.Label(new GUIContent("Share %", "Share of each pick over all tiers. Changing one rescales the others so the list adds up to 100%. Rows that don't appear at a tier drop out, and those left share their picks."),
                EditorStyles.miniLabel, GUILayout.Width(ShareWidth));
            GUILayout.Label(new GUIContent("Min", "At least this many every raid, placed before the shared picks."), EditorStyles.miniLabel, GUILayout.Width(CountWidth));
            GUILayout.Label(new GUIContent("Max", "At most this many per raid. 0 = no limit."), EditorStyles.miniLabel, GUILayout.Width(CountWidth));
            GUILayout.Label(new GUIContent("From", "The lowest raid tier this appears at. 0 = any."), EditorStyles.miniLabel, GUILayout.Width(TierWidth));
            GUILayout.Label(new GUIContent("To", "The highest raid tier this appears at. 0 = any."), EditorStyles.miniLabel, GUILayout.Width(TierWidth));
            GUILayout.Space(RemoveWidth + 4f);
        }

        float total = RaidSpawnList.TotalWeight(entries);
        for (int i = 0; i < entries.Count; i++)
        {
            SpawnTable.Entry entry = entries[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15f);
                DrawIcon(entry.prefab, ShareColour(i));
                entry.prefab = (GameObject)EditorGUILayout.ObjectField(entry.prefab, typeof(GameObject), false);
                float share = total > 0f && entry.prefab != null ? entry.weight / total * 100f : 0f;
                EditorGUI.BeginChangeCheck();
                float newShare = EditorGUILayout.FloatField((float)System.Math.Round(share, 1), GUILayout.Width(ShareWidth));
                if (EditorGUI.EndChangeCheck())
                {
                    RaidSpawnList.SetShare(entries, i, newShare);
                }

                entry.minPerGroup = Mathf.Max(0, EditorGUILayout.IntField(entry.minPerGroup, GUILayout.Width(CountWidth)));
                entry.maxPerGroup = Mathf.Max(0, EditorGUILayout.IntField(entry.maxPerGroup, GUILayout.Width(CountWidth)));
                entry.fromTier = Mathf.Clamp(EditorGUILayout.IntField(entry.fromTier, GUILayout.Width(TierWidth)), 0, Tiers.Max);
                entry.toTier = Mathf.Clamp(EditorGUILayout.IntField(entry.toTier, GUILayout.Width(TierWidth)), 0, Tiers.Max);
                if (GUILayout.Button(new GUIContent("×", "Remove"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)))
                {
                    entries.RemoveAt(i);
                    RaidSpawnList.Normalize(entries);
                    GUI.changed = true;
                    GUIUtility.ExitGUI();
                }
            }

            if (entry.prefab == null)
            {
                EditorGUILayout.LabelField(" ", "Pick a prefab; empty entries never spawn.", EditorStyles.miniLabel);
            }
        }

        DropPrefabs(entries);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(15f);
            if (GUILayout.Button("+ Add Entry", EditorStyles.miniButton, GUILayout.Width(90f)))
            {
                entries.Add(new SpawnTable.Entry { weight = 0f });
                RaidSpawnList.SetShare(entries, entries.Count - 1, 100f / entries.Count);
                GUI.changed = true;
            }

            if (entries.Count > 1 && GUILayout.Button(new GUIContent("Even Split", "Give every entry the same share."), EditorStyles.miniButton, GUILayout.Width(80f)))
            {
                foreach (SpawnTable.Entry entry in entries)
                {
                    entry.weight = 1f;
                }

                RaidSpawnList.Normalize(entries);
                GUI.changed = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(entries.Count == 0 ? "Nothing to spawn yet" : $"Total {Mathf.RoundToInt(total > 0f ? 100f : 0f)}%", EditorStyles.miniLabel);
        }
    }

    // One colour per entry, matching the swatch under its icon, so a list's mix can be read at a glance.
    private static void DrawShareBar(List<SpawnTable.Entry> entries)
    {
        float total = RaidSpawnList.TotalWeight(entries);
        if (total <= 0f)
        {
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(15f);
            Rect bar = GUILayoutUtility.GetRect(0f, 8f, GUILayout.ExpandWidth(true));
            float x = bar.x;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].prefab == null)
                {
                    continue;
                }

                float width = bar.width * entries[i].weight / total;
                EditorGUI.DrawRect(new Rect(x, bar.y, Mathf.Max(0f, width - 1f), bar.height), ShareColour(i));
                x += width;
            }
        }

        EditorGUILayout.Space(2f);
    }

    private static Color ShareColour(int index) => Color.HSVToRGB(Mathf.Repeat(0.09f + index * 0.17f, 1f), 0.55f, 0.85f);

    private static void DrawIcon(GameObject prefab, Color swatch)
    {
        Rect icon = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
        EditorGUI.DrawRect(new Rect(icon.x, icon.yMax - 2f, icon.width, 2f), swatch);
        if (prefab == null)
        {
            return;
        }

        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
        if (preview != null)
        {
            GUI.DrawTexture(icon, preview, ScaleMode.ScaleToFit);
        }
        else if (AssetPreview.IsLoadingAssetPreviews())
        {
            HandleUtility.Repaint();
        }
    }

    // Dropping prefabs anywhere on the row below the table adds them, with an even share each.
    private static void DropPrefabs(List<SpawnTable.Entry> entries)
    {
        Rect zone = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
        zone.xMin += 15f;
        GUI.Box(zone, "+ Drop prefabs here", EditorStyles.helpBox);
        Event current = Event.current;
        if (!zone.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
        {
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (Object dropped in DragAndDrop.objectReferences)
            {
                if (dropped is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
                {
                    entries.Add(new SpawnTable.Entry { prefab = prefab, weight = 0f });
                    RaidSpawnList.SetShare(entries, entries.Count - 1, 100f / entries.Count);
                }
            }

            GUI.changed = true;
        }

        current.Use();
    }

    public static string Range(Vector2Int range) => range.x == range.y ? range.x.ToString() : $"{range.x}-{range.y}";
}

/// <summary>
/// The Preview tab of a raid: rolls the raid's layout for many seeds the way a real raid does and shows what it places
/// on average, per list and per prefab, so a change can be judged before playing.
/// </summary>
internal static class RaidSpawnPreview
{
    private const int SimulatedRaids = 50;
    private static int previewTier;

    private sealed class Row
    {
        public string Name;
        public string When;
        public int Points;
        public float Placed;
        public float Creatures;
    }

    private sealed class Simulation
    {
        public float StartCreatures;
        public float LaterCreatures;
        public readonly List<Row> Rows = new List<Row>();
        public readonly SortedDictionary<string, float> Prefabs = new SortedDictionary<string, float>();
    }

    private static readonly Dictionary<RaidDefinition, Simulation> Simulations = new Dictionary<RaidDefinition, Simulation>();

    public static void Draw(RaidDefinition raid, RaidSceneContext scene)
    {
        if (!scene.DrawSceneBar("simulate its layout"))
        {
            return;
        }

        if (!scene.Entry.HasValue)
        {
            EditorGUILayout.HelpBox("The scene has no RaidMap with a player start, so distances from the start are ignored.", MessageType.Warning);
        }

        if (raid.Difficulties.Count > 0)
        {
            string[] names = raid.Difficulties.Select(tier => "Tier " + Tiers.ToRoman(tier.tier)).ToArray();
            previewTier = Mathf.Clamp(GUILayout.Toolbar(previewTier, names, EditorStyles.miniButton), 0, names.Length - 1);
        }

        if (GUILayout.Button($"Simulate {SimulatedRaids} Raids", GUILayout.Height(26f)))
        {
            Simulations[raid] = Simulate(raid, scene, raid.Difficulties.Count > 0 ? raid.Difficulties[previewTier] : null);
        }

        if (raid.LayoutRules != null)
        {
            EditorGUILayout.HelpBox("This raid generates its map, but the simulation rolls on the points painted in the scene. The mix of what spawns " +
                                    "is right; for how many points a generated map offers, see each list's Where.", MessageType.Info);
        }

        if (!Simulations.TryGetValue(raid, out Simulation simulation))
        {
            EditorGUILayout.HelpBox("Rolls the layout the same way a real raid does and shows the averages.", MessageType.None);
            return;
        }

        EditorGUILayout.HelpBox(
            $"On average {simulation.StartCreatures:0.#} creatures at the start and up to {simulation.LaterCreatures:0.#} more from " +
            "stages and alarms (roaming spawns come on top). Packs count every member, including optional ones.", MessageType.Info);
        ContentUtility.BeginPanel("Per list");
        foreach (Row row in simulation.Rows)
        {
            string creatures = row.Creatures > 0f ? $", {row.Creatures:0.#} creatures" : "";
            EditorGUILayout.LabelField(row.Name, $"{row.When}: {row.Placed:0.#} placed of {row.Points} points{creatures}");
        }

        ContentUtility.EndPanel();
        ContentUtility.BeginPanel("Per prefab", "Average number per raid.");
        foreach (KeyValuePair<string, float> prefab in simulation.Prefabs)
        {
            EditorGUILayout.LabelField(prefab.Key, $"{prefab.Value:0.##}");
        }

        ContentUtility.EndPanel();
    }

    private static Simulation Simulate(RaidDefinition raid, RaidSceneContext scene, RaidDifficulty tier)
    {
        var simulation = new Simulation();
        SpawnRules rules = raid.GetSpawnRules(scene.Entry);
        var random = new System.Random(12345);
        var seeds = new int[SimulatedRaids];
        for (int i = 0; i < seeds.Length; i++)
        {
            seeds[i] = random.Next(1, int.MaxValue);
        }

        var bound = new HashSet<string>();
        foreach (RaidSpawnList list in raid.SpawnLists)
        {
            if (list == null)
            {
                continue;
            }

            bound.UnionWith(list.pointGroups);
            List<Transform> points = scene.PointsOf(list, rules);
            var row = new Row { Name = list.name, When = When(list.trigger, list.stage), Points = points.Count };
            foreach (int seed in seeds)
            {
                // Same seed key as RaidGenerator.SpawnList, so the preview matches play.
                var listRandom = new System.Random(RaidLayout.SeedFor(seed, "List:" + list.name));
                Add(simulation, row, SpawnGroup.Plan(listRandom, list.CountFor(rules), points, list.separation, tier != null ? list.EntriesFor(tier.tier) : list.entries));
            }

            Finish(simulation, row, list.trigger);
        }

        foreach (KeyValuePair<string, SpawnGroup> pair in scene.Groups)
        {
            SpawnGroup group = pair.Value;
            if (bound.Contains(pair.Key) || group.Table == null)
            {
                continue;
            }

            List<Transform> points = group.PointsFor(rules);
            var row = new Row { Name = pair.Key + " (scene group, no list)", When = When(group.Trigger, group.Stage), Points = points.Count };
            foreach (int seed in seeds)
            {
                Add(simulation, row, group.Plan(new System.Random(RaidLayout.SeedFor(seed, PathOf(group.transform))), rules, points));
            }

            Finish(simulation, row, group.Trigger);
        }

        return simulation;
    }

    private static void Add(Simulation simulation, Row row, List<(int point, GameObject prefab)> plan)
    {
        foreach ((int _, GameObject prefab) in plan)
        {
            row.Placed += 1f / SimulatedRaids;
            row.Creatures += (float)prefab.GetComponentsInChildren<MonsterAI>(true).Length / SimulatedRaids;
            simulation.Prefabs.TryGetValue(prefab.name, out float count);
            simulation.Prefabs[prefab.name] = count + 1f / SimulatedRaids;
        }
    }

    private static void Finish(Simulation simulation, Row row, SpawnTrigger trigger)
    {
        if (trigger == SpawnTrigger.RaidStart)
        {
            simulation.StartCreatures += row.Creatures;
        }
        else
        {
            simulation.LaterCreatures += row.Creatures;
        }

        simulation.Rows.Add(row);
    }

    private static string When(SpawnTrigger trigger, int stage) => trigger == SpawnTrigger.Stage ? $"stage {stage}" : trigger == SpawnTrigger.Alarm ? "alarm" : "start";

    // Must match RaidGenerator's group path so a simulated seed rolls the same as a real raid.
    private static string PathOf(Transform target)
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}
