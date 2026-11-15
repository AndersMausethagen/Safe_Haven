using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// How a player looks and where they are, as their friends need it: name, the body made at character creation, the gear
/// they wear (item ids by slot), what is in their hand, and the place (scene) they are in. The owner fills it from their
/// <see cref="Player"/>; the others dress a <see cref="RemotePlayerBody"/> from it. Ids only, like a save.
/// </summary>
public struct PlayerLookState : INetworkSerializable, IEquatable<PlayerLookState>
{
    private const char Separator = '|';

    public FixedString64Bytes Name;
    public int Gender;
    public int Skin;
    public int Hair;
    public int HairColour;
    public int Brows;
    public int Beard;
    public int Eyes;
    public int Underclothes;
    public int Clothes;
    /// <summary>Item ids by gear slot, '|' between them, empty where nothing is worn.</summary>
    public FixedString512Bytes Gear;
    /// <summary>The item shown in the right hand, or empty.</summary>
    public FixedString64Bytes Held;
    /// <summary>The scene the player is in. Friends are only drawn where you are.</summary>
    public FixedString128Bytes Place;
    /// <summary>Knocked down in a shared raid, waiting for a friend to help them up (CoopDowned).</summary>
    public bool Downed;
    /// <summary>Health as a share of the maximum, 0 to 255 (the party panel).</summary>
    public byte Health;
    /// <summary>How easily creatures see them, 0 to 255 (sneaking, cover, fighting: their own game's stealth).</summary>
    public byte Visibility;
    /// <summary>Drawing or aiming a bow, or winding up a cast: the host's creatures answer it as shooting.</summary>
    public bool Drawing;

    /// <summary>Whether <paramref name="player"/> is drawing or aiming a bow or winding up a cast.</summary>
    public static bool DrawingOf(Player player) =>
        player != null && ((player.TryGetComponent(out PlayerArchery archery) && (archery.IsDrawing || archery.IsAiming))
            || (player.TryGetComponent(out PlayerCombat combat) && combat.IsCasting));

    public string DisplayName => Name.Length > 0 ? Name.ToString() : CharacterAppearance.DefaultName;

    /// <summary>The look of the local player now (or an empty look when there is none).</summary>
    /// <summary>A player's stealth visibility as the look carries it.</summary>
    public static byte VisibilityByte(Player player) =>
        player != null && player.Stealth != null ? (byte)Mathf.Clamp(Mathf.RoundToInt(player.Stealth.Visibility * 255f), 0, 255) : (byte)255;

    public static PlayerLookState Of(Player player, string place)
    {
        var state = new PlayerLookState { Place = Clip128(place), Downed = CoopDowned.IsDown };
        CharacterAppearance look = GameSession.Appearance ?? new CharacterAppearance();
        state.Name = Clip64(look.DisplayName);
        state.Gender = (int)look.gender;
        state.Skin = look.skin;
        state.Hair = look.hair;
        state.HairColour = look.hairColour;
        state.Brows = look.brows;
        state.Beard = look.beard;
        state.Eyes = look.eyes;
        state.Underclothes = look.underclothes;
        state.Clothes = look.clothes;
        if (player == null)
        {
            return state;
        }

        state.Visibility = VisibilityByte(player);
        state.Drawing = DrawingOf(player);
        state.Health = player.TryGetComponent(out Damageable health) && health.MaxHealth > 0f
            ? (byte)Mathf.Clamp(Mathf.CeilToInt(health.CurrentHealth / health.MaxHealth * 255f), 0, 255)
            : (byte)255;
        var gear = new StringBuilder();
        for (int slot = 0; slot < GearSlots.Count; slot++)
        {
            if (slot > 0)
            {
                gear.Append(Separator);
            }

            ItemStack stack = player.Equipment != null ? player.Equipment.Gear.Get((GearSlot)slot) : ItemStack.Empty;
            if (!stack.IsEmpty)
            {
                gear.Append(stack.Item.Id);
            }
        }

        state.Gear = Clip512(gear.ToString());
        HeldItemView held = player.GetComponent<HeldItemView>();
        if (held != null && held.Shown != null)
        {
            state.Held = Clip64(held.Shown.Id);
        }

        return state;
    }

    /// <summary>The body made at character creation.</summary>
    public CharacterAppearance Appearance() => new CharacterAppearance
    {
        name = Name.ToString(),
        gender = (CharacterGender)Gender,
        skin = Skin,
        hair = Hair,
        hairColour = HairColour,
        brows = Brows,
        beard = Beard,
        eyes = Eyes,
        underclothes = Underclothes,
        clothes = Clothes,
    };

    /// <summary>The item worn in <paramref name="slot"/>, looked up in <paramref name="content"/>; null when none or unknown.</summary>
    public ItemDefinition Worn(GearSlot slot, IContentLookup content)
    {
        if (content == null)
        {
            return null;
        }

        string[] ids = Gear.ToString().Split(Separator);
        int index = (int)slot;
        return index < ids.Length && ids[index].Length > 0 ? content.FindItem(ids[index]) : null;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Gender);
        serializer.SerializeValue(ref Skin);
        serializer.SerializeValue(ref Hair);
        serializer.SerializeValue(ref HairColour);
        serializer.SerializeValue(ref Brows);
        serializer.SerializeValue(ref Beard);
        serializer.SerializeValue(ref Eyes);
        serializer.SerializeValue(ref Underclothes);
        serializer.SerializeValue(ref Clothes);
        serializer.SerializeValue(ref Gear);
        serializer.SerializeValue(ref Held);
        serializer.SerializeValue(ref Place);
        serializer.SerializeValue(ref Downed);
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Visibility);
        serializer.SerializeValue(ref Drawing);
    }

    public bool Equals(PlayerLookState other) =>
        Name.Equals(other.Name) && Gender == other.Gender && Skin == other.Skin && Hair == other.Hair &&
        HairColour == other.HairColour && Brows == other.Brows && Beard == other.Beard && Eyes == other.Eyes &&
        Underclothes == other.Underclothes && Clothes == other.Clothes && Gear.Equals(other.Gear) &&
        Held.Equals(other.Held) && Place.Equals(other.Place) && Downed == other.Downed && Health == other.Health && Visibility == other.Visibility
        && Drawing == other.Drawing;

    public override bool Equals(object obj) => obj is PlayerLookState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Name, Gear, Held, Place, Gender, Skin, Hair);

    // Fixed strings throw when overfilled: cut long names and gear lists instead.
    private static FixedString64Bytes Clip64(string text) => new FixedString64Bytes(Clip(text, FixedString64Bytes.UTF8MaxLengthInBytes));
    private static FixedString128Bytes Clip128(string text) => new FixedString128Bytes(Clip(text, FixedString128Bytes.UTF8MaxLengthInBytes));
    private static FixedString512Bytes Clip512(string text) => new FixedString512Bytes(Clip(text, FixedString512Bytes.UTF8MaxLengthInBytes));

    private static string Clip(string text, int maxBytes)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        while (Encoding.UTF8.GetByteCount(text) > maxBytes)
        {
            text = text.Substring(0, text.Length - 1);
        }

        return text;
    }
}
