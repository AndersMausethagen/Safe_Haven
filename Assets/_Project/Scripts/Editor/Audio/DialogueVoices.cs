using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spoken dialogue, the Unity half: finding every line a named character says, and writing the voice bank from the
/// recordings that exist. The Voice Lines window (Safe Haven > Voice Lines) and AgentScripts/BuildDialogueVoices.cs
/// both work through this.
/// <para>
/// <b>Harvest</b> finds the lines (story graphs, the prologue's companions and chapters, quest givers, the portal's
/// whispers, residents' and companions' own lines and barks) and writes them to Tools/Audio/Voices/dialogue_lines.json,
/// each with the id its recording is named by. Lines the game said in Play without a recording
/// (Tools/Audio/Voices/dialogue_wanted.tsv, kept by VoiceLinesWanted) are added.
/// </para>
/// <para>
/// <b>Apply</b> harvests again and writes Resources/VoiceBank.asset from the lines whose recording exists in
/// Audio/Voice/Dialogue/&lt;speaker&gt;/&lt;id&gt;.wav. Between the two, Tools/Audio/dialogue_voices.py gives every line a
/// voice and a mood and LocalSpawn speaks them.
/// </para>
/// The Haven scene is read without being saved, and closed again when it was not open.
/// </summary>
public static class DialogueVoices
{
    public const string LinesPath = "Tools/Audio/Voices/dialogue_lines.json";
    private const string WantedPath = "Tools/Audio/Voices/dialogue_wanted.tsv";
    public const string ClipsFolder = "Assets/_Project/Audio/Voice/Dialogue";
    public const string BankPath = "Assets/_Project/Resources/VoiceBank.asset";
    private static readonly string[] Scenes = { "Assets/_Project/Scenes/Prologue_MistyWoods.unity", "Assets/_Project/Scenes/SafeHaven.unity" };

    // Text on these objects that is not something said aloud: names, ids, what the player answers, notes for the tracker.
    private static readonly HashSet<string> NotSpoken = new HashSet<string>
    {
        "id", "displayName", "description", "speakerName", "question", "ask", "askText", "acceptText", "name", "homeScene",
        "statusText", "endingStatus", "waitingForLeaderText", "interactionName", "prompt", "title", "label", "note", "tooltip",
        "job", "role", "story", "summary", "requirement",
    };

    /// <summary>A line someone says: its recording's name (id), who says it, the words, what kind of line it is and where it was found.</summary>
    [System.Serializable]
    public sealed class Found
    {
        public string id, speaker, text, kind, where;
    }

    private static readonly Dictionary<string, Found> found = new Dictionary<string, Found>();

    public static string Harvest()
    {
        Collect();
        Directory.CreateDirectory(Path.GetDirectoryName(LinesPath));
        File.WriteAllText(LinesPath, ToJson(found.Values.OrderBy(f => f.speaker).ThenBy(f => f.kind).ThenBy(f => f.text)), new UTF8Encoding(false));
        var counts = found.Values.GroupBy(f => f.speaker).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}");
        return $"{found.Count} lines for {found.Values.Select(f => f.speaker).Distinct().Count()} speakers -> {LinesPath}. " + string.Join(", ", counts);
    }

    public static string Apply()
    {
        Collect();
        File.WriteAllText(LinesPath, ToJson(found.Values.OrderBy(f => f.speaker).ThenBy(f => f.kind).ThenBy(f => f.text)), new UTF8Encoding(false));
        Dictionary<string, string> moods = ReadMoods();
        var lines = new List<VoiceLine>();
        int missing = 0;
        foreach (Found line in found.Values.OrderBy(f => f.speaker).ThenBy(f => f.text))
        {
            string path = $"{ClipsFolder}/{Folder(line.speaker)}/{line.id}.wav";
            if (!File.Exists(path))
            {
                missing++;
                continue;
            }

            Import(path);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                moods.TryGetValue(line.id, out string mood);
                lines.Add(new VoiceLine { speaker = line.speaker, text = line.text, clip = clip, mood = mood ?? "" });
            }
        }

        var bank = AssetDatabase.LoadAssetAtPath<VoiceBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<VoiceBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }

        bank.Author(lines);
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssetIfDirty(bank);
        return $"Voice bank: {lines.Count} lines have a recording, {missing} do not yet ({found.Count} found).";
    }

    // ----- Finding the lines -----

    /// <summary>Every line found, in the order they are listed: by speaker, kind and words. Finds them again each time.</summary>
    public static List<Found> Find()
    {
        Collect();
        return found.Values.OrderBy(f => f.speaker).ThenBy(f => f.kind).ThenBy(f => f.text).ToList();
    }

    /// <summary>Where a line's recording is, whether or not it exists yet.</summary>
    public static string ClipPath(Found line) => $"{ClipsFolder}/{Folder(line.speaker)}/{line.id}.wav";

    private static void Collect()
    {
        found.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ResidentDefinition"))
        {
            var resident = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Strings(resident, resident.DisplayName, "resident", resident.name);
        }

        // Small scenes between friends (SmallSceneSettings): each line by whoever says it, in turn.
        SmallSceneSettings small = SmallSceneSettings.Current;
        foreach (SmallSceneSettings.Scene scene in small.Scenes)
        {
            for (int i = 0; scene != null && scene.first != null && scene.second != null && i < scene.lines.Length; i++)
            {
                ResidentDefinition who = i % 2 == 0 ? scene.first : scene.second;
                Add(who.DisplayName, scene.lines[i], "small scene", small.name);
            }
        }

        // A wish for a home met: the resident's thanks, said aloud (HomeWishes).
        foreach (HomeLifeSettings.Liking liking in HomeLifeSettings.Current.Likings)
        {
            if (liking != null && liking.resident != null && !string.IsNullOrWhiteSpace(liking.thanks))
            {
                Add(liking.resident.DisplayName, liking.thanks, "wish met", "HomeLifeSettings");
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:PrologueDefinition"))
        {
            var prologue = AssetDatabase.LoadAssetAtPath<PrologueDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (prologue.Leader != null)
            {
                // The chapter's line, said by whoever leads; the texts for the screen are in NotSpoken.
                Strings(prologue, prologue.Leader.DisplayName, "prologue", prologue.name, only: new[] { "arrivalText", "nextWaveText" });
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null)
            {
                Objects(prefab.GetComponentsInChildren<Component>(true), prefab.name);
            }
        }

        foreach (string path in Scenes)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Objects(root.GetComponentsInChildren<Component>(true), scene.name);
                }
            }
            finally
            {
                if (opened)
                {
                    // Never saved: only read.
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        if (File.Exists(WantedPath))
        {
            foreach (string row in File.ReadAllLines(WantedPath))
            {
                string[] parts = row.Split('\t');
                if (parts.Length == 2)
                {
                    Add(parts[0], parts[1].Replace("\\n", "\n"), "heard in play", "dialogue_wanted");
                }
            }
        }
    }

    private static void Objects(IEnumerable<Component> components, string where)
    {
        foreach (Component component in components)
        {
            switch (component)
            {
                case StoryScript story:
                    foreach (StoryNode node in story.Nodes)
                    {
                        if (node is SayNode say && !say.speaker.player)
                        {
                            Add(say.speaker.ToSpeaker().Name, say.line, say.aside ? "story aside" : "story", where);
                        }
                        else if (node is ChoiceNode choice && !choice.speaker.player)
                        {
                            Add(choice.speaker.ToSpeaker().Name, choice.line, "story question", where);
                        }
                    }

                    break;
                case PrologueCompanion companion:
                    Strings(companion, SpeakerOf(companion), "companion", where);
                    break;
                case QuestGiver giver:
                    string who = SpeakerOf(giver);
                    Strings(giver, who, "quest giver", where);
                    Add(who, "Come back when it's done.", "quest giver", where);
                    break;
                case Talkable talkable:
                    Strings(talkable, talkable.SpeakerName, "follow-up", where, only: new[] { "followUpLine" });
                    break;
                case HomePortal portal:
                    Strings(portal, HomePortal.SpeakerName, "portal whisper", where);
                    break;
                case Companion fighter:
                    var serialized = new SerializedObject(fighter);
                    var definition = serialized.FindProperty("definition").objectReferenceValue;
                    if (definition != null)
                    {
                        // Barks and replies to orders are the definition's, said by whoever fights with it.
                        Strings(definition, fighter.DisplayName, "bark", where);
                    }

                    break;
            }
        }
    }

    // The name the dialogue box shows for someone: their Talkable's, as the code that speaks for them uses.
    private static string SpeakerOf(Component component)
    {
        if (component.TryGetComponent(out Talkable talkable))
        {
            return talkable.SpeakerName;
        }

        string own = new SerializedObject(component).FindProperty("speakerName")?.stringValue;
        return string.IsNullOrWhiteSpace(own) ? component.gameObject.name : own;
    }

    // Every piece of text on an object that reads as something said, by its field's name.
    private static void Strings(Object host, string speaker, string kind, string where, string[] only = null)
    {
        var serialized = new SerializedObject(host);
        SerializedProperty property = serialized.GetIterator();
        bool children = true;
        while (property.Next(children))
        {
            children = property.propertyType != SerializedPropertyType.String;
            if (property.propertyType != SerializedPropertyType.String || property.propertyPath.StartsWith("m_"))
            {
                continue;
            }

            string field = FieldName(property.propertyPath);
            if (only != null ? !only.Contains(field) : NotSpoken.Contains(field))
            {
                continue;
            }

            Add(speaker, property.stringValue, $"{kind}: {field}", where);
        }
    }

    // "campTopics.Array.data[2].answer" -> "answer"; "healLines.Array.data[0]" -> "healLines".
    private static string FieldName(string path)
    {
        string[] parts = path.Replace(".Array.data[", "[").Split('.');
        string last = parts[parts.Length - 1];
        int bracket = last.IndexOf('[');
        return bracket >= 0 ? last.Substring(0, bracket) : last;
    }

    private static void Add(string speaker, string text, string kind, string where)
    {
        if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // A chapter's call can name who makes it ("Runa: More of them!"): the director says it as theirs.
        var called = System.Text.RegularExpressions.Regex.Match(text, @"^([A-Z][a-z]+): (.+)$", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (kind.StartsWith("prologue") && called.Success)
        {
            speaker = called.Groups[1].Value;
            text = called.Groups[2].Value;
        }

        // A key named by its action is spoken as the key the game ships with ("press L"), never as the token.
        text = GameInput.DefaultKeys(text);
        string plain = VoiceBank.Plain(text);
        // Something said has words in it: an id or a single token is a setting that slipped through.
        if (plain.Length < 3 || (!plain.Contains(" ") && !".!?".Contains(plain[plain.Length - 1].ToString())) || text.Contains("{"))
        {
            return;
        }

        string id = VoiceBank.Id(speaker, text);
        if (!found.ContainsKey(id))
        {
            found[id] = new Found { id = id, speaker = speaker.Trim(), text = text.Trim(), kind = kind, where = where };
        }
    }

    // ----- Files -----

    /// <summary>The folder a speaker's recordings are in: their name in lower case, anything but letters and digits an underscore.</summary>
    public static string Folder(string speaker)
    {
        var name = new StringBuilder();
        foreach (char c in speaker.Trim().ToLowerInvariant())
        {
            name.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        }

        return name.ToString().Trim('_');
    }

    private static void Import(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path);
            importer = AssetImporter.GetAtPath(path) as AudioImporter;
        }

        if (importer == null)
        {
            return;
        }

        AudioImporterSampleSettings sample = importer.defaultSampleSettings;
        if (importer.forceToMono && sample.loadType == AudioClipLoadType.CompressedInMemory && sample.compressionFormat == AudioCompressionFormat.Vorbis
            && Mathf.Approximately(sample.quality, 0.7f))
        {
            return;
        }

        // Hundreds of short lines: kept compressed until one is said.
        importer.forceToMono = true;
        sample.loadType = AudioClipLoadType.CompressedInMemory;
        sample.compressionFormat = AudioCompressionFormat.Vorbis;
        sample.quality = 0.7f;
        importer.defaultSampleSettings = sample;
        importer.SaveAndReimport();
    }

    // id -> mood, from the script the voices were spoken from.
    private static Dictionary<string, string> ReadMoods()
    {
        var moods = new Dictionary<string, string>();
        const string script = "Tools/Audio/Voices/voice_script.json";
        if (!File.Exists(script))
        {
            return moods;
        }

        string id = null;
        foreach (string row in File.ReadAllLines(script))
        {
            string line = row.Trim();
            if (line.StartsWith("\"id\":"))
            {
                id = line.Split('"')[3];
            }
            else if (line.StartsWith("\"mood\":") && id != null)
            {
                moods[id] = line.Split('"')[3];
            }
        }

        return moods;
    }

    private static string ToJson(IEnumerable<Found> lines)
    {
        var json = new StringBuilder("[\n");
        bool first = true;
        foreach (Found line in lines)
        {
            json.Append(first ? "" : ",\n");
            first = false;
            json.Append($"  {{\"id\": \"{line.id}\", \"speaker\": {Quote(line.speaker)}, \"text\": {Quote(line.text)}, \"kind\": {Quote(line.kind)}, \"where\": {Quote(line.where)}}}");
        }

        return json.Append("\n]\n").ToString();
    }

    private static string Quote(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': quoted.Append("\\\""); break;
                case '\\': quoted.Append("\\\\"); break;
                case '\n': quoted.Append("\\n"); break;
                case '\r': break;
                case '\t': quoted.Append("\\t"); break;
                default: quoted.Append(c < 32 ? ' ' : c); break;
            }
        }

        return quoted.Append('"').ToString();
    }
}
