using UnityEngine;

/// <summary>
/// Keeps a column laid out from its top (the start menu's) wholly on screen: it is made as tall as its children reach, centred in its
/// parent's height, and shrunk evenly when that is taller than the parent allows. The start menu's six buttons ran past a 16:9 screen
/// (found 2026-11-27: the title cut at the top and Leave the Forest under the screen's foot at 1920x1080). Checked when the screen's
/// size changes; added by MainMenuView.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class FitColumnToScreen : MonoBehaviour
{
    [Tooltip("Room kept free above and below the column, in canvas units: clear of the line of words at the screen's foot.")]
    [SerializeField, Min(0f)] private float margin = 64f;

    private Vector2 fittedFor;

    public float Margin { get => margin; set { margin = Mathf.Max(0f, value); fittedFor = default; } }

    private void LateUpdate()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null || parent.rect.size == fittedFor)
        {
            return;
        }

        fittedFor = parent.rect.size;
        Fit(parent.rect.height);
    }

    /// <summary>Lays the column out for a parent <paramref name="room"/> tall.</summary>
    public void Fit(float room)
    {
        var column = (RectTransform)transform;
        float reach = Reach(column);
        column.sizeDelta = new Vector2(column.sizeDelta.x, reach);
        column.localScale = Vector3.one * Scale(reach, room, margin);
        // Pivot at its middle-left (as the menu's is): centred in the parent's height.
        column.anchoredPosition = new Vector2(column.anchoredPosition.x, 0f);
    }

    /// <summary>How much a column <paramref name="reach"/> tall must shrink to fit <paramref name="room"/> with
    /// <paramref name="margin"/> above and below: 1 when it fits. Pure, for tests.</summary>
    public static float Scale(float reach, float room, float margin) =>
        reach <= 0f ? 1f : Mathf.Clamp(Mathf.Max(1f, room - 2f * margin) / reach, 0.3f, 1f);

    // How far below the column's top its lowest active child reaches (children laid from the top).
    private static float Reach(RectTransform column)
    {
        float lowest = 0f;
        foreach (RectTransform child in column)
        {
            if (!child.gameObject.activeSelf)
            {
                continue;
            }

            float bottom = -child.anchoredPosition.y + child.rect.height * child.pivot.y;
            lowest = Mathf.Max(lowest, bottom);
        }

        return lowest;
    }
}
