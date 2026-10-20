/// <summary>
/// Finds authored content by its stable id. Save files store ids rather than asset references, so loading goes through
/// this to turn them back into definitions. Returns null for ids that no longer exist.
/// </summary>
public interface IContentLookup
{
    ItemDefinition FindItem(string id);
    ResourceDefinition FindResource(string id);
    RecipeDefinition FindRecipe(string id);
    QuestDefinition FindQuest(string id);
    BuildingDefinition FindBuilding(string id);
    ResidentDefinition FindResident(string id);
    JobDefinition FindJob(string id);
}