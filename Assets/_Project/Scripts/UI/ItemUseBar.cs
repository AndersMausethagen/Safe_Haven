using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Says what the player is using, above the noise meter over the action bar: "Using Healing Herb" over a thin bar that fills for as long
/// as a meal or draught takes (PlayerEating), or a quick full bar for what happens at once (gear put on, a lantern
/// refilled, a rune book read). It sits above open windows, so a right-click in the bag is seen to work. Built in code;
/// it only reads ItemInventory.ItemUsed and PlayerEating. Hearthline: type and a thin line in UIStyle's colours.
/// While the player harvests it shows how far the work has come too: a node's swings ("Pine 3/8", PlayerGathering's
/// session) or a timed job's share (picking, repairing: Interactable.Progress). The minigame's own timing bar is hidden
/// with hold to raise, and the world prompt hides while working, so nothing else showed it (the user, 2026-11-15:
/// "there's no progress bar while harvesting resources any more").
/// </summary>
public sealed class ItemUseBar : MonoBehaviour
{
    private const float BarWidth = 260f;
    private const float InstantSeconds = 0.45f;
    private const float FadeSeconds = 0.25f;

    private ItemInventory inventory;
    private PlayerEating eating;
    private PlayerGathering gathering;
    private PlayerInteractor interactor;
    private GameObject root;
    private CanvasGroup group;
    private TMP_Text label;
    private RectTransform fill;
    private string instantName;
    private float instantUntil = float.NegativeInfinity;
    private float shownAlpha;

    /// <summary>Adds the bar for a player (once).</summary>
    public static ItemUseBar For(GameObject player)
    {
        if (player == null)
        {
            return null;
        }

        return player.TryGetComponent(out ItemUseBar bar) ? bar : player.AddComponent<ItemUseBar>();
    }

    private void Awake()
    {
        inventory = GetComponent<ItemInventory>();
        eating = GetComponent<PlayerEating>();
        gathering = GetComponent<PlayerGathering>();
        interactor = GetComponent<PlayerInteractor>();
        Build();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.ItemUsed += OnUsed;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.ItemUsed -= OnUsed;
        }

        shownAlpha = 0f;
        if (group != null)
        {
            group.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root);
        }
    }

    private void OnUsed(ItemDefinition item, ItemContainer from, int slot)
    {
        // A meal shows its own progress; anything else is done at once and shown as done.
        if (eating != null && eating.IsEating && eating.Meal == item)
        {
            return;
        }

        instantName = item.DisplayName;
        instantUntil = Time.unscaledTime + InstantSeconds;
    }

    private void Update()
    {
        bool meal = eating != null && eating.IsEating && eating.Meal != null;
        bool instant = !meal && Time.unscaledTime < instantUntil;
        bool work = !meal && !instant && ShowWork();
        if (meal)
        {
            label.text = $"Using {eating.Meal.DisplayName}";
            SetFill(eating.Progress);
        }
        else if (instant)
        {
            label.text = $"Using {instantName}";
            SetFill(1f);
        }

        float target = meal || instant || work ? 1f : 0f;
        shownAlpha = Mathf.MoveTowards(shownAlpha, target, Time.unscaledDeltaTime / FadeSeconds);
        group.alpha = shownAlpha;
    }

    private void SetFill(float progress) => fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

    // Harvesting: a node's swings so far, or a timed job's share. False when the player is doing neither.
    private bool ShowWork()
    {
        Interactable job = interactor != null ? interactor.Current : null;
        GatheringSession session = gathering != null ? gathering.Session : null;
        if (session != null && session.SwingsRequired > 0)
        {
            string name = job != null ? WorldInteractionIndicator.NameOf(job) : null;
            label.text = string.IsNullOrEmpty(name) ? session.Counter : $"{name}  {session.Counter}";
            SetFill(session.Progress / session.SwingsRequired);
            return true;
        }

        if (job != null && !job.HoldOpen && job.Duration > 0f)
        {
            string verb = job.TryGetComponent(out IInteractionVerb named) && !string.IsNullOrEmpty(named.Verb) ? named.Verb : null;
            string name = WorldInteractionIndicator.NameOf(job);
            label.text = verb != null ? $"{verb} {name}" : name;
            SetFill(job.Progress);
            return true;
        }

        return false;
    }

    private void Build()
    {
        UIStyle style = UIStyle.Current;
        root = new GameObject("Item Use Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 430;
        UIRoot.ConfigureScaler(root.GetComponent<CanvasScaler>());
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var area = (RectTransform)root.transform;
        RectTransform track = Rect("Track", area, new Vector2(0.5f, 0f), new Vector2(BarWidth, 5f), new Vector2(0f, 275f));
        track.gameObject.AddComponent<Image>().color = style.Track;
        fill = Rect("Fill", track, Vector2.zero, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = style.Accent;

        label = Rect("Label", track, new Vector2(0.5f, 1f), new Vector2(BarWidth + 200f, 30f), new Vector2(0f, 18f)).gameObject.AddComponent<TextMeshProUGUI>();
        if (style.BodyFont != null)
        {
            label.font = style.BodyFont;
        }

        label.fontSize = 20f;
        label.color = style.Text;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        foreach (Image image in root.GetComponentsInChildren<Image>())
        {
            image.raycastTarget = false;
        }

        label.raycastTarget = false;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}
