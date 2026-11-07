using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The code conventions in ARCHITECTURE.md ("Conventions: reaching a system, and keeping classes to one job") that a
/// reader can't be trusted to keep, checked over the runtime scripts' source. Entering Play doesn't reload scripts in
/// this project, so anything static outlives a Play session unless it is reset when the next one starts.
/// </summary>
public sealed class ConventionTests
{
    private const string Hook = "RuntimeInitializeLoadType.SubsystemRegistration";

    private static readonly Regex Singleton = new Regex(@"public static (\w+) Current \{ get; private set; \}");
    private static readonly Regex BackingFieldSingleton = new Regex(@"public static (\w+) Current\s*=>\s*\w+;");
    private static readonly Regex InstanceSingleton = new Regex(@"\b(?:public|internal) static\s+\w+\s+Instance\b");
    private static readonly Regex StaticCollection = new Regex(@"static readonly (?:List|Dictionary|HashSet|Queue|Stack)<");
    private static readonly Regex StaticEvent = new Regex(@"public static event [^;]*?(\w+);");

    private static IEnumerable<(string path, string source)> RuntimeScripts()
    {
        foreach (string file in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (!path.Contains("/Editor/"))
            {
                yield return (path, WithoutComments(File.ReadAllText(path)));
            }
        }
    }

    private static string WithoutComments(string source) => Regex.Replace(source, @"^\s*//.*$", "", RegexOptions.Multiline);

    [Test]
    public void EverySingleton_IsReachedThroughCurrent()
    {
        var offenders = new List<string>();
        foreach ((string path, string source) in RuntimeScripts())
        {
            if (InstanceSingleton.IsMatch(source))
            {
                offenders.Add($"{path}: an Instance (use Current)");
            }

            if (BackingFieldSingleton.IsMatch(source))
            {
                offenders.Add($"{path}: Current reads a field (use Current {{ get; private set; }})");
            }
        }

        Assert.IsEmpty(offenders, string.Join("\n", offenders));
    }

    [Test]
    public void EverySingleton_IsResetWhenPlayStarts()
    {
        string[] offenders = RuntimeScripts().Where(script => Singleton.IsMatch(script.source) && !script.source.Contains(Hook))
            .Select(script => script.path).ToArray();
        Assert.IsEmpty(offenders, "Current is never reset when Play starts:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void EveryStaticCollection_IsClearedWhenPlayStarts()
    {
        // Registries of live objects and caches of made meshes: left from the last session, they hold destroyed objects.
        string[] offenders = RuntimeScripts().Where(script => StaticCollection.IsMatch(script.source) && !script.source.Contains(Hook))
            .Select(script => script.path).ToArray();
        Assert.IsEmpty(offenders, "A static list, set or table is never cleared when Play starts:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void EveryStaticEventSubscription_IsUndone()
    {
        // A static event can't simply be cleared when Play starts (Editor tools listen to some, and they don't subscribe
        // again), so whatever subscribes in the game has to unsubscribe, or the next session runs the old handlers too.
        List<(string path, string source)> scripts = RuntimeScripts().ToList();
        var events = new List<string>();
        foreach ((string path, string source) in scripts)
        {
            string type = Path.GetFileNameWithoutExtension(path).Split('.')[0];
            events.AddRange(StaticEvent.Matches(source).Cast<Match>().Select(match => $"{type}.{match.Groups[1].Value}"));
        }

        var offenders = new List<string>();
        foreach ((string path, string source) in scripts)
        {
            foreach (string staticEvent in events)
            {
                int added = Regex.Matches(source, Regex.Escape(staticEvent) + @"\s*\+=").Count;
                int removed = Regex.Matches(source, Regex.Escape(staticEvent) + @"\s*-=").Count;
                if (added > removed)
                {
                    offenders.Add($"{path}: {staticEvent} += {added}, -= {removed}");
                }
            }
        }

        Assert.Greater(events.Count, 10, "The scan found the static events");
        Assert.IsEmpty(offenders, string.Join("\n", offenders));
    }
}
