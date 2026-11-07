using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A friend as you see them in co-op: a modular body built like the character preview's (no player components, so
/// nothing in the game mistakes it for you), dressed from their <see cref="PlayerLookState"/>, holding what they hold,
/// moved to where they are and animated as their own game animates them (<see cref="AnimatorMirror"/>), with their name
/// over their head. Made and driven by <see cref="NetworkPlayerAvatar"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class RemotePlayerBody : MonoBehaviour
{
    private readonly List<GearLookDefinition> outfit = new List<GearLookDefinition>();
    private ModularCharacter character;
    private Animator animator;
    private AnimatorMirror mirror;
    private TextMeshPro nameTag;
    private Transform grip;
    private GameObject heldModel;
    private string heldId = string.Empty;
    private Pose gripPose;
    private Camera viewer;

    public AnimatorMirror Mirror => mirror;

    public static RemotePlayerBody Create(Transform parent, MultiplayerSettings settings)
    {
        var holder = new GameObject("Remote Player Body");
        holder.transform.SetParent(parent, false);
        holder.SetActive(false);
        var animator = holder.AddComponent<Animator>();
        animator.runtimeAnimatorController = settings.BodyAnimations;
        animator.applyRootMotion = false;
        // The clips' events call into the player's own components, which a friend's body doesn't have.
        animator.fireEvents = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        var body = holder.AddComponent<RemotePlayerBody>();
        body.animator = animator;
        body.gripPose = settings.GripPose;
        body.character = holder.AddComponent<ModularCharacter>();
        body.mirror = new AnimatorMirror(animator);
        body.nameTag = NameTag(holder.transform, settings.NameHeight);
        holder.SetActive(true);
        return body;
    }

    /// <summary>Dresses the body as <paramref name="look"/> says; only what changed is rebuilt.</summary>
    public void Dress(PlayerLookState look, IContentLookup content)
    {
        CharacterAppearance appearance = look.Appearance();
        character.SetAppearance(appearance);
        PlayerLook.Outfit(slot => look.Worn(slot, content), appearance.clothes, outfit);
        character.SetOutfit(outfit);
        nameTag.text = look.DisplayName;
        Hold(look.Held.ToString(), content);
    }

    /// <summary>"Astrid · Down!" in red over a friend who is down; their plain name otherwise.</summary>
    public void ShowState(string name, bool down, bool fallen)
    {
        UIStyle style = UIStyle.Current;
        string shown = down ? $"{name} · Down!" : fallen ? $"{name} · Fallen" : name;
        if (nameTag.text != shown)
        {
            nameTag.text = shown;
            nameTag.color = down || fallen ? style.Danger : style.Text;
        }
    }

    public void Place(Vector3 position, float yaw)
    {
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    private void Awake()
    {
        if (character != null)
        {
            character.SkeletonReplaced += ForgetGrip;
        }
    }

    private void OnDestroy()
    {
        if (character != null)
        {
            character.SkeletonReplaced -= ForgetGrip;
        }
    }

    private void LateUpdate()
    {
        // The name faces the camera and stays upright.
        if (viewer == null || !viewer.isActiveAndEnabled)
        {
            viewer = Camera.main;
        }

        if (viewer != null && nameTag != null)
        {
            nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - viewer.transform.position, Vector3.up);
        }
    }

    private void Hold(string id, IContentLookup content)
    {
        if (id == heldId && (heldModel != null || id.Length == 0))
        {
            return;
        }

        heldId = id;
        if (heldModel != null)
        {
            Destroy(heldModel);
            heldModel = null;
        }

        ItemDefinition item = id.Length > 0 && content != null ? content.FindItem(id) : null;
        Transform hand = Grip();
        if (item != null && hand != null)
        {
            heldModel = HeldItemView.CreateModel(item, hand);
        }
    }

    private Transform Grip()
    {
        if (grip != null)
        {
            return grip;
        }

        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null)
        {
            return null;
        }

        grip = new GameObject("Held Item Grip").transform;
        grip.SetParent(hand, false);
        grip.SetLocalPositionAndRotation(gripPose.position, gripPose.rotation);
        return grip;
    }

    // A new gender brings a new skeleton: the old hand (and what it held) went with it.
    private void ForgetGrip()
    {
        grip = null;
        heldModel = null;
        heldId = string.Empty;
    }

    private static TextMeshPro NameTag(Transform parent, float height)
    {
        var holder = new GameObject("Name");
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = new Vector3(0f, height, 0f);
        var text = holder.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 2.4f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
        // Hearthline's text-on-the-world: the body font with its soft shadow (a shared material, not one per friend).
        UIStyle style = UIStyle.Current;
        style.OnWorld(text);
        text.color = style.Text;
        return text;
    }
}
