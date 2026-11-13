using UnityEngine;

/// <summary>
/// The horse window's artwork (Tools/UI/hearthline_horse_art.py, put in by BuildHorses): its gilded corner, rounded card
/// and rim (sliced), and its icons by name. White, so the window tints them. Lives in Resources as HorsePanelArt.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/UI/Horse Panel Art", fileName = "HorsePanelArt")]
public sealed class HorsePanelArt : ScriptableObject
{
    [SerializeField] private Sprite corner;
    [SerializeField] private Sprite card;
    [SerializeField] private Sprite cardRim;
    [SerializeField] private Sprite[] icons = System.Array.Empty<Sprite>();

    private static HorsePanelArt current;

    public static HorsePanelArt Current => current != null ? current : current = Resources.Load<HorsePanelArt>("HorsePanelArt");

    public Sprite Corner => corner;
    public Sprite Card => card;
    public Sprite CardRim => cardRim;

    /// <summary>The icon of this name, or null.</summary>
    public Sprite Icon(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (Sprite icon in icons)
        {
            if (icon != null && icon.name == name)
            {
                return icon;
            }
        }

        return null;
    }

    /// <summary>For the builder.</summary>
    public void Set(Sprite gildedCorner, Sprite roundedCard, Sprite roundedRim, Sprite[] all)
    {
        corner = gildedCorner;
        card = roundedCard;
        cardRim = roundedRim;
        icons = all ?? System.Array.Empty<Sprite>();
    }
}
