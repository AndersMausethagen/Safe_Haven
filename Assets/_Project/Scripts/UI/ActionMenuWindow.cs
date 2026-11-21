using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One choice in an ActionMenuWindow: a label, a detail such as a cost, whether it can be chosen, and what it does.</summary>
public sealed class MenuAction
{
    public MenuAction(string label, string detail, bool enabled, Action chosen, bool closesMenu = false, QuestDefinition quest = null, QuestAnswer does = QuestAnswer.None)
    {
        Label = label;
        this.detail = detail;
        this.enabled = enabled;
        Chosen = chosen;
        ClosesMenu = closesMenu;
        Quest = quest;
        Does = does;
    }

    private readonly string detail;
    private readonly bool enabled;

    public string Label { get; }

    /// <summary>The detail, or, for a visitor in a friend's Haven (co-op) and a choice that is the host's (<see cref="HostOnly"/>), whose it is.</summary>
    public string Detail => RefusedHere ? HavenVisit.Refusal : detail;

    /// <summary>Whether it can be chosen: as made, and never by a visitor when it is the host's alone (every window asks this).</summary>
    public bool Enabled => enabled && !RefusedHere;

    /// <summary>
    /// The host's alone in a friend's Haven (co-op): a visitor sees it greyed, with whose it is, and can't choose it. Declared where the
    /// choice is made, read by every window through Enabled and Detail (Docs/BugPatterns.md, second round), instead of each choice
    /// refusing itself when chosen (its own check, HavenVisit.LeftToHost, stays behind it).
    /// </summary>
    public bool HostOnly { get; set; }

    private bool RefusedHere => HostOnly && HavenVisit.InHostsHaven;
    public Action Chosen { get; }
    public bool ClosesMenu { get; }
    /// <summary>The quest this answer is about, when spoken in a conversation (DialogueBox shows it in its rank's colour).</summary>
    public QuestDefinition Quest { get; }
    public QuestAnswer Does { get; }
    /// <summary>Where a window that sorts its choices puts this one (HorsePanelWindow: Main, Care, Gear, Name); empty for none.</summary>
    public string Group { get; set; } = "";
    /// <summary>The name of its icon, for a window that shows one (UIStyle.Icon, the UI kit); empty for none.</summary>
    public string Icon { get; set; } = "";
    /// <summary>A short state shown at its right by windows that have room ("Not enough", "Ready"); empty for none.</summary>
    public string Status { get; set; } = "";
}

/// <summary>Things a MenuPage shows as cards with their pictures and counts (KitHaul): a heading, then each item and essence.</summary>
public sealed class MenuHaul
{
    public string Title = "";
    /// <summary>The kit icon before the heading; empty for none.</summary>
    public string Icon = "";
    /// <summary>What goes before each count: "+" for what comes back, "" for a cost.</summary>
    public string Sign = "";
    /// <summary>The counts' colour; the text colour when not set.</summary>
    public Color? Tint;
    public IReadOnlyList<ItemAmount> Items = System.Array.Empty<ItemAmount>();
    public IReadOnlyList<ResourceAmount> Resources = System.Array.Empty<ResourceAmount>();
}

/// <summary>What an ActionMenuWindow shows: a title, a rich-text body and a list of choices.</summary>
public sealed class MenuPage
{
    public string Title = "";
    /// <summary>What is said, the story's voice (the portal's words): shown apart from the rest, in italics.</summary>
    public string Story = "";
    public string Body = "";
    /// <summary>A painting above the title (a raid event's scene), or null for none.</summary>
    public Sprite Picture;
    /// <summary>Things shown as pictures under the body (what taking a building down gives back, what an upgrade costs), or null.</summary>
    public MenuHaul Haul;
    public readonly List<MenuAction> Actions = new List<MenuAction>();

    public MenuPage Add(MenuAction action)
    {
        Actions.Add(action);
        return this;
    }
}

/// <summary>
/// A small reusable window for talking to things in the world: the portal, the portal, the Purification
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

    // A page's picture sits above its title, at most this tall, its width as the panel allows.
    private const float PictureHeight = 250f;

    private readonly List<ActionRowView> rows = new List<ActionRowView>();
    private Image picture;
    private Func<MenuPage> source;
    private Action closed;
    private Transform anchor;
    private bool hadAnchor;
    private GameObject viewer;
    private float range;
    private float nextRefresh;

    /// <summary>The menu window in the loaded scene, or null.</summary>
    public static ActionMenuWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public bool IsOpen => window != null && window.IsOpen;

    /// <summary>True while it is open on what stands at <paramref name="near"/> (a horse's menu: CoopHorses).</summary>
    public bool IsOpenAt(Transform near) => IsOpen && near != null && anchor == near;

    private void Awake()
    {
        Current = this;
        rowTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Close);
        AddPicture();
        AddHaul();
        window.OpenChanged += OnOpenChanged;
    }

    private void OnDestroy()
    {
        OnOpenChanged(false);
        if (Current == this)
        {
            Current = null;
        }

        window.OpenChanged -= OnOpenChanged;
    }

    private void OnDisable()
    {
        if (window != null && window.IsOpen) window.Close();
        OnOpenChanged(false);
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
        hadAnchor = near != null;
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

        picture.sprite = page.Picture;
        picture.gameObject.SetActive(page.Picture != null);
        // Keys named by their actions ({key:Interact}) become the keys bound now.
        titleLabel.text = GameInput.WithRebinds(page.Title);
        // Hearthline 3: its numbers stand out (HearthKit.ToneNumbers).
        string story = string.IsNullOrWhiteSpace(page.Story) ? "" : $"<i>{page.Story}</i>\n\n";
        bodyLabel.text = story + HearthKit.ToneNumbers(GameInput.WithRebinds(page.Body));
        bodyLabel.gameObject.SetActive(!string.IsNullOrEmpty(page.Body));
        ShowHaul(page.Haul);
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

    // The page's things as cards, one more child of the panel's layout, under the body (Hearthline 3: pictures, not a list of words).
    private const float HaulCard = 64f;
    private KitHaul haul;
    private LayoutElement haulLayout;

    private void AddHaul()
    {
        var holder = new GameObject("Haul", typeof(RectTransform));
        holder.transform.SetParent(bodyLabel.transform.parent, false);
        holder.transform.SetSiblingIndex(bodyLabel.transform.GetSiblingIndex() + 1);
        haulLayout = holder.AddComponent<LayoutElement>();
        haul = new KitHaul((RectTransform)holder.transform, "Things", 0f, HaulCard);
        holder.SetActive(false);
    }

    private void ShowHaul(MenuHaul shown)
    {
        GameObject holder = haulLayout.gameObject;
        bool any = shown != null && (shown.Items.Count > 0 || shown.Resources.Count > 0);
        holder.SetActive(any);
        if (!any)
        {
            return;
        }

        // Laid out first, so the cards are spread over the width the panel gives it.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)holder.transform.parent);
        float height = haul.Show(shown.Title, string.IsNullOrEmpty(shown.Icon) ? null : shown.Icon, shown.Tint ?? UIStyle.Current.Text, shown.Sign,
            shown.Items, shown.Resources, null);
        haulLayout.preferredHeight = height;
        haulLayout.minHeight = height;
    }

    // Built here rather than in the prefab: one more child of the panel's layout, just above the title.
    private void AddPicture()
    {
        var holder = new GameObject("Picture", typeof(RectTransform));
        holder.transform.SetParent(titleLabel.transform.parent, false);
        holder.transform.SetSiblingIndex(titleLabel.transform.GetSiblingIndex());
        picture = holder.AddComponent<Image>();
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        var layout = holder.AddComponent<LayoutElement>();
        layout.preferredHeight = PictureHeight;
        layout.minHeight = PictureHeight;
        holder.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // A menu of something that is gone (a horse sold, a building taken down) closes: a destroyed anchor read as "no anchor",
        // and the menu stayed, rebuilding against the dead object.
        if (hadAnchor && (anchor == null || viewer == null || HorizontalDistance(viewer.transform.position, anchor.position) > range))
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
            hadAnchor = false;
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
