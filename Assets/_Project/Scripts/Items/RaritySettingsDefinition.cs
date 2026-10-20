using System;
using UnityEngine;

/// <summary>How one rarity looks and sounds.</summary>
[Serializable]
public sealed class RarityStyle
{
    [SerializeField] private ItemRarity rarity;
    [Tooltip("Shown in tooltips and the raid summary.")]
    [SerializeField] private string label;
    [Tooltip("Tints the item's slot and colors its name in tooltips, messages and the raid summary.")]
    [SerializeField] private Color color = Color.white;
    [Tooltip("Played when the player receives an item of this rarity. Empty means silent.")]
    [SerializeField] private SoundDefinition pickupSound;
    [Tooltip("Announces the find with an extra message, so it stands out from ordinary loot.")]
    [SerializeField] private bool announce;

    public RarityStyle(ItemRarity rarity, string label, Color color, bool announce)
    {
        this.rarity = rarity;
        this.label = label;
        this.color = color;
        this.announce = announce;
    }

    public ItemRarity Rarity => rarity;
    public string Label => string.IsNullOrWhiteSpace(label) ? rarity.ToString() : label;
    public Color Color => color;
    public SoundDefinition PickupSound => pickupSound;
    public bool Announce => announce;
}

/// <summary>
/// The look and sound of each item rarity, in one asset designers can tune: Resources/RaritySettings. Read through the
/// static Rarity class. Items choose their rarity on their ItemDefinition.
/// </summary>
[CreateAssetMenu(fileName = "RaritySettings", menuName = "Safe Haven/Items/Rarity Settings")]
public sealed class RaritySettingsDefinition : ScriptableObject
{
    [Tooltip("One entry per rarity. Rarities without an entry use the built-in defaults.")]
    [SerializeField] private RarityStyle[] styles = Defaults();

    public RarityStyle Find(ItemRarity rarity)
    {
        if (styles != null)
        {
            foreach (RarityStyle style in styles)
            {
                if (style != null && style.Rarity == rarity)
                {
                    return style;
                }
            }
        }

        return DefaultStyle(rarity);
    }

    public static RarityStyle DefaultStyle(ItemRarity rarity)
    {
        RarityStyle[] defaults = Defaults();
        int index = Mathf.Clamp((int)rarity, 0, defaults.Length - 1);
        return defaults[index];
    }

    /// <summary>The built-in styles, in ItemRarity order.</summary>
    public static RarityStyle[] Defaults()
    {
        return new[]
        {
            new RarityStyle(ItemRarity.Common, "Common", new Color(0.82f, 0.82f, 0.82f), false),
            new RarityStyle(ItemRarity.Uncommon, "Uncommon", new Color(0.45f, 0.88f, 0.45f), false),
            new RarityStyle(ItemRarity.Rare, "Rare", new Color(0.4f, 0.65f, 1f), true),
            new RarityStyle(ItemRarity.Epic, "Epic", new Color(0.78f, 0.45f, 1f), true),
            new RarityStyle(ItemRarity.Legendary, "Legendary", new Color(1f, 0.7f, 0.2f), true),
        };
    }
}
