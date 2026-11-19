using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows what the player holds in the right hand: the equipped main-hand weapon, or, while gathering, the tool for the
/// job from the tool belt (axe, pickaxe, sickle), then the weapon again. A bow is held in the left hand (the off-hand
/// grip), as archers do; the right hand draws the string. Models come from the item
/// (<see cref="ItemDefinition.HeldModel"/>, placed by its held pose) and sit in a grip under the hand bone; each is made
/// once and reused. Held models are only visuals: their colliders, rigidbodies and scripts are removed.
/// <para>Where it belongs: on the PlayerArmature prefab. The grip (relative to the hand bone) is fitted by
/// Safe Haven > Content > Held Item Grips, so models made for a standard hand prop bone sit right.</para>
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
    private PlayerTransport transportUser;
    private GameObject glowing;
    private EnchantmentDefinition glowEnchantment;
    private int glowEmpower;
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
        artCombat = GetComponent<PlayerCombat>();
        artFocus = GetComponent<PlayerFocus>();
        gathering = GetComponent<PlayerGathering>();
        swimming = GetComponent<PlayerSwimming>();
        transportUser = GetComponent<PlayerTransport>();
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

    /// <summary>The weapon in hand is out of it, thrown (a weapon art): its model is hidden until it comes back.</summary>
    public bool Thrown { get; set; }

    /// <summary>The model in the right hand right now, or null.</summary>
    public GameObject Current => current;

    private void LateUpdate()
    {
        ItemDefinition wanted = Wanted();
        if (wanted != Shown)
        {
            Show(wanted);
        }

        if (current != null && current.activeSelf == Thrown)
        {
            current.SetActive(!Thrown);
        }

        UpdateArtReady();

        UpdateGlow();
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

    // ----- The weapon art ready (teach the verbs, Docs/CombatResearch.md §3.3 #13) -----
    // When Focus can pay for the art in hand, a faint ember light breathes on the weapon: the art (middle mouse) is there to use.
    private static readonly Color ArtReadyColour = new Color(1f, 0.62f, 0.3f);
    private const float ArtReadyIntensity = 0.9f;
    private PlayerCombat artCombat;
    private PlayerFocus artFocus;
    private Light artLight;

    /// <summary>True while the weapon in hand glows: its art can be paid for in Focus.</summary>
    public bool ArtReady { get; private set; }

    private void UpdateArtReady()
    {
        WeaponDefinition weapon = artCombat != null ? artCombat.Weapon : null;
        ArtReady = current != null && current.activeInHierarchy && weapon != null && weapon.WeaponArt != null && !weapon.TakesAttackInput
            && artFocus != null && artFocus.CanSpend(weapon.WeaponArtFocus) && weapon.WeaponArtFocus > 0f;
        // A counter ready (after a parry or a perfect dodge) lights the weapon in the counter's own pale steel for as long as it lasts:
        // the window was a sound alone, with nothing to say how long it stays (Docs/CombatFeelReview_2026-11-19.md §7).
        bool counter = current != null && current.activeInHierarchy && artCombat != null && artCombat.CounterIsReady;
        // A heavy held to charge glows hotter as it fills (§11): letting go early or late was a blind count.
        bool charging = current != null && current.activeInHierarchy && artCombat != null && artCombat.IsCharging;
        if (!ArtReady && !counter && !charging)
        {
            if (artLight != null && artLight.enabled) artLight.enabled = false;
            return;
        }

        if (artLight == null)
        {
            var glow = new GameObject("Art Ready Glow");
            artLight = glow.AddComponent<Light>();
            artLight.type = LightType.Point;
            artLight.color = ArtReadyColour;
            artLight.range = 1.1f;
            artLight.shadows = LightShadows.None;
        }

        if (artLight.transform.parent != current.transform)
        {
            artLight.transform.SetParent(current.transform, false);
            artLight.transform.localPosition = Vector3.up * 0.4f;
        }

        artLight.enabled = true;
        artLight.range = 1.1f;
        if (charging)
        {
            CombatFeelSettings feel = CombatFeelSettings.Current;
            float charge = artCombat.Charge;
            artLight.color = Color.Lerp(feel.ChargeLightLow, feel.ChargeLightHigh, charge);
            artLight.range = Mathf.Lerp(feel.ChargeLightRangeLow, feel.ChargeLightRangeHigh, charge);
            float flicker = charge >= 1f ? 0.8f + 0.2f * Mathf.PerlinNoise(Time.unscaledTime * 24f, 0.5f) : 1f;
            artLight.intensity = Mathf.Lerp(feel.ChargeLightIntensityLow, feel.ChargeLightIntensityHigh, charge) * flicker;
            return;
        }

        if (counter)
        {
            artLight.color = CounterReadyColour;
            artLight.intensity = CounterReadyIntensity * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f * 2f * Mathf.PI));
            return;
        }

        artLight.color = ArtReadyColour;
        artLight.intensity = ArtReadyIntensity * (0.55f + 0.45f * Mathf.Sin(Time.time * 4f));
    }

    // The counter's own colour (its slash is tinted so: CombatFeedback), brighter than the art's glow, pulsing quickly while it lasts.
    private static readonly Color CounterReadyColour = new Color(0.55f, 0.85f, 1f, 1f);
    private const float CounterReadyIntensity = 1.4f;

    // Only touches the materials when what is in the hand, or what was done to it, has changed.
    private void UpdateGlow()
    {
        ItemStack held = equipment != null && equipment.MainHand.Item == Shown ? equipment.MainHand : ItemStack.Empty;
        if (current == glowing && held.Enchantment == glowEnchantment && held.Empower == glowEmpower)
        {
            return;
        }

        glowing = current;
        glowEnchantment = held.Enchantment;
        glowEmpower = held.Empower;
        GearGlow.Apply(current, held, EssenceSettings.Current);
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

    // In the water or with the hands on a cart or the oars, what is held is put away.
    private bool IsSwimming => (swimming != null && swimming.IsSwimming) || (transportUser != null && transportUser.Using != null);

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
        return weapon.IsEmpty || weapon.IsBroken || equipment.IsSheathed ? null : weapon.Item;
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
