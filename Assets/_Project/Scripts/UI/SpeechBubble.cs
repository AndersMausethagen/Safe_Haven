using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A speech bubble over whoever is talking: their name, the line and the "E ›" hint, with a tail pointing down at them. It
/// follows their head on screen and stays inside it (the tail still points at them). Made and driven by DialogueBox, which
/// keeps the conversation's rules (typing, answers, input); this only draws. Hearthline: a solid panel with the rim, the
/// name in the accent colour.
/// </summary>
public sealed class SpeechBubble : MonoBehaviour
{
    // Smaller and see-through since 2026-11-13: the user found the lines too big and in the way while playing.
    private const float MaxLineWidth = 380f;
    private const float MinWidth = 140f;
    private const float PaddingX = 16f;
    private const float PaddingTop = 9f;
    private const float PaddingBottom = 12f;
    private const float NameHeight = 22f;
    private const float HintHeight = 16f;
    // Hearthline 3: nearly solid; see-through, the walnut went muddy over the sunlit meadow.
    private const float BackAlpha = 0.93f;
    private const float ScreenMargin = 16f;
    private const float AboveHead = 0.45f;
    private const float TailSize = 14f;
    // The speaker's painted portrait beside the line (the user, 2026-11-19: "a picture of the one talking ... with the 2D artwork").
    private const float PortraitSize = 84f;
    private const float PortraitGap = 12f;

    private RectTransform area;
    private RectTransform bubble;
    private RectTransform tail;
    private CanvasGroup group;
    private TMP_Text nameLabel;
    private TMP_Text lineLabel;
    private TMP_Text hintLabel;
    private RectTransform portraitFrame;
    private Image portraitImage;
    private Transform speaker;
    private Transform head;
    private float headHeight;

    /// <summary>The line's text: DialogueBox types it out through maxVisibleCharacters.</summary>
    public TMP_Text Line => lineLabel;

    /// <summary>The hint under the line ("E ›").</summary>
    public TMP_Text Hint => hintLabel;

    public bool IsShown => speaker != null;

    /// <summary>Builds a bubble on a screen-space overlay canvas (DialogueBox's).</summary>
    public static SpeechBubble Create(Transform canvas)
    {
        var holder = new GameObject("Speech Bubble", typeof(RectTransform), typeof(CanvasGroup));
        holder.transform.SetParent(canvas, false);
        var bubble = holder.AddComponent<SpeechBubble>();
        bubble.Build((RectTransform)canvas);
        bubble.Hide();
        return bubble;
    }

    /// <summary>Whether the last line shown has a portrait beside it.</summary>
    public bool HasPortrait => portraitFrame != null && portraitFrame.gameObject.activeSelf;

    /// <summary>Shows <paramref name="line"/> by <paramref name="speakerName"/> over <paramref name="over"/>, sized to the
    /// text (wrapped at a comfortable width), with <paramref name="portrait"/> framed on its left when given.</summary>
    public void Show(string speakerName, string line, Transform over, Sprite portrait = null)
    {
        speaker = over;
        FindHead(over);
        // Text is only measured on an active object (hidden, its preferred size is 0).
        gameObject.SetActive(true);
        nameLabel.text = speakerName ?? "";
        nameLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(speakerName));
        lineLabel.text = line ?? "";
        hintLabel.text = "";

        float natural = lineLabel.GetPreferredValues(lineLabel.text, MaxLineWidth * 4f, 0f).x;
        float nameWidth = nameLabel.gameObject.activeSelf ? nameLabel.GetPreferredValues(nameLabel.text).x : 0f;
        float width = Mathf.Clamp(Mathf.Max(natural, nameWidth), MinWidth - PaddingX * 2f, MaxLineWidth);
        float lineHeight = lineLabel.GetPreferredValues(lineLabel.text, width, 0f).y;
        float top = nameLabel.gameObject.activeSelf ? NameHeight : 0f;
        bool pictured = portrait != null;
        float left = PaddingX + (pictured ? PortraitSize + PortraitGap : 0f);
        float height = Mathf.Max(PaddingTop + top + lineHeight + HintHeight + PaddingBottom,
            pictured ? PaddingTop + PortraitSize + PaddingBottom : 0f);
        bubble.sizeDelta = new Vector2(width + left + PaddingX, height);
        nameLabel.rectTransform.sizeDelta = new Vector2(width, NameHeight);
        nameLabel.rectTransform.anchoredPosition = new Vector2(left, -PaddingTop);
        lineLabel.rectTransform.sizeDelta = new Vector2(width, lineHeight);
        lineLabel.rectTransform.anchoredPosition = new Vector2(left, -PaddingTop - top);
        portraitFrame.gameObject.SetActive(pictured);
        portraitImage.sprite = portrait;
        portraitFrame.anchoredPosition = new Vector2(PaddingX, -PaddingTop);
        group.alpha = 1f;
        Place();
    }

    public void Hide()
    {
        speaker = null;
        head = null;
        if (group != null)
        {
            group.alpha = 0f;
        }

        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (speaker == null)
        {
            Hide();
            return;
        }

        Place();
    }

    // Over the speaker's head, kept inside the screen; the tail keeps pointing at them when the bubble is pushed aside.
    private void Place()
    {
        Camera view = Camera.main;
        if (view == null || speaker == null)
        {
            return;
        }

        Vector3 world = head != null ? head.position + Vector3.up * AboveHead : speaker.position + Vector3.up * (headHeight + AboveHead);
        Vector3 screen = view.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            group.alpha = 0f;
            return;
        }

        group.alpha = 1f;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 point))
        {
            return;
        }

        Rect bounds = area.rect;
        Vector2 size = bubble.sizeDelta;
        float x = Mathf.Clamp(point.x, bounds.xMin + ScreenMargin + size.x * 0.5f, bounds.xMax - ScreenMargin - size.x * 0.5f);
        float y = Mathf.Clamp(point.y + TailSize, bounds.yMin + ScreenMargin, bounds.yMax - ScreenMargin - size.y);
        bubble.anchoredPosition = new Vector2(x, y);
        float tailX = Mathf.Clamp(point.x - x, -size.x * 0.5f + TailSize * 1.5f, size.x * 0.5f - TailSize * 1.5f);
        tail.anchoredPosition = new Vector2(tailX, 0f);
    }

    // The head bone of a humanoid, else the top of what is drawn (the portal's arch, a creature).
    private void FindHead(Transform over)
    {
        head = null;
        headHeight = 1.9f;
        if (over == null)
        {
            return;
        }

        Animator animator = over.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        if (head == null)
        {
            bool any = false;
            Bounds drawn = default;
            foreach (Renderer part in over.GetComponentsInChildren<Renderer>())
            {
                if (part is ParticleSystemRenderer || !part.enabled)
                {
                    continue;
                }

                if (any)
                {
                    drawn.Encapsulate(part.bounds);
                }
                else
                {
                    drawn = part.bounds;
                    any = true;
                }
            }

            if (any)
            {
                headHeight = Mathf.Max(0.5f, drawn.max.y - over.position.y);
            }
        }
    }

    private void Build(RectTransform canvas)
    {
        area = canvas;
        UIStyle style = UIStyle.Current;
        var self = (RectTransform)transform;
        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.one;
        self.offsetMin = self.offsetMax = Vector2.zero;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        bubble = Rect("Bubble", self, new Vector2(0.5f, 0.5f), new Vector2(300f, 100f));
        bubble.pivot = new Vector2(0.5f, 0f);
        Image back = HearthKit.Sliced(bubble.gameObject.AddComponent<Image>(), style.Card, 1.3f);
        back.color = SeeThrough(style.Panel, BackAlpha);
        HearthKit.Rim(bubble, 0.8f, 1.3f);

        // The tail: a square turned on its corner, half of it hanging below the bubble, the same colour and rim.
        tail = Rect("Tail", bubble, new Vector2(0.5f, 0f), new Vector2(TailSize, TailSize));
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.SetAsFirstSibling();
        tail.gameObject.AddComponent<Image>().color = SeeThrough(style.Panel, BackAlpha);
        var tailRim = tail.gameObject.AddComponent<Outline>();
        tailRim.effectColor = HearthKit.Faded(style.Accent, 0.8f);
        tailRim.effectDistance = new Vector2(1.5f, -1.5f);

        // The portrait: the painting in a rimmed frame at the top left, on a dark inset so a narrow picture doesn't show the panel.
        portraitFrame = Rect("Portrait", bubble, new Vector2(0f, 1f), new Vector2(PortraitSize, PortraitSize));
        portraitFrame.pivot = new Vector2(0f, 1f);
        portraitFrame.gameObject.AddComponent<Image>().color = style.Inset;
        RectTransform painting = Rect("Painting", portraitFrame, new Vector2(0.5f, 0.5f), new Vector2(PortraitSize - 4f, PortraitSize - 4f));
        portraitImage = painting.gameObject.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        HearthKit.Rim(portraitFrame, 0.9f, 1.2f);
        portraitFrame.gameObject.SetActive(false);

        nameLabel = Text("Name", bubble, 18f, style.Accent, TextAlignmentOptions.TopLeft);
        style.Title(nameLabel);
        nameLabel.color = style.Accent;
        nameLabel.fontStyle = FontStyles.Bold;
        lineLabel = Text("Line", bubble, 18f, style.Text, TextAlignmentOptions.TopLeft);
        hintLabel = Text("Hint", bubble, 13f, style.Accent, TextAlignmentOptions.BottomRight);
        // Words on a see-through back stay readable with the shadowed face made for text on the world.
        // The name keeps the title face on the now solid back; the line keeps the shadowed face made for text on the world.
        style.OnWorld(lineLabel);
        RectTransform hint = hintLabel.rectTransform;
        hint.anchorMin = hint.anchorMax = new Vector2(1f, 0f);
        hint.pivot = new Vector2(1f, 0f);
        hint.sizeDelta = new Vector2(160f, HintHeight);
        hint.anchoredPosition = new Vector2(-12f, 6f);
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private static Color SeeThrough(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, colour.a * alpha);

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorAt, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(string name, RectTransform parent, float size, Color colour, TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(200f, 30f));
        rect.pivot = new Vector2(0f, 1f);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (UIStyle.Current.BodyFont != null)
        {
            text.font = UIStyle.Current.BodyFont;
        }

        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
