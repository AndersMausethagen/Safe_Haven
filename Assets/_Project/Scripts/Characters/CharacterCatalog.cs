using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The characters of the game: the male and female modular bodies, the look a new character starts with, and the emotes
/// (the wheel's, and the gestures of conversations, quests and resting). In Resources, read through Current.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Characters/Character Catalog", fileName = "CharacterCatalog")]
public sealed class CharacterCatalog : GameSettings<CharacterCatalog>
{
    [SerializeField] private CharacterBodyDefinition male;
    [SerializeField] private CharacterBodyDefinition female;
    [Tooltip("The look the character creation screen starts from.")]
    [SerializeField] private CharacterAppearance startingLook = new CharacterAppearance();
    [Tooltip("Names offered by the dice on the character creation screen.")]
    [SerializeField] private List<string> maleNames = new List<string>();
    [SerializeField] private List<string> femaleNames = new List<string>();
    [Tooltip("What the character creation screen's preview stands with (an idle).")]
    [SerializeField] private RuntimeAnimatorController previewAnimations;

    [Header("Plain Clothes")]
    [Tooltip("Worn by the player while nothing is in the Body slot; chosen on the character creation screen.")]
    [SerializeField] private List<NamedLook> plainClothes = new List<NamedLook>();
    [Tooltip("Worn by the player while nothing is in the Feet slot.")]
    [SerializeField] private GearLookDefinition plainShoes;

    [Header("Emotes")]
    [Tooltip("Every emote; those marked for it are offered on the command wheel.")]
    [SerializeField] private List<EmoteDefinition> emotes = new List<EmoteDefinition>();
    [Tooltip("Gestures while speaking in a conversation, picked in turn.")]
    [SerializeField] private List<EmoteDefinition> speaking = new List<EmoteDefinition>();
    [Tooltip("Made by the quest giver and the player when a quest is taken.")]
    [SerializeField] private EmoteDefinition questTaken;
    [Tooltip("Held while resting at a fire.")]
    [SerializeField] private EmoteDefinition sitAtFire;

    public CharacterBodyDefinition Male => male;
    public CharacterBodyDefinition Female => female;
    public CharacterAppearance StartingLook => startingLook.Clone();
    public IReadOnlyList<EmoteDefinition> Emotes => emotes;
    public IReadOnlyList<EmoteDefinition> Speaking => speaking;
    public EmoteDefinition QuestTaken => questTaken;
    public EmoteDefinition SitAtFire => sitAtFire;
    public RuntimeAnimatorController PreviewAnimations => previewAnimations;
    public IReadOnlyList<NamedLook> PlainClothes => plainClothes;
    public GearLookDefinition PlainShoes => plainShoes;

    /// <summary>The plain clothes of this index (clamped), or null when there are none.</summary>
    public GearLookDefinition PlainClothing(int index) =>
        plainClothes.Count == 0 ? null : plainClothes[Mathf.Clamp(index, 0, plainClothes.Count - 1)].look;

    public CharacterBodyDefinition Body(CharacterGender gender) => gender == CharacterGender.Female && female != null ? female : male;

    public IReadOnlyList<string> Names(CharacterGender gender) => gender == CharacterGender.Female ? femaleNames : maleNames;

#if UNITY_EDITOR
    public void EditorSet(CharacterBodyDefinition male, CharacterBodyDefinition female, CharacterAppearance startingLook, List<string> maleNames,
        List<string> femaleNames, List<EmoteDefinition> emotes, List<EmoteDefinition> speaking, EmoteDefinition questTaken, EmoteDefinition sitAtFire)
    {
        this.male = male;
        this.female = female;
        this.startingLook = startingLook;
        this.maleNames = maleNames;
        this.femaleNames = femaleNames;
        this.emotes = emotes;
        this.speaking = speaking;
        this.questTaken = questTaken;
        this.sitAtFire = sitAtFire;
    }

    public void EditorSetPreviewAnimations(RuntimeAnimatorController controller) => previewAnimations = controller;

    public void EditorSetPlainClothes(List<NamedLook> clothes, GearLookDefinition shoes)
    {
        plainClothes = clothes;
        plainShoes = shoes;
    }
#endif
}

/// <summary>A worn look with the name it is offered by.</summary>
[System.Serializable]
public struct NamedLook
{
    public string label;
    public GearLookDefinition look;
}
