using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A bar over every building at work on a queue (the user, 2026-11-19: queued work should show its progress over the building, as
/// its station window shows it): the Hearthline bar the window draws (KitBar), what is being made and how many wait after it, and
/// "full" when its output has no room. Over buildings in front of the camera and within <see cref="Reach"/> metres only, and not
/// while a window is open (the station's own window shows the same). It only reads CraftingStation.Active and their queues, and the
/// Purification Altar's batches (SettlementState), which work the same way (the user's playtest, 2026-11-21: "Purification altar
/// should also show a progress bar as the other buildings does").
/// </summary>
public sealed class StationProgressBars : MonoBehaviour
{
    private const float Reach = 45f;
    private const float BarWidth = 130f;
    private const float BarHeight = 8f;
    private const float AboveRoof = 0.6f;
    // Pixels (at the reference size) the bar keeps below the screen's top edge, clear of the clock and the region name.
    private const float ScreenTop = 150f;

    private sealed class Widget
    {
        public RectTransform Root;
        public RectTransform Fill;
        public Image FillImage;
        public TMP_Text Label;
    }

    private readonly List<Widget> widgets = new List<Widget>();
    private readonly Dictionary<Component, float> roofs = new Dictionary<Component, float>();
    private RectTransform area;
    private GameObject root;

    /// <summary>The one on the player, made the first time.</summary>
    public static StationProgressBars For(GameObject player) =>
        player.TryGetComponent(out StationProgressBars bars) ? bars : player.AddComponent<StationProgressBars>();

    private void Awake()
    {
        root = new GameObject("Station Progress Bars", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;
        UIRoot.ConfigureScaler(root.GetComponent<CanvasScaler>());
        area = (RectTransform)root.transform;
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root);
        }
    }

    private void OnDisable()
    {
        foreach (Widget widget in widgets)
        {
            widget.Root.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        int used = 0;
        Camera view = Camera.main;
        bool windowOpen = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        if (view != null && !windowOpen)
        {
            Vector3 from = transform.position;
            foreach (CraftingStation station in CraftingStation.Active)
            {
                StationQueue queue = station != null ? station.Queue : null;
                if (queue == null || queue.Current == null || (station.transform.position - from).sqrMagnitude > Reach * Reach)
                {
                    continue;
                }

                Vector3 over = station.transform.position + Vector3.up * RoofOf(station);
                Vector3 screen = view.WorldToScreenPoint(over);
                if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 at))
                {
                    continue;
                }

                // A tall building's top can be off the top of the screen while its door is in view: the bar keeps inside it.
                Rect bounds = area.rect;
                at.x = Mathf.Clamp(at.x, bounds.xMin + BarWidth, bounds.xMax - BarWidth);
                at.y = Mathf.Min(at.y, bounds.yMax - ScreenTop);
                Widget widget = WidgetAt(used++);
                widget.Root.anchoredPosition = at;
                float share = Mathf.Clamp01(queue.Progress / Mathf.Max(0.01f, queue.Current.CraftSeconds));
                UIStyle style = UIStyle.Current;
                KitBar.Set(widget.Fill, widget.FillImage, share, queue.Blocked ? style.Danger : style.Accent);
                int waiting = queue.QueuedCount - 1;
                string made = queue.Current.DisplayName;
                widget.Label.text = queue.Blocked ? $"{made}  <color={style.DangerHex}>full</color>" : waiting > 0 ? $"{made}  +{waiting}" : made;
            }

            used = ShowAltars(view, from, used);
        }

        for (int i = used; i < widgets.Count; i++)
        {
            widgets[i].Root.gameObject.SetActive(false);
        }
    }

    // The altar's batches: the one under way and how many wait; "needs a Purifier" while nobody works it (the batch doesn't move).
    private int ShowAltars(Camera view, Vector3 from, int used)
    {
        IHavenView settlement = GameSession.Here;
        SettlementSettings settings = SettlementSettings.Current;
        if (settlement == null || settings == null || settlement.PurificationQueued <= 0 || HavenVisit.InHostsHaven)
        {
            return used;
        }

        foreach (PurificationAltar altar in PurificationAltar.Standing)
        {
            if (altar == null || (altar.transform.position - from).sqrMagnitude > Reach * Reach
                || !Place(view, altar.transform.position + Vector3.up * RoofOf(altar), out Vector2 at))
            {
                continue;
            }

            Widget widget = WidgetAt(used++);
            widget.Root.anchoredPosition = at;
            UIStyle style = UIStyle.Current;
            bool staffed = settlement.HasWorker(settings.PurifierJob);
            float share = Mathf.Clamp01(settlement.PurificationProgress / Mathf.Max(0.01f, settings.SecondsPerBatch));
            KitBar.Set(widget.Fill, widget.FillImage, share, staffed ? style.Accent : style.Danger);
            int waiting = settlement.PurificationQueued - 1;
            string made = settings.PurifiedEssence != null ? settings.PurifiedEssence.DisplayName : "Purifying";
            string worker = settings.PurifierJob != null ? settings.PurifierJob.DisplayName : "worker";
            widget.Label.text = !staffed ? $"{made}  <color={style.DangerHex}>needs a {worker}</color>" : waiting > 0 ? $"{made}  +{waiting}" : made;
        }

        return used;
    }

    // Where over the screen a point stands, kept inside it; false behind the camera or off the bar's area.
    private bool Place(Camera view, Vector3 over, out Vector2 at)
    {
        Vector3 screen = view.WorldToScreenPoint(over);
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out at))
        {
            at = default;
            return false;
        }

        Rect bounds = area.rect;
        at.x = Mathf.Clamp(at.x, bounds.xMin + BarWidth, bounds.xMax - BarWidth);
        at.y = Mathf.Min(at.y, bounds.yMax - ScreenTop);
        return true;
    }

    // Metres above its foot that the bar stands: the top of what is drawn, worked out once per building.
    private float RoofOf(Component station)
    {
        if (roofs.TryGetValue(station, out float roof))
        {
            return roof;
        }

        roof = 3f;
        bool any = false;
        Bounds drawn = default;
        foreach (Renderer part in station.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled || part is ParticleSystemRenderer)
            {
                continue;
            }

            if (any)
            {
                drawn.Encapsulate(part.bounds);
            }
            else
            {
                drawn = part.bounds;
                any = true;
            }
        }

        if (any)
        {
            roof = drawn.max.y - station.transform.position.y + AboveRoof;
        }

        roofs[station] = roof;
        return roof;
    }

    private Widget WidgetAt(int index)
    {
        while (widgets.Count <= index)
        {
            widgets.Add(MakeWidget());
        }

        Widget widget = widgets[index];
        widget.Root.gameObject.SetActive(true);
        return widget;
    }

    private Widget MakeWidget()
    {
        UIStyle style = UIStyle.Current;
        var holder = new GameObject("Station Bar", typeof(RectTransform));
        holder.transform.SetParent(area, false);
        var rect = (RectTransform)holder.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(BarWidth, BarHeight + 22f);
        var widget = new Widget { Root = rect };
        widget.Fill = KitBar.Make(rect, 0f, 0f, BarWidth, BarHeight, out widget.FillImage);
        RectTransform track = (RectTransform)widget.Fill.parent;
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.anchoredPosition = Vector2.zero;
        var labelHolder = new GameObject("Label", typeof(RectTransform));
        labelHolder.transform.SetParent(rect, false);
        var labelRect = (RectTransform)labelHolder.transform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(BarWidth + 120f, 20f);
        labelRect.anchoredPosition = new Vector2(0f, BarHeight + 2f);
        TextMeshProUGUI label = labelHolder.AddComponent<TextMeshProUGUI>();
        if (style.BodyFont != null)
        {
            label.font = style.BodyFont;
        }

        label.fontSize = 15f;
        label.color = style.Text;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        style.OnWorld(label);
        label.raycastTarget = false;
        widget.Label = label;
        foreach (Graphic graphic in holder.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        return widget;
    }
}
