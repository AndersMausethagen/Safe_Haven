using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abilities found rather than taught (the user, 2026-11-27: "we could also unlock abilities other ways. through exploring or finding rare
/// items"): a lore item (a saga-stave, a carved bone: ItemDefinition › Teaches Skill) teaches its ability when read at home, as an
/// inscription teaches its enchantment. The raids give one at their rare moments (a great creature, an elite, a hoard, a Lost One laid to
/// rest, a strange place), one the player neither knows nor carries while there is any; the Haven's hidden places hold theirs. Lost with
/// the bag like anything carried: the find has to get home. Plain rules over the settings and GameSession.
/// </summary>
public static class SkillLore
{
    /// <summary>True when the player knows its ability, or carries it home unread (the bag, the pocket, the stash).</summary>
    public static bool KnownOrCarried(ItemDefinition lore, GameObject player)
    {
        if (lore == null || lore.TeachesSkill == null)
        {
            return true;
        }

        PlayerSkills skills = player != null ? player.GetComponentInParent<PlayerSkills>() : null;
        if (skills != null && skills.IsKnown(lore.TeachesSkill))
        {
            return true;
        }

        ItemInventory bag = player != null ? player.GetComponentInParent<ItemInventory>() : null;
        return bag != null && (bag.Container.Count(lore) > 0 || bag.SafePocket != null && bag.SafePocket.Count(lore) > 0)
               || GameSession.Stash.Items.Count(lore) > 0;
    }

    /// <summary>True when the player has found any lore at all (known or carried): the first great creature's is then no longer sure.</summary>
    public static bool AnyFound(GameObject player)
    {
        foreach (ItemDefinition lore in SkillLoreSettings.Current.Lore)
        {
            if (lore != null && KnownOrCarried(lore, player))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One lore item the player neither knows nor carries, or null when every one is found.</summary>
    public static ItemDefinition Pick(GameObject player, System.Random random)
    {
        var unknown = new List<ItemDefinition>();
        foreach (ItemDefinition lore in SkillLoreSettings.Current.Lore)
        {
            if (lore != null && !KnownOrCarried(lore, player))
            {
                unknown.Add(lore);
            }
        }

        return unknown.Count > 0 ? unknown[random.Next(unknown.Count)] : null;
    }

    /// <summary>Rolls <paramref name="chance"/> and, on a hit, puts one unknown lore item in the player's bag. The item given, or null.</summary>
    public static ItemDefinition Roll(GameObject player, float chance, string from)
    {
        if (player == null || chance <= 0f || Random.value >= chance)
        {
            return null;
        }

        ItemDefinition lore = Pick(player, new System.Random(Random.Range(0, int.MaxValue)));
        return lore != null && Grant(player, lore, from) ? lore : null;
    }

    /// <summary>
    /// Puts a lore item in the player's bag (at their feet when it is full) and notes it for the raid's report. The bag announces it as it
    /// arrives (Announce), as it does lore taken from a body or a Haven place. <paramref name="from"/>: "the Old Moss Troll", "a supply cache".
    /// </summary>
    public static bool Grant(GameObject player, ItemDefinition lore, string from)
    {
        ItemInventory bag = player != null ? player.GetComponentInParent<ItemInventory>() : null;
        if (lore == null || lore.TeachesSkill == null || bag == null)
        {
            return false;
        }

        if (ItemDelivery.Give(bag, new ItemStack(lore, 1), newFind: true, toStash: false).Fell)
        {
            GameMessages.Post("Bag full: it falls at your feet.", MessageKind.Warning);
            // On the ground it isn't in the bag yet: said now, and again (by the bag) when it is picked up.
            Announce(player, lore);
        }

        if (RaidDirector.Current != null)
        {
            RaidDirector.Current.Session.RecordEvent($"Found {lore.DisplayName} ({from})");
        }

        NoteFound(lore, from);
        return true;
    }

    /// <summary>The playtest log's LoreFound row, once per find and naming where it came from: written where lore is given or laid on a
    /// body, not as it reaches the bag (a full bag's drop picked up, or lore bought back, logged it again: review 2026-11-27).</summary>
    public static void NoteFound(ItemDefinition lore, string from)
    {
        if (lore != null && lore.TeachesSkill != null)
        {
            PlaytestLogger.Note(PlaytestLog.LoreFound, lore.TeachesSkill.Id, 1, from);
        }
    }

    /// <summary>Lore has come into the bag (any way: a grant, a body's loot, a Haven place): a legendary find's sound and words
    /// (PlayerSkills listens to the bag).</summary>
    public static void Announce(GameObject player, ItemDefinition lore)
    {
        if (lore == null || lore.TeachesSkill == null)
        {
            return;
        }

        GameMessages.Post($"{lore.DisplayName}! It holds {lore.TeachesSkill.DisplayName}: read it at home to learn it.", MessageKind.Gain);
        if (player != null)
        {
            AudioManager.Play(Rarity.StyleOf(ItemRarity.Legendary).PickupSound, player.transform.position);
        }
    }

    /// <summary>
    /// Reads the lore in a slot: at home it teaches its ability for good (onto a free slot of its kind). Out on a raid it waits: the find
    /// has to get home. One whose ability is known already isn't used up: it can be sold. True when it was used up.
    /// </summary>
    public static bool Read(GameObject reader, ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        SkillDefinition skill = stack.IsEmpty ? null : stack.Item.TeachesSkill;
        PlayerSkills skills = reader != null ? reader.GetComponentInParent<PlayerSkills>() : null;
        if (skill == null || skills == null)
        {
            return false;
        }

        if (RaidDirector.Current != null)
        {
            GameMessages.Post($"There is no time to take in {skill.DisplayName} out here. Get it home.", MessageKind.Warning);
            return false;
        }

        if (skills.IsKnown(skill))
        {
            GameMessages.Post($"You know {skill.DisplayName} already. The Trading Post would pay well for {stack.Item.DisplayName}.", MessageKind.Info);
            return false;
        }

        from.RemoveAt(slot, 1);
        skills.Learn(skill, true, $"You take in what {stack.Item.DisplayName} holds: {skill.DisplayName} is yours ({SkillKinds.Name(skill.Kind)}).");
        PlaytestLogger.Note(PlaytestLog.LoreRead, skill.Id, 1, stack.Item.Id);
        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, stack.Item, 1, reader));
        return true;
    }
}
