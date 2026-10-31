using UnityEngine;

/// <summary>
/// What happens to a load left alone in a raid (GDD §8.5). Creatures nearby catch its scent and come to look, the way an
/// angry forest sends them (MonsterAI.Investigate): a smell isn't a noise, so cover doesn't stop it. Anything that reaches
/// it tears cargo out a bite at a time. Letting go to fight is still right, but a cart left for long comes back lighter.
/// In a raid only, only with something in it, and not while someone stands by it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Transport))]
public sealed class CartScavenging : MonoBehaviour
{
    private Transport transport;
    private float nextScent;
    private float nextBite;

    /// <summary>When something last took from it. Negative when nothing has.</summary>
    public float LastBiteAt { get; private set; } = -100f;

    /// <summary>How many items have been lost this raid.</summary>
    public int Taken { get; private set; }

    public bool IsBeingEaten => Time.time - LastBiteAt < TransportSettings.Current.ScavengeSeconds * 1.5f;

    /// <summary>Takes up to <paramref name="amount"/> items from one stack, chosen by <paramref name="pick"/> (0..1). Returns how many.</summary>
    public static int Bite(ItemContainer cargo, int amount, float pick)
    {
        int occupied = 0;
        for (int i = 0; i < cargo.SlotCount; i++)
        {
            occupied += cargo.GetSlot(i).IsEmpty ? 0 : 1;
        }

        int skip = Mathf.Min(occupied - 1, Mathf.FloorToInt(pick * occupied));
        for (int i = 0; i < cargo.SlotCount; i++)
        {
            if (cargo.GetSlot(i).IsEmpty)
            {
                continue;
            }

            if (skip-- == 0)
            {
                return cargo.RemoveAt(i, amount);
            }
        }

        return 0;
    }

    private void Awake() => transport = GetComponent<Transport>();

    private void Update()
    {
        TransportSettings settings = TransportSettings.Current;
        if (RaidDirector.Current == null || transport.Driver.InUse || transport.Cargo.CountItems() == 0 || IsAttended(settings))
        {
            return;
        }

        if (Time.time >= nextScent)
        {
            nextScent = Time.time + settings.ScentSeconds;
            DrawCreatures(settings);
        }

        if (Time.time >= nextBite && CreatureAtIt(settings))
        {
            nextBite = Time.time + settings.ScavengeSeconds;
            TakeABite(settings);
        }
    }

    private bool IsAttended(TransportSettings settings)
    {
        return Player.Current != null && (Player.Current.transform.position - transform.position).sqrMagnitude <= settings.AttendedReach * settings.AttendedReach;
    }

    private void DrawCreatures(TransportSettings settings)
    {
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (IsHostile(monster) && (monster.transform.position - transform.position).sqrMagnitude <= settings.ScentRadius * settings.ScentRadius)
            {
                monster.Investigate(transform.position);
            }
        }
    }

    private static bool IsHostile(MonsterAI monster)
    {
        return monster.CurrentState != MonsterAI.State.Dead && monster.Definition != null && monster.Definition.Faction.WillAttack(Faction.Player);
    }

    private bool CreatureAtIt(TransportSettings settings)
    {
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (IsHostile(monster) && (monster.transform.position - transform.position).sqrMagnitude <= settings.ScavengeReach * settings.ScavengeReach)
            {
                return true;
            }
        }

        return false;
    }

    private void TakeABite(TransportSettings settings)
    {
        bool first = !IsBeingEaten;
        Taken += Bite(transport.Cargo, settings.ScavengeItems, Random.value);
        LastBiteAt = Time.time;
        AudioManager.Play(settings.Bump, transform.position);
        if (first)
        {
            GameMessages.Post($"Something is tearing at the {transport.Definition.DisplayName}.", MessageKind.Warning);
        }
    }
}
