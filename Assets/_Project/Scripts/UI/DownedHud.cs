using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// While this player is down on a raid together (<see cref="CoopDowned"/>): "Down" with the seconds left in the middle of
/// the screen, and, while a friend holds E beside them, who is helping and how far along they are. Hearthline's
/// frameless style. Made by <see cref="CoopSession"/> with the session.
/// </summary>
public sealed class DownedHud : MonoBehaviour
{
    private CanvasGroup group;
    private TMP_Text title;
    private TMP_Text detail;
    private RectTransform fill;
    private int shownSeconds = -1;
    private string shownHelper;

    public static DownedHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public static DownedHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Downed HUD");
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        Current = root.AddComponent<DownedHud>();
        Current.group = root.AddComponent<CanvasGroup>();
        Current.group.blocksRaycasts = false;
        Current.group.interactable = false;
        Current.group.alpha = 0f;
        Current.Build(root.transform);
        return Current;
    }

    public static void Remove()
    {
        if (Current != null)
        {
            Destroy(Current.gameObject);
            Current = null;
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        title = Label(root, "Title", new Vector2(0f, 120f), new Vector2(700f, 60f), 44f, style.Danger, true);
        detail = Label(root, "Detail", new Vector2(0f, 70f), new Vector2(900f, 34f), 22f, style.Text, false);
        var bar = (RectTransform)new GameObject("Help", typeof(RectTransform)).transform;
        bar.SetParent(root, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.sizeDelta = new Vector2(320f, 3f);
        bar.anchoredPosition = new Vector2(0f, 44f);
        bar.gameObject.AddComponent<Image>().color = style.Track;
        fill = (RectTransform)new GameObject("Fill", typeof(RectTransform)).transform;
        fill.SetParent(bar, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = style.Good;
    }

    private void Update()
    {
        CoopSession session = CoopSession.Current;
        bool down = CoopDowned.IsDown && session != null;
        group.alpha = down ? 1f : 0f;
        if (!down)
        {
            return;
        }

        CoopDowned downed = session.Downed;
        int seconds = Mathf.CeilToInt(downed.BleedOutLeft);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            title.text = $"Down · {seconds}";
        }

        float help = downed.HelpShare;
        string helper = help >= 0f ? downed.Helper : null;
        if (helper != shownHelper || string.IsNullOrEmpty(detail.text))
        {
            shownHelper = helper;
            detail.text = helper != null ? $"{helper} is helping you up" : "Crawl to a friend, or hold out for one to reach you";
        }

        fill.parent.gameObject.SetActive(help >= 0f);
        KitBar.Drive(fill, help, true);
    }

    private static TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, Color colour, bool titleFont)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        UIStyle.Current.OnWorld(text, titleFont);
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }
}
