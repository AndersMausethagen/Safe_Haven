using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One choice row in the ActionMenuWindow: a button with a label and a detail line (cost, state).</summary>
public sealed class ActionRowView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Color enabledText = new Color(0.95f, 0.9f, 0.8f);
    [SerializeField] private Color disabledText = new Color(0.55f, 0.52f, 0.48f);

    private MenuAction action;

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
        label.color = shown.Enabled ? enabledText : disabledText;
        detail.text = GameInput.WithRebinds(shown.Detail ?? "");
        detail.gameObject.SetActive(!string.IsNullOrEmpty(shown.Detail));
        button.interactable = shown.Enabled;
    }
}
