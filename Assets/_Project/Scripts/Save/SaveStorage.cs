using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads and writes save files, and nothing else. Each slot is one JSON file in the save folder (by default
/// persistentDataPath/Saves). Writes go to a temporary file that then replaces the save, keeping the previous save as a
/// backup, so a crash mid-write never leaves a half-written save; reading falls back to the backup if the save is
/// damaged.
/// </summary>
public sealed class SaveStorage
{
    public const string DefaultSlot = "save";
    private const string Extension = ".json";

    public SaveStorage(string directory = null)
    {
        Directory = string.IsNullOrEmpty(directory) ? Path.Combine(Application.persistentDataPath, "Saves") : directory;
    }

    public string Directory { get; }

    public string PathFor(string slot) => Path.Combine(Directory, slot + Extension);

    public bool Exists(string slot) => File.Exists(PathFor(slot)) || File.Exists(BackupPathFor(slot));

    /// <summary>Writes the save. Throws IOException or UnauthorizedAccessException when the disk refuses.</summary>
    public void Write(string slot, SaveData data)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string path = PathFor(slot);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(data, true));

        if (File.Exists(path))
        {
            File.Replace(temporary, path, BackupPathFor(slot));
        }
        else
        {
            File.Move(temporary, path);
        }
    }

    /// <summary>Reads the save, or its backup if the save itself is missing or damaged.</summary>
    public bool TryRead(string slot, out SaveData data, out string error)
    {
        if (TryReadFile(PathFor(slot), out data, out error))
        {
            return true;
        }

        if (TryReadFile(BackupPathFor(slot), out data, out string backupError))
        {
            Debug.LogWarning($"The save '{slot}' couldn't be read ({error}); loaded its backup instead.");
            error = null;
            return true;
        }

        error ??= backupError;
        return false;
    }

    public void Delete(string slot)
    {
        File.Delete(PathFor(slot));
        File.Delete(BackupPathFor(slot));
    }

    private string BackupPathFor(string slot) => PathFor(slot) + ".bak";

    private static bool TryReadFile(string path, out SaveData data, out string error)
    {
        data = null;
        if (!File.Exists(path))
        {
            error = "There is no save.";
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            error = data != null ? null : "The save file is empty.";
            return data != null;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            error = "The save file couldn't be read: " + exception.Message;
            return false;
        }
    }
}
