using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's worn gear (GDD §10.4: no locked classes, gear defines the build), shown in the character screen (C).
/// Owns a GearSet – armor and jewelry slots, the main-hand weapon, the backpack and a tool belt – and connects it to the
/// rest of the player: equipped gear's stat bonuses go on the Combatant's stats, the main-hand weapon is what
/// PlayerCombat fights with (unarmed without one, or with a broken one), BackpackCapacity reads the backpack slot and
/// gathering reads the tool belt (ItemInventory.GatheringTier).
/// <para>
/// Equipping moves the item out of the bag (right-click in the bag, or drag onto a gear slot); unequipping moves it back
/// (right-click in the character screen). New gear arriving in the bag is put on automatically when its slot is empty.
/// The gear travels between scenes and into saves via PlayerSnapshot, and is kept after a failed raid with durability
/// loss like everything else that was carried in. Where it belongs: on the PlayerArmature prefab, reached through
/// Player.Current.Equipment.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemInventory), typeof(PlayerCombat))]
public sealed class PlayerEquipment : MonoBehaviour
{
    [Tooltip("Used when no weapon is equipped.")]
    [SerializeField] private WeaponDefinition unarmed;

    private readonly GearSet gear = new GearSet();
    private readonly bool[] wasBroken = new bool[GearSlots.Count];
    private ItemInventory inventory;
    // The two-handed weapon put away for the torch (ToggleTorch), taken out again when the torch goes back.
    private ItemDefinition stowedWeapon;
    // The shield put away for the torch, taken out again when the torch goes back.
    private ItemDefinition stowedShield;
    // The weapon in hand now and the one before it (however it changed: Z, the bag, a story), for SwapWeapon.
    private ItemDefinition heldWeapon;
    private ItemDefinition previousWeapon;
    private PlayerCombat combat;
    private Combatant combatant;
    private bool restoring;

    /// <summary>The worn gear, slot by slot.</summary>
    public GearSet Gear => gear;

    /// <summary>The main-hand weapon stack, or empty.</summary>
    public ItemStack MainHand => gear.Get(GearSlot.MainHand);
    public ItemStack OffHand => gear.Get(GearSlot.OffHand);

    /// <summary>What the player carries on their body: the weight of worn armor, rings and the weapon (not the tool belt
    /// or the backpack). Decides the dodge (PlayerMovementSettings › Equipment load).</summary>
    public float EquipLoad
    {
        get
        {
            float load = 0f;
            for (GearSlot slot = GearSlot.Head; slot <= GearSlot.MainHand; slot++)
            {
                ItemStack stack = gear.Get(slot);
                if (!stack.IsEmpty)
                {
                    load += stack.Item.Weight;
                }
            }

            ItemStack offHand = OffHand;
            if (!offHand.IsEmpty) load += offHand.Item.Weight;
            return load;
        }
    }

    /// <summary>Raised after any gear slot changes: equipped, unequipped, worn down or repaired.</summary>
    public event Action Changed;

    private ItemInventory Inventory => inventory != null ? inventory : inventory = GetComponent<ItemInventory>();

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        combatant = GetComponent<Combatant>();
    }

    private void OnEnable()
    {
        gear.Changed += OnGearChanged;
        gear.Bind(combatant != null ? combatant.Stats : null);
        ApplyWeapon();
    }

    private void OnDisable()
    {
        gear.Changed -= OnGearChanged;
        gear.Bind(null);
    }

    /// <summary>Equips the item in a slot of the bag (or safe pocket) into the gear slot it fits.</summary>
    public bool Equip(ItemContainer from, int index)
    {
        ItemDefinition item = from != null ? from.GetSlot(index).Item : null;
        return Report(gear.Equip(from, index, out string reason), reason, item);
    }

    /// <summary>Equips the item in a slot of the bag (or safe pocket) into one particular gear slot.</summary>
    public bool EquipTo(ItemContainer from, int index, GearSlot slot)
    {
        ItemDefinition item = from != null ? from.GetSlot(index).Item : null;
        return Report(gear.EquipTo(from, index, slot, out string reason), reason, item);
    }

    /// <summary>Takes off the item in a gear slot and puts it in the bag.</summary>
    public bool Unequip(GearSlot slot)
    {
        bool done = gear.Unequip(slot, Inventory.Container, out string reason);
        if (!done && reason != null)
        {
            GameMessages.Post(reason, MessageKind.Warning);
        }

        return done;
    }

    /// <summary>Puts on gear of this kind from the bag when its slot is empty, such as a freshly crafted pickaxe.</summary>
    public void AutoEquip(ItemDefinition item)
    {
        if (item == null || !item.IsEquippable)
        {
            return;
        }

        if (gear.AutoEquipFrom(Inventory.Container, item) > 0)
        {
            GameMessages.Post($"Equipped {item.DisplayName}.");
        }
    }

    /// <summary>
    /// Puts on this item from the bag right away, such as a weapon handed over in a story: what is in its slot goes back
    /// to the bag, and a two-handed weapon puts the off-hand item (the torch) away first.
    /// </summary>
    public bool EquipFromBag(ItemDefinition item)
    {
        ItemContainer bag = Inventory.Container;
        int index = FindInBag(found => found == item);
        if (index < 0 || !item.IsEquippable)
        {
            return false;
        }

        if (item.IsWeapon && item.Weapon.RequiresBothHands && !OffHand.IsEmpty && !gear.Unequip(GearSlot.OffHand, bag, out _))
        {
            return false;
        }

        return Equip(bag, index);
    }

    /// <summary>
    /// Lights the torch or puts it away (L), so a two-handed weapon such as a bow and the torch can take turns in the
    /// hands: taking the torch out puts the two-handed weapon in the bag; putting the torch away takes that weapon out again.
    /// </summary>
    public bool ToggleTorch()
    {
        ItemContainer bag = Inventory.Container;
        if (IsTorch(OffHand.Item))
        {
            if (!gear.Unequip(GearSlot.OffHand, bag, out string reason))
            {
                return Report(false, reason, null);
            }

            ItemDefinition weapon = stowedWeapon;
            stowedWeapon = null;
            if (weapon != null && MainHand.IsEmpty)
            {
                EquipFromBag(weapon);
            }

            ItemDefinition shield = stowedShield;
            stowedShield = null;
            if (shield != null && OffHand.IsEmpty)
            {
                EquipFromBag(shield);
            }

            return true;
        }

        int torch = FindInBag(IsTorch);
        if (torch < 0)
        {
            GameMessages.Post("You have no torch.", MessageKind.Warning);
            return false;
        }

        if (!MainHand.IsEmpty && MainHand.Item.IsWeapon && MainHand.Item.Weapon.RequiresBothHands)
        {
            ItemDefinition held = MainHand.Item;
            if (!gear.Unequip(GearSlot.MainHand, bag, out string reason))
            {
                return Report(false, reason, null);
            }

            stowedWeapon = held;
        }

        if (!OffHand.IsEmpty)
        {
            ItemDefinition shield = OffHand.Item;
            if (!gear.Unequip(GearSlot.OffHand, bag, out string reason))
            {
                return Report(false, reason, null);
            }

            stowedShield = shield;
        }

        return EquipTo(bag, torch, GearSlot.OffHand);
    }

    private static bool IsTorch(ItemDefinition item) => item != null && item.IsWeapon && item.Weapon.Kind == WeaponKind.Torch;

    /// <summary>
    /// Swaps the weapon in hand for the one held before it (Z), such as the axe and the bow. The first time, or when that
    /// one is no longer in the bag, it takes out the next weapon in the bag; with no other weapon it puts the one in hand
    /// away (fists), and the next press takes it out again. Not mid-swing. The torch keeps its own key (ToggleTorch).
    /// </summary>
    public bool SwapWeapon()
    {
        if (combat != null && combat.IsAttacking)
        {
            return false;
        }

        ItemDefinition held = MainHand.IsEmpty ? null : MainHand.Item;
        ItemDefinition next = previousWeapon != null && previousWeapon != held && FindInBag(found => found == previousWeapon) >= 0
            ? previousWeapon
            : null;
        if (next == null)
        {
            int index = FindInBag(found => IsSwappable(found) && found != held);
            next = index >= 0 ? Inventory.Container.GetSlot(index).Item : null;
        }

        if (next != null)
        {
            return EquipFromBag(next);
        }

        if (held != null && Unequip(GearSlot.MainHand))
        {
            GameMessages.Post($"Put away {held.DisplayName}.");
            return true;
        }

        if (held == null)
        {
            GameMessages.Post("You have no weapon to take out.", MessageKind.Warning);
        }

        return false;
    }

    private static bool IsSwappable(ItemDefinition item) => item != null && item.IsWeapon && item.Weapon.Kind != WeaponKind.Torch && !item.Weapon.IsShield;

    // Remembers the weapon put away whenever the main hand changes (equipping goes through an empty hand: old, none, new).
    private void TrackHeldWeapon()
    {
        ItemDefinition now = MainHand.IsEmpty ? null : MainHand.Item;
        if (now == heldWeapon)
        {
            return;
        }

        if (IsSwappable(heldWeapon) && !restoring)
        {
            previousWeapon = heldWeapon;
        }

        heldWeapon = now;
    }

    private int FindInBag(Predicate<ItemDefinition> match)
    {
        ItemContainer bag = Inventory.Container;
        for (int i = 0; i < bag.SlotCount; i++)
        {
            if (!bag.GetSlot(i).IsEmpty && match(bag.GetSlot(i).Item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Puts back the gear from a snapshot without announcing it. Snapshots taken before gear slots existed (old saves)
    /// have no gear: the weapon that was equipped from the bag, then any other gear in the bag, is put on instead.
    /// </summary>
    public void Restore(IReadOnlyList<PlayerSnapshot.SlotRecord> records, bool captured, int legacyWeaponSlot = -1)
    {
        restoring = true;
        try
        {
            gear.Slots.Clear();
            if (captured)
            {
                foreach (PlayerSnapshot.SlotRecord record in records)
                {
                    gear.Slots.SetSlot(record.Index, record.Stack);
                }
            }
            else
            {
                if (legacyWeaponSlot >= 0)
                {
                    gear.EquipTo(Inventory.Container, legacyWeaponSlot, GearSlot.MainHand, out _);
                }

                gear.AutoEquipFrom(Inventory.Container);
            }

            MoveOutOfRetiredSlots();
            for (int i = 0; i < wasBroken.Length; i++)
            {
                wasBroken[i] = gear.Slots.GetSlot(i).IsBroken;
            }
        }
        finally
        {
            restoring = false;
        }

        ApplyWeapon();
        Changed?.Invoke();
    }

    // Gear worn in a slot that has since been retired (an old save) goes to the bag, or the stash when the bag is full.
    private void MoveOutOfRetiredSlots()
    {
        for (int i = 0; i < GearSlots.Count; i++)
        {
            ItemStack stack = gear.Slots.GetSlot(i);
            if (stack.IsEmpty || !GearSlots.IsRetired((GearSlot)i))
            {
                continue;
            }

            gear.Slots.SetSlot(i, ItemStack.Empty);
            if (Inventory.Container.AddStack(stack) < stack.Count)
            {
                GameSession.Stash.Items.AddStack(stack);
            }

            GameMessages.Post($"{stack.Item.DisplayName} has no gear slot any more: put in your bag.", MessageKind.Info);
        }
    }

    private bool Report(bool done, string reason, ItemDefinition item)
    {
        if (done)
        {
            GameMessages.Post($"Equipped {item.DisplayName}.");
        }
        else if (reason != null)
        {
            GameMessages.Post(reason, MessageKind.Warning);
        }

        return done;
    }

    private void OnGearChanged(GearSlot slot)
    {
        ItemStack stack = gear.Get(slot);
        bool broken = stack.IsBroken;
        if (broken && !wasBroken[(int)slot] && !restoring)
        {
            GameMessages.Post($"{stack.Item.DisplayName} is broken and gives nothing until it is repaired.", MessageKind.Warning);
        }

        wasBroken[(int)slot] = broken;
        if (slot == GearSlot.MainHand)
        {
            TrackHeldWeapon();
        }

        if (restoring)
        {
            return;
        }

        ApplyWeapon();
        Changed?.Invoke();
    }

    private void ApplyWeapon()
    {
        if (combat == null)
        {
            combat = GetComponent<PlayerCombat>();
        }

        ItemStack stack = MainHand;
        WeaponDefinition weapon = !stack.IsEmpty && stack.Item.IsWeapon && !stack.IsBroken ? stack.Item.Weapon : unarmed;
        if (combat != null && combat.Weapon != weapon)
        {
            combat.Weapon = weapon;
        }

        ItemStack offHand = OffHand;
        if (combat != null)
        {
            combat.Shield = !offHand.IsEmpty && offHand.Item.IsWeapon && offHand.Item.Weapon.IsShield && !offHand.IsBroken ? offHand.Item.Weapon : null;
        }
    }
}
