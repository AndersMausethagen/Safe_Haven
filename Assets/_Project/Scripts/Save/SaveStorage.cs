using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads and writes save files, and nothing else. Each slot is one saved game: a JSON file in the save folder (by default
/// persistentDataPath/Saves). Writes go to a temporary file that then replaces the save, keeping the previous save as a
/// backup, so a crash mid-write never leaves a half-written save; reading falls back to the backup if the save is
/// damaged.
/// </summary>
public sealed class SaveStorage
{
    /// <summary>The one file there was before a game could have several: it is simply the first save in the list.</summary>
    public const string DefaultSlot = "save";
    private const string Extension = ".json";
    private const string BackupExtension = Extension + ".bak";
    private const string SlotPrefix = "journey_";

    public SaveStorage(string directory = null)
    {
        Directory = string.IsNullOrEmpty(directory) ? Path.Combine(Application.persistentDataPath, "Saves") : directory;
    }

    public string Directory { get; }

    public string PathFor(string slot) => Path.Combine(Directory, slot + Extension);

    public bool Exists(string slot) => File.Exists(PathFor(slot)) || File.Exists(BackupPathFor(slot));

    /// <summary>Every save in the folder, by slot. A save that only has its backup left still counts.</summary>
    public List<string> ListSlots()
    {
        var slots = new List<string>();
        if (!System.IO.Directory.Exists(Directory))
        {
            return slots;
        }

        foreach (string file in System.IO.Directory.GetFiles(Directory))
        {
            string name = Path.GetFileName(file);
            string slot = name.EndsWith(BackupExtension, StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - BackupExtension.Length)
                : name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - Extension.Length) : null;
            if (slot != null && !slots.Contains(slot))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>A slot no file uses yet.</summary>
    public string NewSlot(DateTime now)
    {
        string baseName = SlotPrefix + now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string slot = baseName;
        for (int i = 2; Exists(slot); i++)
        {
            slot = $"{baseName}_{i}";
        }

        return slot;
    }

    /// <summary>When the save was last written, for saves that can't say so themselves.</summary>
    public DateTime LastWritten(string slot)
    {
        string path = File.Exists(PathFor(slot)) ? PathFor(slot) : BackupPathFor(slot);
        return File.GetLastWriteTime(path);
    }

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

    private string BackupPathFor(string slot) => Path.Combine(Directory, slot + BackupExtension);

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
