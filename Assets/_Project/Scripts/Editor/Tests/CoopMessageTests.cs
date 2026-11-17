using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Guard 2 of Docs/BugPatterns.md: co-op messages as types. Every message is a struct (INetworkSerializable) sent whole, so
/// CoopWireTests fingerprints its fields and a field added or dropped raises the wire format. Here every such struct in the game
/// is given a value that isn't its default in every field, written and read back, and must come back whole: a field its
/// NetworkSerialize forgets fails at once (a friend's blow lost its strike kind and how the guard took it that way, when each
/// handler wrote its own fields). And nothing in Multiplayer writes or reads field by field outside a NetworkSerialize, where
/// neither test would see it.
/// </summary>
public sealed class CoopMessageTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Regex FieldByField = new Regex(@"\b(?:Write|Read)ValueSafe\(");
    private static readonly Regex SerializerDeclaration = new Regex(@"\bvoid NetworkSerialize<\w+>\s*\(");

    // Messages the probe can't fill, by type name, and why. None today: a new one goes here only with its reason.
    private static readonly Dictionary<string, string> Skipped = new Dictionary<string, string>();

    private int counter;

    [Test]
    public void EveryMessage_RoundTripsEveryField()
    {
        var problems = new List<string>();
        var tried = new List<string>();
        MethodInfo roundTrip = typeof(CoopMessageTests).GetMethod(nameof(RoundTrip), BindingFlags.Static | BindingFlags.NonPublic);
        foreach (Type type in typeof(CoopSession).Assembly.GetTypes())
        {
            if (!type.IsValueType || type.ContainsGenericParameters || !typeof(INetworkSerializable).IsAssignableFrom(type) || Skipped.ContainsKey(type.Name))
            {
                continue;
            }

            object sent = Probe(type, type.Name, problems);
            object read;
            try
            {
                read = roundTrip.MakeGenericMethod(type).Invoke(null, new[] { sent });
            }
            catch (TargetInvocationException exception)
            {
                problems.Add($"{type.Name} couldn't be written and read back: {exception.InnerException?.Message}");
                continue;
            }

            Compare(type.Name, type, sent, read, problems);
            tried.Add(type.Name);
        }

        Assert.Greater(tried.Count, 30, "the messages were found");
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void NoMessage_IsWrittenFieldByField()
    {
        var found = new List<string>();
        int files = 0;
        int serializers = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "_Project/Scripts/Multiplayer"), "*.cs", SearchOption.AllDirectories))
        {
            files++;
            // Comment lines blanked (the line count kept), so a word about WriteValueSafe isn't a call.
            string source = Regex.Replace(File.ReadAllText(file), @"^[ \t]*//[^\r\n]*", "", RegexOptions.Multiline);
            List<(int start, int end)> bodies = SerializerBodies(source);
            serializers += bodies.Count;
            foreach (Match call in FieldByField.Matches(source))
            {
                if (!bodies.Any(body => call.Index > body.start && call.Index < body.end))
                {
                    found.Add($"{Path.GetFileName(file)}:{1 + source.Take(call.Index).Count(c => c == '\n')}: {call.Value}");
                }
            }
        }

        Assert.Greater(files, 10, "the co-op scripts were found");
        Assert.Greater(serializers, 30, "their messages' NetworkSerialize bodies were found");
        Assert.IsEmpty(found, "written or read field by field outside a NetworkSerialize, where CoopWireTests can't see the fields: put them in a " +
            "message struct (INetworkSerializable) and send it whole (WriteNetworkSerializable, ReadNetworkSerializable):\n" + string.Join("\n", found));
    }

    [Test]
    public void ThePosesHuntingByte_GivesEachFlagItsOwnBit()
    {
        // A creature's pose packs whom it is after and its flags into one byte (CreatureHost.PoseOf): a flag sharing a bit with another,
        // or with the hunt, would read as the other on a friend's copy (a strike's topple as a splintered shield).
        byte[] flags =
        {
            CreatureHost.WaitingFlag, CreatureHost.FelledFlag, CreatureHost.GuardingFlag, CreatureHost.MarkedFlag,
            CreatureHost.ShieldBrokenFlag, CreatureHost.StrikeToppleFlag,
        };
        int seen = CreatureHost.HuntMask;
        foreach (byte flag in flags)
        {
            Assert.AreEqual(1, CountBits(flag), $"0x{flag:X2} is one bit");
            Assert.AreEqual(0, seen & flag, $"0x{flag:X2} is its own");
            seen |= flag;
        }

        Assert.LessOrEqual((int)MonsterAI.RemoteHunt.Friend, CreatureHost.HuntMask, "whom it hunts fits under the mask");
        Assert.AreEqual(MonsterAI.PoiseBlow.Parry, (MonsterAI.PoiseBlow)(byte)MonsterAI.PoiseBlow.Parry, "a poise blow goes as a byte");
        Assert.AreEqual(MonsterAI.PoiseBlow.Crush, (MonsterAI.PoiseBlow)(byte)MonsterAI.PoiseBlow.Crush);
    }

    private static int CountBits(int value)
    {
        int count = 0;
        for (; value != 0; value &= value - 1)
        {
            count++;
        }

        return count;
    }

    // ---- The round trip

    private static object RoundTrip<T>(object value) where T : INetworkSerializable, new()
    {
        var sent = (T)value;
        using var writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(in sent);
        using var reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out T read);
        // A reader that stops short (or reads on) is out of step with its writer, whatever the fields say.
        if (reader.Position != writer.Length)
        {
            throw new InvalidOperationException($"read {reader.Position} of the {writer.Length} bytes written");
        }

        return read;
    }

    // Structs of the game's (messages and what they carry) are filled and compared field by field; anything else as a value.
    private static bool IsComposite(Type type) =>
        type.IsValueType && !type.IsPrimitive && !type.IsEnum
        && (typeof(INetworkSerializable).IsAssignableFrom(type) || type.Assembly == typeof(CoopSession).Assembly);

    // A value that isn't the default: in a struct every field, in an array or list one element.
    private object Probe(Type type, string path, List<string> problems)
    {
        counter++;
        long number = 1 + counter % 100;
        if (type == typeof(bool)) { return true; }
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int)
            || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
        {
            return Convert.ChangeType(number, type);
        }

        // Halves and whole quarters: exact at the half precision some fields go at (AnimatorFrame's weights).
        if (type == typeof(float)) { return 0.5f; }
        if (type == typeof(double)) { return 0.5; }
        if (type == typeof(string)) { return "probe"; }
        if (type == typeof(Vector2)) { return new Vector2(1f, 2f); }
        if (type == typeof(Vector3)) { return new Vector3(1f, 2f, 3f); }
        if (type == typeof(Vector4)) { return new Vector4(1f, 2f, 3f, 4f); }
        if (type == typeof(Quaternion)) { return Quaternion.Euler(10f, 20f, 30f); }
        if (type == typeof(Color)) { return new Color(0.25f, 0.5f, 0.75f, 1f); }
        if (type == typeof(FixedString32Bytes)) { return new FixedString32Bytes("probe"); }
        if (type == typeof(FixedString64Bytes)) { return new FixedString64Bytes("probe"); }
        if (type == typeof(FixedString128Bytes)) { return new FixedString128Bytes("probe"); }
        if (type == typeof(FixedString512Bytes)) { return new FixedString512Bytes("probe"); }
        if (type.IsEnum)
        {
            object none = Activator.CreateInstance(type);
            foreach (object value in Enum.GetValues(type))
            {
                if (!value.Equals(none))
                {
                    return value;
                }
            }
        }
        else if (type.IsArray && type.GetArrayRank() == 1)
        {
            Array array = Array.CreateInstance(type.GetElementType(), 1);
            array.SetValue(Probe(type.GetElementType(), path + "[0]", problems), 0);
            return array;
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(type);
            list.Add(Probe(type.GetGenericArguments()[0], path + "[0]", problems));
            return list;
        }
        else if (IsComposite(type))
        {
            object filled = Activator.CreateInstance(type);
            foreach (FieldInfo field in type.GetFields(Fields))
            {
                field.SetValue(filled, Probe(field.FieldType, path + "." + NameOf(field), problems));
            }

            return filled;
        }

        problems.Add($"{path} ({type.Name}) can't be filled: teach the probe its type, or skip the message in Skipped, saying why.");
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static void Compare(string path, Type type, object sent, object read, List<string> problems)
    {
        if (IsComposite(type))
        {
            foreach (FieldInfo field in type.GetFields(Fields))
            {
                Compare(path + "." + NameOf(field), field.FieldType, field.GetValue(sent), field.GetValue(read), problems);
            }

            return;
        }

        if (sent is IList sentItems && !(sent is string))
        {
            if (!(read is IList readItems) || readItems.Count != sentItems.Count)
            {
                problems.Add($"{path} didn't come back ({sentItems.Count} sent, {(read as IList)?.Count.ToString() ?? "none"} read)");
                return;
            }

            Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            for (int i = 0; i < sentItems.Count; i++)
            {
                Compare($"{path}[{i}]", element, sentItems[i], readItems[i], problems);
            }

            return;
        }

        if (!Equals(sent, read))
        {
            problems.Add($"{path} didn't come back");
        }
    }

    // An auto-property's backing field by the property's name.
    private static string NameOf(FieldInfo field)
    {
        Match property = Regex.Match(field.Name, @"^<(\w+)>k__BackingField$");
        return property.Success ? property.Groups[1].Value : field.Name;
    }

    // ---- The scan

    // Where each NetworkSerialize's body is in the source: a braced block, or "=> ...;".
    private static List<(int start, int end)> SerializerBodies(string source)
    {
        var bodies = new List<(int, int)>();
        foreach (Match declaration in SerializerDeclaration.Matches(source))
        {
            int brace = source.IndexOf('{', declaration.Index);
            int arrow = source.IndexOf("=>", declaration.Index, StringComparison.Ordinal);
            if (arrow >= 0 && (brace < 0 || arrow < brace))
            {
                bodies.Add((arrow, source.IndexOf(';', arrow)));
                continue;
            }

            int depth = 0;
            for (int at = brace; at >= 0 && at < source.Length; at++)
            {
                if (source[at] == '{')
                {
                    depth++;
                }
                else if (source[at] == '}' && --depth == 0)
                {
                    bodies.Add((brace, at));
                    break;
                }
            }
        }

        return bodies;
    }
}
