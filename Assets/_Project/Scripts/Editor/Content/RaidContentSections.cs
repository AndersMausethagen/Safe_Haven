using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Raids in the Content Editor, one tab per part of the raid: Overview (name, scene, briefing), Rules (time, strength,
/// density), Enemies / Resources / Loot &amp; Events / Escalation (spawn lists: where, how many and what, in percent),
/// Systems (optional systems such as roaming spawns, edited in the Systems Editor) and Preview (simulated layouts).
/// Rules shared by every raid live in the Systems Editor (Raids > Global Rules).
/// </summary>
public sealed class RaidSection : ContentSection
{
    private static readonly string[] Tabs = { "Overview", "Rules", "Enemies", "Resources", "Loot & Events", "Escalation", "Systems", "Preview" };
    private const string TabKey = "SafeHaven.ContentEditor.RaidTab";

    private static readonly string[] Hidden = { "spawnLists", "systems", "roaming", "stages" };

    private SerializedObject serialized;

    public override string Title => "Raids";
    public override string Noun => "Raid";
    public override string SidebarGroup => "Raids";
    public override Type AssetType => typeof(RaidDefinition);
    public override string Folder => "Assets/_Project/Data/Raids";
    public override string Help =>
        "Everything about one raid, by tab. Rules shared by all raids and optional raid systems are in the Systems Editor " +
        "(Raids page).";

    public override bool DrawsOwnSettings => true;
    public override ICollection<string> HiddenFields => Hidden;
    protected override IEnumerable<string> CreateHiddenFields => Hidden;

    private static int Tab
    {
        get => Mathf.Clamp(EditorPrefs.GetInt(TabKey, 0), 0, Tabs.Length - 1);
        set => EditorPrefs.SetInt(TabKey, value);
    }

    public override int? ListTier(Object asset) => ((RaidDefinition)asset).Tier;

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var raid = (RaidDefinition)asset;
        string path = FindScenePath(raid.SceneName);
        if (string.IsNullOrWhiteSpace(raid.SceneName))
        {
            problems.Add("No scene, so a portal can't send anyone there.");
        }
        else if (path == null)
        {
            problems.Add($"There is no scene called '{raid.SceneName}' in the project.");
        }
        else if (!IsInBuild(raid.SceneName))
        {
            problems.Add($"The scene '{raid.SceneName}' isn't enabled in Build Settings, so it can't be loaded.");
        }

        RaidSceneContext scene = path != null ? RaidSceneContext.For(raid, path) : null;
        var owners = new Dictionary<string, string>();
        var names = new HashSet<string>();
        foreach (RaidSpawnList list in raid.SpawnLists)
        {
            if (list == null)
            {
                continue;
            }

            string label = $"{RaidSpawnListEditor.Label(list.category)} list '{list.name}'";
            if (!names.Add(list.name ?? ""))
            {
                problems.Add($"Two spawn lists are called '{list.name}'; they would roll the same.");
            }

            if (list.pointGroups.Count == 0)
            {
                problems.Add($"{label} has no spawn groups, so it places nothing.");
            }

            if (list.entries.Count == 0 || RaidSpawnList.TotalWeight(list.entries) <= 0f)
            {
                problems.Add($"{label} has nothing to spawn.");
            }
            else if (list.entries.Exists(entry => entry.prefab == null))
            {
                problems.Add($"{label} has an entry without a prefab.");
            }

            foreach (string group in list.pointGroups)
            {
                if (owners.TryGetValue(group, out string other))
                {
                    problems.Add($"Spawn group '{group}' is used by both '{other}' and '{list.name}'; only the first fills it.");
                }
                else
                {
                    owners.Add(group, list.name);
                }

                if (scene != null && scene.Loaded && !scene.Groups.ContainsKey(group))
                {
                    problems.Add($"{label} uses spawn group '{group}', which isn't in the scene.");
                }
            }
        }

        return problems;
    }

    public override bool DrawSettings(Object asset)
    {
        var raid = (RaidDefinition)asset;
        if (serialized == null || serialized.targetObject != raid)
        {
            serialized = new SerializedObject(raid);
        }

        serialized.Update();
        string path = FindScenePath(raid.SceneName);
        RaidSceneContext scene = RaidSceneContext.For(raid, path);

        EditorGUILayout.Space(4f);
        int tab = GUILayout.Toolbar(Tab, Tabs, GUILayout.Height(24f));
        if (tab != Tab)
        {
            Tab = tab;
            GUI.FocusControl(null);
        }

        EditorGUILayout.Space(4f);
        EditorGUI.BeginChangeCheck();
        switch (Tabs[Tab])
        {
            case "Overview":
                DrawOverview(raid, path);
                break;
            case "Rules":
                DrawRules(raid, scene);
                break;
            case "Enemies":
                DrawSpawnTab(raid, SpawnCategory.Enemies, scene);
                break;
            case "Resources":
                DrawSpawnTab(raid, SpawnCategory.Resources, scene);
                break;
            case "Loot & Events":
                DrawSpawnTab(raid, SpawnCategory.LootAndEvents, scene);
                break;
            case "Escalation":
                ContentUtility.BeginPanel("Danger stages", "The raid gets more dangerous as time passes. Each stage begins at a share " +
                                                           "of the raid's time; Stage lists below spawn when theirs begins.");
                Field("stages");
                ContentUtility.EndPanel();
                DrawSpawnTab(raid, SpawnCategory.Escalation, scene);
                break;
            case "Systems":
                DrawSystems(raid);
                break;
            default:
                ContentUtility.BeginPanel("Preview", "What the raid's layout places on average, rolled like real raids.");
                RaidSpawnPreview.Draw(raid, scene);
                ContentUtility.EndPanel();
                break;
        }

        serialized.ApplyModifiedProperties();
        return EditorGUI.EndChangeCheck();
    }

    private void DrawOverview(RaidDefinition raid, string path)
    {
        ContentUtility.BeginPanel("Identity", "How the raid is named and described at the raid portal.");
        Field("displayName");
        Field("id");
        Field("description");
        Field("tier");
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Scene", "The scene the raid is played in. Its Spawn Groups are the places spawn lists fill.");
        EditorGUI.BeginChangeCheck();
        var sceneAsset = (SceneAsset)EditorGUILayout.ObjectField("Scene", path != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null,
            typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            serialized.FindProperty("sceneName").stringValue = sceneAsset != null ? sceneAsset.name : "";
            serialized.ApplyModifiedProperties();
            Host?.RefreshList();
        }

        if (path != null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!IsInBuild(raid.SceneName) && GUILayout.Button("Add to Build Settings"))
                {
                    var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                    scenes.RemoveAll(entry => entry.path == path);
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    EditorBuildSettings.scenes = scenes.ToArray();
                    Host?.RefreshList();
                }

                if (GUILayout.Button("Open Scene") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(path);
                    GUIUtility.ExitGUI();
                }
            }
        }

        Field("locked");
        if (raid.IsLocked)
        {
            Field("lockedText");
        }

        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Briefing", "Shown before the raid so the player knows what to expect.");
        Field("knownEnemies");
        Field("likelyItems");
        Field("likelyResources");
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Results screen", "Artwork after the raid ends.");
        Field("extractedArtwork");
        Field("failedArtwork");
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("At a glance");
        foreach (SpawnCategory category in Enum.GetValues(typeof(SpawnCategory)))
        {
            int lists = raid.SpawnLists.Count(list => list != null && list.category == category);
            EditorGUILayout.LabelField(RaidSpawnListEditor.Label(category), lists == 1 ? "1 spawn list" : $"{lists} spawn lists");
        }

        EditorGUILayout.LabelField("Systems", SystemsSummary(raid));
        ContentUtility.EndPanel();
    }

    private void DrawRules(RaidDefinition raid, RaidSceneContext scene)
    {
        ContentUtility.BeginPanel("Time", "How long the raid lasts and how long extracting takes.");
        Field("durationSeconds");
        Field("extractionSeconds");
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Strength", "Multipliers on top of each creature's and cache's own values. Raid modifiers change them further.");
        Field("lootMultiplier");
        Field("enemyHealthMultiplier");
        Field("enemyPowerMultiplier");
        ContentUtility.EndPanel();

        RaidGlobalSettings global = RaidGlobalSettings.Current;
        ContentUtility.BeginPanel("Spawns", "How many enemies the raid places, and what 'near' and 'far' from the start mean.");
        Field("enemyDensity");
        float globalDensity = global != null ? global.GlobalEnemyDensity : 1f;
        EditorGUILayout.LabelField(" ", $"x {globalDensity:0.##} global density = {raid.EffectiveEnemyDensity:0.##} in play", EditorStyles.miniLabel);
        Field("overrideBands");
        using (new EditorGUI.DisabledScope(!raid.OverridesBands))
        {
            Field("nearDistance");
            Field("farDistance");
        }

        if (!raid.OverridesBands && global != null)
        {
            EditorGUILayout.LabelField(" ", $"Using the global distances: near {global.NearDistance:0} m, far {global.FarDistance:0} m.",
                EditorStyles.miniLabel);
        }

        ContentUtility.EndPanel();
    }

    private static void DrawSpawnTab(RaidDefinition raid, SpawnCategory category, RaidSceneContext scene)
    {
        ContentUtility.BeginPanel(RaidSpawnListEditor.Label(category), RaidSpawnListEditor.Describe(category));
        RaidSpawnListEditor.Draw(raid, category, scene);
        ContentUtility.EndPanel();
    }

    private static void DrawSystems(RaidDefinition raid)
    {
        ContentUtility.BeginPanel("Raid systems", "Optional systems this raid uses, such as roaming spawns. They are added and " +
                                                  "tuned in the Systems Editor so the same kinds of systems are set up in one place.");
        if (raid.Systems.Count == 0)
        {
            EditorGUILayout.LabelField("This raid uses no extra systems.", ContentUtility.WrappedMiniLabel);
        }

        foreach (RaidSystem system in raid.Systems)
        {
            if (system != null)
            {
                EditorGUILayout.LabelField(system.DisplayName, system.Enabled ? "on" : "off");
            }
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Edit Raid Systems in the Systems Editor", GUILayout.Height(24f)))
        {
            RaidsAndPortalPage.ShowSystemsOf(raid);
        }

        ContentUtility.EndPanel();
    }

    private static string SystemsSummary(RaidDefinition raid)
    {
        var on = new List<string>();
        foreach (RaidSystem system in raid.Systems)
        {
            if (system != null && system.Enabled)
            {
                on.Add(system.DisplayName);
            }
        }

        return on.Count == 0 ? "none" : string.Join(", ", on);
    }

    private void Field(string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    internal static string FindScenePath(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return path;
            }
        }

        return null;
    }

    private static bool IsInBuild(string sceneName)
    {
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (entry.enabled && System.IO.Path.GetFileNameWithoutExtension(entry.path) == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Raid modifiers in the Content Editor: what they change and which items let the player use them.</summary>
public sealed class RaidModifierSection : ContentSection
{
    public override string Title => "Raid Modifiers";
    public override string Noun => "Raid Modifier";
    public override string SidebarGroup => "Raids";
    public override Type AssetType => typeof(RaidModifierDefinition);
    public override string Folder => "Assets/_Project/Data/Raids/Modifiers";
    public override string Help =>
        "Changes to a raid's rules: more time, faster extraction, richer caches, tougher enemies. Point an item's Raid " +
        "Modifier at one to make it something the player crafts in Safe Haven and picks at the raid portal.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var modifier = (RaidModifierDefinition)asset;
        if (modifier.Effects.Count == 0)
        {
            problems.Add("No effects, so using it changes nothing.");
        }

        if (ItemsUsing(modifier).Count == 0)
        {
            problems.Add("No item uses this modifier, so players can't pick it yet.");
        }

        return problems;
    }

    public override void DrawExtras(Object asset)
    {
        var modifier = (RaidModifierDefinition)asset;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("In Game", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Effects", modifier.DescribeEffects(), EditorStyles.wordWrappedLabel);

        List<ItemDefinition> items = ItemsUsing(modifier);
        if (items.Count == 0)
        {
            EditorGUILayout.HelpBox("Point an item's Raid Modifier field at this asset so players can craft and use it.", MessageType.Info);
            return;
        }

        ContentSection itemSection = FindItemSection();
        foreach (ItemDefinition item in items)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Used by", item.DisplayName);
                if (itemSection != null && GUILayout.Button("Open", GUILayout.Width(60f)))
                {
                    Host.Select(itemSection, item);
                }
            }
        }
    }

    private ContentSection FindItemSection()
    {
        if (Host == null)
        {
            return null;
        }

        foreach (ContentSection section in Host.Sections)
        {
            if (section is ItemSection)
            {
                return section;
            }
        }

        return null;
    }

    private static List<ItemDefinition> ItemsUsing(RaidModifierDefinition modifier)
    {
        return ResourceCatalogTools.LoadAll<ItemDefinition>().FindAll(item => item.RaidModifier == modifier);
    }
}
