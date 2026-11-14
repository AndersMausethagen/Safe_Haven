using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the Focus bar to the vitals HUD (Safe Haven › Setup › UI › Add Focus Bar to Vitals HUD): a thin warm-gold copy of the
/// stamina bar with its label, under stamina, the panel grown to make room. Running it again does nothing once the bar
/// is there.
/// </summary>
public static class VitalsFocusBar
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/PlayerVitalsHUD.prefab";
    private const float Row = 22f;
    private const float BarHeight = 9f;
    private static readonly Color FocusColor = new Color(0.93f, 0.74f, 0.33f, 1f);

    [MenuItem("Safe Haven/Setup/UI/Add Focus Bar to Vitals HUD", false, 200)]
    public static void AddMenu() => Debug.Log(Add());

    public static string Add()
    {
        if (RetiredBuilders.Refuse("Add Focus Bar to Vitals HUD")) return "retired (see AgentScripts/RETIRED.md)";
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform panel = root.transform.Find("Vitals Panel");
            Transform staminaBar = panel != null ? panel.Find("Stamina Bar") : null;
            Transform staminaLabel = panel != null ? panel.Find("Stamina Label") : null;
            if (staminaBar == null)
            {
                return "Focus bar: the vitals HUD has no Stamina Bar.";
            }

            if (panel.Find("Focus Bar") != null)
            {
                return "Focus bar: already there.";
            }

            // Make room: the panel grows and everything below the stamina bar moves down one row.
            var stamina = (RectTransform)staminaBar;
            float below = stamina.anchoredPosition.y;
            var panelRect = (RectTransform)panel;
            panelRect.sizeDelta += new Vector2(0f, Row);
            foreach (RectTransform child in panel.Cast<Transform>().OfType<RectTransform>())
            {
                if (child.anchorMin.y > 0.99f && child.anchorMax.y > 0.99f && child.anchoredPosition.y < below - 1f)
                {
                    child.anchoredPosition += new Vector2(0f, -Row);
                }
            }

            var bar = (RectTransform)Object.Instantiate(stamina, panel);
            bar.name = "Focus Bar";
            bar.SetSiblingIndex(stamina.GetSiblingIndex() + 1);
            bar.anchoredPosition = stamina.anchoredPosition + new Vector2(0f, -(stamina.sizeDelta.y + 6f));
            bar.sizeDelta = new Vector2(stamina.sizeDelta.x, BarHeight);
            foreach (Transform cap in bar.Cast<Transform>().Where(t => t.name.StartsWith("Cap")))
            {
                ((RectTransform)cap).sizeDelta = Vector2.one * BarHeight * 0.55f;
            }

            Transform well = bar.Find("Well");
            Image fill = well.Find("Fill").GetComponent<Image>();
            Image trail = well.Find("Trail").GetComponent<Image>();
            fill.color = FocusColor;
            trail.color = new Color(1f, 0.93f, 0.75f, 0.55f);

            if (staminaLabel != null)
            {
                var label = (RectTransform)Object.Instantiate(staminaLabel, panel);
                label.name = "Focus Label";
                label.anchoredPosition = new Vector2(bar.anchoredPosition.x + bar.sizeDelta.x - label.sizeDelta.x, bar.anchoredPosition.y + 14f);
                if (label.TryGetComponent(out TMP_Text text))
                {
                    text.text = "Focus";
                    text.alignment = TextAlignmentOptions.Right;
                    text.fontSize *= 0.8f;
                    text.color = FocusColor;
                }
            }

            var view = new SerializedObject(root.GetComponentInChildren<PlayerVitalsView>(true));
            SerializedProperty focus = view.FindProperty("focus");
            focus.FindPropertyRelative("fill").objectReferenceValue = fill.rectTransform;
            focus.FindPropertyRelative("trail").objectReferenceValue = trail.rectTransform;
            focus.FindPropertyRelative("value").objectReferenceValue = null;
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Focus bar added to the vitals HUD.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
