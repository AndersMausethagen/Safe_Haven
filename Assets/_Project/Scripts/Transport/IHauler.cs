using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Whatever pulls a cart by its handles (Haulable): the player on foot (PlayerTransport), or a horse it is hitched to
/// (HorseHitch). The cart springs toward a point behind it at the hauler's speed, as hard as the hauler can pull.
/// </summary>
public interface IHauler
{
    Transform transform { get; }

    GameObject gameObject { get; }

    /// <summary>Its speed over the ground, for the spring at the handle.</summary>
    Vector3 Velocity { get; }

    /// <summary>How many times a person's pull it has (TransportDefinition.PullForce is a person's).</summary>
    float PullScale { get; }

    /// <summary>Metres behind its centre the handles are held, beyond the cart's own hold distance.</summary>
    float ExtraReach { get; }

    /// <summary>Its own colliders, which the cart must not push against.</summary>
    IEnumerable<Collider> HaulerColliders { get; }

    void Began(Transport transport);

    void Ended();
}
