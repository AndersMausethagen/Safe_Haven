using UnityEngine;

/// <summary>
/// Art runes (Docs/CombatResearch.md Phase 3, No Rest for the Wicked's runes): an item carved with Runecraft that, used from the bag,
/// attunes every weapon of its kind (WeaponDefinition › Signature) to a second weapon art. Attacking out of the guard without a shield
/// strikes the rune's art; the middle button keeps the weapon's own. One rune per kind is attuned at a time (the character's progress
/// keeps which, and saves it); the rune stays in the bag.
/// </summary>
public static class ArtRunes
{
    private static ContentDatabase content;

    /// <summary>The rune attuned for this weapon's kind, or null.</summary>
    public static ItemDefinition For(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.Signature == WeaponSignature.None || GameSession.Progress == null)
        {
            return null;
        }

        string id = GameSession.Progress.RuneIdFor(weapon.Signature);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (content == null)
        {
            content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        }

        ItemDefinition rune = content != null ? content.FindItem(id) : null;
        return rune != null && rune.IsArtRune && rune.RuneFor == weapon.Signature ? rune : null;
    }

    /// <summary>Attunes the rune's kind of weapon to it, with a word to the player. False when it was attuned already.</summary>
    public static bool Attune(ItemDefinition rune)
    {
        if (rune == null || !rune.IsArtRune || GameSession.Progress == null)
        {
            return false;
        }

        if (!GameSession.Progress.AttuneRune(rune.RuneFor, rune.Id))
        {
            GameMessages.Post($"{rune.DisplayName} is already attuned.", MessageKind.Info);
            return false;
        }

        GameMessages.Post($"{rune.DisplayName} attuned: attack out of your guard to strike {rune.RuneArt.DisplayName}.", MessageKind.Gain);
        return true;
    }
}
