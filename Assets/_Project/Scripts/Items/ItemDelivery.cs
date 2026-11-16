using UnityEngine;

/// <summary>
/// Gives a stack to a player so that none of it is ever lost: into the bag, then (where allowed: at home) the stash, then onto
/// the ground at their feet. The one way rewards, refunds, finds and moved gear arrive (review 2026-11-16: each place did it its
/// own way, each with its own gap: a sale into a full stash, a refund counted again as a find, a reward with nowhere to go).
/// <para>Where it belongs: every "give the player this" outside picking up and crafting's own output. Pure rules on containers,
/// apart from the ground (DroppedItem).</para>
/// </summary>
public static class ItemDelivery
{
    /// <summary>How many pieces went where.</summary>
    public readonly struct Delivery
    {
        public readonly int Bag;
        public readonly int Stash;
        public readonly int Ground;

        public Delivery(int bag, int stash, int ground)
        {
            Bag = bag;
            Stash = stash;
            Ground = ground;
        }

        public int Total => Bag + Stash + Ground;

        /// <summary>Some of it fell at the player's feet: the bag (and the stash) were full.</summary>
        public bool Fell => Ground > 0;
    }

    /// <summary>
    /// Gives <paramref name="stack"/> to a player's bag, the rest to the stash (when <paramref name="toStash"/>), the rest at
    /// <paramref name="dropAt"/> (the bag's owner when null).
    /// </summary>
    /// <param name="newFind">Newly come by (a reward, a find): what reaches the bag raises ItemObtained, so collect quests count it
    /// and gear is put on. False for what goes back or is only moved (a refund, gear out of a retired slot), which is no find.</param>
    /// <param name="toStash">The stash may take what the bag can't: at home only, where it can be reached.</param>
    public static Delivery Give(ItemInventory bag, ItemStack stack, bool newFind, bool toStash, Transform dropAt = null)
    {
        if (stack.IsEmpty)
        {
            return default;
        }

        int inBag = bag == null ? 0 : newFind ? bag.GiveMade(stack) : bag.Container.AddStack(stack);
        ItemContainer stash = toStash && GameSession.Stash != null ? GameSession.Stash.Items : null;
        Delivery rest = Give(null, stack.WithCount(stack.Count - inBag), stash, dropAt != null ? dropAt : bag != null ? bag.transform : null);
        return new Delivery(inBag, rest.Stash, rest.Ground);
    }

    /// <summary>The same into bare containers, raising nothing (MaterialPool's refunds). Either container may be null.</summary>
    public static Delivery Give(ItemContainer bag, ItemStack stack, ItemContainer stash, Transform dropAt)
    {
        if (stack.IsEmpty)
        {
            return default;
        }

        int inBag = bag != null ? bag.AddStack(stack) : 0;
        int left = stack.Count - inBag;
        int inStash = left > 0 && stash != null ? stash.AddStack(stack.WithCount(left)) : 0;
        left -= inStash;
        int onGround = 0;
        // Water as a measure is poured into containers, never set down: a "Water" sack could only be picked up into a waterskin.
        if (left > 0 && dropAt != null && !stack.Item.IsWaterMeasure)
        {
            DroppedItem.Drop(dropAt, stack.WithCount(left));
            onGround = left;
        }

        return new Delivery(inBag, inStash, onGround);
    }
}
