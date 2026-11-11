using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Where the player is in the Haven's lands (Territory): crossing into a region shows its name large for a few seconds, with how
/// the Haven stands there and how dangerous it is; and the region's name sits small above the minimap all the while. Made for
/// every scene with a player and the Haven's atlas (ExperienceTracker); it only reads.
/// </summary>
public sealed class RegionTitleHud : MonoBehaviour
{
    private const float CheckSeconds = 0.5f;
    private const float ShowSeconds = 4.5f;
    private const float FadeSeconds = 1.2f;
    // Above the minimap (MinimapHud: 26 from the corner, 236 across by default).
    private const float MinimapTop = 26f + 236f + 8f;

    private TMP_Text title;
    private TMP_Text subtitle;
    private CanvasGroup titleGroup;
    private TMP_Text corner;
    private string shownRegion;
    private float shownAt = -100f;
    private float nextCheck;

    public static RegionTitleHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public static void Create(string sceneName)
    {
        if (Current != null || sceneName != Territory.HomeScene || Territory.Atlas == null)
        {
            return;
        }

        var root = new GameObject("Region Title");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        Current = root.AddComponent<RegionTitleHud>();
        Current.Build(root.transform);
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform band = CodeUI.Box("Title", root, new Vector2(0.5f, 1f), new Vector2(900f, 110f), new Vector2(0f, -150f));
        titleGroup = band.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleGroup.blocksRaycasts = false;
        title = CodeUI.Label("Name", band, 0f, 0f, 900f, 60f, 40f, style.Text, TextAlignmentOptions.Center);
        style.OnWorld(title, true);
        title.characterSpacing = 10f;
        subtitle = CodeUI.Label("Standing", band, 0f, 62f, 900f, 34f, 20f, style.Muted, TextAlignmentOptions.Center);
        style.OnWorld(subtitle);

        RectTransform small = CodeUI.Box("Here", root, new Vector2(1f, 0f), new Vector2(236f, 26f), new Vector2(-26f - 118f, MinimapTop + 13f));
        corner = small.gameObject.AddComponent<TextMeshProUGUI>();
        style.OnWorld(corner, true);
        corner.fontSize = 16f;
        corner.characterSpacing = 4f;
        corner.alignment = TextAlignmentOptions.Center;
        corner.color = style.Text;
        corner.raycastTarget = false;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>())
        {
            text.raycastTarget = false;
        }
    }

    private void Update()
    {
        // Out of the way while a window is up, as the minimap is.
        bool covered = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        corner.enabled = !covered;
        float since = Time.time - shownAt;
        titleGroup.alpha = covered ? 0f
            : since < FadeSeconds ? since / FadeSeconds
            : since < ShowSeconds ? 1f
            : Mathf.Clamp01(1f - (since - ShowSeconds) / FadeSeconds);
        if (Time.time < nextCheck || Player.Current == null)
        {
            return;
        }

        nextCheck = Time.time + CheckSeconds;
        string here = Territory.RegionAt(Player.Current.transform.position)?.name;
        if (here == null)
        {
            return;
        }

        corner.text = here.ToUpperInvariant();
        if (here == shownRegion)
        {
            return;
        }

        bool first = shownRegion == null;
        shownRegion = here;
        // Not on arriving: the player knows they are home.
        if (first)
        {
            return;
        }

        TerritorySettings.RegionInfo info = TerritorySettings.Current.Info(here);
        title.text = here.ToUpperInvariant();
        subtitle.text = info != null ? $"{Territory.Describe(here)}  ·  {info.danger}  ·  {info.gives}" : Territory.Describe(here);
        shownAt = Time.time;
    }
}
