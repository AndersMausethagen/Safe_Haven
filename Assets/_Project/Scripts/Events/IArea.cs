using UnityEngine;

/// <summary>
/// A place on the map that a GameEventTrigger can watch the player step into, without a collider: the portal's
/// ward, which grows with its level. Put it on a component.
/// </summary>
public interface IArea
{
    bool Contains(Vector3 position);
}
