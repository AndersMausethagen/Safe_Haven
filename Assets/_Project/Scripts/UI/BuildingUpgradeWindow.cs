using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A building's Upgrade window: its painting and name over the same Upgrade tab a station's window has (StationUpgradeTab: the next tier,
/// the module slots and what fits), in a gilded window of its own. It is for what has no station window, such as a Hut, the Watch Post or
/// the altar, and for the build menu's Manage › Upgrade (the user, 2026-11-21: "proper art work. Not just boxes of text and plain
/// buttons"). A wall piece keeps its action-menu page, with its gate and torch (ConstructionUpgradeOffer). It closes when the building
/// is gone.
/// </summary>
public sealed class BuildingUpgradeWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 760f;
    private const float Margin = 40f;
    private const float PageTop = 168f;
    private const float RefreshSeconds = 0.25f;

    private static BuildingUpgradeWindow current;

    private UIWindow window;
    private RectTransform panel;
    private Image picture;
    private TMP_Text nameLabel;
    private TMP_Text kindLabel;
    private StationUpgradeTab tab;
    private BuildingObject building;
    private GameObject user;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>True while it is open.</summary>
    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Opens it on <paramref name="shown"/> for <paramref name="who"/> (whose bag and chests pay).</summary>
    public static void Show(BuildingObject shown, GameObject who)
    {
        if (shown == null || who == null)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Building Upgrade Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<BuildingUpgradeWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        current.building = shown;
        current.user = who;
        // Open first: its labels are measured as they are filled, which they can't be while hidden.
        current.window.Open();
        HearthKit.FitToScreen(current.panel, (RectTransform)current.transform, Width, Height);
        current.Refresh();
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        panel = HearthKit.Window(root, Width, Height, () => window.Close(), out RectTransform content);
        RectTransform frame = HearthKit.Card(panel, "Picture Frame", Margin, 30f, 120f, 120f, 0.7f, style.Inset, 1.6f);
        picture = CodeUI.At("Picture", frame, 8f, 8f, 104f, 104f).gameObject.AddComponent<Image>();
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        nameLabel = HearthKit.DisplayName(panel, Margin + 144f, 34f, Width - Margin * 2f - 220f, 46f);
        kindLabel = CodeUI.Label("Kind", panel, Margin + 148f, 116f, 600f, 28f, 19f, style.Accent);
        HearthKit.Rule(panel, Margin, PageTop - 14f, Width - Margin * 2f);
        RectTransform page = CodeUI.At("Upgrade Page", panel, Margin, PageTop, Width - Margin * 2f, Height - PageTop - 34f);
        tab = new StationUpgradeTab(page, Refresh);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.BuildingUpgrade, content.gameObject);
        window.OpenChanged += open => PlayerHold.Set(this, open);
    }

    private void Update()
    {
        if (window == null || !window.IsOpen)
        {
            return;
        }

        // Taken down, or a scaffold that was never finished cancelled: nothing left to upgrade.
        if (building == null || user == null)
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
        if (building == null || building.Definition == null || !building.TryGetComponent(out Construction construction))
        {
            return;
        }

        picture.sprite = building.Definition.Icon;
        picture.enabled = picture.sprite != null;
        nameLabel.text = construction.Title;
        kindLabel.text = HearthKit.ToneNumbers($"Upgrade  ·  Tier {Tiers.ToRoman(Mathf.Max(1, construction.Level))} of {Tiers.ToRoman(building.Definition.MaxLevel)}");
        tab.Show(building, user);
    }
}
