using UnityEngine;

/// <summary>
/// A named place where the player appears when entering a scene for a reason: "HeartTree" (waking up, reviving after a
/// failed raid, GDD §8.3) or "Portal" (coming home from a raid). Whoever changes scene asks for one with
/// GameSession.SetArrivalPoint; PlayerPersistence moves the player here on arrival. A scene without the requested point
/// keeps the player where the scene put them.
/// <para>
/// Where it belongs: an empty GameObject in a scene, positioned and rotated where the player should stand.
/// </para>
/// </summary>
public sealed class PlayerArrivalPoint : MonoBehaviour
{
    public const string HeartTree = "HeartTree";
    public const string Portal = "Portal";

    [Tooltip("The name scene changes ask for, such as HeartTree or Portal.")]
    [SerializeField] private string id = HeartTree;

    public string Id => id;

    /// <summary>The point with the id in the loaded scenes, or null.</summary>
    public static PlayerArrivalPoint Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (PlayerArrivalPoint point in FindObjectsByType<PlayerArrivalPoint>(FindObjectsInactive.Exclude))
        {
            if (point.id == id)
            {
                return point;
            }
        }

        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.5f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward);
    }
}
