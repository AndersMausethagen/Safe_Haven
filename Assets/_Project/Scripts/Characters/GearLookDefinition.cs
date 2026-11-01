using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The parts of the naked body a worn piece covers, so they are hidden under it.</summary>
[Flags]
public enum BodyCover
{
    None = 0,
    Torso = 1 << 0,
    Bracers = 1 << 1,
    Hands = 1 << 2,
    Legs = 1 << 3,
    Boots = 1 << 4,
    Feet = 1 << 5,
    /// <summary>Hoods and helmets: the hair goes under them.</summary>
    Hair = 1 << 6,
}

/// <summary>One mesh of a worn look, skinned to the same skeleton as the body. A piece without a prefab only paints the
/// skin (gloves and trousers drawn onto the body).</summary>
[Serializable]
public sealed class WornPiece
{
    [Tooltip("Skinned meshes bound to the body's bones by name. Optional.")]
    public GameObject prefab;
    public BodyCover covers;
    [Tooltip("Painted over the body skin, like gloves or trousers. Optional.")]
    public Material skinOverlay;
}

/// <summary>
/// What a piece of gear looks like on a body: meshes for the male and the female body, and the body parts they hide.
/// An ItemDefinition points at one, so several items can share a look; ModularCharacter puts it on.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Characters/Gear Look", fileName = "GearLook")]
public sealed class GearLookDefinition : ScriptableObject
{
    [SerializeField] private List<WornPiece> male = new List<WornPiece>();
    [SerializeField] private List<WornPiece> female = new List<WornPiece>();

    public IReadOnlyList<WornPiece> For(CharacterGender gender) => gender == CharacterGender.Female ? female : male;

#if UNITY_EDITOR
    public void EditorSet(List<WornPiece> male, List<WornPiece> female)
    {
        this.male = male;
        this.female = female;
    }
#endif
}
