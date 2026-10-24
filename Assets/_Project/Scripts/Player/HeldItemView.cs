using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows what the player holds in the right hand: the equipped main-hand weapon, or, while gathering, the tool for the
/// job from the tool belt (axe, pickaxe, sickle), then the weapon again. Models come from the item
/// (<see cref="ItemDefinition.HeldModel"/>, placed by its held pose) and sit in a grip under the hand bone; each is made
/// once and reused. Held models are only visuals: their colliders, rigidbodies and scripts are removed.
/// <para>Where it belongs: on the PlayerArmature prefab. The grip (relative to the hand bone) is fitted by
/// Safe Haven > Characters > Held Item Grips, so models made for a standard hand prop bone sit right.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class HeldItemView : MonoBehaviour
{
    [Tooltip("Where held models sit relative to the right hand bone (the grip).")]
    [SerializeField] private Vector3 gripPosition;
    [SerializeField] private Vector3 gripRotation;

    private readonly Dictionary<ItemDefinition, GameObject> models = new Dictionary<ItemDefinition, GameObject>();
    private PlayerEquipment equipment;
    private PlayerGathering gathering;
    private Transform grip;
    private GameObject current;

    /// <summary>The item in the hand right now (null when empty-handed).</summary>
    public ItemDefinition Shown { get; private set; }

    /// <summary>An item to hold instead of the equipped one (the grip tool's preview); null for normal play.</summary>
    public ItemDefinition Preview { get; set; }

    public Transform Grip => grip;

    public Pose GripPose
    {
        get => new Pose(gripPosition, Quaternion.Euler(gripRotation));
        set
        {
            gripPosition = value.position;
            gripRotation = value.rotation.eulerAngles;
            if (grip != null)
            {
                grip.SetLocalPositionAndRotation(gripPosition, value.rotation);
            }
        }
    }

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        gathering = GetComponent<PlayerGathering>();
        Animator animator = GetComponentInChildren<Animator>();
        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null)
        {
            Debug.LogWarning($"{name}: Held Item View needs a humanoid Animator with a right hand bone.", this);
            enabled = false;
            return;
        }

        grip = new GameObject("Held Item Grip").transform;
        grip.SetParent(hand, false);
        grip.SetLocalPositionAndRotation(gripPosition, Quaternion.Euler(gripRotation));
    }

    private void LateUpdate()
    {
        ItemDefinition wanted = Wanted();
        if (wanted != Shown)
        {
            Show(wanted);
        }
    }

    /// <summary>The tool while gathering with one on the belt, else the main-hand weapon.</summary>
    private ItemDefinition Wanted()
    {
        if (Preview != null)
        {
            return Preview;
        }

        if (equipment == null)
        {
            return null;
        }

        if (gathering != null && gathering.Session != null && GearSlots.TryGetToolSlot(gathering.Tool, out GearSlot slot))
        {
            ItemStack tool = equipment.Gear.Get(slot);
            if (!tool.IsEmpty)
            {
                return tool.Item;
            }
        }

        ItemStack weapon = equipment.MainHand;
        return weapon.IsEmpty ? null : weapon.Item;
    }

    /// <summary>Puts <paramref name="item"/>'s model in the hand (nothing for null or an item without a model).</summary>
    public void Show(ItemDefinition item)
    {
        if (current != null)
        {
            current.SetActive(false);
            current = null;
        }

        Shown = item;
        if (item == null || item.HeldModel == null || grip == null)
        {
            return;
        }

        if (!models.TryGetValue(item, out GameObject model) || model == null)
        {
            model = Instantiate(item.HeldModel, grip);
            model.name = item.HeldModel.name + " (held)";
            StripToVisual(model);
            models[item] = model;
        }

        model.transform.SetLocalPositionAndRotation(item.HeldPose.position, item.HeldPose.rotation);
        model.transform.localScale = Vector3.one * item.HeldScale;
        model.SetActive(true);
        current = model;
    }

    /// <summary>Forgets the models made so far (an item's held model or grip changed) and shows the hand again.</summary>
    public void Refresh()
    {
        foreach (GameObject model in models.Values)
        {
            if (model != null)
            {
                Destroy(model);
            }
        }

        models.Clear();
        current = null;
        Shown = null;
        Show(Wanted());
    }

    /// <summary>Removes everything but transforms, meshes and renderers: a held model must not collide, fall or run
    /// its world behaviour (pickups, interactables).</summary>
    public static void StripToVisual(GameObject model)
    {
        foreach (Component component in model.GetComponentsInChildren<Component>(true))
        {
            if (component is Transform || component is MeshFilter || component is Renderer || component is LODGroup)
            {
                continue;
            }

            if (component is Collider collider)
            {
                collider.enabled = false;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }
    }
}
