using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>One spoken line: who says it, the words as they are written in the game, and its recording.</summary>
[Serializable]
public sealed class VoiceLine
{
    [Tooltip("The speaker's name as the dialogue box shows it.")]
    public string speaker;
    [TextArea(1, 4)] public string text;
    [Tooltip("The recording of this line. Any clip can be put here: a generated one, or an actor's.")]
    public AudioClip clip;
    [Tooltip("How it was asked to be read, for whoever looks at the list. Not used by the game.")]
    public string mood;
}

/// <summary>
/// The game's spoken dialogue: a recording for a line, found by who says it and what they say. The dialogue box asks
/// for one whenever it shows a line, whichever system the line comes from (a story graph, a quest giver, a companion's
/// bark), so no dialogue data had to change to be voiced, and a builder that rewrites a story keeps its voices as long
/// as the words stay the same. A line without a recording is only read, as before.
/// <para>
/// The list is written by AgentScripts/BuildDialogueVoices.cs from the clips in Audio/Voice/Dialogue (made by
/// Tools/Audio/dialogue_voices.py and LocalSpawn's Voice tab). A recording put on a line by hand under Hand Placed is
/// kept when the list is rebuilt, and wins over a generated one.
/// </para>
/// </summary>
public sealed class VoiceBank : GameSettings<VoiceBank>
{
    private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
    // "(G)", "[Left Shift]", and the same after a rebind, which GameInput.WithRebinds marks with \u0001.
    private static readonly Regex KeyHints = new Regex(@"[\(\[][^\(\)\[\]]{1,14}[\)\]]", RegexOptions.Compiled);

    [Tooltip("Written by the builder: every line it found a recording for.")]
    [SerializeField] private List<VoiceLine> lines = new List<VoiceLine>();
    [Tooltip("Lines given a recording by hand (an actor's take, a replacement). Kept when the list above is rebuilt, and used first.")]
    [SerializeField] private List<VoiceLine> handPlaced = new List<VoiceLine>();
    [Tooltip("Seconds a subtitle stays after its recording ends.")]
    [SerializeField, Min(0f)] private float subtitleTail = 0.6f;

    private Dictionary<string, AudioClip> byKey;
    private HashSet<string> speakers;

    public IReadOnlyList<VoiceLine> Lines => lines;
    public IReadOnlyList<VoiceLine> HandPlaced => handPlaced;
    public float SubtitleTail => subtitleTail;

    /// <summary>The words of a line as they are compared: without rich-text tags, key hints, case or extra spaces.
    /// A key hint ("(G)", "[E]") is left out because the player can rebind it, and the line is still the same line. A key
    /// named by its action ("({key:Build})", "press {key:ToggleTorch}") counts as the key the game ships with, as it was
    /// spoken: so the hint is left out, and "press L" is still "press L" after the torch is rebound.</summary>
    public static string Plain(string text) =>
        Spaces.Replace(KeyHints.Replace(GameInput.DefaultKeys(Tags.Replace(text ?? "", "")), " "), " ").Trim().ToLowerInvariant();

    /// <summary>What a line is found by: the speaker and the plain words.</summary>
    public static string Key(string speaker, string text) => (speaker ?? "").Trim().ToLowerInvariant() + "|" + Plain(text);

    /// <summary>A short name for a line that stays the same as long as its speaker and words do: its recording's file name.</summary>
    public static string Id(string speaker, string text)
    {
        using (SHA1 sha = SHA1.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Key(speaker, text)));
            var id = new StringBuilder(12);
            for (int i = 0; i < 6; i++)
            {
                id.Append(hash[i].ToString("x2"));
            }

            return id.ToString();
        }
    }

    /// <summary>The recording of a line, or null when it has none.</summary>
    public AudioClip Find(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (byKey == null)
        {
            byKey = new Dictionary<string, AudioClip>();
            // Generated first, hand-placed after, so a hand-placed recording replaces a generated one.
            foreach (List<VoiceLine> list in new[] { lines, handPlaced })
            {
                foreach (VoiceLine line in list)
                {
                    if (line != null && line.clip != null)
                    {
                        byKey[Key(line.speaker, line.text)] = line.clip;
                    }
                }
            }
        }

        return byKey.TryGetValue(Key(speaker, text), out AudioClip clip) ? clip : null;
    }

    /// <summary>True when anything this speaker says has a recording: someone the game has a voice for.</summary>
    public bool SpeaksFor(string speaker)
    {
        if (speakers == null)
        {
            speakers = new HashSet<string>();
            foreach (List<VoiceLine> list in new[] { lines, handPlaced })
            {
                foreach (VoiceLine line in list)
                {
                    if (line != null && line.clip != null)
                    {
                        speakers.Add((line.speaker ?? "").Trim().ToLowerInvariant());
                    }
                }
            }
        }

        return speakers.Contains((speaker ?? "").Trim().ToLowerInvariant());
    }

    private void OnValidate()
    {
        byKey = null;
        speakers = null;
    }

#if UNITY_EDITOR
    /// <summary>Replaces the generated list. For the editor script that writes this asset.</summary>
    public void Author(List<VoiceLine> generated)
    {
        lines = generated ?? new List<VoiceLine>();
        byKey = null;
        speakers = null;
    }
#endif
}
