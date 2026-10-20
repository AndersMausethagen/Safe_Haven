using UnityEngine;

/// <summary>
/// Carries the player's belongings and knowledge across scene loads, and puts the player where a loaded save says. When
/// the player appears in a scene it takes the state the previous scene (or the save system) handed over through
/// GameSession and applies it before any other system reads the inventories (hence the early execution order), so the
/// bag, resources and recipes are the ones the player left with and nothing announces them as new. A scene started
/// directly, with no handover, keeps the inventories' own starting contents.
/// <para>
/// Other systems reach it through Player.Current.Persistence. To carry a new part of the player between scenes (equipment,
/// health), add it to PlayerSnapshot (capture and apply) rather than here.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
[RequireComponent(typeof(ItemInventory), typeof(ResourceInventory))]
public sealed class PlayerPersistence : MonoBehaviour
{
    private void Awake()
    {
        PlayerSnapshot carried = GameSession.TakePlayerState();
        if (carried != null)
        {
            foreach (ItemAmount lost in carried.ApplyTo(gameObject))
            {
                Debug.LogWarning($"No room in the bag for {lost.Amount} {lost.Item.DisplayName} carried over from the last scene.", this);
            }
        }

        if (GameSession.TakeSpawnPose(out PlayerPose pose))
        {
            GameSession.TakeArrivalPoint();
            MoveTo(pose);
        }
        else
        {
            PlayerArrivalPoint point = PlayerArrivalPoint.Find(GameSession.TakeArrivalPoint());
            if (point != null)
            {
                MoveTo(new PlayerPose(point.transform.position, point.transform.eulerAngles.y));
            }
        }
    }

    /// <summary>What the player owns and knows right now.</summary>
    public PlayerSnapshot Capture() => PlayerSnapshot.Capture(gameObject);

    public PlayerPose Pose => PlayerPose.Of(transform);

    // A CharacterController keeps its own idea of where it is, so it is switched off while the player is moved.
    private void MoveTo(PlayerPose pose)
    {
        var body = GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (wasEnabled)
        {
            body.enabled = false;
        }

        transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0f, pose.Yaw, 0f));
        if (wasEnabled)
        {
            body.enabled = true;
        }
    }
}
