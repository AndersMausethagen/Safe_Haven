/// <summary>
/// A definition's name as players read it ("Iron Ore", "Oak Plank"), for code that names a definition without knowing
/// its type: quest text, station output. Items and essences give more through <see cref="IStackDefinition"/>.
/// </summary>
public interface IDisplayName
{
    string DisplayName { get; }
}
