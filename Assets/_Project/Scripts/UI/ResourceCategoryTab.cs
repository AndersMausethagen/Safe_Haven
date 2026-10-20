using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A category button for the resource panel. It displays a label, reports clicks, and asks for the width its text
/// needs so the tab bar (and panel) can grow as categories are added.
/// </summary>
[RequireComponent(typeof(Button), typeof(LayoutElement))]
public sealed class ResourceCategoryTab : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(0)] private float horizontalPadding = 24f;
    [SerializeField] private Color idleColor = new Color(0.14f, 0.14f, 0.16f, 0.9f);
    [SerializeField] private Color idleTextColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color selectedTextColor = Color.white;

    private Color accent = Color.white;

    public void Bind(string text, Color accentColor, Action onClick)
    {
        label.text = text;
        accent = accentColor;
        GetComponent<LayoutElement>().preferredWidth = label.preferredWidth + horizontalPadding;
        Button button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? Color.Lerp(idleColor, new Color(accent.r, accent.g, accent.b, 1f), 0.75f) : idleColor;
        label.color = selected ? selectedTextColor : idleTextColor;
    }
}
