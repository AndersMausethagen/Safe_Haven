using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// What the player needs to see while moving a transport (GDD §8.5), in type and a thin line above the noise meter: its
/// load against its capacity and how much it slows them, in words when overfilled. In a raid it stays up for a cart that
/// was let go, because that is the one that gets left behind: it says how far off it is and whether it would come home.
/// Built in code by the first transport in a scene; only reads PlayerTransport.
/// </summary>
public sealed class TransportHud : MonoBehaviour
{
    private const float Width = 300f;
    private const float Height = 180f;

    private CanvasGroup group;
    private TMP_Text title;
    private TMP_Text load;
    private TMP_Text status;
    private RectTransform fill;
    private Image fillImage;

    public static TransportHud Current { get; private set; }

    public static TransportHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Transport HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var hud = root.AddComponent<TransportHud>();
        hud.group = root.AddComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false;
        hud.Build(root.transform);
        return hud;
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform box = Box("Load", root, new Vector2(0.5f, 0f), new Vector2(Width, 56f), new Vector2(0f, Height));
        title = Label("Title", box, 0f, 0f, Width, 26f, 20f, style.Text);
        style.OnWorld(title, true);
        load = Label("Load", box, 0f, 2f, Width, 24f, 17f, style.Text, TextAlignmentOptions.Right);
        style.OnWorld(load);
        RectTransform bar = At("Bar", box, 0f, 30f, Width, 2f);
        bar.gameObject.AddComponent<Image>().color = style.Track;
        fill = Stretch("Fill", bar);
        fill.anchorMax = new Vector2(0f, 1f);
        fillImage = fill.gameObject.AddComponent<Image>();
        status = Label("Status", box, 0f, 34f, Width, 24f, 17f, style.Text);
        style.OnWorld(status);
    }

    /// <summary>Whether a cart in a raid comes home, in words. The hands always bring it.</summary>
    public static string RaidLine(bool held, float distance, float reach)
    {
        if (held)
        {
            return "Comes home with you";
        }

        return distance <= reach ? $"{distance:0} m away. In reach to come home" : $"{distance:0} m away. It stays behind if you leave";
    }

    private void LateUpdate()
    {
        PlayerTransport player = PlayerTransport.Current;
        Transport used = player != null ? player.Using : null;
        bool inRaid = RaidDirector.Current != null;
        float away = 0f;
        Transport cart = player != null && inRaid ? player.RaidCart(out away) : null;
        Transport shown = used != null ? used : cart;
        bool windows = UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen;
        group.alpha = shown != null && !windows && !GamePause.IsPaused ? 1f : 0f;
        if (shown == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        DrawLoad(shown, style);
        float reach = TransportSettings.Current.ExtractionRange;
        bool held = used != null && used == cart;
        status.text = cart != null ? RaidLine(held, away, reach) : "";
        status.color = held || away <= reach ? style.Text : style.Warning;
    }

    private void DrawLoad(Transport used, UIStyle style)
    {
        title.text = used.IsOverfilled ? "Overfilled" : used.Definition.DisplayName;
        title.color = used.IsOverfilled ? style.Warning : style.Accent;
        load.text = $"{used.Weight:0} / {used.Definition.Capacity:0}   <alpha=#AA>{(1f - used.SpeedScale) * 100f:0}% slower";
        fill.anchorMax = new Vector2(Mathf.Clamp01(used.Weight / used.Definition.Capacity), 1f);
        fillImage.color = used.IsOverfilled ? style.Warning : style.Accent;
    }
}
