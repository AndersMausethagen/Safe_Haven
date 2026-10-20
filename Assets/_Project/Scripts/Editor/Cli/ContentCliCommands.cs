using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Pipeline.Commands;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Edit-time content commands for the Unity CLI (`unity command sh_content ...`). They replace the long ad-hoc eval
/// scripts used to list, inspect and health-check game data. Each returns compact plain text.
/// </summary>
public static class ContentCliCommands
{
    private const string Tag = "safehaven";
    private static readonly Regex GuidPattern = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);
    private static readonly string[] YamlExtensions = { ".asset", ".prefab", ".unity", ".mat", ".controller", ".overrideController", ".anim", ".playable" };

    [CliCommand("sh_content", "List or inspect Safe Haven content. One match with no --fields shows every field.", Tags = new[] { Tag })]
    public static string Content(
        [CliArg("type", "item, recipe, node, building, quest, monster, raid, modifier, essence, or a ScriptableObject class name")] string type,
        [CliArg("filter", "Part of an asset name, id or display name")] string filter = "",
        [CliArg("fields", "Comma-separated serialized field names, or * for all")] string fields = "",
        [CliArg("limit", "Maximum rows (0 = all)")] int limit = 40)
    {
        Type assetType = CliLookup.ResolveType(type, out string defaultFields);
        List<Object> matches = CliLookup.LoadAll(assetType).Where(asset => CliLookup.Matches(asset, filter)).ToList();
        if (matches.Count == 0)
        {
            return $"No {assetType.Name} matches '{filter}'.";
        }

        // A single hit is almost always "show me this one": give everything unless fields were asked for.
        string shown = !string.IsNullOrWhiteSpace(fields) ? fields : matches.Count == 1 ? "*" : defaultFields ?? "*";
        List<string> lines = matches.Select(asset => $"{CliLookup.Label(asset)}: {CliLookup.DescribeFields(asset, shown)}").ToList();
        return CliLookup.JoinLimited(lines, limit, $"{matches.Count} {assetType.Name}");
    }

    [CliCommand("sh_health", "Run every Content Editor page's checks and list the problems found.", Tags = new[] { Tag })]
    public static string Health(
        [CliArg("page", "Only this page title (e.g. Items, Recipes)")] string page = "",
        [CliArg("limit", "Maximum rows (0 = all)")] int limit = 60)
    {
        var lines = new List<string>();
        int checkedAssets = 0;
        foreach (ContentSection section in ContentEditorWindow.CreateSections())
        {
            try
            {
                if (section is ContentHealthSection || section is PrefabBrowserSection)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(page) && section.Title.IndexOf(page, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                List<Object> all = section.Load();
                checkedAssets += all.Count;
                foreach (Object asset in all)
                {
                    foreach (string problem in section.FindProblems(asset, all))
                    {
                        lines.Add($"[{section.Title}] {asset.name}: {problem}");
                    }
                }
            }
            catch (Exception exception)
            {
                lines.Add($"[{section.Title}] check failed: {exception.Message}");
            }
            finally
            {
                section.Dispose();
            }
        }

        return lines.Count == 0
            ? $"No problems in {checkedAssets} assets."
            : CliLookup.JoinLimited(lines, limit, $"{lines.Count} problems in {checkedAssets} assets");
    }

    [CliCommand("sh_missing_refs", "Find GUID references in scenes, prefabs and assets that point at deleted assets.", Tags = new[] { Tag })]
    public static string MissingReferences(
        [CliArg("folder", "Folder to scan")] string folder = "Assets/_Project",
        [CliArg("limit", "Maximum rows (0 = all)")] int limit = 40)
    {
        var lines = new List<string>();
        int scanned = 0;
        foreach (string file in YamlFiles(folder))
        {
            scanned++;
            var missing = new HashSet<string>();
            foreach (Match match in GuidPattern.Matches(File.ReadAllText(file)))
            {
                string guid = match.Groups[1].Value;
                // All-zero-prefixed GUIDs are Unity's built-in resources.
                if (!guid.StartsWith("0000000000000000") && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    missing.Add(guid);
                }
            }

            if (missing.Count > 0)
            {
                lines.Add($"{ToAssetPath(file)}: {string.Join(", ", missing)}");
            }
        }

        return lines.Count == 0
            ? $"No missing references in {scanned} files."
            : CliLookup.JoinLimited(lines, limit, $"{lines.Count} files with missing references (of {scanned})");
    }

    [CliCommand("sh_find_refs", "List the scenes, prefabs and assets that reference an asset (every asset sharing the name).", Tags = new[] { Tag })]
    public static string FindReferences(
        [CliArg("asset", "Asset path, GUID, or name/id of an asset")] string asset,
        [CliArg("folder", "Folder to scan")] string folder = "Assets",
        [CliArg("limit", "Maximum rows per asset (0 = all)")] int limit = 40)
    {
        List<string> targets = ResolveTargets(asset);
        var hits = targets.ToDictionary(path => path, _ => new List<string>());
        var guids = targets.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
        foreach (string file in YamlFiles(folder))
        {
            string assetPath = ToAssetPath(file);
            string text = File.ReadAllText(file);
            foreach (string target in targets)
            {
                if (assetPath != target && text.Contains(guids[target]))
                {
                    hits[target].Add(assetPath);
                }
            }
        }

        return string.Join("\n", targets.Select(target => hits[target].Count == 0
            ? $"Nothing references {target}."
            : CliLookup.JoinLimited(hits[target], limit, $"{hits[target].Count} files reference {target}")));
    }

    private static List<string> ResolveTargets(string key)
    {
        if (Regex.IsMatch(key ?? "", "^[0-9a-f]{32}$"))
        {
            string fromGuid = AssetDatabase.GUIDToAssetPath(key);
            if (string.IsNullOrEmpty(fromGuid))
            {
                throw new ArgumentException($"No asset has GUID {key}.");
            }

            return new List<string> { fromGuid };
        }

        if (File.Exists(key) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(key)))
        {
            return new List<string> { key.Replace('\\', '/') };
        }

        // Content assets match by id or display name too; any other asset by its file name.
        var paths = CliLookup.FindExact(typeof(UnityEngine.ScriptableObject), key, out _)
            .Select(AssetDatabase.GetAssetPath)
            .Concat(AssetDatabase.FindAssets(key).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), key, StringComparison.OrdinalIgnoreCase)))
            .Where(p => !string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
            .Distinct()
            .ToList();
        if (paths.Count == 0)
        {
            throw new ArgumentException($"No asset matches '{key}'.");
        }

        return paths;
    }

    private static IEnumerable<string> YamlFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new ArgumentException($"Folder '{folder}' doesn't exist.");
        }

        return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(file => YamlExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
    }

    private static string ToAssetPath(string file) => file.Replace('\\', '/');
}
