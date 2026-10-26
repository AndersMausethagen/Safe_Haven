using System.IO;
using System.Linq;
using System.Text;
using Unity.Pipeline.Commands;

/// <summary>Unity CLI access to the planner import: `unity command sh_planner_preview` then `sh_planner_apply`.</summary>
public static class PlannerCliCommands
{
    private const string Tag = "safehaven";

    [CliCommand("sh_planner_preview", "Show what the Progression Planner manifest would create and change. Changes nothing.", Tags = new[] { Tag })]
    public static string Preview([CliArg("manifest", "Manifest path (default: ../ProgressionPlanner/export/manifest.json)")] string manifest = "")
    {
        var plan = PlannerImporter.Plan(PlannerImporter.Parse(File.ReadAllText(Resolve(manifest))));
        var text = new StringBuilder();
        foreach (PlannerPlanEntry entry in plan)
        {
            text.AppendLine($"{entry.State.ToString().ToUpperInvariant()} {entry.Entity.key}");
            foreach (string error in entry.Errors)
            {
                text.AppendLine("  ! " + error);
            }

            foreach (PlannerPlanEntry.FieldChange change in entry.Changes)
            {
                text.AppendLine($"  {change.Field.name}: {(entry.State == PlannerPlanEntry.Kind.New ? change.New : change.Old + " -> " + change.New)}");
            }
        }

        return plan.Count == 0 ? "The manifest is empty." : text.ToString().TrimEnd();
    }

    [CliCommand("sh_planner_apply", "Create new assets and update changed fields from the Progression Planner manifest. Never deletes.", Tags = new[] { Tag })]
    public static string Apply([CliArg("manifest", "Manifest path (default: ../ProgressionPlanner/export/manifest.json)")] string manifest = "")
    {
        var plan = PlannerImporter.Plan(PlannerImporter.Parse(File.ReadAllText(Resolve(manifest))));
        int blocked = plan.Count(e => e.State == PlannerPlanEntry.Kind.Error);
        return PlannerImporter.Apply(plan) + (blocked > 0 ? $" {blocked} entries were skipped because of errors." : string.Empty);
    }

    private static string Resolve(string manifest)
    {
        return string.IsNullOrWhiteSpace(manifest) ? PlannerImportWindow.DefaultManifestPath() : manifest;
    }
}
