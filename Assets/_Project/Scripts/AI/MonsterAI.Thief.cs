using UnityEngine;

/// <summary>
/// A thief (MonsterDefinition › Game › Thief): the tusse. When it notices the player it sneaks up at a run, to the player or to a
/// cart near them with something in it, snatches one stack and runs off with it. It runs when struck as well. It fights only
/// when cornered: the player right beside it and it getting nowhere for a moment, and then only for Cornered Seconds before
/// it tries to run again. Killed, it drops what it took; once it is Escape Distance from the player with it, it is gone, and so
/// is the stack. Its faction must be one that would fight the player (so it can strike back); it never picks a fight itself.
/// Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    private const float ThiefStep = 0.3f;
    private const float FleeStride = 9f;
    private const float CorneredRange = 2.6f;
    private const float StuckSeconds = 1.1f;

    private ItemStack loot;
    private bool thiefFleeing;
    private bool thiefHooked;
    private float thiefNextStep;
    private float fightBackUntil = float.NegativeInfinity;
    private float stuckFor;
    private Vector3 lastThiefPosition;
    private float lastThiefHealth = -1f;

    /// <summary>What it has taken and is carrying off (empty when nothing).</summary>
    public ItemStack Loot => loot;

    /// <summary>True while it runs, with or without something.</summary>
    public bool IsFleeing => thiefFleeing;

    /// <summary>True while it fights back, cornered.</summary>
    public bool IsCornered => Time.time < fightBackUntil;

    // Run by Update before the ordinary hunting. True while it moves itself (nothing else should).
    private bool UpdateThief()
    {
        if (!definition.Thief)
        {
            return false;
        }

        if (!thiefHooked)
        {
            thiefHooked = true;
            damageable.Died += DropLoot;
            lastThiefPosition = transform.position;
            lastThiefHealth = damageable.CurrentHealth;
        }

        // Cornered: the ordinary fighting has it, for a while.
        if (IsCornered)
        {
            return false;
        }

        // It never picks a fight: whatever it took on (being struck engages) is let go, and it runs instead.
        bool struck = damageable.CurrentHealth < lastThiefHealth - 0.01f;
        lastThiefHealth = damageable.CurrentHealth;
        if (target != null)
        {
            ClearTarget();
            if (CurrentState == State.Chasing || CurrentState == State.Attacking)
            {
                attacks.Cancel();
                pose?.CancelAttack();
                SetState(State.Idle);
            }
        }

        Player player = Player.Current;
        if (player == null)
        {
            return false;
        }

        if (struck)
        {
            thiefFleeing = true;
        }

        Vector3 playerAt = player.transform.position;
        float distance = HorizontalDistance(playerAt);
        if (!thiefFleeing)
        {
            bool noticed = distance < definition.CloseSenseRange || senses.CanNotice(player.transform);
            if (!noticed)
            {
                return false;
            }

            Sneak(player, playerAt);
            return true;
        }

        // Away with it.
        if (!loot.IsEmpty && distance > definition.EscapeDistance)
        {
            GameMessages.Post($"The {definition.DisplayName} got away with your {loot.DisplayName}.");
            loot = ItemStack.Empty;
            Destroy(gameObject);
            return true;
        }

        // Nothing taken and far off: it gives up and lives here now.
        if (loot.IsEmpty && distance > definition.DetectionRange * 1.5f)
        {
            thiefFleeing = false;
            home = transform.position;
            return false;
        }

        Flee(player, playerAt, distance);
        return true;
    }

    // At a run to the player, or to a cart beside them with something in it, and a grab when in reach.
    private void Sneak(Player player, Vector3 playerAt)
    {
        ItemContainer from = player.TryGetComponent(out ItemInventory inventory) ? inventory.Container : null;
        Vector3 goal = playerAt;
        foreach (Transport cart in Transport.All)
        {
            if (cart == null || cart.Cargo == null || cart.Cargo.CountItems() == 0)
            {
                continue;
            }

            Vector3 at = cart.transform.position;
            if ((at - playerAt).sqrMagnitude < 15f * 15f && HorizontalDistance(at) < HorizontalDistance(goal))
            {
                goal = at;
                from = cart.Cargo;
            }
        }

        if (HorizontalDistance(goal) <= definition.StealReach + (goal == playerAt ? 0f : 1f))
        {
            Steal(from, goal == playerAt ? null : "cart");
            return;
        }

        if (Time.time >= thiefNextStep)
        {
            thiefNextStep = Time.time + ThiefStep;
            movement.SetSpeed(definition.MoveSpeed * HeldScale);
            movement.MoveTo(goal);
        }
    }

    private void Steal(ItemContainer from, string place)
    {
        thiefFleeing = true;
        thiefNextStep = 0f;
        if (from == null)
        {
            return;
        }

        // Any stack it can lift: one at random.
        int chosen = -1, seen = 0;
        for (int i = 0; i < from.SlotCount; i++)
        {
            if (!from.GetSlot(i).IsEmpty && Random.Range(0, ++seen) == 0)
            {
                chosen = i;
            }
        }

        if (chosen < 0)
        {
            return;
        }

        loot = from.GetSlot(chosen);
        from.SetSlot(chosen, ItemStack.Empty);
        GameMessages.Post(place == null
            ? $"A {definition.DisplayName} snatched your {loot.DisplayName}!"
            : $"A {definition.DisplayName} snatched {loot.DisplayName} from the {place}!");
        AudioManager.Play(AudioManager.Sounds?.ItemDropped, transform.position);
    }

    // Away from the player, as game bolts; with the player right beside it and nowhere to go, it turns and fights.
    private void Flee(Player player, Vector3 playerAt, float distance)
    {
        float moved = Vector3.Distance(transform.position, lastThiefPosition);
        lastThiefPosition = transform.position;
        stuckFor = distance < CorneredRange && moved < 1.2f * Time.deltaTime ? stuckFor + Time.deltaTime : 0f;
        if (stuckFor >= StuckSeconds && player.Health != null && Engage(player.Health))
        {
            stuckFor = 0f;
            fightBackUntil = Time.time + definition.CorneredSeconds;
            return;
        }

        if (Time.time < thiefNextStep)
        {
            return;
        }

        thiefNextStep = Time.time + ThiefStep;
        Vector3 away = transform.position - playerAt;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
        movement.SetSpeed(definition.MoveSpeed * HeldScale);
        foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f })
        {
            Vector3 destination = transform.position + Quaternion.Euler(0f, turn, 0f) * away * FleeStride;
            if (movement.TryFindGround(destination, 3f, out Vector3 ground) && movement.MoveTo(ground))
            {
                break;
            }
        }
    }

    // Killed: what it carried falls where it lies.
    private void DropLoot(Damageable body, DamageInfo info)
    {
        if (loot.IsEmpty)
        {
            return;
        }

        DroppedItem.Drop(transform, loot);
        GameMessages.Post($"The {definition.DisplayName} dropped your {loot.DisplayName}.");
        loot = ItemStack.Empty;
    }
}
