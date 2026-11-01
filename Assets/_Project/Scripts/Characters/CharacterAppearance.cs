using System;

public enum CharacterGender
{
    Male = 0,
    Female = 1,
}

/// <summary>
/// How a character looks and what they are called: the choices made on the character creation screen (or authored on an
/// NPC). Plain data, kept per game in GameSession and saved with it. Every number is an index into the matching list of
/// the gender's CharacterBodyDefinition, which clamps it, so a look stays valid when options are added or removed.
/// </summary>
[Serializable]
public sealed class CharacterAppearance
{
    public const int MaximumNameLength = 24;
    public const string DefaultName = "Wanderer";

    public string name = "";
    public CharacterGender gender;
    /// <summary>Skin tone of the body.</summary>
    public int skin;
    /// <summary>The face (head texture).</summary>
    public int face;
    public int hair;
    public int hairColour;
    public int brows;
    /// <summary>Only men grow one. 0 is none.</summary>
    public int beard;
    public int eyes;
    /// <summary>What is worn under the gear.</summary>
    public int underclothes;
    /// <summary>The plain clothes (an index into the catalog's) worn while no body armour is.</summary>
    public int clothes;

    /// <summary>The name to show: the chosen one, or <see cref="DefaultName"/> when none was given.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();

    public CharacterAppearance Clone() => (CharacterAppearance)MemberwiseClone();

    /// <summary>Changes to the name only don't need the body rebuilt, so it isn't part of the key.</summary>
    public string LookKey => $"{(int)gender}.{skin}.{face}.{hair}.{hairColour}.{brows}.{beard}.{eyes}.{underclothes}";

    /// <summary>Keeps the name to the allowed length and without surrounding spaces.</summary>
    public static string CleanName(string value)
    {
        value = (value ?? "").Trim();
        return value.Length > MaximumNameLength ? value.Substring(0, MaximumNameLength) : value;
    }
}
