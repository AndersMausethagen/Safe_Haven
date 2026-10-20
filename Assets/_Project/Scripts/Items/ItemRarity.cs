using UnityEngine;

/// <summary>
/// How rare and exciting an item is. Drives its color in the UI, the sound it makes when picked up and whether the find is
/// announced. Values are serialized as numbers, so add new rarities at the end and never renumber.
/// </summary>
public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

/// <summary>
/// How rarities look and sound everywhere (slots, tooltips, the message feed, the raid summary), read from the
/// RaritySettings asset in Resources. Falls back to built-in colors when the asset is missing, so nothing breaks.
///
/// Where this belongs: Items. To change rarity colors or sounds, edit Resources/RaritySettings in the Inspector; to add
/// a rarity, add it to ItemRarity and give it a style in RaritySettingsDefinition.Defaults.
/// </summary>
public static class Rarity
{
    private const string SettingsPath = "RaritySettings";

    private static RaritySettingsDefinition settings;
    private static bool loaded;

    public static RaritySettingsDefinition Settings
    {
        get
        {
            // Also reloads when the loaded asset was destroyed, such as after a reimport in the editor.
            if (!loaded || (!ReferenceEquals(settings, null) && settings == null))
            {
                settings = Resources.Load<RaritySettingsDefinition>(SettingsPath);
                loaded = true;
            }

            return settings;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        settings = null;
        loaded = false;
    }

    public static RarityStyle StyleOf(ItemRarity rarity)
    {
        RaritySettingsDefinition current = Settings;
        return current != null ? current.Find(rarity) : RaritySettingsDefinition.DefaultStyle(rarity);
    }

    public static Color ColorOf(ItemRarity rarity) => StyleOf(rarity).Color;

    public static string NameOf(ItemRarity rarity) => StyleOf(rarity).Label;

    /// <summary>Text wrapped in the rarity's color, for UI text with rich text enabled.</summary>
    public static string Rich(string text, ItemRarity rarity)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(ColorOf(rarity))}>{text}</color>";
    }

    /// <summary>The item's name in its rarity's color.</summary>
    public static string RichName(ItemDefinition item) => item != null ? Rich(item.DisplayName, item.Rarity) : "";
}
