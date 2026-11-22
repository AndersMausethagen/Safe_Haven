using System.Collections;
using UnityEngine;

/// <summary>
/// In Safe Haven the player can't die for good (GDD §6.3, §6.9): after falling, they lie still for a moment and then wake
/// at the portal with their health restored. What the fall costs is the death rules' (HomeFalls: a cairn, fading lessons, meals
/// ended; nothing under Gentle, in the prologue or the first breach), shown on a card on waking. In a ward breach the wait is the
/// breach's (WardBreachDirector.WakeSeconds: longer with each fall).
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
        if (animation != null)
        {
            animation.SetDead(true);
        }
        PlayerControl.Hold(player, this);

        GameMessages.Post(fallenText, MessageKind.Warning);
        // Where they fell, before anything moves them: the cairn is left there.
        HomeFalls.Cost cost = HomeFalls.Take(player, player.transform.position);
        // A frame on, so the breach has counted this fall whichever heard of it first.
        yield return null;
        WardBreachDirector breach = WardBreachDirector.Current;
        float wait = breach != null && breach.IsActive && !cost.Spared ? breach.WakeSeconds : delay;
        yield return new WaitForSeconds(Mathf.Max(0f, wait - Time.deltaTime));

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
        PlayerControl.Release(player, this);

        MenuPage card = HomeFalls.Card(cost);
        if (card != null && ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => card, player);
        }
    }
}
