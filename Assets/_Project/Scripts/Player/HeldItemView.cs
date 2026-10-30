using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows what the player holds in the right hand: the equipped main-hand weapon, or, while gathering, the tool for the
/// job from the tool belt (axe, pickaxe, sickle), then the weapon again. A bow is held in the left hand (the off-hand
/// grip), as archers do; the right hand draws the string. Models come from the item
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
    [SerializeField] private Vector3 offHandGripPosition;
    [SerializeField] private Vector3 offHandGripRotation;

    private readonly Dictionary<ItemDefinition, GameObject> models = new Dictionary<ItemDefinition, GameObject>();
    private PlayerEquipment equipment;
    private PlayerGathering gathering;
    private PlayerSwimming swimming;
    private Transform grip;
    private GameObject current;
    private Transform offHandGrip;
    private GameObject offHandModel;
    private ItemDefinition offHandShown;
    private readonly Dictionary<ItemDefinition, GameObject> offHandModels = new Dictionary<ItemDefinition, GameObject>();
    public ItemDefinition OffHandShown => offHandShown;

    /// <summary>The item in the hand right now (null when empty-handed).</summary>
    public ItemDefinition Shown { get; private set; }

    /// <summary>The model of <see cref="Shown"/> (null when there is none), e.g. for the bow's string.</summary>
    public GameObject ShownModel => current;

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
        swimming = GetComponent<PlayerSwimming>();
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
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (left != null)
        {
            offHandGrip = new GameObject("Off Hand Item Grip").transform;
            offHandGrip.SetParent(left, false);
            offHandGrip.SetLocalPositionAndRotation(offHandGripPosition, Quaternion.Euler(offHandGripRotation));
        }
    }

    private void LateUpdate()
    {
        ItemDefinition wanted = Wanted();
        if (wanted != Shown)
        {
            Show(wanted);
        }
        ItemStack offHand = equipment != null ? equipment.OffHand : ItemStack.Empty;
        // Gathering tools and broken lights do not leave a burning torch in the other hand.
        bool previewOffHand = Preview != null && Preview.IsWeapon && Preview.Weapon.CanUseOffHand;
        bool needsBothHands = wanted != null && wanted.IsWeapon && wanted.Weapon.RequiresBothHands;
        ItemDefinition offItem = previewOffHand ? Preview : !offHand.IsEmpty && !offHand.IsBroken && !IsSwimming &&
            !needsBothHands && (gathering == null || gathering.Session == null) ? offHand.Item : null;
        if (offItem != offHandShown)
        {
            if (offHandModel != null) offHandModel.SetActive(false);
            offHandShown = offItem;
            offHandModel = ModelFor(offItem, offHandGrip, offHandModels);
        }
        if (IsTorch(Shown) && current != null) current.transform.rotation = Quaternion.identity;
        if (offHandModel != null && IsTorch(offHandShown)) offHandModel.transform.rotation = Quaternion.identity;
    }

    private static bool IsTorch(ItemDefinition item) => item != null && item.IsWeapon && item.Weapon.Kind == WeaponKind.Torch;

    /// <summary>Bows are held in the off (left) hand.</summary>
    public static bool IsBow(ItemDefinition item) => item != null && item.IsWeapon && item.Weapon.IsBow;

    /// <summary>Where off-hand models (a bow, a torch) sit relative to the left hand bone.</summary>
    public Pose OffHandGripPose => new Pose(offHandGripPosition, Quaternion.Euler(offHandGripRotation));

    /// <summary>A visual-only copy of <paramref name="item"/>'s held model on <paramref name="grip"/>, placed by the
    /// item's held pose and scale. Also how companions hold their weapons. Null for an item without a model.</summary>
    public static GameObject CreateModel(ItemDefinition item, Transform grip)
    {
        if (item == null || item.HeldModel == null || grip == null) return null;
        GameObject model = Instantiate(item.HeldModel, grip);
        model.name = item.HeldModel.name + " (held)";
        StripToVisual(model, IsTorch(item));
        model.transform.SetLocalPositionAndRotation(item.HeldPose.position, item.HeldPose.rotation);
        model.transform.localScale = Vector3.one * item.HeldScale;
        return model;
    }

    private GameObject ModelFor(ItemDefinition item, Transform parent, Dictionary<ItemDefinition, GameObject> cache)
    {
        if (item == null || item.HeldModel == null || parent == null) return null;
        if (!cache.TryGetValue(item, out GameObject model) || model == null)
        {
            model = CreateModel(item, parent);
            cache[item] = model;
        }
        model.transform.SetLocalPositionAndRotation(item.HeldPose.position, item.HeldPose.rotation);
        model.transform.localScale = Vector3.one * item.HeldScale;
        model.SetActive(true);
        return model;
    }

    private bool IsSwimming => swimming != null && swimming.IsSwimming;

    /// <summary>The tool while gathering with one on the belt, else the main-hand weapon; nothing while swimming.</summary>
    private ItemDefinition Wanted()
    {
        if (Preview != null && !(Preview.IsWeapon && Preview.Weapon.CanUseOffHand))
        {
            return Preview;
        }

        if (equipment == null || IsSwimming)
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
        return weapon.IsEmpty || weapon.IsBroken ? null : weapon.Item;
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

        current = ModelFor(item, IsBow(item) && offHandGrip != null ? offHandGrip : grip, models);
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
        foreach (GameObject model in offHandModels.Values)
            if (model != null) Destroy(model);
        offHandModels.Clear();
        offHandModel = null;
        offHandShown = null;
        current = null;
        Shown = null;
        Show(Wanted());
    }

    /// <summary>Removes everything but transforms, meshes and renderers: a held model must not collide, fall or run
    /// its world behaviour (pickups, interactables).</summary>
    public static void StripToVisual(GameObject model, bool keepTorchLight = false)
    {
        Component[] components = model.GetComponentsInChildren<Component>(true);
        // Remove behaviours before their required components (e.g. LightFlicker requires Light).
        for (int pass = 0; pass < 2; pass++)
        foreach (Component component in components)
        {
            if (component == null || (component is MonoBehaviour) != (pass == 0)) continue;
            if (component is Transform || component is MeshFilter || component is Renderer || component is LODGroup ||
                keepTorchLight && (component is Light || component is LightFlicker || component is AtmosphereLight))
            {
                continue;
            }

            if (component is Collider collider)
            {
                collider.enabled = false;
            }
            if (component is Behaviour behaviour) behaviour.enabled = false;

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }

            if (keepTorchLight)
            {
                foreach (Light light in model.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Point && light.GetComponent<AtmosphereLight>() == null)
                        light.gameObject.AddComponent<AtmosphereLight>();
                }
            }
        }
    }
}
