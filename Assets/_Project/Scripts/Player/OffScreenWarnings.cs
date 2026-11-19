using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A warning at the screen's edge for a blow from out of frame (Docs/CombatFeelReview_2026-11-19.md §12; the third part of
/// Docs/GameDesignResearch_2026-11-17.md G1): while a creature out of view winds up a strike at this game's player, a chevron sits on
/// the rim on its side, in the strike's tier colour (a dark rim behind it for a great blow), growing and brightening with the wind-up.
/// It goes when the blow lands or the creature comes into view. Three at most; none while a window is open. Only during wind-ups at the
/// player, so it is no clutter outside a fight. Added to the player by Player.Awake (AddTo); reads AttackTelegraph.AtThePlayer.
/// </summary>
[DisallowMultipleComponent]
public sealed class OffScreenWarnings : MonoBehaviour
{
    private const int Most = 3;
    // How far past the edge a body still counts as seen (as MonsterAI's OffScreen).
    private const float Margin = 0.02f;

    private Player player;
    private Canvas canvas;
    private RectTransform[] arrows;
    private TextMeshProUGUI[] glyphs;
    private TextMeshProUGUI[] rims;

    public static void AddTo(GameObject player)
    {
        if (player != null && !player.TryGetComponent(out OffScreenWarnings _))
        {
            player.AddComponent<OffScreenWarnings>();
        }
    }

    private void Awake() => player = GetComponent<Player>();

    private void OnDisable() => HideFrom(0);

    private void OnDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    /// <summary>Whether a point in the viewport (as WorldToViewportPoint gives it) is out of view.</summary>
    public static bool IsOffScreen(Vector3 viewport) =>
        viewport.z < 0f || viewport.x < -Margin || viewport.x > 1f + Margin || viewport.y < -Margin || viewport.y > 1f + Margin;

    private void LateUpdate()
    {
        int shown = 0;
        Camera view = Camera.main;
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        if (player != null && Player.Current == player && view != null && !windows)
        {
            var strikes = AttackTelegraph.AtThePlayer;
            for (int i = 0; i < strikes.Count && shown < Most; i++)
            {
                AttackTelegraph strike = strikes[i];
                if (strike == null || !strike.IsWarning)
                {
                    continue;
                }

                Vector3 point = view.WorldToViewportPoint(strike.transform.position + Vector3.up * 1.2f);
                if (!IsOffScreen(point))
                {
                    continue;
                }

                Build();
                Show(shown++, point, strike);
            }
        }

        HideFrom(shown);
    }

    private void Show(int index, Vector3 point, AttackTelegraph strike)
    {
        // Toward it, on the rim (as StealthReadout's arrow): behind the camera, the other way.
        Vector2 toward = new Vector2(point.x - 0.5f, point.y - 0.5f) * (point.z < 0f ? -1f : 1f);
        toward = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector2.down;
        float reach = 0.46f / Mathf.Max(Mathf.Abs(toward.x), Mathf.Abs(toward.y));
        Vector2 viewport = new Vector2(0.5f, 0.5f) + toward * Mathf.Min(reach, 0.62f);
        RectTransform arrow = arrows[index];
        arrow.anchorMin = arrow.anchorMax = viewport;
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg - 90f);
        float progress = strike.Progress;
        arrow.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.25f, progress);
        Color color = strike.WarningColor;
        color.a = Mathf.Lerp(0.55f, 1f, progress);
        glyphs[index].color = color;
        bool heavy = strike.WarnsHeavy;
        rims[index].enabled = heavy;
        if (heavy)
        {
            Color rim = CombatFeelSettings.Current.HeavyEdgeColor;
            rim.a = color.a;
            rims[index].color = rim;
        }

        if (!arrow.gameObject.activeSelf)
        {
            arrow.gameObject.SetActive(true);
        }
    }

    private void HideFrom(int index)
    {
        if (arrows == null)
        {
            return;
        }

        for (int i = index; i < arrows.Length; i++)
        {
            // On a scene's unload the arrows' canvas can go first (an exception in the playtest build, twice, 2026-11-19).
            if (arrows[i] != null && arrows[i].gameObject.activeSelf)
            {
                arrows[i].gameObject.SetActive(false);
            }
        }
    }

    // Made the first time one is needed, kept out of the player's hierarchy (as the stealth arrow is).
    private void Build()
    {
        if (canvas != null)
        {
            return;
        }

        var canvasObject = new GameObject("Off Screen Warnings", typeof(Canvas), typeof(CanvasScaler));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 41;
        arrows = new RectTransform[Most];
        glyphs = new TextMeshProUGUI[Most];
        rims = new TextMeshProUGUI[Most];
        for (int i = 0; i < Most; i++)
        {
            var arrowObject = new GameObject($"Warning {i + 1}", typeof(RectTransform));
            arrows[i] = (RectTransform)arrowObject.transform;
            arrows[i].SetParent(canvasObject.transform, false);
            arrows[i].sizeDelta = new Vector2(72f, 72f);
            rims[i] = Glyph(arrows[i], "Rim", 66f);
            glyphs[i] = Glyph(arrows[i], "Chevron", 52f);
            arrowObject.SetActive(false);
        }
    }

    private static TextMeshProUGUI Glyph(RectTransform parent, string name, float size)
    {
        var glyphObject = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)glyphObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var glyph = glyphObject.AddComponent<TextMeshProUGUI>();
        glyph.text = "▲";
        glyph.fontSize = size;
        glyph.alignment = TextAlignmentOptions.Center;
        glyph.raycastTarget = false;
        return glyph;
    }
}
