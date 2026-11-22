using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Keeper's view's tooltip (KeeperHud): what a command, a person, a store or an alert is about, shown while the pointer is over
/// it, so the HUD itself can be pictures and a word (the rule: words on hover, never walls of text). A gold-rimmed card that sizes
/// itself to its words and sits beside what it is about, kept on the screen. A plain helper; KeeperHud owns it.
/// </summary>
public sealed class KeeperTip
{
    private const float Width = 340f;
    private const float Pad = 14f;

    private readonly RectTransform root;
    private readonly RectTransform canvas;
    private readonly TMP_Text title;
    private readonly TMP_Text body;

    public KeeperTip(RectTransform canvas)
    {
        UIStyle style = UIStyle.Current;
        this.canvas = canvas;
        root = HearthKit.Card(canvas, "Keeper Tip", 0f, 0f, Width, 80f, 0.6f, style.Panel);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        title = CodeUI.Label("Title", root, Pad, 10f, Width - Pad * 2f, 26f, 19f, style.AccentBright);
        title.overflowMode = TextOverflowModes.Overflow;
        title.textWrappingMode = TextWrappingModes.Normal;
        body = CodeUI.Paragraph("Body", root, Pad, 40f, Width - Pad * 2f, 40f, 15f, style.Text);
        body.overflowMode = TextOverflowModes.Overflow;
        root.gameObject.SetActive(false);
    }

    /// <summary>Shows <paramref name="heading"/> and <paramref name="words"/> beside <paramref name="near"/>: above it, or below it when
    /// <paramref name="below"/>, or to its side when <paramref name="beside"/> (-1 left, 1 right).</summary>
    public void Show(string heading, string words, RectTransform near, bool below = false, int beside = 0)
    {
        if (near == null || string.IsNullOrEmpty(heading))
        {
            Hide();
            return;
        }

        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        title.text = heading;
        body.text = HearthKit.ToneNumbers(words ?? "");
        float titleHeight = Mathf.Ceil(title.GetPreferredValues(heading, Width - Pad * 2f, 0f).y);
        float bodyHeight = string.IsNullOrEmpty(words) ? 0f : Mathf.Ceil(body.GetPreferredValues(body.text, Width - Pad * 2f, 0f).y);
        title.rectTransform.sizeDelta = new Vector2(Width - Pad * 2f, titleHeight);
        body.rectTransform.anchoredPosition = new Vector2(Pad, -(10f + titleHeight + 6f));
        body.rectTransform.sizeDelta = new Vector2(Width - Pad * 2f, bodyHeight);
        body.gameObject.SetActive(bodyHeight > 0f);
        float height = 10f + titleHeight + (bodyHeight > 0f ? 6f + bodyHeight : 0f) + 12f;
        root.sizeDelta = new Vector2(Width, height);

        // Where the thing is, in the canvas: its corners.
        var corners = new Vector3[4];
        near.GetWorldCorners(corners);
        Vector2 min = canvas.InverseTransformPoint(corners[0]);
        Vector2 max = canvas.InverseTransformPoint(corners[2]);
        Vector2 at = beside != 0
            ? new Vector2(beside > 0 ? max.x + 10f : min.x - 10f - Width, max.y)
            : new Vector2((min.x + max.x) * 0.5f - Width * 0.5f, below ? min.y - 10f : max.y + 10f + height);
        Vector2 size = canvas.rect.size;
        at.x = Mathf.Clamp(at.x, -size.x * 0.5f + 8f, size.x * 0.5f - Width - 8f);
        at.y = Mathf.Clamp(at.y, -size.y * 0.5f + height + 8f, size.y * 0.5f - 8f);
        root.anchoredPosition = at;
    }

    public void Hide()
    {
        if (root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(false);
        }
    }
}
