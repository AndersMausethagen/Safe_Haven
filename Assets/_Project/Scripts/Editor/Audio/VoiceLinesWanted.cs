using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

/// <summary>
/// Keeps a list of the lines the game said in Play that have no recording yet (Tools/Audio/Voices/dialogue_wanted.tsv:
/// speaker, tab, words). Most lines are found by reading the game's data (AgentScripts/BuildDialogueVoices.cs); this
/// catches the ones put together while the game runs, or kept somewhere that script doesn't look. The next harvest
/// takes them in, so they are voiced with the rest.
/// </summary>
[InitializeOnLoad]
public static class VoiceLinesWanted
{
    private const string Path = "Tools/Audio/Voices/dialogue_wanted.tsv";
    private static HashSet<string> known;

    static VoiceLinesWanted()
    {
        DialogueBox.Unvoiced -= Note;
        DialogueBox.Unvoiced += Note;
    }

    private static void Note(string speaker, string line)
    {
        // Only someone the game has a voice for: the player's own lines, a notice board and a stranger's name are read,
        // not spoken, and would only fill the list.
        if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(line) || !VoiceBank.Current.SpeaksFor(speaker))
        {
            return;
        }

        if (known == null)
        {
            known = new HashSet<string>(File.Exists(Path) ? File.ReadAllLines(Path) : new string[0]);
        }

        // One row a line: a line break in the words is written as \n, which the harvest turns back.
        string row = speaker.Trim() + "\t" + line.Trim().Replace("\r", "").Replace("\n", "\\n");
        if (known.Add(row))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.AppendAllText(Path, row + "\n", new UTF8Encoding(false));
        }
    }
}
