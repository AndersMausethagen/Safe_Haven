using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One choice row in the ActionMenuWindow: a button with an icon, a label and a detail line (cost, state). Hearthline 3:
/// the icon is the action's own (MenuAction.Icon) or one its first word suggests (HearthKit.IconForAction); a row that
/// can't be chosen sinks darker and fainter, its icon dim, rather than lightening.
/// </summary>
public sealed class ActionRowView : MonoBehaviour
{
    private const float IconSize = 30f;
    private const float TextIndent = 46f;

    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Color enabledText = new Color(0.95f, 0.9f, 0.8f);
    [SerializeField] private Color disabledText = new Color(0.55f, 0.52f, 0.48f);

    private MenuAction action;
    private Image icon;

    /// <summary>Raised when the row is clicked, with its choice.</summary>
    public event Action<MenuAction> Clicked;

    private void Awake()
    {
        button.onClick.AddListener(() => Clicked?.Invoke(action));
    }

    public void Bind(MenuAction shown)
    {
        action = shown;
        label.text = GameInput.WithRebinds(shown.Label);
        label.color = shown.Enabled ? enabledText : UIStyle.Current.Inactive;
        detail.text = HearthKit.ToneNumbers(GameInput.WithRebinds(shown.Detail ?? ""));
        detail.gameObject.SetActive(!string.IsNullOrEmpty(shown.Detail));
        button.interactable = shown.Enabled;
        Dress(shown);
    }

    private void Dress(MenuAction shown)
    {
        UIStyle style = UIStyle.Current;
        if (style.Card == null)
        {
            return;
        }

        if (icon == null)
        {
            var rect = (RectTransform)button.transform;
            icon = CodeUI.Box("Icon", rect, new Vector2(0f, 0.5f), Vector2.one * IconSize, new Vector2(14f + IconSize * 0.5f, 0f)).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            // Outside the row's layout: as a laid-out child it stretched the row.
            icon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            LayoutGroup layout = button.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.padding.left += Mathf.RoundToInt(TextIndent);
            }
            else
            {
                Indent(label.rectTransform);
                Indent(detail.rectTransform);
            }
        }

        // Disabled sinks darker than at rest, and opaque (a faint row let the lighter panel show through). Each time: the
        // row's UIRole paints its colours after the first bind.
        ColorBlock colors = button.colors;
        Graphic face = button.targetGraphic;
        colors.disabledColor = face != null ? HearthKit.GreyTint(face.color) : new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;

        string name = !string.IsNullOrEmpty(shown.Icon) ? shown.Icon : HearthKit.IconForAction(shown.Label);
        icon.sprite = style.Icon(name);
        icon.enabled = icon.sprite != null;
        icon.color = shown.Enabled ? HearthKit.IconColour(name) : style.Inactive;
        detail.color = shown.Enabled ? style.Muted : style.Inactive;
    }

    // The words move right to make room for the icon (whatever their anchors: only the left edge moves).
    private static void Indent(RectTransform text)
    {
        text.offsetMin = new Vector2(text.offsetMin.x + TextIndent, text.offsetMin.y);
    }
}
