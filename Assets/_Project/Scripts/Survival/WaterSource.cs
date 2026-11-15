using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stream, well or spring (GDD §21.2 "fill water"). Using it (its Interactable) lets the player drink, restoring
/// thirst, and fills water containers: it tops the bag up to a number of the fill item (Waterskins) so the player can
/// carry water into a raid or cook with it. Water sources never run dry.
/// <para>
/// Where it belongs: on a stream object in Safe Haven, next to an Interactable that doesn't deactivate on completion.
/// Tune the amounts here on the object; the fill item is an ordinary consumable ItemDefinition.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class WaterSource : MonoBehaviour, IInteractionName
{
    public string InteractionName => string.IsNullOrWhiteSpace(sourceName) ? "Stream" : sourceName;

    [Tooltip("What the prompt calls it. Empty: Stream.")]
    [SerializeField] private string sourceName;
    [Tooltip("Thirst restored by drinking here.")]
    [SerializeField, Min(0f)] private float drinkAmount = 100f;
    [Tooltip("The water container handed out, such as a Waterskin. Empty means the source is only for drinking.")]
    [SerializeField] private ItemDefinition fillItem;
    [Tooltip("The bag is topped up to this many of the fill item.")]
    [SerializeField, Min(0)] private int fillUpTo = 3;

    private Interactable interactable;
    private int iceBrokenOnDay = -1;

    /// <summary>In a snowing season a well or trough freezes over each night: the first use of the day breaks the ice.</summary>
    public static bool FreezesNow
    {
        get
        {
            WeatherSystem weather = WeatherSystem.Active;
            return weather != null && weather.Snowing && weather.Snow >= 0.3f;
        }
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    /// <summary>Sets it up from code (water made while playing, such as a generated map's ponds).</summary>
    public void Configure(string name, float drink, ItemDefinition item, int upTo)
    {
        sourceName = name;
        drinkAmount = Mathf.Max(0f, drink);
        fillItem = item;
        fillUpTo = Mathf.Max(0, upTo);
    }

    private static readonly List<WaterSource> active = new List<WaterSource>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    /// <summary>
    /// The nearest place to drink from a point: along a river's or lake's edge (its WaterShore) or at a well. False when
    /// there is none.
    /// </summary>
    public static bool Nearest(Vector3 from, out Vector3 at)
    {
        at = from;
        float best = float.MaxValue;
        foreach (WaterSource source in active)
        {
            if (source == null)
            {
                continue;
            }

            Vector3 point = source.TryGetComponent(out WaterShore shore) ? shore.ClosestPoint(from) : source.transform.position;
            float distance = (point - from).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                at = point;
            }
        }

        return best < float.MaxValue;
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        interactable.Completed.RemoveListener(OnUsed);
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user == null)
        {
            return;
        }

        int today = GameSession.Settlement.DaysPassed;
        if (FreezesNow && iceBrokenOnDay != today)
        {
            iceBrokenOnDay = today;
            AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.IceBreak : null, transform.position);
            GameMessages.Post($"The {InteractionName.ToLowerInvariant()} has frozen over in the night. You break the ice.");
            return;
        }

        PlayerNeeds needs = user.GetComponentInParent<PlayerNeeds>();
        bool drank = false;
        if (needs != null && drinkAmount > 0f && !needs.Thirst.IsFull)
        {
            drank = needs.TryConsume("Fresh water", 0f, drinkAmount);
        }

        int filled = 0;
        ItemInventory bag = user.GetComponentInParent<ItemInventory>();
        if (bag != null)
        {
            filled = Refill(bag.Container);
        }

        if (bag != null && fillItem != null)
        {
            // Hands out containers up to Fill Up To (a water container comes full).
            int missing = fillUpTo - bag.Container.Count(fillItem);
            if (missing > 0)
            {
                filled += bag.Give(fillItem, missing);
            }
        }

        if (!drank && filled == 0)
        {
            GameMessages.Post("You've had your fill, and your water containers are full.");
            return;
        }

        // Any water will do, so quests about drinking ask for this rather than a place (the Haven's stream is gone).
        GameEventDefinition told = SurvivalSettings.Current.DrankAtWater;
        if (told != null)
        {
            GameEvents.Raise(told, user);
        }
    }

    /// <summary>Fills every water container (ItemDefinition.HoldsWater) in the bag that isn't full. Returns how many.</summary>
    public static int Refill(ItemContainer container)
    {
        int filled = 0;
        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.HoldsWater && stack.Durability < 0.999f)
            {
                container.SetDurability(i, 1f);
                filled++;
            }
        }

        return filled;
    }
}
