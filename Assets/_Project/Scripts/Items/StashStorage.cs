/// <summary>
/// What the player keeps safe in Safe Haven: items and raw resources that stay home during raids, so a failed raid can't
/// take them. Plain data owned by the GameSession so it outlives scene loads; the StashWindow moves things in and out.
/// </summary>
public sealed class StashStorage
{
    public const int Columns = 10;
    public const int Rows = 6;
    public const int ResourceCapacity = 9999;

    public StashStorage()
    {
        Items = new ItemContainer(Columns, Rows);
        Resources = new ResourceStorage(ResourceCapacity);
    }

    public ItemContainer Items { get; }
    public ResourceStorage Resources { get; }
}
