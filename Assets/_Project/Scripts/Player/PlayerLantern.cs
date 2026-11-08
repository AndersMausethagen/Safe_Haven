using UnityEngine;

/// <summary>
/// The lantern on the player's belt (the Light slot): it hangs at the left hip, swings as the player moves, and while lit
/// (L) shines with its LanternDefinition's light, breathing like the fireflies inside it, and burns down in in-game hours.
/// What is left is the lantern's own condition (ItemStack.LightHours), written back every few in-game minutes, so it is
/// saved with the gear and shown in the slot. Empty, it goes dark; L then refills it from the bag if there is a firefly.
/// With no lantern worn, L is the torch's (PlayerEquipment.ToggleTorch). Both hands stay free.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerEquipment))]
public sealed class PlayerLantern : MonoBehaviour
{
    // Light used up is written to the lantern in steps this big (in-game hours), not every frame.
    private const float WriteEvery = 0.05f;
    // How far and how fast it swings on its cord.
    private const float SwingSpring = 38f;
    private const float SwingDamping = 5f;
    private const float SwingMost = 0.09f;

    private PlayerEquipment equipment;
    private ItemInventory items;
    private Animator animator;
    private Transform hips;
    private DayNightCycle clock;
    private float lookedForClock = -10f;
    private ItemDefinition shownItem;
    private GameObject model;
    private Light glow;
    private Renderer[] glass = System.Array.Empty<Renderer>();
    private Transform[] motes = System.Array.Empty<Transform>();
    private Vector3[] moteHome = System.Array.Empty<Vector3>();
    private MaterialPropertyBlock block;
    private float burnt;
    private float seed;
    private bool lit = true;
    private Vector3 swing;
    private Vector3 swingVelocity;
    private Vector3 lastHips;

    /// <summary>The local player's lantern, or null.</summary>
    public static PlayerLantern Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>The lantern worn, or empty.</summary>
    public ItemStack Worn => equipment != null ? equipment.Gear.Slots.GetSlot((int)GearSlot.Light) : ItemStack.Empty;

    /// <summary>True while it shines: worn, lit and not burnt out.</summary>
    public bool IsLit => lit && !Worn.IsEmpty && Worn.Item.IsLantern && Worn.LightHours > 0f;

    /// <summary>Its light while lit (fireflies come to it), else null.</summary>
    public Light Glow => IsLit && glow != null && glow.enabled ? glow : null;

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        items = GetComponent<ItemInventory>();
        block = new MaterialPropertyBlock();
        seed = Random.value * 50f;
    }

    private void OnEnable()
    {
        if (GetComponent<Player>() == Player.Current || Current == null)
        {
            Current = this;
        }
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }

        if (model != null)
        {
            Destroy(model);
            model = null;
            shownItem = null;
        }
    }

    /// <summary>
    /// Lights or puts out the worn lantern (L). An empty one is refilled first when the bag has its refill. False when no
    /// lantern is worn, so the key can go to the torch instead.
    /// </summary>
    public bool Toggle()
    {
        ItemStack worn = Worn;
        if (worn.IsEmpty || !worn.Item.IsLantern)
        {
            return false;
        }

        if (!lit || worn.LightHours > 0f)
        {
            lit = !lit;
            if (lit && worn.LightHours <= 0f && !RefillWorn())
            {
                lit = false;
                Dark(worn.Item);
            }

            return true;
        }

        if (!RefillWorn())
        {
            Dark(worn.Item);
        }

        return true;
    }

    private bool RefillWorn() => items != null && items.Refill(equipment.Gear.Slots, (int)GearSlot.Light);

    private static void Dark(ItemDefinition lantern)
    {
        ItemDefinition refill = lantern.Lantern.Refill;
        GameMessages.Post(refill != null
            ? $"The {lantern.DisplayName} is dark. Catch a {refill.DisplayName.ToLowerInvariant()} to fill it."
            : $"The {lantern.DisplayName} is dark.", MessageKind.Warning);
    }

    private void Update()
    {
        ItemStack worn = Worn;
        ItemDefinition item = !worn.IsEmpty && worn.Item.IsLantern ? worn.Item : null;
        if (item != shownItem)
        {
            Show(item);
        }

        if (item == null || !lit)
        {
            return;
        }

        float hours = worn.LightHours;
        if (hours <= 0f)
        {
            // Burnt out: a firefly from the bag goes in by itself, as a player would do it.
            if (!RefillWorn())
            {
                lit = false;
                Dark(item);
            }

            return;
        }

        burnt += Time.deltaTime * HoursPerSecond();
        if (burnt >= WriteEvery || burnt >= hours)
        {
            equipment.Gear.Slots.SetDurability((int)GearSlot.Light, worn.WithLightHours(Mathf.Max(0f, hours - burnt)).Durability);
            burnt = 0f;
        }
    }

    private void LateUpdate()
    {
        if (model == null)
        {
            return;
        }

        if (hips == null)
        {
            FindHips();
        }

        Transform body = transform;
        Vector3 anchor = (hips != null ? hips.position : body.position + Vector3.up) + body.rotation * shownItem.Lantern.BeltOffset;
        // Hung on a cord: it lags behind the hips and swings back.
        Vector3 moved = anchor - lastHips;
        lastHips = anchor;
        if (Time.deltaTime > 0f && moved.sqrMagnitude < 1f)
        {
            swingVelocity += (-moved / Time.deltaTime * 0.02f - swing * SwingSpring - swingVelocity * SwingDamping) * Time.deltaTime;
            swing = Vector3.ClampMagnitude(swing + swingVelocity * Time.deltaTime, SwingMost);
        }

        Vector3 hang = Vector3.down * 0.2f + new Vector3(swing.x, 0f, swing.z);
        model.transform.SetPositionAndRotation(anchor + new Vector3(swing.x, 0f, swing.z) * 0.5f,
            Quaternion.FromToRotation(Vector3.down, hang.normalized) * Quaternion.Euler(0f, body.eulerAngles.y, 0f));

        LanternDefinition lantern = shownItem.Lantern;
        bool shining = IsLit;
        float pulse = shining ? lantern.Pulse(Time.time, seed) : 0f;
        if (glow != null)
        {
            glow.enabled = shining;
            glow.intensity = lantern.Intensity * pulse;
        }

        Color emission = lantern.Colour * (lantern.Glow * pulse);
        foreach (Renderer renderer in glass)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", emission);
            block.SetColor("_BaseColor", Color.Lerp(new Color(0.08f, 0.1f, 0.07f), lantern.Colour * 0.6f, shining ? 1f : 0f));
            renderer.SetPropertyBlock(block);
        }

        // The fireflies inside wander about the glass, and only show while it is lit.
        for (int i = 0; i < motes.Length; i++)
        {
            if (motes[i] == null) continue;
            motes[i].gameObject.SetActive(shining);
            float t = Time.time * 0.9f + i * 13.7f + seed;
            motes[i].localPosition = moteHome[i] + new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, t) - 0.5f, Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 0.08f;
        }
    }

    private float HoursPerSecond()
    {
        if (clock == null && Time.unscaledTime - lookedForClock > 5f)
        {
            lookedForClock = Time.unscaledTime;
            clock = FindAnyObjectByType<DayNightCycle>();
        }

        // Without a clock (a scene with no day), an in-game day is the usual 24 minutes.
        return clock != null && clock.Profile != null
            ? clock.ClockSpeed * 24f / (Mathf.Max(0.1f, clock.Profile.DayLengthMinutes) * 60f)
            : 24f / (24f * 60f);
    }

    private void Show(ItemDefinition item)
    {
        if (model != null)
        {
            Destroy(model);
        }

        model = null;
        glow = null;
        glass = System.Array.Empty<Renderer>();
        motes = System.Array.Empty<Transform>();
        shownItem = item;
        burnt = 0f;
        if (item == null || item.Lantern.Model == null)
        {
            return;
        }

        model = Instantiate(item.Lantern.Model);
        model.name = item.DisplayName + " (worn)";
        foreach (Collider collider in model.GetComponentsInChildren<Collider>())
        {
            Destroy(collider);
        }

        glow = model.GetComponentInChildren<Light>(true);
        if (glow != null)
        {
            glow.color = item.Lantern.Colour;
            glow.range = item.Lantern.Range;
            glow.shadows = LightShadows.None;
        }

        var found = new System.Collections.Generic.List<Renderer>();
        var moteList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Glow") && child.TryGetComponent(out Renderer renderer)) found.Add(renderer);
            if (child.name.StartsWith("Mote")) moteList.Add(child);
        }

        glass = found.ToArray();
        motes = moteList.ToArray();
        moteHome = System.Array.ConvertAll(motes, m => m.localPosition);
        FindHips();
        lastHips = hips != null ? hips.position : transform.position;
    }

    private void FindHips()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            animator = GetComponentInChildren<Animator>();
        }

        hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
    }
}
