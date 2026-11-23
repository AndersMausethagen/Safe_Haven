using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guard 1 of Docs/BugPatterns.md: what the game keeps comes back. Every field of the saved state (SettlementState with its
/// residents, fallen, Territory and Workshops, QuestLog, CharacterProgress with its journal) is given a value that isn't its
/// default, the game is saved as on disk (SaveMapper.ToSaveData, through JsonUtility) and loaded (FromSaveData), and each must
/// come back equal. A field left out on purpose says so with [NotSaved("why")]; one the probe can't fill is checked by the tests
/// named in <see cref="CheckedElsewhere"/>. The day's reviews found the stable's clock, the held-quest share, quest rewards
/// waiting for home and the asks' homecoming lost this way, each a field added and forgotten in the mapper.
/// </summary>
public sealed class SaveCoverageTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Saved, but the probe can't fill them: each is covered by the tests named.
    private static readonly Dictionary<string, string> CheckedElsewhere = new Dictionary<string, string>
    {
        ["Quests.entries"] = "QuestTests, QuestPacingTests (status, counts, rewards waiting)",
        ["Settlement.Territory.holds"] = "TerritoryTests.TheLands_SurviveASave",
        ["Settlement.Plunder"] = "DeathRulesTests.TheRulesAndThePlunder_AreSaved_AndOldSavesAreNormal (its leader and items)",
        // People (Docs/PeopleResearch_2026-11-23.md): kit is a real item's stack, passions only Passion's values and standings only
        // pairs of ids, none of which the probe's filler gives; each is round-tripped in its own test.
        ["Settlement.residents[0].kitWeapon"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.residents[0].kitBody"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.residents[0].talents.passions"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.fallen[0].KitWeapon"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.fallen[0].KitBody"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.fallen[0].Talents.passions"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.Standings.values"] = "PeopleTests.WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank",
        ["Settlement.Workshops.queues"] = "StationQueueTests.Workshops_RoundTripThroughSave",
        // Rings are only kept while they hold a radius the probe's filler doesn't give; the round trip is its own test.
        ["Settlement.rings"] = "RingWorkTests.Rings_AreSaved",
        ["Settlement.posts"] = "GuardPostsTests.Posts_AreSaved",
        // Parties are kept only for real kinds, by the land's name: the probe's filler gives neither.
        ["Settlement.parties"] = "LandPartiesTests.Parties_AreSaved",
        // Outposts are kept by the land's name and a resident's id: the probe's filler gives neither.
        ["Settlement.outposts"] = "OutpostsTests.Outposts_AndTheRing_AreSaved",
        // A foray names a real destination and real residents (whom it marks away): the probe's filler gives neither.
        ["Settlement.Foray"] = "ForaysTests.ThoseWhoGo_AreAwayUntilHome_ASaveKeepsThem_AndTheHaulComesHome",
        // Only real perks and skills are kept on loading, and ranks to the deepest there is: a probe can't invent them.
        ["Progress.perks"] = "ProgressionTests, TechniqueTests (perks a save keeps)",
        ["Progress.skillRanks"] = "EssenceTests (skill ranks)",
        // A dictionary of real times the probe can't fill.
        ["Progress.encounterKills"] = "EncounterTests.GatesReachedAndKills_SurviveASave",
        // Stacks of real items the probe can't invent.
        ["Progress.LostOnes.records[0].Carried"] = "LostOneTests.TheRoster_SurvivesASave (what it carries, wear and all)",
    };

    private readonly List<(string root, string path, Func<object, object>[] chain, Func<object, bool> holds)> probes =
        new List<(string, string, Func<object, object>[], Func<object, bool>)>();

    private readonly List<string> unfilled = new List<string>();
    private int counter;

    [Test]
    public void EverySavedField_ComesBack_OrSaysWhyNot()
    {
        var settlement = new SettlementState();
        var quests = new QuestLog();
        var progress = new CharacterProgress();
        Populate(settlement, progress);
        Fill("Settlement", settlement, "Settlement", new Func<object, object>[0], 0);
        Fill("Quests", quests, "Quests", new Func<object, object>[0], 0);
        Fill("Progress", progress, "Progress", new Func<object, object>[0], 0);

        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, DateTime.UtcNow, null, quests, settlement, null, progress);
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        LoadedGame loaded = SaveMapper.FromSaveData(data, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
        var roots = new Dictionary<string, object> { ["Settlement"] = loaded.Settlement, ["Quests"] = loaded.Quests, ["Progress"] = loaded.Progress };

        var lost = new List<string>();
        foreach ((string root, string path, Func<object, object>[] chain, Func<object, bool> holds) in probes)
        {
            object value;
            try
            {
                value = Read(roots[root], chain);
            }
            catch (Exception)
            {
                value = null; // a list that came back empty
            }

            if (!holds(value))
            {
                lost.Add(path);
            }
        }

        var problems = new List<string>();
        if (unfilled.Count > 0)
        {
            problems.Add("No probe for: " + string.Join(", ", unfilled) + ". Give them [NotSaved(\"why\")], a filler here, or a test named in CheckedElsewhere.");
        }

        if (lost.Count > 0)
        {
            problems.Add("Saved and loaded, these didn't come back: " + string.Join(", ", lost) + ". Save them in SaveMapper (both ways), or mark them [NotSaved(\"why\")].");
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // A saved component's state (ISaveable: JSON of its own small struct) with every number and yes/no changed comes back as given:
    // what it writes, it reads. One kind of each component is tried, on the first prefab that carries it, outside any scene.
    private static readonly Dictionary<string, string> ComponentsCheckedElsewhere = new Dictionary<string, string>
    {
    };

    // Values a component writes fresh at each save, not read back: the moment of saving.
    private static readonly HashSet<string> WrittenFresh = new HashSet<string> { "ResourceNode.savedAt" };

    // Values that count the real time between saving and loading (a node regrowing while away): they come back less the seconds the
    // test itself took, so they are compared to within a few seconds.
    private static readonly HashSet<string> CountsTimeAway = new HashSet<string> { "ResourceNode.respawnRemaining" };

    [Test]
    public void EverySavedComponent_ReadsBackWhatItWrites()
    {
        // A state can say the thing is gone (a drop picked up): its Destroy complains outside Play, which is no failure here.
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        var tried = new HashSet<Type>();
        // Read back, not only found: a component whose state isn't JSON is skipped, and counted in `tried` (the test audit of 2026-11-18).
        int probed = 0;
        var lost = new List<string>();
        var couldNot = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<SaveableEntity>(true) == null)
            {
                continue;
            }

            ISaveable[] kinds = asset.GetComponentsInChildren<ISaveable>(true);
            if (kinds.All(kind => tried.Contains(kind.GetType())))
            {
                continue;
            }

            GameObject copy = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (ISaveable saveable in copy.GetComponentsInChildren<ISaveable>(true))
                {
                    Type type = saveable.GetType();
                    if (!tried.Add(type) || ComponentsCheckedElsewhere.ContainsKey(type.Name))
                    {
                        continue;
                    }

                    try
                    {
                        // Outside Play nothing has woken: what a component looks up in its Awake (its health, its body) is looked up
                        // here, for it and the parts it leans on. Their OnEnable, where they join the game's lists, isn't run.
                        if (saveable is Component part)
                        {
                            foreach (Component leanedOn in part.GetComponents<Component>())
                            {
                                if (leanedOn == part || leanedOn is Damageable || leanedOn is Combatant)
                                {
                                    Wake(leanedOn);
                                }
                            }
                        }

                        string written = saveable.CaptureState();
                        if (string.IsNullOrEmpty(written) || !written.TrimStart().StartsWith("{"))
                        {
                            continue;
                        }

                        probed++;
                        string changed = Probed(written, field => WrittenFresh.Contains($"{type.Name}.{field}"));
                        saveable.RestoreState(changed);
                        string back = saveable.CaptureState();
                        foreach (string field in Differences(changed, back, name => CountsTimeAway.Contains($"{type.Name}.{name}") ? 5.0 : 1e-3))
                        {
                            if (!WrittenFresh.Contains($"{type.Name}.{field}"))
                            {
                                lost.Add($"{type.Name}.{field} ({System.IO.Path.GetFileNameWithoutExtension(path)})");
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        couldNot.Add($"{type.Name} on {System.IO.Path.GetFileNameWithoutExtension(path)}: {exception.GetType().Name}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(copy);
            }
        }

        Assert.Greater(tried.Count, 3, "saved components were found to try");
        Assert.Greater(probed, 3, "and that many were written, changed and read back");
        Assert.IsEmpty(couldNot, "these couldn't be saved and restored outside Play: make CaptureState/RestoreState work without Awake, or name the test that covers them in ComponentsCheckedElsewhere");
        Assert.IsEmpty(lost, "restored with these changed, the component saved them differently: read back what CaptureState writes (or clamp on purpose and say so here)");
    }

    private static void Wake(Component component)
    {
        for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            MethodInfo awake = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            if (awake != null)
            {
                awake.Invoke(component, null);
                return;
            }
        }
    }

    // Every number in a JSON state moved (a whole number up by one, a fraction by a half, down when that keeps it within what it was:
    // a full health or a share of 1 stays possible), every yes/no flipped. Strings stay (they are ids, checked against content).
    private static string Probed(string json, Func<string, bool> leave)
    {
        string flipped = System.Text.RegularExpressions.Regex.Replace(json, "\"(\\w+)\":(true|false)", match => leave(match.Groups[1].Value) ? match.Value
            : $"\"{match.Groups[1].Value}\":{(match.Groups[2].Value == "true" ? "false" : "true")}");
        return System.Text.RegularExpressions.Regex.Replace(flipped, "\"(\\w+)\":(-?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?)(?=[,}\\]])", match =>
        {
            if (leave(match.Groups[1].Value))
            {
                return match.Value;
            }

            string number = match.Groups[2].Value;
            bool whole = !number.Contains(".") && !number.Contains("e") && !number.Contains("E");
            double value = double.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
            string probed = whole ? ((long)value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (value >= 0.5 ? value - 0.5 : value + 0.5).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return $"\"{match.Groups[1].Value}\":{probed}";
        });
    }

    // The named values that differ between two flat JSON states, numbers compared as numbers.
    private static IEnumerable<string> Differences(string expected, string actual, Func<string, double> tolerance)
    {
        var pattern = new System.Text.RegularExpressions.Regex("\"(\\w+)\":(-?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?|true|false)");
        Dictionary<string, string> Values(string json)
        {
            var values = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(json))
            {
                values[match.Groups[1].Value] = match.Groups[2].Value;
            }

            return values;
        }

        Dictionary<string, string> want = Values(expected), got = Values(actual);
        foreach (KeyValuePair<string, string> pair in want)
        {
            if (!got.TryGetValue(pair.Key, out string value) || !SameText(pair.Value, value, tolerance(pair.Key)))
            {
                yield return pair.Key;
            }
        }
    }

    private static bool SameText(string a, string b, double tolerance)
    {
        if (a == b)
        {
            return true;
        }

        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        return double.TryParse(a, System.Globalization.NumberStyles.Float, invariant, out double x)
            && double.TryParse(b, System.Globalization.NumberStyles.Float, invariant, out double y) && Math.Abs(x - y) < tolerance;
    }

    // Records made of content can't be invented: one of each is made from real content, and its own fields are then probed.
    private static void Populate(SettlementState settlement, CharacterProgress progress)
    {
        List<ResidentDefinition> people = Assets<ResidentDefinition>().Where(person => !string.IsNullOrEmpty(person.Id)).Take(2).ToList();
        Assert.AreEqual(2, people.Count, "two residents to probe with");
        settlement.AddResident(people[0]);
        settlement.RestoreFallen(people[1], "probe", 1, 1, null, "probe-house");
        JobDefinition job = Assets<JobDefinition>().First(found => !string.IsNullOrEmpty(found.Id));
        typeof(SettlementState).GetField("vacantJobs", Fields).FieldType.GetMethod("Add")
            .Invoke(typeof(SettlementState).GetField("vacantJobs", Fields).GetValue(settlement), new object[] { job });
        progress.Journal.Restore(new List<string>(), new List<string>(), new List<string>(), new List<string>(),
            new List<RaidRecord> { new RaidRecord { Lines = new List<string>() } }, null);
        // One of the Lost, whose own fields are then probed.
        progress.LostOnes.Add(new LostOneRecord());
    }

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(found => found != null);

    private void Fill(string root, object target, string path, Func<object, object>[] chain, int depth)
    {
        foreach (FieldInfo field in target.GetType().GetFields(Fields))
        {
            if (typeof(Delegate).IsAssignableFrom(field.FieldType) || field.IsLiteral || field.GetCustomAttribute<NotSavedAttribute>() != null)
            {
                continue;
            }

            string name = path + "." + field.Name.Replace("<", "").Replace(">k__BackingField", "");
            if (CheckedElsewhere.ContainsKey(name))
            {
                continue;
            }

            Func<object, object>[] here = chain.Concat(new Func<object, object>[] { field.GetValue }).ToArray();
            Type type = field.FieldType;
            object current = field.GetValue(target);
            if (TryValue(type, current, out object probe))
            {
                field.SetValue(target, probe);
                probes.Add((root, name, here, read => Same(read, probe)));
            }
            else if (current is UnityEngine.Object content)
            {
                // A link to content (a resident's definition): it must come back as the same asset.
                probes.Add((root, name, here, read => ReferenceEquals(read, content)));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                // An empty link (a resident without a job): nothing to probe.
            }
            else if (TryElement(type, out object element, out object elementValue))
            {
                if (current == null)
                {
                    unfilled.Add(name + " (null " + type.Name + ")");
                    continue;
                }

                AddTo(current, element, elementValue);
                probes.Add((root, name, here, read => Holds(read, element, elementValue)));
            }
            else if (current is IList list && type.IsGenericType && list.Count > 0 && list[0] is UnityEngine.Object linked)
            {
                probes.Add((root, name, here, read => read is IList back && back.Contains(linked)));
            }
            else if (current is IList records && type.IsGenericType && records.Count > 0 && IsGameType(type.GetGenericArguments()[0]) && depth < 3)
            {
                // The first record's own fields (Populate made one).
                Fill(root, records[0], name + "[0]", here.Concat(new Func<object, object>[] { read => ((IList)read)[0] }).ToArray(), depth + 1);
            }
            else if (current is IDictionary nested && IsListOfStrings(type))
            {
                string key = "probe" + ++counter;
                string value = "probe" + ++counter;
                nested[key] = new List<string> { value };
                probes.Add((root, name, here, read => read is IDictionary back && back.Contains(key) && back[key] is IList values && values.Contains(value)));
            }
            else if (depth < 3 && IsGameType(type) && !(current is IEnumerable))
            {
                object inner = current ?? Activator.CreateInstance(type, true);
                Fill(root, inner, name, here, depth + 1);
                field.SetValue(target, inner); // a struct is filled as a box and written back
            }
            else
            {
                unfilled.Add(name + " (" + type.Name + ")");
            }
        }
    }

    private static bool IsGameType(Type type) =>
        type.Assembly == typeof(SettlementState).Assembly && !typeof(UnityEngine.Object).IsAssignableFrom(type) && (type.IsClass || (type.IsValueType && !type.IsEnum));

    private static bool IsListOfStrings(Type dictionary) =>
        dictionary.IsGenericType && dictionary.GetGenericTypeDefinition() == typeof(Dictionary<,>) && dictionary.GetGenericArguments()[0] == typeof(string)
        && dictionary.GetGenericArguments()[1] == typeof(List<string>);

    private bool TryValue(Type type, object current, out object probe)
    {
        counter++;
        if (type == typeof(bool)) { probe = !(bool)current; return true; }
        if (type == typeof(int)) { probe = (int)current + 3 + counter % 40; return true; }
        if (type == typeof(long)) { probe = (long)current + 3 + counter % 40; return true; }
        // Halves: a share stays within 0..1, and what is stored in tenths (a fish's size) keeps it.
        if (type == typeof(float)) { probe = (float)current + 0.5f; return true; }
        if (type == typeof(double)) { probe = (double)current + 0.5; return true; }
        if (type == typeof(string)) { probe = "probe" + counter; return true; }
        if (type.IsEnum)
        {
            Array values = Enum.GetValues(type);
            probe = values.Length > 1 ? values.GetValue((Array.IndexOf(values, current) + 1) % values.Length) : current;
            return values.Length > 1;
        }

        probe = null;
        return false;
    }

    // Lists and sets of plain values, and dictionaries keyed by them: one entry each.
    private bool TryElement(Type type, out object element, out object value)
    {
        element = value = null;
        if (!type.IsGenericType)
        {
            return false;
        }

        Type[] arguments = type.GetGenericArguments();
        if (!arguments.All(IsPlain))
        {
            return false;
        }

        Type definition = type.GetGenericTypeDefinition();
        if ((definition == typeof(List<>) || definition == typeof(HashSet<>)) && TryValue(arguments[0], Default(arguments[0]), out element))
        {
            return true;
        }

        return definition == typeof(Dictionary<,>) && TryValue(arguments[0], Default(arguments[0]), out element)
            && TryValue(arguments[1], Default(arguments[1]), out value);
    }

    private static bool IsPlain(Type type) => type == typeof(string) || type.IsPrimitive || type.IsEnum;

    private static object Default(Type type) => type == typeof(string) ? "" : IsPlain(type) ? Activator.CreateInstance(type) : null;

    private static void AddTo(object collection, object element, object value)
    {
        if (collection is IDictionary dictionary)
        {
            dictionary[element] = value;
        }
        else
        {
            collection.GetType().GetMethod("Add").Invoke(collection, new[] { element });
        }
    }

    private static bool Holds(object collection, object element, object value)
    {
        if (collection is IDictionary dictionary)
        {
            return dictionary.Contains(element) && Same(dictionary[element], value);
        }

        return collection is IEnumerable items && items.Cast<object>().Any(item => Same(item, element));
    }

    private static bool Same(object a, object b)
    {
        if (a is float x && b is float y)
        {
            return Mathf.Abs(x - y) < 1e-3f;
        }

        if (a is double p && b is double q)
        {
            return Math.Abs(p - q) < 1e-3;
        }

        return Equals(a, b);
    }

    private static object Read(object root, Func<object, object>[] chain)
    {
        object at = root;
        foreach (Func<object, object> step in chain)
        {
            if (at == null)
            {
                return null;
            }

            at = step(at);
        }

        return at;
    }
}
