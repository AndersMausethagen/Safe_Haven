#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The raid HUD's layout after the UX pass: the way line with its arrow beside it, one news line under it, and the companion and what
/// is at risk moved down above the vitals. Safe to run again.
/// </summary>
public static class BuildRaidHudPass
{
    private const string Path = "Assets/_Project/Prefabs/UI/RaidHUD.prefab";
    private static readonly Vector2 Left = new Vector2(36f, 176f);

    public static string Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            RectTransform timerPanel = Find(root, "Timer Panel");
            timerPanel.sizeDelta = new Vector2(760f, timerPanel.sizeDelta.y);

            RectTransform arrow = Find(root, "Extraction Arrow");
            arrow.sizeDelta = new Vector2(22f, 22f);
            arrow.anchoredPosition = new Vector2(0f, 17f);

            RectTransform panel = Find(root, "Exits Panel");
            panel.anchoredPosition = new Vector2(0f, -124f);
            panel.sizeDelta = new Vector2(760f, panel.sizeDelta.y);

            RectTransform risk = Find(root, "At Risk");
            Status(risk, root.transform, Left);

            RectTransform companion = FindOrNull(root, "Companion");
            if (companion == null)
            {
                companion = Object.Instantiate(risk.gameObject, root.transform).GetComponent<RectTransform>();
                companion.name = "Companion";
            }

            Status(companion, root.transform, Left + new Vector2(0f, 22f));

            var view = new SerializedObject(root.GetComponent<RaidHudView>());
            view.FindProperty("companionLabel").objectReferenceValue = companion.GetComponent<TMP_Text>();
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            return $"panel {panel.GetComponents<Component>().Length} components; alert label {view.FindProperty("alertLabel").objectReferenceValue?.name}; companion {view.FindProperty("companionLabel").objectReferenceValue?.name}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Out of the top block's layout group, on the left over the vitals.
    private static void Status(RectTransform rect, Transform parent, Vector2 position)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(620f, 22f);
        rect.localScale = Vector3.one;
        rect.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;
        rect.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static RectTransform Find(GameObject root, string name)
    {
        RectTransform found = FindOrNull(root, name);
        if (found == null)
        {
            throw new System.InvalidOperationException(name + " is not in the Raid HUD.");
        }

        return found;
    }

    private static RectTransform FindOrNull(GameObject root, string name)
    {
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name == name)
            {
                return rect;
            }
        }

        return null;
    }
}
