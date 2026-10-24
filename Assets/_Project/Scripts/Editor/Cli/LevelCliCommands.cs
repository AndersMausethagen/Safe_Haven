using System.Linq;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// The Level Check for the Unity CLI (`unity command sh_level`, `-- --fix true` to apply every fix it can), the
/// command-line twin of Safe Haven > Level > Level Check.
/// </summary>
public static class LevelCliCommands
{
    private const string Tag = "safehaven";

    [CliCommand("sh_level", "Check the open scene for level problems (floating, unreachable, under water, save ids, leftovers) and count its contents. --fix true applies every fix it can.", Tags = new[] { Tag })]
    public static string Level(bool fix = false, bool census = true)
    {
        if (EditorApplication.isPlaying)
        {
            return "Stop playing first: the Level Check looks at the scene as saved.";
        }

        Scene scene = SceneManager.GetActiveScene();
        var report = new StringBuilder();
        if (fix)
        {
            int fixedCount = 0;
            Undo.IncrementCurrentGroup();
            foreach (LevelIssue issue in LevelCheck.Run(scene).Where(i => i.Fix != null && i.Target != null))
            {
                issue.Fix();
                fixedCount++;
            }

            Undo.SetCurrentGroupName("Level Check fixes");
            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            report.AppendLine($"Fixed {fixedCount} problem(s) (undoable; save the scene to keep them).");
        }

        if (census)
        {
            report.AppendLine($"{scene.name} contains:");
            foreach (var pair in LevelCheck.Census(scene))
            {
                report.AppendLine($"  {pair.Key}: {pair.Value}");
            }
        }

        var issues = LevelCheck.Run(scene);
        report.AppendLine(issues.Count == 0 ? "No problems found." : $"{issues.Count} problem(s):");
        foreach (LevelIssue issue in issues)
        {
            report.AppendLine($"  [{issue.Kind}] {issue.Message}{(issue.Fix != null ? $" (fix: {issue.FixLabel})" : "")}");
        }

        return report.ToString().TrimEnd();
    }
}
