using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One choice in an ActionMenuWindow: a label, a detail such as a cost, whether it can be chosen, and what it does.</summary>
public sealed class MenuAction
{
    public MenuAction(string label, string detail, bool enabled, Action chosen, bool closesMenu = false)
    {
        Label = label;
        Detail = detail;
        Enabled = enabled;
        Chosen = chosen;
        ClosesMenu = closesMenu;
    }

    public string Label { get; }
    public string Detail { get; }
    public bool Enabled { get; }
    public Action Chosen { get; }
    public bool ClosesMenu { get; }
}

/// <summary>What an ActionMenuWindow shows: a title, a rich-text body and a list of choices.</summary>
public sealed class MenuPage
{
    public string Title = "";
    public string Body = "";
    public readonly List<MenuAction> Actions = new List<MenuAction>();

    public MenuPage Add(MenuAction action)
    {
        Actions.Add(action);
        return this;
    }
}

/// <summary>
/// A small reusable window for talking to things in the world: the Heart Tree, the portal, residents, the Purification
/// Altar, the repair bench and the build menu all use it. The caller hands over a function that builds a MenuPage; the
/// window shows it, rebuilds it every half second (so costs and progress stay current) and after every choice, and
/// closes when the player walks away from the object it was opened at.
/// <para>
/// Where it belongs: one ActionMenuWindow prefab on the Safe Haven canvas set (UI/ActionMenuWindow). Systems find it through
/// <see cref="Current"/>. To give a new object a menu, open it with a page builder; no new UI is needed.
/// </para>
/// </summary>
public sealed class ActionMenuWindow : MonoBehaviour
{
    [SerializeField] private UIWindow window;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [Tooltip("Parent for the choice rows. Should have a vertical layout group.")]
    [SerializeField] private RectTransform actionList;
    [Tooltip("A hidden row that is copied for each choice.")]
    [SerializeField] private ActionRowView rowTemplate;
    [SerializeField] private Button closeButton;
    [Tooltip("Seconds between automatic refreshes while open.")]
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

    private readonly List<ActionRowView> rows = new List<ActionRowView>();
    private Func<MenuPage> source;
    private Action closed;
    private Transform anchor;
    private GameObject viewer;
    private float range;
    private float nextRefresh;

    /// <summary>The menu window in the loaded scene, or null.</summary>
    public static ActionMenuWindow Current { get; private set; }

    public bool IsOpen => window != null && window.IsOpen;

    private void Awake()
    {
        Current = this;
        rowTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Close);
        window.OpenChanged += OnOpenChanged;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }

        window.OpenChanged -= OnOpenChanged;
    }

    /// <summary>
    /// Opens the menu. The page is rebuilt from pageSource while open. With an anchor, the menu closes when the user is
    /// further than maxDistance meters from it. onClosed runs once when this page goes away: closed, walked away from or
    /// replaced by another Open.
    /// </summary>
    public void Open(Func<MenuPage> pageSource, GameObject user, Transform near = null, float maxDistance = 6f, Action onClosed = null)
    {
        Action previous = closed;
        closed = onClosed;
        previous?.Invoke();
        source = pageSource;
        viewer = user;
        anchor = near;
        range = maxDistance;
        Refresh();
        window.Open();
    }

    public void Close()
    {
        window.Close();
    }

    /// <summary>Rebuilds the page now.</summary>
    public void Refresh()
    {
        nextRefresh = Time.unscaledTime + refreshInterval;
        MenuPage page = source != null ? source() : null;
        if (page == null)
        {
            Close();
            return;
        }

        titleLabel.text = page.Title;
        bodyLabel.text = page.Body;
        bodyLabel.gameObject.SetActive(!string.IsNullOrEmpty(page.Body));
        while (rows.Count < page.Actions.Count)
        {
            ActionRowView row = Instantiate(rowTemplate, actionList);
            row.Clicked += OnRowClicked;
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < page.Actions.Count;
            rows[i].gameObject.SetActive(used);
            if (used)
            {
                rows[i].Bind(page.Actions[i]);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)actionList.parent);
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (anchor != null && (viewer == null || HorizontalDistance(viewer.transform.position, anchor.position) > range))
        {
            Close();
            return;
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh();
        }
    }

    private void OnRowClicked(MenuAction action)
    {
        if (action == null || !action.Enabled)
        {
            return;
        }

        action.Chosen?.Invoke();
        if (action.ClosesMenu)
        {
            Close();
        }
        else if (IsOpen)
        {
            Refresh();
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            source = null;
            anchor = null;
            viewer = null;
            Action onClosed = closed;
            closed = null;
            onClosed?.Invoke();
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
