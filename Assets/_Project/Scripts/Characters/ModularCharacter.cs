using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Builds a character's body from the modular character pack: the gender's skinned model and humanoid avatar, skin, face,
/// eyes and underclothes painted on, hair, brows and beard, and the worn look of each piece of gear, every mesh bound to
/// the one skeleton. Rebuilds only what changed (a look key), so it is cheap to call; a new gender swaps the skeleton and
/// avatar, which must happen before anything looks up bones, so it builds in Awake, early.
/// <para>
/// Where it belongs: on the object with the humanoid Animator (the player, NPCs). The player's look comes from GameSession
/// through PlayerLook; an NPC keeps the appearance and outfit authored here. Edit-mode builders call
/// <see cref="Build"/> so prefabs and scenes show the body without playing.
/// </para>
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("Safe Haven/Characters/Modular Character")]
public sealed class ModularCharacter : MonoBehaviour
{
    private const string PartsName = "Worn Parts";

    [SerializeField] private CharacterAppearance appearance = new CharacterAppearance();
    [Tooltip("What an NPC wears. The player's outfit follows their gear instead.")]
    [SerializeField] private List<GearLookDefinition> outfit = new List<GearLookDefinition>();
    [SerializeField, HideInInspector] private Transform geometry;
    [SerializeField, HideInInspector] private Transform skeleton;
    [SerializeField, HideInInspector] private Transform parts;
    [SerializeField, HideInInspector] private int builtGender = -1;
    [SerializeField, HideInInspector] private string builtKey;
    [Tooltip("The Animator's controller before the body's own clips were swapped in.")]
    [SerializeField, HideInInspector] private RuntimeAnimatorController baseController;

    private Animator animator;
    private AnimatorOverrideController swapped;
    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly List<Material> bodyMaterials = new List<Material>();
    private readonly List<Material> headMaterials = new List<Material>();
    private readonly List<WornPiece> worn = new List<WornPiece>();

    /// <summary>Raised after the body was rebuilt with a new skeleton, so cached bones must be looked up again.</summary>
    public event Action SkeletonReplaced;

    public CharacterAppearance Appearance => appearance;
    public IReadOnlyList<GearLookDefinition> Outfit => outfit;
    public Transform Skeleton => skeleton;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        Build();
    }

    private void Start() => ApplyAnimationSwaps();

    private void OnDestroy()
    {
        if (swapped != null)
        {
            Destroy(swapped);
        }
    }

    /// <summary>Takes a new look (a copy is kept) and rebuilds what changed.</summary>
    public void SetAppearance(CharacterAppearance look)
    {
        appearance = look != null ? look.Clone() : new CharacterAppearance();
        Build();
        ApplyAnimationSwaps();
    }

    /// <summary>Puts on these looks instead of the current outfit and rebuilds what changed.</summary>
    public void SetOutfit(IEnumerable<GearLookDefinition> looks)
    {
        outfit.Clear();
        if (looks != null)
        {
            foreach (GearLookDefinition look in looks)
            {
                if (look != null && !outfit.Contains(look))
                {
                    outfit.Add(look);
                }
            }
        }

        Build();
    }

    /// <summary>Rebuilds the body when the look or outfit changed since the last build (always, with <paramref name="force"/>).</summary>
    public void Build(bool force = false)
    {
        CharacterBodyDefinition body = CharacterCatalog.Current.Body(appearance.gender);
        if (body == null || body.Model == null)
        {
            return;
        }

        animator = animator != null ? animator : GetComponent<Animator>();
        string key = Key();
        bool newSkeleton = force || skeleton == null || geometry == null || builtGender != (int)body.Gender;
        if (!newSkeleton && key == builtKey && parts != null)
        {
            return;
        }

        if (newSkeleton)
        {
            ReplaceSkeleton(body);
        }

        IndexBones();
        CollectWorn();
        BodyCover covered = BodyCover.None;
        foreach (WornPiece piece in worn)
        {
            covered |= piece.covers;
        }

        PaintBody(body, covered);
        RebuildParts(body, covered);
        builtKey = key;
        if (newSkeleton)
        {
            SkeletonReplaced?.Invoke();
        }
    }

    /// <summary>
    /// Plays the body's own clips (a woman's walk) wherever the Animator's controller uses the male ones. Call again after
    /// giving the Animator another controller, as companions do.
    /// </summary>
    public void ApplyAnimationSwaps()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        animator = animator != null ? animator : GetComponent<Animator>();
        RuntimeAnimatorController current = animator.runtimeAnimatorController;
        if (current == null)
        {
            return;
        }

        if (current != swapped)
        {
            baseController = current;
        }

        CharacterBodyDefinition body = CharacterCatalog.Current.Body(appearance.gender);
        if (body == null || body.AnimationSwaps.Count == 0)
        {
            if (current == swapped)
            {
                animator.runtimeAnimatorController = baseController;
            }

            return;
        }

        var map = new Dictionary<AnimationClip, AnimationClip>();
        foreach (ClipSwap swap in body.AnimationSwaps)
        {
            if (swap.from != null && swap.to != null)
            {
                map[swap.from] = swap.to;
            }
        }

        // Overrides of overrides aren't supported, so the base controller's own overrides are carried into a new one.
        var existing = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        RuntimeAnimatorController root = baseController;
        if (baseController is AnimatorOverrideController overrides)
        {
            overrides.GetOverrides(existing);
            root = overrides.runtimeAnimatorController;
        }

        var carried = new Dictionary<AnimationClip, AnimationClip>();
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in existing)
        {
            if (pair.Key != null && pair.Value != null)
            {
                carried[pair.Key] = pair.Value;
            }
        }

        if (swapped != null)
        {
            Destroy(swapped);
        }

        swapped = new AnimatorOverrideController(root) { name = root.name + " (" + body.name + ")" };
        var list = new List<KeyValuePair<AnimationClip, AnimationClip>>(swapped.overridesCount);
        swapped.GetOverrides(list);
        for (int i = 0; i < list.Count; i++)
        {
            AnimationClip original = list[i].Key;
            AnimationClip playing = carried.TryGetValue(original, out AnimationClip carriedClip) ? carriedClip : original;
            AnimationClip result = map.TryGetValue(playing, out AnimationClip own) ? own : playing;
            list[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, result == original ? null : result);
        }

        swapped.ApplyOverrides(list);
        animator.runtimeAnimatorController = swapped;
    }

    private string Key()
    {
        var key = new StringBuilder(appearance.LookKey);
        foreach (GearLookDefinition look in outfit)
        {
            if (look != null)
            {
                key.Append('|').Append(look.name);
            }
        }

        return key.ToString();
    }

    private void CollectWorn()
    {
        worn.Clear();
        foreach (GearLookDefinition look in outfit)
        {
            if (look == null)
            {
                continue;
            }

            foreach (WornPiece piece in look.For(appearance.gender))
            {
                if (piece != null)
                {
                    worn.Add(piece);
                }
            }
        }
    }

    // The model's children (Geometry and the skeleton) go straight under this object, so the avatar's bone paths match.
    private void ReplaceSkeleton(CharacterBodyDefinition body)
    {
        DestroyChild(parts);
        DestroyChild(geometry);
        DestroyChild(skeleton);
        parts = null;

        GameObject model = Instantiate(body.Model);
        geometry = null;
        skeleton = null;
        while (model.transform.childCount > 0)
        {
            Transform child = model.transform.GetChild(0);
            child.SetParent(transform, false);
            if (child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                geometry = child;
            }
            else
            {
                skeleton = child;
            }
        }

        DestroyObject(model);
        animator.avatar = body.Avatar;
        animator.Rebind();
        builtGender = (int)body.Gender;
        foreach (SkinnedMeshRenderer mesh in geometry.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Prepare(mesh);
        }
    }

    private void IndexBones()
    {
        bones.Clear();
        if (skeleton == null)
        {
            return;
        }

        foreach (Transform bone in skeleton.GetComponentsInChildren<Transform>(true))
        {
            if (!bones.ContainsKey(bone.name))
            {
                bones.Add(bone.name, bone);
            }
        }
    }

    private void PaintBody(CharacterBodyDefinition body, BodyCover covered)
    {
        bodyMaterials.Clear();
        Add(bodyMaterials, body.Skin(appearance.skin));
        Add(bodyMaterials, body.Underclothing(appearance.underclothes));
        foreach (WornPiece piece in worn)
        {
            Add(bodyMaterials, piece.skinOverlay);
        }

        headMaterials.Clear();
        Add(headMaterials, body.Face(appearance.skin));
        Add(headMaterials, body.Stubble(appearance.hairColour));
        Material eye = body.Eye(appearance.eyes);
        Material brow = body.BrowTint(appearance.hairColour);

        foreach (SkinnedMeshRenderer mesh in geometry.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            BodyPart part = PartOf(mesh.name);
            switch (part)
            {
                case BodyPart.Head:
                    mesh.sharedMaterials = headMaterials.ToArray();
                    break;
                case BodyPart.Eyes:
                    if (eye != null)
                    {
                        mesh.sharedMaterial = eye;
                    }

                    break;
                case BodyPart.Brows:
                    if (brow != null)
                    {
                        mesh.sharedMaterial = brow;
                    }

                    break;
                case BodyPart.Other:
                    break;
                default:
                    mesh.sharedMaterials = bodyMaterials.ToArray();
                    mesh.enabled = (covered & CoverOf(part)) == 0;
                    break;
            }
        }
    }

    private void RebuildParts(CharacterBodyDefinition body, BodyCover covered)
    {
        DestroyChild(parts);
        parts = new GameObject(PartsName).transform;
        parts.SetParent(transform, false);

        if ((covered & BodyCover.Hair) == 0)
        {
            AddPiece(body.Hair(appearance.hair, appearance.hairColour), body);
        }

        AddPiece(body.Brow(appearance.brows, appearance.hairColour), body);
        if (appearance.gender == CharacterGender.Male)
        {
            AddPiece(body.Beard(appearance.beard, appearance.hairColour), body);
        }

        foreach (WornPiece piece in worn)
        {
            AddPiece(piece.prefab, body);
        }
    }

    // Each skinned mesh of the piece is bound to this skeleton by bone name and moved under the parts holder; the piece's
    // own copy of the skeleton is thrown away.
    private void AddPiece(GameObject prefab, CharacterBodyDefinition body)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject piece = Instantiate(prefab, parts, false);
        foreach (SkinnedMeshRenderer mesh in piece.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!mesh.gameObject.activeSelf || !mesh.enabled)
            {
                continue;
            }

            Transform[] source = mesh.bones;
            var bound = new Transform[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                bound[i] = source[i] != null && bones.TryGetValue(source[i].name, out Transform bone) ? bone : null;
            }

            mesh.bones = bound;
            mesh.rootBone = mesh.rootBone != null && bones.TryGetValue(mesh.rootBone.name, out Transform root) ? root : skeleton;
            mesh.sharedMaterials = Reskin(mesh.sharedMaterials, body);
            Prepare(mesh);
            mesh.transform.SetParent(parts, false);
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = Vector3.one;
        }

        DestroyObject(piece);
    }

    // Where a piece shows skin it carries the body's or head's preview material: put the real skin there.
    private Material[] Reskin(Material[] materials, CharacterBodyDefinition body)
    {
        var result = new List<Material>(materials.Length + 2);
        foreach (Material material in materials)
        {
            if (material == null)
            {
                continue;
            }

            if (Matches(material, body.BodyPlaceholder) || body.IsSkin(material))
            {
                result.AddRange(bodyMaterials);
            }
            else if (Matches(material, body.HeadPlaceholder) || body.IsFace(material))
            {
                result.AddRange(headMaterials);
            }
            else if (!body.IsStubble(material))
            {
                result.Add(material);
            }
        }

        return result.ToArray();
    }

    private static bool Matches(Material material, Material placeholder) =>
        placeholder != null && (material == placeholder || material.name == placeholder.name);

    private static void Prepare(SkinnedMeshRenderer mesh)
    {
        mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mesh.updateWhenOffscreen = false;
        mesh.skinnedMotionVectors = false;
    }

    private static void Add(List<Material> list, Material material)
    {
        if (material != null)
        {
            list.Add(material);
        }
    }

    private void DestroyChild(Transform child)
    {
        if (child != null)
        {
            DestroyObject(child.gameObject);
        }
    }

    private static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            // Out of the hierarchy now, so bone lookups and rebinds this frame don't see it.
            target.transform.SetParent(null, false);
            target.SetActive(false);
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private enum BodyPart
    {
        Other,
        Torso,
        Bracers,
        Hands,
        Legs,
        Boots,
        Feet,
        Head,
        Eyes,
        Brows,
    }

    // The two bodies name their parts slightly differently ("Head" and "Head 1", "Feet" and "Feet_Naked").
    private static BodyPart PartOf(string meshName)
    {
        string name = meshName.ToLowerInvariant();
        if (name.StartsWith("torso")) return BodyPart.Torso;
        if (name.StartsWith("bracers")) return BodyPart.Bracers;
        if (name.StartsWith("hands")) return BodyPart.Hands;
        if (name.StartsWith("pants")) return BodyPart.Legs;
        if (name.StartsWith("boots")) return BodyPart.Boots;
        if (name.StartsWith("feet")) return BodyPart.Feet;
        if (name.StartsWith("head") || name.StartsWith("ears")) return BodyPart.Head;
        if (name.StartsWith("eyes")) return BodyPart.Eyes;
        if (name.StartsWith("brows")) return BodyPart.Brows;
        return BodyPart.Other;
    }

    private static BodyCover CoverOf(BodyPart part)
    {
        switch (part)
        {
            case BodyPart.Torso: return BodyCover.Torso;
            case BodyPart.Bracers: return BodyCover.Bracers;
            case BodyPart.Hands: return BodyCover.Hands;
            case BodyPart.Legs: return BodyCover.Legs;
            case BodyPart.Boots: return BodyCover.Boots;
            case BodyPart.Feet: return BodyCover.Feet;
            default: return BodyCover.None;
        }
    }
}
