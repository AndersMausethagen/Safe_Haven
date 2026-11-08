using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A text tab (a label with a line under it, the child called Line, that marks the open one) that answers the pointer:
/// an open tab is amber with its line; a closed one under the pointer brightens to the text colour and shows a faint line,
/// so the player sees what a click would choose. Colours from UIStyle. Whoever lays the tabs out says which is open
/// (<see cref="SetOpen"/>); RecipeBrowser adds it to its tabs.
/// </summary>
public sealed class UITabHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TMP_Text label;
    private Graphic line;
    private bool open;
    private bool hovered;

    /// <summary>Marks the tab open or closed and repaints it.</summary>
    public void SetOpen(bool isOpen)
    {
        open = isOpen;
        Paint();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Paint();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Paint();
    }

    private void OnDisable()
    {
        hovered = false;
    }

    private void Paint()
    {
        label ??= GetComponentInChildren<TMP_Text>(true);
        if (line == null)
        {
            Transform found = transform.Find("Line");
            line = found != null ? found.GetComponent<Graphic>() : null;
        }

        UIStyle style = UIStyle.Current;
        if (label != null)
        {
            label.color = open ? style.Accent : hovered ? style.Text : style.Muted;
        }

        if (line != null)
        {
            line.gameObject.SetActive(open || hovered);
            Color colour = open ? style.Accent : style.Muted;
            colour.a = open ? 1f : 0.6f;
            line.color = colour;
        }
    }
}
