using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One material a selected recipe needs in a RecipeBrowser: its icon and name, and what is held against what it costs.</summary>
public sealed class RecipeCostRow : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text amountLabel;

    public void Show(Sprite sprite, string materialName, int have, int need)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        nameLabel.text = materialName;
        amountLabel.text = $"{have} / {need}";
        amountLabel.color = have >= need ? UIStyle.Current.Good : UIStyle.Current.Danger;
    }
}
