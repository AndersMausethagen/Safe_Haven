using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

/// <summary>
/// Sends a host's world (a <see cref="SaveData"/>, see <see cref="HavenVisit.WorldOf"/>) to a guest: as JSON, gzipped
/// (a Haven's 25-150 KB of JSON becomes a few dozen KB), cut into pieces small enough for one network message each
/// (Unity Transport's default payload limit is 6 KB), and put back together on arrival, whatever order the pieces come
/// in. Plain C#: <see cref="CoopSession"/> does the sending.
/// </summary>
public static class WorldTransfer
{
    /// <summary>Bytes of world per message, below the transports' payload limits with room for the header.</summary>
    public const int PieceSize = 4000;

    public static byte[] Pack(SaveData world) => Pack((object)world);

    public static SaveData Unpack(byte[] packed) => Unpack<SaveData>(packed);

    /// <summary>Any [Serializable] message as gzipped JSON (the world, a batch of changed objects, the stash).</summary>
    public static byte[] Pack(object message)
    {
        byte[] json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
        using var packed = new MemoryStream();
        using (var zip = new GZipStream(packed, System.IO.Compression.CompressionLevel.Optimal, true))
        {
            zip.Write(json, 0, json.Length);
        }

        return packed.ToArray();
    }

    public static T Unpack<T>(byte[] packed)
    {
        using var source = new MemoryStream(packed);
        using var zip = new GZipStream(source, CompressionMode.Decompress);
        using var json = new MemoryStream();
        zip.CopyTo(json);
        return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(json.ToArray()));
    }

    /// <summary>The packed world in pieces of at most <see cref="PieceSize"/> bytes, in order.</summary>
    public static List<byte[]> Split(byte[] packed, int pieceSize = PieceSize)
    {
        var pieces = new List<byte[]>();
        for (int at = 0; at < packed.Length; at += pieceSize)
        {
            int length = Math.Min(pieceSize, packed.Length - at);
            var piece = new byte[length];
            Buffer.BlockCopy(packed, at, piece, 0, length);
            pieces.Add(piece);
        }

        if (pieces.Count == 0)
        {
            pieces.Add(Array.Empty<byte>());
        }

        return pieces;
    }

    /// <summary>Collects the pieces of one transfer; a new transfer id starts over.</summary>
    public sealed class Receiver
    {
        private byte[][] pieces;
        private int received;
        private int transfer = -1;

        /// <summary>Takes a piece; returns the whole packed world once every piece has come, else null.</summary>
        public byte[] Add(int transferId, int index, int count, byte[] piece)
        {
            if (count <= 0 || index < 0 || index >= count)
            {
                return null;
            }

            if (transferId != transfer || pieces == null || pieces.Length != count)
            {
                transfer = transferId;
                pieces = new byte[count][];
                received = 0;
            }

            if (pieces[index] == null)
            {
                pieces[index] = piece;
                received++;
            }

            if (received < count)
            {
                return null;
            }

            using var whole = new MemoryStream();
            foreach (byte[] part in pieces)
            {
                whole.Write(part, 0, part.Length);
            }

            pieces = null;
            return whole.ToArray();
        }
    }
}
