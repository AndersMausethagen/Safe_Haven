using UnityEngine;

/// <summary>
/// A forward camp, set down from a kit made at the Workshop: a small hearth that holds a circle of the woods for a while, so a player
/// who has gone far from the way home can catch their breath. Inside it they heal, and the light keeps night creatures out
/// (VisibilitySource.IsLit). It doesn't stop what is already hunting them, and it burns out. Its lifetime is its own: it is not saved.
/// </summary>
[DisallowMultipleComponent]
public sealed class FieldCamp : MonoBehaviour
{
    [SerializeField, Min(1f)] private float radius = 5f;
    [SerializeField, Min(1f)] private float seconds = 120f;
    [Tooltip("Share of maximum health restored each second to whoever is inside.")]
    [SerializeField, Range(0f, 1f)] private float healPerSecond = 0.02f;

    private float endsAt;

    private void Start()
    {
        endsAt = Time.time + seconds;
    }

    private void Update()
    {
        if (Time.time >= endsAt)
        {
            Destroy(gameObject);
            return;
        }

        Player player = Player.Current;
        if (player == null || !player.Health.IsAlive || (player.transform.position - transform.position).sqrMagnitude > radius * radius)
        {
            return;
        }

        player.Health.Heal(player.Health.MaxHealth * healPerSecond * Time.deltaTime);
    }
}
