using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses the player: the look made on the character creation screen (GameSession.Appearance) and the worn look of each
/// piece of gear they have on, rebuilt whenever their gear changes. Runs before ModularCharacter's own build, so a
/// woman's body is in place before anything looks up her bones.
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularCharacter), typeof(PlayerEquipment))]
[AddComponentMenu("Safe Haven/Player/Player Look")]
public sealed class PlayerLook : MonoBehaviour
{
    private readonly List<GearLookDefinition> looks = new List<GearLookDefinition>();
    private ModularCharacter character;
    private PlayerEquipment equipment;

    private void Awake()
    {
        character = GetComponent<ModularCharacter>();
        equipment = GetComponent<PlayerEquipment>();
        character.SetAppearance(GameSession.Appearance);
    }

    private void OnEnable()
    {
        equipment.Changed += Dress;
        Dress();
    }

    private void OnDisable() => equipment.Changed -= Dress;

    private void Dress()
    {
        Outfit(slot =>
        {
            ItemStack stack = equipment.Gear.Get(slot);
            return stack.IsEmpty ? null : stack.Item;
        }, character.Appearance.clothes, looks);
        character.SetOutfit(looks);
    }

    /// <summary>
    /// What someone wearing this gear looks like: each worn item's look, and their plain clothes and shoes where chest or
    /// feet are bare (not just underclothes). Shared with co-op's remote players, who know the gear only by item.
    /// </summary>
    public static void Outfit(Func<GearSlot, ItemDefinition> wornIn, int clothes, List<GearLookDefinition> into)
    {
        into.Clear();
        for (int slot = 0; slot < GearSlots.Count; slot++)
        {
            ItemDefinition item = wornIn((GearSlot)slot);
            if (item != null && item.WornLook != null)
            {
                into.Add(item.WornLook);
            }
        }

        CharacterCatalog catalog = CharacterCatalog.Current;
        if (catalog != null)
        {
            if (wornIn(GearSlot.Chest) == null)
            {
                into.Add(catalog.PlainClothing(clothes));
            }

            if (wornIn(GearSlot.Feet) == null)
            {
                into.Add(catalog.PlainShoes);
            }
        }
    }
}
