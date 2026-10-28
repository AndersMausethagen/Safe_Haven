using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bow's readout above the action bar, shown while a bow is in hand: the chosen arrows and how many are left
/// ("Crude Stone Arrow ×17 · Z"), a draw bar that fills while drawing and turns gold in the perfect release window, and
/// "Quick shot ready" while a perfect dodge has readied the instant shot. Hidden while the dialogue box is open. Made by CombatFeedback for the player in every
/// scene; it only reads PlayerArchery.
/// </summary>
public sealed class QuiverHud : MonoBehaviour
{
    private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color BarFill = new Color(1f, 0.97f, 0.9f, 0.9f);
    private static readonly Color BarPerfect = new Color(1f, 0.78f, 0.3f, 1f);
    private static readonly Color CounterColor = new Color(0.55f, 0.85f, 1f);

    private PlayerArchery archery;
    private CanvasGroup group;
    private TMP_Text arrows;
    private TMP_Text callout;
    private RectTransform bar;
    private Image fill;
    private ItemDefinition shownArrow;
    private int shownCount = -1;

    public static QuiverHud Create(PlayerArchery archery)
    {
        var go = new GameObject("Quiver HUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = go.AddComponent<QuiverHud>();
        hud.group = go.AddComponent<CanvasGroup>();
        hud.group.alpha = 0f;
        hud.group.interactable = false;
        hud.group.blocksRaycasts = false;
        hud.archery = archery;

        hud.arrows = Text("Arrows", go.transform, 22f, new Vector2(0f, 150f));
        hud.callout = Text("Callout", go.transform, 24f, new Vector2(0f, 205f));
        hud.callout.color = CounterColor;

        var back = new GameObject("Draw", typeof(RectTransform));
        back.transform.SetParent(go.transform, false);
        hud.bar = (RectTransform)back.transform;
        hud.bar.anchorMin = hud.bar.anchorMax = new Vector2(0.5f, 0f);
        hud.bar.sizeDelta = new Vector2(220f, 8f);
        hud.bar.anchoredPosition = new Vector2(0f, 178f);
        back.AddComponent<Image>().color = BarBack;
        var front = new GameObject("Fill", typeof(RectTransform));
        front.transform.SetParent(back.transform, false);
        var frontRect = (RectTransform)front.transform;
        frontRect.anchorMin = Vector2.zero;
        frontRect.anchorMax = new Vector2(0f, 1f);
        frontRect.pivot = new Vector2(0f, 0.5f);
        frontRect.offsetMin = frontRect.offsetMax = Vector2.zero;
        hud.fill = front.AddComponent<Image>();
        hud.fill.raycastTarget = false;
        return hud;
    }

    private static TMP_Text Text(string name, Transform parent, float size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = "";
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(700f, size * 1.4f);
        rect.anchoredPosition = position;
        return text;
    }

    private void LateUpdate()
    {
        if (archery == null)
        {
            Destroy(gameObject);
            return;
        }

        bool bow = archery.isActiveAndEnabled && archery.Bow != null && !GamePause.IsPaused && !DialogueBox.IsOpen;
        group.alpha = Mathf.MoveTowards(group.alpha, bow ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (!bow)
        {
            return;
        }

        ItemDefinition arrow = archery.NextArrow;
        int count = archery.Count(arrow);
        if (arrow != shownArrow || count != shownCount)
        {
            shownArrow = arrow;
            shownCount = count;
            arrows.text = arrow != null ? $"{arrow.DisplayName} ×{count}  <size=70%><alpha=#99>Z</size>" : "<alpha=#99>No arrows";
        }

        float draw = archery.Draw;
        bar.gameObject.SetActive(archery.IsDrawing);
        ((RectTransform)fill.transform).anchorMax = new Vector2(draw, 1f);
        fill.color = archery.InPerfectWindow ? BarPerfect : BarFill;
        callout.text = archery.CounterShotReady ? "Quick shot ready" : "";
    }
}
