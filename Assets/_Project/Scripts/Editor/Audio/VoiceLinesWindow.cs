using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Safe Haven > Voice Lines: every line a character says, whether it has a recording, and the buttons that make one.
/// <para>
/// Write a line where it belongs (a story graph, a quest giver, a companion's barks), then here: <b>Speak new lines</b>
/// finds the lines, gives each new one a mood, has LocalSpawn speak them in the speaker's voice and adds the clips to
/// the game (the voice bank). Select a line to hear it, to change how it is said (a mood, a feeling in words, how
/// strongly, another take) and to have it spoken again.
/// </para>
/// The speaking is LocalSpawn's (Tools/Audio/dialogue_voices.py runs its voice generator); this window only starts it
/// and reads its progress. What is set for single lines here is kept in Tools/Audio/Voices/dialogue_direction.json.
/// </summary>
public sealed class VoiceLinesWindow : EditorWindow
{
    private const string Tool = "Tools/Audio/dialogue_voices.py";
    private const string ScriptPath = "Tools/Audio/Voices/voice_script.json";
    private const string DirectionPath = "Tools/Audio/Voices/dialogue_direction.json";
    private const string PythonPref = "SafeHaven.VoiceLines.Python";
    private const string DefaultPython = @"D:\ComfyUI\.venv\Scripts\python.exe";
    private const string Automatic = "Automatic";
    private const float RowHeight = 20f;
    private static readonly string[] Moods = { "Neutral", "Calm", "Glad", "Joyful", "Sad", "Tired", "Stern", "Angry", "Scared", "Whisper", "Yelling" };
    private static readonly string[] MoodChoices = new[] { Automatic }.Concat(Moods).ToArray();
    private static readonly string[] States = { "All lines", "No recording yet", "Recorded" };
    private static readonly Regex Progress = new Regex(@"^\[\s*(\d+)%\]\s*(.*)$");

    [Serializable]
    private sealed class LineList
    {
        public List<DialogueVoices.Found> lines = new List<DialogueVoices.Found>();
    }

    [Serializable]
    private sealed class ScriptRow
    {
        public string id, voice, mood;
    }

    [Serializable]
    private sealed class ScriptList
    {
        public List<ScriptRow> lines = new List<ScriptRow>();
    }

    /// <summary>How one line is to be said, set by hand in this window.</summary>
    [Serializable]
    public sealed class DirectionRow
    {
        public string id, mood, feeling;
        public float strength = 1f;
        public int take = 1;
    }

    [Serializable]
    private sealed class DirectionList
    {
        public List<DirectionRow> lines = new List<DirectionRow>();
    }

    private List<DialogueVoices.Found> lines = new List<DialogueVoices.Found>();
    private readonly List<DialogueVoices.Found> shown = new List<DialogueVoices.Found>();
    private readonly Dictionary<string, ScriptRow> script = new Dictionary<string, ScriptRow>();
    private readonly HashSet<string> voiced = new HashSet<string>();
    private readonly HashSet<string> recorded = new HashSet<string>();
    private readonly HashSet<string> inGame = new HashSet<string>();
    private DirectionList direction = new DirectionList();
    private string[] speakers = { "Everyone" };
    private int speaker, state;
    private string search = "";
    private string selected;
    private Vector2 scroll;
    private string status = "";
    private bool showSettings;

    private Process process;
    private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
    private readonly List<string> log = new List<string>();
    private float progress;
    private string running;
    private bool cancelled;

    [MenuItem("Safe Haven/Voice Lines", false, 41)]
    public static void Open()
    {
        var window = GetWindow<VoiceLinesWindow>("Voice Lines");
        window.minSize = new Vector2(640f, 420f);
        window.Reload();
    }

    private static string Python
    {
        get => EditorPrefs.GetString(PythonPref, DefaultPython);
        set => EditorPrefs.SetString(PythonPref, value);
    }

    private void OnEnable() => Reload();

    private void OnFocus()
    {
        if (process == null)
        {
            ReadRecordings();
        }
    }

    // ----- What there is -----

    private void Reload()
    {
        lines = File.Exists(DialogueVoices.LinesPath) ? Wrapped<LineList>(DialogueVoices.LinesPath).lines : new List<DialogueVoices.Found>();
        script.Clear();
        voiced.Clear();
        if (File.Exists(ScriptPath))
        {
            foreach (ScriptRow row in Wrapped<ScriptList>(ScriptPath).lines)
            {
                script[row.id] = row;
            }
        }

        foreach (DialogueVoices.Found line in lines)
        {
            if (script.ContainsKey(line.id))
            {
                voiced.Add(line.speaker);
            }
        }

        direction = File.Exists(DirectionPath) ? JsonUtility.FromJson<DirectionList>(File.ReadAllText(DirectionPath)) ?? new DirectionList() : new DirectionList();
        speakers = new[] { "Everyone" }.Concat(lines.Select(line => line.speaker).Distinct().OrderBy(name => name)).ToArray();
        speaker = Mathf.Clamp(speaker, 0, speakers.Length - 1);
        ReadRecordings();
    }

    // A file that is a bare list, read as {"lines": [...]} (Unity's reader wants an object).
    private static T Wrapped<T>(string path) where T : new()
    {
        string text = File.ReadAllText(path).Trim();
        return JsonUtility.FromJson<T>(text.StartsWith("[") ? "{\"lines\":" + text + "}" : text) ?? new T();
    }

    private void ReadRecordings()
    {
        recorded.Clear();
        if (Directory.Exists(DialogueVoices.ClipsFolder))
        {
            foreach (string file in Directory.GetFiles(DialogueVoices.ClipsFolder, "*.wav", SearchOption.AllDirectories))
            {
                // The untouched recordings LocalSpawn keeps beside each clip are not the game's.
                if (Path.GetFileName(Path.GetDirectoryName(file)) != "raw")
                {
                    recorded.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
        }

        inGame.Clear();
        var bank = AssetDatabase.LoadAssetAtPath<VoiceBank>(DialogueVoices.BankPath);
        if (bank != null)
        {
            foreach (VoiceLine line in bank.Lines.Concat(bank.HandPlaced))
            {
                if (line != null && line.clip != null)
                {
                    inGame.Add(VoiceBank.Id(line.speaker, line.text));
                }
            }
        }

        Filter();
        Repaint();
    }

    private void Filter()
    {
        shown.Clear();
        string wanted = search.Trim().ToLowerInvariant();
        foreach (DialogueVoices.Found line in lines)
        {
            bool has = recorded.Contains(line.id) || inGame.Contains(line.id);
            if ((speaker > 0 && line.speaker != speakers[speaker]) || (state == 1 && has) || (state == 2 && !has)
                || (wanted.Length > 0 && !line.text.ToLowerInvariant().Contains(wanted) && !line.speaker.ToLowerInvariant().Contains(wanted)))
            {
                continue;
            }

            shown.Add(line);
        }
    }

    private int NewLines => lines.Count(line => voiced.Contains(line.speaker) && !recorded.Contains(line.id) && !inGame.Contains(line.id));

    private DirectionRow DirectionOf(string id, bool create)
    {
        DirectionRow row = direction.lines.FirstOrDefault(r => r.id == id);
        if (row == null && create)
        {
            row = new DirectionRow { id = id, mood = "" };
            direction.lines.Add(row);
        }

        return row;
    }

    private void SaveDirection()
    {
        // A row with nothing set is a line left to the automatic direction.
        direction.lines.RemoveAll(row => string.IsNullOrEmpty(row.mood) && string.IsNullOrWhiteSpace(row.feeling) && row.take <= 1 && Mathf.Approximately(row.strength, 1f));
        File.WriteAllText(DirectionPath, JsonUtility.ToJson(direction, true), new UTF8Encoding(false));
    }

    // ----- The window -----

    private void OnGUI()
    {
        bool busy = process != null;
        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(busy || playing))
            {
                if (GUILayout.Button(new GUIContent("Find lines", "Read every line the characters say from the stories, quest givers, companions and scenes."),
                        EditorStyles.toolbarButton, GUILayout.Width(74f)))
                {
                    FindLines();
                }

                int fresh = NewLines;
                if (GUILayout.Button(new GUIContent($"Speak new lines ({fresh})", "Find the lines, give each new one a mood, have LocalSpawn speak those without a recording, and add them to the game."),
                        EditorStyles.toolbarButton, GUILayout.Width(150f)))
                {
                    FindLines();
                    Run("new", "Speaking new lines");
                }

                if (GUILayout.Button(new GUIContent("Add recordings to the game", "Write the voice bank from the recordings in Audio/Voice/Dialogue. Done for you after speaking."),
                        EditorStyles.toolbarButton, GUILayout.Width(170f)))
                {
                    status = DialogueVoices.Apply();
                    Reload();
                }
            }

            GUILayout.FlexibleSpace();
            showSettings = GUILayout.Toggle(showSettings, "Settings", EditorStyles.toolbarButton, GUILayout.Width(60f));
        }

        if (showSettings)
        {
            Python = EditorGUILayout.TextField(new GUIContent("LocalSpawn's Python", "The Python that LocalSpawn runs with. It speaks the lines."), Python);
            if (!File.Exists(Python))
            {
                EditorGUILayout.HelpBox("That file does not exist: lines cannot be spoken from here.", MessageType.Warning);
            }
        }

        if (playing)
        {
            EditorGUILayout.HelpBox("Leave Play mode to find or speak lines.", MessageType.Info);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            speaker = EditorGUILayout.Popup(speaker, speakers, GUILayout.Width(120f));
            state = EditorGUILayout.Popup(state, States, GUILayout.Width(130f));
            if (EditorGUI.EndChangeCheck())
            {
                Filter();
            }
        }

        DrawList();
        DrawSelected(busy || playing);
        DrawProgress();
    }

    private void DrawList()
    {
        if (lines.Count == 0)
        {
            EditorGUILayout.HelpBox("No lines read yet. Press Find lines.", MessageType.Info);
            GUILayout.FlexibleSpace();
            return;
        }

        EditorGUILayout.LabelField($"{shown.Count} of {lines.Count} lines · {lines.Count(l => inGame.Contains(l.id))} spoken in the game · {NewLines} new", EditorStyles.miniLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        Rect area = GUILayoutUtility.GetRect(0f, shown.Count * RowHeight, GUILayout.ExpandWidth(true));
        int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight) - 1);
        int last = Mathf.Min(shown.Count - 1, first + Mathf.CeilToInt(position.height / RowHeight) + 2);
        for (int i = first; i <= last; i++)
        {
            DialogueVoices.Found line = shown[i];
            var row = new Rect(area.x, area.y + i * RowHeight, area.width, RowHeight);
            if (line.id == selected)
            {
                EditorGUI.DrawRect(row, new Color(0.24f, 0.37f, 0.59f, 0.6f));
            }

            bool has = recorded.Contains(line.id);
            using (new EditorGUI.DisabledScope(!has))
            {
                if (GUI.Button(new Rect(row.x + 2f, row.y + 1f, 22f, RowHeight - 2f), "▶", EditorStyles.miniButton))
                {
                    Play(line);
                }
            }

            string mark = inGame.Contains(line.id) ? "✔" : has ? "●" : voiced.Contains(line.speaker) ? "–" : "";
            GUI.Label(new Rect(row.x + 28f, row.y, 18f, RowHeight), new GUIContent(mark, MarkTip(line, has)));
            GUI.Label(new Rect(row.x + 46f, row.y, 84f, RowHeight), line.speaker, EditorStyles.boldLabel);
            DirectionRow own = DirectionOf(line.id, false);
            string mood = own != null && !string.IsNullOrEmpty(own.mood) ? own.mood + "*" : script.TryGetValue(line.id, out ScriptRow planned) ? planned.mood : "";
            GUI.Label(new Rect(row.x + 132f, row.y, 62f, RowHeight), mood, EditorStyles.miniLabel);
            var words = new Rect(row.x + 196f, row.y, row.width - 200f, RowHeight);
            if (GUI.Button(words, Regex.Replace(line.text, "<[^>]*>", "").Replace("\n", " "), EditorStyles.label))
            {
                selected = line.id;
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private string MarkTip(DialogueVoices.Found line, bool has) =>
        inGame.Contains(line.id) ? "Spoken in the game." :
        has ? "Recorded, not added to the game yet: press Add recordings to the game." :
        voiced.Contains(line.speaker) ? "No recording yet." : "Nobody speaks for this name: text only.";

    private void DrawSelected(bool locked)
    {
        DialogueVoices.Found line = lines.FirstOrDefault(l => l.id == selected);
        if (line == null)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"{line.speaker}  ·  {line.kind}  ·  {line.where}", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(Regex.Replace(line.text, "<[^>]*>", ""), EditorStyles.wordWrappedLabel);
            if (!voiced.Contains(line.speaker))
            {
                EditorGUILayout.HelpBox("Nobody speaks for this name. Give it a voice in the cast at the top of Tools/Audio/dialogue_voices.py (and design the voice in LocalSpawn).", MessageType.Info);
                return;
            }

            DirectionRow row = DirectionOf(line.id, false) ?? new DirectionRow { id = line.id, mood = "" };
            string automatic = script.TryGetValue(line.id, out ScriptRow planned) ? planned.mood : "decided when spoken";
            EditorGUI.BeginChangeCheck();
            int mood = Mathf.Max(0, Array.IndexOf(MoodChoices, string.IsNullOrEmpty(row.mood) ? Automatic : row.mood));
            mood = EditorGUILayout.Popup(new GUIContent("Mood", $"How the line is said. Automatic: {automatic}."), mood, MoodChoices);
            string feeling = EditorGUILayout.TextField(new GUIContent("Feeling", "The feeling in your own words, instead of the mood's: \"bitter, holding back tears\". Plain feeling words work; stage directions do not."), row.feeling ?? "");
            float strength = EditorGUILayout.Slider(new GUIContent("Strength", "How much of the feeling is played. 1 is as tuned. Past 1 the voice starts to drift from the speaker."), row.strength, 0f, 1.5f);
            int take = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Take", "1 is the voice's own reading. 2, 3... are other readings of the same line."), row.take));
            if (EditorGUI.EndChangeCheck())
            {
                row = DirectionOf(line.id, true);
                row.mood = mood == 0 ? "" : MoodChoices[mood];
                row.feeling = feeling;
                row.strength = Mathf.Round(strength * 20f) / 20f;
                row.take = take;
                SaveDirection();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!recorded.Contains(line.id)))
                {
                    if (GUILayout.Button("▶ Play", GUILayout.Width(70f)))
                    {
                        Play(line);
                    }

                    if (GUILayout.Button("Show recording", GUILayout.Width(110f)))
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AudioClip>(DialogueVoices.ClipPath(line)));
                    }
                }

                using (new EditorGUI.DisabledScope(locked))
                {
                    if (GUILayout.Button(new GUIContent(recorded.Contains(line.id) ? "Speak this line again" : "Speak this line", "Have LocalSpawn speak it as set above, and add it to the game."), GUILayout.Width(150f)))
                    {
                        Run("again " + line.id, "Speaking: " + VoiceBank.Plain(line.text));
                    }
                }
            }
        }
    }

    private void DrawProgress()
    {
        if (process != null)
        {
            Rect bar = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.ProgressBar(new Rect(bar.x, bar.y, bar.width - 66f, bar.height), progress, running);
            if (GUI.Button(new Rect(bar.xMax - 62f, bar.y, 62f, bar.height), "Cancel"))
            {
                Cancel();
            }
        }

        if (!string.IsNullOrEmpty(status))
        {
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void Play(DialogueVoices.Found line)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DialogueVoices.ClipPath(line));
        if (clip == null)
        {
            AssetDatabase.ImportAsset(DialogueVoices.ClipPath(line));
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DialogueVoices.ClipPath(line));
        }

        if (clip != null)
        {
            SoundPreview.Play(clip);
        }
    }

    // ----- Doing the work -----

    private void FindLines()
    {
        status = DialogueVoices.Harvest();
        Reload();
    }

    // Runs Tools/Audio/dialogue_voices.py with LocalSpawn's Python, in the background; the recordings are added when it ends.
    private void Run(string arguments, string what)
    {
        if (process != null)
        {
            return;
        }

        if (!File.Exists(Python))
        {
            status = $"LocalSpawn's Python was not found at {Python}. Set it under Settings.";
            return;
        }

        var start = new ProcessStartInfo(Python, $"-u \"{Tool}\" {arguments}")
        {
            WorkingDirectory = Directory.GetCurrentDirectory(), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        log.Clear();
        progress = 0f;
        running = what.Length > 70 ? what.Substring(0, 70) + "…" : what;
        status = "";
        try
        {
            process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception problem)
        {
            process = null;
            status = "Could not start LocalSpawn's Python: " + problem.Message;
            return;
        }

        EditorApplication.update += Pump;
    }

    private void Pump()
    {
        while (output.TryDequeue(out string line))
        {
            Match step = Progress.Match(line);
            if (step.Success)
            {
                progress = int.Parse(step.Groups[1].Value) / 100f;
                running = step.Groups[2].Value;
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                log.Add(line);
            }

            Repaint();
        }

        if (process == null || !process.HasExited)
        {
            return;
        }

        int code = process.ExitCode;
        process.Dispose();
        process = null;
        EditorApplication.update -= Pump;
        string said = log.LastOrDefault(l => l.StartsWith("SPOKEN")) ?? log.LastOrDefault() ?? "";
        string differ = string.Join("\n", log.Where(l => l.StartsWith("WORDS DIFFER")));
        if (cancelled || code != 0)
        {
            status = cancelled ? "Cancelled. Lines already spoken are kept: Add recordings to the game puts them in."
                : "LocalSpawn stopped with an error:\n" + string.Join("\n", log.Skip(Mathf.Max(0, log.Count - 6)));
            cancelled = false;
            AssetDatabase.Refresh();
            ReadRecordings();
            return;
        }

        AssetDatabase.Refresh();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // The voice bank is written from the scenes' lines, which can't be read while the game runs.
            status = said + "\nLeave Play mode and press Add recordings to the game.";
            ReadRecordings();
            return;
        }

        status = said + "\n" + DialogueVoices.Apply() + (differ.Length > 0 ? "\n" + differ : "");
        Reload();
    }

    private void Cancel()
    {
        if (process == null)
        {
            return;
        }

        try
        {
            // The Python is a launcher with the real interpreter and the speech model under it: the whole tree goes.
            Process.Start(new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(5000);
        }
        catch (Exception problem)
        {
            UnityEngine.Debug.LogWarning("Could not stop LocalSpawn: " + problem.Message);
        }

        cancelled = true;
    }

    private void OnDestroy()
    {
        EditorApplication.update -= Pump;
    }
}
