/// <summary>The build menu's tabs. Buildings are sorted into them by what they are (BuildingDefinition), not by a field.</summary>
public enum BuildCategory
{
    Stations,
    Walls,
    Structures,
    Manage,
    Decorations,
}

public static class BuildCategories
{
    public static BuildCategory Of(BuildingDefinition building)
    {
        // A station's extensions (a bellows, a log rack) sit with the stations they extend.
        if (building.Station != CraftingStationType.None || building.Extends != null)
        {
            return BuildCategory.Stations;
        }

        if (building.PlaceInLines)
        {
            return BuildCategory.Walls;
        }

        return building.Decoration ? BuildCategory.Decorations : BuildCategory.Structures;
    }

    public static string Name(BuildCategory category)
    {
        switch (category)
        {
            case BuildCategory.Stations: return "Crafting Stations";
            case BuildCategory.Walls: return "Fences & Paths";
            case BuildCategory.Structures: return "Structures";
            case BuildCategory.Decorations: return "Decorations";
            default: return "Manage";
        }
    }
}
