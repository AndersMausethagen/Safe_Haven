using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The state of a ward breach while it runs: the wave, how many are left and how much the ward can still take. Type and a thin
/// line under the clock, as the rest of the HUD. Only reads WardBreachDirector. Made when a breach begins.
/// </summary>
public sealed class BreachHud : MonoBehaviour
{
    private const float Width = 340f;
    private const float FromTop = 138f;
    private const float WardInDanger = 0.3f;

    private CanvasGroup group;
    private TMP_Text title;
    private TMP_Text progress;
    private RectTransform fill;
    private Image fillImage;

    public static BreachHud Current { get; private set; }

    public static BreachHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Breach HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var hud = root.AddComponent<BreachHud>();
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
        RectTransform box = Box("Breach", root, new Vector2(0.5f, 1f), new Vector2(Width, 56f), new Vector2(0f, -FromTop));
        title = Label("Title", box, 0f, 0f, Width, 26f, 20f, style.Accent, TextAlignmentOptions.Center);
        style.OnWorld(title, true);
        RectTransform bar = At("Ward", box, 0f, 30f, Width, 3f);
        bar.gameObject.AddComponent<Image>().color = style.Track;
        fill = Stretch("Fill", bar);
        fillImage = fill.gameObject.AddComponent<Image>();
        progress = Label("Progress", box, 0f, 35f, Width, 24f, 17f, style.Text, TextAlignmentOptions.Center);
        style.OnWorld(progress);
    }

    private void LateUpdate()
    {
        WardBreachDirector breach = WardBreachDirector.Current;
        bool running = breach != null && breach.IsActive;
        bool windows = UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen;
        group.alpha = running && !windows && !GamePause.IsPaused ? 1f : 0f;
        if (!running)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        title.text = breach.Definition.DisplayName;
        int left = breach.AttackersLeft;
        progress.text = $"Wave {breach.Wave} of {breach.WaveCount}  ·  {left} left  ·  Ward {Mathf.CeilToInt(breach.WardHealth * 100f)}%";
        fill.anchorMax = new Vector2(breach.WardHealth, 1f);
        fillImage.color = breach.WardHealth <= WardInDanger ? style.Danger : style.Accent;
    }
}
