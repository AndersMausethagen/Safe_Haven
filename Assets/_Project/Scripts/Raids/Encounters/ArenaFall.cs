using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Where a fall in a boss arena ends (the river under Brukam's Bridge): the player caught in this trigger is carried to Landing and
/// loses a share of their health, never the last of it (the research: "Falling is not death. The river carries you to the near bank
/// with heavy damage, and you climb back"; a deadly drop is hard to judge from above). Only this game's own player: a friend's game
/// catches its own.
/// <para>
/// Where it belongs: a trigger box under the arena's drop, well above the ground or water below, with Landing on the near bank.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[OnAFriendsCopy(CopyRule.Same, "each game catches its own player")]
public sealed class ArenaFall : MonoBehaviour
{
    [Tooltip("Where the player is set down, facing its forward.")]
    [SerializeField] private Transform landing;
    [Tooltip("Share of the player's most health a fall takes (never the last of it).")]
    [SerializeField, Range(0f, 0.9f)] private float damageShare = 0.3f;
    [Tooltip("Told when it happens.")]
    [SerializeField] private string line = "The river takes you and throws you up on the near bank.";

    private float lastCaught = -10f;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        // Once per fall: the body can touch the box more than once on the way in.
        if (player == null || player != Player.Current || landing == null || Time.time < lastCaught + 1f)
        {
            return;
        }

        lastCaught = Time.time;
        Damageable health = player.Health;
        if (health != null && health.IsAlive)
        {
            float take = Mathf.Min(health.MaxHealth * damageShare, health.CurrentHealth - 1f);
            if (take > 0f)
            {
                health.PayExactly(new DamageInfo(null, null, take, DamageType.Physical, false, player.transform.position, Vector3.down));
            }
        }

        SetDown(player.transform, landing);
        CameraShake.Add(0.5f);
        if (!string.IsNullOrWhiteSpace(line))
        {
            GameMessages.Post(line, MessageKind.Warning);
        }
    }

    /// <summary>Moves the player to a place at once (no walk), facing its forward, with the follow camera kept on them.</summary>
    public static void SetDown(Transform player, Transform landing)
    {
        Vector3 from = player.position;
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (wasEnabled)
        {
            body.enabled = false;
        }

        Vector3 forward = Vector3.ProjectOnPlane(landing.forward, Vector3.up);
        player.SetPositionAndRotation(landing.position, forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : player.rotation);
        if (wasEnabled)
        {
            body.enabled = true;
        }

        // The follow camera would otherwise sweep across the arena to catch up.
        Vector3 moved = player.position - from;
        foreach (CinemachineCamera follow in FindObjectsByType<CinemachineCamera>())
        {
            if (follow.Follow != null && follow.Follow.IsChildOf(player))
            {
                follow.OnTargetObjectWarped(follow.Follow, moved);
            }
        }
    }
}
