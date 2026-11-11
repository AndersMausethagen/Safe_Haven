using UnityEngine;

/// <summary>
/// The whistle (X): the player whistles, and the nearest of their horses that hears it comes at a canter: one whose bond
/// is at least HorseSettings › Whistle Bond, within a reach that grows with the bond (HorseBondRules). A stabled horse, a
/// ridden one or one on a rope stays where it is. Lives for the whole game, made by itself.
/// </summary>
public sealed class HorseWhistle : MonoBehaviour
{
    private const float Cooldown = 2.5f;
    private float ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Make()
    {
        if (FindAnyObjectByType<HorseWhistle>() != null)
        {
            return;
        }

        var holder = new GameObject("Horse Whistle");
        DontDestroyOnLoad(holder);
        holder.AddComponent<HorseWhistle>();
    }

    private void Update()
    {
        if (GameInput.Whistle == null || !GameInput.Whistle.WasPressedThisFrame() || Time.time < ready || GamePause.IsPaused
            || (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen) || DialogueBox.IsOpen)
        {
            return;
        }

        Player player = Player.Current;
        if (player == null || (player.TryGetComponent(out PlayerRiding riding) && (riding.IsRiding || riding.IsHoldingRope)))
        {
            return;
        }

        ready = Time.time + Cooldown;
        Whistle(player.transform.position);
    }

    /// <summary>Whistles at <paramref name="from"/>: the horse that comes, or null (with a word why).</summary>
    public static Horse Whistle(Vector3 from)
    {
        HorseSettings settings = HorseSettings.Current;
        if (settings.Whistle != null)
        {
            AudioManager.Play(settings.Whistle, from);
        }

        Horse nearest = null;
        Horse coming = null;
        float nearestDistance = float.MaxValue;
        float comingDistance = float.MaxValue;
        foreach (Horse horse in Horse.All)
        {
            if (horse == null || !horse.IsTamed || !horse.IsAlive || horse.IsStabled || horse.Rider != null)
            {
                continue;
            }

            float distance = Vector3.Distance(horse.transform.position, from);
            if (distance < nearestDistance)
            {
                nearest = horse;
                nearestDistance = distance;
            }

            if (distance < comingDistance && HorseBondRules.HearsWhistle(horse.CurrentBond(), distance, settings))
            {
                coming = horse;
                comingDistance = distance;
            }
        }

        if (coming != null && coming.Motion.Come(from))
        {
            GameMessages.Post($"{coming.HorseName} lifts its head and comes.", MessageKind.Info);
            return coming;
        }

        if (nearest == null)
        {
            GameMessages.Post("You whistle. No horse of yours is out to hear it.", MessageKind.Info);
        }
        else if (nearest.CurrentBond() < settings.WhistleBond)
        {
            GameMessages.Post($"{nearest.HorseName} doesn't know your whistle yet. Feed it, groom it and ride it.", MessageKind.Info);
        }
        else
        {
            GameMessages.Post($"{nearest.HorseName} is too far off to hear you ({nearestDistance:0} m).", MessageKind.Info);
        }

        return null;
    }
}
