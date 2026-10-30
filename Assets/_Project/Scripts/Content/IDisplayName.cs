/// <summary>
/// A definition's name as players read it ("Iron Ore", "Oak Plank"), for code that names a definition without knowing
/// its type: quest text, station output.
/// </summary>
public interface IDisplayName
{
    string DisplayName { get; }
}
