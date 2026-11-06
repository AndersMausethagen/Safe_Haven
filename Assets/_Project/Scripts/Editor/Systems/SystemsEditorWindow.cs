using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Systems Editor (Safe Haven > Systems Editor): one place to balance how the game's systems behave, separate from
/// the Content Editor where items, resources, monsters and quests are made. Each page shows a system's settings next to
/// the numbers they produce (how long a full belly lasts, how much a repair costs, how big the ward grows) and tables for
/// tuning many assets side by side.
/// <para>
/// Extending: add a SystemsPage subclass to CreatePages. Pages are grouped in the sidebar by their Group.
/// </para>
/// </summary>
public sealed class SystemsEditorWindow : EditorWindow
{
    private const float SidebarWidth = 190f;

    private List<SystemsPage> pages;
    private int selected;
    private Vector2 sidebarScroll;
    private Vector2 pageScroll;

    [MenuItem("Safe Haven/Systems Editor", false, 2)]
    public static void Open()
    {
        var window = GetWindow<SystemsEditorWindow>();
        window.titleContent = new GUIContent("Systems Editor");
        window.minSize = new Vector2(820f, 480f);
        window.Show();
    }

    /// <summary>Opens the window on the first page of type T.</summary>
    public static void ShowPage<T>() where T : SystemsPage
    {
        Open();
        var window = GetWindow<SystemsEditorWindow>();
        window.pages ??= CreatePages();
        int index = window.pages.FindIndex(page => page is T);
        if (index >= 0)
        {
            window.selected = index;
            EditorPrefs.SetInt("SafeHaven.SystemsEditor.Page", index);
        }

        window.Repaint();
    }

    private static List<SystemsPage> CreatePages()
    {
        return new List<SystemsPage>
        {
            new SurvivalPage(),
            new CarryingPage(),
            new DeathAndRepairPage(),
            new GatheringMinigamePage(),
            new TreeChoppingPage(),
            new MovementAndCombatPage(),
            new SensesAndStealthPage(),
            new LockpickingPage(),
            new RaidsAndPortalPage(),
            new HomePortalAndBuildingPage(),
            new CraftingPage(),
            new ResidentsAndPurificationPage(),
            new EssencePage(),
            new RelicsPage(),
            new TransportPage(),
            new QuestMarkersPage(),
            new StoryPacingPage(),
            new WardBreachPage(),
            new LootAndRarityPage(),
            new GearScorePage(),
            new UIStylePage(),
            new PlaytestPage(),
            new MultiplayerPage(),
        };
    }

    private void OnEnable()
    {
        pages = CreatePages();
        selected = Mathf.Clamp(EditorPrefs.GetInt("SafeHaven.SystemsEditor.Page", 0), 0, pages.Count - 1);
    }

    private void OnGUI()
    {
        pages ??= CreatePages();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidebar();
            DrawPage();
        }
    }

    private void DrawSidebar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true)))
        {
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            string group = null;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].Group != group)
                {
                    group = pages[i].Group;
                    GUILayout.Space(group == pages[0].Group ? 2f : 8f);
                    EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel);
                }

                var style = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, fixedHeight = 22f };
                if (GUILayout.Toggle(i == selected, pages[i].Title, style) && i != selected)
                {
                    selected = i;
                    pageScroll = Vector2.zero;
                    EditorPrefs.SetInt("SafeHaven.SystemsEditor.Page", selected);
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Items, resources, monsters and quests are made in the Content Editor.", ContentUtility.WrappedMiniLabel);
            if (GUILayout.Button("Open Content Editor"))
            {
                EditorApplication.ExecuteMenuItem("Safe Haven/Content Editor");
            }
        }
    }

    private void DrawPage()
    {
        SystemsPage page = pages[selected];
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField(page.Title, ContentUtility.HeaderLabel);
            EditorGUILayout.LabelField(page.Help, ContentUtility.WrappedMiniLabel);
            GUILayout.Space(6f);
            pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
            page.Draw();
            EditorGUILayout.EndScrollView();
        }
    }

    private void OnInspectorUpdate()
    {
        // Derived numbers follow edits made elsewhere (the Inspector, the Content Editor).
        Repaint();
    }
}
