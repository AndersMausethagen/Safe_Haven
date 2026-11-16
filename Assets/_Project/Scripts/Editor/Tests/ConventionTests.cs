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
    private const string HookAttribute = "RuntimeInitializeLoadType.SubsystemRegistration)]";

    private static readonly Regex Singleton = new Regex(@"public static (\w+) Current \{ get; private set; \}");
    private static readonly Regex BackingFieldSingleton = new Regex(@"public static (\w+) Current\s*=>\s*\w+;");
    private static readonly Regex InstanceSingleton = new Regex(@"\b(?:public|internal) static\s+\w+\s+Instance\b");
    private static readonly Regex StaticCollection = new Regex(@"static readonly (?:List|Dictionary|HashSet|Queue|Stack)<.*?> (\w+) = new");
    private static readonly Regex StaticEvent = new Regex(@"public static event [^;]*?(\w+);");
    private static readonly Regex TypeDeclaration = new Regex(@"\b(?:class|struct)\s+(\w+)");

    // Static collections deliberately kept when Play starts, and why.
    private static readonly Dictionary<string, string> KeptCollections = new Dictionary<string, string>
    {
        ["MeshSurface.all"] = "its surfaces register in edit mode too (ExecuteAlways) and keep their slot in it",
        ["MeshSurface.materials"] = "goes with MeshSurface.all",
    };

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

    // What runs when Play starts: the bodies of the SubsystemRegistration hooks, and of the methods they call in the same file.
    private static string ResetCode(string source)
    {
        var code = new List<string>();
        int at = 0;
        while ((at = source.IndexOf(HookAttribute, at, System.StringComparison.Ordinal)) >= 0)
        {
            at += HookAttribute.Length;
            code.Add(MethodBody(source, source.IndexOf(')', at) + 1));
        }

        string hooks = string.Join("\n", code);
        foreach (Match call in Regex.Matches(hooks, @"\b(\w+)\(\)"))
        {
            Match declaration = Regex.Match(source, @"\bvoid " + call.Groups[1].Value + @"\(\)");
            if (declaration.Success)
            {
                code.Add(MethodBody(source, declaration.Index + declaration.Length));
            }
        }

        return string.Join("\n", code);
    }

    // The body after a method's parameter list: "=> ...;" or a braced block.
    private static string MethodBody(string source, int from)
    {
        int start = from;
        while (start < source.Length && char.IsWhiteSpace(source[start]))
        {
            start++;
        }

        if (start < source.Length && source[start] == '=')
        {
            return source.Substring(start, source.IndexOf(';', start) - start + 1);
        }

        int depth = 0;
        for (int i = start; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0 && source[i] == '}')
            {
                return source.Substring(start, i - start + 1);
            }
        }

        return string.Empty;
    }

    private static string TypeAt(string source, int position, string path)
    {
        string type = null;
        foreach (Match declaration in TypeDeclaration.Matches(source))
        {
            if (declaration.Index > position)
            {
                break;
            }

            type = declaration.Groups[1].Value;
        }

        return type ?? Path.GetFileNameWithoutExtension(path).Split('.')[0];
    }

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
        string[] offenders = RuntimeScripts()
            .Where(script => Singleton.IsMatch(script.source) && !Regex.IsMatch(ResetCode(script.source), @"\bCurrent\s*=\s*null"))
            .Select(script => script.path).ToArray();
        Assert.IsEmpty(offenders, "Current isn't set to null when Play starts:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void EveryStaticCollection_IsClearedWhenPlayStarts()
    {
        // Registries of live objects, caches of made meshes and pools: left from the last session, they hold destroyed objects.
        var offenders = new List<string>();
        foreach ((string path, string source) in RuntimeScripts())
        {
            string reset = null;
            foreach (Match collection in StaticCollection.Matches(source))
            {
                string name = collection.Groups[1].Value;
                if (KeptCollections.ContainsKey($"{TypeAt(source, collection.Index, path)}.{name}"))
                {
                    continue;
                }

                reset ??= ResetCode(source);
                if (!Regex.IsMatch(reset, @"\b" + name + @"\.Clear\(\)"))
                {
                    offenders.Add($"{path}: {name}");
                }
            }
        }

        Assert.IsEmpty(offenders, "Not cleared when Play starts:\n" + string.Join("\n", offenders));
    }

    // Guard 4 of Docs/BugPatterns.md: a serialized field that starts at -1 means "the default" (or "off"), and is never handed out as a
    // number: its getter gives a nullable, or the rule that resolves it. Attack Definition › Stamina Cost was read as a cost of -1
    // (Frenzied restored nothing, the Fight card showed no swings).
    private static readonly Regex PlaceholderField = new Regex(@"\[SerializeField[^\]]*\]\s*private\s+(?:float|int)\s+(\w+)\s*=\s*-1f?\s*;");

    [Test]
    public void APlaceholderValue_IsNeverHandedOutAsANumber()
    {
        var raw = new List<string>();
        foreach ((string path, string source) in RuntimeScripts())
        {
            foreach (Match field in PlaceholderField.Matches(source))
            {
                string name = field.Groups[1].Value;
                if (Regex.IsMatch(source, @"=>\s*" + name + @"\s*;") || Regex.IsMatch(source, @"return\s+" + name + @"\s*;"))
                {
                    raw.Add($"{Path.GetFileNameWithoutExtension(path)}.{name}");
                }
            }
        }

        CollectionAssert.IsEmpty(raw, "these hand out a -1 placeholder as a number: give a nullable (float? Own...) or the resolved value");
    }

    // Guard 7 of Docs/BugPatterns.md: a player's blow from the shadows (a sure critical, Ambush Damage) is worked out in PlayerBlows for
    // every way it lands, so no path forgets one (thrown weapons had neither; the takedown estimate left out Ambush Damage).
    [Test]
    public void BlowsFromTheShadows_AreWorkedOutInPlayerBlows()
    {
        var direct = new List<string>();
        foreach ((string path, string source) in RuntimeScripts())
        {
            if (path.EndsWith("/PlayerBlows.cs") || path.EndsWith("/PlayerProficiencies.cs"))
            {
                continue;
            }

            if (Regex.IsMatch(source, @"PlayerProficiencies\.(?:CaughtUnaware|AmbushScale)\s*\("))
            {
                direct.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        CollectionAssert.IsEmpty(direct, "ask PlayerBlows (FromTheShadows, CatchesUnaware, UnawareBlowWorth) instead");
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
            events.AddRange(StaticEvent.Matches(source).Cast<Match>().Select(match => $"{TypeAt(source, match.Index, path)}.{match.Groups[1].Value}"));
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

    [Test]
    public void TheResetCheck_ReadsHookBodies()
    {
        const string source = @"
public sealed class Example : MonoBehaviour
{
    private static readonly List<int> kept = new List<int>();
    public static Example Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Clean();

    private static void Clean()
    {
        Current = null;
        kept.Clear();
    }
}";
        string reset = ResetCode(source);
        StringAssert.Contains("Current = null", reset, "Through the method the hook calls");
        StringAssert.Contains("kept.Clear()", reset);
        Assert.AreEqual("Example", TypeAt(source, source.IndexOf("kept", System.StringComparison.Ordinal), "Other.cs"));
        StringAssert.DoesNotContain("Current = null", ResetCode(source.Replace("        Current = null;\n", "")));
    }
}
