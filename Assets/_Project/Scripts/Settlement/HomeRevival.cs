using System.Collections;
using UnityEngine;

/// <summary>
/// In Safe Haven the player can't die for good (GDD §6.3, §6.9): after falling, they lie still for a moment and then wake
/// at the portal with their health restored. Nothing is lost; a breach in progress simply ends (WardBreachDirector).
/// Raids handle death themselves (RaidDirector), so this belongs only in home scenes.
/// <para>
/// Where it belongs: on a scene object in Safe Haven (the portal is a good place), with a PlayerArrivalPoint
/// "HomePortal" nearby.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class HomeRevival : MonoBehaviour
{
    [Tooltip("Seconds the player lies still before waking.")]
    [SerializeField, Min(0f)] private float delay = 3f;
    [Tooltip("Share of health the player wakes with.")]
    [SerializeField, Range(0.1f, 1f)] private float healthOnWaking = 1f;
    [SerializeField, TextArea] private string fallenText = "You fall... and the portal's light pulls you back home.";

    private Damageable health;

    private void Start()
    {
        if (Player.Current != null)
        {
            health = Player.Current.Health;
            health.Died += OnDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        StartCoroutine(Revive(victim.gameObject));
    }

    private IEnumerator Revive(GameObject player)
    {
        var animation = player.GetComponent<PlayerAnimationController>();
        var input = player.GetComponent<PlayerInputHandler>();
        if (animation != null)
        {
            animation.SetDead(true);
        }
        if (input != null)
        {
            input.enabled = false;
        }

        GameMessages.Post(fallenText, MessageKind.Warning);
        yield return new WaitForSeconds(delay);

        PlayerArrivalPoint point = PlayerArrivalPoint.Find(PlayerArrivalPoint.HomePortal);
        if (point != null)
        {
            var body = player.GetComponent<CharacterController>();
            bool wasEnabled = body != null && body.enabled;
            if (wasEnabled)
            {
                body.enabled = false;
            }

            player.transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
            if (wasEnabled)
            {
                body.enabled = true;
            }
        }

        health.Revive(healthOnWaking);
        if (animation != null)
        {
            animation.SetDead(false);
        }
        if (input != null)
        {
            input.enabled = true;
        }
    }
}
