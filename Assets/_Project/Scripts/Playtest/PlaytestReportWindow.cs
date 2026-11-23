using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Friends' playtest reports (the user, 2026-11-21: "have them report bugs and suggestions", sent as zips via Discord). F8, or Report
/// in the Escape menu, takes a screenshot of the moment and opens a short form: Bug, Idea, Confusing or Liked, a few words, and the
/// tester's name (remembered). Saved to Reports beside the saves: the words and where it was (scene, position, quests, raid, minute
/// of the run) as JSON, and the screenshot. "Send my reports" zips the reports with the run logs and Unity's logs, and opens the
/// folder to drop the zip in Discord. Tools/Playtest/analyse.py reads those zips with Claude.
/// <para>Sending it straight to the playtest Discord (the user, 2026-11-23): when the build holds the #playtest-logs webhook
/// (StreamingAssets/playtest-upload.txt, written by AgentScripts/PublishBuild.ps1, never in the project), the first click says what goes
/// and the second sends the zip there. Without it, over Discord's size limit, or if the upload fails, the folder opens as before. Unity's
/// log goes with the PC's user name blanked out (it holds file paths).</para>
/// </summary>
public sealed class PlaytestReportWindow : MonoBehaviour
{
    private const float Width = 920f;
    private const float Height = 560f;
    private const float Margin = 40f;
    private const string NameKey = "SafeHaven.Playtest.ReporterName";
    // Each log file goes in at most this much from its end (Discord takes 10 MB a file without Nitro).
    private const int LogTail = 1_500_000;
    // Discord takes files up to 10 MB on a server without Boosts: a little under, to be safe.
    private const long MostUpload = 9_500_000;
    private const string UploadFile = "playtest-upload.txt";
    private const float ConfirmSeconds = 15f;
    private TMP_Text sendLabel;
    private float confirmUntil = -1f;
    private bool sending;

    private static readonly string[] Kinds = { "Bug", "Idea", "Confusing", "Liked" };

    private static PlaytestReportWindow current;
    private static PlaytestReportKey key;

    private UIWindow window;
    private TMP_InputField words;
    private TMP_InputField reporter;
    private TMP_Text status;
    private readonly List<(Button button, TMP_Text label)> kindButtons = new List<(Button, TMP_Text)>();
    private int kind;
    private byte[] shot;
    private Dictionary<string, string> moment;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        current = null;
        key = null;
    }

    /// <summary>Where reports and the zips made of them are kept.</summary>
    public static string Folder => Path.Combine(Application.persistentDataPath, "Reports");

    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Listens for F8 from now on, in every scene (made once, by the playtest logger).</summary>
    public static void Listen()
    {
        if (key == null)
        {
            var holder = new GameObject("Playtest Report Key");
            DontDestroyOnLoad(holder);
            key = holder.AddComponent<PlaytestReportKey>();
        }
    }

    // F8 anywhere a report makes sense: not while typing elsewhere, nor over a cinematic.
    private sealed class PlaytestReportKey : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && !IsOpen && !GameInput.IsGameplaySuspended && !CinematicScreen.IsPlaying)
            {
                Begin();
            }
        }

        private void OnDestroy()
        {
            if (key == this)
            {
                key = null;
            }
        }
    }

    /// <summary>Takes the screenshot (before any window covers the game) and opens the form.</summary>
    public static void Begin()
    {
        if (key == null)
        {
            Listen();
        }

        key.StartCoroutine(CaptureThenOpen());
    }

    private static IEnumerator CaptureThenOpen()
    {
        yield return new WaitForEndOfFrame();
        byte[] picture = null;
        Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
        if (full != null)
        {
            picture = Shrunk(full, 1600).EncodeToJPG(85);
            Destroy(full);
        }

        Open(picture, Moment());
    }

    // A screenshot as wide as width at most (a 4K one is 10 MB as PNG; this is a few hundred KB).
    private static Texture2D Shrunk(Texture2D source, int width)
    {
        if (source.width <= width)
        {
            return source;
        }

        int height = Mathf.RoundToInt(source.height * (width / (float)source.width));
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var small = new Texture2D(width, height, TextureFormat.RGB24, false);
        small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        small.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return small;
    }

    // Where the player is and what they are doing, read when F8 was pressed.
    private static Dictionary<string, string> Moment()
    {
        var at = new Dictionary<string, string>
        {
            ["scene"] = SceneManager.GetActiveScene().name,
            ["run_id"] = GameSession.Playtest != null ? GameSession.Playtest.Id : "",
            ["played_min"] = GameSession.Playtest != null ? (GameSession.Playtest.PlayedSeconds / 60f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "0",
            ["fps"] = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString(),
            ["raid"] = RaidDirector.Current != null && RaidDirector.Current.Session != null && RaidDirector.Current.Session.Definition != null
                ? RaidDirector.Current.Session.Definition.DisplayName : "",
        };
        if (Player.Current != null)
        {
            Vector3 p = Player.Current.transform.position;
            at["position"] = $"{p.x:0.0};{p.y:0.0};{p.z:0.0}";
        }

        var quests = new List<string>();
        if (GameSession.Quests != null)
        {
            foreach (QuestLog.Entry entry in GameSession.Quests.Entries)
            {
                if (entry.Status == QuestStatus.Active && quests.Count < 4)
                {
                    quests.Add(entry.Quest.DisplayName);
                }
            }
        }

        at["quest"] = string.Join("; ", quests);
        return at;
    }

    private static void Open(byte[] picture, Dictionary<string, string> at)
    {
        if (current == null)
        {
            var root = new GameObject("Playtest Report Window");
            root.SetActive(false);
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<PlaytestReportWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        current.shot = picture;
        current.moment = at;
        current.words.text = "";
        current.reporter.text = PlayerPrefs.GetString(NameKey, "");
        current.status.text = $"Where: {at["scene"]}{(at["raid"].Length > 0 ? ", " + at["raid"] : "")}, {at["played_min"]} min in. A screenshot is taken.";
        current.ShowKind(0);
        current.window.Open();
        current.words.ActivateInputField();
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = CodeUI.Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform panel = CodeUI.Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text kicker = CodeUI.Label("Kicker", panel, Margin, 30f, 500f, 24f, 17f, style.Accent);
        kicker.text = "PLAYTEST REPORT";
        kicker.characterSpacing = 12f;
        TMP_Text title = CodeUI.Label("Title", panel, Margin, 56f, Width - Margin * 2f, 44f, 30f, style.Text);
        title.text = "What happened? What would you change?";
        style.Title(title);

        // Bug, Idea, Confusing, Liked.
        float bx = Margin;
        for (int i = 0; i < Kinds.Length; i++)
        {
            int index = i;
            TMP_Text label = CodeUI.AddButton(Kinds[i], panel, bx, 112f, 190f, UIRole.Kind.Button, () => ShowKind(index), out Button button);
            label.text = Kinds[i];
            kindButtons.Add((button, label));
            bx += 200f;
        }

        words = Field(panel, "Words", Margin, 172f, Width - Margin * 2f, 200f, "What you saw, what you expected, or your idea...", true);
        words.characterLimit = 2000;
        TMP_Text nameLabel = CodeUI.Label("Name Label", panel, Margin, 392f, 140f, 40f, 20f, style.Muted);
        nameLabel.text = "Your name";
        reporter = Field(panel, "Reporter", Margin + 140f, 388f, 300f, 46f, "Who you are", false);
        reporter.characterLimit = 32;

        status = CodeUI.Label("Status", panel, Margin, 446f, Width - Margin * 2f, 30f, 17f, style.Muted);
        sendLabel = CodeUI.AddButton("Send", panel, Margin, Height - 74f, 260f, UIRole.Kind.Button, SendAll, out _);
        sendLabel.text = "Send my reports";
        CodeUI.AddButton("Cancel", panel, Width - Margin - 400f, Height - 74f, 180f, UIRole.Kind.Button, () => window.Close(), out _).text = "Cancel";
        CodeUI.AddButton("Save", panel, Width - Margin - 200f, Height - 74f, 200f, UIRole.Kind.PrimaryButton, Save, out _).text = "Save report";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.PlaytestReport, content.gameObject);
        // Typing: letters are letters (no key acts behind the form); the game waits while the tester writes.
        window.OpenChanged += open =>
        {
            if (open)
            {
                GameInput.SuspendGameplay(this);
                GamePause.Request(this);
            }
            else
            {
                GameInput.ResumeGameplay(this);
                GamePause.Release(this);
            }
        };
    }

    private void OnDestroy()
    {
        if (window != null && window.IsOpen)
        {
            GameInput.ResumeGameplay(this);
            GamePause.Release(this);
        }
    }

    private TMP_InputField Field(RectTransform panel, string name, float x, float y, float width, float height, string hint, bool multiline)
    {
        UIStyle style = UIStyle.Current;
        RectTransform box = CodeUI.At(name, panel, x, y, width, height);
        box.gameObject.AddComponent<Image>();
        box.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        RectTransform area = CodeUI.Stretch("Text Area", box);
        area.offsetMin = new Vector2(14f, 8f);
        area.offsetMax = new Vector2(-14f, -8f);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = CodeUI.Label("Text", area, 0f, 0f, width, height, 21f, style.Text);
        Fill((RectTransform)text.transform);
        TMP_Text placeholder = CodeUI.Label("Hint", area, 0f, 0f, width, height, 21f, style.Muted);
        Fill((RectTransform)placeholder.transform);
        placeholder.text = hint;
        if (multiline)
        {
            text.alignment = TextAlignmentOptions.TopLeft;
            placeholder.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        TMP_InputField field = box.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        field.selectionColor = HearthKit.Faded(style.Accent, 0.45f);
        field.customCaretColor = true;
        field.caretColor = style.AccentBright;
        return field;
    }

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void ShowKind(int index)
    {
        kind = index;
        UIStyle style = UIStyle.Current;
        for (int i = 0; i < kindButtons.Count; i++)
        {
            kindButtons[i].label.color = i == kind ? style.AccentBright : style.Text;
            kindButtons[i].label.fontStyle = i == kind ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    // ----- Saving and sending -----

    [Serializable]
    private sealed class ReportData
    {
        public string id, utc, kind, text, reporter, scene, position, quest, raid, run_id, build;
        public float played_min;
        public int fps;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(words.text))
        {
            status.text = $"<color={UIStyle.Current.DangerHex}>Write a few words first.</color>";
            return;
        }

        PlayerPrefs.SetString(NameKey, reporter.text.Trim());
        PlayerPrefs.Save();
        Directory.CreateDirectory(Folder);
        string id = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        float.TryParse(moment["played_min"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float played);
        var data = new ReportData
        {
            id = id, utc = DateTime.UtcNow.ToString("o"), kind = Kinds[kind].ToLowerInvariant(), text = words.text.Trim(),
            reporter = reporter.text.Trim(), scene = moment["scene"], position = moment.TryGetValue("position", out string p) ? p : "",
            quest = moment["quest"], raid = moment["raid"], run_id = moment["run_id"], build = Application.version, played_min = played,
            fps = int.TryParse(moment["fps"], out int fps) ? fps : 0,
        };
        File.WriteAllText(Path.Combine(Folder, id + ".json"), JsonUtility.ToJson(data, true));
        if (shot != null)
        {
            File.WriteAllBytes(Path.Combine(Folder, id + ".jpg"), shot);
        }

        PlaytestLogger.Note(PlaytestLog.Report, data.kind, 1, id);
        window.Close();
        GameMessages.Post($"Report saved ({Count()} so far). Escape › Report › Send my reports when you're done.", MessageKind.Info);
    }

    private static int Count() => Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Length : 0;

    private void SendAll()
    {
        if (sending)
        {
            return;
        }

        string address = UploadAddress();
        // Sending asks first: what goes, and a second click to send it.
        if (address != null && Time.unscaledTime > confirmUntil)
        {
            confirmUntil = Time.unscaledTime + ConfirmSeconds;
            sendLabel.text = "Yes, send it";
            status.text = "This sends your reports, their screenshots and your play logs (your PC's user name left out) to the Safe Haven " +
                          "playtest Discord. Click again to send.";
            return;
        }

        confirmUntil = -1f;
        sendLabel.text = "Send my reports";
        if (!string.IsNullOrWhiteSpace(words.text))
        {
            Save();
        }

        try
        {
            string name = string.IsNullOrWhiteSpace(reporter.text) ? PlayerPrefs.GetString(NameKey, "") : reporter.text.Trim();
            string zip = MakeZip(name);
            if (address != null && new FileInfo(zip).Length <= MostUpload)
            {
                StartCoroutine(Upload(address, zip, name));
                return;
            }

            if (address != null)
            {
                GameMessages.Post("Your reports are too big to send from the game: drop the zip in #playtest-logs instead.", MessageKind.Warning);
            }

            OpenZipFolder(zip);
        }
        catch (Exception e)
        {
            status.text = $"<color={UIStyle.Current.DangerHex}>Couldn't make the zip: {e.Message}</color>";
            Debug.LogWarning($"Playtest report zip failed: {e}");
        }
    }

    // The folder with the zip opens, its path is copied: the tester drops it in Discord by hand.
    private void OpenZipFolder(string zip)
    {
        Application.OpenURL("file:///" + Path.GetDirectoryName(zip).Replace('\\', '/'));
        GUIUtility.systemCopyBuffer = zip;
        GameMessages.Post($"Your reports and logs are zipped: {Path.GetFileName(zip)}. Drop it in the playtest Discord channel.", MessageKind.Gain);
        window.Close();
    }

    // Sends the zip to #playtest-logs through its webhook; on any failure the folder opens instead, so nothing is lost.
    private IEnumerator Upload(string address, string zip, string name)
    {
        sending = true;
        status.text = "Sending to the playtest Discord.";
        string who = string.IsNullOrWhiteSpace(name) ? "a tester" : name;
        string payload = "{\"content\":\"Reports from **" + Json(who) + "** (build " + Json(Application.version) + ")\",\"allowed_mentions\":{\"parse\":[]}}";
        var form = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("payload_json", payload),
            new MultipartFormFileSection("files[0]", File.ReadAllBytes(zip), Path.GetFileName(zip), "application/zip"),
        };
        using (UnityWebRequest request = UnityWebRequest.Post(address, form))
        {
            request.timeout = 120;
            yield return request.SendWebRequest();
            sending = false;
            if (request.result == UnityWebRequest.Result.Success)
            {
                GameMessages.Post("Your reports are in the playtest Discord. Thank you!", MessageKind.Gain);
                window.Close();
            }
            else
            {
                Debug.LogWarning($"Playtest report upload failed: {request.responseCode} {request.error}");
                GameMessages.Post("Couldn't send them from the game: the zip's folder is open, drop it in #playtest-logs.", MessageKind.Warning);
                OpenZipFolder(zip);
            }
        }
    }

    // The #playtest-logs webhook: in a build, the file PublishBuild.ps1 writes beside the game; in the Editor, the local config
    // (AgentScripts/discord.local.json, "logs"), for testing. Null when there is none: the zip is dropped by hand.
    private static string UploadAddress()
    {
        try
        {
#if UNITY_EDITOR
            string config = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "AgentScripts", "discord.local.json");
            if (!File.Exists(config))
            {
                return null;
            }

            System.Text.RegularExpressions.Match found = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(config), "\"logs\"\\s*:\\s*\"([^\"]+)\"");
            string address = found.Success ? found.Groups[1].Value : null;
#else
            string file = Path.Combine(Application.streamingAssetsPath, UploadFile);
            string address = File.Exists(file) ? File.ReadAllText(file).Trim() : null;
#endif
            return address != null && address.StartsWith("https://discord.com/api/webhooks/") ? address : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Playtest upload address unreadable: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// The zip for Discord: meta.json (who, which build, which PC), reports/ (each report and its screenshot), playtests/ (every run
    /// log) and logs/ (the end of Unity's Player.log and Player-prev.log). Tools/Playtest/analyse.py reads exactly this layout.
    /// </summary>
    public static string MakeZip(string name)
    {
        string outbox = Path.Combine(Folder, "Send");
        Directory.CreateDirectory(outbox);
        string safe = string.IsNullOrWhiteSpace(name) ? "tester" : string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
        string path = Path.Combine(outbox, $"SafeHaven-Reports-{safe}-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        using (FileStream stream = File.Create(path))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var meta = new StringBuilder();
            meta.Append("{\n");
            meta.Append($"  \"player\": \"{Json(name)}\",\n  \"build\": \"{Json(Application.version)}\",\n");
            meta.Append($"  \"os\": \"{Json(SystemInfo.operatingSystem)}\",\n  \"gpu\": \"{Json(SystemInfo.graphicsDeviceName)}\",\n");
            meta.Append($"  \"ram_mb\": {SystemInfo.systemMemorySize},\n  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            meta.Append($"  \"created_utc\": \"{DateTime.UtcNow:o}\"\n}}\n");
            Add(zip, "meta.json", Encoding.UTF8.GetBytes(meta.ToString()));
            if (Directory.Exists(Folder))
            {
                foreach (string file in Directory.GetFiles(Folder))
                {
                    if (file.EndsWith(".json") || file.EndsWith(".jpg"))
                    {
                        Add(zip, "reports/" + Path.GetFileName(file), File.ReadAllBytes(file));
                    }
                }
            }

            if (Directory.Exists(PlaytestLog.Folder))
            {
                foreach (string file in Directory.GetFiles(PlaytestLog.Folder, "*.csv"))
                {
                    Add(zip, "playtests/" + Path.GetFileName(file), Read(file, int.MaxValue));
                }
            }

            foreach (string log in new[] { "Player.log", "Player-prev.log" })
            {
                string file = Path.Combine(Application.persistentDataPath, log);
                if (File.Exists(file))
                {
                    Add(zip, "logs/" + log, WithoutUserName(Read(file, LogTail)));
                }
            }
        }

        return path;
    }

    private static void Add(ZipArchive zip, string entryName, byte[] data)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
        using (Stream s = entry.Open())
        {
            s.Write(data, 0, data.Length);
        }
    }

    // A file being written (the run's log, Unity's own) read without locking it out; its last `most` bytes.
    private static byte[] Read(string file, int most)
    {
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            long start = Math.Max(0, stream.Length - most);
            stream.Seek(start, SeekOrigin.Begin);
            var data = new byte[stream.Length - start];
            int read = 0;
            while (read < data.Length)
            {
                int n = stream.Read(data, read, data.Length - read);
                if (n <= 0)
                {
                    break;
                }

                read += n;
            }

            return data;
        }
    }

    // Unity's log names the PC's user in its file paths: blanked before it leaves the PC.
    private static byte[] WithoutUserName(byte[] log)
    {
        string user = Environment.UserName;
        if (string.IsNullOrEmpty(user) || user.Length < 2)
        {
            return log;
        }

        string text = Encoding.UTF8.GetString(log);
        text = System.Text.RegularExpressions.Regex.Replace(text, System.Text.RegularExpressions.Regex.Escape(user), "<user>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return Encoding.UTF8.GetBytes(text);
    }

    private static string Json(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}
