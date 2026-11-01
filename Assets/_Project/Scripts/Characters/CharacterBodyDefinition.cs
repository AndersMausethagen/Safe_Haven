using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A material with the name the character creation screen shows for it.</summary>
[Serializable]
public struct NamedMaterial
{
    public string label;
    public Material material;
}

/// <summary>A worn mesh (hair, brows, a beard) in each hair colour, in the order of the body's hair colours. An option with
/// no prefabs is "none" (bald, clean-shaven).</summary>
[Serializable]
public sealed class ColouredPart
{
    public string label;
    public List<GameObject> byColour = new List<GameObject>();

    public GameObject For(int colour) => byColour.Count == 0 ? null : byColour[Mathf.Clamp(colour, 0, byColour.Count - 1)];
}

/// <summary>An animation clip made for the male body and its counterpart for this one.</summary>
[Serializable]
public struct ClipSwap
{
    public AnimationClip from;
    public AnimationClip to;
}

/// <summary>
/// Everything one gender's modular body is built from: the skinned model and its humanoid avatar, the materials that skin
/// it, and the hair, brows and beards that can be put on it. Read-only data; ModularCharacter builds a body from it and a
/// CharacterAppearance (whose numbers index these lists). Lives in the CharacterCatalog.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Characters/Character Body", fileName = "CharacterBody")]
public sealed class CharacterBodyDefinition : ScriptableObject
{
    [SerializeField] private CharacterGender gender;
    [Tooltip("The full skinned body (Geometry and Root), whose children are copied under the character.")]
    [SerializeField] private GameObject model;
    [SerializeField] private Avatar avatar;

    [Header("Skin")]
    [Tooltip("Body skin tones, put on every naked body part.")]
    [SerializeField] private List<NamedMaterial> skins = new List<NamedMaterial>();
    [Tooltip("Faces: head and ears.")]
    [SerializeField] private List<NamedMaterial> faces = new List<NamedMaterial>();
    [SerializeField] private List<NamedMaterial> eyes = new List<NamedMaterial>();
    [Tooltip("Painted over the body skin under the gear. Their own layer, so never left naked.")]
    [SerializeField] private List<NamedMaterial> underclothes = new List<NamedMaterial>();
    [Tooltip("The preview material gear pieces carry where skin shows through; replaced with the body's skin.")]
    [SerializeField] private Material bodyPlaceholder;
    [SerializeField] private Material headPlaceholder;

    [Header("Hair")]
    [SerializeField] private List<string> hairColours = new List<string>();
    [Tooltip("Shading on the scalp and jaw under the hair, one per hair colour.")]
    [SerializeField] private List<Material> stubble = new List<Material>();
    [SerializeField] private List<ColouredPart> hairStyles = new List<ColouredPart>();
    [SerializeField] private List<ColouredPart> brows = new List<ColouredPart>();
    [Tooltip("For bodies whose brows are part of the head mesh: their material in each hair colour.")]
    [SerializeField] private List<Material> browTints = new List<Material>();
    [Tooltip("Empty for bodies that grow none. The first is usually 'None'.")]
    [SerializeField] private List<ColouredPart> beards = new List<ColouredPart>();

    [Header("Animation")]
    [Tooltip("Clips made for the male body replaced by this body's own (a woman's walk). Empty for the male body.")]
    [SerializeField] private List<ClipSwap> animationSwaps = new List<ClipSwap>();

    public CharacterGender Gender => gender;
    public GameObject Model => model;
    public Avatar Avatar => avatar;
    public IReadOnlyList<NamedMaterial> Skins => skins;
    public IReadOnlyList<NamedMaterial> Faces => faces;
    public IReadOnlyList<NamedMaterial> Eyes => eyes;
    public IReadOnlyList<NamedMaterial> Underclothes => underclothes;
    public Material BodyPlaceholder => bodyPlaceholder;
    public Material HeadPlaceholder => headPlaceholder;
    public IReadOnlyList<string> HairColours => hairColours;
    public IReadOnlyList<ColouredPart> HairStyles => hairStyles;
    public IReadOnlyList<ColouredPart> Brows => brows;
    public IReadOnlyList<ColouredPart> Beards => beards;
    public IReadOnlyList<ClipSwap> AnimationSwaps => animationSwaps;

    public Material Skin(int index) => Pick(skins, index);
    public Material Face(int index) => Pick(faces, index);
    public Material Eye(int index) => Pick(eyes, index);
    public Material Underclothing(int index) => Pick(underclothes, index);
    public Material Stubble(int colour) => stubble.Count == 0 ? null : stubble[Mathf.Clamp(colour, 0, stubble.Count - 1)];
    public GameObject Hair(int style, int colour) => PickPart(hairStyles, style, colour);
    public GameObject Brow(int style, int colour) => PickPart(brows, style, colour);
    public GameObject Beard(int style, int colour) => PickPart(beards, style, colour);
    public Material BrowTint(int colour) => browTints.Count == 0 ? null : browTints[Mathf.Clamp(colour, 0, browTints.Count - 1)];

    /// <summary>True for one of this body's skin tones, which gear made for a particular skin may carry.</summary>
    public bool IsSkin(Material material) => Contains(skins, material);
    public bool IsFace(Material material) => Contains(faces, material);
    public bool IsStubble(Material material) => material != null && stubble.Exists(m => m != null && m.name == material.name);

    private static bool Contains(List<NamedMaterial> list, Material material) =>
        material != null && list.Exists(entry => entry.material != null && entry.material.name == material.name);

    private static Material Pick(List<NamedMaterial> list, int index) =>
        list.Count == 0 ? null : list[Mathf.Clamp(index, 0, list.Count - 1)].material;

    private static GameObject PickPart(List<ColouredPart> list, int style, int colour) =>
        list.Count == 0 ? null : list[Mathf.Clamp(style, 0, list.Count - 1)].For(colour);

#if UNITY_EDITOR
    /// <summary>Filled by the editor's CharacterLibraryBuilder from the imported character pack.</summary>
    public void EditorSet(CharacterGender gender, GameObject model, Avatar avatar, List<NamedMaterial> skins, List<NamedMaterial> faces,
        List<NamedMaterial> eyes, List<NamedMaterial> underclothes, Material bodyPlaceholder, Material headPlaceholder, List<string> hairColours,
        List<Material> stubble, List<ColouredPart> hairStyles, List<ColouredPart> brows, List<Material> browTints, List<ColouredPart> beards,
        List<ClipSwap> animationSwaps)
    {
        this.browTints = browTints;
        this.gender = gender;
        this.model = model;
        this.avatar = avatar;
        this.skins = skins;
        this.faces = faces;
        this.eyes = eyes;
        this.underclothes = underclothes;
        this.bodyPlaceholder = bodyPlaceholder;
        this.headPlaceholder = headPlaceholder;
        this.hairColours = hairColours;
        this.stubble = stubble;
        this.hairStyles = hairStyles;
        this.brows = brows;
        this.beards = beards;
        this.animationSwaps = animationSwaps;
    }
#endif
}
