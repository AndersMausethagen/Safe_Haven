using UnityEngine;

/// <summary>
/// The gestures characters make on their own: talking with their hands while they speak, and the nod of a quest being
/// taken. Finds the character's CharacterEmotes under the given object (an NPC's root, the player) and leaves alone anyone
/// without one, anyone walking and anyone holding a pose such as sitting.
/// </summary>
public static class CharacterGestures
{
    private static int speakingTurn;

    /// <summary>A speaking gesture, the catalog's in turn so a long talk doesn't repeat one.</summary>
    public static bool Speak(Transform who)
    {
        var speaking = CharacterCatalog.Current.Speaking;
        if (speaking.Count == 0)
        {
            return false;
        }

        speakingTurn = (speakingTurn + 1) % speaking.Count;
        return Play(who, speaking[speakingTurn]);
    }

    /// <summary>Plays the emote on them when they are free to make it.</summary>
    public static bool Play(Transform who, EmoteDefinition emote)
    {
        CharacterEmotes emotes = who != null ? who.GetComponentInChildren<CharacterEmotes>() : null;
        if (emote == null || emotes == null || !emotes.isActiveAndEnabled || emotes.IsHolding || emotes.IsMoving)
        {
            return false;
        }

        return emotes.Play(emote);
    }
}
