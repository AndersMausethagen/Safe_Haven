using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Safe Haven > Audio > Sound Editor. One place for the game's audio:
/// <list type="bullet">
/// <item>Music: the game-wide default music and every scene's own Scene Music (exploring, combat, raid final stretch,
/// ambience), with a "what plays where" overview and a live "now playing" strip in Play mode;</item>
/// <item>Sound Effects: every sound asset with preview, settings and where it is used, plus the game-wide fallback effects.
/// Object-specific sounds (a monster's death) are set on that monster's data or prefab, not here;</item>
/// <item>Usage &amp; Cleanup: where any clip or sound is used, and which ones nothing uses.</item>
/// </list>
/// </summary>
public sealed class SoundEditorWindow : EditorWindow
{
    private enum Tab { Music, SoundEffects, Usage }

    private const string SettingsPath = "Assets/_Project/Resources/AudioSettings.asset";
    private const string RaritySettingsPath = "Assets/_Project/Resources/RaritySettings.asset";
    private const string SoundFolder = "Assets/_Project/Audio/Sounds";
    private const string SceneFolder = "Assets/_Project/Scenes";
    private const string GameWide = "";

    private sealed class SceneSummary
    {
        public bool HasMusic;
        public string Exploring = "", Combat = "", FinalStretch = "", Ambience = "";
        public bool CombatMusic = true;
        public float FinalStretchSeconds;
    }

    [SerializeField] private Tab tab;
    [SerializeField] private string selectedScene = GameWide;
    [SerializeField] private string selectedSound = GameWide;
    [SerializeField] private string usageFolder = "Assets/_Project";
    [SerializeField] private Object usageTarget;
    [SerializeField] private bool showAllAudio;

    private readonly Dictionary<string, SceneSummary> unopenedScenes = new Dictionary<string, SceneSummary>();
    private List<string> scenePaths;
    private List<SoundDefinition> soundAssets;
    private Vector2 leftScroll, rightScroll;
    private string soundSearch = "";
    private Editor soundEditor;
    private GUIStyle header, note, rowLabel;

    [MenuItem("Safe Haven/Audio/Sound Editor", false, 0)]
    public static void Open() => Show(null);

    public static void OpenMusic() => Show(Tab.Music);

    private static void Show(Tab? tab)
    {
        var window = GetWindow<SoundEditorWindow>();
        window.titleContent = new GUIContent("Sound Editor", EditorGUIUtility.IconContent("AudioSource Icon").image);
        window.minSize = new Vector2(820f, 520f);
        if (tab.HasValue) window.tab = tab.Value;
        window.Show();
    }

    private void OnEnable() => Reload();

    private void OnProjectChange()
    {
        Reload();
        Repaint();
    }

    private void OnDisable()
    {
        SoundPreview.Stop();
        if (soundEditor != null) DestroyImmediate(soundEditor);
    }

    // Keeps the live panel and preview buttons current.
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying || SoundPreview.Current != null) Repaint();
    }

    private void Reload()
    {
        scenePaths = FindScenes();
        soundAssets = new List<SoundDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:SoundDefinition", new[] { "Assets" }))
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (sound != null) soundAssets.Add(sound);
        }

        soundAssets.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
    }

    private void OnGUI()
    {
        header ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        note ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
        rowLabel ??= new GUIStyle(EditorStyles.label) { richText = true };

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tab = (Tab)GUILayout.Toolbar((int)tab, new[] { "Music", "Sound Effects", "Usage & Cleanup" }, EditorStyles.toolbarButton, GUILayout.Width(360f));
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(SoundPreview.Current == null))
            {
                string playing = SoundPreview.Current != null ? $"■ Stop  {SoundPreview.Current.name}" : "Nothing previewing";
                if (GUILayout.Button(playing, EditorStyles.toolbarButton, GUILayout.MaxWidth(300f))) SoundPreview.Stop();
            }
        }

        if (Application.isPlaying) DrawNowPlaying();

        switch (tab)
        {
            case Tab.Music:
                DrawMusicTab();
                break;
            case Tab.SoundEffects:
                DrawEffectsTab();
                break;
            default:
                DrawUsageTab();
                break;
        }
    }

    // ----- Now playing -----

    private void DrawNowPlaying()
    {
        AudioManager audio = AudioManager.Instance;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (audio == null)
            {
                EditorGUILayout.LabelField("Playing: the audio system has not started yet.", note);
                return;
            }

            MusicTrack track = audio.CurrentMusic;
            AudioSource source = audio.CurrentMusicSource;
            string time = track?.clip != null && source != null
                ? $"{MusicPlaylistGUI.FormatLength(source.time)} / {MusicPlaylistGUI.FormatLength(track.clip.length)}" : "";
            string raid = RaidDirector.Current != null ? $"   •   raid time left {RaidStats.FormatTime(RaidDirector.Current.Session.TimeRemaining)}" : "";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"<b>Now playing</b>  {(track?.clip != null ? track.clip.name : "no music")}  {time}   •   <b>{audio.MusicReason}</b>   •   mood: {audio.Mood}{raid}",
                    rowLabel);
                if (GUILayout.Button("Skip Track", EditorStyles.miniButton, GUILayout.Width(80f))) audio.SkipTrack();
                using (new EditorGUI.DisabledScope(audio.SceneMusic == null))
                {
                    if (GUILayout.Button("Scene Music", EditorStyles.miniButton, GUILayout.Width(85f))) Selection.activeObject = audio.SceneMusic;
                }
            }

            MusicTrack ambience = audio.CurrentAmbience;
            EditorGUILayout.LabelField($"Ambience  {(ambience?.clip != null ? ambience.clip.name : "none")}", note);
        }
    }

    // ----- Music -----

    private void DrawMusicTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(leftScroll, EditorStyles.helpBox, GUILayout.Width(220f)))
            {
                leftScroll = scroll.scrollPosition;
                if (ListButton("Game-wide defaults", selectedScene == GameWide)) Pick(GameWide);
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Scenes", EditorStyles.miniBoldLabel);
                foreach (string path in scenePaths)
                {
                    Scene scene = SceneManager.GetSceneByPath(path);
                    string mark = scene.isLoaded ? (FindSceneAudio(scene) != null ? "●" : "○") : "  ";
                    if (ListButton($"{mark} {Path.GetFileNameWithoutExtension(path)}", selectedScene == path)) Pick(path);
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("● open, has Scene Music\n○ open, plays defaults", note);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(rightScroll))
            {
                rightScroll = scroll.scrollPosition;
                if (selectedScene == GameWide) DrawDefaults();
                else DrawScene(selectedScene);
            }
        }
    }

    private void Pick(string scene)
    {
        selectedScene = scene;
        rightScroll = Vector2.zero;
        GUI.FocusControl(null);
    }

    private void DrawDefaults()
    {
        AudioSettingsDefinition settings = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(SettingsPath);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Audio Settings is missing from " + SettingsPath + ". Safe Haven > Audio > Generate Placeholder Sounds creates it.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Game-wide music", header);
        EditorGUILayout.LabelField("Each scene sets its own music on its Scene Music. This music plays in scenes that set none, so a new scene " +
            "sounds right straight away. Drop audio files on a list to add them.", note);
        EditorGUILayout.Space(4f);

        var data = new SerializedObject(settings);
        data.Update();
        MusicPlaylistGUI.Draw(data.FindProperty("defaultExploring"), "Default Exploring", null, "Scenes without exploring music are silent.");
        MusicPlaylistGUI.Draw(data.FindProperty("defaultCombat"), "Default Combat", null, "Fights keep the exploring music in scenes without combat music.");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("How music behaves", EditorStyles.boldLabel);
        Field(data, "crossfadeSeconds");
        Field(data, "weaponDrawn", "Weapon drawn = combat");
        Field(data, "enemyChasingPlayer", "Enemy chasing = combat");
        Field(data, "recentDamageSeconds", "Recent hit = combat (s)");
        Field(data, "combatLingerSeconds", "Combat music lingers (s)");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Default volumes (players change their own in settings)", EditorStyles.boldLabel);
        Field(data, "masterVolume");
        Field(data, "musicVolume");
        Field(data, "ambienceVolume");
        Field(data, "effectsVolume");
        data.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        DrawOverview(settings);
    }

    private void DrawOverview(AudioSettingsDefinition settings)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("What plays where", header);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Read Unopened Scenes", "Loads each closed scene in the background to show its music. Nothing is changed."),
                GUILayout.Width(160f)))
            {
                ReadUnopenedScenes();
            }
        }

        EditorGUILayout.LabelField("<color=#888888>Grey</color> = falls back to the game-wide default. Click a scene to edit it.", note);
        string defaultExploring = MusicPlaylistGUI.Describe(settings.DefaultExploring);
        string defaultCombat = MusicPlaylistGUI.Describe(settings.DefaultCombat);

        Row("<b>Scene</b>", "<b>Exploring</b>", "<b>Combat</b>", "<b>Raid final stretch</b>", "<b>Ambience</b>", null);
        foreach (string path in scenePaths)
        {
            SceneSummary summary = SummaryOf(path);
            string name = Path.GetFileNameWithoutExtension(path);
            if (summary == null)
            {
                Row(name, "<i>not read yet</i>", "", "", "", path);
                continue;
            }

            string exploring = Or(summary.Exploring, defaultExploring);
            string combat = !summary.CombatMusic ? "<i>off: exploring continues</i>"
                : !string.IsNullOrEmpty(summary.Combat) ? summary.Combat
                : !string.IsNullOrEmpty(defaultCombat) ? $"<color=#888888>{defaultCombat}</color>"
                : "<i>exploring continues</i>";
            string final = string.IsNullOrEmpty(summary.FinalStretch) ? "" : $"{summary.FinalStretch}  (last {summary.FinalStretchSeconds:0}s)";
            Row(summary.HasMusic ? name : name + " <i>(no Scene Music)</i>", exploring, combat, final, summary.Ambience, path);
        }
    }

    private static string Or(string own, string fallback)
    {
        if (!string.IsNullOrEmpty(own)) return own;
        return string.IsNullOrEmpty(fallback) ? "<i>silence</i>" : $"<color=#888888>{fallback}</color>";
    }

    private void Row(string scene, string exploring, string combat, string final, string ambience, string path)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            float width = Mathf.Max(90f, (position.width - 290f) / 4f);
            if (path != null)
            {
                if (GUILayout.Button(new GUIContent(scene, path), rowLabel, GUILayout.Width(140f))) Pick(path);
            }
            else
            {
                EditorGUILayout.LabelField(scene, rowLabel, GUILayout.Width(140f));
            }

            EditorGUILayout.LabelField(exploring, note, GUILayout.Width(width));
            EditorGUILayout.LabelField(combat, note, GUILayout.Width(width));
            EditorGUILayout.LabelField(final, note, GUILayout.Width(width));
            EditorGUILayout.LabelField(ambience, note, GUILayout.Width(width));
        }
    }

    private void DrawScene(string path)
    {
        EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(path), header);
        bool inBuild = System.Array.Exists(EditorBuildSettings.scenes, s => s.path == path && s.enabled);
        EditorGUILayout.LabelField(path + (inBuild ? "" : "   •   not in Build Settings"), note);
        EditorGUILayout.Space(4f);

        Scene scene = SceneManager.GetSceneByPath(path);
        if (!scene.isLoaded)
        {
            SceneSummary summary = SummaryOf(path);
            if (summary != null)
            {
                EditorGUILayout.HelpBox(summary.HasMusic
                    ? $"Exploring: {Fallback(summary.Exploring)}\nCombat: {(summary.CombatMusic ? Fallback(summary.Combat) : "off")}\n" +
                      $"Final stretch: {(string.IsNullOrEmpty(summary.FinalStretch) ? "none" : summary.FinalStretch)}\nAmbience: {Fallback(summary.Ambience, "none")}"
                    : "This scene has no Scene Music, so it plays the game-wide defaults.", MessageType.None);
            }

            EditorGUILayout.HelpBox("The scene is closed. Open it to change its music.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if (GUILayout.Button("Open Alongside", GUILayout.Height(24f))) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (GUILayout.Button("Open (replace current)", GUILayout.Height(24f)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    }

                    if (summary == null && GUILayout.Button("Read Its Music", GUILayout.Height(24f))) unopenedScenes[path] = ReadClosedScene(path);
                }
            }

            return;
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play mode: changes are heard right away but are lost when you stop playing.", MessageType.Warning);
        }

        SceneAudio sceneAudio = FindSceneAudio(scene);
        if (sceneAudio == null)
        {
            EditorGUILayout.HelpBox("This scene has no Scene Music, so it plays the game-wide defaults. Add one to give it its own music.", MessageType.Info);
            if (GUILayout.Button("Add Scene Music", GUILayout.Height(26f)))
            {
                var host = new GameObject("Scene Music");
                SceneManager.MoveGameObjectToScene(host, scene);
                Undo.RegisterCreatedObjectUndo(host, "Add Scene Music");
                Undo.AddComponent<SceneAudio>(host);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
        else
        {
            var data = new SerializedObject(sceneAudio);
            data.Update();
            SceneAudioEditor.DrawSceneMusic(data);
            data.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(sceneAudio == null))
            {
                if (GUILayout.Button("Select in Hierarchy")) EditorGUIUtility.PingObject(Selection.activeObject = sceneAudio);
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying || !scene.isDirty))
            {
                if (GUILayout.Button(scene.isDirty ? "Save Scene" : "Saved")) EditorSceneManager.SaveScene(scene);
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying || SceneManager.sceneCount < 2))
            {
                if (GUILayout.Button("Close Scene") && (!scene.isDirty || EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene })))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }

    private static string Fallback(string value, string none = "game-wide default") => string.IsNullOrEmpty(value) ? none : value;

    private SceneSummary SummaryOf(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        if (scene.isLoaded) return Summarize(FindSceneAudio(scene));
        return unopenedScenes.TryGetValue(path, out SceneSummary summary) ? summary : null;
    }

    private void ReadUnopenedScenes()
    {
        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                if (SceneManager.GetSceneByPath(path).isLoaded) continue;
                if (EditorUtility.DisplayCancelableProgressBar("Reading scene music", path, i / (float)scenePaths.Count)) break;
                unopenedScenes[path] = ReadClosedScene(path);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // A preview scene loads the file without touching the open scenes or the hierarchy; it is read and thrown away.
    private static SceneSummary ReadClosedScene(string path)
    {
        Scene preview = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            return Summarize(FindSceneAudio(preview));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static SceneSummary Summarize(SceneAudio sceneAudio)
    {
        if (sceneAudio == null) return new SceneSummary();
        return new SceneSummary
        {
            HasMusic = true,
            Exploring = MusicPlaylistGUI.Describe(sceneAudio.Exploring),
            Combat = MusicPlaylistGUI.Describe(sceneAudio.Combat),
            FinalStretch = MusicPlaylistGUI.Describe(sceneAudio.FinalStretch),
            Ambience = MusicPlaylistGUI.Describe(sceneAudio.Ambience),
            CombatMusic = sceneAudio.CombatMusic,
            FinalStretchSeconds = sceneAudio.FinalStretchSeconds,
        };
    }

    private static SceneAudio FindSceneAudio(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<SceneAudio>(true);
            if (found != null) return found;
        }

        return null;
    }

    private static List<string> FindScenes()
    {
        var paths = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!string.IsNullOrEmpty(scene.path) && File.Exists(scene.path) && !paths.Contains(scene.path)) paths.Add(scene.path);
        }

        if (AssetDatabase.IsValidFolder(SceneFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path)) paths.Add(path);
            }
        }

        return paths;
    }

    // ----- Sound effects -----

    private void DrawEffectsTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(220f)))
            {
                soundSearch = EditorGUILayout.TextField(soundSearch, EditorStyles.toolbarSearchField);
                using (var scroll = new EditorGUILayout.ScrollViewScope(leftScroll))
                {
                    leftScroll = scroll.scrollPosition;
                    if (ListButton("Game-wide effects", selectedSound == GameWide)) PickSound(GameWide);
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Sounds", EditorStyles.miniBoldLabel);
                    foreach (SoundDefinition sound in soundAssets)
                    {
                        if (sound == null) continue;
                        string name = sound.DisplayName;
                        if (!string.IsNullOrEmpty(soundSearch) && name.IndexOf(soundSearch, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string path = AssetDatabase.GetAssetPath(sound);
                        string warn = sound.HasClips ? "" : "  ⚠";
                        if (ListButton($"{name}{warn}", selectedSound == path)) PickSound(path);
                    }
                }

                if (GUILayout.Button("+ New Sound", GUILayout.Height(24f))) CreateSound();
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(rightScroll))
            {
                rightScroll = scroll.scrollPosition;
                var selected = selectedSound == GameWide ? null : AssetDatabase.LoadAssetAtPath<SoundDefinition>(selectedSound);
                if (selected != null) DrawSound(selected);
                else DrawGameWideEffects();
            }
        }
    }

    private void PickSound(string path)
    {
        selectedSound = path;
        rightScroll = Vector2.zero;
        GUI.FocusControl(null);
    }

    private void CreateSound()
    {
        PrefabBuildUtility.EnsureFolder(SoundFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath(SoundFolder + "/New Sound.asset");
        var sound = CreateInstance<SoundDefinition>();
        AssetDatabase.CreateAsset(sound, path);
        AssetDatabase.SaveAssets();
        Reload();
        PickSound(path);
    }

    private void DrawSound(SoundDefinition sound)
    {
        EditorGUILayout.LabelField(sound.DisplayName, header);
        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(sound), note);
        EditorGUILayout.LabelField("A reusable sound effect: one of its clips is picked at random each time. Hang it on a monster's Death, Hurt or " +
            "Attack Sound, a building's Death Sound, a weapon's or attack's Swing and Hit Sound, a DeathSound or SoundEmitter on a prefab, " +
            "a rarity's pickup sound, a gathering feedback profile, or a game-wide slot.", note);
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!sound.HasClips))
            {
                if (GUILayout.Button("▶  Play a random clip", GUILayout.Height(24f))) SoundPreview.Play(sound);
            }

            if (GUILayout.Button("■  Stop", GUILayout.Height(24f), GUILayout.Width(80f))) SoundPreview.Stop();
            if (GUILayout.Button("Ping", GUILayout.Height(24f), GUILayout.Width(60f))) EditorGUIUtility.PingObject(sound);
        }

        if (!sound.HasClips) EditorGUILayout.HelpBox("No audio clips, so it plays nothing.", MessageType.Warning);
        if (sound.Channel == AudioChannel.Music)
        {
            EditorGUILayout.HelpBox("Music is not a sound asset: it is set as clips on each scene's Scene Music (Music tab). This sound is " +
                "only useful as an effect.", MessageType.Info);
        }
        else if (sound.Channel == AudioChannel.Ambience)
        {
            EditorGUILayout.HelpBox("An ambience loop: used by the rain, wind and stream slots (Game-wide effects › Weather and water). A " +
                "scene's own ambience is set on its Scene Music (Music tab).", MessageType.Info);
        }

        EditorGUILayout.Space(4f);
        Editor.CreateCachedEditor(sound, null, ref soundEditor);
        soundEditor.OnInspectorGUI();

        EditorGUILayout.Space(8f);
        DrawUsers(sound);
    }

    private void DrawGameWideEffects()
    {
        EditorGUILayout.LabelField("Game-wide effects", header);
        EditorGUILayout.LabelField("Sounds the whole game falls back on. A monster, building or prefab with its own sound always wins.", note);
        EditorGUILayout.Space(4f);

        var settings = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(SettingsPath);
        if (settings != null)
        {
            var data = new SerializedObject(settings);
            data.Update();
            SoundField(data.FindProperty("creatureKilled"), "Creature killed (default)");
            SoundField(data.FindProperty("structureDestroyed"), "Structure destroyed (default)");
            Field(data, "maxSimultaneousEffects", "Max effects at once");

            Group("Combat", "Weapons, attacks and monsters can set their own; these play when they don't.");
            foreach ((string field, string label) in new[]
            {
                ("swing", "Swing"), ("heavySwing", "Heavy swing"), ("hit", "Hit (creature)"), ("criticalHit", "Critical / finisher / counter (layered)"),
                ("structureHit", "Hit (structure)"), ("block", "Block"), ("parry", "Parry"), ("guardBreak", "Guard break"), ("stagger", "Stagger"),
                ("perfectDodge", "Perfect dodge"), ("counterReady", "Counter ready"), ("dodge", "Dodge"), ("playerHurt", "Player hurt"),
                ("outOfStamina", "Out of stamina"),
            })
            {
                SoundField(data.FindProperty(field), label);
            }

            Group("Footsteps", "Terrain steps follow the ground layer's name; the first surface whose words match wins.");
            EditorGUILayout.PropertyField(data.FindProperty("footstepSurfaces"), new GUIContent("Surfaces"), true);
            SoundField(data.FindProperty("footstep"), "Anything else");
            SoundField(data.FindProperty("footstepWood"), "Buildings");
            SoundField(data.FindProperty("footstepWater"), "In water");
            SoundField(data.FindProperty("land"), "Landing");
            Field(data, "strideLength", "Stride without animation events (m)");

            Group("World and interface", null);
            foreach ((string field, string label) in new[]
            {
                ("questStarted", "Quest started"), ("questCompleted", "Quest completed"), ("crafted", "Crafted (by hand)"), ("built", "Built"),
                ("consumed", "Eat or drink"), ("click", "Button click"), ("windowOpen", "Window opens"), ("windowClose", "Window closes"),
            })
            {
                SoundField(data.FindProperty(field), label);
            }

            Group("Weather and water", "Loops on the Ambience volume: rain and wind follow the weather, water plays at every stream.");
            foreach ((string field, string label) in new[] { ("rainLoop", "Rain"), ("windLoop", "Wind"), ("thunder", "Thunder"), ("waterLoop", "Streams") })
            {
                SoundField(data.FindProperty(field), label);
            }

            data.ApplyModifiedProperties();
            EditorGUILayout.Space(4f);
            if (GUILayout.Button(new GUIContent("Fill Empty Slots from the Free Sound Library",
                    "Safe Haven › Audio › Set Up Free Sound Library: CC0 sounds in Audio/SFX (see CREDITS.md). Slots already set are kept.")))
            {
                SoundLibrarySetup.SetUp();
                Reload();
            }
        }

        var rarity = AssetDatabase.LoadAssetAtPath<RaritySettingsDefinition>(RaritySettingsPath);
        if (rarity != null)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Loot pickup sounds, by rarity", EditorStyles.boldLabel);
            var data = new SerializedObject(rarity);
            data.Update();
            SerializedProperty styles = data.FindProperty("styles");
            for (int i = 0; i < styles.arraySize; i++)
            {
                SerializedProperty style = styles.GetArrayElementAtIndex(i);
                string label = style.FindPropertyRelative("rarity").enumDisplayNames[style.FindPropertyRelative("rarity").enumValueIndex];
                SoundField(style.FindPropertyRelative("pickupSound"), label);
            }

            data.ApplyModifiedProperties();
        }
    }

    private void Group(string title, string help)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(help))
        {
            EditorGUILayout.LabelField(help, note);
        }
    }

    private void SoundField(SerializedProperty property, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var sound = property.objectReferenceValue as SoundDefinition;
            using (new EditorGUI.DisabledScope(sound == null || !sound.HasClips))
            {
                if (GUILayout.Button("▶", GUILayout.Width(24f))) SoundPreview.Play(sound);
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label));
            using (new EditorGUI.DisabledScope(sound == null))
            {
                if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(40f))) PickSound(AssetDatabase.GetAssetPath(sound));
            }
        }
    }

    // ----- Usage & cleanup -----

    private void DrawUsageTab()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(rightScroll))
        {
            rightScroll = scroll.scrollPosition;
            EditorGUILayout.LabelField("Usage & Cleanup", header);
            EditorGUILayout.LabelField("Finds which scenes, prefabs and data use each audio file and sound. Audio loaded by name from code " +
                "(a Resources folder) can't be seen, so it is never called unused.", note);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(AudioUsageIndex.Ready ? "Scan Again" : "Scan Project", GUILayout.Height(24f), GUILayout.Width(120f)))
                {
                    AudioUsageIndex.Scan();
                }

                EditorGUILayout.LabelField(AudioUsageIndex.Ready ? $"Scanned {AudioUsageIndex.ScannedAt:HH:mm:ss}" : "Not scanned yet.", note);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Where is it used?", EditorStyles.boldLabel);
            usageTarget = EditorGUILayout.ObjectField("Clip or sound", usageTarget, typeof(Object), false);
            if (usageTarget != null && !(usageTarget is AudioClip) && !(usageTarget is SoundDefinition))
            {
                EditorGUILayout.HelpBox("Pick an audio file or a sound asset.", MessageType.Warning);
            }
            else if (usageTarget != null)
            {
                DrawUsers(usageTarget);
            }

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(showAllAudio ? "All audio in" : "Unused audio in", EditorStyles.boldLabel, GUILayout.Width(110f));
                usageFolder = EditorGUILayout.TextField(usageFolder);
                showAllAudio = GUILayout.Toggle(showAllAudio, "Show all", EditorStyles.miniButton, GUILayout.Width(70f));
            }

            if (!AudioUsageIndex.Ready)
            {
                EditorGUILayout.HelpBox("Scan the project to list audio.", MessageType.Info);
                return;
            }

            List<string> list = showAllAudio ? AllAudio(usageFolder) : AudioUsageIndex.Unused(usageFolder);
            if (list.Count == 0) EditorGUILayout.LabelField(showAllAudio ? "No audio in that folder." : "Everything there is used.", note);
            foreach (string path in list) DrawAudioRow(path);
        }
    }

    private static List<string> AllAudio(string folder)
    {
        var list = new List<string>();
        foreach (string path in AudioUsageIndex.Sounds) if (AudioUsageIndex.InFolder(path, folder)) list.Add(path);
        foreach (string path in AudioUsageIndex.Clips) if (AudioUsageIndex.InFolder(path, folder)) list.Add(path);
        return list;
    }

    private void DrawAudioRow(string path)
    {
        Object asset = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset == null) return;
        int uses = AudioUsageIndex.UsersOf(path).Count;
        bool unused = AudioUsageIndex.IsUnused(path);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (asset is AudioClip clip) SoundPreview.Button(clip);
            else if (asset is SoundDefinition sound)
            {
                using (new EditorGUI.DisabledScope(!sound.HasClips))
                {
                    if (GUILayout.Button("▶", GUILayout.Width(26f))) SoundPreview.Play(sound);
                }
            }

            GUIContent icon = EditorGUIUtility.ObjectContent(asset, asset.GetType());
            GUILayout.Label(icon.image, GUILayout.Width(18f), GUILayout.Height(18f));
            EditorGUILayout.LabelField(new GUIContent(asset.name, path), GUILayout.MinWidth(160f));
            string status = unused ? (uses > 0 ? "only in unused sounds" : "unused") : $"{uses} use{(uses == 1 ? "" : "s")}";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel, GUILayout.Width(120f));
            if (GUILayout.Button("Where", EditorStyles.miniButtonLeft, GUILayout.Width(50f))) usageTarget = asset;
            if (GUILayout.Button("Ping", EditorStyles.miniButtonMid, GUILayout.Width(40f))) EditorGUIUtility.PingObject(asset);
            using (new EditorGUI.DisabledScope(!unused))
            {
                if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(50f))
                    && EditorUtility.DisplayDialog("Delete audio", $"Move {path} to the trash? Nothing in the project uses it.", "Delete", "Cancel"))
                {
                    SoundPreview.Stop();
                    if (AssetDatabase.MoveAssetToTrash(path)) AudioUsageIndex.Forget(path);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private void DrawUsers(Object asset)
    {
        if (!AudioUsageIndex.Ready)
        {
            if (GUILayout.Button("Find where it is used")) AudioUsageIndex.Scan();
            return;
        }

        IReadOnlyList<string> users = AudioUsageIndex.UsersOf(asset);
        EditorGUILayout.LabelField(users.Count == 0 ? "Used by: nothing" : $"Used by ({users.Count})", EditorStyles.boldLabel);
        DrawUserList(users, 0);
    }

    private void DrawUserList(IReadOnlyList<string> users, int depth)
    {
        foreach (string user in users)
        {
            System.Type type = AssetDatabase.GetMainAssetTypeAtPath(user);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f + depth * 16f);
                string kind = type == typeof(SceneAsset) ? "Scene" : type == typeof(GameObject) ? "Prefab" : type?.Name ?? "Asset";
                EditorGUILayout.LabelField(new GUIContent($"{Path.GetFileNameWithoutExtension(user)}   <color=#888888>{kind}</color>", user), rowLabel);
                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(user));
                }

                if (type == typeof(SceneAsset) && GUILayout.Button("Music", EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    tab = Tab.Music;
                    Pick(user);
                }
            }

            if (type == typeof(SoundDefinition) && depth < 2)
            {
                DrawUserList(AudioUsageIndex.UsersOf(user), depth + 1);
            }
        }
    }

    // ----- Helpers -----

    private static bool ListButton(string label, bool selected)
    {
        var style = new GUIStyle(EditorStyles.label) { richText = true, padding = new RectOffset(6, 4, 2, 2) };
        if (selected)
        {
            style.normal.background = Texture2D.whiteTexture;
            style.normal.textColor = Color.white;
        }

        Color previous = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(0.24f, 0.48f, 0.9f);
        bool clicked = GUILayout.Button(label, style);
        GUI.backgroundColor = previous;
        return clicked;
    }

    private static void Field(SerializedObject data, string name, string label = null)
    {
        SerializedProperty property = data.FindProperty(name);
        if (property == null) return;
        if (label == null) EditorGUILayout.PropertyField(property);
        else EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
