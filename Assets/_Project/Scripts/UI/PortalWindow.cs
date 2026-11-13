using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Haven portal's window (the user's picture, 2026-11-13: the portal as the window's motif), in place of the menu of
/// lines: its painting tall on the left (Resources/WindowArt/Portal), and on the right its name in the display face, what
/// it says (its voice, in italics, apart from the rest), what it tells of the ward, the task in hand (the first open step
/// of the quest at the top of the tracker, and how far its marker is), what it offers as rows with an icon, a line and
/// a state on the right ("Not enough" in red), the Haven's own card ("How is Safe Haven doing?") and Close. Its page comes
/// from HomePortalMenu (MenuPage: Story, Body, actions with Group, Icon and Status), rebuilt every half second and after
/// a choice; it closes when the player walks away. Made of the UI kit; built in code the first time it opens.
/// </summary>
public sealed class PortalWindow : MonoBehaviour
{
    private const float Width = 1200f;
    private const float MinHeight = 700f;
    private const float Margin = 30f;
    private const float PaintingWidth = 400f;
    private const float RightX = Margin + PaintingWidth + 28f;
    private const float RightWidth = Width - RightX - 36f;
    private const float RowHeight = 66f;
    private const float RefreshSeconds = 0.5f;

    private static PortalWindow current;

    private UIWindow window;
    private RectTransform panel;
    private RectTransform painting;
    private TMP_Text title;
    private Image rule;
    private TMP_Text story;
    private TMP_Text info;
    private RectTransform task;
    private TMP_Text taskLine;
    private TMP_Text taskQuest;
    private TMP_Text taskDistance;
    private readonly List<KitButton> rows = new List<KitButton>();
    private RectTransform haven;
    private TMP_Text havenTitle;
    private TMP_Text havenLine;
    private Button havenButton;
    private RectTransform closeButton;

    private Func<MenuPage> source;
    private GameObject viewer;
    private Transform anchor;
    private float range;
    private Action closed;
    private MenuAction havenAction;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Opens it on a page (rebuilt as it shows), for <paramref name="user"/>, until they are further than
    /// <paramref name="maxDistance"/> from <paramref name="near"/>; <paramref name="onClosed"/> once it closes.</summary>
    public static void Show(Func<MenuPage> pageSource, GameObject user, Transform near, float maxDistance, Action onClosed = null)
    {
        if (current == null)
        {
            var root = new GameObject("Portal Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 204;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<PortalWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        Action previous = current.closed;
        current.closed = onClosed;
        previous?.Invoke();
        current.source = pageSource;
        current.viewer = user;
        current.anchor = near;
        current.range = maxDistance;
        current.window.Open();
        current.Refresh();
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        panel = HearthKit.Window(root, Width, MinHeight, () => window.Close(), out RectTransform content);

        painting = HearthKit.Card(panel, "Painting", Margin, Margin, PaintingWidth, MinHeight - Margin * 2f, 0f, null, 1.5f);
        RectTransform inner = CodeUI.Stretch("Image", painting);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);
        inner.gameObject.AddComponent<RectMask2D>();
        RectTransform art = CodeUI.Box("Art", inner, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var image = art.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = Resources.Load<Sprite>("WindowArt/Portal");
        var fit = art.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = image.sprite != null ? image.sprite.rect.width / image.sprite.rect.height : 0.75f;
        HearthKit.Rim(painting, 0.85f, 1.5f).transform.SetAsLastSibling();

        title = HearthKit.DisplayName(panel, RightX, 14f, RightWidth - 60f, 46f);
        rule = HearthKit.Rule(panel, RightX, 102f, RightWidth * 0.75f, 0.55f);
        story = CodeUI.Paragraph("Story", panel, RightX, 118f, RightWidth, 100f, 21f, style.Text);
        style.Title(story);
        story.fontStyle = FontStyles.Italic;
        info = CodeUI.Paragraph("Info", panel, RightX, 230f, RightWidth, 60f, 18f, style.Muted);

        task = HearthKit.Card(panel, "Task", RightX, 300f, RightWidth, 70f, 0.75f, Color.Lerp(style.Inset, style.AccentDeep, 0.22f), 1.3f);
        Image diamond = CodeUI.At("Diamond", task, 22f, 26f, 16f, 16f).gameObject.AddComponent<Image>();
        diamond.color = style.AccentBright;
        diamond.raycastTarget = false;
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        taskLine = CodeUI.Label("Task", task, 56f, 8f, RightWidth - 200f, 30f, 21f, style.Text);
        taskQuest = CodeUI.Label("Quest", task, 56f, 38f, RightWidth - 200f, 24f, 16f, style.Accent);
        taskDistance = CodeUI.Label("Distance", task, RightWidth - 150f, 0f, 130f, 70f, 19f, style.AccentBright, TextAlignmentOptions.MidlineRight);

        haven = HearthKit.Card(panel, "Haven", RightX, 500f, RightWidth, 88f, 0.55f, style.Panel, 1.3f);
        var havenFill = haven.GetComponent<Image>();
        havenFill.raycastTarget = true;
        havenButton = haven.gameObject.AddComponent<Button>();
        havenButton.targetGraphic = havenFill;
        HearthKit.Tint(havenButton);
        UIHighlight.On(haven.gameObject);
        havenButton.onClick.AddListener(() => Choose(havenAction));
        Image mark = CodeUI.At("Hearthmark", haven, 18f, 12f, 64f, 64f).gameObject.AddComponent<Image>();
        mark.sprite = style.Hearthmark;
        mark.color = style.Accent;
        mark.preserveAspect = true;
        mark.raycastTarget = false;
        havenTitle = CodeUI.Label("Title", haven, 98f, 14f, RightWidth - 120f, 34f, 22f, style.Text);
        style.Title(havenTitle);
        havenTitle.fontStyle = FontStyles.Bold;
        havenLine = CodeUI.Label("Line", haven, 98f, 48f, RightWidth - 120f, 26f, 17f, style.Muted);

        TMP_Text closeLabel = CodeUI.AddButton("Close", panel, RightX + (RightWidth - 240f) * 0.5f, 600f, 240f, UIRole.Kind.Button, () => window.Close(), out Button close);
        closeLabel.text = "Close";
        style.Title(closeLabel);
        closeButton = (RectTransform)close.transform;

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Portal, content.gameObject);
        window.OpenChanged += open =>
        {
            if (!open)
            {
                Action done = closed;
                closed = null;
                source = null;
                done?.Invoke();
            }
        };
    }

    private void Update()
    {
        if (window == null || !window.IsOpen)
        {
            return;
        }

        if (viewer == null || anchor != null && (viewer.transform.position - anchor.position).sqrMagnitude > range * range)
        {
            window.Close();
            return;
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        MenuPage page = source != null ? source() : null;
        if (page == null)
        {
            window.Close();
            return;
        }

        UIStyle style = UIStyle.Current;
        title.text = page.Title;
        float y = 118f;
        story.gameObject.SetActive(!string.IsNullOrWhiteSpace(page.Story));
        if (story.gameObject.activeSelf)
        {
            story.text = "“" + GameInput.WithRebinds(page.Story.Trim()) + "”";
            y = Place(story, y) + 16f;
        }

        info.text = HearthKit.ToneNumbers(GameInput.WithRebinds(page.Body ?? "").Trim());
        info.gameObject.SetActive(info.text.Length > 0);
        if (info.gameObject.activeSelf)
        {
            y = Place(info, y) + 18f;
        }

        y = ShowTask(y);

        havenAction = null;
        var shown = new List<MenuAction>();
        foreach (MenuAction action in page.Actions)
        {
            if (action.Group == "Haven")
            {
                havenAction = action;
            }
            else
            {
                shown.Add(action);
            }
        }

        while (rows.Count < shown.Count)
        {
            rows.Add(new KitButton(panel, RightX, 0f, RightWidth, RowHeight, Choose, KitButtonShape.Row));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            MenuAction action = i < shown.Count ? shown[i] : null;
            if (action != null && string.IsNullOrEmpty(action.Icon))
            {
                action.Icon = HearthKit.IconForAction(action.Label) ?? "Star";
            }

            rows[i].Bind(action);
            if (action != null)
            {
                rows[i].Rect.anchoredPosition = new Vector2(RightX, -y);
                y += RowHeight + 10f;
            }
        }

        haven.gameObject.SetActive(havenAction != null);
        if (havenAction != null)
        {
            y += 8f;
            havenTitle.text = havenAction.Label;
            havenLine.text = GameInput.WithRebinds(havenAction.Detail ?? "");
            haven.anchoredPosition = new Vector2(RightX, -y);
            y += 88f;
        }

        y += 22f;
        closeButton.anchoredPosition = new Vector2(closeButton.anchoredPosition.x, -y);
        y += 46f + Margin;
        float height = Mathf.Max(MinHeight, y);
        panel.sizeDelta = new Vector2(Width, height);
        painting.sizeDelta = new Vector2(PaintingWidth, height - Margin * 2f);
        HearthKit.FitToScreen(panel, (RectTransform)transform, Width, height);
    }

    // A paragraph laid at y, as tall as its words; returns where it ends.
    private static float Place(TMP_Text text, float y)
    {
        text.rectTransform.anchoredPosition = new Vector2(RightX, -y);
        text.ForceMeshUpdate();
        float height = Mathf.Max(24f, text.preferredHeight);
        text.rectTransform.sizeDelta = new Vector2(RightWidth, height);
        return y + height;
    }

    // The task in hand: the first open step of the quest at the top of the tracker (the pinned one first), and the way to
    // its marker when it has one in this place.
    private float ShowTask(float y)
    {
        QuestLog.Entry entry = TopQuest(out int step);
        task.gameObject.SetActive(entry != null);
        if (entry == null)
        {
            return y;
        }

        QuestObjective objective = entry.Quest.Objectives[step];
        taskLine.text = GameInput.WithRebinds(objective.Amount > 1 ? $"{objective.Describe()}  {entry.Counts[step]}/{objective.Amount}" : objective.Describe());
        taskQuest.text = entry.Quest.DisplayName;
        taskDistance.text = "";
        foreach (QuestWaypoint marker in QuestWaypoint.Active)
        {
            if (marker != null && marker.Quest == entry.Quest && viewer != null)
            {
                float metres = Vector3.Distance(viewer.transform.position, marker.transform.position);
                taskDistance.text = $"{HearthKit.InlineIcon("Lead")} {metres:0} m";
                break;
            }
        }

        task.anchoredPosition = new Vector2(RightX, -y);
        return y + 70f + 16f;
    }

    private static QuestLog.Entry TopQuest(out int step)
    {
        step = -1;
        string pinned = GameSession.Progress.Journal.PinnedQuest;
        QuestLog.Entry best = null;
        foreach (QuestLog.Entry entry in GameSession.Quests.Entries)
        {
            if (entry.Status != QuestStatus.Active)
            {
                continue;
            }

            for (int i = 0; i < entry.Counts.Length; i++)
            {
                if (entry.IsObjectiveOpen(i))
                {
                    if (best == null || entry.Quest.Id == pinned)
                    {
                        best = entry;
                        step = i;
                    }

                    break;
                }
            }

            if (best != null && best.Quest.Id == pinned)
            {
                break;
            }
        }

        return best;
    }

    private void Choose(MenuAction action)
    {
        if (action == null || !action.Enabled)
        {
            return;
        }

        Func<MenuPage> before = source;
        action.Chosen?.Invoke();
        if (action.ClosesMenu)
        {
            window.Close();
        }
        else if (source == before && window.IsOpen)
        {
            Refresh();
        }
    }
}
