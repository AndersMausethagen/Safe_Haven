using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a transport is moved: by its handles (Haulable) or at the oars (Rowboat).</summary>
public interface ITransportDriver
{
    /// <summary>True while someone is moving it.</summary>
    bool InUse { get; }
    bool IsHeldBy(GameObject user);
    /// <summary>Stops moving it, however that came about.</summary>
    void LetGo();
    /// <summary>Adds how to take it to its menu.</summary>
    void AddActions(MenuPage page, GameObject user);
}

/// <summary>
/// A transport standing in the world (GDD §8.5): a Rigidbody with cargo. The cargo's weight is real mass, so a full cart
/// is heavy to the physics as well as to the rules, and the same weight sets how much it slows the player
/// (TransportDefinition.SpeedScale). How it moves is its ITransportDriver's business. Left alone it comes to rest and turns
/// kinematic, so it stays where it was left and costs nothing. Using it opens its menu; a cart in the hands lets go at once.
/// <para>
/// Where it belongs: on a transport prefab, with a SaveableEntity (position and cargo are saved) and an Interactable that
/// doesn't deactivate. Its colliders go on the Ignore Raycast layer, so its own wheels don't find it as ground.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(Interactable))]
public sealed class Transport : MonoBehaviour, ISaveable
{
    /// <summary>What a raycast may stand a wheel or a person on: not transports themselves (Ignore Raycast), not water.</summary>
    public const int GroundMask = ~((1 << 2) | (1 << 4));

    private const float RestSpeed = 0.1f;
    private const float RestSeconds = 1.5f;
    private const float FallingSpeed = 6f;
    private const float FallLimit = 20f;

    [Serializable]
    private struct Saved
    {
        public List<SlotSaveData> slots;
    }

    [SerializeField] private TransportDefinition definition;
    [Tooltip("The menu closes when the player walks further than this.")]
    [SerializeField, Min(1f)] private float menuRange = 5f;

    private static readonly List<Transport> all = new List<Transport>();

    private Interactable interactable;
    private SaveableEntity entity;
    private float restedFor;
    private float extraMass;
    private Vector3 safePosition;

    /// <summary>Every transport in the loaded scenes.</summary>
    public static IReadOnlyList<Transport> All => all;

    /// <summary>The cargo in reach of a point, to build and craft from. Null when there is none, which is nearly always.</summary>
    public static List<ItemContainer> CargoNear(Vector3 point)
    {
        List<ItemContainer> near = null;
        float reach = TransportSettings.Current.MaterialReach;
        foreach (Transport transport in all)
        {
            if ((transport.transform.position - point).sqrMagnitude <= reach * reach)
            {
                near ??= new List<ItemContainer>();
                near.Add(transport.Cargo);
            }
        }

        return near;
    }

    public TransportDefinition Definition => definition;
    public ItemContainer Cargo { get; private set; }
    public Rigidbody Body { get; private set; }
    public ITransportDriver Driver { get; private set; }
    public float Weight => Cargo.CurrentWeight;
    public bool IsOverfilled => definition.IsOverfilled(Weight);
    /// <summary>The share of normal speed its load leaves.</summary>
    public float SpeedScale => definition.SpeedScale(Weight);
    public bool IsOverturned => Vector3.Dot(transform.up, Vector3.up) < 0.4f;

    /// <summary>What it takes on top of its cargo: a person at the oars. Counts towards its mass, not its capacity.</summary>
    public float ExtraMass
    {
        get => extraMass;
        set
        {
            extraMass = Mathf.Max(0f, value);
            UpdateMass();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        entity = GetComponent<SaveableEntity>();
        Body = GetComponent<Rigidbody>();
        Driver = GetComponent<ITransportDriver>();
        Cargo = new ItemContainer(definition.Columns, definition.Rows);
        Cargo.SetWeightCapacity(definition.OverloadLimit);
        Cargo.WeightChanged += OnCargoChanged;
        UpdateMass();
    }

    private void OnEnable()
    {
        all.Add(this);
        interactable.Completed.AddListener(OnUsed);
        safePosition = transform.position;
    }

    private void Start() => TransportHud.Create();

    private void OnDisable()
    {
        all.Remove(this);
        interactable.Completed.RemoveListener(OnUsed);
        if (Driver != null && Driver.InUse)
        {
            Driver.LetGo();
        }
    }

    private void FixedUpdate()
    {
        if (Body.isKinematic)
        {
            return;
        }

        CatchFall();
        bool resting = !Driver.InUse && Body.linearVelocity.sqrMagnitude < RestSpeed * RestSpeed && Body.angularVelocity.sqrMagnitude < RestSpeed;
        restedFor = resting ? restedFor + Time.fixedDeltaTime : 0f;
        if (restedFor >= RestSeconds)
        {
            Body.isKinematic = true;
        }
    }

    // Physics can lose an object through a seam in the ground, and what it carries is someone's whole haul. It goes back
    // to where it last stood and stays there until it is taken up again.
    private void CatchFall()
    {
        if (Body.linearVelocity.y > -FallingSpeed)
        {
            safePosition = Body.position;
        }
        else if (Body.position.y < safePosition.y - FallLimit)
        {
            if (Driver.InUse)
            {
                Driver.LetGo();
            }

            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = safePosition + Vector3.up * 0.5f;
            Body.isKinematic = true;
        }
    }

    /// <summary>Hands it back to the physics.</summary>
    public void Wake()
    {
        Body.isKinematic = false;
        restedFor = 0f;
    }

    private void OnCargoChanged()
    {
        UpdateMass();
        Wake();
    }

    private void UpdateMass() => Body.mass = definition.EmptyMass + Weight * definition.MassPerWeight + extraMass;

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (Driver.IsHeldBy(user))
        {
            Driver.LetGo();
        }
        else if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => Page(user), user, transform, menuRange);
        }
    }

    private MenuPage Page(GameObject user)
    {
        bool empty = Cargo.CurrentWeight <= 0f && Cargo.CountItems() == 0;
        var page = new MenuPage { Title = definition.DisplayName, Body = $"{definition.Description}\n\n{LoadText()}" };
        if (IsOverturned)
        {
            page.Add(new MenuAction("Set it upright", null, true, SetUpright));
        }
        else
        {
            Driver.AddActions(page, user);
        }

        page.Add(new MenuAction("Open the cargo", "Move things between it and your bag", true, () => CargoWindow.Create().Open(this, user), closesMenu: true));
        page.Add(new MenuAction("Pack it up", empty ? "Back into your bag as a kit" : "Empty it first", empty, () => PackUp(user), closesMenu: true));
        return page;
    }

    /// <summary>"120 / 200 weight, 15% slower".</summary>
    public string LoadText()
    {
        int slower = Mathf.RoundToInt((1f - SpeedScale) * 100f);
        return $"{Weight:0} / {definition.Capacity:0} weight, {slower}% slower{(IsOverfilled ? ". Overfilled" : "")}";
    }

    private void SetUpright()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Body.Move(transform.position + Vector3.up * 0.6f, Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward, Vector3.up));
        Wake();
    }

    private void PackUp(GameObject user)
    {
        ItemInventory inventory = user != null ? user.GetComponent<ItemInventory>() : null;
        if (inventory == null || Cargo.CountItems() > 0)
        {
            return;
        }

        if (inventory.Container.Add(definition.Kit, 1) < 1)
        {
            GameMessages.Post("No room for it in your bag.", MessageKind.Warning);
            return;
        }

        Remove();
    }

    /// <summary>Takes it out of the world for good.</summary>
    public void Remove()
    {
        if (entity != null)
        {
            entity.MarkDestroyed();
        }

        Destroy(gameObject);
    }

    // ----- Saving -----

    string ISaveable.SaveKey => "transport";

    public string CaptureState() => JsonUtility.ToJson(new Saved { slots = SaveMapper.ToSlots(Cargo) });

    public void RestoreState(string state)
    {
        Saved saved = JsonUtility.FromJson<Saved>(state);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (saved.slots != null && content != null)
        {
            SaveMapper.ReadSlots(saved.slots, Cargo, content, new List<string>());
        }
    }
}
