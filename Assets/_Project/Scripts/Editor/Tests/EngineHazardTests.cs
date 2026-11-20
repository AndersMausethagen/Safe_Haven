using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// The engine's own traps, checked over every runtime script at once (Docs/BugPatterns.md, third round): what the compiler accepts and a
/// reader misses, found by kind rather than one bug at a time. Each rule reads the source with its comments and strings taken out, and
/// the game's types by reflection where a name alone can't tell (a static's type, whether a field holds a Unity object). What a rule
/// allows on purpose is listed beside it with its reason.
/// <list type="bullet">
/// <item>State kept in a static outlives a Play session (this project enters Play without reloading scripts): reset when Play starts.</item>
/// <item>A Unity object is null-checked with == (the engine's own null), never with ?. ?? or "is null", which miss a destroyed object
/// and, in the Editor, an empty Inspector slot or a missing GetComponent.</item>
/// <item>Whatever subscribes to a system that outlives it (reached through Current, Active or GameSession) unsubscribes.</item>
/// <item>A mesh, texture, material or render target made in code is destroyed by what made it, or handed to an owner that does.</item>
/// <item>A renderer feature's Create runs on every change to the renderer: its material is made once.</item>
/// <item>Every reference in the scenes, prefabs and data points at an asset that still exists.</item>
/// </list>
/// </summary>
public sealed class EngineHazardTests
{
    private const string ScriptsRoot = "Assets/_Project/Scripts";

    // ---------------------------------------------------------------- reading the source

    private static List<(string path, string source)> scripts;
    private static Dictionary<string, string> sourceByType;
    private static HashSet<string> writtenFromOutside;

    // The runtime scripts, comments and the insides of strings taken out (a word in a message is not code).
    private static List<(string path, string source)> Scripts()
    {
        if (scripts == null)
        {
            scripts = new List<(string, string)>();
            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!path.Contains("/Editor/"))
                {
                    scripts.Add((path, CodeOnly(File.ReadAllText(path))));
                }
            }
        }

        // A guard's canary (GuardCanaryTests) adds its bad case for the length of one run.
        return ConventionTests.Planted.Count == 0
            ? scripts
            : scripts.Concat(ConventionTests.Planted.Select(planted => (planted.path, CodeOnly(planted.source)))).ToList();
    }

    /// <summary>The code of a C# source: comments gone, string and character literals emptied (their quotes kept).</summary>
    internal static string CodeOnly(string source)
    {
        var code = new StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && next == '*')
            {
                int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 2;
                code.Append(' ');
            }
            else if (c == '"' || (c == '@' && (next == '"' || next == '$')) || (c == '$' && (next == '"' || next == '@')))
            {
                bool verbatim = c == '@' || (c == '$' && next == '@');
                while (source[i] != '"')
                {
                    i++;
                }

                i++;
                while (i < source.Length)
                {
                    if (!verbatim && source[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (source[i] == '"')
                    {
                        if (verbatim && i + 1 < source.Length && source[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    if (!verbatim && source[i] == '\n')
                    {
                        break;
                    }

                    i++;
                }

                i++;
                code.Append("\"\"");
            }
            else if (c == '\'' && i + 2 < source.Length && (source[i + 2] == '\'' || (source[i + 1] == '\\' && i + 3 < source.Length)))
            {
                int end = source.IndexOf('\'', i + (source[i + 1] == '\\' ? 3 : 2));
                i = end < 0 ? i + 1 : end + 1;
                code.Append("' '");
            }
            else
            {
                code.Append(c);
                i++;
            }
        }

        return code.ToString();
    }

    private static readonly Regex TypeDeclaration = new Regex(@"\b(?:class|struct|interface)\s+(\w+)");

    // The source of every file declaring a type of this name (a partial class's parts together).
    private static string SourceOf(Type type)
    {
        if (sourceByType == null)
        {
            var parts = new Dictionary<string, List<string>>();
            foreach ((string _, string source) in Scripts())
            {
                foreach (string name in TypeDeclaration.Matches(source).Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
                {
                    if (!parts.TryGetValue(name, out List<string> list))
                    {
                        parts[name] = list = new List<string>();
                    }

                    list.Add(source);
                }
            }

            sourceByType = parts.ToDictionary(pair => pair.Key, pair => string.Join("\n", pair.Value));
        }

        return sourceByType.TryGetValue(PlainName(type), out string found) ? found : null;
    }

    private static Dictionary<string, string> bodyByType;

    // The braces of every declaration of a type of this name (a partial class's parts together; its nested types inside).
    private static string BodyOf(Type type)
    {
        if (bodyByType == null)
        {
            var parts = new Dictionary<string, StringBuilder>();
            foreach ((string _, string source) in Scripts())
            {
                foreach (Match declaration in TypeDeclaration.Matches(source))
                {
                    int open = source.IndexOf('{', declaration.Index);
                    int semicolon = source.IndexOf(';', declaration.Index);
                    if (open < 0 || (semicolon >= 0 && semicolon < open))
                    {
                        continue;
                    }

                    string body = ConventionTests.MethodBody(source, open);
                    string name = declaration.Groups[1].Value;
                    if (!parts.TryGetValue(name, out StringBuilder all))
                    {
                        parts[name] = all = new StringBuilder();
                    }

                    all.Append(body).Append('\n');
                }
            }

            bodyByType = parts.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        }

        return bodyByType.TryGetValue(PlainName(type), out string found) ? found : null;
    }

    private static string PlainName(Type type)
    {
        int tick = type.Name.IndexOf('`');
        return tick < 0 ? type.Name : type.Name.Substring(0, tick);
    }

    private static IEnumerable<Type> GameTypes()
    {
        Assembly game = typeof(GameSession).Assembly;
        return game.GetTypes().Where(type => !type.Name.Contains("<") && type.GetCustomAttribute<CompilerGeneratedAttribute>() == null);
    }

    // ---------------------------------------------------------------- 1. statics reset when Play starts

    // Statics that keep their value from one Play session to the next on purpose, and why.
    private static readonly Dictionary<string, string> KeptStatics = new Dictionary<string, string>
    {
        ["InteractableGrid.stamp"] = "a pass counter members compare with: back at 0 it would match members never stamped",
        ["InputDevices.Changes"] = "a version readers compare with what they last saw: it only ever goes up",
        ["InputRebinds.Changes"] = "a version readers compare with what they last saw: it only ever goes up",
        ["SceneTransition.generation"] = "only goes up: a running step compares it with the one it started under",
        ["GrassInteractor.sortEye"] = "set just before the sort that reads it",
        ["TerrainFootprints.sortEye"] = "set just before the sort that reads it",
        ["TreeBender.sortEye"] = "set just before the sort that reads it",
        ["DayNightCycle.sampleDirections"] = "worked out once from constants",
        ["DayNightCycle.sampleWeight"] = "worked out once from constants",
        ["CharacterGestures.speakingTurn"] = "which talking gesture comes next: any start is as good",
        ["ChainBoltEffect.leaping"] = "a guard against a leap's own hit leaping again, set and cleared in one call (try/finally)",
        ["DeathBurstEffect.bursting"] = "a guard against a burst's own kills bursting again, set and cleared in one call (try/finally)",
        ["GraphicsOptions.dlssAvailable"] = "what this machine's graphics card can do, asked once",
        ["GraphicsOptions.fsr2Available"] = "what this machine's graphics card can do, asked once",
        ["FishInTheWater.baseShape"] = "the vertices of the one shared fish mesh, read once",
        ["HavenDecisions.decisions"] = "the decisions found once by reflection over the code, which doesn't change in a session",
        // An [ExecuteAlways] registry may be reset too: entering Play here calls OnEnable again on the open scene's objects (measured
        // 2026-11-18 in Guardian's Hollow: mist, terrain mist, weather and ground fog all registered again after their resets).
    };

    // A static holding an asset made or loaded once (a material, a mesh, a sprite, a settings asset): made again when the engine has
    // destroyed it (== null), so it may outlive a session. Components and GameObjects are the scene's, and go with it.
    private static bool MadeOnce(Type type)
    {
        if (type.IsArray)
        {
            return MadeOnce(type.GetElementType());
        }

        if (type.IsGenericParameter)
        {
            return type.GetGenericParameterConstraints().Any(MadeOnce);
        }

        return typeof(Object).IsAssignableFrom(type) && !typeof(Component).IsAssignableFrom(type) && type != typeof(GameObject);
    }

    private static string StaticName(FieldInfo field)
    {
        Match backing = Regex.Match(field.Name, @"^<(\w+)>k__BackingField$");
        if (backing.Success)
        {
            return backing.Groups[1].Value;
        }

        return field.Name.Contains("<") ? null : field.Name;
    }

    private static string Writes(string name) =>
        $@"(?<![\w.]){name}\s*(?:=(?![=>])|\+=|-=|\*=|/=|\|=|&=|\?\?=|\+\+|--)|(?:\+\+|--){name}\b|\b(?:ref|out)\s+{name}\b" +
        $@"|(?<![\w.]){name}\s*\[[^\]\n]*\]\s*=(?!=)|(?<![\w.]){name}\.(?:Add|Clear|Remove|Insert|Enqueue|Push|Dequeue|Pop|UnionWith|ExceptWith)\w*\(";

    private static bool IsWritten(Type type, string source, string name)
    {
        // Its declaration (with any first value) is not a write.
        string body = Regex.Replace(source, $@"\bstatic\b[^;{{}}()\n]*?\b{name}\b\s*(?:\{{[^}}]*\}}\s*(?:=[^;]*;)?|=[^;]*;|;)", " ");
        if (Regex.IsMatch(body, Writes(name)))
        {
            return true;
        }

        if (writtenFromOutside == null)
        {
            writtenFromOutside = new HashSet<string>();
            foreach ((string _, string script) in Scripts())
            {
                foreach (Match write in Regex.Matches(script, @"\b(\w+)\.(\w+)\s*(?:=(?![=>])|\+=|-=|\|=|\+\+|--)"))
                {
                    writtenFromOutside.Add(write.Groups[1].Value + "." + write.Groups[2].Value);
                }
            }
        }

        return writtenFromOutside.Contains(PlainName(type) + "." + name);
    }

    [Test]
    public void EveryStaticState_IsResetWhenPlayStarts()
    {
        var offenders = new List<string>();
        int checkedStatics = 0;
        foreach (Type type in GameTypes())
        {
            FieldInfo[] statics = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (statics.Length == 0)
            {
                continue;
            }

            string source = SourceOf(type);
            if (source == null)
            {
                continue;
            }

            string reset = null;
            foreach (FieldInfo field in statics)
            {
                string name = StaticName(field);
                if (field.IsLiteral || field.IsInitOnly || name == null || MadeOnce(field.FieldType)
                    || typeof(Delegate).IsAssignableFrom(field.FieldType) && type.GetEvent(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null
                    || KeptStatics.ContainsKey($"{PlainName(type)}.{name}"))
                {
                    continue;
                }

                checkedStatics++;
                if (!IsWritten(type, source, name))
                {
                    continue;
                }

                reset ??= ConventionTests.ResetCode(source);
                if (!Regex.IsMatch(reset, $@"(?<![\w.]){name}\s*(?:=(?!=)|\.Clear\(\))"))
                {
                    offenders.Add($"{PlainName(type)}.{name} ({field.FieldType.Name})");
                }
            }
        }

        Assert.Greater(checkedStatics, 100, "the scan found the statics");
        Assert.IsEmpty(offenders, "Kept from the last Play session (set them in a SubsystemRegistration hook, or list why in KeptStatics):\n"
            + string.Join("\n", offenders.OrderBy(o => o)));
    }

    [Test]
    public void TheStaticScan_SeesWritesAndResets()
    {
        const string source = @"
public sealed class Example
{
    private static int count = 3;
    private static bool open;
    public static float Share { get; private set; } = 1f;
    private static string never = """";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => open = false;

    public void Use() { count++; open = true; Share = 0.5f; Debug.Log(never); }
}";
        Assert.IsTrue(IsWritten(typeof(EngineHazardTests), source, "count"));
        Assert.IsTrue(IsWritten(typeof(EngineHazardTests), source, "Share"));
        Assert.IsFalse(IsWritten(typeof(EngineHazardTests), source, "never"), "its declaration is not a write");
        string reset = ConventionTests.ResetCode(source);
        StringAssert.Contains("open = false", reset);
        StringAssert.DoesNotContain("count", reset);
    }

    // ---------------------------------------------------------------- 2. Unity objects null-checked with ==

    // A Unity object's field or property whose ?. or ?? is safe, and why.
    private static readonly Dictionary<string, string> PlainNullChecks = new Dictionary<string, string>
    {
        ["AudioManager.Sounds"] = "the audio settings asset, never destroyed in play; without a manager it is C#'s own null (Current != null ? ... : null), and a deleted asset is caught by EveryReference_InScenesPrefabsAndData_PointsAtAnAssetThatExists",
    };

    private static readonly BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static IEnumerable<string> UnityObjectMembers(Type type)
    {
        foreach (FieldInfo field in type.GetFields(Members))
        {
            if (typeof(Object).IsAssignableFrom(field.FieldType) && StaticName(field) is string name)
            {
                yield return name;
            }
        }

        foreach (PropertyInfo property in type.GetProperties(Members))
        {
            if (typeof(Object).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0)
            {
                yield return property.Name;
            }
        }
    }

    // GetComponent and its kin hand back the engine's null; in the Editor a missing component is a stand-in object that ?. calls into.
    private static readonly Regex ComponentLookupQuestioned = new Regex(@"\bGetComponent(?:InChildren|InParent)?\s*<[^>]+>\s*\([^()]*\)\s*(?:\?\.|\?\?)");

    [Test]
    public void UnityObjects_AreNullCheckedWithTheEnginesNull()
    {
        var offenders = new List<string>();
        foreach (Type type in GameTypes())
        {
            string[] members = UnityObjectMembers(type).Distinct().ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            string source = BodyOf(type);
            if (source == null)
            {
                continue;
            }

            foreach (string name in members)
            {
                if (PlainNullChecks.ContainsKey($"{PlainName(type)}.{name}"))
                {
                    continue;
                }

                foreach (Match plain in Regex.Matches(source, $@"(?<![\w.]){name}\s*(?:\?\.|\?\?(?!=)|\?\?=|\bis\s+(?:not\s+)?null\b)|(?<![\w.]){name}\s*\?\["))
                {
                    offenders.Add($"{PlainName(type)}.{name}: {plain.Value.Trim()}");
                }
            }
        }

        // A type's static Unity objects reached from anywhere (AudioManager.Current?.Play...).
        var reached = new HashSet<string>();
        foreach (Type type in GameTypes())
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (typeof(Object).IsAssignableFrom(field.FieldType) && StaticName(field) is string name)
                {
                    reached.Add($"{PlainName(type)}.{name}");
                }
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (typeof(Object).IsAssignableFrom(property.PropertyType))
                {
                    reached.Add($"{PlainName(type)}.{property.Name}");
                }
            }
        }

        var staticQuestioned = new Regex(@"\b([A-Z]\w*\.[A-Z]\w*)\s*(?:\?\.|\?\?)");
        foreach ((string path, string source) in Scripts())
        {
            foreach (Match lookup in ComponentLookupQuestioned.Matches(source))
            {
                offenders.Add($"{Path.GetFileName(path)}: {lookup.Value.Trim()}");
            }

            foreach (Match use in staticQuestioned.Matches(source))
            {
                if (reached.Contains(use.Groups[1].Value) && !PlainNullChecks.ContainsKey(use.Groups[1].Value))
                {
                    offenders.Add($"{Path.GetFileName(path)}: {use.Value.Trim()}");
                }
            }
        }

        Assert.IsEmpty(offenders.Distinct().ToList(),
            "A Unity object checked with ?. ?? or 'is null' (use == null, or TryGetComponent; list why in PlainNullChecks if it is safe):\n"
            + string.Join("\n", offenders.Distinct().OrderBy(o => o)));
    }

    // ---------------------------------------------------------------- 3. subscriptions to longer-lived systems undone

    private static readonly Regex SystemSubscription =
        new Regex(@"((?:\w+\.)*(?:Current|Active|GameSession\.\w+|Player\.Current\.\w+))\??\.(\w+)\s*\+=\s*([\w.]+)\s*;");

    [Test]
    public void EverySubscriptionToALongerLivedSystem_IsUndone()
    {
        var offenders = new List<string>();
        int seen = 0;
        foreach ((string path, string source) in Scripts())
        {
            foreach (Match subscription in SystemSubscription.Matches(source))
            {
                seen++;
                string undone = $@"\.{subscription.Groups[2].Value}\s*-=\s*{Regex.Escape(subscription.Groups[3].Value)}\s*;";
                if (!Regex.IsMatch(source, undone))
                {
                    offenders.Add($"{Path.GetFileName(path)}: {subscription.Value.Trim()}");
                }
            }
        }

        Assert.Greater(seen, 4, "the scan found the subscriptions");
        Assert.IsEmpty(offenders, "Never unsubscribed: the system outlives this, and calls a destroyed object next time:\n" + string.Join("\n", offenders));
    }

    // ---------------------------------------------------------------- 4. made objects destroyed

    // Reading a renderer's material(s) makes a copy too, the renderer's own (the test audit of 2026-11-18: the rule saw only `new`).
    private static readonly Regex Making =
        new Regex(@"\bnew\s+(Material|Mesh|Texture2D|Texture3D|Texture2DArray|RenderTexture|Cubemap|ComputeBuffer|GraphicsBuffer)\s*[({]|\bCoreUtils\.CreateEngineMaterial\s*\(|\bSprite\.Create\s*\(" +
            @"|\b\w*[Rr]enderer\w*\s*\.\s*materials?\b(?!\s*=[^=])");

    private static readonly Regex LettingGo =
        new Regex(@"\bDestroy(?:Immediate)?\s*\(|\bCoreUtils\.Destroy\s*\(|\.Release\s*\(\s*\)|\.Dispose\s*\(\s*\)|ReleaseTemporary\s*\(");

    // A factory hands what it makes to the caller (a method returning one); a static keeps one made once.
    private static readonly Regex Factory = new Regex(@"\b(?:Material|Mesh|Texture2D|Texture3D|Texture2DArray|RenderTexture|Sprite|Texture)\s+\w+\s*\(");
    private static readonly Regex StaticKeeper = new Regex(@"\bstatic\s+(?:Material|Mesh|Texture2D|RenderTexture|Sprite)(?:\[\])?\s+\w+\s*[;=]");

    // Files that make and hand what they made to an owner other than a returned value, and who destroys it.
    private static readonly Dictionary<string, string> HandedOver = new Dictionary<string, string>
    {
        ["RaidLilyPads.cs"] = "adds the meshes it made to the list it is given: the raid map destroys them (RaidMapGenerator.LilyPads)",
        // Destroying them with the character drew severed and smashed pieces with destroyed materials (the GPU Resident Drawer's
        // "invalid Material ID", found by SceneSmokeLiveTests in a rainy Moss Marsh).
        ["CharacterWetness.cs"] = "a death ragdoll's pieces take the wet copies (DeathRagdoll) and may outlive the character: the scene's unload frees them",
        // Open (Docs/BugPatterns.md, sixth round): property blocks can't switch Glow's emission keyword or keep Tint's multiplying, so the
        // change needs its own look in the game.
        ["CreatureLook.cs"] = "a creature's renderer.materials copies, freed only when its scene goes: OPEN, to be replaced with a look in the game",
    };

    [Test]
    public void WhatIsMadeInCode_IsDestroyedByItsOwner()
    {
        var offenders = new List<string>();
        foreach ((string path, string source) in Scripts())
        {
            if (!Making.IsMatch(source) || LettingGo.IsMatch(source) || Factory.IsMatch(source) || StaticKeeper.IsMatch(source)
                || HandedOver.ContainsKey(Path.GetFileName(path)))
            {
                continue;
            }

            offenders.Add($"{Path.GetFileName(path)}: {string.Join(", ", Making.Matches(source).Cast<Match>().Select(m => m.Value.Trim()).Distinct())}");
        }

        Assert.IsEmpty(offenders, "Made and never destroyed (it stays in memory until the next scene):\n" + string.Join("\n", offenders));
    }

    // ---------------------------------------------------------------- 5. renderer features make their material once

    [Test]
    public void EveryRendererFeature_MakesItsMaterialOnce()
    {
        var offenders = new List<string>();
        int features = 0;
        foreach (Type type in GameTypes().Where(t => typeof(ScriptableRendererFeature).IsAssignableFrom(t) && !t.IsAbstract))
        {
            features++;
            string source = SourceOf(type);
            Match create = Regex.Match(source ?? "", @"\boverride\s+void\s+Create\s*\(\s*\)");
            if (!create.Success)
            {
                continue;
            }

            string body = ConventionTests.MethodBody(source, create.Index + create.Length);
            foreach (Match making in Regex.Matches(body, @"(\w+)\s*=\s*(?:CoreUtils\.CreateEngineMaterial|new\s+Material)\s*\("))
            {
                // The field it is kept in is asked first: "if (material == null || ...)" (an early "shader == null" doesn't count).
                string field = making.Groups[1].Value;
                if (!Regex.IsMatch(body.Substring(0, making.Index), $@"\b{field}\s*==\s*null"))
                {
                    offenders.Add($"{type.Name}.{field}");
                }
            }
        }

        Assert.Greater(features, 3, "the scan found the renderer features");
        Assert.IsEmpty(offenders, "Create runs again on every change to the renderer: make the material only when there is none:\n" + string.Join("\n", offenders));
    }

    // ---------------------------------------------------------------- 7. names in strings name something that exists

    private static readonly Regex ResourcePath = new Regex(@"\bResources\.Load(?:Async|All)?\s*(?:<\w+>)?\s*\(\s*""([^""]+)""");
    private static readonly Regex ShaderName = new Regex(@"\bShader\.Find\s*\(\s*""([^""]+)""");
    private static readonly Regex AnimatorName = new Regex(
        @"(?:\b[aA]nim\w*\??\.(?:SetTrigger|ResetTrigger|SetBool|SetFloat|SetInteger|GetBool|GetFloat|GetInteger|Play|CrossFade\w*)|\bAnimator\.StringToHash)\s*\(\s*""([^""]+)""");
    private static readonly Regex TagName = new Regex(@"\b(?:CompareTag|FindWithTag|FindGameObjectsWithTag|FindGameObjectWithTag)\s*\(\s*""([^""]+)""");
    private static readonly Regex LayerNames = new Regex(@"\b(?:NameToLayer|GetMask)\s*\(([^)]*)\)");
    private static readonly Regex ActionName = new Regex(@"\bFindAction\s*\(\s*""([^""]+)""");
    private static readonly Regex SceneName = new Regex(@"\bLoadScene(?:Async)?\s*\(\s*""([^""]+)""");
    private static readonly Regex NamedMethod = new Regex(@"\b(?:Invoke|InvokeRepeating|CancelInvoke|StartCoroutine|StopCoroutine|SendMessage|BroadcastMessage)\s*\(\s*""(\w+)""");

    // The strings are read from the raw source (CodeOnly empties them): line comments are dropped first.
    private static IEnumerable<(string path, string source)> ScriptsWithStrings()
    {
        foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (!path.Contains("/Editor/"))
            {
                yield return (path, Regex.Replace(File.ReadAllText(path), @"(?m)^\s*//.*$|(?<=[;{}])\s*//[^\n""]*$", ""));
            }
        }
    }

    [Test]
    public void EveryNameInAString_NamesSomethingThatExists()
    {
        var animatorNames = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" }))
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            if (controller == null)
            {
                continue;
            }

            animatorNames.UnionWith(controller.parameters.Select(p => p.name));
            foreach (UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers)
            {
                animatorNames.Add(layer.name);
                Collect(layer.stateMachine, animatorNames);
            }
        }

        var tags = new HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);
        var scenes = new HashSet<string>(EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)));
        var actions = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:InputActionAsset", new[] { "Assets" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null)
            {
                continue;
            }

            foreach (UnityEngine.InputSystem.InputActionMap map in asset.actionMaps)
            {
                actions.Add(map.name);
                foreach (UnityEngine.InputSystem.InputAction action in map.actions)
                {
                    actions.Add(action.name);
                    actions.Add(map.name + "/" + action.name);
                }
            }
        }

        var missing = new List<string>();
        int names = 0;
        foreach ((string path, string source) in ScriptsWithStrings())
        {
            string file = Path.GetFileName(path);
            void Check(Regex pattern, Func<string, bool> exists, string what)
            {
                foreach (Match match in pattern.Matches(source))
                {
                    names++;
                    if (!exists(match.Groups[1].Value))
                    {
                        missing.Add($"{file}: {what} \"{match.Groups[1].Value}\"");
                    }
                }
            }

            // A path ending in "/" is a folder the rest of the name is added to.
            Check(ResourcePath, p => p.EndsWith("/") ? Resources.LoadAll(p.TrimEnd('/')).Length > 0 : Resources.Load(p) != null, "nothing in a Resources folder at");
            Check(ShaderName, ShaderShips, "no shader in a build called (in the Editor Shader.Find finds every one)");
            Check(AnimatorName, n => animatorNames.Contains(n) || animatorNames.Contains(n.Split('.').Last()), "no animator state or parameter called");
            Check(TagName, tags.Contains, "no tag called (CompareTag throws)");
            Check(ActionName, actions.Contains, "no input action called");
            Check(SceneName, scenes.Contains, "no scene in the build called");
            foreach (Match layers in LayerNames.Matches(source))
            {
                foreach (Match layer in Regex.Matches(layers.Groups[1].Value, @"""([^""]+)"""))
                {
                    names++;
                    if (LayerMask.NameToLayer(layer.Groups[1].Value) < 0)
                    {
                        missing.Add($"{file}: no layer called \"{layer.Groups[1].Value}\"");
                    }
                }
            }

            foreach (Match call in NamedMethod.Matches(source))
            {
                names++;
                if (!Regex.IsMatch(source, $@"\b{call.Groups[1].Value}\s*\([^)]*\)\s*(?:\{{|=>)"))
                {
                    missing.Add($"{file}: no method called \"{call.Groups[1].Value}\" for {call.Value.Split('(')[0].Trim()}");
                }
            }
        }

        Assert.Greater(names, 100, "the scan found the names");
        Assert.IsEmpty(missing, "These name nothing (renamed or deleted?), and fail without a word at run time:\n" + string.Join("\n", missing.Distinct()));
    }

    private static string shipped;

    // A build has a shader of ours when it is always included, sits in a Resources folder, or is named by something that ships: a
    // material, the renderer's feature slots (Settings), a settings asset in Resources. The pipeline's and the engine's own count.
    private static bool ShaderShips(string name)
    {
        Shader shader = Shader.Find(name);
        if (shader == null)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(shader);
        if (!path.StartsWith("Assets/") || path.Contains("/Resources/"))
        {
            return true;
        }

        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        SerializedProperty always = graphics.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < always.arraySize; i++)
        {
            if (always.GetArrayElementAtIndex(i).objectReferenceValue == shader)
            {
                return true;
            }
        }

        if (shipped == null)
        {
            var text = new StringBuilder();
            foreach (string file in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                string at = file.Replace('\\', '/');
                bool ships = at.EndsWith(".mat") || ((at.Contains("/Resources/") || at.StartsWith("Assets/_Project/Settings/")) && at.EndsWith(".asset"));
                if (ships && new FileInfo(at).Length < 20_000_000)
                {
                    text.Append(File.ReadAllText(at));
                }
            }

            shipped = text.ToString();
        }

        return shipped.Contains("guid: " + AssetDatabase.AssetPathToGUID(path));
    }

    private static void Collect(UnityEditor.Animations.AnimatorStateMachine machine, HashSet<string> into)
    {
        into.Add(machine.name);
        foreach (UnityEditor.Animations.ChildAnimatorState state in machine.states)
        {
            into.Add(state.state.name);
            // A state's tag is looked up by name too (AnimatorStateInfo.IsTag / tagHash).
            if (!string.IsNullOrEmpty(state.state.tag))
            {
                into.Add(state.state.tag);
            }
        }

        foreach (UnityEditor.Animations.ChildAnimatorStateMachine child in machine.stateMachines)
        {
            Collect(child.stateMachine, into);
        }
    }

    // ---------------------------------------------------------------- 8. what the raid's navmesh is built from can be read

    // A raid's navmesh is built as it loads, from the physics colliders (RaidMap.RebuildNavMesh). A mesh collider's mesh that isn't
    // readable "will work in play mode in the Editor but not in the player": in a build the creatures' ground lost every such rock,
    // stump and ore node, and the bridges and boardwalks walked on (found by SceneSmokeLiveTests: 55 models). Read/Write on the models
    // would double their render meshes' memory (ModelBudgetTests), so the colliders alone are copied (NavMeshColliderCopies).
    [Test]
    public void EveryMeshCollidersMesh_CanBeReadByTheRaidsNavMesh()
    {
        List<Mesh> unreadable = NavMeshColliderCopiesBuilder.Unreadable();
        var copies = AssetDatabase.LoadAssetAtPath<NavMeshColliderCopies>(NavMeshColliderCopiesBuilder.AssetPath);
        Assert.IsNotNull(copies, NavMeshColliderCopiesBuilder.AssetPath);
        Assert.AreSame(copies, Resources.Load<NavMeshColliderCopies>(NavMeshColliderCopies.ResourceName), "the game finds it in Resources");
        Assert.Greater(unreadable.Count, 20, "the scan found the colliders' meshes");
        var stale = new List<string>();
        foreach (Mesh mesh in unreadable)
        {
            NavMeshColliderCopies.Copy wanted = NavMeshColliderCopiesBuilder.CopyOf(mesh);
            if (!copies.TryGet(mesh, out NavMeshColliderCopies.Copy copy) || !copy.vertices.SequenceEqual(wanted.vertices)
                || !copy.indices.SequenceEqual(wanted.indices))
            {
                stale.Add($"{AssetDatabase.GetAssetPath(mesh)} ({mesh.name})");
            }
        }

        Assert.IsEmpty(stale, "Missing or out of date: run Safe Haven › Navigation › Gather Collider Copies\n" + string.Join("\n", stale));
    }

    [Test]
    public void TheRaidsNavMesh_ReadsTheCopiesInPlaceOfTheColliders()
    {
        Assert.IsTrue(NavMeshColliderCopies.Reach(out _, out _), "NavMeshSurface's own source collection is reached (AI Navigation package)");
        Mesh mesh = NavMeshColliderCopiesBuilder.Unreadable()[0];
        var sources = new List<UnityEngine.AI.NavMeshBuildSource>
        {
            new UnityEngine.AI.NavMeshBuildSource { shape = UnityEngine.AI.NavMeshBuildSourceShape.Mesh, sourceObject = mesh, transform = Matrix4x4.identity },
            new UnityEngine.AI.NavMeshBuildSource { shape = UnityEngine.AI.NavMeshBuildSourceShape.Box, size = Vector3.one, transform = Matrix4x4.identity },
        };
        int swapped = NavMeshColliderCopies.Swap(sources, NavMeshColliderCopies.Current, out int missing);
        Assert.AreEqual(1, swapped);
        Assert.AreEqual(0, missing);
        var readable = (Mesh)sources[0].sourceObject;
        Assert.IsTrue(readable.isReadable);
        Assert.AreEqual(mesh.vertexCount, readable.vertexCount, "the same shape");
        Assert.AreEqual(UnityEngine.AI.NavMeshBuildSourceShape.Box, sources[1].shape, "other sources are left as they are");
        NavMeshColliderCopies.Swap(sources, NavMeshColliderCopies.Current, out _);
        Assert.AreSame(readable, sources[0].sourceObject, "a readable source isn't swapped again");
    }

    // ---------------------------------------------------------------- 6. references point at assets that exist

    private static readonly Regex AssetReference = new Regex(@"guid: ([0-9a-f]{32}), type: \d");

    [Test]
    public void EveryReference_InScenesPrefabsAndData_PointsAtAnAssetThatExists()
    {
        var known = new Dictionary<string, bool>();
        var broken = new List<string>();
        int files = 0;
        foreach (string file in Directory.GetFiles("Assets/_Project", "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (extension != ".unity" && extension != ".prefab" && extension != ".asset" && extension != ".mat" && extension != ".controller"
                && extension != ".overrideController" && extension != ".playable" && extension != ".mixer")
            {
                continue;
            }

            string text;
            using (var reader = new StreamReader(file))
            {
                char[] start = new char[5];
                if (reader.Read(start, 0, 5) < 5 || new string(start) != "%YAML")
                {
                    continue; // binary
                }

                text = reader.ReadToEnd();
            }

            files++;
            var missing = new HashSet<string>();
            foreach (Match reference in AssetReference.Matches(text))
            {
                string guid = reference.Groups[1].Value;
                if (guid.StartsWith("0000000000000000", StringComparison.Ordinal))
                {
                    continue; // the engine's built-in resources
                }

                if (!known.TryGetValue(guid, out bool exists))
                {
                    known[guid] = exists = !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
                }

                if (!exists)
                {
                    missing.Add(guid);
                }
            }

            if (missing.Count > 0)
            {
                broken.Add($"{file.Replace('\\', '/')}: {string.Join(", ", missing.Take(3))}{(missing.Count > 3 ? $" and {missing.Count - 3} more" : "")}");
            }
        }

        Assert.Greater(files, 500, "the scan found the scenes, prefabs and data");
        Assert.IsEmpty(broken, "These point at assets that no longer exist (a missing script, mesh, material or definition):\n" + string.Join("\n", broken));
    }
}
