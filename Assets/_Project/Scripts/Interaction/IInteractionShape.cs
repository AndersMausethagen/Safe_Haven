using UnityEngine;

/// <summary>
/// The outline of something to interact with that has no collider to measure reach to, such as a pond's shore: a collider there
/// would stop the player, or find its way into the navmesh. Put it on the Interactable's GameObject, before the Interactable.
/// </summary>
public interface IInteractionShape
{
    /// <summary>The point of the shape nearest the given one, or that point itself when it is inside.</summary>
    Vector3 ClosestPoint(Vector3 point);
}
