using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// In Play: every shown button that covers the words of a label or another button it is not part of, on the screen as
/// drawn (the user, 2026-11-13: "some buttons lie over elements ... make sure that does not happen"). Run after any UI
/// change with the windows open: unity command script run -- --file AgentScripts/UIOverlapAudit.cs --entry UIOverlapAudit.Run
/// </summary>
public static class UIOverlapAudit
{
    private static Rect ScreenRect(RectTransform rect)
    {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private static bool Shown(Component c)
    {
        if (!c.gameObject.activeInHierarchy) return false;
        foreach (CanvasGroup g in c.GetComponentsInParent<CanvasGroup>())
            if (g.alpha < 0.05f) return false;
        return true;
    }

    private static string Name(Transform t) => t.parent != null ? t.parent.name + "/" + t.name : t.name;

    public static string Run()
    {
        var report = new StringBuilder();
        var buttons = Object.FindObjectsByType<Button>().Where(Shown).ToList();
        var labels = Object.FindObjectsByType<TMP_Text>().Where(Shown).Where(t => t.text.Trim().Length > 0).ToList();
        int found = 0;
        for (int bi = 0; bi < buttons.Count; bi++)
        {
            Button button = buttons[bi];
            var rect = (RectTransform)button.transform;
            Rect a = ScreenRect(rect);
            if (a.width < 2f || a.height < 2f) continue;
            foreach (TMP_Text label in labels)
            {
                if (label.transform.IsChildOf(button.transform) || button.transform.IsChildOf(label.transform)) continue;
                if (label.canvas == null || button.GetComponentInParent<Canvas>() == null || label.canvas.rootCanvas != button.GetComponentInParent<Canvas>().rootCanvas) continue;
                // The words themselves, not their box.
                Bounds b = label.textBounds;
                if (b.size.x < 1f) continue;
                Vector3 min = label.transform.TransformPoint(b.min), max = label.transform.TransformPoint(b.max);
                Rect words = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                Rect hit = Rect.MinMaxRect(Mathf.Max(a.xMin, words.xMin), Mathf.Max(a.yMin, words.yMin), Mathf.Min(a.xMax, words.xMax), Mathf.Min(a.yMax, words.yMax));
                if (hit.width > 3f && hit.height > 3f)
                {
                    found++;
                    report.Append($"button {Name(button.transform)} covers \"{label.text.Substring(0, Mathf.Min(24, label.text.Length)).Replace("\n", " ")}\" ({Name(label.transform)})\n");
                }
            }

            for (int oi = bi + 1; oi < buttons.Count; oi++)
            {
                Button other = buttons[oi];
                if (other.transform.IsChildOf(button.transform) || button.transform.IsChildOf(other.transform)) continue;
                Rect o = ScreenRect((RectTransform)other.transform);
                Rect hit = Rect.MinMaxRect(Mathf.Max(a.xMin, o.xMin), Mathf.Max(a.yMin, o.yMin), Mathf.Min(a.xMax, o.xMax), Mathf.Min(a.yMax, o.yMax));
                if (hit.width > 3f && hit.height > 3f)
                {
                    found++;
                    report.Append($"button {Name(button.transform)} overlaps button {Name(other.transform)}\n");
                }
            }
        }

        return found + " overlaps\n" + report;
    }
}
