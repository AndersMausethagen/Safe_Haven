using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A control lights up under the pointer (Hearthline 3): a warm sheen slides in over it from the left with a bright line
/// along its top, and it lifts a little; both ease back when the pointer leaves. Not while it can't be pressed. The sheen
/// sits over the control's own image and under its words. Put on by UIRole (buttons) and the kit's buttons, tabs and slots.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private const float Lift = 1.03f;
    private const float Speed = 9f;

    private Image sheen;
    private Selectable control;
    private bool over;
    private float lit;
    private Vector3 rest = Vector3.one;

    /// <summary>Gives a control its highlight (once).</summary>
    public static UIHighlight On(GameObject control)
    {
        if (control == null || UIStyle.Current.Sheen == null)
        {
            return null;
        }

        UIHighlight highlight = control.GetComponent<UIHighlight>();
        return highlight != null ? highlight : control.AddComponent<UIHighlight>();
    }

    private void Awake()
    {
        control = GetComponent<Selectable>();
        rest = transform.localScale;
        UIStyle style = UIStyle.Current;
        var rect = (RectTransform)transform;
        RectTransform holder = CodeUI.Stretch("Sheen", rect);
        holder.SetSiblingIndex(0);
        holder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        sheen = holder.gameObject.AddComponent<Image>();
        sheen.sprite = style.Sheen;
        sheen.type = Image.Type.Sliced;
        sheen.raycastTarget = false;
        sheen.color = Tint(0f);
    }

    private static Color Tint(float alpha)
    {
        Color warm = UIStyle.Current.AccentBright;
        warm.a = alpha * 0.55f;
        return warm;
    }

    public void OnPointerEnter(PointerEventData eventData) => Set(true);

    public void OnPointerExit(PointerEventData eventData) => Set(false);

    // A controller's focus lights it as the pointer would.
    public void OnSelect(BaseEventData eventData) => Set(true);

    public void OnDeselect(BaseEventData eventData) => Set(false);

    // It stays enabled: a disabled behaviour gets no pointer events. At rest its Update does nothing.
    private void Set(bool on) => over = on;

    private void OnDisable()
    {
        over = false;
        lit = 0f;
        if (sheen != null)
        {
            sheen.color = Tint(0f);
        }

        transform.localScale = rest;
    }

    private void Update()
    {
        bool wanted = over && (control == null || control.IsInteractable());
        if (!wanted && lit <= 0f)
        {
            return;
        }

        lit = Mathf.MoveTowards(lit, wanted ? 1f : 0f, Time.unscaledDeltaTime * Speed);
        float eased = lit * lit * (3f - 2f * lit);
        sheen.color = Tint(eased);
        // It slides in from the left as it lights.
        sheen.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.35f, 1f, eased), 1f);
        transform.localScale = rest * Mathf.Lerp(1f, Lift, eased);
    }
}
