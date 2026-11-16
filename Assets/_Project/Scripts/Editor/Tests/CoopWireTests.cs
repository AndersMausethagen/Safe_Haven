using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;

/// <summary>
/// The co-op wire format (MultiplayerSettings.WireFormat) is raised whenever a message changes, so builds that would misread
/// each other's messages are turned away instead. A fingerprint of every message (its name, the fields of what is sent as a
/// whole, and the signatures that write and read them) is kept here by format: change a message, and this fails until the
/// format is raised and its new fingerprint put below. Messages written field by field inside a handler aren't seen: keep
/// their writing in a Write/Read pair (CoopLands, CoopFishing...).
/// </summary>
public sealed class CoopWireTests
{
    // Format -> fingerprint. Add a line when raising the format; never change an old one.
    private static readonly Dictionary<int, string> Fingerprints = new Dictionary<int, string>
    {
        [21] = "c8d2be2d283c800d221e1ecebbb69670",
        // 22 (horses, CoopHorses) went out without one; 23 adds the Haven's decisions (CoopDecisions).
        [23] = "54295e6eb7f1a2795b8bbf713d654006",
        // 2026-11-15: creature poses carry poise, waiting, felled, guarding, marked and noticing (22 bytes); look state carries Drawing;
        // friends' noise, shots and casts as RPCs.
        [24] = "b11aa4e41e43882d9564f37168585240",
        [25] = "fbdd95d903325dec171610c2ae9adaba",
        // 2026-11-16: raid news, pings and "helping up" carry the sender's client id (written inside their handlers, so the
        // fingerprint is the same).
        [26] = "fbdd95d903325dec171610c2ae9adaba",
    };

    /// <summary>The fingerprint of the messages as they are now.</summary>
    public static string Fingerprint()
    {
        Assembly game = typeof(CoopSession).Assembly;
        var parts = new List<string>();
        foreach (Type type in game.GetTypes())
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsLiteral && field.FieldType == typeof(string) && field.GetRawConstantValue() is string name && name.StartsWith("SafeHaven.", StringComparison.Ordinal))
                {
                    parts.Add("message " + name);
                }
            }

            if (typeof(INetworkSerializable).IsAssignableFrom(type) && type.IsValueType)
            {
                parts.Add("sent whole " + type.Name + ": " + string.Join(", ", type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.FieldType.Name + " " + f.Name)));
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if ((method.Name.StartsWith("Write", StringComparison.Ordinal) || method.Name.StartsWith("Read", StringComparison.Ordinal))
                    && parameters.Any(p => p.ParameterType == typeof(FastBufferWriter) || p.ParameterType == typeof(FastBufferReader)))
                {
                    parts.Add($"{type.Name}.{method.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})");
                }
            }
        }

        parts.Sort(StringComparer.Ordinal);
        using var md5 = System.Security.Cryptography.MD5.Create();
        return string.Concat(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", parts))).Select(b => b.ToString("x2")));
    }

    [Test]
    public void AChangedMessage_RaisesTheWireFormat()
    {
        string now = Fingerprint();
        int format = MultiplayerSettings.WireFormat;
        Assert.IsTrue(Fingerprints.TryGetValue(format, out string known),
            $"Wire format {format} has no fingerprint yet: add [{format}] = \"{now}\" to CoopWireTests.Fingerprints.");
        Assert.AreEqual(known, now, $"A co-op message changed since format {format}: raise MultiplayerSettings.WireFormat to {format + 1} " +
            $"and add [{format + 1}] = \"{now}\" to CoopWireTests.Fingerprints, so older builds are turned away.");
    }
}
