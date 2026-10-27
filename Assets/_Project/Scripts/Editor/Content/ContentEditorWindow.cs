using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// One place to create and change all game content without code: resources and their categories, monsters, buildings,
/// items, weapons, attacks, combat stats, raids, game settings and prefabs. Pick a kind of content on the left, an entry
/// in the middle, and change everything about it on the right, including the data it uses and its generated prefab.
/// Needs Attention lists every problem across all pages; the Table view edits a whole page at once and round-trips to
/// CSV; each entry shows its connections, where it comes from and, while playing, buttons to try it in the game.
/// Open it from Safe Haven > Content Editor.
/// </summary>
public sealed class ContentEditorWindow : EditorWindow, IContentEditorHost
{
    private const float SidebarWidth = 175f;
    private const float ListWidth = 270f;
    private static readonly Color SelectedColor = new Color(0.24f, 0.48f, 0.90f, 0.45f);
    private static readonly Color SeparatorColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly string[] ViewModes = { "List", "Table" };

    [SerializeField] private int sectionIndex;
    [SerializeField] private Object selected;
    [SerializeField] private string search = "";
    [SerializeField] private bool tableMode;

    private List<ContentSection> sections;
    private ContentInspector inspector;
    private readonly ContentTableView table = new ContentTableView();
    private readonly ContentConnections connections = new ContentConnections();
    private readonly EconomyPanel economy = new EconomyPanel();
    private readonly List<Object> assets = new List<Object>();
    private readonly Dictionary<Object, List<string>> problems = new Dictionary<Object, List<string>>();
    private int[] counts;
    private bool listDirty = true;
    private Vector2 sidebarScroll;
    private Vector2 listScroll;
    private Vector2 detailsScroll;
    private ScriptableObject draft;
    private string draftName = "";
    private GUIContent warningIcon;
    private GUIStyle tierStyle;

    public ContentInspector Inspector => inspector;
    public IReadOnlyList<ContentSection> Sections => sections;
    private ContentSection Current => sections[sectionIndex];

    [MenuItem("Safe Haven/Content Editor", false, 0)]
    public static void OpenWindow() => Open(null);

    [MenuItem("Safe Haven/Resources/Resource Creator")]
    private static void OpenResources() => Open(typeof(ResourceSection));

    [MenuItem("Safe Haven/Monsters/Monster Creator")]
    private static void OpenMonsters() => Open(typeof(MonsterSection));

    [MenuItem("Safe Haven/Buildings/Building Creator")]
    private static void OpenBuildings() => Open(typeof(BuildingSection));

    [MenuItem("Safe Haven/Items/Item Creator")]
    private static void OpenItems() => Open(typeof(ItemSection));

    [MenuItem("Safe Haven/Content/Prefab Browser")]
    private static void OpenPrefabs() => Open(typeof(PrefabBrowserSection));

    /// <summary>Opens the editor, optionally on one kind of content.</summary>
    public static ContentEditorWindow Open(Type sectionType)
    {
        var window = GetWindow<ContentEditorWindow>();
        window.titleContent = new GUIContent("Content Editor");
        window.minSize = new Vector2(920f, 560f);
        window.EnsureSections();
        if (sectionType != null)
        {
            int index = window.sections.FindIndex(section => section.GetType() == sectionType);
            if (index >= 0)
            {
                window.SwitchSection(index);
            }
        }

        window.Show();
        return window;
    }

    public void RefreshList()
    {
        listDirty = true;
        inspector?.ClearCache();
        connections.Clear();
        Repaint();
    }

    public void Select(ContentSection section, Object asset)
    {
        int index = sections.IndexOf(section);
        if (index >= 0 && index != sectionIndex)
        {
            SwitchSection(index);
        }

        DiscardDraft();
        selected = asset;
        tableMode = false;
        search = "";
        listDirty = true;
        Repaint();
    }

    public void CreateNew(ContentSection section, Action<ScriptableObject> initialize)
    {
        int index = sections.IndexOf(section);
        if (index < 0 || !section.CanCreate)
        {
            return;
        }

        if (index != sectionIndex)
        {
            SwitchSection(index);
        }

        tableMode = false;
        StartDraft();
        initialize?.Invoke(draft);
        Repaint();
    }

    public ContentSection FindSectionFor(Object asset)
    {
        EnsureSections();
        return asset != null ? sections.Find(section => section.Handles(asset)) : null;
    }

    public ContentSection FindSection(Type sectionType)
    {
        EnsureSections();
        return sections.Find(section => section.GetType() == sectionType);
    }

    public void Notify(string message)
    {
        ShowNotification(new GUIContent(message), 2.5);
    }

    private void OnEnable()
    {
        EnsureSections();
        listDirty = true;
    }

    private void OnDisable()
    {
        AssetDatabase.SaveAssets();
        DiscardDraft();
        if (sections != null)
        {
            foreach (ContentSection section in sections)
            {
                section.Dispose();
            }
        }
    }

    private void OnFocus()
    {
        listDirty = true;
    }

    private void OnLostFocus()
    {
        AssetDatabase.SaveAssets();
    }

    private void OnProjectChange()
    {
        RefreshList();
    }

    private void EnsureSections()
    {
        if (sections != null)
        {
            return;
        }

        sections = CreateSections();
        foreach (ContentSection section in sections)
        {
            section.Host = this;
        }

        inspector = new ContentInspector(FindSectionByTypeName, () =>
        {
            listDirty = true;
            Repaint();
        });
        sectionIndex = Mathf.Clamp(sectionIndex, 0, sections.Count - 1);
    }

    /// <summary>A fresh set of every page, in sidebar order. Also used without a window (CLI health checks).</summary>
    public static List<ContentSection> CreateSections()
    {
        // Content only: game-wide rules and balance (survival, carrying, raids, the ward...) live in the Systems Editor.
        return new List<ContentSection>
        {
            new ContentHealthSection(),
            new ResourceSection(),
            new GatheringNodeSection(),
            new HarvestFeedbackSection(),
            new ResourceCategorySection(),
            new MonsterSection(),
            new BuildingSection(),
            new ItemSection(),
            new RecipeSection(),
            new WeaponSection(),
            new ArrowSection(),
            new AttackSection(),
            new CombatStatsSection(),
            new RaidSection(),
            new RaidModifierSection(),
            new ResidentSection(),
            new JobSection(),
            new WardBreachSection(),
            new QuestSection(),
            new QuestEventSection(),
            new PrefabBrowserSection(),
        };
    }

    private ContentSection FindSectionByTypeName(string typeName)
    {
        return sections.Find(section => section.UsesDataInspector && section.AssetType.Name == typeName);
    }

    private void SwitchSection(int index)
    {
        AssetDatabase.SaveAssets();
        DiscardDraft();
        sectionIndex = index;
        selected = null;
        search = "";
        listScroll = Vector2.zero;
        detailsScroll = Vector2.zero;
        listDirty = true;
    }

    private void OnGUI()
    {
        EnsureSections();
        warningIcon ??= EditorGUIUtility.IconContent("console.warnicon.sml");
        if (listDirty)
        {
            RebuildList();
        }

        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
            DrawSidebar();
            Separator();
            DrawList();
            Separator();
            DrawDetails();
        }
    }

    private void RebuildList()
    {
        listDirty = false;
        assets.Clear();
        assets.AddRange(Current.Load());
        assets.Sort(Current.Compare);
        RecomputeProblems();

        counts = new int[sections.Count];
        for (int i = 0; i < sections.Count; i++)
        {
            counts[i] = i == sectionIndex ? assets.Count : sections[i].Load().Count;
        }

        if (selected != null && !assets.Contains(selected))
        {
            selected = null;
        }
    }

    private void RecomputeProblems()
    {
        problems.Clear();
        foreach (Object asset in assets)
        {
            if (asset != null)
            {
                problems[asset] = Current.FindProblems(asset, assets);
            }
        }
    }

    // ----- Sidebar -----

    private void DrawSidebar()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Content", ContentUtility.HeaderLabel);
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
            string group = null;
            for (int i = 0; i < sections.Count; i++)
            {
                ContentSection section = sections[i];
                if (section.SidebarGroup != group)
                {
                    group = section.SidebarGroup;
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(group.ToUpperInvariant(), EditorStyles.miniBoldLabel);
                }

                string label = counts != null ? $"{section.Title}  ({counts[i]})" : section.Title;
                if (Row(label, i == sectionIndex, null, null, null, false, 0) && i != sectionIndex)
                {
                    SwitchSection(i);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    // ----- List -----

    private void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(Current.Title, ContentUtility.HeaderLabel);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            if (Current.UsesDataInspector)
            {
                int mode = GUILayout.Toolbar(tableMode ? 1 : 0, ViewModes, EditorStyles.miniButton);
                if (mode == 1 != tableMode)
                {
                    AssetDatabase.SaveAssets();
                    DiscardDraft();
                    tableMode = mode == 1;
                    GUIUtility.ExitGUI();
                }
            }

            if (Current.CanCreate && GUILayout.Button($"+  New {Current.Noun}", GUILayout.Height(24f)))
            {
                StartDraft();
                GUIUtility.ExitGUI();
            }

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            string group = null;
            int shown = 0;
            foreach (Object asset in assets)
            {
                if (asset == null || !Matches(asset))
                {
                    continue;
                }

                shown++;
                string assetGroup = Current.ListGroup(asset);
                if (assetGroup != null && assetGroup != group)
                {
                    group = assetGroup;
                    EditorGUILayout.Space(3f);
                    EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel);
                }

                bool warn = problems.TryGetValue(asset, out List<string> found) && found.Count > 0;
                if (Row(Current.Label(asset), asset == selected && draft == null, Current.ListIcon(asset), Current.ListSwatch(asset),
                        Current.ListTier(asset), warn, assetGroup != null ? 1 : 0))
                {
                    AssetDatabase.SaveAssets();
                    DiscardDraft();
                    selected = asset;
                    tableMode = false;
                    detailsScroll = Vector2.zero;
                    GUIUtility.ExitGUI();
                }
            }

            if (shown == 0)
            {
                EditorGUILayout.LabelField(assets.Count == 0 ? Current.EmptyListText : "Nothing matches the search.",
                    ContentUtility.WrappedMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private bool Matches(Object asset)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(Current.Label(asset)) || Contains(Current.ListGroup(asset)) || Contains(asset.name);
    }

    private bool Contains(string text)
    {
        return !string.IsNullOrEmpty(text) && text.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ----- Details -----

    private void DrawDetails()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            if (tableMode && draft == null && Current.UsesDataInspector)
            {
                table.Draw(Current, assets.FindAll(asset => asset != null && Matches(asset)), inspector,
                    asset => problems.TryGetValue(asset, out List<string> found) && found.Count > 0,
                    asset =>
                    {
                        tableMode = false;
                        selected = asset;
                        detailsScroll = Vector2.zero;
                    },
                    () =>
                    {
                        RecomputeProblems();
                        Repaint();
                    });
                return;
            }

            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            EditorGUILayout.Space(6f);
            if (draft != null)
            {
                DrawDraft();
            }
            else if (selected != null)
            {
                DrawSelected();
            }
            else
            {
                DrawSectionHelp();
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSectionHelp()
    {
        EditorGUILayout.LabelField(Current.Title, ContentUtility.HeaderLabel);
        if (!string.IsNullOrEmpty(Current.Help))
        {
            EditorGUILayout.LabelField(Current.Help, new GUIStyle(EditorStyles.wordWrappedLabel));
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            Current.CanCreate
                ? $"Pick an entry in the list to change it, or press New {Current.Noun} to make one. Hover any setting for an explanation."
                : "Pick an entry in the list to change it. Hover any setting for an explanation.",
            MessageType.Info);
    }

    private void DrawSelected()
    {
        Object asset = selected;
        using (new EditorGUILayout.HorizontalScope())
        {
            Sprite icon = Current.ListIcon(asset);
            if (icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));
                ContentUtility.DrawSprite(iconRect, icon);
            }

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(Current.Label(asset), ContentUtility.HeaderLabel);
                string id = ContentUtility.IdOf(asset);
                EditorGUILayout.LabelField(
                    AssetDatabase.GetAssetPath(asset) + (string.IsNullOrEmpty(id) ? "" : "   •   id: " + id),
                    ContentUtility.WrappedMiniLabel);
            }
        }

        if (problems.TryGetValue(asset, out List<string> found) && found.Count > 0)
        {
            EditorGUILayout.HelpBox("Needs attention:\n• " + string.Join("\n• ", found), MessageType.Warning);
        }

        PlaytestTools.Draw(this, Current, asset);

        if (Current.UsesDataInspector && Current.DrawsOwnSettings)
        {
            if (Current.DrawSettings(asset))
            {
                RecomputeProblems();
            }
        }
        else if (Current.UsesDataInspector)
        {
            ContentUtility.BeginPanel("Settings", "Hover any setting for an explanation. Changes are saved automatically.");
            if (inspector.Draw(asset, Current.HiddenFields))
            {
                RecomputeProblems();
            }

            ContentUtility.EndPanel();
        }

        Current.DrawExtras(asset);
        economy.Draw(this, asset);
        connections.Draw(this, asset);
        DrawFileActions(asset);
    }

    private void DrawFileActions(Object asset)
    {
        ContentUtility.BeginPanel("File");
        string suggested = PrefabBuildUtility.MakeFileName(Current.Label(asset));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Show in Project"))
            {
                ContentUtility.Reveal(asset);
            }

            if (Current.UsesDataInspector && asset.name != suggested
                && GUILayout.Button(new GUIContent($"Rename File to {suggested}", "Keeps the file name in step with the name shown in the game.")))
            {
                string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(asset), suggested);
                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.DisplayDialog("Rename failed", error, "OK");
                }

                RefreshList();
                GUIUtility.ExitGUI();
            }

            if (Current.CanDuplicate && GUILayout.Button(new GUIContent("Duplicate", "Makes a copy with its own name and id.")))
            {
                Duplicate(asset);
                GUIUtility.ExitGUI();
            }

            if (Current.CanDelete)
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                if (GUILayout.Button("Delete"))
                {
                    Delete(asset);
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = previous;
            }
        }

        ContentUtility.EndPanel();
    }

    // ----- Creating -----

    private void StartDraft()
    {
        AssetDatabase.SaveAssets();
        DiscardDraft();
        draft = Current.CreateDraft();
        draftName = "";
        selected = null;
        detailsScroll = Vector2.zero;
    }

    private void DiscardDraft()
    {
        if (draft != null)
        {
            DestroyImmediate(draft);
        }

        draft = null;
        draftName = "";
    }

    private void DrawDraft()
    {
        EditorGUILayout.LabelField($"New {Current.Noun}", ContentUtility.HeaderLabel);

        ContentUtility.BeginPanel("Name", "Shown in the game. The file is named after it.");
        draftName = EditorGUILayout.TextField("Name", draftName);
        bool hasId = ContentUtility.IdOf(draft) != null;
        string id = PrefabBuildUtility.MakeId(draftName);
        if (hasId)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(new GUIContent("Id (automatic)", "Used by saves. Made from the name."), id);
            }
        }

        EditorGUILayout.LabelField("Saved as " + $"{Current.Folder}/{PrefabBuildUtility.MakeFileName(draftName)}.asset", ContentUtility.WrappedMiniLabel);
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("Settings", "Hover any setting for an explanation. Everything can be changed later.");
        inspector.Draw(draft, Current.HiddenWhenCreating);
        ContentUtility.EndPanel();

        Current.DrawCreateOptions();

        string error = null;
        if (string.IsNullOrWhiteSpace(draftName))
        {
            error = "Enter a name.";
        }
        else if (hasId && assets.Exists(asset => asset != null && ContentUtility.IdOf(asset) == id))
        {
            error = $"Another {Current.Noun.ToLowerInvariant()} already has the id '{id}'. Choose another name.";
        }
        else
        {
            error = Current.ValidateCreate(draft, draftName);
        }

        EditorGUILayout.Space(6f);
        if (error != null)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button($"Create {Current.Noun}", GUILayout.Height(30f)))
                {
                    Create();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Cancel", GUILayout.Height(30f), GUILayout.Width(90f)))
            {
                DiscardDraft();
                GUIUtility.ExitGUI();
            }
        }
    }

    private void Create()
    {
        try
        {
            ScriptableObject created = Current.CreateFromDraft(draft, draftName.Trim(), true);
            DiscardDraft();
            RefreshList();
            selected = created;
            ContentUtility.Reveal(created);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
        {
            EditorUtility.DisplayDialog($"{Current.Noun} not created", exception.Message, "OK");
        }
    }

    private void Duplicate(Object asset)
    {
        try
        {
            Object copy = Current.Duplicate(asset);
            RefreshList();
            selected = copy;
        }
        catch (InvalidOperationException exception)
        {
            EditorUtility.DisplayDialog("Not duplicated", exception.Message, "OK");
        }
    }

    // ----- Deleting -----

    private void Delete(Object asset)
    {
        string blocker = Current.DeleteBlocker(asset);
        if (blocker != null)
        {
            EditorUtility.DisplayDialog($"Can't delete {Current.Label(asset)}", blocker, "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(asset);
        List<string> users = ContentUtility.FindReferences(asset, Current.IgnoreReference);
        GameObject prefab = Current.LinkedPrefab(asset);

        var message = new System.Text.StringBuilder($"Move '{path}' to the trash?");
        if (users.Count > 0)
        {
            message.Append($"\n\n{users.Count} other file(s) use it; those references will become empty:");
            for (int i = 0; i < users.Count && i < 8; i++)
            {
                message.Append("\n  • ").Append(users[i]);
            }

            if (users.Count > 8)
            {
                message.Append($"\n  …and {users.Count - 8} more");
            }
        }

        bool deletePrefab = false;
        if (prefab != null)
        {
            message.Append($"\n\nIts prefab '{AssetDatabase.GetAssetPath(prefab)}' can be deleted with it.");
            int choice = EditorUtility.DisplayDialogComplex($"Delete {Current.Label(asset)}", message.ToString(),
                "Delete Data and Prefab", "Cancel", "Delete Data Only");
            if (choice == 1)
            {
                return;
            }

            deletePrefab = choice == 0;
        }
        else if (!EditorUtility.DisplayDialog($"Delete {Current.Label(asset)}", message.ToString(), "Delete", "Cancel"))
        {
            return;
        }

        Current.BeforeDelete(asset);
        selected = null;
        inspector.ClearCache();
        AssetDatabase.MoveAssetToTrash(path);
        if (deletePrefab)
        {
            AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(prefab));
        }

        RefreshList();
    }

    // ----- Drawing helpers -----

    private bool Row(string label, bool isSelected, Sprite icon, Color? swatch, int? tier, bool warning, int indent)
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
        if (isSelected)
        {
            EditorGUI.DrawRect(rect, SelectedColor);
        }

        float x = rect.x + 6f + indent * 10f;
        if (swatch.HasValue)
        {
            EditorGUI.DrawRect(new Rect(x, rect.y + 5f, 4f, 12f), swatch.Value);
            x += 9f;
        }

        if (icon != null)
        {
            ContentUtility.DrawSprite(new Rect(x, rect.y + 2f, 18f, 18f), icon);
            x += 22f;
        }

        float right = rect.xMax - 4f;
        if (warning)
        {
            GUI.Label(new Rect(right - 18f, rect.y + 2f, 18f, 18f), warningIcon);
            right -= 20f;
        }

        if (tier.HasValue)
        {
            tierStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
            tierStyle.normal.textColor = Tiers.ColorOf(tier.Value);
            GUI.Label(new Rect(right - 30f, rect.y + 3f, 28f, 16f), Tiers.ToRoman(tier.Value), tierStyle);
            right -= 32f;
        }

        GUI.Label(new Rect(x, rect.y + 3f, Mathf.Max(0f, right - x), 18f), label);

        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
        {
            current.Use();
            return true;
        }

        return false;
    }

    private static void Separator()
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(rect, SeparatorColor);
    }
}
