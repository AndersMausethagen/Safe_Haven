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
    }
}
