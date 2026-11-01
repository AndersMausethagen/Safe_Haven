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
        looks.Clear();
        for (int slot = 0; slot < GearSlots.Count; slot++)
        {
            ItemStack stack = equipment.Gear.Get((GearSlot)slot);
            if (!stack.IsEmpty && stack.Item.WornLook != null)
            {
                looks.Add(stack.Item.WornLook);
            }
        }

        // Without body armour or boots the player wears their plain clothes, not just their underclothes.
        CharacterCatalog catalog = CharacterCatalog.Current;
        if (catalog != null)
        {
            if (equipment.Gear.Get(GearSlot.Chest).IsEmpty)
            {
                looks.Add(catalog.PlainClothing(character.Appearance.clothes));
            }

            if (equipment.Gear.Get(GearSlot.Feet).IsEmpty)
            {
                looks.Add(catalog.PlainShoes);
            }
        }

        character.SetOutfit(looks);
    }
}
