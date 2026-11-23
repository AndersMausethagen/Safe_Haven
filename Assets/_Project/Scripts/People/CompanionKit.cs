using UnityEngine;

/// <summary>
/// A person's own kit (Docs/PeopleResearch_2026-11-23.md §3.6; the user's "equip companions from the stash"): a weapon and a body piece
/// the player gives them in conversation, from the bag. The weapon must suit how they fight (a bow for an archer, a blade or maul for the
/// rest) and is what they hold; its blow, against their kind's own weapon, scales their damage, and both pieces' stats count as the
/// player's gear does (GearPower), so the player's smithing feeds their people. Kit wears with every raid along and is mended like the
/// player's (taken back, repaired, given again). After enough raids along a piece is theirs, by name ("Gunnar's Yew Bow"); lost to the
/// forest, it waits with them. Plain rules (PeopleTests).
/// </summary>
public static class CompanionKit
{
    /// <summary>True when <paramref name="item"/> suits them as a weapon (<paramref name="weapon"/>) or a body piece.</summary>
    public static bool Fits(ResidentRecord record, ItemDefinition item, bool weapon)
    {
        if (record?.Resident == null || item == null)
        {
            return false;
        }

        CompanionDefinition how = record.Resident.RaidCompanion;
        if (how == null)
        {
            return false;
        }

        if (!weapon)
        {
            return item.IsArmor && item.ArmorSlot == ArmorSlot.Chest;
        }

        if (!item.IsWeapon)
        {
            return false;
        }

        WeaponKind kind = item.Weapon.Kind;
        return how.IsRanged ? kind == WeaponKind.Bow : kind == WeaponKind.Melee;
    }

    /// <summary>
    /// Gives them the piece in <paramref name="from"/>'s <paramref name="slot"/>: what they had of that kind comes back to the bag (or the
    /// stash, or the feet). Raids with a piece are counted by its item (RaidsWith). False when it doesn't suit them.
    /// </summary>
    public static bool Give(ResidentRecord record, ItemContainer from, int slot, ItemContainer stash, Transform dropAt)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        if (stack.IsEmpty || record == null)
        {
            return false;
        }

        bool weapon = Fits(record, stack.Item, true);
        if (!weapon && !Fits(record, stack.Item, false))
        {
            return false;
        }

        from.RemoveAt(slot, 1);
        ItemStack given = stack.WithCount(1);
        ItemStack before = weapon ? record.KitWeapon : record.KitBody;
        if (weapon)
        {
            record.KitWeapon = given;
        }
        else
        {
            record.KitBody = given;
        }

        if (!before.IsEmpty)
        {
            ItemDelivery.Give(from, before, stash, dropAt);
        }

        // A first gift of this kind of piece grows the bond (once an item: handing the same blade back and forth doesn't).
        string key = "kitgiven:" + given.Item.Id;
        if (record.DeedOf(key) == 0)
        {
            record.AddDeed(key, 1);
            PeopleBond.Grow(record, PeopleSettings.Current.BondKit, "kit given");
        }

        GameEvents.Raise(new GameEvent(GameEventKind.KitGiven, given.Item, 1, detail: record.Resident));
        return true;
    }

    /// <summary>Takes a piece back (to be mended, or for another): into the bag, the stash or at the feet. False with nothing there.</summary>
    public static bool TakeBack(ResidentRecord record, bool weapon, ItemContainer to, ItemContainer stash, Transform dropAt)
    {
        ItemStack had = record == null ? ItemStack.Empty : weapon ? record.KitWeapon : record.KitBody;
        if (had.IsEmpty)
        {
            return false;
        }

        // The raids it went with them stay counted (by the item: RaidsWith), so taking it back to mend it costs nothing of that.
        if (weapon)
        {
            record.KitWeapon = ItemStack.Empty;
        }
        else
        {
            record.KitBody = ItemStack.Empty;
        }

        ItemDelivery.Give(to, had, stash, dropAt);
        return true;
    }

    /// <summary>Their kit's stats on <paramref name="stats"/> under <paramref name="source"/>, as the player's gear gives them (a broken piece gives nothing).</summary>
    public static void Apply(StatSheet stats, ResidentRecord record, object source, bool withWeapon = true)
    {
        foreach (ItemStack piece in new[] { withWeapon ? record.KitWeapon : ItemStack.Empty, record.KitBody })
        {
            if (piece.IsEmpty || piece.IsBroken)
            {
                continue;
            }

            foreach (StatBonus bonus in GearPower.BonusesOf(piece, EssenceSettings.Current, true))
            {
                if (bonus.value != 0f)
                {
                    stats.AddModifier(new StatModifier(bonus.stat, bonus.op, bonus.value, source));
                }
            }
        }
    }

    /// <summary>What their weapon makes of their kind's damage: its blow against the kind's own weapon's (1 with none, or a broken one),
    /// kept between half and three times.</summary>
    public static float DamageScale(ResidentRecord record, CompanionDefinition how)
    {
        ItemStack piece = record != null ? record.KitWeapon : ItemStack.Empty;
        if (piece.IsEmpty || piece.IsBroken || how == null || !Fits(record, piece.Item, true))
        {
            return 1f;
        }

        float own = how.Weapon != null && how.Weapon.IsWeapon ? how.Weapon.Weapon.Damage : 0f;
        float given = piece.Item.Weapon.Damage;
        return own > 0f && given > 0f ? Mathf.Clamp(given / own, 0.5f, 3f) : 1f;
    }

    /// <summary>The weapon to show in their hand: the one given, while it suits them and isn't broken; else null (their kind's own).</summary>
    public static ItemDefinition ShownWeapon(ResidentRecord record)
    {
        ItemStack piece = record != null ? record.KitWeapon : ItemStack.Empty;
        return !piece.IsEmpty && !piece.IsBroken && Fits(record, piece.Item, true) ? piece.Item : null;
    }

    /// <summary>A raid come home from beside the player: each piece wears (more when it failed) and counts one more raid with them.</summary>
    public static void AfterRaid(ResidentRecord record, bool extracted)
    {
        if (record == null)
        {
            return;
        }

        PeopleSettings settings = PeopleSettings.Current;
        float wear = extracted ? settings.KitWearPerRaid : settings.KitWearFailed;
        if (!record.KitWeapon.IsEmpty)
        {
            record.KitWeapon = Worn(record.KitWeapon, wear);
            record.AddDeed(RaidsKey(record.KitWeapon.Item), 1);
        }

        if (!record.KitBody.IsEmpty)
        {
            record.KitBody = Worn(record.KitBody, wear);
            record.AddDeed(RaidsKey(record.KitBody.Item), 1);
        }
    }

    /// <summary>Raids gone along on with a piece of this item (kept through mending, and while the forest has them).</summary>
    public static int RaidsWith(ResidentRecord record, ItemDefinition item) => record != null && item != null ? record.DeedOf(RaidsKey(item)) : 0;

    /// <summary>Raids with what they carry now in that hand (0 with nothing).</summary>
    public static int RaidsWith(ResidentRecord record, bool weapon)
    {
        ItemStack piece = record == null ? ItemStack.Empty : weapon ? record.KitWeapon : record.KitBody;
        return piece.IsEmpty ? 0 : RaidsWith(record, piece.Item);
    }

    private static string RaidsKey(ItemDefinition item) => "kit:" + item.Id;

    private static ItemStack Worn(ItemStack piece, float wear) =>
        piece.Item.HasDurability ? piece.WithDurability(Mathf.Max(0f, piece.Durability - wear)) : piece;

    /// <summary>True when the piece is theirs by now (enough raids along with it).</summary>
    public static bool IsTheirs(ResidentRecord record, bool weapon) => RaidsWith(record, weapon) >= PeopleSettings.Current.KitNamedAfter;

    /// <summary>"Gunnar's Yew Bow" once it is theirs; the piece's own name before.</summary>
    public static string NameOf(ResidentRecord record, bool weapon)
    {
        ItemStack piece = record == null ? ItemStack.Empty : weapon ? record.KitWeapon : record.KitBody;
        if (piece.IsEmpty)
        {
            return "";
        }

        return IsTheirs(record, weapon) ? $"{record.Resident.DisplayName}'s {piece.DisplayName}" : piece.DisplayName;
    }
}
