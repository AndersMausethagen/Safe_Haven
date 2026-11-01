/// <summary>The build menu's tabs. Buildings are sorted into them by what they are (BuildingDefinition), not by a field.</summary>
public enum BuildCategory
{
    Stations,
    Walls,
    Structures,
    Manage,
}

public static class BuildCategories
{
    public static BuildCategory Of(BuildingDefinition building)
    {
        if (building.Station != CraftingStationType.None)
        {
            return BuildCategory.Stations;
        }

        return building.PlaceInLines ? BuildCategory.Walls : BuildCategory.Structures;
    }

    public static string Name(BuildCategory category)
    {
        switch (category)
        {
            case BuildCategory.Stations: return "Crafting Stations";
            case BuildCategory.Walls: return "Walls & Fences";
            case BuildCategory.Structures: return "Structures";
            default: return "Manage";
        }
    }
}
