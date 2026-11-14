#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The raid planner builds itself in code now. This empties its prefab down to the shell the scenes keep their instances of:
/// the canvas, the UIWindow and a full-screen, dimmed content object. Run once with:
/// unity command script run --file AgentScripts/BuildRaidPlanner.cs --entry BuildRaidPlanner.Run
/// </summary>
public static class BuildRaidPlanner
{
    private const string Path = "Assets/_Project/Prefabs/UI/RaidPreparationWindow.prefab";

    public static string Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var window = root.GetComponent<RaidPreparationWindow>();
            Transform found = root.transform.Find("Panel") ?? root.transform.Find("Content");
            var content = (RectTransform)found;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(content.GetChild(i).gameObject);
            }

            if (content.TryGetComponent(out UIRole role))
            {
                Object.DestroyImmediate(role);
            }

            if (content.TryGetComponent(out Outline outline))
            {
                Object.DestroyImmediate(outline);
            }

            // With the other windows, over the HUD: the hotbar was drawn across the planner's tooltips.
            root.GetComponent<Canvas>().sortingOrder = 200;
            content.name = "Content";
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var so = new SerializedObject(window);
            so.FindProperty("content").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            return $"planner emptied: {content.name} has {content.childCount} children, window content {so.FindProperty("window").objectReferenceValue != null}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
