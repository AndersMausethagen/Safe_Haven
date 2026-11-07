using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Points the way to the nearest wanted QuestWaypoint: a marker in the colour of its quest's rank over it with the distance while it is on screen,
/// an arrow at the edge of the screen pointing towards it when it isn't. Hidden when no quest has a waypoint to show,
/// while a window is open, and within a few metres of the spot. Made by the QuestTrackerView; it only reads the
/// waypoints and the QuestLog.
/// </summary>
public sealed class QuestWaypointHud : MonoBehaviour
{
    private const float EdgeMargin = 70f;
    private const float HideWithin = 4f;

    private RectTransform root;
    private RectTransform marker;
    private RectTransform arrow;
    private TMP_Text text;
    private Image diamond;
    private TMP_Text arrowMark;
    private Camera view;
    private QuestWaypoint writtenTarget;
    private string writtenLabel;
    private int writtenMetres = -1;

    public static QuestWaypointHud Create()
    {
        var go = new GameObject("Quest Waypoint HUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        UIRoot.ConfigureScaler(go.AddComponent<CanvasScaler>());
        var hud = go.AddComponent<QuestWaypointHud>();
        hud.root = (RectTransform)go.transform;

        hud.marker = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
        hud.marker.SetParent(go.transform, false);
        hud.marker.sizeDelta = new Vector2(30f, 30f);

        var diamondRect = new GameObject("Diamond", typeof(RectTransform)).GetComponent<RectTransform>();
        diamondRect.SetParent(hud.marker, false);
        diamondRect.sizeDelta = new Vector2(16f, 16f);
        diamondRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        hud.diamond = diamondRect.gameObject.AddComponent<Image>();
        hud.diamond.raycastTarget = false;

        hud.arrow = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
        hud.arrow.SetParent(hud.marker, false);
        hud.arrow.sizeDelta = new Vector2(34f, 34f);
        hud.arrowMark = hud.arrow.gameObject.AddComponent<TextMeshProUGUI>();
        hud.arrowMark.text = ">";
        hud.arrowMark.fontStyle = FontStyles.Bold;
        hud.arrowMark.fontSize = 30f;
        hud.arrowMark.alignment = TextAlignmentOptions.Center;
        hud.arrowMark.raycastTarget = false;

        var label = new GameObject("Distance", typeof(RectTransform)).GetComponent<RectTransform>();
        label.SetParent(hud.marker, false);
        label.sizeDelta = new Vector2(260f, 44f);
        label.anchoredPosition = new Vector2(0f, -34f);
        hud.text = label.gameObject.AddComponent<TextMeshProUGUI>();
        hud.text.fontSize = 18f;
        hud.text.alignment = TextAlignmentOptions.Center;
        hud.text.raycastTarget = false;
        hud.text.textWrappingMode = TextWrappingModes.NoWrap;
        return hud;
    }

    private void LateUpdate()
    {
        QuestWaypoint target = Pick(out float distance);
        bool show = target != null && distance > HideWithin && !GamePause.IsPaused
                    && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen);
        marker.gameObject.SetActive(show);
        if (!show)
        {
            return;
        }

        view = view != null && view.isActiveAndEnabled ? view : Camera.main;
        if (view == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = view.WorldToScreenPoint(target.transform.position + Vector3.up * 2.5f);
        float scale = root.localScale.x > 0f ? root.localScale.x : 1f;
        Vector2 size = root.sizeDelta;
        var local = new Vector2(screen.x / scale - size.x * 0.5f, screen.y / scale - size.y * 0.5f);
        bool behind = screen.z < 0f;
        if (behind)
        {
            local = -local;
        }

        Vector2 half = size * 0.5f - new Vector2(EdgeMargin, EdgeMargin);
        bool onScreen = !behind && Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        if (!onScreen)
        {
            // Push it to the screen's edge along the direction to the target.
            float push = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(local.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(local.y)));
            local *= push;
        }

        Color colour = target.Quest != null ? QuestMarkerSettings.Current.RankColor(target.Quest.Rank) : QuestMarkerSettings.Current.RankColor(QuestRank.MainStory);
        diamond.color = colour;
        arrowMark.color = colour;
        text.color = colour;
        marker.anchoredPosition = local;
        arrow.gameObject.SetActive(!onScreen);
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg);
        arrow.anchoredPosition = local.normalized * 26f;
        // Written again only when the target, its label or the whole metres change.
        int metres = Mathf.RoundToInt(distance);
        if (target != writtenTarget || metres != writtenMetres || target.Label != writtenLabel)
        {
            writtenTarget = target;
            writtenLabel = target.Label;
            writtenMetres = metres;
            text.text = $"{target.Label}\n{metres} m";
        }
    }

    // The nearest waypoint whose quest wants the player there.
    private static QuestWaypoint Pick(out float distance)
    {
        distance = 0f;
        Player player = Player.Current;
        if (player == null)
        {
            return null;
        }

        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        QuestWaypoint best = null;
        float nearest = float.MaxValue;
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint == null || !waypoint.IsWanted(log))
            {
                continue;
            }

            float d = Vector3.Distance(waypoint.transform.position, player.transform.position);
            if (d < nearest)
            {
                nearest = d;
                best = waypoint;
            }
        }

        distance = nearest;
        return best;
    }
}
