using System;
using System.Collections.Generic;

/// <summary>
/// What someone who carves enchantments (a job with Carves Past: Liv, the Rune Scholar) adds to a conversation at home
/// (Docs/EnchantingResearch.md #6): how enchanting works and where enchantments are found, how far past the player's own Runecraft
/// she carves now, and handing her inscriptions, which raises her rank and teaches the player what she reads aloud. ResidentNpc
/// calls this for a carver; the words are hers, the rules are Inscriptions'.
/// </summary>
public static class EnchanterTalk
{
    // At most this many "give" answers at once, so the conversation stays short; the rest come after.
    private const int MostOffered = 4;

    /// <summary>The words of her lesson, also her first quest's journal: what an enchantment is, and where to find them.</summary>
    public const string Lesson =
        "Gear takes one mark at a time, cut at the altar with essence. Purified marks are safe. Cursed ones are stronger, and they always take a price: " +
        "it's written on them, so you know before you cut.\n\n" +
        "Nobody can teach a mark from nothing. It has to be found. The elites out there carry the mark that made them what they are: kill one and its " +
        "inscription may be on it. The great ones, the trolls and the thing under the roots, carry great marks, the kind that change how a fight goes. " +
        "And the carved stones in the deep woods: press a Blank Parchment to one and rub charcoal over it. Fiber and charcoal, nothing more.\n\n" +
        "Bring the inscription home and read it here. Out there you'd only lose it in the mud. And mind this: a mark counts once, however many pieces " +
        "carry it. Two of the same is one.";

    public static void AddTopics(MenuPage page, DialogueSpeaker speaker, ResidentRecord carver, Action afterAnswer)
    {
        if (carver == null || carver.Job == null || !carver.Job.Carves)
        {
            return;
        }

        page.Add(new MenuAction("Ask about enchanting", "How it works, and where to find more", true,
            () => DialogueBox.Current.Say(speaker, Lesson, afterAnswer)));

        int reach = Inscriptions.CarverReach(carver);
        int own = Inscriptions.RunecraftLevel();
        page.Add(new MenuAction("Ask what she can carve", $"{SettlementSettings.Current.RankFor(carver.WorkDone).name}: Runecraft {own + reach}", true,
            () => DialogueBox.Current.Say(speaker,
                $"With my hands on the chisel, marks up to Runecraft {own + reach}: {reach} past your own. Every inscription you bring me teaches me " +
                "something, even one you already know. And every mark we cut together.", afterAnswer)));

        ItemInventory bag = Player.Current != null ? Player.Current.GetComponent<ItemInventory>() : null;
        if (bag == null)
        {
            return;
        }

        int offered = 0;
        foreach ((ItemContainer container, int slot) in Carried(bag))
        {
            if (offered++ >= MostOffered)
            {
                break;
            }

            EnchantmentDefinition enchantment = container.GetSlot(slot).Enchantment;
            bool known = GameSession.Progress.Knows(enchantment);
            page.Add(new MenuAction($"Give her the {enchantment.DisplayName} inscription", known ? "You know it: she learns from it" : "She reads it with you: you learn it", true, () =>
            {
                // Found again by its enchantment: the bag may have changed since the page was built.
                if (!TryFind(bag, enchantment, out ItemContainer from, out int at) || !Inscriptions.Give(bag.gameObject, from, at, carver))
                {
                    DialogueBox.Current.Say(speaker, "You don't have it on you any more.", afterAnswer);
                    return;
                }

                DialogueBox.Current.Say(speaker, known
                    ? $"{enchantment.DisplayName}. I've seen this hand before, but not this way of closing the loop. Thank you."
                    : $"{enchantment.DisplayName}. Here, follow the line with me. There. You can carve it now.", afterAnswer);
            }));
        }
    }

    // Every inscription the player carries that teaches something: the bag first, then the pocket.
    private static IEnumerable<(ItemContainer container, int slot)> Carried(ItemInventory bag)
    {
        foreach (ItemContainer container in new[] { bag.Container, bag.SafePocket })
        {
            for (int i = 0; container != null && i < container.SlotCount; i++)
            {
                ItemStack stack = container.GetSlot(i);
                if (!stack.IsEmpty && stack.Item.IsInscription && stack.Enchantment != null)
                {
                    yield return (container, i);
                }
            }
        }
    }

    private static bool TryFind(ItemInventory bag, EnchantmentDefinition enchantment, out ItemContainer container, out int slot)
    {
        foreach ((ItemContainer where, int at) in Carried(bag))
        {
            if (where.GetSlot(at).Enchantment == enchantment)
            {
                container = where;
                slot = at;
                return true;
            }
        }

        container = null;
        slot = -1;
        return false;
    }
}
