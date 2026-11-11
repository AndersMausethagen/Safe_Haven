using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cart hitched to a horse (from the horse's menu, with a free cart beside it): the horse takes the cart's shafts as the
/// player would (Haulable's spring at the handles), with a horse's pull, a little further behind it. A loaded cart slows
/// it by its strain. Unhitched from the menu again, or when the horse dies.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Horse))]
public sealed class HorseHitch : MonoBehaviour, IHauler
{
    private const float HorsePull = 6f;
    private const float BehindTheHorse = 1.5f;

    private Horse horse;
    private Collider[] own;

    /// <summary>The cart it pulls, or null.</summary>
    public Haulable Cart { get; private set; }
    public Horse Horse => horse;

    private void Awake()
    {
        horse = GetComponent<Horse>();
        own = GetComponentsInChildren<Collider>();
    }

    /// <summary>The cart the horse pulls, or null.</summary>
    public static Haulable Pulling(Horse horse) =>
        horse != null && horse.TryGetComponent(out HorseHitch hitch) && hitch.Cart != null ? hitch.Cart : null;

    public static void Hitch(Horse horse, Haulable cart)
    {
        if (horse == null || cart == null || cart.InUse || !horse.IsTamed)
        {
            return;
        }

        HorseHitch hitch = horse.TryGetComponent(out HorseHitch found) ? found : horse.gameObject.AddComponent<HorseHitch>();
        Unhitch(horse);
        hitch.Cart = cart;
        cart.Take(hitch);
        GameMessages.Post($"{horse.HorseName} pulls the cart now. Ride, and it follows.", MessageKind.Info);
    }

    public static void Unhitch(Horse horse)
    {
        if (horse == null || !horse.TryGetComponent(out HorseHitch hitch) || hitch.Cart == null)
        {
            return;
        }

        Haulable cart = hitch.Cart;
        hitch.Cart = null;
        if (ReferenceEquals(cart.Hauler, hitch))
        {
            cart.LetGo();
        }
    }

    /// <summary>What a hitched cart's strain leaves of the horse's speed.</summary>
    public static float SpeedScaleFor(Horse horse)
    {
        Haulable cart = Pulling(horse);
        return cart != null ? Mathf.Lerp(1f, 0.75f, cart.Strain) : 1f;
    }

    private void OnDisable() => Unhitch(horse);

    // IHauler: the horse at the shafts.
    public Vector3 Velocity => horse.Motion != null ? horse.Motion.Velocity : Vector3.zero;
    public float PullScale => HorsePull;
    public float ExtraReach => BehindTheHorse;
    public IEnumerable<Collider> HaulerColliders => own;

    public void Began(Transport transport)
    {
    }

    public void Ended()
    {
        // The cart let go by itself (stuck): forget it.
        Cart = null;
    }
}
