using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One raid modifier item in the raid preparation window: its icon, how many are in the bag, what it does and whether it
/// is picked. Clicking it asks the window to toggle the pick; the window decides and calls SetPicked.
/// </summary>
public sealed class RaidModifierRowView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text effectLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private Color normalColor = new Color(0.13f, 0.14f, 0.17f, 0.95f);
    [SerializeField] private Color pickedColor = new Color(0.18f, 0.33f, 0.22f, 0.95f);

    private Action<ItemDefinition> onToggle;

    public ItemDefinition Item { get; private set; }

    private void Awake()
    {
        button.onClick.AddListener(() => onToggle?.Invoke(Item));
    }

    public void Bind(ItemDefinition item, int count, Action<ItemDefinition> toggle)
    {
        Item = item;
        onToggle = toggle;
        icon.sprite = item.Icon;
        icon.enabled = icon.sprite != null;
        nameLabel.text = count > 1 ? $"{item.DisplayName}  <color=#AAB0BC>x{count}</color>" : item.DisplayName;
        effectLabel.text = item.RaidModifier.DescribeEffects();
    }

    public void SetPicked(bool picked)
    {
        background.color = picked ? pickedColor : normalColor;
        stateLabel.text = picked ? "<color=#8BE08B>Using</color>" : "Use";
    }
}
