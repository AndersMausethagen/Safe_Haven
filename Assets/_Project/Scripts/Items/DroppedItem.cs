using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A stack lying in the world, dropped from the bag (ItemInventory.Drop) and waiting to be picked up. It keeps the whole
/// ItemStack, wear and essence work included, in a one-slot container, so it is saved the way any other slot is. At home a
/// drop stays until it is picked up, through saves and raids. A raid scene isn't persistent, so what is dropped there stays
/// behind with the raid.
/// <para>
/// Shows the item's own model, or a sack for the many items that have none. It has no collider on purpose: nobody trips on
/// it, nothing lands on it, and Interactable measures its reach from the object when there are no colliders.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class DroppedItem : MonoBehaviour, ISaveable, IInteractionName, IInteractionVerb
{
    private const float HandHeight = 1.1f;
    private const float GroundSearch = 3f;
    private const float WaterReach = 1.5f;
    private const float ThrowSeconds = 0.3f;
    private const float ThrowArc = 0.5f;

    [Serializable]
    private struct Saved
    {
        public List<SlotSaveData> slots;
    }

    private static readonly List<DroppedItem> all = new List<DroppedItem>();

    [Tooltip("What moves when the stack is thrown down. The item's model is put in here.")]
    [SerializeField] private Transform visual;
    [Tooltip("Shown for items without a model of their own.")]
    [SerializeField] private GameObject sack;

    private Interactable interactable;
    private ItemContainer held;
    private Transform model;
    private Vector3 thrownFrom;
    private float thrown;

    private ItemContainer Held => held ??= new ItemContainer(1, 1);

    /// <summary>Every drop lying in the loaded scene.</summary>
    public static IReadOnlyList<DroppedItem> All => all;

    public ItemStack Stack => Held.GetSlot(0);
    public bool ShowsOwnModel => model != null;
    public string InteractionName => Stack.Count > 1 ? $"{Stack.Count} {Stack.DisplayName}" : Stack.DisplayName;
    public string Verb => "Pick up";
    public string SaveKey => "droppedItem";

    /// <summary>Raised when a player puts something on the ground (co-op shares it with friends on the run: CoopDrops).</summary>
    public static event System.Action<DroppedItem> DroppedByPlayer;

    /// <summary>Co-op: the drop's name in every friend's game on the run, or null when it is only here.</summary>
    public string ShareId { get; set; }

    /// <summary>A friend's drop made here (co-op): at its spot, holding what it held (its saved state). Null when it can't be.</summary>
    public static DroppedItem PlaceAt(string state, Vector3 at, Quaternion facing)
    {
        DroppedItemSettings settings = DroppedItemSettings.Current;
        if (settings.Prefab == null || string.IsNullOrEmpty(state))
        {
            return null;
        }

        var dropped = SaveableEntity.Spawn(settings.Prefab, at, facing).GetComponent<DroppedItem>();
        ((ISaveable)dropped).RestoreState(state);
        return dropped != null && !dropped.Stack.IsEmpty ? dropped : null;
    }

    /// <summary>Taken by someone else (co-op): it goes, without a word.</summary>
    public void Vanish() => Remove();

    /// <summary>Puts a stack on the ground in front of someone. Null when it can't be, and then the stack is still theirs.</summary>
    public static DroppedItem Drop(Transform user, ItemStack stack)
    {
        DroppedItemSettings settings = DroppedItemSettings.Current;
        if (settings.Prefab == null)
        {
            Debug.LogWarning("DroppedItemSettings has no prefab, so nothing can be dropped.", settings);
            return null;
        }

        Quaternion facing = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        var dropped = SaveableEntity.Spawn(settings.Prefab, FindSpot(user, settings), facing).GetComponent<DroppedItem>();
        dropped.Hold(stack);
        if (user != null && Player.IsPlayer(user.gameObject))
        {
            DroppedByPlayer?.Invoke(dropped);
        }

        dropped.ThrowFrom(user.position + Vector3.up * HandHeight);
        AudioManager.Play(AudioManager.Sounds?.ItemDropped, dropped.transform.position);
        return dropped;
    }

    /// <summary>Moves what fits into a bag, and returns how many went. Not a new find, so it raises no ItemObtained: dropping an item and picking it up again must not count towards a quest.</summary>
    public int TakeInto(ItemContainer bag) => Held.TransferTo(0, bag);

    /// <summary>Gives this drop its stack and shows it.</summary>
    public void Hold(ItemStack stack)
    {
        Held.SetSlot(0, stack);
        Show();
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        interactable.Completed.AddListener(OnPickedUp);
        all.Add(this);
        enabled = false;
    }

    private void OnDestroy()
    {
        all.Remove(this);
        interactable.Completed.RemoveListener(OnPickedUp);
    }

    // Entering play mode doesn't reload scripts in this project, so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void Update()
    {
        thrown = Mathf.Min(1f, thrown + Time.deltaTime / ThrowSeconds);
        Vector3 offset = Vector3.Lerp(thrownFrom, Vector3.zero, thrown) + Vector3.up * (4f * ThrowArc * thrown * (1f - thrown));
        visual.localPosition = Quaternion.Inverse(transform.rotation) * offset;
        enabled = thrown < 1f;
    }

    private void ThrowFrom(Vector3 position)
    {
        thrownFrom = position - transform.position;
        thrown = 0f;
        enabled = true;
    }

    private void OnPickedUp()
    {
        // Shared with friends on a raid together: the host decides who gets it (CoopDrops), then PickUp follows.
        if (CoopDrops.Current != null && CoopDrops.Current.Claim(this, interactable.Interactor))
        {
            return;
        }

        PickUp(interactable.Interactor);
    }

    /// <summary>Into the user's bag, as much as fits.</summary>
    public void PickUp(GameObject user)
    {
        ItemInventory bag = user != null ? user.GetComponentInParent<ItemInventory>() : null;
        if (bag == null)
        {
            return;
        }

        ItemStack stack = Stack;
        int moved = TakeInto(bag.Container);
        if (moved <= 0)
        {
            GameMessages.Post($"No room in your bag for {stack.DisplayName}.", MessageKind.Warning);
            return;
        }

        AudioManager.Play(Rarity.StyleOf(stack.Item.Rarity).PickupSound, transform.position);
        if (Stack.IsEmpty)
        {
            GameMessages.Post($"Picked up {moved} {stack.DisplayName}.", MessageKind.Gain);
            Remove();
        }
        else
        {
            GameMessages.Post($"Bag full, still on the ground: {InteractionName}", MessageKind.Warning);
        }
    }

    private void Remove()
    {
        if (TryGetComponent(out SaveableEntity saveable))
        {
            saveable.MarkDestroyed();
        }

        Destroy(gameObject);
    }

    // Ahead of the user. With a wall in the way, or a drop there too far to reach back from, it lands at their feet.
    private static Vector3 FindSpot(Transform user, DroppedItemSettings settings)
    {
        Vector3 hands = user.position + Vector3.up * HandHeight;
        Vector2 scatter = UnityEngine.Random.insideUnitCircle * settings.Scatter;
        Vector3 ahead = hands + user.forward * settings.Reach + new Vector3(scatter.x, 0f, scatter.y);
        bool clear = !Physics.Linecast(hands, ahead, Transport.GroundMask, QueryTriggerInteraction.Ignore);
        if (!(clear && TryFindGround(ahead, out Vector3 spot)) && !TryFindGround(hands, out spot))
        {
            spot = user.position;
        }

        // Floats instead of sinking out of sight.
        if (WaterSurface.TryFind(spot + Vector3.up * WaterReach, WaterReach, out float surface) && surface > spot.y)
        {
            spot.y = surface;
        }

        return spot;
    }

    // The first thing below that stays put: not the user, a creature or anything on wheels.
    private static bool TryFindGround(Vector3 from, out Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, GroundSearch, Transport.GroundMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.attachedRigidbody == null && !(hit.collider is CharacterController) && hit.collider.GetComponentInParent<NavMeshAgent>() == null)
            {
                point = hit.point;
                return true;
            }
        }

        point = default;
        return false;
    }

    private void Show()
    {
        ItemDefinition item = Stack.Item;
        GameObject source = item == null ? null : item.Prefab != null ? item.Prefab : item.HeldModel;
        if (model == null && source != null)
        {
            model = CopyModel(source);
        }

        if (sack != null)
        {
            sack.SetActive(model == null);
        }
    }

    // Only the meshes: a held model can carry lights, and a world model what makes it a world object. A skinned model has
    // no plain meshes, and is shown as a sack.
    private Transform CopyModel(GameObject source)
    {
        var copy = new GameObject("Model").transform;
        copy.SetParent(visual, false);
        Matrix4x4 toModel = Matrix4x4.Scale(source.transform.localScale) * source.transform.worldToLocalMatrix;
        foreach (MeshFilter part in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (part.sharedMesh == null || !part.TryGetComponent(out MeshRenderer look))
            {
                continue;
            }

            var piece = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
            Matrix4x4 place = toModel * part.transform.localToWorldMatrix;
            piece.transform.SetParent(copy, false);
            piece.transform.SetLocalPositionAndRotation(place.GetPosition(), place.rotation);
            piece.transform.localScale = place.lossyScale;
            piece.GetComponent<MeshFilter>().sharedMesh = part.sharedMesh;
            piece.GetComponent<MeshRenderer>().sharedMaterials = look.sharedMaterials;
        }

        if (copy.childCount == 0)
        {
            DestroyImmediate(copy.gameObject);
            return null;
        }

        LayDown(copy);
        return copy;
    }

    // A held model stands however the hand holds it: turned to lie on its flattest side, resting on the spot.
    private void LayDown(Transform copy)
    {
        Quaternion facing = transform.rotation;
        transform.rotation = Quaternion.identity;
        Vector3 size = BoundsOf(copy).size;
        if (size.x < size.y && size.x <= size.z)
        {
            copy.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else if (size.z < size.y)
        {
            copy.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        Bounds bounds = BoundsOf(copy);
        Vector3 spot = transform.position;
        copy.position += new Vector3(spot.x - bounds.center.x, spot.y - bounds.min.y, spot.z - bounds.center.z);
        transform.rotation = facing;
    }

    private static Bounds BoundsOf(Transform copy)
    {
        var renderers = copy.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    public string CaptureState() => JsonUtility.ToJson(new Saved { slots = SaveMapper.ToSlots(Held) });

    public void RestoreState(string state)
    {
        Saved saved = JsonUtility.FromJson<Saved>(state);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (saved.slots != null && content != null)
        {
            Held.Clear();
            SaveMapper.ReadSlots(saved.slots, Held, content, new List<string>());
        }

        // An item taken out of the game since this was saved.
        if (Stack.IsEmpty)
        {
            Remove();
            return;
        }

        Show();
    }
}
